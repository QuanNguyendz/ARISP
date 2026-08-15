import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { AlertCircle, Loader2, RefreshCw, X } from 'lucide-react'
import { interviewService, type SlotCandidate } from '@ari/shared/fservices/interview'
import { interviewKeys } from './interviewQueryKeys'
import { fmtDate, fmtTime } from './format'
import { INTERVIEWS_NS } from './workspaceConfig'

/**
 * Dời một hoặc nhiều ứng viên sang ca khác.
 *
 * Điểm khác bản cũ:
 *  - Danh sách ca đích được ĐỌC TỪ CACHE react-query (`interviewKeys.slots`) thay vì truyền qua prop
 *    xuyên ba tầng component. Đây chính là lỗi người dùng báo: mảng ca cũ được nạp một lần lúc mở
 *    thẻ tin rồi giữ nguyên, nên tăng sức chứa ở màn cấu hình lịch xong modal vẫn hiện số cũ.
 *  - Gửi MỘT request cho cả nhóm (được ăn cả ngã về không) thay vì N request tuần tự.
 *  - Đọc `seatsAvailable` do server tính sẵn, không tự trừ `capacity - bookedCount`.
 */
export function RescheduleModal({
  candidates,
  jobId,
  currentSlotId,
  currentRoundNumber,
  onClose,
  onSuccess,
}: {
  candidates: SlotCandidate[]
  jobId: string
  currentSlotId: string
  currentRoundNumber: number
  onClose: () => void
  onSuccess: () => void
}) {
  const { t } = useTranslation(INTERVIEWS_NS)
  const [selectedSlotId, setSelectedSlotId] = useState<string>('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [failures, setFailures] = useState<Array<{ name: string; message: string }>>([])

  const { data: slots = [], isLoading } = useQuery({
    queryKey: interviewKeys.slots(jobId),
    queryFn: () => interviewService.getSlotsForJob(jobId),
    staleTime: 30_000,
  })

  const validSlots = useMemo(
    () =>
      slots.filter(
        (s) =>
          s.slotId !== currentSlotId &&
          !s.isPast &&
          s.roundNumber === currentRoundNumber &&
          s.seatsAvailable >= candidates.length
      ),
    [slots, currentSlotId, currentRoundNumber, candidates.length]
  )

  const handleConfirm = async () => {
    if (!selectedSlotId || candidates.length === 0) return
    setSubmitting(true)
    setError(null)
    setFailures([])
    try {
      const res = await interviewService.rescheduleBookings(
        candidates.map((c) => c.bookingId),
        selectedSlotId
      )

      if (res.failed?.length) {
        setFailures(
          res.failed.map((f) => ({
            name:
              candidates.find((c) => c.bookingId === f.bookingId)?.candidateName ??
              t('reschedule.fallbackName'),
            message: f.message,
          }))
        )
      }

      if (res.movedCount > 0) {
        onSuccess()
        // Còn người thất bại thì GIỮ modal mở để nhân sự đọc lý do từng người.
        if (!res.failed?.length) onClose()
      } else if (!res.failed?.length) {
        setError(t('reschedule.noneMoved'))
      }
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } } }
      setError(e?.response?.data?.message || t('reschedule.error'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
      <motion.div
        initial={{ scale: 0.95, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        className="w-full max-w-md bg-white dark:bg-ink-900 border border-ink-200 dark:border-white/10 rounded-2xl shadow-xl p-6 space-y-4"
      >
        <div className="flex items-center justify-between border-b border-ink-100 dark:border-white/10 pb-3">
          <h3 className="font-bold text-base text-ink-900 dark:text-white flex items-center gap-2">
            <RefreshCw className="w-4 h-4 text-brand-500" />
            {t('reschedule.title', { count: candidates.length })}
          </h3>
          <button onClick={onClose} className="text-ink-400 hover:text-ink-600 dark:hover:text-white">
            <X className="w-5 h-5" />
          </button>
        </div>

        {error && (
          <p className="text-xs text-red-600 bg-red-50 dark:bg-red-500/10 p-2.5 rounded-xl border border-red-200 dark:border-red-500/20">
            {error}
          </p>
        )}

        {failures.length > 0 && (
          <div className="rounded-xl border border-amber-200 dark:border-amber-500/20 bg-amber-50 dark:bg-amber-500/10 p-2.5 space-y-1">
            <p className="text-xs font-bold text-amber-800 dark:text-amber-300 flex items-center gap-1.5">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" />{' '}
              {t('reschedule.failed', { count: failures.length })}
            </p>
            {failures.map((f, i) => (
              <p key={i} className="text-[11px] text-amber-700 dark:text-amber-400">
                <strong>{f.name}:</strong> {f.message}
              </p>
            ))}
          </div>
        )}

        <div className="text-xs text-ink-500 dark:text-ink-400 space-y-1">
          <p>
            {t('reschedule.selectedCandidates')}{' '}
            <strong className="text-ink-900 dark:text-white">
              {candidates.map((c) => c.candidateName).join(', ')}
            </strong>
          </p>
          <p>
            {t('reschedule.roundLabel')}{' '}
            <strong>{t('reschedule.roundValue', { number: currentRoundNumber })}</strong>
          </p>
        </div>

        <div className="space-y-2">
          <label className="block text-xs font-semibold text-ink-700 dark:text-ink-200">
            {t('reschedule.chooseSlot', {
              round: currentRoundNumber,
              count: candidates.length,
            })}
          </label>

          {isLoading ? (
            <p className="text-xs text-ink-400 flex items-center gap-2 py-3">
              <Loader2 className="w-3.5 h-3.5 animate-spin" /> {t('reschedule.loadingSlots')}
            </p>
          ) : validSlots.length === 0 ? (
            <p className="text-xs text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-500/10 p-3 rounded-xl border border-amber-200 dark:border-amber-500/20">
              {t('reschedule.noSlots', { round: currentRoundNumber, count: candidates.length })}
            </p>
          ) : (
            <div className="max-h-48 overflow-y-auto space-y-2 pr-1">
              {validSlots.map((s) => (
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
                        {t('reschedule.slotLine', {
                          round: s.roundNumber,
                          date: fmtDate(s.startTime),
                        })}
                      </p>
                      <p className="text-xs text-ink-500">
                        {fmtTime(s.startTime)} – {fmtTime(s.endTime)}
                      </p>
                    </div>
                  </div>
                  <span className="text-[11px] font-medium text-ink-400">
                    {t('reschedule.seats', {
                      available: s.seatsAvailable,
                      capacity: s.capacity,
                    })}
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
            {failures.length > 0 ? t('reschedule.close') : t('reschedule.cancel')}
          </button>
          <button
            disabled={!selectedSlotId || submitting}
            onClick={handleConfirm}
            className="px-4 py-2 text-xs font-semibold text-white bg-brand-600 hover:bg-brand-700 disabled:opacity-50 rounded-xl transition-colors inline-flex items-center gap-1.5"
          >
            {submitting && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
            {submitting
              ? t('reschedule.submitting')
              : t('reschedule.confirm', { count: candidates.length })}
          </button>
        </div>
      </motion.div>
    </div>
  )
}
