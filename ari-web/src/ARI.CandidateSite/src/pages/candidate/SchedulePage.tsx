import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
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
  Lock,
  ShieldAlert,
  LogIn,
} from 'lucide-react'
import { scheduleService } from '@ari/shared/fservices/schedule'
import type { CandidateScheduleItem } from '@ari/shared/fservices/schedule'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

/**
 * Trang xem lịch phỏng vấn. Từ ADR-048, ứng viên KHÔNG tự chọn khung giờ — nhân sự gán trực tiếp.
 * Ứng viên chỉ XÁC NHẬN lịch hoặc TỪ CHỐI kèm lý do (bận) để nhân sự xếp lịch khác phù hợp hơn.
 *
 * Quyết định là DUY NHẤT: sau khi bấm Xác nhận hoặc Từ chối cho một lịch, không thể đổi lại
 * (backend khoá). Từ email mời, 2 nút Confirm/Reject deep-link về đây kèm ?booking=&action= để
 * tự mở hộp thoại xác nhận tương ứng.
 */
function isUnauthorized(e: unknown): boolean {
  const x = e as { response?: { status?: number } }
  return x?.response?.status === 401
}

function errMsg(e: unknown, fallback: string): string {
  const x = e as { response?: { data?: { message?: string } } }
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

type ModalState = { bookingId: string; action: 'confirm' | 'decline' } | null

export default function CandidateSchedulePage() {
  const queryClient = useQueryClient()
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()

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

  const needsLogin = error ? isUnauthorized(error) : false
  const displayError = error && !needsLogin ? errMsg(error, 'Không tải được lịch phỏng vấn.') : ''

  // Hộp thoại xác nhận (confirm / decline) + lý do từ chối + lỗi thao tác.
  const [modal, setModal] = useState<ModalState>(null)
  const [reason, setReason] = useState('')
  const [actionError, setActionError] = useState('')
  const deepLinkHandled = useRef(false)

  const confirmMut = useMutation({
    mutationFn: (bookingId: string) => scheduleService.confirmSchedule(bookingId),
    onSuccess: () => {
      setModal(null)
      queryClient.invalidateQueries({ queryKey: ['my-schedule'] })
    },
    onError: (e) => setActionError(errMsg(e, 'Xác nhận lịch thất bại.')),
  })

  const declineMut = useMutation({
    mutationFn: (p: { bookingId: string; reason: string }) =>
      scheduleService.declineSchedule(p.bookingId, p.reason),
    onSuccess: () => {
      setModal(null)
      setReason('')
      queryClient.invalidateQueries({ queryKey: ['my-schedule'] })
    },
    onError: (e) => setActionError(errMsg(e, 'Gửi lý do thất bại.')),
  })

  const openModal = (bookingId: string, action: 'confirm' | 'decline') => {
    setActionError('')
    setReason('')
    setModal({ bookingId, action })
  }

  const closeModal = () => {
    setModal(null)
    setReason('')
    setActionError('')
  }

  const submitModal = () => {
    if (!modal) return
    if (modal.action === 'confirm') {
      confirmMut.mutate(modal.bookingId)
    } else {
      if (reason.trim().length < 3) {
        setActionError('Vui lòng nhập lý do (ít nhất 3 ký tự).')
        return
      }
      declineMut.mutate({ bookingId: modal.bookingId, reason: reason.trim() })
    }
  }

  // Deep-link từ email: ?booking=&action=confirm|decline → tự mở hộp thoại đúng lịch (1 lần).
  useEffect(() => {
    if (deepLinkHandled.current || loading || !data) return
    const bookingId = searchParams.get('booking')
    const action = searchParams.get('action')
    if (!bookingId || (action !== 'confirm' && action !== 'decline')) return

    deepLinkHandled.current = true
    const target = upcoming.find((s) => s.bookingId === bookingId)
    if (target && target.confirmationStatus !== 'confirmed') {
      openModal(bookingId, action)
    } else {
      // Lịch đã phản hồi rồi (hoặc không còn ở danh sách sắp tới) — báo nhẹ, không mở hộp thoại.
      setActionError('Lịch này đã được phản hồi trước đó và không thể thay đổi.')
    }
    // Dọn query param cho gọn URL.
    const next = new URLSearchParams(searchParams)
    next.delete('booking')
    next.delete('action')
    setSearchParams(next, { replace: true })
  }, [loading, data, upcoming, searchParams, setSearchParams])

  const grouped = useMemo(() => {
    const map = new Map<string, CandidateScheduleItem[]>()
    for (const s of upcoming) {
      const k = dayKey(s.startTime)
      if (!map.has(k)) map.set(k, [])
      map.get(k)!.push(s)
    }
    return Array.from(map.entries())
  }, [upcoming])

  const modalItem = modal ? upcoming.find((s) => s.bookingId === modal.bookingId) : undefined
  const submitting = confirmMut.isPending || declineMut.isPending

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
              Lịch do bộ phận nhân sự xếp. Vui lòng xác nhận, hoặc từ chối kèm lý do để được xếp lịch
              khác.
            </p>
          </div>
        </div>

        {actionError && (
          <div className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {actionError}
          </div>
        )}

        {needsLogin ? (
          <div className="rounded-2xl border border-ink-200 bg-white p-6 text-center shadow-sm sm:p-10">
            <LogIn className="mx-auto mb-3 h-12 w-12 text-brand-500" />
            <p className="mb-4 text-sm text-ink-600">
              Vui lòng đăng nhập Portal ứng viên để xem và xác nhận lịch phỏng vấn của bạn.
            </p>
            <Link
              to={`/auth/candidate-login?returnUrl=${encodeURIComponent(
                location.pathname + location.search
              )}`}
              className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
            >
              <LogIn className="h-4 w-4" /> Đăng nhập để tiếp tục
            </Link>
          </div>
        ) : displayError ? (
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
            {/* Lịch đã từ chối, đang chờ nhân sự xếp lại */}
            {awaiting.map((s) => (
              <div
                key={s.bookingId}
                className="rounded-2xl border border-amber-200 bg-amber-50 p-5 shadow-sm"
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="flex items-center gap-2 text-sm font-semibold text-amber-800">
                    <CalendarClock className="h-4 w-4" />
                    {s.jobTitle || 'Phỏng vấn'} · Vòng {s.roundNumber}
                  </p>
                  <span className="inline-flex items-center gap-1 rounded-full bg-red-100 px-3 py-1 text-xs font-semibold text-red-700">
                    <X className="h-3.5 w-3.5" /> Đã từ chối
                  </span>
                </div>
                <p className="mt-2 text-sm text-amber-700">
                  Bạn đã từ chối lịch{s.declineReason ? `: “${s.declineReason}”` : ''}. Nhân sự sẽ
                  xếp một khung giờ khác và thông báo lại cho bạn.
                </p>
              </div>
            ))}

            {/* Lịch sắp tới — xác nhận / từ chối (mỗi lịch phản hồi 1 lần) */}
            {grouped.map(([day, daySlots]) => (
              <div key={day} className="rounded-2xl border border-ink-200 bg-white p-5 shadow-sm">
                <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold capitalize text-ink-800">
                  <Clock className="h-4 w-4 text-brand-600" /> {day}
                </h3>
                <div className="space-y-3">
                  {daySlots.map((s) => {
                    const confirmed = s.confirmationStatus === 'confirmed'
                    return (
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
                          {confirmed ? (
                            <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700">
                              <CheckCircle2 className="h-3.5 w-3.5" /> Đã xác nhận
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-700">
                              <AlertCircle className="h-3.5 w-3.5" /> Chưa xác nhận
                            </span>
                          )}
                        </div>

                        {confirmed ? (
                          <p className="mt-3 flex items-center gap-1.5 text-xs text-ink-400">
                            <Lock className="h-3.5 w-3.5" /> Bạn đã xác nhận tham dự. Quyết định đã
                            được khoá và không thể thay đổi.
                          </p>
                        ) : (
                          <div className="mt-3 flex flex-wrap gap-2">
                            <button
                              type="button"
                              onClick={() => openModal(s.bookingId, 'confirm')}
                              className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
                            >
                              <CheckCircle2 className="h-4 w-4" /> Xác nhận tham dự
                            </button>
                            <button
                              type="button"
                              onClick={() => openModal(s.bookingId, 'decline')}
                              className="inline-flex items-center gap-1.5 rounded-xl border border-ink-300 bg-white px-4 py-2 text-sm font-medium text-ink-700 hover:bg-ink-100"
                            >
                              <CalendarClock className="h-4 w-4" /> Từ chối / đổi lịch
                            </button>
                          </div>
                        )}
                      </div>
                    )
                  })}
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

      {/* Hộp thoại xác nhận — "bạn đã chắc chắn chưa?" + cảnh báo không thể thay đổi */}
      {modal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-ink-900/50 p-4"
          onClick={closeModal}
        >
          <div
            className="w-full max-w-md rounded-2xl border border-ink-200 bg-white p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-3 flex items-center gap-2">
              {modal.action === 'confirm' ? (
                <span className="grid h-10 w-10 place-items-center rounded-xl bg-emerald-100 text-emerald-600">
                  <CheckCircle2 className="h-5 w-5" />
                </span>
              ) : (
                <span className="grid h-10 w-10 place-items-center rounded-xl bg-red-100 text-red-600">
                  <X className="h-5 w-5" />
                </span>
              )}
              <h2 className="text-base font-bold text-ink-900">
                {modal.action === 'confirm' ? 'Xác nhận tham dự?' : 'Từ chối / xin đổi lịch?'}
              </h2>
            </div>

            {modalItem && (
              <p className="mb-3 text-sm text-ink-600">
                {modalItem.jobTitle || 'Phỏng vấn'} · Vòng {modalItem.roundNumber} —{' '}
                <span className="font-semibold text-ink-800">
                  {dayKey(modalItem.startTime)}, {timeRange(modalItem)}
                </span>
              </p>
            )}

            {modal.action === 'decline' && (
              <div className="mb-3">
                <label className="mb-1 block text-xs font-medium text-ink-600">
                  Lý do bạn không thể tham dự (nhân sự sẽ xếp lịch khác):
                </label>
                <textarea
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  rows={3}
                  maxLength={500}
                  autoFocus
                  placeholder="Ví dụ: Trùng lịch thi ở trường, bận công việc..."
                  className="w-full rounded-xl border border-ink-200 bg-white px-3 py-2 text-sm text-ink-800 focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-100"
                />
              </div>
            )}

            {/* Cảnh báo không thể sửa lại */}
            <div className="mb-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3">
              <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
              <p className="text-xs text-amber-800">
                Bạn đã chắc chắn với quyết định của mình chưa?{' '}
                <strong>Sau khi bấm, bạn sẽ không thể sửa lại lựa chọn này nữa.</strong>
                {modal.action === 'decline' &&
                  ' Nhân sự sẽ sắp xếp một khung giờ khác dựa trên lý do của bạn.'}
              </p>
            </div>

            {actionError && (
              <div className="mb-3 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-2.5 text-xs text-red-700">
                <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {actionError}
              </div>
            )}

            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={closeModal}
                disabled={submitting}
                className="inline-flex items-center gap-1.5 rounded-xl border border-ink-300 bg-white px-4 py-2 text-sm font-medium text-ink-700 hover:bg-ink-100 disabled:opacity-50"
              >
                <X className="h-4 w-4" /> Để sau
              </button>
              <button
                type="button"
                onClick={submitModal}
                disabled={submitting}
                className={`inline-flex items-center gap-1.5 rounded-xl px-4 py-2 text-sm font-semibold text-white disabled:opacity-50 ${
                  modal.action === 'confirm'
                    ? 'bg-emerald-600 hover:bg-emerald-700'
                    : 'bg-red-600 hover:bg-red-700'
                }`}
              >
                {submitting ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : modal.action === 'confirm' ? (
                  <CheckCircle2 className="h-4 w-4" />
                ) : (
                  <X className="h-4 w-4" />
                )}
                {modal.action === 'confirm' ? 'Tôi chắc chắn — Xác nhận' : 'Tôi chắc chắn — Từ chối'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
