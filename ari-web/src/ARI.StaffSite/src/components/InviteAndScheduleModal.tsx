import { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import { X, Calendar, Send, CheckCircle2, Users, AlertCircle, Loader2 } from 'lucide-react'
import { scheduleService, type HmAvailabilityWindow } from '@ari/shared/fservices/schedule'
import EmailComposerModal from '@ari/shared/ui/EmailComposerModal'
import { EMAIL_TEMPLATES } from '@ari/shared/fservices/email'
import type { AvailabilitySlot } from '@ari/shared/types/job'

export interface InviteTargetApplication {
  id: string
  name: string
}

interface InviteAndScheduleModalProps {
  /** Một hoặc nhiều hồ sơ cùng nhận một khung giờ (duyệt hàng loạt dùng chung 1 ca). */
  applications: InviteTargetApplication[]
  jobPostingId: string
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
 * Xếp lịch phỏng vấn — LUÔN kèm một khung giờ cụ thể, và ĐÂY là nơi thư báo "qua vòng" đi.
 *
 * Trước đây modal có thêm lựa chọn "gửi lời mời chung (xếp sau)": ứng viên được duyệt nhưng
 * không có giờ hẹn nên **không nhận được email nào**, chỉ có chuông trong Portal.
 *
 * ADR-067 bỏ nốt chế độ "duyệt CV kèm xếp lịch": duyệt hồ sơ nay là gửi cho Hiring Manager, và
 * xếp lịch là việc riêng sau khi HM duyệt và gửi khung giờ họ có mặt được. Modal chỉ hiện những
 * ca NẰM TRỌN trong khung đó — chọn ca ngoài thì server chặn, mà để người dùng chọn rồi mới báo
 * hỏng là bắt họ đoán.
 */
export default function InviteAndScheduleModal({
  applications,
  jobPostingId,
  targetRoundNumber = 1,
  onClose,
  onSuccess,
}: InviteAndScheduleModalProps) {
  const [slots, setSlots] = useState<AvailabilitySlot[]>([])
  const [windows, setWindows] = useState<HmAvailabilityWindow[]>([])
  const [loadingSlots, setLoadingSlots] = useState(true)
  const [selectedSlotId, setSelectedSlotId] = useState<string>('')
  // Trình soạn thư (ADR-061): mở SAU khi đã chọn ca (nội dung thư có giờ hẹn), gửi thì mới chốt chỗ.
  const [composing, setComposing] = useState(false)

  /**
   * Trình soạn thư CHỈ dùng được khi gửi cho ĐÚNG MỘT ứng viên.
   *
   * Thư mời là thư cá nhân hoá: tên người nhận, giờ hẹn, địa điểm, hai nút xác nhận gắn với đúng
   * hồ sơ đó. Chế độ hàng loạt trước đây xem trước thư của `applications[0]` rồi gửi CÙNG MỘT khối
   * HTML cho tất cả — ứng viên thứ 2 trở đi nhận thư ghi TÊN VÀ GIỜ HẸN CỦA NGƯỜI KHÁC. Đó vừa là
   * rò rỉ thông tin cá nhân, vừa là bốn người bị báo sai giờ.
   *
   * Không có cách nào sửa một khối HTML cho đúng với N người, nên gửi hàng loạt đi thẳng bằng mẫu —
   * server dựng riêng cho từng hồ sơ với dữ liệu đúng của họ. Đúng tinh thần "what-you-see-is-
   * what-is-sent" của ADR-061: xem được một thứ thì mới sửa được thứ đó.
   */
  const canCompose = applications.length === 1
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const effectiveRound = targetRoundNumber > 0 ? targetRoundNumber : 1
  const isBatch = applications.length > 1
  const selectedSlot = slots.find((s) => s.id === selectedSlotId)

  // Một ca chỉ nhận MỘT ứng viên (ADR-067) — chọn nhiều người rồi dồn vào một ca là không thể.
  const tooManyCandidates = applications.length > 1

  useEffect(() => {
    let isMounted = true
    async function load() {
      try {
        setLoadingSlots(true)
        // Hai lời gọi song song: danh sách ca, và khung giờ Hiring Manager có mặt được.
        const [data, hmWindows] = await Promise.all([
          scheduleService.getSlots(jobPostingId, effectiveRound),
          scheduleService.getHmAvailability(jobPostingId, effectiveRound).catch(() => []),
        ])

        // Chỉ ca của đúng vòng, chưa trôi qua và còn chỗ.
        const now = new Date()
        const open = data.filter(
          (s) =>
            (s.roundNumber == null || s.roundNumber === effectiveRound) &&
            new Date(s.startTime) > now &&
            s.bookedCount < s.capacity
        )

        // Lọc theo lịch rảnh của HM — cùng vị từ với server (nằm TRỌN trong một khung), để danh sách
        // trên màn không bao giờ chứa một lựa chọn mà bấm vào sẽ bị từ chối.
        //
        // Chưa khai khung nào thì KHÔNG lọc: lúc đó không có gì để đối chiếu, và thông báo bên dưới
        // nói rõ đang thiếu gì thay vì hiện một danh sách trống không giải thích.
        const valid =
          hmWindows.length === 0
            ? open
            : open.filter((s) =>
                hmWindows.some(
                  (w) =>
                    new Date(w.startTime) <= new Date(s.startTime) &&
                    new Date(w.endTime) >= new Date(s.endTime)
                )
              )

        if (isMounted) {
          setWindows(hmWindows)
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

  const handleConfirm = async (emailOverride?: { subject: string; bodyHtml: string }) => {
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
          await scheduleService.assign({
            applicationId: app.id,
            slotId: selectedSlot.id,
            round: selectedSlot.roundNumber ?? effectiveRound,
            emailOverride,
          })
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

      onSuccess(
        failures.length === 0
          ? `Đã xếp lịch cho ${success} ứng viên. Thư báo qua vòng kèm giờ hẹn đã được gửi.`
          : `Đã xếp lịch cho ${success} ứng viên; ${failures.length} hồ sơ chưa xong (${failures.join(' | ')}).`
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
                Xếp lịch phỏng vấn
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
            Chọn ca phỏng vấn cho ứng viên. Đây là lúc thư báo <strong>qua vòng</strong> kèm giờ hẹn
            và địa điểm được gửi đi — trước bước này ứng viên chưa nhận thông báo nào.
          </p>

          {/* Lịch rảnh của Hiring Manager: hiện NGUYÊN VĂN thay vì chỉ lặng lẽ lọc bớt ca, để
              Recruiter biết cần chốt giờ nào với ứng viên (việc chốt đó diễn ra ngoài hệ thống —
              SMS, Zalo, gọi điện). */}
          {windows.length > 0 && (
            <div className="rounded-xl border border-sky-200 bg-sky-50 p-3 text-xs text-sky-900 dark:border-sky-500/20 dark:bg-sky-500/10 dark:text-sky-200">
              <p className="font-semibold">Hiring Manager có mặt được:</p>
              <ul className="mt-1 space-y-0.5">
                {windows.map((w) => {
                  const from = fmtSlotTime(w.startTime)
                  const to = fmtSlotTime(w.endTime)
                  return (
                    <li key={w.id}>
                      {from.dateStr} · {from.timeStr} – {to.timeStr}
                      {w.note ? ` (${w.note})` : ''}
                    </li>
                  )
                })}
              </ul>
              <p className="mt-1.5 opacity-80">
                Chỉ những ca nằm trọn trong các khung trên mới xếp được.
              </p>
            </div>
          )}

          {!loadingSlots && windows.length === 0 && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-300">
              Hiring Manager chưa gửi khung giờ có mặt được cho vòng {effectiveRound}. Nếu tin này có
              Hiring Manager, hãy đề nghị họ gửi lịch rảnh trước — nếu không, thao tác xếp lịch sẽ bị
              server từ chối.
            </div>
          )}

          {tooManyCandidates && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-300">
              Mỗi ca phỏng vấn chỉ nhận <strong>một</strong> ứng viên (Hiring Manager ngồi cùng AI
              trong suốt buổi). Hãy xếp lịch cho từng người, mỗi người một ca.
            </div>
          )}

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
            disabled={submitting || !selectedSlotId || slots.length === 0 || tooManyCandidates}
            onClick={() => (canCompose ? setComposing(true) : void handleConfirm())}
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
                <span>Xếp lịch &amp; gửi thư mời</span>
              </>
            )}
          </button>
        </div>
      </motion.div>

      {/* Soạn thư trước, chốt chỗ sau: huỷ ở đây = không ca nào bị chiếm, không thư nào gửi đi. */}
      {composing && canCompose && (
        <EmailComposerModal
          open={composing}
          templateKey={EMAIL_TEMPLATES.InterviewInvite}
          contextId={applications[0].id}
          secondaryId={selectedSlotId}
          sending={submitting}
          onCancel={() => setComposing(false)}
          onSend={async (override) => {
            setComposing(false)
            await handleConfirm(override)
          }}
        />
      )}
    </div>
  )
}
