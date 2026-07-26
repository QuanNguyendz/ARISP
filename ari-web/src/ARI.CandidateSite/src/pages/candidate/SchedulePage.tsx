import { useMemo, useState } from 'react'
import {
  CalendarCheck,
  Clock,
  Loader2,
  AlertCircle,
  CalendarX,
  Play,
  CheckCircle2,
  CalendarClock,
  X,
} from 'lucide-react'
import { scheduleService } from '@ari/shared/fservices/schedule'
import type { CandidateScheduleItem } from '@ari/shared/fservices/schedule'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

/**
 * Trang xem lịch phỏng vấn. Từ ADR-048, ứng viên KHÔNG tự chọn khung giờ — nhân sự gán trực tiếp.
 * Ứng viên chỉ XÁC NHẬN lịch hoặc TỪ CHỐI kèm lý do (bận) để nhân sự xếp lịch khác phù hợp hơn.
 */
function errMsg(e: unknown, fallback: string): string {
  const x = e as { response?: { data?: { message?: string }; status?: number } }
  if (x?.response?.status === 401) return 'Vui lòng đăng nhập Portal ứng viên để xem lịch phỏng vấn.'
  return x?.response?.data?.message || fallback
}

function dayKey(iso: string): string {
  return new Date(iso).toLocaleDateString('vi-VN', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

function timeRange(item: CandidateScheduleItem): string {
  const t = (x: string) =>
    new Date(x).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  return `${t(item.startTime)} – ${t(item.endTime)}`
}

export default function CandidateSchedulePage() {
  const queryClient = useQueryClient()
  const {
    data,
    isLoading: loading,
    error,
  } = useQuery({
    queryKey: ['my-schedule'],
    queryFn: () => scheduleService.getMySchedule(),
    retry: false,
  })

  const upcoming = useMemo(() => data?.upcoming ?? [], [data])
  const awaiting = useMemo(() => data?.awaitingReschedule ?? [], [data])
  const displayError = error ? errMsg(error, 'Không tải được lịch phỏng vấn.') : ''

  // Form từ chối: booking đang mở + nội dung lý do.
  const [decliningId, setDecliningId] = useState<string | null>(null)
  const [reason, setReason] = useState('')
  const [actionError, setActionError] = useState('')

  const confirmMut = useMutation({
    mutationFn: (bookingId: string) => scheduleService.confirmSchedule(bookingId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['my-schedule'] }),
    onError: (e) => setActionError(errMsg(e, 'Xác nhận lịch thất bại.')),
  })

  const declineMut = useMutation({
    mutationFn: (p: { bookingId: string; reason: string }) =>
      scheduleService.declineSchedule(p.bookingId, p.reason),
    onSuccess: () => {
      setDecliningId(null)
      setReason('')
      queryClient.invalidateQueries({ queryKey: ['my-schedule'] })
    },
    onError: (e) => setActionError(errMsg(e, 'Gửi lý do thất bại.')),
  })

  const openDecline = (id: string) => {
    setActionError('')
    setReason('')
    setDecliningId(id)
  }

  const submitDecline = (bookingId: string) => {
    if (reason.trim().length < 3) {
      setActionError('Vui lòng nhập lý do (ít nhất 3 ký tự).')
      return
    }
    declineMut.mutate({ bookingId, reason: reason.trim() })
  }

  const grouped = useMemo(() => {
    const map = new Map<string, CandidateScheduleItem[]>()
    for (const s of upcoming) {
      const k = dayKey(s.startTime)
      if (!map.has(k)) map.set(k, [])
      map.get(k)!.push(s)
    }
    return Array.from(map.entries())
  }, [upcoming])

  return (
    <div className="min-h-screen bg-ink-50 px-4 py-10">
      <div className="mx-auto max-w-2xl">
        <div className="mb-6 flex items-center gap-3">
          <span className="grid h-11 w-11 place-items-center rounded-xl bg-brand-600 text-white">
            <CalendarCheck className="h-6 w-6" />
          </span>
          <div>
            <h1 className="text-xl font-bold text-ink-900">Lịch phỏng vấn của bạn</h1>
            <p className="text-sm text-ink-500">
              Lịch do bộ phận nhân sự xếp. Vui lòng xác nhận, hoặc báo bận kèm lý do để được xếp lịch
              khác.
            </p>
          </div>
        </div>

        {actionError && (
          <div className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {actionError}
          </div>
        )}

        {displayError ? (
          <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {displayError}
          </div>
        ) : loading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="h-7 w-7 animate-spin text-brand-600" />
          </div>
        ) : upcoming.length === 0 && awaiting.length === 0 ? (
          <div className="rounded-2xl border border-ink-200 bg-white p-6 text-center shadow-sm sm:p-10">
            <CalendarX className="mx-auto mb-3 h-12 w-12 text-ink-300" />
            <p className="text-sm text-ink-600">
              Nhân sự chưa xếp lịch phỏng vấn cho bạn. Bạn sẽ nhận thông báo khi có lịch.
            </p>
          </div>
        ) : (
          <div className="space-y-5">
            {/* Lịch đã báo bận, đang chờ nhân sự xếp lại */}
            {awaiting.map((s) => (
              <div
                key={s.bookingId}
                className="rounded-2xl border border-amber-200 bg-amber-50 p-5 shadow-sm"
              >
                <p className="flex items-center gap-2 text-sm font-semibold text-amber-800">
                  <CalendarClock className="h-4 w-4" />
                  {s.jobTitle || 'Phỏng vấn'} · Vòng {s.roundNumber}
                </p>
                <p className="mt-2 text-sm text-amber-700">
                  Bạn đã báo bận{s.declineReason ? `: “${s.declineReason}”` : ''}. Nhân sự sẽ xếp một
                  khung giờ khác và thông báo lại cho bạn.
                </p>
              </div>
            ))}

            {/* Lịch sắp tới — xác nhận / báo bận */}
            {grouped.map(([day, daySlots]) => (
              <div key={day} className="rounded-2xl border border-ink-200 bg-white p-5 shadow-sm">
                <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold capitalize text-ink-800">
                  <Clock className="h-4 w-4 text-brand-600" /> {day}
                </h3>
                <div className="space-y-3">
                  {daySlots.map((s) => (
                    <div
                      key={s.bookingId}
                      className="rounded-xl border border-ink-200 bg-ink-50/60 p-4"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div>
                          <p className="text-sm font-semibold text-ink-900">{timeRange(s)}</p>
                          <p className="text-xs text-ink-500">
                            {s.jobTitle || 'Phỏng vấn'} · Vòng {s.roundNumber}
                          </p>
                        </div>
                        {s.confirmationStatus === 'confirmed' && (
                          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700">
                            <CheckCircle2 className="h-3.5 w-3.5" /> Đã xác nhận
                          </span>
                        )}
                      </div>

                      {s.confirmationStatus !== 'confirmed' && decliningId !== s.bookingId && (
                        <div className="mt-3 flex flex-wrap gap-2">
                          <button
                            type="button"
                            onClick={() => confirmMut.mutate(s.bookingId)}
                            disabled={confirmMut.isPending}
                            className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
                          >
                            {confirmMut.isPending && confirmMut.variables === s.bookingId ? (
                              <Loader2 className="h-4 w-4 animate-spin" />
                            ) : (
                              <CheckCircle2 className="h-4 w-4" />
                            )}
                            Xác nhận tham dự
                          </button>
                          <button
                            type="button"
                            onClick={() => openDecline(s.bookingId)}
                            className="inline-flex items-center gap-1.5 rounded-xl border border-ink-300 bg-white px-4 py-2 text-sm font-medium text-ink-700 hover:bg-ink-100"
                          >
                            <CalendarClock className="h-4 w-4" /> Tôi bận, đổi lịch
                          </button>
                        </div>
                      )}

                      {decliningId === s.bookingId && (
                        <div className="mt-3 space-y-2">
                          <label className="block text-xs font-medium text-ink-600">
                            Lý do bạn không thể tham dự (nhân sự sẽ xếp lịch khác):
                          </label>
                          <textarea
                            value={reason}
                            onChange={(e) => setReason(e.target.value)}
                            rows={3}
                            maxLength={500}
                            placeholder="Ví dụ: Trùng lịch thi ở trường, bận công việc..."
                            className="w-full rounded-xl border border-ink-200 bg-white px-3 py-2 text-sm text-ink-800 focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-100"
                          />
                          <div className="flex gap-2">
                            <button
                              type="button"
                              onClick={() => submitDecline(s.bookingId)}
                              disabled={declineMut.isPending}
                              className="inline-flex items-center gap-1.5 rounded-xl bg-amber-600 px-4 py-2 text-sm font-semibold text-white hover:bg-amber-700 disabled:opacity-50"
                            >
                              {declineMut.isPending ? (
                                <Loader2 className="h-4 w-4 animate-spin" />
                              ) : (
                                <CalendarClock className="h-4 w-4" />
                              )}
                              Gửi lý do
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                setDecliningId(null)
                                setReason('')
                                setActionError('')
                              }}
                              className="inline-flex items-center gap-1.5 rounded-xl border border-ink-300 bg-white px-4 py-2 text-sm font-medium text-ink-700 hover:bg-ink-100"
                            >
                              <X className="h-4 w-4" /> Huỷ
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            ))}

            {upcoming.length > 0 && (
              <a
                href="/candidate/applications"
                className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
              >
                <Play className="h-4 w-4" /> Vào hồ sơ ứng tuyển để luyện tập phỏng vấn thử
              </a>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
