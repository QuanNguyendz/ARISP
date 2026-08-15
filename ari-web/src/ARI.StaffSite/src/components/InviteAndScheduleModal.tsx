import { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import { X, Calendar, Send, CheckCircle2, Users, AlertCircle, Loader2 } from 'lucide-react'
import { scheduleService } from '@ari/shared/fservices/schedule'
import { applicationService } from '@ari/shared/fservices/application'
import type { AvailabilitySlot } from '@ari/shared/types/job'

export interface InviteTargetApplication {
  id: string
  name: string
}

interface InviteAndScheduleModalProps {
  /** Một hoặc nhiều hồ sơ cùng nhận một khung giờ (duyệt hàng loạt dùng chung 1 ca). */
  applications: InviteTargetApplication[]
  jobPostingId: string
  /**
   * `accept` = hồ sơ chưa qua vòng CV: duyệt + xếp lịch vòng 1 trong một lần gọi.
   * `assign` = hồ sơ đã qua CV: chỉ xếp lịch cho vòng đang tới.
   */
  mode: 'accept' | 'assign'
  targetRoundNumber?: number
  onClose: () => void
  onSuccess: (message: string) => void
}

function fmtSlotTime(iso: string | Date) {
  const d = new Date(iso)
  const dateStr = d.toLocaleDateString('vi-VN', {
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
  const timeStr = d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  return { dateStr, timeStr }
}

/**
 * Duyệt CV / xếp lịch — LUÔN kèm một khung giờ cụ thể.
 *
 * Trước đây modal có thêm lựa chọn "gửi lời mời chung (xếp sau)": ứng viên được duyệt nhưng
 * không có giờ hẹn nên **không nhận được email nào**, chỉ có chuông trong Portal. Bỏ hẳn lựa chọn
 * đó — duyệt là phải có lịch, và thư mời kèm giờ hẹn do backend gửi trong cùng thao tác.
 */
export default function InviteAndScheduleModal({
  applications,
  jobPostingId,
  mode,
  targetRoundNumber = 1,
  onClose,
  onSuccess,
}: InviteAndScheduleModalProps) {
  const [slots, setSlots] = useState<AvailabilitySlot[]>([])
  const [loadingSlots, setLoadingSlots] = useState(true)
  const [selectedSlotId, setSelectedSlotId] = useState<string>('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Duyệt CV luôn gắn với vòng 1; xếp lịch vòng sau mới dùng targetRoundNumber.
  const effectiveRound = mode === 'accept' ? 1 : targetRoundNumber > 0 ? targetRoundNumber : 1
  const isBatch = applications.length > 1
  const selectedSlot = slots.find((s) => s.id === selectedSlotId)
  const remainingSeats = selectedSlot ? selectedSlot.capacity - selectedSlot.bookedCount : 0
  const notEnoughSeats = !!selectedSlot && applications.length > remainingSeats

  useEffect(() => {
    let isMounted = true
    async function load() {
      try {
        setLoadingSlots(true)
        const data = await scheduleService.getSlots(jobPostingId, effectiveRound)
        // Chỉ ca của đúng vòng, chưa trôi qua và còn chỗ.
        const now = new Date()
        const valid = data.filter(
          (s) =>
            (s.roundNumber == null || s.roundNumber === effectiveRound) &&
            new Date(s.startTime) > now &&
            s.bookedCount < s.capacity
        )
        if (isMounted) {
          setSlots(valid)
          if (valid.length > 0) setSelectedSlotId(valid[0].id)
        }
      } catch {
        if (isMounted) setError('Không thể tải danh sách ca phỏng vấn.')
      } finally {
        if (isMounted) setLoadingSlots(false)
      }
    }
    load()
    return () => {
      isMounted = false
    }
  }, [jobPostingId, effectiveRound])

  const handleConfirm = async () => {
    if (!selectedSlot) {
      setError('Vui lòng chọn ca phỏng vấn.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      // Tuần tự chứ không song song: sức chứa ca là tài nguyên tranh chấp, chạy song song thì
      // các lỗi "hết chỗ" trả về cùng lúc và không biết ai đã vào được ai chưa.
      const failures: string[] = []
      let success = 0
      for (const app of applications) {
        try {
          if (mode === 'accept') {
            await applicationService.acceptApplication(app.id, selectedSlot.id)
          } else {
            await scheduleService.assign({
              applicationId: app.id,
              slotId: selectedSlot.id,
              round: selectedSlot.roundNumber ?? effectiveRound,
            })
          }
          success++
        } catch (err) {
          const e = err as { response?: { data?: { message?: string } } }
          failures.push(`${app.name}: ${e?.response?.data?.message || 'lỗi không xác định'}`)
        }
      }

      if (success === 0) {
        setError(failures.join(' | '))
        return
      }

      const verb = mode === 'accept' ? 'Đã duyệt CV và xếp lịch' : 'Đã xếp lịch'
      onSuccess(
        failures.length === 0
          ? `${verb} cho ${success} ứng viên. Thư mời kèm giờ hẹn đã được gửi.`
          : `${verb} cho ${success} ứng viên; ${failures.length} hồ sơ chưa xong (${failures.join(' | ')}).`
      )
      onClose()
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
        className="w-full max-w-lg bg-white dark:bg-ink-900 border border-ink-200 dark:border-white/10 rounded-2xl shadow-2xl overflow-hidden"
      >
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-ink-100 dark:border-white/10 bg-gradient-to-r from-brand-50/50 to-transparent dark:from-brand-500/10">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-xl bg-brand-500/10 text-brand-600 dark:text-brand-400">
              <Calendar className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-ink-900 dark:text-white">
                {mode === 'accept' ? 'Duyệt CV & xếp lịch phỏng vấn' : 'Xếp lịch phỏng vấn'}
              </h3>
              <p className="text-xs text-ink-500 dark:text-ink-400">
                {isBatch ? (
                  <>
                    <span className="font-semibold text-brand-600 dark:text-brand-400">
                      {applications.length} ứng viên
                    </span>{' '}
                    được chọn
                  </>
                ) : (
                  <>
                    Ứng viên:{' '}
                    <span className="font-semibold text-brand-600 dark:text-brand-400">
                      {applications[0]?.name}
                    </span>
                  </>
                )}
              </p>
            </div>
          </div>
          <button onClick={onClose} className="p-1 text-ink-400 hover:text-ink-600 rounded-lg">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-4">
          <p className="text-xs text-ink-600 dark:text-ink-300 bg-ink-50 dark:bg-white/5 border border-ink-100 dark:border-white/5 rounded-xl p-3">
            {mode === 'accept'
              ? 'Chọn khung giờ vòng 1 cho ứng viên. Hệ thống sẽ duyệt hồ sơ, giữ chỗ và gửi thư mời phỏng vấn kèm giờ hẹn + địa điểm để ứng viên xác nhận hoặc báo bận.'
              : 'Chọn khung giờ cho vòng phỏng vấn tiếp theo. Ứng viên sẽ nhận thư mời kèm giờ hẹn để xác nhận hoặc báo bận.'}
          </p>

          {error && (
            <div className="p-3 rounded-xl bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-300 text-xs flex items-start gap-2 border border-red-200 dark:border-red-500/20">
              <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          <div className="space-y-3">
            <div className="flex items-center justify-between text-xs font-semibold text-ink-700 dark:text-ink-300">
              <span>Chọn ca phỏng vấn khả dụng:</span>
              <span className="px-2 py-0.5 rounded bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 font-bold text-[11px]">
                Vòng {effectiveRound}
              </span>
            </div>

            {loadingSlots ? (
              <div className="flex items-center justify-center py-8 text-ink-400 text-xs gap-2">
                <Loader2 className="w-4 h-4 animate-spin text-brand-500" />
                <span>Đang tải ca phỏng vấn vòng {effectiveRound}...</span>
              </div>
            ) : slots.length === 0 ? (
              <div className="p-4 rounded-xl bg-amber-50 dark:bg-amber-500/10 text-amber-800 dark:text-amber-300 text-xs space-y-2 border border-amber-200 dark:border-amber-500/20">
                <p className="font-bold flex items-center gap-1.5">
                  <AlertCircle className="w-4 h-4 text-amber-600" />
                  Chưa có ca phỏng vấn trống nào cho vòng {effectiveRound}
                </p>
                <p className="text-amber-700 dark:text-amber-400">
                  Hãy mở thêm khung giờ trong màn Phỏng vấn của tin này rồi quay lại duyệt hồ sơ —
                  duyệt mà không có lịch thì ứng viên sẽ không nhận được thư mời.
                </p>
              </div>
            ) : (
              <div className="max-h-60 overflow-y-auto space-y-2 pr-1">
                {slots.map((s) => {
                  const { dateStr, timeStr } = fmtSlotTime(s.startTime)
                  const end = fmtSlotTime(s.endTime).timeStr
                  const isSelected = selectedSlotId === s.id
                  return (
                    <div
                      key={s.id}
                      onClick={() => setSelectedSlotId(s.id)}
                      className={`p-3 rounded-xl border transition-all cursor-pointer flex items-center justify-between ${
                        isSelected
                          ? 'border-brand-500 bg-brand-50/70 dark:bg-brand-500/15 shadow-sm'
                          : 'border-ink-200 dark:border-white/10 hover:border-ink-300 dark:hover:border-white/20 bg-white dark:bg-white/5'
                      }`}
                    >
                      <div className="space-y-1">
                        <div className="flex items-center gap-2">
                          <span className="px-2 py-0.5 rounded-md text-[10px] font-bold bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400">
                            Vòng {s.roundNumber}
                          </span>
                          <span className="text-xs font-bold text-ink-900 dark:text-white">
                            {dateStr}
                          </span>
                        </div>
                        <p className="text-xs text-ink-600 dark:text-ink-300 font-medium">
                          {timeStr} – {end}
                        </p>
                      </div>

                      <div className="flex items-center gap-3">
                        <span className="text-[11px] font-medium text-ink-500 dark:text-ink-400 flex items-center gap-1">
                          <Users className="w-3.5 h-3.5" /> {s.bookedCount}/{s.capacity}
                        </span>
                        <div
                          className={`w-4 h-4 rounded-full border flex items-center justify-center ${
                            isSelected ? 'border-brand-600 bg-brand-600 text-white' : 'border-ink-300'
                          }`}
                        >
                          {isSelected && <CheckCircle2 className="w-3 h-3" />}
                        </div>
                      </div>
                    </div>
                  )
                })}
              </div>
            )}

            {notEnoughSeats && (
              <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-500/10 text-amber-800 dark:text-amber-300 text-xs border border-amber-200 dark:border-amber-500/20">
                Ca này chỉ còn <strong>{remainingSeats}</strong> chỗ cho{' '}
                <strong>{applications.length}</strong> ứng viên đã chọn. Những hồ sơ vượt sức chứa sẽ
                báo lỗi — hãy tăng sức chứa ca hoặc duyệt làm nhiều đợt.
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-ink-100 dark:border-white/10 bg-ink-50/50 dark:bg-white/5 flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-xl text-xs font-semibold text-ink-600 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 transition-colors"
          >
            Hủy
          </button>
          <button
            type="button"
            disabled={submitting || !selectedSlotId || slots.length === 0}
            onClick={handleConfirm}
            className="flex items-center gap-2 px-5 py-2 rounded-xl text-xs font-bold bg-brand-600 hover:bg-brand-700 text-white shadow-md shadow-brand-600/20 transition-colors disabled:opacity-50"
          >
            {submitting ? (
              <>
                <Loader2 className="w-3.5 h-3.5 animate-spin" />
                <span>Đang xử lý...</span>
              </>
            ) : (
              <>
                <Send className="w-3.5 h-3.5" />
                <span>{mode === 'accept' ? 'Duyệt & gửi thư mời' : 'Xếp lịch & gửi thư mời'}</span>
              </>
            )}
          </button>
        </div>
      </motion.div>
    </div>
  )
}
