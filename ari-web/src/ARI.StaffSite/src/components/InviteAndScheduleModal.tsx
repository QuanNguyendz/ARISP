import { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import { X, Calendar, Send, CheckCircle2, Clock, Users, AlertCircle, Loader2 } from 'lucide-react'
import { scheduleService } from '@ari/shared/fservices/schedule'
import { applicationService } from '@ari/shared/fservices/application'
import type { AvailabilitySlot } from '@ari/shared/types/job'

interface InviteAndScheduleModalProps {
  applicationId: string
  candidateName: string
  jobPostingId: string
  targetRoundNumber?: number
  onClose: () => void
  onSuccess: (message: string) => void
}

function fmtSlotTime(iso: string | Date) {
  const d = new Date(iso)
  const dateStr = d.toLocaleDateString('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' })
  const timeStr = d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  return { dateStr, timeStr }
}

export default function InviteAndScheduleModal({
  applicationId,
  candidateName,
  jobPostingId,
  targetRoundNumber = 1,
  onClose,
  onSuccess,
}: InviteAndScheduleModalProps) {
  const [slots, setSlots] = useState<AvailabilitySlot[]>([])
  const [loadingSlots, setLoadingSlots] = useState(true)
  const [selectedSlotId, setSelectedSlotId] = useState<string>('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<'schedule' | 'direct'>('schedule')

  const effectiveRound = targetRoundNumber > 0 ? targetRoundNumber : 1

  useEffect(() => {
    let isMounted = true
    async function load() {
      try {
        setLoadingSlots(true)
        const data = await scheduleService.getSlots(jobPostingId, effectiveRound)
        // Lọc các ca chưa trôi qua trong quá khứ, chưa đầy và đúng Vòng thi
        const now = new Date()
        const valid = data.filter(
          (s) =>
            (s.roundNumber == null || s.roundNumber === effectiveRound) &&
            new Date(s.startTime) > now &&
            s.bookedCount < s.capacity
        )
        if (isMounted) {
          setSlots(valid)
          if (valid.length > 0) {
            setSelectedSlotId(valid[0].id)
          }
        }
      } catch (err: any) {
        if (isMounted) setError('Không thể tải danh sách ca phỏng vấn.')
      } finally {
        if (isMounted) setLoadingSlots(false)
      }
    }
    load()
    return () => { isMounted = false }
  }, [jobPostingId, effectiveRound])

  const handleConfirm = async () => {
    setSubmitting(true)
    setError(null)
    try {
      if (activeTab === 'schedule' && selectedSlotId) {
        const slot = slots.find(s => s.id === selectedSlotId)
        if (!slot) {
          setError('Vui lòng chọn ca phỏng vấn hợp lệ.')
          setSubmitting(false)
          return
        }
        // Gán ca phỏng vấn trước
        await scheduleService.assign({
          applicationId,
          slotId: selectedSlotId,
          round: slot.roundNumber ?? 1,
        })
        onSuccess(`Đã gán ca phỏng vấn thành công và gửi lời mời đến ứng viên ${candidateName}!`)
      } else {
        // Gửi lời mời chung (chưa gán ca)
        await applicationService.sendInvite(applicationId)
        onSuccess(`Đã gửi lời mời phỏng vấn đến ứng viên ${candidateName}!`)
      }
      onClose()
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Có lỗi xảy ra khi xử lý lời mời phỏng vấn.')
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
                Mời Phỏng Vấn & Xếp Lịch
              </h3>
              <p className="text-xs text-ink-500 dark:text-ink-400">
                Ứng viên: <span className="font-semibold text-brand-600 dark:text-brand-400">{candidateName}</span>
              </p>
            </div>
          </div>
          <button onClick={onClose} className="p-1 text-ink-400 hover:text-ink-600 rounded-lg">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tab selection */}
        <div className="p-5 space-y-4">
          <div className="grid grid-cols-2 gap-2 p-1 bg-ink-50 dark:bg-white/5 rounded-xl border border-ink-200/50 dark:border-white/5">
            <button
              type="button"
              onClick={() => setActiveTab('schedule')}
              className={`py-2 px-3 rounded-lg text-xs font-bold transition-all flex items-center justify-center gap-1.5 ${
                activeTab === 'schedule'
                  ? 'bg-white dark:bg-ink-800 text-brand-600 dark:text-brand-400 shadow-sm'
                  : 'text-ink-500 hover:text-ink-900 dark:text-ink-400 dark:hover:text-white'
              }`}
            >
              <Clock className="w-4 h-4" />
              <span>Mời & Xếp ca phỏng vấn</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('direct')}
              className={`py-2 px-3 rounded-lg text-xs font-bold transition-all flex items-center justify-center gap-1.5 ${
                activeTab === 'direct'
                  ? 'bg-white dark:bg-ink-800 text-brand-600 dark:text-brand-400 shadow-sm'
                  : 'text-ink-500 hover:text-ink-900 dark:text-ink-400 dark:hover:text-white'
              }`}
            >
              <Send className="w-4 h-4" />
              <span>Gửi lời mời chung (Xếp sau)</span>
            </button>
          </div>

          {error && (
            <div className="p-3 rounded-xl bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-300 text-xs flex items-center gap-2 border border-red-200 dark:border-red-500/20">
              <AlertCircle className="w-4 h-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {activeTab === 'schedule' ? (
            <div className="space-y-3">
              <label className="block text-xs font-semibold text-ink-700 dark:text-ink-300 flex items-center justify-between">
                <span>Chọn Ca Phỏng Vấn Khả Dụng:</span>
                <span className="px-2 py-0.5 rounded bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 font-bold text-[11px]">
                  Vòng {effectiveRound}
                </span>
              </label>

              {loadingSlots ? (
                <div className="flex items-center justify-center py-8 text-ink-400 text-xs gap-2">
                  <Loader2 className="w-4 h-4 animate-spin text-brand-500" />
                  <span>Đang tải ca phỏng vấn khả dụng Vòng {effectiveRound}...</span>
                </div>
              ) : slots.length === 0 ? (
                <div className="p-4 rounded-xl bg-amber-50 dark:bg-amber-500/10 text-amber-800 dark:text-amber-300 text-xs space-y-2 border border-amber-200 dark:border-amber-500/20">
                  <p className="font-bold flex items-center gap-1.5">
                    <AlertCircle className="w-4 h-4 text-amber-600" />
                    Chưa có ca phỏng vấn mở nào cho Vòng {effectiveRound} của vị trí này!
                  </p>
                  <p className="text-amber-700 dark:text-amber-400">
                    Vui lòng tạo ca phỏng vấn mới trong phần Quản lý lịch hoặc chuyển sang chế độ "Gửi lời mời chung".
                  </p>
                </div>
              ) : (
                <div className="max-h-60 overflow-y-auto space-y-2 pr-1">
                  {slots.map(s => {
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
                          <div className={`w-4 h-4 rounded-full border flex items-center justify-center ${isSelected ? 'border-brand-600 bg-brand-600 text-white' : 'border-ink-300'}`}>
                            {isSelected && <CheckCircle2 className="w-3 h-3" />}
                          </div>
                        </div>
                      </div>
                    )
                  })}
                </div>
              )}
            </div>
          ) : (
            <div className="p-4 rounded-xl bg-ink-50 dark:bg-white/5 text-ink-600 dark:text-ink-300 text-xs space-y-2 border border-ink-100 dark:border-white/5">
              <p className="font-semibold text-ink-900 dark:text-white">Gửi lời mời chung qua Email & Portal:</p>
              <p>Hồ sơ ứng viên sẽ chuyển sang trạng thái <strong>Screening (Đã duyệt CV)</strong>. Lịch phỏng vấn sẽ được xếp sau.</p>
            </div>
          )}
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
            disabled={submitting || (activeTab === 'schedule' && (!selectedSlotId || slots.length === 0))}
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
                <span>{activeTab === 'schedule' ? 'Xác Nhận & Xếp Lịch' : 'Gửi Lời Mời'}</span>
              </>
            )}
          </button>
        </div>
      </motion.div>
    </div>
  )
}
