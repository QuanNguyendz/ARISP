import { useEffect, useMemo, useState } from 'react'
import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { motion, AnimatePresence } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  KeyRound,
  Search,
  Loader2,
  Copy,
  Check,
  Clock,
  Mail,
  ShieldCheck,
  Calendar,
  Users,
  ChevronDown,
  ChevronRight,
  ChevronLeft,
  CalendarDays,
  X,
  CheckCircle2,
  Briefcase
} from 'lucide-react'
import { PageHeader, ErrorAlert, EmptyState } from '@ari/shared/ui'
import { applicationService } from '@ari/shared/fservices/application'
import {
  interviewService,
  type InterviewJobSummary,
  type InterviewSlotDetail,
  type SlotCandidate
} from '@ari/shared/fservices/interview'

interface IssuedCode {
  code: string
  expiresAt: string
}

const fmtDate = (iso: string) =>
  new Date(iso).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })

const fmtTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })

const initials = (name: string) =>
  name.trim().split(/\s+/).slice(-2).map((n) => n[0]).join('').toUpperCase()

const isSameDay = (iso: string, targetDateStr: string) => {
  if (!targetDateStr) return true
  const d = new Date(iso)
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}` === targetDateStr
}

// ── sub-component: Slot Card (Ca Phỏng Vấn) ──────────────────────────────────
function SessionSlotCard({
  slot,
  searchQuery,
  issuedCodes,
  onGenerateCode,
}: {
  slot: InterviewSlotDetail
  searchQuery: string
  issuedCodes: Record<string, IssuedCode>
  onGenerateCode: (appId: string) => Promise<void>
}) {
  const [open, setOpen] = useState(false)
  const [candidates, setCandidates] = useState<SlotCandidate[]>([])
  const [loading, setLoading] = useState(false)
  const [copiedCode, setCopiedCode] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  // Multi-select state
  const [selectedAppIds, setSelectedAppIds] = useState<string[]>([])
  const [batchLoading, setBatchLoading] = useState(false)
  const [batchToast, setBatchToast] = useState<string | null>(null)

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
    setOpen((v) => !v)
  }

  // Tự động mở ca nếu tìm kiếm khớp với ca này
  useEffect(() => {
    if (searchQuery && candidates.length === 0) {
      loadCandidates()
      setOpen(true)
    }
  }, [searchQuery])

  const filteredCandidates = useMemo(() => {
    const q = searchQuery.trim().toLowerCase()
    if (!q) return candidates
    return candidates.filter(
      (c) =>
        c.candidateName.toLowerCase().includes(q) ||
        c.candidateEmail.toLowerCase().includes(q)
    )
  }, [candidates, searchQuery])

  const handleCopy = async (code: string) => {
    try {
      await navigator.clipboard.writeText(code)
      setCopiedCode(code)
      setTimeout(() => setCopiedCode(null), 2000)
    } catch { }
  }

  const handleSingleGen = async (appId: string) => {
    setBusyId(appId)
    try {
      await onGenerateCode(appId)
    } finally {
      setBusyId(null)
    }
  }

  const handleSelectAll = () => {
    if (selectedAppIds.length === filteredCandidates.length) {
      setSelectedAppIds([])
    } else {
      setSelectedAppIds(filteredCandidates.map((c) => c.applicationId))
    }
  }

  const handleToggleCandidate = (appId: string) => {
    setSelectedAppIds((prev) =>
      prev.includes(appId) ? prev.filter((id) => id !== appId) : [...prev, appId]
    )
  }

  const handleBatchGenerate = async () => {
    if (selectedAppIds.length === 0) return
    setBatchLoading(true)
    let success = 0
    for (const appId of selectedAppIds) {
      try {
        await onGenerateCode(appId)
        success++
      } catch { }
    }
    setBatchLoading(false)
    setBatchToast(`Đã cấp mã thành công cho ${success}/${selectedAppIds.length} ứng viên!`)
    setTimeout(() => setBatchToast(null), 4000)
  }

  const expiresIn = (iso: string): string => {
    const ms = new Date(iso).getTime() - Date.now()
    if (ms <= 0) return 'Hết hạn'
    const mins = Math.round(ms / 60000)
    if (mins < 60) return `${mins} phút`
    return `${Math.round(mins / 60)} giờ`
  }

  return (
    <div className={`rounded-xl border transition-all ${slot.isPast ? 'border-ink-200 dark:border-white/10 opacity-80' : 'border-violet-200 dark:border-violet-500/30'} bg-white dark:bg-white/5 overflow-hidden shadow-xs`}>
      {/* Slot Card Header */}
      <button
        onClick={toggle}
        className="w-full flex items-center justify-between p-4 text-left hover:bg-ink-50/70 dark:hover:bg-white/5 transition-colors"
      >
        <div className="flex items-center gap-3 min-w-0">
          <div className={`w-3 h-3 rounded-full shrink-0 ${slot.isPast ? 'bg-ink-300' : 'bg-violet-500 animate-pulse'}`} />
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-sm font-bold text-ink-900 dark:text-white">
                {fmtDate(slot.startTime)} · {fmtTime(slot.startTime)} – {fmtTime(slot.endTime)}
              </span>
              <span className="px-2 py-0.5 rounded-md text-[11px] font-semibold bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-300">
                Vòng {slot.roundNumber}
              </span>
              {slot.isPast && (
                <span className="text-[11px] text-ink-400 bg-ink-100 dark:bg-white/10 px-2 py-0.5 rounded-md">
                  Đã qua
                </span>
              )}
            </div>
            <div className="flex items-center gap-3 mt-1 text-xs text-ink-500 dark:text-ink-400">
              <span className="flex items-center gap-1 font-medium">
                <Users className="w-3.5 h-3.5 text-ink-400" /> {slot.bookedCount}/{slot.capacity} ứng viên
              </span>
              {slot.confirmedCount > 0 && (
                <span className="flex items-center gap-1 font-medium text-emerald-600 dark:text-emerald-400">
                  <CheckCircle2 className="w-3.5 h-3.5" /> {slot.confirmedCount} xác nhận
                </span>
              )}
            </div>
          </div>
        </div>

        <div className="flex items-center gap-3 shrink-0">
          <span className="text-xs font-semibold text-violet-600 dark:text-violet-400 bg-violet-50 dark:bg-violet-500/10 px-3 py-1 rounded-full border border-violet-100 dark:border-violet-500/20">
            Ca phỏng vấn
          </span>
          {open ? <ChevronDown className="w-4 h-4 text-ink-400" /> : <ChevronRight className="w-4 h-4 text-ink-400" />}
        </div>
      </button>

      {/* Candidates List inside Slot */}
      <AnimatePresence>
        {open && (
          <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <div className="border-t border-ink-100 dark:border-white/10 p-3 bg-ink-50/40 dark:bg-white/[0.02]">
              {loading ? (
                <p className="text-xs text-ink-400 text-center py-4">Đang tải danh sách ứng viên ca này...</p>
              ) : filteredCandidates.length === 0 ? (
                <p className="text-xs text-ink-400 text-center py-4">Chưa có ứng viên nào trong ca thi này.</p>
              ) : (
                <>
                  {/* Batch toolbar */}
                  <div className="flex items-center justify-between gap-3 px-3 py-2 bg-white dark:bg-white/5 rounded-lg border border-ink-200 dark:border-white/10 mb-2">
                    <label className="flex items-center gap-2 text-xs font-semibold text-ink-700 dark:text-ink-200 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={filteredCandidates.length > 0 && selectedAppIds.length === filteredCandidates.length}
                        onChange={handleSelectAll}
                        className="w-4 h-4 rounded border-ink-300 text-violet-600 focus:ring-violet-500 cursor-pointer"
                      />
                      <span>{selectedAppIds.length > 0 ? `Đã chọn (${selectedAppIds.length}/${filteredCandidates.length})` : 'Chọn tất cả trong ca'}</span>
                    </label>

                    {selectedAppIds.length > 0 && (
                      <button
                        disabled={batchLoading}
                        onClick={handleBatchGenerate}
                        className="flex items-center gap-1.5 px-3 py-1 rounded-lg bg-violet-600 hover:bg-violet-700 text-white text-xs font-semibold shadow-xs transition-colors disabled:opacity-50"
                      >
                        {batchLoading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <KeyRound className="w-3.5 h-3.5" />}
                        <span>Cấp mã hàng loạt ({selectedAppIds.length})</span>
                      </button>
                    )}
                  </div>

                  {batchToast && (
                    <div className="mb-2 p-2 rounded-lg bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 text-xs font-semibold">
                      {batchToast}
                    </div>
                  )}

                  <div className="divide-y divide-ink-100 dark:divide-white/5 bg-white dark:bg-white/5 rounded-xl border border-ink-200 dark:border-white/10 overflow-hidden">
                    {filteredCandidates.map((c) => {
                      const code = issuedCodes[c.applicationId]
                      const isSelected = selectedAppIds.includes(c.applicationId)
                      const isCancelled = c.bookingStatus === 'cancelled'

                      return (
                        <div
                          key={c.bookingId}
                          className={`flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3 transition-colors ${isSelected ? 'bg-violet-50/50 dark:bg-violet-500/10' : 'hover:bg-ink-50 dark:hover:bg-white/5'
                            }`}
                        >
                          <div className="flex items-center gap-3 min-w-0">
                            <input
                              type="checkbox"
                              disabled={isCancelled}
                              checked={isSelected}
                              onChange={() => handleToggleCandidate(c.applicationId)}
                              className="w-4 h-4 rounded border-ink-300 text-violet-600 focus:ring-violet-500 cursor-pointer shrink-0 disabled:opacity-40"
                            />
                            <div className="w-9 h-9 shrink-0 rounded-full bg-gradient-to-br from-violet-600 to-brand-600 flex items-center justify-center text-xs font-bold text-white shadow-xs">
                              {initials(c.candidateName)}
                            </div>
                            <div className="min-w-0">
                              <p className="text-sm font-semibold text-ink-900 dark:text-white truncate">
                                {c.candidateName}
                              </p>
                              <p className="text-xs text-ink-400 truncate flex items-center gap-1">
                                <Mail className="w-3 h-3" /> {c.candidateEmail}
                              </p>
                            </div>
                          </div>

                          <div className="flex items-center gap-3 shrink-0 justify-end">
                            <span className={`px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${c.confirmationStatus === 'confirmed'
                                ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                                : c.confirmationStatus === 'declined'
                                  ? 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
                                  : 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400'
                              }`}>
                              {c.confirmationStatus === 'confirmed'
                                ? 'Đã xác nhận'
                                : c.confirmationStatus === 'declined'
                                  ? 'Từ chối'
                                  : 'Chờ xác nhận'}
                            </span>

                            {code ? (
                              <div className="flex items-center gap-2">
                                <button
                                  onClick={() => handleCopy(code.code)}
                                  className="inline-flex items-center gap-1.5 rounded-lg border border-violet-300 dark:border-violet-500/40 bg-violet-50 dark:bg-violet-500/15 px-3 py-1.5 font-mono text-sm font-bold tracking-wider text-violet-800 dark:text-violet-300 shadow-xs hover:bg-violet-100 transition-colors"
                                  title="Sao chép mã phòng thi"
                                >
                                  <KeyRound className="w-3.5 h-3.5 text-violet-600" />
                                  <span>{code.code}</span>
                                  {copiedCode === code.code ? (
                                    <Check className="w-3.5 h-3.5 text-emerald-500 ml-1" />
                                  ) : (
                                    <Copy className="w-3.5 h-3.5 text-violet-500 ml-1" />
                                  )}
                                </button>
                                <span className="flex items-center gap-1 text-[11px] text-ink-400 font-medium">
                                  <Clock className="w-3 h-3" /> {expiresIn(code.expiresAt)}
                                </span>
                              </div>
                            ) : (
                              <button
                                disabled={busyId === c.applicationId || isCancelled}
                                onClick={() => handleSingleGen(c.applicationId)}
                                className="inline-flex items-center gap-1.5 rounded-lg bg-violet-600 hover:bg-violet-700 text-white text-xs font-semibold px-3 py-1.5 shadow-xs transition-colors disabled:opacity-40"
                              >
                                {busyId === c.applicationId ? (
                                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                                ) : (
                                  <KeyRound className="w-3.5 h-3.5" />
                                )}
                                <span>Cấp mã</span>
                              </button>
                            )}
                          </div>
                        </div>
                      )
                    })}
                  </div>
                </>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}

// ── sub-component: Job Group Card ─────────────────────────────────────────────
function JobSessionsGroup({
  job,
  dateFilter,
  searchQuery,
  issuedCodes,
  onGenerateCode,
}: {
  job: InterviewJobSummary
  dateFilter: string
  searchQuery: string
  issuedCodes: Record<string, IssuedCode>
  onGenerateCode: (appId: string) => Promise<void>
}) {
  const [open, setOpen] = useState(false)
  const [slots, setSlots] = useState<InterviewSlotDetail[]>([])
  const [loading, setLoading] = useState(false)

  const toggle = async () => {
    if (!open && slots.length === 0 && job.totalSlots > 0) {
      setLoading(true)
      try {
        setSlots(await interviewService.getSlotsForJob(job.jobId))
      } catch {
        /* noop */
      } finally {
        setLoading(false)
      }
    }
    setOpen((v) => !v)
  }

  useEffect(() => {
    if ((dateFilter || searchQuery) && job.totalSlots > 0 && slots.length === 0) {
      toggle()
    }
  }, [dateFilter, searchQuery])

  const filteredSlots = useMemo(() => {
    if (!dateFilter) return slots
    return slots.filter((s) => isSameDay(s.startTime, dateFilter))
  }, [slots, dateFilter])

  return (
    <motion.div
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card overflow-hidden"
    >
      {/* Job Card Header */}
      <div
        onClick={toggle}
        role="button"
        tabIndex={0}
        className="w-full flex items-center justify-between p-5 lg:p-6 text-left hover:bg-ink-50/50 dark:hover:bg-white/5 transition-colors cursor-pointer"
      >
        <div className="flex items-center gap-4 min-w-0">
          <div className="w-12 h-12 shrink-0 rounded-xl bg-gradient-to-br from-violet-600 to-brand-600 flex items-center justify-center shadow-md">
            <Briefcase className="w-5 h-5 text-white" />
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap mb-1">
              <h3 className="text-base font-bold text-ink-900 dark:text-white truncate">
                {job.jobTitle}
              </h3>
              <span className="px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400">
                {job.totalSlots} ca thi
              </span>
            </div>
            <div className="flex items-center gap-4 flex-wrap text-xs text-ink-500 dark:text-ink-400">
              <span className="flex items-center gap-1 font-medium">
                <Calendar className="w-3.5 h-3.5" /> {job.totalSlots} ca phỏng vấn
              </span>
              <span className="flex items-center gap-1 font-medium">
                <Users className="w-3.5 h-3.5" /> {job.totalBooked} ứng viên đã xếp lịch
              </span>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0 ml-4">
          {open ? <ChevronDown className="w-5 h-5 text-ink-400" /> : <ChevronRight className="w-5 h-5 text-ink-400" />}
        </div>
      </div>

      {/* Slots inside Job */}
      <AnimatePresence>
        {open && (
          <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <div className="border-t border-ink-100 dark:border-white/10 p-4 lg:p-5 bg-ink-50/50 dark:bg-white/[0.02]">
              {loading ? (
                <p className="text-sm text-ink-400 text-center py-6">Đang tải các ca thi...</p>
              ) : job.totalSlots === 0 ? (
                <p className="text-sm text-ink-400 text-center py-6">Vị trí này chưa có ca thi nào.</p>
              ) : filteredSlots.length === 0 ? (
                <p className="text-sm text-ink-400 text-center py-6">
                  Không có ca thi nào vào ngày <strong className="text-ink-700 dark:text-white">{dateFilter}</strong>
                </p>
              ) : (
                <div className="space-y-3">
                  {filteredSlots.map((slot) => (
                    <SessionSlotCard
                      key={slot.slotId}
                      slot={slot}
                      searchQuery={searchQuery}
                      issuedCodes={issuedCodes}
                      onGenerateCode={onGenerateCode}
                    />
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

// ── Main RecruiterInterviewCodePage Component ─────────────────────────────────
export default function RecruiterInterviewCodePage() {
  const { t } = useTranslation('modules/recruiter/interviewCode')
  const [search, setSearch] = useState('')
  const [dateFilter, setDateFilter] = useState('')
  const [issuedCodes, setIssuedCodes] = useState<Record<string, IssuedCode>>({})
  const [actionError, setActionError] = useState('')

  // Pagination for Jobs
  const [currentPage, setCurrentPage] = useState(1)
  const pageSize = 5

  const {
    data: jobs = [],
    isLoading: loading,
    error: fetchError,
  } = useQuery({
    queryKey: ['recruiter-interview-code-jobs'],
    queryFn: async () => {
      const [allJobs, myApps] = await Promise.all([
        interviewService.getInterviewJobs(),
        applicationService.getApplications(true),
      ])
      const myJobIds = new Set(myApps.map((a) => a.jobPostingId).filter(Boolean))
      return myJobIds.size > 0 ? allJobs.filter((j) => myJobIds.has(j.jobId)) : allJobs
    },
    staleTime: 5 * 60 * 1000,
    placeholderData: keepPreviousData,
  })

  const error = actionError || (fetchError ? (fetchError as any)?.response?.data?.message || t('loadError', 'Không thể tải danh sách ca thi.') : '')

  const handleGenerateCode = async (appId: string) => {
    try {
      const res = await interviewService.generateCode(appId)
      setIssuedCodes((prev) => ({
        ...prev,
        [appId]: { code: res.code, expiresAt: res.expiresAt },
      }))
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Lỗi khi cấp mã phỏng vấn.')
      setTimeout(() => setActionError(''), 4000)
    }
  }

  const filteredJobs = useMemo(() => {
    let list = jobs
    const q = search.trim().toLowerCase()

    if (q) {
      list = list.filter((j) => j.jobTitle.toLowerCase().includes(q))
    }

    return list
  }, [jobs, search])

  const totalPages = Math.ceil(filteredJobs.length / pageSize) || 1
  const paginatedJobs = useMemo(() => {
    const start = (currentPage - 1) * pageSize
    return filteredJobs.slice(start, start + pageSize)
  }, [filteredJobs, currentPage, pageSize])

  useEffect(() => {
    setCurrentPage(1)
  }, [search, dateFilter])

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader
        title={t('title', 'Cấp mã phỏng vấn')}
        description={t('description', 'Sinh Interview Code On-site cho ứng viên check-in tại văn phòng theo từng ca thi')}
      />

      {error && <ErrorAlert message={error} onDismiss={() => setActionError('')} />}

      {/* Info Alert */}
      <div className="mb-6 flex items-start gap-3 rounded-2xl border border-violet-200 dark:border-violet-500/30 bg-gradient-to-b from-violet-50/60 dark:from-violet-500/10 to-white dark:to-white/5 p-4 text-sm text-violet-800 dark:text-violet-300 shadow-xs">
        <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-violet-600" />
        <div>
          <p className="font-semibold text-xs text-violet-900 dark:text-violet-200 mb-0.5">
            Cấp mã phòng thi theo ca thi:
          </p>
          <p className="text-xs">
            Mã dùng 1 lần, mặc định hết hạn sau 2 giờ — cấp ngay khi ứng viên đã đến văn phòng để tham gia phỏng vấn on-site.
          </p>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-ink-400" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm ứng viên, email, vị trí tuyển dụng..."
            className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-900 dark:text-white placeholder:text-ink-400 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500/40"
          />
        </div>

        {/* Date Filter */}
        <div className="relative flex items-center shrink-0">
          <CalendarDays className="absolute left-3 w-4 h-4 text-ink-400 pointer-events-none" />
          <input
            type="date"
            value={dateFilter}
            onChange={(e) => setDateFilter(e.target.value)}
            className="pl-9 pr-8 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-800 dark:text-ink-100 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-violet-500/40"
          />
          {dateFilter && (
            <button
              onClick={() => setDateFilter('')}
              title="Xóa lọc ngày"
              className="absolute right-2 p-1 text-ink-400 hover:text-ink-600 dark:hover:text-white"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
      </div>

      {/* Date Filter notification */}
      {dateFilter && (
        <div className="mb-4 p-3 rounded-xl bg-violet-50 dark:bg-violet-500/10 border border-violet-200 dark:border-violet-500/20 text-xs text-violet-700 dark:text-violet-300 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CalendarDays className="w-4 h-4 shrink-0" />
            <span>
              Đang lọc các ca thi diễn ra vào ngày <strong className="underline">{fmtDate(dateFilter)}</strong>
            </span>
          </div>
          <button onClick={() => setDateFilter('')} className="font-semibold text-violet-800 dark:text-violet-200 hover:underline">
            Xóa lọc
          </button>
        </div>
      )}

      {/* Job & Slot Groups */}
      {loading ? (
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
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
      ) : filteredJobs.length === 0 ? (
        <EmptyState
          icon={<KeyRound className="w-8 h-8 text-ink-400" />}
          title={jobs.length === 0 ? 'Chưa có ca phỏng vấn nào' : 'Không tìm thấy ca phỏng vấn phù hợp'}
          description={jobs.length === 0 ? 'Các ca thi phỏng vấn được khởi tạo từ bài đăng tuyển dụng sẽ hiển thị tại đây.' : 'Thử thay đổi từ khóa hoặc bộ lọc ngày.'}
        />
      ) : (
        <>
          <div className="space-y-4">
            {paginatedJobs.map((job) => (
              <JobSessionsGroup
                key={job.jobId}
                job={job}
                dateFilter={dateFilter}
                searchQuery={search}
                issuedCodes={issuedCodes}
                onGenerateCode={handleGenerateCode}
              />
            ))}
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="mt-8 flex flex-col sm:flex-row items-center justify-between gap-4 pt-4 border-t border-ink-200 dark:border-white/10">
              <p className="text-xs text-ink-500 dark:text-ink-400 font-medium">
                Hiển thị {paginatedJobs.length} trên tổng số {filteredJobs.length} vị trí (Trang {currentPage}/{totalPages})
              </p>
              <div className="flex items-center gap-1.5">
                <button
                  disabled={currentPage === 1}
                  onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                  className="p-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 disabled:opacity-40 transition-colors"
                  title="Trang trước"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>

                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <button
                    key={page}
                    onClick={() => setCurrentPage(page)}
                    className={`w-8 h-8 rounded-xl text-xs font-bold transition-all ${currentPage === page
                        ? 'bg-violet-600 text-white shadow-sm'
                        : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10'
                      }`}
                  >
                    {page}
                  </button>
                ))}

                <button
                  disabled={currentPage === totalPages}
                  onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
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
