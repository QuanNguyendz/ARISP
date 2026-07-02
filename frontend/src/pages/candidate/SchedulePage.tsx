import { useMemo, useState } from 'react'
import { useParams, useSearchParams } from 'react-router-dom'
import {
  CalendarCheck,
  Clock,
  Loader2,
  CheckCircle2,
  AlertCircle,
  CalendarX,
  Play,
} from 'lucide-react'
import { scheduleService } from '@services/schedule'
import type { AvailabilitySlot } from '@/types/job'
import { useQuery } from '@tanstack/react-query'

function errMsg(e: unknown, fallback: string): string {
  const x = e as { response?: { data?: { message?: string }; status?: number } }
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
  const { applicationId } = useParams<{ applicationId: string }>()
  const [params] = useSearchParams()
  const token = params.get('token') || undefined
  const round = Number(params.get('round')) || 1

  const [selected, setSelected] = useState<string | null>(null)
  const [booking, setBooking] = useState(false)
  const [done, setDone] = useState(false)
  const [errorMsg, setErrorMsg] = useState('')

  const { data: slots = [], isLoading: loading, error, refetch } = useQuery({
    queryKey: ['open-slots', applicationId, round, token],
    queryFn: () => scheduleService.getOpenSlots(applicationId!, round, token),
    enabled: !!applicationId,
    retry: false,
  })

  const displayError = errorMsg || (error ? errMsg(error, 'Không tải được khung giờ. Liên kết có thể đã hết hạn.') : '')

  const grouped = useMemo(() => {
    const map = new Map<string, AvailabilitySlot[]>()
    for (const s of slots) {
      const k = dayKey(s.startTime)
      if (!map.has(k)) map.set(k, [])
      map.get(k)!.push(s)
    }
    return Array.from(map.entries())
  }, [slots])

  const confirm = async () => {
    if (!applicationId || !selected) return
    setBooking(true)
    setErrorMsg('')
    try {
      await scheduleService.book(applicationId, { slotId: selected, round, token })
      setDone(true)
    } catch (e) {
      setErrorMsg(errMsg(e, 'Không thể đặt lịch. Vui lòng thử lại.'))
      // tải lại để cập nhật slot đã đầy
      void refetch()
    } finally {
      setBooking(false)
    }
  }

  return (
    <div className="min-h-screen bg-ink-50 px-4 py-10">
      <div className="mx-auto max-w-2xl">
        <div className="mb-6 flex items-center gap-3">
          <span className="grid h-11 w-11 place-items-center rounded-xl bg-brand-600 text-white">
            <CalendarCheck className="h-6 w-6" />
          </span>
          <div>
            <h1 className="text-xl font-bold text-ink-900">Chọn lịch phỏng vấn</h1>
            <p className="text-sm text-ink-500">
              Vòng {round} · chọn một khung giờ phù hợp với bạn
            </p>
          </div>
        </div>

        {done ? (
          <div className="rounded-2xl border border-emerald-200 bg-white p-8 text-center shadow-sm">
            <CheckCircle2 className="mx-auto mb-4 h-14 w-14 text-emerald-500" />
            <h2 className="text-lg font-semibold text-ink-900">Đã đặt lịch thành công!</h2>
            <p className="mx-auto mt-2 max-w-md text-sm text-ink-600">
              Hẹn gặp bạn tại buổi phỏng vấn. Buổi phỏng vấn thật diễn ra tại văn phòng — bạn sẽ
              nhập mã phỏng vấn (Interview Code) do nhân sự cấp tại chỗ. Trước ngày hẹn, bạn có thể
              luyện tập với chế độ <b>phỏng vấn thử</b> trong cổng ứng viên.
            </p>
            <a
              href="/candidate/applications"
              className="mt-5 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
            >
              <Play className="h-4 w-4" /> Vào cổng ứng viên để phỏng vấn thử
            </a>
            <p className="mt-2 text-xs text-ink-400">
              (Đăng nhập bằng tài khoản ứng viên của bạn nếu được yêu cầu)
            </p>
          </div>
        ) : (
          <>
            {displayError && (
              <div className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {displayError}
              </div>
            )}

            {loading ? (
              <div className="flex items-center justify-center py-16">
                <Loader2 className="h-7 w-7 animate-spin text-brand-600" />
              </div>
            ) : slots.length === 0 ? (
              <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-sm">
                <CalendarX className="mx-auto mb-3 h-12 w-12 text-ink-300" />
                <p className="text-sm text-ink-600">
                  Hiện chưa có khung giờ trống cho vòng này. Vui lòng quay lại sau hoặc liên hệ nhân
                  sự.
                </p>
              </div>
            ) : (
              <div className="space-y-5">
                {grouped.map(([day, daySlots]) => (
                  <div
                    key={day}
                    className="rounded-2xl border border-ink-200 bg-white p-5 shadow-sm"
                  >
                    <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold capitalize text-ink-800">
                      <Clock className="h-4 w-4 text-brand-600" /> {day}
                    </h3>
                    <div className="flex flex-wrap gap-2">
                      {daySlots.map((s) => (
                        <button
                          key={s.id}
                          type="button"
                          onClick={() => setSelected(s.id)}
                          className={`rounded-xl border px-4 py-2 text-sm font-medium transition-colors ${
                            selected === s.id
                              ? 'border-brand-600 bg-brand-600 text-white'
                              : 'border-ink-200 bg-white text-ink-700 hover:border-brand-300 hover:bg-brand-50'
                          }`}
                        >
                          {timeLabel(s)}
                        </button>
                      ))}
                    </div>
                  </div>
                ))}

                <button
                  type="button"
                  onClick={confirm}
                  disabled={!selected || booking}
                  className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-3 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
                >
                  {booking ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <CalendarCheck className="h-4 w-4" />
                  )}
                  Xác nhận đặt lịch
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
