import { useEffect, useMemo, useState } from 'react'
import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { motion, AnimatePresence } from 'framer-motion'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  Search, ChevronDown, ChevronRight, ChevronLeft, Calendar, Users, Clock,
  CheckCircle2, XCircle, AlertCircle, Eye, Briefcase,
  Bell, RefreshCw, Plus, Filter, X, CalendarDays, UserX,
  KeyRound, Copy, Check
} from 'lucide-react'
import { PageHeader, EmptyState, ErrorAlert } from '@ari/shared/ui'
import { applicationService } from '@ari/shared/fservices/application'
import {
  interviewService,
  type InterviewJobSummary,
  type InterviewSlotDetail,
  type SlotCandidate,
} from '@ari/shared/fservices/interview'

// ── helpers ──────────────────────────────────────────────────────────────────
const fmtDate = (iso: string) =>
  new Date(iso).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })

const fmtTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })

const fmtDur = (s?: number | null) => {
  if (!s || s <= 0) return null
  const m = Math.floor(s / 60); const sec = s % 60
  return sec === 0 ? `${m}m` : `${m}m ${sec}s`
}

const initials = (name: string) =>
  name.trim().split(/\s+/).slice(-2).map(n => n[0]).join('').toUpperCase()

const isSameDay = (iso: string, targetDateStr: string) => {
  if (!targetDateStr) return true
  const d = new Date(iso)
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}` === targetDateStr
}

// ── Modal: Dời lịch phỏng vấn cho (1 hoặc nhiều) ứng viên ──────────────────────
function RescheduleModal({
  candidates,
  availableSlots,
  currentSlotId,
  currentRoundNumber,
  onClose,
  onSuccess,
}: {
  candidates: SlotCandidate[]
  availableSlots: InterviewSlotDetail[]
  currentSlotId: string
  currentRoundNumber?: number
  onClose: () => void
  onSuccess: () => void
}) {
  const [selectedSlotId, setSelectedSlotId] = useState<string>('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const validSlots = useMemo(() => {
    return availableSlots.filter(s => {
      if (s.slotId === currentSlotId || s.isPast) return false
      if (currentRoundNumber != null && s.roundNumber !== currentRoundNumber) return false
      return (s.capacity - s.bookedCount) >= candidates.length
    })
  }, [availableSlots, currentSlotId, currentRoundNumber, candidates.length])

  const handleConfirm = async () => {
    if (!selectedSlotId || candidates.length === 0) return
    setSubmitting(true)
    setError(null)
    try {
      let successCount = 0
      let failCount = 0
      for (const cand of candidates) {
        try {
          const res = await interviewService.rescheduleBooking(cand.bookingId, selectedSlotId)
          if (res.success) successCount++
          else failCount++
        } catch {
          failCount++
        }
      }

      if (successCount > 0) {
        onSuccess()
        onClose()
      } else {
        setError('Dời lịch thất bại. Vui lòng kiểm tra lại số chỗ khả dụng.')
      }
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Có lỗi xảy ra khi dời lịch.')
    } finally {
      setSubmitting(false)
    }
  }


  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-ink-950/60 backdrop-blur-sm">
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        className="w-full max-w-md bg-white dark:bg-ink-900 border border-ink-200 dark:border-white/10 rounded-2xl shadow-2xl overflow-hidden p-6"
      >
        <div className="flex items-center justify-between pb-4 border-b border-ink-100 dark:border-white/10 mb-4">
          <div className="flex items-center gap-2">
            <RefreshCw className="w-5 h-5 text-brand-500" />
            <h3 className="text-base font-bold text-ink-900 dark:text-white">
              Dời Lịch Phỏng Vấn {candidates.length > 1 ? `(${candidates.length} ứng viên)` : ''}
            </h3>
          </div>
          <button onClick={onClose} className="p-1 text-ink-400 hover:text-ink-600 rounded-lg">
            <X className="w-5 h-5" />
          </button>
        </div>

        {error && <ErrorAlert message={error} onDismiss={() => setError(null)} />}

        <div className="mb-4">
          <p className="text-xs text-ink-400 uppercase tracking-wider font-semibold mb-1">
            Danh sách ứng viên ({candidates.length})
          </p>
          <div className="max-h-24 overflow-y-auto space-y-1 pr-1">
            {candidates.map(c => (
              <div key={c.bookingId} className="flex items-center justify-between text-xs py-1 border-b border-ink-100 dark:border-white/5">
                <span className="font-semibold text-ink-900 dark:text-white truncate">{c.candidateName}</span>
                <span className="text-ink-400 truncate">{c.candidateEmail}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="mb-6">
          <label className="block text-xs font-semibold text-ink-700 dark:text-ink-200 uppercase tracking-wider mb-2">
            Chọn Ca Phỏng Vấn Mới (Còn tối thiểu {candidates.length} chỗ)
          </label>
          {validSlots.length === 0 ? (
            <p className="text-xs text-amber-600 dark:text-amber-400 bg-amber-50 dark:bg-amber-500/10 p-3 rounded-xl border border-amber-200 dark:border-amber-500/20">
              Không có ca phỏng vấn mới nào đủ {candidates.length} chỗ trống. Hãy tạo thêm ca mới trước khi dời lịch.
            </p>
          ) : (
            <div className="space-y-2 max-h-56 overflow-y-auto pr-1">
              {validSlots.map(s => (
                <label
                  key={s.slotId}
                  className={`flex items-center justify-between p-3 rounded-xl border cursor-pointer transition-all ${
                    selectedSlotId === s.slotId
                      ? 'border-brand-500 bg-brand-50/50 dark:bg-brand-500/10 text-brand-900 dark:text-white'
                      : 'border-ink-200 dark:border-white/10 hover:bg-ink-50 dark:hover:bg-white/5'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <input
                      type="radio"
                      name="reschedule_slot"
                      value={s.slotId}
                      checked={selectedSlotId === s.slotId}
                      onChange={() => setSelectedSlotId(s.slotId)}
                      className="text-brand-600 focus:ring-brand-500"
                    />
                    <div>
                      <p className="text-xs font-bold text-ink-900 dark:text-white">
                        Vòng {s.roundNumber} · {fmtDate(s.startTime)}
                      </p>
                      <p className="text-xs text-ink-500">
                        {fmtTime(s.startTime)} – {fmtTime(s.endTime)}
                      </p>
                    </div>
                  </div>
                  <span className="text-[11px] font-medium text-ink-400">
                    Trống {s.capacity - s.bookedCount}/{s.capacity}
                  </span>
                </label>
              ))}
            </div>
          )}
        </div>

        <div className="flex items-center justify-end gap-3 pt-4 border-t border-ink-100 dark:border-white/10">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-medium text-ink-600 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 rounded-xl"
          >
            Hủy
          </button>
          <button
            disabled={!selectedSlotId || submitting}
            onClick={handleConfirm}
            className="px-4 py-2 text-xs font-semibold text-white bg-brand-600 hover:bg-brand-700 disabled:opacity-50 rounded-xl transition-colors"
          >
            {submitting ? 'Đang dời...' : `Xác nhận dời ${candidates.length} ứng viên`}
          </button>
        </div>
      </motion.div>
    </div>
  )
}

interface IssuedCode {
  code: string
  expiresAt: string
}

// ── sub-component: Candidate row inside a slot ────────────────────────────────
function CandidateRow({
  c,
  navigate,
  availableSlots,
  currentSlotId,
  currentRoundNumber,
  isSelected,
  onToggleSelect,
  onReloadSlot,
  issuedCodes,
  onIssueCode,
}: {
  c: SlotCandidate
  navigate: ReturnType<typeof useNavigate>
  availableSlots: InterviewSlotDetail[]
  currentSlotId: string
  currentRoundNumber: number
  isSelected: boolean
  onToggleSelect: () => void
  onReloadSlot: () => void
  issuedCodes: Record<string, IssuedCode>
  onIssueCode: (appId: string) => Promise<void>
}) {
  const [sendingReminder, setSendingReminder] = useState(false)
  const [rejecting, setRejecting] = useState(false)
  const [generatingCode, setGeneratingCode] = useState(false)
  const [copied, setCopied] = useState(false)
  const [reminderToast, setReminderToast] = useState<string | null>(null)
  const [showReschedule, setShowReschedule] = useState(false)

  const codeData = issuedCodes[c.applicationId]
  const displayCode = c.interviewCode || codeData?.code

  const handleGenCode = async () => {
    setGeneratingCode(true)
    try {
      await onIssueCode(c.applicationId)
      onReloadSlot()
    } finally {
      setGeneratingCode(false)
    }
  }

  const handleCopyCode = async (code: string) => {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {}
  }

  const handleRemind = async () => {
    setSendingReminder(true)
    try {
      const res = await interviewService.sendBookingReminder(c.bookingId)
      setReminderToast(res.message || 'Đã gửi email nhắc lịch thành công!')
      setTimeout(() => setReminderToast(null), 3000)
    } catch {
      setReminderToast('Lỗi khi gửi email nhắc lịch.')
      setTimeout(() => setReminderToast(null), 3000)
    } finally {
      setSendingReminder(false)
    }
  }

  const handleReject = async () => {
    if (!window.confirm(`Bạn có chắc chắn muốn loại ứng viên "${c.candidateName}" khỏi quy trình tuyển dụng không?`)) return
    setRejecting(true)
    try {
      await applicationService.rejectApplication(c.applicationId)
      setReminderToast('Đã loại ứng viên thành công.')
      onReloadSlot()
    } catch {
      setReminderToast('Không thể loại ứng viên.')
      setTimeout(() => setReminderToast(null), 3000)
    } finally {
      setRejecting(false)
    }
  }

  const isDeclined = c.confirmationStatus === 'declined'
  const isCancelled = c.bookingStatus === 'cancelled' || c.declineReason?.includes('loại') || (c.confirmationStatus === 'declined' && c.declineReason?.includes('quy trình tuyển dụng'))
  const isInactive = isDeclined || isCancelled

  const confirmCls = isCancelled
    ? 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
    : isDeclined
    ? 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
    : ({
        confirmed: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
        pending: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
      }[c.confirmationStatus] ?? 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300')

  const confirmLabel = isCancelled
    ? 'Bị loại'
    : isDeclined
    ? 'Từ chối (báo bận)'
    : ({ confirmed: 'Đã xác nhận', pending: 'Chờ xác nhận' }[c.confirmationStatus] ?? c.confirmationStatus)

  const verdictCls = c.verdict?.toLowerCase() === 'pass'
    ? 'bg-emerald-50 text-emerald-600 border border-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-400 dark:border-emerald-500/20'
    : 'bg-red-50 text-red-600 border border-red-200 dark:bg-red-500/10 dark:text-red-400 dark:border-red-500/20'

  return (
    <div className={`py-3 px-4 rounded-xl transition-colors space-y-2 ${isSelected ? 'bg-brand-50/60 dark:bg-brand-500/10' : 'hover:bg-ink-50 dark:hover:bg-white/5'}`}>
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <input
            type="checkbox"
            disabled={isCancelled}
            checked={isSelected}
            onChange={onToggleSelect}
            className="w-4 h-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500 cursor-pointer shrink-0 disabled:opacity-40 disabled:cursor-not-allowed"
          />

          <div className="w-9 h-9 shrink-0 rounded-full bg-gradient-to-br from-brand-500 to-ai-600 flex items-center justify-center text-xs font-bold text-white shadow-sm">
            {initials(c.candidateName)}
          </div>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-ink-900 dark:text-white truncate">{c.candidateName}</p>
            <p className="text-xs text-ink-400 truncate">{c.candidateEmail}</p>
          </div>
        </div>

        <div className="flex items-center gap-2 flex-shrink-0 flex-wrap justify-end">
          <span className={`px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${confirmCls}`}>{confirmLabel}</span>

          {c.verdict && (
            <span className={`px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${verdictCls}`}>
              {c.verdict === 'pass' ? 'Pass' : 'Not Pass'}
              {c.overallScore != null && ` · ${c.overallScore}đ`}
            </span>
          )}

          {c.sessionStatus && (
            <span className={`px-2.5 py-0.5 rounded-full text-[11px] font-medium ${
              c.sessionStatus === 'completed'
                ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                : c.sessionStatus === 'active'
                ? 'bg-brand-100 text-brand-700 dark:bg-brand-500/20 dark:text-brand-400 animate-pulse'
                : 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
            }`}>
              {c.sessionStatus === 'completed' ? 'Hoàn tất' : c.sessionStatus === 'active' ? 'Đang thực hiện' : c.sessionStatus}
            </span>
          )}

          {fmtDur(c.durationSeconds) && (
            <span className="hidden sm:flex items-center gap-1 text-xs text-ink-400">
              <Clock className="w-3 h-3" />{fmtDur(c.durationSeconds)}
            </span>
          )}

          {displayCode ? (
            <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-violet-50 dark:bg-violet-500/15 border border-violet-200 dark:border-violet-500/30 text-xs font-mono font-bold text-violet-700 dark:text-violet-300">
              <KeyRound className="w-3.5 h-3.5 text-violet-500 shrink-0" />
              <span>{displayCode}</span>
              <button
                type="button"
                onClick={() => handleCopyCode(displayCode)}
                className="p-1 hover:bg-violet-100 dark:hover:bg-violet-500/20 rounded transition-colors text-violet-600 dark:text-violet-300"
                title="Sao chép mã"
              >
                {copied ? <Check className="w-3.5 h-3.5 text-emerald-500" /> : <Copy className="w-3.5 h-3.5" />}
              </button>
            </div>
          ) : (
            <button
              disabled={generatingCode || isInactive}
              onClick={handleGenCode}
              title={isDeclined ? 'Ứng viên đã từ chối ca phỏng vấn này' : isCancelled ? 'Ứng viên đã bị loại' : 'Cấp mã phòng thi cho ca này'}
              className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-violet-200 dark:border-violet-500/20 bg-violet-50/50 dark:bg-violet-500/10 text-xs font-medium text-violet-700 dark:text-violet-300 hover:bg-violet-100 dark:hover:bg-violet-500/20 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <KeyRound className="w-3.5 h-3.5" />
              <span>{generatingCode ? 'Đang tạo...' : 'Cấp mã'}</span>
            </button>
          )}

          <button
            disabled={sendingReminder || isInactive}
            onClick={handleRemind}
            title={isDeclined ? 'Ứng viên đã từ chối ca phỏng vấn này' : isCancelled ? 'Ứng viên đã bị loại' : 'Gửi mail nhắc lịch phỏng vấn'}
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-amber-200 dark:border-amber-500/20 bg-amber-50/50 dark:bg-amber-500/10 text-xs font-medium text-amber-700 dark:text-amber-400 hover:bg-amber-100 dark:hover:bg-amber-500/20 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <Bell className="w-3.5 h-3.5" />
            <span className="hidden md:inline">{sendingReminder ? 'Đang gửi...' : 'Nhắc lịch'}</span>
          </button>

          <button
            disabled={isCancelled}
            onClick={() => setShowReschedule(true)}
            title={isCancelled ? 'Ứng viên đã bị loại' : 'Dời ứng viên sang ca phỏng vấn khác'}
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-brand-200 dark:border-brand-500/20 bg-brand-50/50 dark:bg-brand-500/10 text-xs font-medium text-brand-700 dark:text-brand-400 hover:bg-brand-100 dark:hover:bg-brand-500/20 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            <span className="hidden md:inline">Dời lịch</span>
          </button>

          <button
            disabled={rejecting || isCancelled}
            onClick={handleReject}
            title={isCancelled ? 'Ứng viên đã bị loại khỏi quy trình tuyển dụng' : 'Loại ứng viên khỏi quy trình tuyển dụng'}
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-red-200 dark:border-red-500/20 bg-red-50/50 dark:bg-red-500/10 text-xs font-medium text-red-700 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-500/20 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <UserX className="w-3.5 h-3.5" />
            <span className="hidden md:inline">{rejecting ? 'Đang loại...' : isCancelled ? 'Đã loại' : 'Loại'}</span>
          </button>

          <button
            onClick={() => navigate(c.evaluationId ? `/hr/evaluations?evaluationId=${c.evaluationId}` : `/hr/candidates/${c.applicationId}`)}
            className="flex items-center gap-1 px-3 py-1.5 rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-xs font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
          >
            <Eye className="w-3.5 h-3.5" /> Xem
          </button>
        </div>
      </div>



      {/* Toast feedback */}
      {reminderToast && (
        <p className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-500/10 px-3 py-1 rounded-lg">
          {reminderToast}
        </p>
      )}

      {/* Reschedule Modal */}
      {showReschedule && (
        <RescheduleModal
          candidates={[c]}
          availableSlots={availableSlots}
          currentSlotId={currentSlotId}
          currentRoundNumber={currentRoundNumber}
          onClose={() => setShowReschedule(false)}
          onSuccess={onReloadSlot}
        />
      )}
    </div>
  )
}

// ── sub-component: Slot card (expandable) ─────────────────────────────────────
function SlotCard({
  slot,
  allJobSlots,
  navigate,
}: {
  slot: InterviewSlotDetail
  allJobSlots: InterviewSlotDetail[]
  navigate: ReturnType<typeof useNavigate>
}) {
  const [open, setOpen] = useState(false)
  const [candidates, setCandidates] = useState<SlotCandidate[]>([])
  const [loading, setLoading] = useState(false)
  const [issuedCodes, setIssuedCodes] = useState<Record<string, IssuedCode>>({})

  // Multi-select state
  const [selectedBookingIds, setSelectedBookingIds] = useState<string[]>([])
  const [batchActionLoading, setBatchActionLoading] = useState(false)
  const [batchToast, setBatchToast] = useState<string | null>(null)
  const [showBatchReschedule, setShowBatchReschedule] = useState(false)

  const loadCandidates = async () => {
    setLoading(true)
    try {
      setCandidates(await interviewService.getCandidatesInSlot(slot.slotId))
    } catch {
      /* noop */
    } finally {
      setLoading(false)
    }
  }

  const toggle = async () => {
    if (!open && candidates.length === 0) {
      await loadCandidates()
    }
    setOpen(v => !v)
  }

  const selectableCandidates = useMemo(() => {
    return candidates.filter(c => {
      const isRejected = c.bookingStatus === 'cancelled' || c.declineReason?.includes('loại') || (c.confirmationStatus === 'declined' && c.declineReason?.includes('quy trình tuyển dụng'))
      return !isRejected
    })
  }, [candidates])

  const candidatesNeedingCode = useMemo(() => {
    return candidates.filter(c => {
      const isDeclined = c.confirmationStatus === 'declined'
      const isCancelled = c.bookingStatus === 'cancelled' || c.declineReason?.includes('loại')
      const hasCode = !!(issuedCodes[c.applicationId]?.code || c.interviewCode)
      return !isDeclined && !isCancelled && !hasCode
    })
  }, [candidates, issuedCodes])

  const handleSelectNeedingCode = () => {
    setSelectedBookingIds(candidatesNeedingCode.map(c => c.bookingId))
  }

  const handleSelectAll = () => {
    if (selectedBookingIds.length === selectableCandidates.length && selectableCandidates.length > 0) {
      setSelectedBookingIds([])
    } else {
      setSelectedBookingIds(selectableCandidates.map(c => c.bookingId))
    }
  }

  const handleToggleCandidate = (bookingId: string) => {
    setSelectedBookingIds(prev =>
      prev.includes(bookingId) ? prev.filter(id => id !== bookingId) : [...prev, bookingId]
    )
  }

  const handleIssueCodeSingle = async (appId: string) => {
    try {
      const res = await interviewService.generateCode(appId)
      setIssuedCodes(prev => ({ ...prev, [appId]: { code: res.code, expiresAt: res.expiresAt } }))
    } catch (e: any) {
      setBatchToast(e?.response?.data?.message || 'Không thể cấp mã phỏng vấn.')
      setTimeout(() => setBatchToast(null), 3000)
    }
  }

  const handleBatchIssueCodes = async () => {
    if (selectedBookingIds.length === 0) return
    setBatchActionLoading(true)
    const selectedApps = candidates.filter(c => selectedBookingIds.includes(c.bookingId))
    const appIds = selectedApps.map(c => c.applicationId)
    try {
      const results = await interviewService.generateCodeBatch(appIds, slot.roundNumber)
      const updated: Record<string, IssuedCode> = {}
      if (Array.isArray(results)) {
        results.forEach((r: any) => {
          const appId = r.applicationId || r.ApplicationId
          const code = r.code || r.Code
          if (appId && code) {
            updated[appId] = { code, expiresAt: '' }
          }
        })
      }
      setIssuedCodes(prev => ({ ...prev, ...updated }))
      setBatchToast(`Đã cấp mã phỏng vấn hàng loạt cho ${results?.length || selectedApps.length} ứng viên thành công!`)
      setTimeout(() => setBatchToast(null), 4000)
      loadCandidates()
    } catch (e: any) {
      setBatchToast(e?.response?.data?.message || 'Không thể cấp mã phỏng vấn hàng loạt.')
      setTimeout(() => setBatchToast(null), 4000)
    } finally {
      setBatchActionLoading(false)
    }
  }

  const handleBatchRemind = async () => {
    if (selectedBookingIds.length === 0) return
    setBatchActionLoading(true)
    let success = 0
    for (const bId of selectedBookingIds) {
      try {
        await interviewService.sendBookingReminder(bId)
        success++
      } catch {}
    }
    setBatchActionLoading(false)
    setBatchToast(`Đã gửi mail nhắc lịch cho ${success}/${selectedBookingIds.length} ứng viên!`)
    setTimeout(() => setBatchToast(null), 4000)
    setSelectedBookingIds([])
  }

  const handleBatchReject = async () => {
    if (selectedBookingIds.length === 0) return
    if (!window.confirm(`Bạn có chắc chắn muốn loại ${selectedBookingIds.length} ứng viên được chọn khỏi quy trình tuyển dụng?`)) return
    setBatchActionLoading(true)
    let success = 0
    const selectedApps = candidates.filter(c => selectedBookingIds.includes(c.bookingId))
    for (const app of selectedApps) {
      try {
        await applicationService.rejectApplication(app.applicationId)
        success++
      } catch {}
    }
    setBatchActionLoading(false)
    setBatchToast(`Đã loại ${success}/${selectedApps.length} ứng viên thành công!`)
    setTimeout(() => setBatchToast(null), 4000)
    setSelectedBookingIds([])
    loadCandidates()
  }

  const selectedCandidatesList = useMemo(() => {
    return candidates.filter(c => selectedBookingIds.includes(c.bookingId))
  }, [candidates, selectedBookingIds])

  const fillPct = slot.capacity > 0 ? Math.round((slot.bookedCount / slot.capacity) * 100) : 0

  return (
    <div className={`rounded-xl border transition-all ${slot.isPast ? 'border-ink-200 dark:border-white/10 opacity-75' : 'border-brand-200 dark:border-brand-500/30'} bg-white dark:bg-white/5 overflow-hidden`}>
      <button onClick={toggle} className="w-full flex items-center justify-between p-4 text-left hover:bg-ink-50 dark:hover:bg-white/5 transition-colors">
        <div className="flex items-center gap-3 min-w-0">
          <div className={`w-2.5 h-2.5 rounded-full flex-shrink-0 ${slot.isPast ? 'bg-ink-300' : 'bg-emerald-500 animate-pulse'}`} />
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-sm font-bold text-ink-900 dark:text-white">
                {fmtDate(slot.startTime)} · {fmtTime(slot.startTime)} – {fmtTime(slot.endTime)}
              </span>
              <span className="px-2 py-0.5 rounded-md text-[11px] font-semibold bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400">
                Vòng {slot.roundNumber}
              </span>
              {slot.isPast && <span className="text-[11px] text-ink-400 bg-ink-100 dark:bg-white/10 px-2 py-0.5 rounded-md">Đã qua</span>}
            </div>
            <div className="flex items-center gap-3 mt-1.5">
              <span className="text-xs text-ink-500 dark:text-ink-400 flex items-center gap-1 font-medium">
                <Users className="w-3.5 h-3.5" /> {slot.bookedCount}/{slot.capacity} ứng viên
              </span>
              {slot.confirmedCount > 0 && (
                <span className="text-xs text-emerald-600 dark:text-emerald-400 flex items-center gap-1 font-medium">
                  <CheckCircle2 className="w-3.5 h-3.5" /> {slot.confirmedCount} xác nhận
                </span>
              )}
              {slot.declinedCount > 0 && (
                <span className="text-xs text-red-500 dark:text-red-400 flex items-center gap-1 font-medium">
                  <XCircle className="w-3.5 h-3.5" /> {slot.declinedCount} từ chối
                </span>
              )}
              {slot.pendingCount > 0 && (
                <span className="text-xs text-amber-500 dark:text-amber-400 flex items-center gap-1 font-medium">
                  <AlertCircle className="w-3.5 h-3.5" /> {slot.pendingCount} chờ
                </span>
              )}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-3 flex-shrink-0">
          <div className="hidden sm:block w-20 h-1.5 rounded-full bg-ink-100 dark:bg-white/10 overflow-hidden">
            <div className="h-full bg-brand-500 rounded-full transition-all" style={{ width: `${fillPct}%` }} />
          </div>
          {open ? <ChevronDown className="w-4 h-4 text-ink-400" /> : <ChevronRight className="w-4 h-4 text-ink-400" />}
        </div>
      </button>

      <AnimatePresence>
        {open && (
          <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <div className="border-t border-ink-100 dark:border-white/10 px-2 py-2">
              {loading ? (
                <p className="text-sm text-ink-400 text-center py-4">Đang tải ứng viên...</p>
              ) : candidates.length === 0 ? (
                <p className="text-sm text-ink-400 text-center py-4">Chưa có ứng viên nào đăng ký ca phỏng vấn này.</p>
              ) : (
                <>
                  {/* Batch Action Toolbar */}
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 px-4 py-2.5 bg-ink-50/80 dark:bg-white/5 rounded-xl mb-3 border border-ink-100 dark:border-white/10">
                    <div className="flex items-center gap-3 flex-wrap">
                      <label className="flex items-center gap-2 text-xs font-semibold text-ink-700 dark:text-ink-200 cursor-pointer">
                        <input
                          type="checkbox"
                          disabled={selectableCandidates.length === 0}
                          checked={selectableCandidates.length > 0 && selectedBookingIds.length === selectableCandidates.length}
                          onChange={handleSelectAll}
                          className="w-4 h-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500 cursor-pointer disabled:opacity-40"
                        />
                        <span>{selectedBookingIds.length > 0 ? `Đã chọn (${selectedBookingIds.length}/${selectableCandidates.length})` : 'Chọn tất cả chưa loại'}</span>
                      </label>

                      {candidatesNeedingCode.length > 0 && (
                        <button
                          type="button"
                          onClick={handleSelectNeedingCode}
                          className="px-2.5 py-1 rounded-md bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-300 hover:bg-violet-200 text-xs font-semibold transition-colors"
                        >
                          ⚡ Chọn nhanh {candidatesNeedingCode.length} người chưa có mã
                        </button>
                      )}
                    </div>

                    {selectedBookingIds.length > 0 && (
                      <div className="flex items-center gap-2 flex-wrap">
                        <button
                          disabled={batchActionLoading}
                          onClick={handleBatchIssueCodes}
                          style={{ backgroundColor: '#7c3aed', color: '#ffffff' }}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-white text-xs font-bold shadow-xs hover:opacity-90 transition-all disabled:opacity-50 cursor-pointer"
                        >
                          <KeyRound className="w-3.5 h-3.5 text-white shrink-0" />
                          <span>Cấp mã hàng loạt ({selectedBookingIds.length})</span>
                        </button>
                        <button
                          disabled={batchActionLoading}
                          onClick={handleBatchReject}
                          style={{ backgroundColor: '#dc2626', color: '#ffffff' }}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-white text-xs font-bold shadow-xs hover:opacity-90 transition-all disabled:opacity-50 cursor-pointer"
                        >
                          <UserX className="w-3.5 h-3.5 text-white shrink-0" />
                          <span>Loại hàng loạt ({selectedBookingIds.length})</span>
                        </button>
                        <button
                          disabled={batchActionLoading}
                          onClick={handleBatchRemind}
                          style={{ backgroundColor: '#d97706', color: '#ffffff' }}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-white text-xs font-bold shadow-xs hover:opacity-90 transition-all disabled:opacity-50 cursor-pointer"
                        >
                          <Bell className="w-3.5 h-3.5 text-white shrink-0" />
                          <span>Gửi nhắc lịch hàng loạt ({selectedBookingIds.length})</span>
                        </button>
                        <button
                          onClick={() => setShowBatchReschedule(true)}
                          style={{ backgroundColor: '#2563eb', color: '#ffffff' }}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-white text-xs font-bold shadow-xs hover:opacity-90 transition-all cursor-pointer"
                        >
                          <RefreshCw className="w-3.5 h-3.5 text-white shrink-0" />
                          <span>Dời lịch hàng loạt ({selectedBookingIds.length})</span>
                        </button>
                      </div>
                    )}
                  </div>

                  {batchToast && (
                    <div className="mx-4 mb-2 p-2 rounded-lg bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 text-xs font-semibold">
                      {batchToast}
                    </div>
                  )}

                  <div className="divide-y divide-ink-100 dark:divide-white/5">
                    {candidates.map(c => (
                      <CandidateRow
                        key={c.bookingId}
                        c={c}
                        navigate={navigate}
                        availableSlots={allJobSlots}
                        currentSlotId={slot.slotId}
                        currentRoundNumber={slot.roundNumber}
                        isSelected={selectedBookingIds.includes(c.bookingId)}
                        onToggleSelect={() => handleToggleCandidate(c.bookingId)}
                        onReloadSlot={loadCandidates}
                        issuedCodes={issuedCodes}
                        onIssueCode={handleIssueCodeSingle}
                      />
                    ))}
                  </div>

                  {/* Batch Reschedule Modal */}
                  {showBatchReschedule && (
                    <RescheduleModal
                      candidates={selectedCandidatesList}
                      availableSlots={allJobSlots}
                      currentSlotId={slot.slotId}
                      currentRoundNumber={slot.roundNumber}
                      onClose={() => setShowBatchReschedule(false)}
                      onSuccess={() => {
                        setSelectedBookingIds([])
                        loadCandidates()
                      }}
                    />
                  )}
                </>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}

// ── sub-component: Job card (expandable) ─────────────────────────────────────
function JobCard({
  job,
  dateFilter,
  navigate,
}: {
  job: InterviewJobSummary
  dateFilter: string
  navigate: ReturnType<typeof useNavigate>
}) {
  const [open, setOpen] = useState(false)
  const [slots, setSlots] = useState<InterviewSlotDetail[]>([])
  const [loading, setLoading] = useState(false)

  const toggle = async () => {
    if (!open && slots.length === 0 && job.totalSlots > 0) {
      setLoading(true)
      try { setSlots(await interviewService.getSlotsForJob(job.jobId)) } catch { /* noop */ }
      finally { setLoading(false) }
    }
    setOpen(v => !v)
  }

  // Tự động mở ca nếu đang lọc theo ngày
  useEffect(() => {
    if (dateFilter && job.totalSlots > 0 && slots.length === 0) {
      toggle()
    }
  }, [dateFilter])

  // Lọc ca phỏng vấn theo ngày được chọn (nếu có)
  const filteredSlots = useMemo(() => {
    if (!dateFilter) return slots
    return slots.filter(s => isSameDay(s.startTime, dateFilter))
  }, [slots, dateFilter])

  const slotsByRound = useMemo(() => {
    const map = new Map<number, InterviewSlotDetail[]>()
    filteredSlots.forEach(s => {
      const arr = map.get(s.roundNumber) ?? []
      arr.push(s); map.set(s.roundNumber, arr)
    })
    return map
  }, [filteredSlots])

  const isActive = job.jobStatus === 'active' || job.jobStatus === 'published'

  return (
    <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card overflow-hidden">
      <div onClick={toggle} role="button" tabIndex={0} className="w-full flex items-center justify-between p-5 lg:p-6 text-left hover:bg-ink-50/50 dark:hover:bg-white/5 transition-colors cursor-pointer">
        <div className="flex items-center gap-4 min-w-0">
          <div className="w-12 h-12 shrink-0 rounded-xl bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center shadow-md">
            <Briefcase className="w-5 h-5 text-white" />
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap mb-1">
              <h3 className="text-base font-bold text-ink-900 dark:text-white truncate">{job.jobTitle}</h3>
              <span className={`px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${isActive ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400' : 'bg-ink-100 text-ink-500 dark:bg-white/10 dark:text-ink-400'}`}>
                {isActive ? 'Đang tuyển' : 'Đã đóng'}
              </span>
              {job.maxRound > 0 && (
                <span className="px-2 py-0.5 rounded-md text-[11px] font-medium bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400">
                  Vòng {job.maxRound}
                </span>
              )}
            </div>
            <div className="flex items-center gap-4 flex-wrap">
              <span className="text-xs text-ink-500 dark:text-ink-400 flex items-center gap-1 font-medium">
                <Calendar className="w-3.5 h-3.5" /> {job.totalSlots} ca phỏng vấn
              </span>
              <span className="text-xs text-ink-500 dark:text-ink-400 flex items-center gap-1 font-medium">
                <Users className="w-3.5 h-3.5" /> {job.totalBooked} ứng viên đã xếp lịch
              </span>
              <span className="text-xs text-emerald-600 dark:text-emerald-400 flex items-center gap-1 font-semibold">
                <CheckCircle2 className="w-3.5 h-3.5" /> {job.totalConfirmed} xác nhận
              </span>
              {job.nextSlotTime && (
                <span className="text-xs text-brand-600 dark:text-brand-400 font-semibold">
                  Ca tiếp: {fmtDate(job.nextSlotTime)} {fmtTime(job.nextSlotTime)}
                </span>
              )}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0 ml-4">
          <button
            onClick={e => { e.stopPropagation(); navigate(`/hr/jobs/${job.jobId}`) }}
            className="hidden sm:flex items-center gap-1 px-3 py-1.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-xs font-medium text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
          >
            <Eye className="w-3.5 h-3.5" /> Quản lý tin
          </button>
          {open ? <ChevronDown className="w-5 h-5 text-ink-400" /> : <ChevronRight className="w-5 h-5 text-ink-400" />}
        </div>
      </div>

      <AnimatePresence>
        {open && (
          <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <div className="border-t border-ink-100 dark:border-white/10 p-4 lg:p-5 bg-ink-50/50 dark:bg-white/[0.02]">
              {loading ? (
                <p className="text-sm text-ink-400 text-center py-6">Đang tải ca phỏng vấn...</p>
              ) : job.totalSlots === 0 ? (
                <div className="text-center py-8 bg-white dark:bg-white/5 rounded-2xl border border-dashed border-ink-200 dark:border-white/10 p-6">
                  <Calendar className="w-8 h-8 text-ink-300 dark:text-white/20 mx-auto mb-2" />
                  <p className="text-sm font-semibold text-ink-700 dark:text-ink-200">Vị trí này chưa có ca phỏng vấn nào được tạo</p>
                  <p className="text-xs text-ink-400 mt-1 mb-4">Hãy mở trang chi tiết vị trí tuyển dụng để cấu hình các khung giờ phỏng vấn.</p>
                  <button
                    onClick={() => navigate(`/hr/jobs/${job.jobId}`)}
                    className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-brand-600 hover:bg-brand-700 text-white text-xs font-semibold transition-colors shadow-sm"
                  >
                    <Plus className="w-4 h-4" /> Tạo ca phỏng vấn cho vị trí này
                  </button>
                </div>
              ) : filteredSlots.length === 0 ? (
                <div className="text-center py-6 text-xs text-ink-400">
                  Không có ca phỏng vấn nào trong ngày <span className="font-semibold text-ink-700 dark:text-white">{dateFilter}</span>
                </div>
              ) : (
                <div className="space-y-6">
                  {[...slotsByRound.entries()].sort(([a], [b]) => a - b).map(([round, rSlots]) => (
                    <div key={round}>
                      <p className="text-xs font-bold text-ink-500 dark:text-ink-400 uppercase tracking-wider mb-3">
                        Vòng {round} ({rSlots.length} ca)
                      </p>
                      <div className="space-y-3">
                        {rSlots.map(slot => (
                          <SlotCard key={slot.slotId} slot={slot} allJobSlots={slots} navigate={navigate} />
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────
export default function InterviewSessionsPage() {
  const navigate = useNavigate()
  
  const { data: jobs = [], isLoading: loading, error: queryError } = useQuery({
    queryKey: ['hr-interview-jobs'],
    queryFn: () => interviewService.getInterviewJobs(),
    staleTime: 5 * 60 * 1000,
    placeholderData: keepPreviousData,
  })

  const error = queryError ? 'Không tải được danh sách vị trí phỏng vấn. Vui lòng thử lại.' : null
  
  const [searchParams, setSearchParams] = useSearchParams()

  const currentPage = Number(searchParams.get('page')) || 1
  const search = searchParams.get('search') || ''
  const jobStatusFilter = (searchParams.get('status') as 'active' | 'closed') || 'active'
  const dateFilter = searchParams.get('date') || ''

  // Map lưu trữ ca phỏng vấn đã nạp của từng job để lọc job chính xác khi chọn dateFilter
  const [jobSlotsMap, setJobSlotsMap] = useState<Record<string, InterviewSlotDetail[]>>({})
  const [loadingSlotsForFilter, setLoadingSlotsForFilter] = useState(false)

  const updateParam = (key: string, value: string) => {
    setSearchParams((prev) => {
      const p = new URLSearchParams(prev)
      if (!value || value === 'active') {
        p.delete(key)
      } else {
        p.set(key, value)
      }
      p.delete('page')
      return p
    }, { replace: true })
  }

  const handlePageChange = (newPage: number) => {
    setSearchParams((prev) => {
      const p = new URLSearchParams(prev)
      if (newPage > 1) p.set('page', String(newPage))
      else p.delete('page')
      return p
    }, { replace: true })
  }
  const pageSize = 5

  // Nạp ca phỏng vấn cho các Job nếu người dùng bật bộ lọc theo ngày
  useEffect(() => {
    if (!dateFilter || jobs.length === 0) return
    let active = true
    ;(async () => {
      setLoadingSlotsForFilter(true)
      const missingJobIds = jobs.map(j => j.jobId).filter(id => !jobSlotsMap[id])
      if (missingJobIds.length > 0) {
        const results = await Promise.allSettled(
          missingJobIds.map(async id => {
            const slots = await interviewService.getSlotsForJob(id)
            return { id, slots }
          })
        )
        if (active) {
          setJobSlotsMap(prev => {
            const next = { ...prev }
            results.forEach(res => {
              if (res.status === 'fulfilled') {
                next[res.value.id] = res.value.slots
              }
            })
            return next
          })
        }
      }
      if (active) setLoadingSlotsForFilter(false)
    })()
    return () => { active = false }
  }, [dateFilter, jobs])


  const filtered = useMemo(() => {
    let list = jobs

    // Filter by Job status (Mặc định active)
    if (jobStatusFilter === 'active') {
      list = list.filter(j => j.jobStatus === 'active' || j.jobStatus === 'published')
    } else if (jobStatusFilter === 'closed') {
      list = list.filter(j => j.jobStatus !== 'active' && j.jobStatus !== 'published')
    }

    // Filter by Search Query
    const q = search.trim().toLowerCase()
    if (q) {
      list = list.filter(j => j.jobTitle.toLowerCase().includes(q))
    }

    // Lọc duy nhất những job CÓ CA PHỎNG VẤN TRONG NGÀY ĐƯỢC CHỌN
    if (dateFilter) {
      list = list.filter(j => {
        const loadedSlots = jobSlotsMap[j.jobId]
        if (loadedSlots) {
          return loadedSlots.some(s => isSameDay(s.startTime, dateFilter))
        }
        // Nếu chưa nạp xong slots, kiểm tra qua nextSlotTime tạm thời
        return j.nextSlotTime ? isSameDay(j.nextSlotTime, dateFilter) : true
      })
    }

    return list
  }, [jobs, search, jobStatusFilter, dateFilter, jobSlotsMap])

  // Phân trang
  const totalPages = Math.ceil(filtered.length / pageSize) || 1
  const paginatedJobs = useMemo(() => {
    const start = (currentPage - 1) * pageSize
    return filtered.slice(start, start + pageSize)
  }, [filtered, currentPage, pageSize])

  const totalJobs = jobs.length
  const totalSlots = jobs.reduce((s, j) => s + j.totalSlots, 0)
  const totalBooked = jobs.reduce((s, j) => s + j.totalBooked, 0)
  const totalConfirmed = jobs.reduce((s, j) => s + j.totalConfirmed, 0)

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader
        title="Quản Lý Lịch Phỏng Vấn"
        description="Quản lý danh sách vị trí tuyển dụng, các ca phỏng vấn và trạng thái tham gia của ứng viên"
      />

      {error && <ErrorAlert message={error} />}

      {/* Stats Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        {[
          { label: 'Tổng vị trí tuyển dụng', value: totalJobs, color: 'text-blue-600 dark:text-blue-400' },
          { label: 'Tổng ca phỏng vấn', value: totalSlots, color: 'text-brand-600 dark:text-brand-400' },
          { label: 'Ứng viên đã xếp lịch', value: totalBooked, color: 'text-ai-600 dark:text-ai-400' },
          { label: 'Ứng viên đã xác nhận', value: totalConfirmed, color: 'text-emerald-600 dark:text-emerald-400' },
        ].map(stat => (
          <div key={stat.label} className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <p className={`text-2xl font-extrabold ${stat.color}`}>{loading ? '—' : stat.value}</p>
            <p className="text-sm font-medium text-ink-500 dark:text-ink-400 mt-1">{stat.label}</p>
          </div>
        ))}
      </div>

      {/* Filter & Search Bar */}
      <div className="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3 mb-6">
        {/* Search */}
        <div className="relative flex-1">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-ink-400" />
          <input
            value={search}
            onChange={e => updateParam('search', e.target.value)}
            placeholder="Tìm kiếm vị trí tuyển dụng..."
            className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-900 dark:text-white placeholder:text-ink-400 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500/40"
          />
        </div>

        <div className="flex items-center gap-2 flex-wrap shrink-0">
          {/* Lọc theo ngày ca phỏng vấn */}
          <div className="relative flex items-center">
            <CalendarDays className="absolute left-3 w-4 h-4 text-ink-400 pointer-events-none" />
            <input
              type="date"
              value={dateFilter}
              onChange={e => updateParam('date', e.target.value)}
              className="pl-9 pr-8 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-800 dark:text-ink-100 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-brand-500/40"
            />
            {dateFilter && (
              <button
                onClick={() => updateParam('date', '')}
                title="Xóa lọc ngày"
                className="absolute right-2 p-1 text-ink-400 hover:text-ink-600 dark:hover:text-white"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>

          {/* Lọc Trạng Thái Job: CHỈ CÓ Đang tuyển dụng & Đã đóng */}
          <div className="flex items-center gap-1.5">
            <Filter className="w-4 h-4 text-ink-400" />
            <select
              value={jobStatusFilter}
              onChange={e => updateParam('status', e.target.value)}
              className="px-3 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-800 dark:text-ink-100 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-brand-500/40"
            >
              <option value="active">Vị trí đang tuyển dụng</option>
              <option value="closed">Vị trí đã đóng tuyển</option>
            </select>
          </div>
        </div>
      </div>

      {/* Notification banner when date filter is active */}
      {dateFilter && (
        <div className="mb-4 p-3 rounded-xl bg-brand-50 dark:bg-brand-500/10 border border-brand-200 dark:border-brand-500/20 text-xs text-brand-700 dark:text-brand-300 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CalendarDays className="w-4 h-4 shrink-0" />
            <span>Đang lọc các vị trí & ca phỏng vấn diễn ra vào ngày <strong className="underline">{fmtDate(dateFilter)}</strong></span>
            {loadingSlotsForFilter && <span className="text-[11px] italic animate-pulse">(Đang quét danh sách ca...)</span>}
          </div>
          <button onClick={() => updateParam('date', '')} className="font-semibold text-brand-800 hover:underline">
            Xóa lọc
          </button>
        </div>
      )}

      {/* List of Jobs */}
      {loading ? (
        <div className="space-y-4">
          {[1, 2, 3].map(i => (
            <div key={i} className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 animate-pulse">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-xl bg-ink-100 dark:bg-white/10" />
                <div className="flex-1 space-y-2">
                  <div className="h-4 bg-ink-100 dark:bg-white/10 rounded w-48" />
                  <div className="h-3 bg-ink-100 dark:bg-white/10 rounded w-72" />
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={<Briefcase className="w-7 h-7 text-ink-400" />}
          title={jobs.length === 0 ? 'Chưa có vị trí tuyển dụng nào' : 'Không tìm thấy vị trí phù hợp'}
          description={jobs.length === 0 ? 'Hãy tạo vị trí tuyển dụng mới và thiết lập khung giờ phỏng vấn.' : 'Thử thay đổi từ khóa hoặc bộ lọc.'}
        />
      ) : (
        <>
          <div className="space-y-4">
            {paginatedJobs.map(job => (
              <JobCard key={job.jobId} job={job} dateFilter={dateFilter} navigate={navigate} />
            ))}
          </div>

          {/* Phân Trang (Pagination Controls) */}
          {totalPages > 1 && (
            <div className="mt-8 flex flex-col sm:flex-row items-center justify-between gap-4 pt-4 border-t border-ink-200 dark:border-white/10">
              <p className="text-xs text-ink-500 dark:text-ink-400 font-medium">
                Hiển thị {paginatedJobs.length} trên tổng số {filtered.length} vị trí (Trang {currentPage}/{totalPages})
              </p>
              <div className="flex items-center gap-1.5">
                <button
                  disabled={currentPage === 1}
                  onClick={() => handlePageChange(Math.max(1, currentPage - 1))}
                  className="p-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 disabled:opacity-40 transition-colors"
                  title="Trang trước"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>

                {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
                  <button
                    key={page}
                    onClick={() => handlePageChange(page)}
                    className={`w-8 h-8 rounded-xl text-xs font-bold transition-all ${
                      currentPage === page
                        ? 'bg-brand-600 text-white shadow-sm'
                        : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10'
                    }`}
                  >
                    {page}
                  </button>
                ))}

                <button
                  disabled={currentPage === totalPages}
                  onClick={() => handlePageChange(Math.min(totalPages, currentPage + 1))}
                  className="p-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 disabled:opacity-40 transition-colors"
                  title="Trang sau"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  )
}
