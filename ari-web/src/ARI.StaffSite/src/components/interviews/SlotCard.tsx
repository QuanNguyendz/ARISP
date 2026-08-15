import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import {
  AlertCircle,
  AlertTriangle,
  Bell,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  KeyRound,
  RefreshCw,
  UserX,
  Users,
  XCircle,
} from 'lucide-react'
import { applicationService } from '@ari/shared/fservices/application'
import { interviewService, type InterviewSlotDetail } from '@ari/shared/fservices/interview'
import { CandidateRow } from './CandidateRow'
import { RescheduleModal } from './RescheduleModal'
import { interviewKeys } from './interviewQueryKeys'
import { isSelectable, needsCode } from './candidateState'
import { fmtDate, fmtTime } from './format'
import type { WorkspaceConfig } from './workspaceConfig'

export function SlotCard({
  slot,
  jobId,
  workspace,
}: {
  slot: InterviewSlotDetail
  jobId: string
  workspace: WorkspaceConfig
}) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [selectedBookingIds, setSelectedBookingIds] = useState<string[]>([])
  const [toast, setToast] = useState<string | null>(null)
  const [showBatchReschedule, setShowBatchReschedule] = useState(false)

  // Nạp qua react-query (trước đây là useState nạp một lần): thu gọn rồi mở lại là nạp lại, và
  // realtime / thao tác nơi khác huỷ hiệu lực được.
  const { data: candidates = [], isLoading } = useQuery({
    queryKey: interviewKeys.candidates(slot.slotId),
    queryFn: () => interviewService.getCandidatesInSlot(slot.slotId),
    enabled: open,
    staleTime: 30_000,
  })

  const reload = () => {
    queryClient.invalidateQueries({ queryKey: interviewKeys.candidates(slot.slotId) })
    queryClient.invalidateQueries({ queryKey: interviewKeys.slots(jobId) })
    queryClient.invalidateQueries({ queryKey: interviewKeys.jobs })
  }

  const selectable = useMemo(() => candidates.filter(isSelectable), [candidates])
  const candidatesNeedingCode = useMemo(() => candidates.filter(needsCode), [candidates])
  const selectedCandidates = useMemo(
    () => candidates.filter((c) => selectedBookingIds.includes(c.bookingId)),
    [candidates, selectedBookingIds]
  )

  const flash = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 4000)
  }

  const batchCodes = useMutation({
    mutationFn: () =>
      interviewService.generateCodeBatch(
        selectedCandidates.map((c) => c.applicationId),
        slot.roundNumber
      ),
    onSuccess: (results) => {
      flash(`Đã cấp mã phỏng vấn cho ${results?.length ?? selectedCandidates.length} ứng viên.`)
      setSelectedBookingIds([])
      reload()
    },
    onError: (e: unknown) => {
      const err = e as { response?: { data?: { message?: string } } }
      flash(err?.response?.data?.message || 'Không thể cấp mã phỏng vấn hàng loạt.')
    },
  })

  const batchRemind = useMutation({
    mutationFn: async () => {
      let ok = 0
      for (const id of selectedBookingIds) {
        try {
          await interviewService.sendBookingReminder(id)
          ok++
        } catch {
          /* tiếp tục với người còn lại */
        }
      }
      return ok
    },
    onSuccess: (ok) => {
      flash(`Đã gửi mail nhắc lịch cho ${ok}/${selectedBookingIds.length} ứng viên.`)
      setSelectedBookingIds([])
    },
  })

  const batchReject = useMutation({
    mutationFn: async () => {
      let ok = 0
      for (const c of selectedCandidates) {
        try {
          await applicationService.rejectApplication(c.applicationId)
          ok++
        } catch {
          /* tiếp tục với người còn lại */
        }
      }
      return ok
    },
    onSuccess: (ok) => {
      flash(`Đã loại ${ok}/${selectedCandidates.length} ứng viên.`)
      setSelectedBookingIds([])
      reload()
    },
  })

  const busy = batchCodes.isPending || batchRemind.isPending || batchReject.isPending

  const toggleSelectAll = () => {
    if (selectedBookingIds.length === selectable.length && selectable.length > 0) setSelectedBookingIds([])
    else setSelectedBookingIds(selectable.map((c) => c.bookingId))
  }

  // Kẹp ở 100%: ca vượt sức chứa (dữ liệu cũ bị lệch) từng vẽ thanh tràn ra ngoài khung.
  const fillPct = slot.capacity > 0 ? Math.min(100, Math.round((slot.bookedCount / slot.capacity) * 100)) : 0

  return (
    <div
      className={`rounded-xl border transition-all ${
        slot.isPast ? 'border-ink-200 dark:border-white/10 opacity-75' : 'border-brand-200 dark:border-brand-500/30'
      } bg-white dark:bg-white/5 overflow-hidden`}
    >
      <button
        onClick={() => setOpen((v) => !v)}
        className="w-full flex items-center justify-between p-4 text-left hover:bg-ink-50 dark:hover:bg-white/5 transition-colors"
      >
        <div className="flex items-center gap-3 min-w-0">
          <div
            className={`w-2.5 h-2.5 rounded-full flex-shrink-0 ${
              slot.isPast ? 'bg-ink-300' : 'bg-emerald-500 animate-pulse'
            }`}
          />
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-sm font-bold text-ink-900 dark:text-white">
                {fmtDate(slot.startTime)} · {fmtTime(slot.startTime)} – {fmtTime(slot.endTime)}
              </span>
              <span className="px-2 py-0.5 rounded-md text-[11px] font-semibold bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400">
                Vòng {slot.roundNumber}
              </span>
              {slot.isPast && (
                <span className="text-[11px] text-ink-400 bg-ink-100 dark:bg-white/10 px-2 py-0.5 rounded-md">Đã qua</span>
              )}
              {slot.isOverCapacity && (
                <span
                  className="inline-flex items-center gap-1 text-[11px] font-semibold text-amber-700 dark:text-amber-400 bg-amber-100 dark:bg-amber-500/20 px-2 py-0.5 rounded-md"
                  title="Số người đang giữ chỗ nhiều hơn sức chứa. Hãy tăng sức chứa hoặc dời bớt ứng viên sang ca khác."
                >
                  <AlertTriangle className="w-3 h-3" /> Vượt sức chứa
                </span>
              )}
            </div>
            <div className="flex items-center gap-3 mt-1.5 flex-wrap">
              <span
                className="text-xs text-ink-500 dark:text-ink-400 flex items-center gap-1 font-medium"
                title="Số ứng viên đang GIỮ CHỖ / sức chứa của ca"
              >
                <Users className="w-3.5 h-3.5" /> {slot.bookedCount}/{slot.capacity} ứng viên
              </span>
              {slot.confirmedCount > 0 && (
                <span className="text-xs text-emerald-600 dark:text-emerald-400 flex items-center gap-1 font-medium">
                  <CheckCircle2 className="w-3.5 h-3.5" /> {slot.confirmedCount} xác nhận
                </span>
              )}
              {slot.pendingCount > 0 && (
                <span className="text-xs text-amber-500 dark:text-amber-400 flex items-center gap-1 font-medium">
                  <AlertCircle className="w-3.5 h-3.5" /> {slot.pendingCount} chờ
                </span>
              )}
              {/* Đã trả chỗ → tách khỏi phân số bên trên, để dấu · cho thấy đây là nhóm khác. */}
              {(slot.declinedCount > 0 || slot.cancelledCount > 0) && (
                <span
                  className="text-xs text-ink-400 flex items-center gap-1"
                  title="Ứng viên từ chối hoặc bị loại đã trả lại chỗ nên không tính vào phân số bên trái."
                >
                  <XCircle className="w-3.5 h-3.5" />
                  {slot.declinedCount > 0 && `${slot.declinedCount} từ chối`}
                  {slot.declinedCount > 0 && slot.cancelledCount > 0 && ' · '}
                  {slot.cancelledCount > 0 && `${slot.cancelledCount} đã loại`}
                  <span className="hidden sm:inline"> (đã trả chỗ)</span>
                </span>
              )}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-3 flex-shrink-0">
          <div className="hidden sm:block w-20 h-1.5 rounded-full bg-ink-100 dark:bg-white/10 overflow-hidden">
            <div
              className={`h-full rounded-full transition-all ${slot.isOverCapacity ? 'bg-amber-500' : 'bg-brand-500'}`}
              style={{ width: `${fillPct}%` }}
            />
          </div>
          {open ? <ChevronDown className="w-4 h-4 text-ink-400" /> : <ChevronRight className="w-4 h-4 text-ink-400" />}
        </div>
      </button>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
          >
            <div className="border-t border-ink-100 dark:border-white/10 px-2 py-2">
              {isLoading ? (
                <p className="text-sm text-ink-400 text-center py-4">Đang tải ứng viên...</p>
              ) : candidates.length === 0 ? (
                <p className="text-sm text-ink-400 text-center py-4">Chưa có ứng viên nào trong ca phỏng vấn này.</p>
              ) : (
                <>
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 px-4 py-2.5 bg-ink-50/80 dark:bg-white/5 rounded-xl mb-3 border border-ink-100 dark:border-white/10">
                    <div className="flex items-center gap-3 flex-wrap">
                      <label className="flex items-center gap-2 text-xs font-semibold text-ink-700 dark:text-ink-200 cursor-pointer">
                        <input
                          type="checkbox"
                          disabled={selectable.length === 0}
                          checked={selectable.length > 0 && selectedBookingIds.length === selectable.length}
                          onChange={toggleSelectAll}
                          className="w-4 h-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500 cursor-pointer disabled:opacity-40"
                        />
                        <span>
                          {selectedBookingIds.length > 0
                            ? `Đã chọn (${selectedBookingIds.length}/${selectable.length})`
                            : 'Chọn tất cả chưa loại'}
                        </span>
                      </label>

                      {candidatesNeedingCode.length > 0 && (
                        <button
                          type="button"
                          onClick={() => setSelectedBookingIds(candidatesNeedingCode.map((c) => c.bookingId))}
                          className="px-2.5 py-1 rounded-md bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-300 hover:bg-violet-200 text-xs font-semibold transition-colors"
                        >
                          ⚡ Chọn nhanh {candidatesNeedingCode.length} người chưa có mã
                        </button>
                      )}
                    </div>

                    {selectedBookingIds.length > 0 && (
                      <div className="flex items-center gap-2 flex-wrap">
                        <button
                          disabled={busy}
                          onClick={() => batchCodes.mutate()}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-violet-600 hover:bg-violet-700 text-white text-xs font-bold shadow-xs transition-all disabled:opacity-50"
                        >
                          <KeyRound className="w-3.5 h-3.5 shrink-0" />
                          <span>Cấp mã hàng loạt ({selectedBookingIds.length})</span>
                        </button>
                        <button
                          disabled={busy}
                          onClick={() => {
                            if (window.confirm(`Loại ${selectedBookingIds.length} ứng viên khỏi quy trình tuyển dụng?`))
                              batchReject.mutate()
                          }}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-red-600 hover:bg-red-700 text-white text-xs font-bold shadow-xs transition-all disabled:opacity-50"
                        >
                          <UserX className="w-3.5 h-3.5 shrink-0" />
                          <span>Loại hàng loạt ({selectedBookingIds.length})</span>
                        </button>
                        <button
                          disabled={busy}
                          onClick={() => batchRemind.mutate()}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-600 hover:bg-amber-700 text-white text-xs font-bold shadow-xs transition-all disabled:opacity-50"
                        >
                          <Bell className="w-3.5 h-3.5 shrink-0" />
                          <span>Nhắc lịch hàng loạt ({selectedBookingIds.length})</span>
                        </button>
                        <button
                          disabled={busy}
                          onClick={() => setShowBatchReschedule(true)}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-xs transition-all disabled:opacity-50"
                        >
                          <RefreshCw className="w-3.5 h-3.5 shrink-0" />
                          <span>Dời lịch hàng loạt ({selectedBookingIds.length})</span>
                        </button>
                      </div>
                    )}
                  </div>

                  {toast && (
                    <div className="mx-4 mb-2 p-2 rounded-lg bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 text-xs font-semibold">
                      {toast}
                    </div>
                  )}

                  <div className="divide-y divide-ink-100 dark:divide-white/5">
                    {candidates.map((c) => (
                      <CandidateRow
                        key={c.bookingId}
                        c={c}
                        jobId={jobId}
                        currentSlotId={slot.slotId}
                        currentRoundNumber={slot.roundNumber}
                        isSelected={selectedBookingIds.includes(c.bookingId)}
                        onToggleSelect={() =>
                          setSelectedBookingIds((prev) =>
                            prev.includes(c.bookingId)
                              ? prev.filter((id) => id !== c.bookingId)
                              : [...prev, c.bookingId]
                          )
                        }
                        onReload={reload}
                        workspace={workspace}
                      />
                    ))}
                  </div>

                  {showBatchReschedule && (
                    <RescheduleModal
                      candidates={selectedCandidates}
                      jobId={jobId}
                      currentSlotId={slot.slotId}
                      currentRoundNumber={slot.roundNumber}
                      onClose={() => setShowBatchReschedule(false)}
                      onSuccess={() => {
                        setSelectedBookingIds([])
                        reload()
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
