import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
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
  Trash2,
  ChevronRight,
  ChevronLeft,
  KeyRound,
  History,
  Timer,
  Sparkles,
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
 *
 * Trang này mở ở HAI chỗ và dữ liệu giống hệt nhau (`GET /api/candidate/schedule` trả TOÀN BỘ lịch
 * của ứng viên đang đăng nhập, không lọc theo hồ sơ):
 *   - `/candidate/schedule` — mục "Lịch phỏng vấn" trong menu tài khoản, nằm trong layout có header.
 *   - `/portal/schedule/:applicationId` — link sâu từ email mời; `standalone` để tự dựng nền + lối
 *     quay lại, vì route đó KHÔNG bọc layout nào. Tham số `:applicationId` chỉ để giữ nguyên dạng
 *     link cũ đã gửi đi trong email — trang không đọc tới nó.
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

/**
 * Khoảng cách tới giờ phỏng vấn, dạng người đọc được. `urgent` = trong vòng 24h → tô đỏ/hổ phách
 * để ứng viên không lướt qua mất buổi sắp diễn ra.
 */
function countdown(t: TFunction, iso: string, now: number): { text: string; urgent: boolean } {
  const diff = new Date(iso).getTime() - now
  if (diff <= 0) return { text: t('relative.inProgress'), urgent: true }
  const mins = Math.round(diff / 60000)
  if (mins < 60) return { text: t('relative.minutes', { count: mins }), urgent: true }
  const hours = Math.floor(mins / 60)
  if (hours < 24) return { text: t('relative.hours', { count: hours }), urgent: true }
  const days = Math.round(hours / 24)
  if (days === 1) return { text: t('relative.tomorrow'), urgent: true }
  return { text: t('relative.days', { count: days }), urgent: false }
}

function dateParts(iso: string): { day: number; month: number } {
  const d = new Date(iso)
  return { day: d.getDate(), month: d.getMonth() + 1 }
}

type ModalState = { bookingId: string; action: 'confirm' | 'decline' } | null

/** Ô ngày kiểu tờ lịch — dùng chung cho khối theo ngày và dải "buổi gần nhất". */
function DateChip({ iso, tone = 'brand' }: { iso: string; tone?: 'brand' | 'white' }) {
  const { day, month } = dateParts(iso)
  const cls =
    tone === 'white'
      ? 'bg-white/15 text-white ring-white/30'
      : 'bg-white text-brand-700 ring-brand-200'
  return (
    <span
      className={`flex h-11 w-11 shrink-0 flex-col items-center justify-center rounded-xl ring-1 ${cls}`}
    >
      <span className="font-display text-base font-extrabold leading-none">{day}</span>
      <span className="mt-0.5 text-[10px] font-semibold uppercase leading-none opacity-80">
        Th{month}
      </span>
    </span>
  )
}

function StatCard({
  icon: Icon,
  label,
  value,
  tone,
}: {
  icon: typeof Clock
  label: string
  value: number
  tone: 'brand' | 'amber' | 'ink'
}) {
  const tones = {
    brand: 'bg-brand-50 text-brand-600',
    amber: 'bg-amber-50 text-amber-600',
    ink: 'bg-ink-100 text-ink-500',
  }
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-ink-200 bg-white px-4 py-3 shadow-card">
      <span className={`grid h-9 w-9 shrink-0 place-items-center rounded-xl ${tones[tone]}`}>
        <Icon className="h-4 w-4" />
      </span>
      <div className="min-w-0">
        <div className="font-display text-lg font-extrabold leading-none text-ink-900">{value}</div>
        <div className="mt-1 truncate text-xs text-ink-500">{label}</div>
      </div>
    </div>
  )
}

function ScheduleSkeleton() {
  const bar = 'animate-shimmer rounded-lg bg-[linear-gradient(90deg,#e2e8f0_25%,#f1f5f9_50%,#e2e8f0_75%)] bg-[length:200%_100%]'
  return (
    <div className="space-y-5">
      <div className={`h-32 rounded-2xl ${bar}`} />
      <div className="grid gap-3 sm:grid-cols-3">
        {[0, 1, 2].map((i) => (
          <div key={i} className={`h-16 rounded-2xl ${bar}`} />
        ))}
      </div>
      <div className={`h-44 rounded-2xl ${bar}`} />
    </div>
  )
}

export default function CandidateSchedulePage({ standalone = false }: { standalone?: boolean }) {
  const { t } = useTranslation('modules/candidate/schedule')
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
  const past = useMemo(() => data?.past ?? [], [data])

  // Đồng hồ đếm ngược phải tự trôi: mở trang lúc "còn 2 giờ" rồi để đó thì con số phải giảm
  // theo, không thì ứng viên đọc một mốc thời gian đã cũ. 60s/lần là đủ mịn cho đơn vị phút.
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 60_000)
    return () => clearInterval(id)
  }, [])

  // Tên vị trí có thể null (job đã xoá / dữ liệu cũ) — dự phòng phải làm Ở ĐÂY.
  // i18next KHÔNG hiểu biểu thức trong {{ }}: viết "{{jobTitle || 'Phỏng vấn'}}" thì nó
  // coi cả cụm là tên biến, không tìm thấy, và in nguyên chuỗi đó ra màn hình.
  const jobTitleOf = (item: CandidateScheduleItem) => item.jobTitle || t('jobTitleFallback')

  const needsLogin = error ? isUnauthorized(error) : false
  const displayError = error && !needsLogin ? errMsg(error, t('errors.loadFailed')) : ''

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
    onError: (e) => setActionError(errMsg(e, t('errors.confirmFailed'))),
  })

  const declineMut = useMutation({
    mutationFn: (p: { bookingId: string; reason: string }) =>
      scheduleService.declineSchedule(p.bookingId, p.reason),
    onSuccess: () => {
      setModal(null)
      setReason('')
      queryClient.invalidateQueries({ queryKey: ['my-schedule'] })
    },
    onError: (e) => setActionError(errMsg(e, t('errors.declineFailed'))),
  })

  const dismissMut = useMutation({
    mutationFn: (bookingId: string) => scheduleService.dismissSchedule(bookingId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['my-schedule'] }),
    onError: (e) => setActionError(errMsg(e, t('errors.dismissFailed'))),
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
        setActionError(t('errors.reasonTooShort'))
        return
      }
      declineMut.mutate({ bookingId: modal.bookingId, reason: reason.trim() })
    }
  }

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
      setActionError(t('errors.alreadyResponded'))
    }
    const next = new URLSearchParams(searchParams)
    next.delete('booking')
    next.delete('action')
    setSearchParams(next, { replace: true })
  }, [loading, data, upcoming, searchParams, setSearchParams, t])

  const grouped = useMemo(() => {
    const map = new Map<string, CandidateScheduleItem[]>()
    for (const s of upcoming) {
      const k = dayKey(s.startTime)
      if (!map.has(k)) map.set(k, [])
      map.get(k)!.push(s)
    }
    return Array.from(map.entries())
  }, [upcoming])

  const nextUp = upcoming[0]
  const pendingCount = upcoming.filter((s) => s.confirmationStatus !== 'confirmed').length
  const hasAnything = upcoming.length > 0 || awaiting.length > 0 || past.length > 0

  const modalItem = modal ? upcoming.find((s) => s.bookingId === modal.bookingId) : undefined
  const submitting = confirmMut.isPending || declineMut.isPending

  return (
    <div className={standalone ? 'min-h-screen bg-ink-50 py-8' : 'py-6'}>
      <div className="mx-auto max-w-3xl px-4 sm:px-6">
        {/* Lối quay lại: standalone (mở từ email) không có header nên phải tự dựng. */}
        {standalone ? (
          <Link
            to="/candidate/applications"
            className="mb-4 inline-flex items-center gap-1.5 text-sm font-medium text-ink-500 transition hover:text-brand-600"
          >
            <ChevronLeft className="h-4 w-4" /> {t('backToApplications')}
          </Link>
        ) : (
          <div className="mb-4 flex items-center gap-2 text-sm text-ink-400">
            <Link to="/jobs" className="transition hover:text-brand-600">
              {t('breadcrumb.home')}
            </Link>
            <ChevronRight className="h-4 w-4" />
            <span className="font-medium text-ink-600">{t('pageTitle')}</span>
          </div>
        )}

        {actionError && (
          <div className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {actionError}
          </div>
        )}

        {needsLogin ? (
          <div className="rounded-2xl border border-ink-200 bg-white p-6 text-center shadow-card sm:p-10">
            <span className="mx-auto mb-4 grid h-14 w-14 place-items-center rounded-2xl bg-brand-50 text-brand-600">
              <LogIn className="h-7 w-7" />
            </span>
            <p className="mb-5 text-sm text-ink-600">{t('loginRequired.message')}</p>
            <Link
              to={`/auth/candidate-login?returnUrl=${encodeURIComponent(
                location.pathname + location.search
              )}`}
              className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:opacity-90"
            >
              <LogIn className="h-4 w-4" /> {t('loginRequired.button')}
            </Link>
          </div>
        ) : displayError ? (
          <div className="flex items-start gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {displayError}
          </div>
        ) : loading ? (
          <ScheduleSkeleton />
        ) : (
          <div className="space-y-5">
            {/* ===== Hero: tiêu đề + buổi gần nhất ===== */}
            <div className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
              <div className="bg-gradient-to-r from-brand-600 via-ai-600 to-ai-500 px-5 py-6 sm:px-7">
                <div className="flex items-start gap-4">
                  <span className="grid h-12 w-12 shrink-0 place-items-center rounded-2xl bg-white/15 text-white ring-1 ring-white/30">
                    <CalendarCheck className="h-6 w-6" />
                  </span>
                  <div className="min-w-0">
                    <h1 className="font-display text-xl font-extrabold leading-tight text-white sm:text-2xl">
                      {t('pageTitle')}
                    </h1>
                    <p className="mt-1 text-sm leading-relaxed text-white/80">
                      {t('pageDescription')}
                    </p>
                  </div>
                </div>
              </div>

              {nextUp && (
                <div className="flex flex-wrap items-center gap-x-4 gap-y-3 border-t border-brand-100 bg-brand-50/60 px-5 py-4 sm:px-7">
                  <DateChip iso={nextUp.startTime} />
                  <div className="min-w-0 flex-1">
                    <div className="text-xs font-semibold uppercase tracking-wide text-brand-600">
                      {t('nextUp.label')}
                    </div>
                    <div className="mt-0.5 truncate text-sm font-semibold text-ink-900">
                      {jobTitleOf(nextUp)}
                    </div>
                    <div className="truncate text-xs text-ink-500">
                      {dayKey(nextUp.startTime)} · {timeRange(nextUp)}
                    </div>
                  </div>
                  {(() => {
                    const c = countdown(t, nextUp.startTime, now)
                    return (
                      <span
                        className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-bold ${
                          c.urgent ? 'bg-amber-100 text-amber-800' : 'bg-white text-brand-700 ring-1 ring-brand-200'
                        }`}
                      >
                        <Timer className="h-3.5 w-3.5" /> {c.text}
                      </span>
                    )
                  })()}
                </div>
              )}
            </div>

            {/* ===== Số liệu nhanh ===== */}
            {hasAnything && (
              <div className="grid gap-3 sm:grid-cols-3">
                <StatCard
                  icon={CalendarCheck}
                  label={t('summary.upcoming')}
                  value={upcoming.length}
                  tone="brand"
                />
                <StatCard
                  icon={AlertCircle}
                  label={t('summary.pending')}
                  value={pendingCount}
                  tone="amber"
                />
                <StatCard
                  icon={History}
                  label={t('summary.past')}
                  value={past.length}
                  tone="ink"
                />
              </div>
            )}

            {/* ===== Chờ nhân sự xếp lại ===== */}
            {awaiting.map((s) => (
              <div
                key={s.bookingId}
                className="rounded-2xl border border-amber-200 bg-amber-50 p-5 shadow-card"
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="flex items-center gap-2 text-sm font-bold text-amber-900">
                    <CalendarClock className="h-4 w-4 shrink-0" />
                    {t('awaitingSection.title', { jobTitle: jobTitleOf(s), round: s.roundNumber })}
                  </p>
                  <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-red-100 px-3 py-1 text-xs font-semibold text-red-700">
                    <X className="h-3.5 w-3.5" /> {t('awaitingSection.badge')}
                  </span>
                </div>
                <p className="mt-2 text-sm leading-relaxed text-amber-800">
                  {t('awaitingSection.message', {
                    declineReason: s.declineReason ? `: "${s.declineReason}"` : '',
                  })}
                </p>
                <div className="mt-3 flex justify-end">
                  <button
                    type="button"
                    onClick={() => dismissMut.mutate(s.bookingId)}
                    disabled={dismissMut.isPending}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-medium text-ink-600 transition hover:border-red-300 hover:text-red-600 disabled:opacity-50"
                  >
                    {dismissMut.isPending && dismissMut.variables === s.bookingId ? (
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                    ) : (
                      <Trash2 className="h-3.5 w-3.5" />
                    )}
                    {t('awaitingSection.dismissButton')}
                  </button>
                </div>
              </div>
            ))}

            {/* ===== Rỗng hoàn toàn ===== */}
            {!hasAnything && (
              <div className="rounded-2xl border border-ink-200 bg-white p-8 text-center shadow-card sm:p-12">
                <span className="mx-auto mb-4 grid h-16 w-16 place-items-center rounded-2xl bg-ink-100 text-ink-400">
                  <CalendarX className="h-8 w-8" />
                </span>
                <h2 className="font-display text-lg font-extrabold text-ink-900">
                  {t('noScheduleTitle')}
                </h2>
                <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-ink-500">
                  {t('noSchedule')}
                </p>
                <Link
                  to="/candidate/applications"
                  className="mt-5 inline-flex items-center gap-2 rounded-xl border border-ink-200 px-4 py-2 text-sm font-semibold text-ink-700 transition hover:bg-ink-50"
                >
                  {t('backToApplications')} <ChevronRight className="h-4 w-4" />
                </Link>
              </div>
            )}

            {/* ===== Lịch sắp tới, gom theo ngày ===== */}
            {grouped.map(([day, daySlots]) => (
              <section
                key={day}
                className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card"
              >
                <header className="flex items-center gap-3 border-b border-ink-100 bg-ink-50/70 px-4 py-3 sm:px-5">
                  <DateChip iso={daySlots[0].startTime} />
                  <h3 className="min-w-0 flex-1 text-sm font-semibold capitalize text-ink-800">
                    {day}
                  </h3>
                  <span className="shrink-0 rounded-full bg-brand-50 px-2.5 py-1 text-xs font-semibold text-brand-700">
                    {t('dayGroup.count', { count: daySlots.length })}
                  </span>
                </header>

                <div className="divide-y divide-ink-100">
                  {daySlots.map((s) => {
                    const confirmed = s.confirmationStatus === 'confirmed'
                    const c = countdown(t, s.startTime, now)
                    return (
                      <div
                        key={s.bookingId}
                        className={`border-l-4 px-4 py-4 sm:px-5 ${
                          confirmed ? 'border-l-emerald-500' : 'border-l-amber-400'
                        }`}
                      >
                        <div className="flex flex-wrap items-start justify-between gap-3">
                          <div className="min-w-0">
                            <p className="font-display text-lg font-extrabold leading-none text-ink-900">
                              {timeRange(s)}
                            </p>
                            <p className="mt-1.5 truncate text-sm font-semibold text-ink-700">
                              {jobTitleOf(s)}
                            </p>
                            <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-ink-400">
                              <span className="rounded-md bg-ink-100 px-1.5 py-0.5 font-medium text-ink-600">
                                {t('scheduleItem.roundLabel', { round: s.roundNumber })}
                              </span>
                              <span className="inline-flex items-center gap-1">
                                <Clock className="h-3 w-3" /> {s.timezone}
                              </span>
                            </div>
                          </div>
                          <div className="flex shrink-0 flex-col items-end gap-1.5">
                            {confirmed ? (
                              <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700">
                                <CheckCircle2 className="h-3.5 w-3.5" /> {t('scheduleItem.confirmed')}
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-700">
                                <AlertCircle className="h-3.5 w-3.5" />{' '}
                                {t('scheduleItem.pendingConfirmation')}
                              </span>
                            )}
                            <span
                              className={`inline-flex items-center gap-1 text-xs font-medium ${
                                c.urgent ? 'text-amber-700' : 'text-ink-400'
                              }`}
                            >
                              <Timer className="h-3 w-3" /> {c.text}
                            </span>
                          </div>
                        </div>

                        {confirmed ? (
                          <p className="mt-3 flex items-start gap-1.5 rounded-lg bg-emerald-50 px-3 py-2 text-xs leading-relaxed text-emerald-800">
                            <Lock className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                            {t('scheduleItem.confirmedLocked')}
                          </p>
                        ) : (
                          <div className="mt-3 flex flex-wrap gap-2">
                            <button
                              type="button"
                              onClick={() => openModal(s.bookingId, 'confirm')}
                              className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2 text-sm font-semibold text-white transition hover:opacity-90"
                            >
                              <CheckCircle2 className="h-4 w-4" /> {t('scheduleItem.confirmButton')}
                            </button>
                            <button
                              type="button"
                              onClick={() => openModal(s.bookingId, 'decline')}
                              className="inline-flex items-center gap-1.5 rounded-xl border border-ink-300 bg-white px-4 py-2 text-sm font-medium text-ink-700 transition hover:bg-ink-50"
                            >
                              <CalendarClock className="h-4 w-4" /> {t('scheduleItem.declineButton')}
                            </button>
                          </div>
                        )}
                      </div>
                    )
                  })}
                </div>
              </section>
            ))}

            {/* ===== Nhắc mang gì đi ===== */}
            {upcoming.length > 0 && (
              <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
                <h3 className="flex items-center gap-2 text-sm font-bold text-ink-900">
                  <KeyRound className="h-4 w-4 text-brand-600" /> {t('prep.title')}
                </h3>
                <ul className="mt-3 space-y-2">
                  {[t('prep.item1'), t('prep.item2'), t('prep.item3')].map((line) => (
                    <li key={line} className="flex items-start gap-2 text-sm text-ink-600">
                      <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-500" />
                      <span className="leading-relaxed">{line}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* ===== Mời luyện tập ===== */}
            {upcoming.length > 0 && (
              <div className="flex flex-wrap items-center gap-4 rounded-2xl border border-ai-100 bg-gradient-to-r from-brand-50 to-ai-50 p-5">
                <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-white text-ai-600 shadow-card">
                  <Sparkles className="h-5 w-5" />
                </span>
                <div className="min-w-0 flex-1">
                  <div className="text-sm font-bold text-ink-900">{t('practiceCard.title')}</div>
                  <p className="mt-0.5 text-xs leading-relaxed text-ink-600">
                    {t('practiceCard.description')}
                  </p>
                </div>
                <Link
                  to="/candidate/applications"
                  className="inline-flex shrink-0 items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-semibold text-white transition hover:opacity-90"
                >
                  <Play className="h-4 w-4" /> {t('practiceLink')}
                </Link>
              </div>
            )}

            {/* ===== Đã diễn ra ===== */}
            {past.length > 0 && (
              <section className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
                <header className="flex items-center gap-2 border-b border-ink-100 bg-ink-50/70 px-4 py-3 sm:px-5">
                  <History className="h-4 w-4 text-ink-400" />
                  <h3 className="text-sm font-semibold text-ink-700">{t('pastSection.title')}</h3>
                </header>
                <ul className="divide-y divide-ink-100">
                  {past.map((s) => (
                    <li
                      key={s.bookingId}
                      className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 sm:px-5"
                    >
                      <span className="text-sm font-semibold text-ink-500">{timeRange(s)}</span>
                      <span className="min-w-0 flex-1 truncate text-sm text-ink-500">
                        {jobTitleOf(s)} ·{' '}
                        {t('scheduleItem.roundLabel', { round: s.roundNumber })}
                      </span>
                      <span className="shrink-0 text-xs text-ink-400">{dayKey(s.startTime)}</span>
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </div>
        )}
      </div>

      {modal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-ink-900/50 p-4 backdrop-blur-sm"
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
              <h2 className="font-display text-base font-extrabold text-ink-900">
                {modal.action === 'confirm' ? t('modal.confirmTitle') : t('modal.declineTitle')}
              </h2>
            </div>

            {modalItem && (
              <p className="mb-3 rounded-xl bg-ink-50 px-3 py-2.5 text-sm leading-relaxed text-ink-700">
                {t('modal.confirmSummary', {
                  jobTitle: jobTitleOf(modalItem),
                  round: modalItem.roundNumber,
                  day: dayKey(modalItem.startTime),
                  time: timeRange(modalItem),
                })}
              </p>
            )}

            {modal.action === 'decline' && (
              <div className="mb-3">
                <label className="mb-1 block text-xs font-medium text-ink-600">
                  {t('modal.declineReasonLabel')}
                </label>
                <textarea
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  rows={3}
                  maxLength={500}
                  autoFocus
                  placeholder={t('modal.declineReasonPlaceholder')}
                  className="w-full rounded-xl border border-ink-200 bg-white px-3 py-2 text-sm text-ink-800 focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-100"
                />
                {/* Lý do là thứ nhân sự dựa vào để xếp ca mới — gợi ý thẳng cách viết cho có ích. */}
                <p
                  className="mt-1.5 text-xs leading-relaxed text-ink-500"
                  dangerouslySetInnerHTML={{ __html: t('modal.declineReasonHint') }}
                />
              </div>
            )}

            <div className="mb-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3">
              <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
              <p className="text-xs leading-relaxed text-amber-800">
                {t('modal.warning')}
                {modal.action === 'decline' && t('modal.warningDecline')}
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
                className="inline-flex items-center gap-1.5 rounded-xl border border-ink-300 bg-white px-4 py-2 text-sm font-medium text-ink-700 transition hover:bg-ink-50 disabled:opacity-50"
              >
                <X className="h-4 w-4" /> {t('modal.laterButton')}
              </button>
              <button
                type="button"
                onClick={submitModal}
                disabled={submitting}
                className={`inline-flex items-center gap-1.5 rounded-xl px-4 py-2 text-sm font-semibold text-white transition disabled:opacity-50 ${
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
                {modal.action === 'confirm' ? t('modal.confirmButton') : t('modal.declineButton')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
