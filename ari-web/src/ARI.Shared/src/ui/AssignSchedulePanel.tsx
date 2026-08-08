import { useEffect, useMemo, useState } from 'react'
import {
  CalendarClock,
  CalendarCheck,
  CalendarPlus,
  Loader2,
  AlertCircle,
  CheckCircle2,
} from 'lucide-react'
import { scheduleService } from '@ari/shared/fservices/schedule'
import type { AvailabilitySlot } from '@ari/shared/types/job'

/**
 * Panel HR gán cứng 1 khung giờ (trong kho slot của tin) cho 1 ứng viên — ADR-048.
 * Thay cho luồng ứng viên tự chọn. Nhúng vào cột thao tác ở trang chi tiết ứng viên.
 */
export interface AssignSchedulePanelProps {
  applicationId: string
  jobPostingId: string
  /** Vòng phỏng vấn hiện tại (mặc định 1). */
  round: number
  /** Ứng viên đã có lịch cho vòng hiện tại chưa. */
  hasScheduled: boolean
  /** Giờ đã được xếp (ISO) — hiển thị khi hasScheduled. */
  scheduledAt?: string | null
  /** Phản hồi của ứng viên với lịch hiện tại: pending | confirmed (khi hasScheduled). */
  confirmationStatus?: string | null
  /** Lý do ứng viên báo bận lần gần nhất — hiển thị khi đang chờ xếp lại (không còn lịch). */
  declineReason?: string | null
  /** Trạng thái hồ sơ (screening/interview mới được xếp lịch). */
  status: string
  /** Gọi sau khi gán thành công để trang cha refetch hồ sơ. */
  onAssigned: () => void
}

const PRE_SCREENING = ['cv_submitted', 'invited', 'cv_rejected', 'not_pass', 'rejected']

function errMsg(e: unknown, fallback: string): string {
  const x = e as { response?: { data?: { message?: string } } }
  return x?.response?.data?.message || fallback
}

function slotLabel(s: AvailabilitySlot): string {
  const start = new Date(s.startTime)
  const end = new Date(s.endTime)
  const d = start.toLocaleDateString('vi-VN', {
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
  const t = (x: Date) => x.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  return `${d} · ${t(start)}–${t(end)}`
}

function fullDateTime(iso: string): string {
  return new Date(iso).toLocaleString('vi-VN', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export default function AssignSchedulePanel({
  applicationId,
  jobPostingId,
  round,
  hasScheduled,
  scheduledAt,
  confirmationStatus,
  declineReason,
  status,
  onAssigned,
}: AssignSchedulePanelProps) {
  const canAssign = !PRE_SCREENING.includes(status.toLowerCase())

  const [slots, setSlots] = useState<AvailabilitySlot[]>([])
  const [loading, setLoading] = useState(false)
  const [selected, setSelected] = useState('')
  const [assigning, setAssigning] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    // Chỉ nạp kho slot khi đủ điều kiện gán và chưa có lịch.
    if (hasScheduled || !canAssign || !jobPostingId) return
    let alive = true
    setLoading(true)
    setError('')
    scheduleService
      .getSlots(jobPostingId, round)
      .then((data) => {
        if (alive) setSlots(data)
      })
      .catch((e) => {
        if (alive) setError(errMsg(e, 'Không tải được khung giờ.'))
      })
      .finally(() => {
        if (alive) setLoading(false)
      })
    return () => {
      alive = false
    }
  }, [jobPostingId, round, hasScheduled, canAssign])

  // Chỉ khung giờ tương lai còn chỗ trống.
  const openSlots = useMemo(() => {
    const now = Date.now()
    return slots
      .filter((s) => new Date(s.startTime).getTime() > now && s.bookedCount < s.capacity)
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())
  }, [slots])

  const assign = async () => {
    if (!selected) return
    setAssigning(true)
    setError('')
    try {
      await scheduleService.assign({ applicationId, slotId: selected, round })
      onAssigned()
    } catch (e) {
      setError(errMsg(e, 'Gán lịch thất bại.'))
    } finally {
      setAssigning(false)
    }
  }

  return (
    <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5">
      <h2 className="mb-4 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
        <CalendarClock className="h-4 w-4 text-brand-600 dark:text-brand-400" />
        Xếp lịch phỏng vấn · Vòng {round}
      </h2>

      {!hasScheduled && canAssign && declineReason && (
        <div className="mb-3 rounded-xl border border-amber-200 bg-amber-50 p-3 dark:border-amber-500/30 dark:bg-amber-500/10">
          <p className="flex items-center gap-1.5 text-xs font-semibold text-amber-700 dark:text-amber-400">
            <AlertCircle className="h-3.5 w-3.5" /> Ứng viên đã từ chối lịch trước (xin đổi lịch)
          </p>
          <p className="mt-1 text-sm italic text-amber-800 dark:text-amber-300">“{declineReason}”</p>
          <p className="mt-1 text-xs text-amber-600 dark:text-amber-400">
            Hãy chọn một khung giờ khác phù hợp hơn cho ứng viên.
          </p>
        </div>
      )}

      {hasScheduled ? (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-3 dark:border-emerald-500/30 dark:bg-emerald-500/10">
          <p className="mb-1 flex items-center gap-1.5 text-xs font-medium text-emerald-700 dark:text-emerald-400">
            <CalendarCheck className="h-3.5 w-3.5" /> Đã xếp lịch cho ứng viên
          </p>
          <p className="text-sm font-semibold text-emerald-800 dark:text-emerald-300">
            {scheduledAt ? fullDateTime(scheduledAt) : 'Đã có lịch phỏng vấn thật'}
          </p>
          {confirmationStatus === 'confirmed' ? (
            <span className="mt-1.5 inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-semibold text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400">
              <CheckCircle2 className="h-3.5 w-3.5" /> Đã xác nhận
            </span>
          ) : (
            <span className="mt-1.5 inline-flex items-center gap-1.5 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-700 dark:bg-amber-500/15 dark:text-amber-400">
              <AlertCircle className="h-3.5 w-3.5" /> Chưa xác nhận
            </span>
          )}
        </div>
      ) : !canAssign ? (
        <p className="flex items-start gap-1.5 text-xs text-ink-400">
          <CalendarClock className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          Hãy gửi lời mời phỏng vấn (duyệt CV) trước khi xếp lịch cho ứng viên.
        </p>
      ) : loading ? (
        <div className="flex items-center justify-center py-6">
          <Loader2 className="h-5 w-5 animate-spin text-brand-600" />
        </div>
      ) : openSlots.length === 0 ? (
        <p className="flex items-start gap-1.5 text-xs text-ink-400">
          <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          Chưa có khung giờ trống cho vòng {round}. Hãy tạo khung giờ ở trang cấu hình lịch của tin
          tuyển dụng trước.
        </p>
      ) : (
        <div className="space-y-3">
          {error && (
            <div className="flex items-start gap-1.5 rounded-lg border border-red-200 bg-red-50 p-2.5 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
              <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {error}
            </div>
          )}
          <label className="block text-xs font-medium text-ink-600 dark:text-ink-300">
            Chọn khung giờ ấn định
            <select
              value={selected}
              onChange={(e) => setSelected(e.target.value)}
              className="mt-1 w-full rounded-xl border border-ink-200 bg-white px-3 py-2.5 text-sm text-ink-800 focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-100 dark:border-white/10 dark:bg-ink-900 dark:text-white dark:focus:ring-brand-500/20"
            >
              <option value="">— Chọn khung giờ —</option>
              {openSlots.map((s) => (
                <option key={s.id} value={s.id}>
                  {slotLabel(s)}
                  {s.capacity > 1 ? ` (còn ${s.capacity - s.bookedCount}/${s.capacity})` : ''}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            onClick={assign}
            disabled={!selected || assigning}
            className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-3 py-2.5 text-sm font-semibold text-white hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {assigning ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <CalendarPlus className="h-4 w-4" />
            )}
            Gán lịch cho ứng viên
          </button>
          <p className="flex items-start gap-1.5 text-xs text-ink-400">
            <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            Sau khi gán, ứng viên nhận thông báo giờ hẹn và có thể luyện tập phỏng vấn thử.
          </p>
        </div>
      )}
    </div>
  )
}
