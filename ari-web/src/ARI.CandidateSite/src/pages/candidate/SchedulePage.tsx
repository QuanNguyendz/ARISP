import { useMemo } from 'react'
import { CalendarCheck, Clock, Loader2, AlertCircle, CalendarX, Play } from 'lucide-react'
import { scheduleService } from '@ari/shared/fservices/schedule'
import type { AvailabilitySlot } from '@ari/shared/types/job'
import { useQuery } from '@tanstack/react-query'

/**
 * Trang xem lịch phỏng vấn (read-only). Từ ADR-048, ứng viên KHÔNG tự chọn khung giờ —
 * nhân sự gán trực tiếp. Trang này chỉ hiển thị giờ hẹn đã được xếp.
 */
function errMsg(e: unknown, fallback: string): string {
  const x = e as { response?: { data?: { message?: string }; status?: number } }
  if (x?.response?.status === 401) return 'Vui lòng đăng nhập Portal ứng viên để xem lịch phỏng vấn.'
  return x?.response?.data?.message || fallback
}

function dayKey(iso: string | Date): string {
  return new Date(iso).toLocaleDateString('vi-VN', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

function timeLabel(s: AvailabilitySlot): string {
  const t = (x: Date) => x.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  return `${t(new Date(s.startTime))} – ${t(new Date(s.endTime))}`
}

export default function CandidateSchedulePage() {
  const {
    data,
    isLoading: loading,
    error,
  } = useQuery({
    queryKey: ['my-schedule'],
    queryFn: () => scheduleService.getMySchedule(),
    retry: false,
  })

  const upcoming = useMemo(() => data?.upcomingSlots ?? [], [data])
  const displayError = error ? errMsg(error, 'Không tải được lịch phỏng vấn.') : ''

  const grouped = useMemo(() => {
    const map = new Map<string, AvailabilitySlot[]>()
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
              Lịch do bộ phận nhân sự xếp. Vui lòng đến đúng khung giờ được thông báo.
            </p>
          </div>
        </div>

        {displayError ? (
          <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {displayError}
          </div>
        ) : loading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="h-7 w-7 animate-spin text-brand-600" />
          </div>
        ) : upcoming.length === 0 ? (
          <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-sm">
            <CalendarX className="mx-auto mb-3 h-12 w-12 text-ink-300" />
            <p className="text-sm text-ink-600">
              Nhân sự chưa xếp lịch phỏng vấn cho bạn. Bạn sẽ nhận thông báo khi có lịch.
            </p>
          </div>
        ) : (
          <div className="space-y-5">
            {grouped.map(([day, daySlots]) => (
              <div key={day} className="rounded-2xl border border-ink-200 bg-white p-5 shadow-sm">
                <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold capitalize text-ink-800">
                  <Clock className="h-4 w-4 text-brand-600" /> {day}
                </h3>
                <div className="flex flex-wrap gap-2">
                  {daySlots.map((s) => (
                    <span
                      key={s.id}
                      className="rounded-xl border border-brand-200 bg-brand-50 px-4 py-2 text-sm font-medium text-brand-700"
                    >
                      {timeLabel(s)}
                    </span>
                  ))}
                </div>
              </div>
            ))}

            <a
              href="/candidate/applications"
              className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
            >
              <Play className="h-4 w-4" /> Vào hồ sơ ứng tuyển để luyện tập phỏng vấn thử
            </a>
          </div>
        )}
      </div>
    </div>
  )
}
