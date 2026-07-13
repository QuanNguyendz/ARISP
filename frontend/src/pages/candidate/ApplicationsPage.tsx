import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Sparkles,
  MapPin,
  Mail,
  Phone,
  Check,
  Clock,
  Eye,
  XCircle,
  FileText,
  File as FileIcon,
  Briefcase,
  AlertCircle,
  ChevronRight,
  Code2,
  Server,
  BrainCircuit,
  LayoutDashboard,
  Copy,
  Play,
  Download,
  UploadCloud,
  CalendarClock,
  BadgeCheck,
  CheckCircle2,
  Circle,
  MessageSquareText,
  ArrowDownUp,
  Pencil,
} from 'lucide-react'
import { applicationService } from '@services/application/applicationService'
import { profileService } from '@services/profile/profileService'
import { CANDIDATE_DATA_REFRESH_EVENT } from '@services/notification/notificationService'
import type { CandidateProfile } from '@services/profile/profileService'
import { resolveAssetUrl } from '@config/constants'
import { useAuthStore } from '@store/auth/authStore'
import { Skeleton } from '@components/ui/Skeleton'
import { useDocumentViewer } from '@components/document/DocumentViewer'
import type { MyApplicationItem, MyApplicationRound } from '../../types/application'

type FilterKey = 'all' | 'action' | 'processing' | 'done'
type TFunction = (key: string, options?: Record<string, unknown>) => string

function metaOf(t: TFunction, status: string) {
  const labels: Record<
    string,
    { label: string; group: Exclude<FilterKey, 'all'>; icon: typeof Clock }
  > = {
    invited: { label: t('applications.status.invited'), group: 'processing', icon: Eye },
    cv_submitted: { label: t('applications.status.cvSubmitted'), group: 'processing', icon: Eye },
    screening: { label: t('applications.status.screening'), group: 'processing', icon: Clock },
    interview: { label: t('applications.status.interview'), group: 'action', icon: AlertCircle },
    pass: { label: t('applications.status.pass'), group: 'done', icon: Check },
    not_pass: { label: t('applications.status.notPass'), group: 'done', icon: XCircle },
    withdrawn: { label: t('applications.status.withdrawn'), group: 'done', icon: XCircle },
  }
  return (
    labels[status] ?? {
      label: status,
      group: 'processing' as const,
      icon: Clock,
    }
  )
}

/**
 * Nhóm hiển thị của một hồ sơ — phản ánh quy trình:
 * có việc cần ứng viên làm (mã phỏng vấn còn hiệu lực, hoặc đã qua CV và còn lượt phỏng vấn thử)
 * → "Cần hành động"; còn lại theo trạng thái gốc (HR xem hồ sơ → Đang xử lý; pass/not_pass → Đã hoàn tất).
 */
function groupOf(t: TFunction, app: MyApplicationItem): Exclude<FilterKey, 'all'> {
  if (app.interviewCode) return 'action'
  if (app.practiceAvailable) return 'action'
  return metaOf(t, app.status).group
}

function formatDate(iso?: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function formatRelative(t: TFunction, iso?: string | null): string {
  if (!iso) return ''
  const diffMs = Date.now() - new Date(iso).getTime()
  const mins = Math.round(diffMs / 60000)
  if (mins < 1) return t('applications.appliedTimeAgo')
  if (mins < 60) return t('applications.minutesAgo', { count: mins })
  const hrs = Math.round(mins / 60)
  if (hrs < 24) return t('applications.hoursAgo', { count: hrs })
  const days = Math.round(hrs / 24)
  if (days === 1) return t('applications.yesterday')
  return t('applications.daysAgo', { count: days })
}

/** Đếm ngược tới thời điểm hết hạn → "1g 48p" / "Đã hết hạn". */
function formatCountdown(t: TFunction, iso: string): string {
  const diff = new Date(iso).getTime() - Date.now()
  if (diff <= 0) return t('applications.countdownExpired')
  const totalMin = Math.floor(diff / 60000)
  const h = Math.floor(totalMin / 60)
  const m = totalMin % 60
  if (h > 0) return `${h}g ${m}p`
  return `${m}p`
}

/** Thông tin hiển thị lịch phỏng vấn: ngày đầy đủ (thứ, dd/mm/yyyy), giờ và nhãn tương đối. */
function scheduleInfo(t: TFunction, iso: string, now: number) {
  const d = new Date(iso)
  const diff = d.getTime() - now
  const mins = Math.round(diff / 60000)
  let rel: string
  if (diff <= 0) rel = t('applications.scheduleRel.inProgress')
  else if (mins < 60) rel = t('applications.scheduleRel.minutesLeft', { count: mins })
  else if (mins < 24 * 60) {
    const h = Math.floor(mins / 60)
    const m = mins % 60
    rel =
      m > 0
        ? t('applications.scheduleRel.minutesLeft', { count: h * 60 + m })
        : t('applications.scheduleRel.minutesLeft', { count: h * 60 })
  } else {
    const days = Math.round(mins / (24 * 60))
    rel =
      days === 1
        ? t('applications.scheduleRel.tomorrow')
        : t('applications.scheduleRel.daysLeft', { count: days })
  }
  return {
    day: d.getDate(),
    month: d.getMonth() + 1,
    dateFull: d.toLocaleDateString('vi-VN', {
      weekday: 'long',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    }),
    time: d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
    rel,
  }
}

function initialsOf(name?: string, email?: string): string {
  const src = (name || email || 'U').trim()
  const parts = src.split(/\s+/).filter(Boolean)
  if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
  return src.slice(0, 2).toUpperCase()
}

function deptIcon(department?: string | null) {
  const d = (department || '').toLowerCase()
  if (d.includes('data') || d.includes('ai') || d.includes('ml')) return BrainCircuit
  if (d.includes('backend') || d.includes('server') || d.includes('devops')) return Server
  if (d.includes('qa') || d.includes('test')) return Code2
  if (d.includes('full')) return LayoutDashboard
  return Code2
}

function roundTypeLabel(t: TFunction, type?: string): string {
  const map: Record<string, string> = {
    screening: t('applications.roundType.screening'),
    technical: t('applications.roundType.technical'),
    online_test: t('applications.roundType.online_test'),
    final: t('applications.roundType.final'),
  }
  return map[(type || '').toLowerCase()] || type || ''
}

function RoundStepper({ t, rounds }: { t: (key: string) => string; rounds: MyApplicationRound[] }) {
  if (!rounds.length) return null
  return (
    <div className="mt-4 flex flex-wrap items-center gap-1.5 text-xs">
      {rounds.map((r, i) => {
        const done = r.status === 'completed'
        const active = r.status === 'in_progress' || r.status === 'active'
        const cls = done
          ? 'bg-emerald-50 text-emerald-700'
          : active
            ? 'bg-brand-50 text-brand-700'
            : 'bg-ink-100 text-ink-400'
        return (
          <span key={i} className="flex items-center gap-1.5">
            {i > 0 && <span className="h-px w-4 bg-ink-200" />}
            <span
              className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 font-semibold ${cls}`}
            >
              {done && <Check className="h-3.5 w-3.5" />}V{r.roundNumber}{' '}
              {roundTypeLabel(t, r.roundType)}
              {r.verdict && (
                <span className={r.verdict === 'pass' ? 'text-emerald-700' : 'text-red-600'}>
                  ·{' '}
                  {r.verdict === 'pass'
                    ? t('applications.status.pass')
                    : t('applications.status.notPass')}
                </span>
              )}
            </span>
          </span>
        )
      })}
    </div>
  )
}

function MatchBadge({ score }: { score?: number | null }) {
  if (typeof score !== 'number') return null
  return (
    <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full bg-ai-50 px-2.5 py-1 text-xs font-bold text-ai-700 ring-1 ring-ai-200">
      <Sparkles className="h-3.5 w-3.5" /> Match {score}
    </span>
  )
}

function CardFooter({
  t,
  app,
  action,
}: {
  t: TFunction
  app: MyApplicationItem
  action?: React.ReactNode
}) {
  return (
    <div className="mt-3 flex items-center justify-between border-t border-ink-100 pt-3">
      <span className="text-xs text-ink-400">
        {t('applications.appliedTime')} {formatDate(app.createdAt)}
        {app.updatedAt &&
          app.updatedAt !== app.createdAt &&
          ` · ${t('applications.updatedTime', { time: formatRelative(t, app.updatedAt) })}`}
      </span>
      {action ?? (
        <Link
          to={`/candidate/applications/${app.id}`}
          className="inline-flex items-center gap-1 rounded-xl border border-ink-200 px-4 py-1.5 text-sm font-semibold text-ink-700 hover:bg-ink-50"
        >
          {t('applications.viewDetail')} <ChevronRight className="h-4 w-4" />
        </Link>
      )}
    </div>
  )
}

function ApplicationCard({ t, app }: { t: TFunction; app: MyApplicationItem }) {
  const meta = metaOf(t, app.status)
  const statusCls: Record<string, string> = {
    invited: 'bg-brand-50 text-brand-700 ring-brand-200',
    cv_submitted: 'bg-brand-50 text-brand-700 ring-brand-200',
    screening: 'bg-brand-50 text-brand-700 ring-brand-200',
    interview: 'bg-amber-50 text-amber-700 ring-amber-200',
    pass: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
    not_pass: 'bg-red-50 text-red-700 ring-red-200',
    withdrawn: 'bg-ink-100 text-ink-500 ring-ink-200',
  }
  const StatusIcon = meta.icon
  const Icon = deptIcon(app.department)
  const [copied, setCopied] = useState(false)
  const hasCode = !!app.interviewCode
  const isClosed = app.status === 'not_pass' || app.status === 'withdrawn'
  // Practice chỉ hiện khi backend xác nhận đủ điều kiện (ĐÃ QUA vòng CV — ADR-038) và chưa có mã On-site.
  const showPractice = app.practiceAvailable && !hasCode

  const copyCode = () => {
    if (!app.interviewCode) return
    navigator.clipboard?.writeText(app.interviewCode.code).then(() => {
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1800)
    })
  }

  const borderCls = hasCode
    ? 'border-amber-300'
    : isClosed
      ? 'border-ink-200 opacity-90'
      : 'border-ink-200 hover:border-brand-200 hover:shadow-card-hover'

  return (
    <article
      className={`group overflow-hidden rounded-2xl border bg-white shadow-card transition ${borderCls}`}
    >
      {hasCode && (
        <div className="flex items-center gap-2 bg-amber-50 px-5 py-2 text-xs font-semibold text-amber-700">
          <AlertCircle className="h-4 w-4" />{' '}
          {t('applications.actionNeeded', { round: app.interviewCode!.roundNumber })}
        </div>
      )}
      <div className="p-5">
        <div className="flex gap-4">
          <div
            className={`grid h-12 w-12 shrink-0 place-items-center rounded-xl ${
              isClosed ? 'bg-ink-100 text-ink-400' : 'bg-brand-50 text-brand-600'
            }`}
          >
            <Icon className="h-6 w-6" />
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h3
                  className={`font-semibold ${isClosed ? 'text-ink-700' : 'text-ink-900 group-hover:text-brand-700'}`}
                >
                  {app.jobTitle || t('applications.jobPosting')}
                </h3>
                <div className="mt-0.5 flex items-center gap-1.5 text-sm text-ink-500">
                  <MapPin className="h-3.5 w-3.5" />
                  {[app.location, app.department].filter(Boolean).join(' · ') ||
                    t('applications.locationUnknown')}
                </div>
              </div>
              <div className="flex shrink-0 flex-col items-end gap-1.5">
                <span
                  className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ring-1 ${statusCls[app.status] || 'bg-ink-100 text-ink-600 ring-ink-200'}`}
                >
                  <StatusIcon className="h-3.5 w-3.5" /> {meta.label}
                </span>
                <MatchBadge score={app.matchScore} />
              </div>
            </div>

            <RoundStepper t={t} rounds={app.rounds} />

            {/* Mã phỏng vấn On-site */}
            {hasCode && (
              <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50/60 p-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <div className="text-xs font-medium text-ink-500">
                      {t('applications.interviewCode')}
                    </div>
                    <div className="mt-1 font-display text-2xl font-extrabold tracking-[0.3em] text-ink-900">
                      {app.interviewCode!.code}
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="inline-flex items-center gap-1 text-xs font-semibold text-amber-700">
                      <Clock className="h-3.5 w-3.5" /> {t('applications.expireCountdown')}{' '}
                      {formatCountdown(t, app.interviewCode!.expiresAt)}
                    </div>
                    <div className="mt-0.5 text-[11px] text-ink-400">
                      {t('applications.oneTimeSixChars')}
                    </div>
                  </div>
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Link
                    to={`/jobs/${app.jobPostingId}`}
                    className="flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2 text-sm font-bold text-white hover:bg-brand-700"
                  >
                    <MapPin className="h-4 w-4" /> {t('applications.viewJobPosting')}
                  </Link>
                  <button
                    onClick={copyCode}
                    className="flex items-center gap-2 rounded-xl border border-ink-200 bg-white px-4 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50"
                  >
                    <Copy className="h-4 w-4" />{' '}
                    {copied ? t('applications.copied') : t('applications.copyCode')}
                  </button>
                </div>
              </div>
            )}

            {/* Chờ HR xác nhận (AI đã chấm nhưng chưa chia sẻ — không lộ điểm theo mô hình bảo mật) */}
            {!hasCode && app.pendingHrReview && (
              <div className="mt-3 rounded-xl bg-ink-50 p-3 text-sm">
                <div className="flex items-center gap-2 text-ink-600">
                  <Clock className="h-4 w-4 text-amber-600" /> {t('applications.hrConfirming')}
                </div>
                <p className="mt-1 text-xs text-ink-400">{t('applications.hrConfirmingHint')}</p>
              </div>
            )}

            {/* Phỏng vấn thử (Practice) */}
            {showPractice && (
              <div className="mt-3 rounded-xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-3">
                <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
                  <Sparkles className="h-4 w-4" />{' '}
                  {t('applications.practiceRemote', { round: app.activeRound ?? 1 })}
                </div>
                <p
                  className="mt-1 text-xs text-ink-500"
                  dangerouslySetInnerHTML={{ __html: t('applications.practiceAvailable') }}
                />
                <Link
                  to={`/interview/practice/${app.id}?round=${app.activeRound ?? 1}`}
                  className="mt-2 inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2 text-sm font-bold text-white hover:opacity-90"
                >
                  <Play className="h-4 w-4" /> {t('applications.startPractice')}
                </Link>
              </div>
            )}

            {/* Phản hồi khi đã kết thúc */}
            {isClosed && app.hrFeedback && (
              <div className="mt-3 rounded-xl bg-ink-50 p-3 text-sm text-ink-600">
                {app.hrFeedback}
              </div>
            )}

            <CardFooter
              t={t}
              app={app}
              action={
                isClosed ? (
                  <Link
                    to={`/candidate/applications/${app.id}`}
                    className="inline-flex items-center gap-2 rounded-xl border border-ink-200 px-4 py-1.5 text-sm font-semibold text-ink-700 hover:bg-ink-50"
                  >
                    <MessageSquareText className="h-4 w-4" /> {t('applications.viewFeedback')}
                  </Link>
                ) : undefined
              }
            />
          </div>
        </div>
      </div>
    </article>
  )
}

/** Khung skeleton (shimmer) mô phỏng bố cục trang khi đang tải. */
function ApplicationsSkeleton() {
  return (
    <div className="mx-auto grid max-w-6xl gap-8 px-6 py-6 lg:grid-cols-[1fr_320px]">
      {/* LEFT */}
      <div className="space-y-6">
        {/* Profile banner */}
        <div className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
          <Skeleton className="h-20 rounded-none" />
          <div className="px-6 pb-6">
            <Skeleton className="-mt-10 h-20 w-20 rounded-2xl ring-4 ring-white dark:ring-ink-900" />
            <div className="mt-3 flex items-center justify-between gap-3">
              <div className="space-y-2">
                <Skeleton className="h-6 w-48" />
                <Skeleton className="h-4 w-36" />
              </div>
              <Skeleton className="h-9 w-28 rounded-xl" />
            </div>
            <div className="mt-4 flex flex-wrap gap-4">
              <Skeleton className="h-4 w-44" />
              <Skeleton className="h-4 w-28" />
              <Skeleton className="h-4 w-40" />
            </div>
          </div>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
              <Skeleton className="h-7 w-10" />
              <Skeleton className="mt-2 h-3 w-20" />
            </div>
          ))}
        </div>

        {/* Filter chips */}
        <div className="flex gap-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-8 w-24 rounded-full" />
          ))}
        </div>

        {/* Cards */}
        <div className="space-y-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
              <div className="flex gap-4">
                <Skeleton className="h-12 w-12 shrink-0 rounded-xl" />
                <div className="min-w-0 flex-1">
                  <div className="flex items-start justify-between gap-3">
                    <div className="space-y-2">
                      <Skeleton className="h-5 w-56" />
                      <Skeleton className="h-4 w-40" />
                    </div>
                    <Skeleton className="h-6 w-24 rounded-full" />
                  </div>
                  <div className="mt-4 flex gap-1.5">
                    <Skeleton className="h-7 w-28 rounded-full" />
                    <Skeleton className="h-7 w-28 rounded-full" />
                  </div>
                  <div className="mt-4 flex items-center justify-between border-t border-ink-100 pt-3">
                    <Skeleton className="h-3 w-48" />
                    <Skeleton className="h-8 w-28 rounded-xl" />
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* RIGHT sidebar */}
      <aside className="space-y-5">
        {Array.from({ length: 2 }).map((_, i) => (
          <div key={i} className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="mt-3 h-16 w-full rounded-xl" />
            <Skeleton className="mt-3 h-9 w-full rounded-xl" />
          </div>
        ))}
      </aside>
    </div>
  )
}

export default function ApplicationsPage() {
  const { t } = useTranslation('candidate')
  const { user } = useAuthStore()
  const { openDocument } = useDocumentViewer()
  const [apps, setApps] = useState<MyApplicationItem[]>([])
  const [profile, setProfile] = useState<CandidateProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [filter, setFilter] = useState<FilterKey>('all')
  const [now, setNow] = useState(() => Date.now())

  const FILTERS: { key: FilterKey; label: string }[] = [
    { key: 'all', label: t('applications.all') },
    { key: 'action', label: t('applications.action') },
    { key: 'processing', label: t('applications.processing') },
    { key: 'done', label: t('applications.done') },
  ]

  // silent = refetch nền (không bật skeleton) khi có push SignalR — giữ nguyên UI, chỉ đổi dữ liệu.
  const loadData = useCallback(
    async (silent = false) => {
      if (!silent) setLoading(true)
      try {
        const [data, prof] = await Promise.all([
          applicationService.getMyApplications(),
          profileService.getProfile().catch(() => null),
        ])
        setApps(data)
        setProfile(prof)
      } catch (err) {
        if (!silent) setError(err instanceof Error ? err.message : t('applications.error'))
      } finally {
        if (!silent) setLoading(false)
      }
    },
    [t]
  )

  useEffect(() => {
    loadData()
  }, [loadData])

  // Cấp mã phỏng vấn / đổi trạng thái → useAppNotifications phát event này → refetch nền để
  // bảng hiển thị mã mới tức thời, không cần F5.
  useEffect(() => {
    const handler = () => loadData(true)
    window.addEventListener(CANDIDATE_DATA_REFRESH_EVENT, handler)
    return () => window.removeEventListener(CANDIDATE_DATA_REFRESH_EVENT, handler)
  }, [loadData])

  const counts = useMemo(() => {
    const c = { all: apps.length, action: 0, processing: 0, done: 0 }
    apps.forEach((a) => {
      c[groupOf(t, a)]++
    })
    return c
  }, [apps, t])

  const filtered = useMemo(
    () => (filter === 'all' ? apps : apps.filter((a) => groupOf(t, a) === filter)),
    [apps, filter, t]
  )

  const passedRounds = useMemo(
    () => apps.reduce((n, a) => n + a.rounds.filter((r) => r.verdict === 'pass').length, 0),
    [apps]
  )

  // Cập nhật realtime mỗi 30s để đếm ngược chạy và tự ẩn lịch đã qua.
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 30000)
    return () => clearInterval(id)
  }, [])

  // Lịch phỏng vấn sắp tới gần nhất trên toàn bộ hồ sơ (chỉ lấy mốc còn ở tương lai).
  const nextSchedule = useMemo(() => {
    const items = apps
      .filter((a) => a.upcomingInterview && new Date(a.upcomingInterview.startTime).getTime() > now)
      .map((a) => ({ app: a, slot: a.upcomingInterview! }))
      .sort((x, y) => new Date(x.slot.startTime).getTime() - new Date(y.slot.startTime).getTime())
    return items[0] ?? null
  }, [apps, now])

  // Độ hoàn thiện hồ sơ (đồng bộ với ProfilePage).
  const completeness = useMemo(() => {
    if (!profile) return null
    const checks = [
      {
        ok: !!profile.fullName && !!profile.headline && !!profile.phone,
        label: t('applications.profileChecks.personalInfo'),
      },
      { ok: !!profile.profileCvUrl, label: t('applications.profileChecks.uploadCv') },
      {
        ok: profile.skills.length > 0 && profile.experience.length > 0,
        label: t('applications.profileChecks.skillsExperience'),
      },
      {
        ok: !!(profile.linkedinUrl || profile.githubUrl || profile.portfolioUrl),
        label: t('applications.profileChecks.links'),
      },
    ]
    const pct = Math.round((checks.filter((c) => c.ok).length / checks.length) * 100)
    return { pct, checks }
  }, [profile, t])

  const displayName = profile?.fullName || user?.name || t('profile.candidate')
  const initials = initialsOf(displayName, user?.email)

  return (
    <>
      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/jobs" className="hover:text-brand-600">
            {t('profile.home')}
          </Link>
          <ChevronRight className="h-4 w-4" />
          <span className="font-medium text-ink-600">{t('applications.myApplications')}</span>
        </div>
      </div>

      {loading ? (
        <ApplicationsSkeleton />
      ) : (
        <div className="mx-auto grid max-w-6xl gap-8 px-6 py-6 lg:grid-cols-[1fr_320px]">
          {/* LEFT */}
          <div className="space-y-6">
            {/* Profile banner */}
            <div className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
              <div className="h-20 bg-gradient-to-r from-brand-600 via-ai-600 to-ai-500" />
              <div className="px-6 pb-6">
                <div className="-mt-10">
                  <span className="grid h-20 w-20 shrink-0 place-items-center rounded-2xl bg-gradient-to-br from-brand-600 to-ai-600 text-2xl font-extrabold text-white shadow-card ring-4 ring-white dark:ring-ink-900">
                    {initials}
                  </span>
                </div>
                <div className="mt-3 flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h1 className="font-display text-2xl font-extrabold leading-tight">
                      {displayName}
                    </h1>
                    {profile?.headline && (
                      <p className="text-sm text-ink-500">{profile.headline}</p>
                    )}
                  </div>
                  <Link
                    to="/candidate/profile"
                    className="flex items-center gap-2 rounded-xl border border-ink-200 px-4 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50"
                  >
                    <Pencil className="h-4 w-4" /> {t('applications.editProfile')}
                  </Link>
                </div>
                <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-ink-500">
                  <span className="inline-flex items-center gap-1.5">
                    <Mail className="h-4 w-4 text-ink-400" /> {profile?.email || user?.email}
                  </span>
                  {profile?.phone && (
                    <span className="inline-flex items-center gap-1.5">
                      <Phone className="h-4 w-4 text-ink-400" /> {profile.phone}
                    </span>
                  )}
                  {profile?.location && (
                    <span className="inline-flex items-center gap-1.5">
                      <MapPin className="h-4 w-4 text-ink-400" /> {profile.location}
                    </span>
                  )}
                </div>
              </div>
            </div>

            {/* Stats */}
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
              <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
                <div className="font-display text-2xl font-extrabold">{counts.all}</div>
                <div className="mt-0.5 text-xs text-ink-500">
                  {t('applications.totalApplications')}
                </div>
              </div>
              <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
                <div className="font-display text-2xl font-extrabold text-brand-600">
                  {counts.processing}
                </div>
                <div className="mt-0.5 text-xs text-ink-500">{t('applications.processing')}</div>
              </div>
              <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
                <div className="font-display text-2xl font-extrabold text-amber-600">
                  {counts.action}
                </div>
                <div className="mt-0.5 text-xs text-ink-500">{t('applications.action')}</div>
              </div>
              <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
                <div className="font-display text-2xl font-extrabold text-emerald-600">
                  {passedRounds}
                </div>
                <div className="mt-0.5 text-xs text-ink-500">{t('applications.passedRounds')}</div>
              </div>
            </div>

            {/* Filter tabs */}
            <div className="flex items-center gap-2 overflow-x-auto pb-1 text-sm">
              {FILTERS.map((f) => (
                <button
                  key={f.key}
                  onClick={() => setFilter(f.key)}
                  className={`shrink-0 rounded-full px-4 py-1.5 font-medium ${
                    filter === f.key
                      ? 'bg-brand-600 font-semibold text-white'
                      : 'border border-ink-200 bg-white text-ink-600 hover:border-brand-300'
                  }`}
                >
                  {f.label}{' '}
                  <span className={filter === f.key ? 'opacity-80' : 'text-ink-400'}>
                    {counts[f.key]}
                  </span>
                </button>
              ))}
              <span className="ml-auto hidden shrink-0 items-center gap-1.5 text-ink-400 sm:flex">
                <ArrowDownUp className="h-4 w-4" /> {t('applications.newest')}
              </span>
            </div>

            {/* List */}
            {error ? (
              <div className="flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
                <AlertCircle className="h-4 w-4" /> {error}
              </div>
            ) : filtered.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-ink-300 bg-white p-10 text-center shadow-card">
                <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
                  <FileText className="h-6 w-6" />
                </div>
                <p className="mt-3 font-semibold text-ink-700">
                  {t('applications.noApplications')}
                </p>
                <p className="mt-1 text-sm text-ink-500">{t('applications.exploreJobs')}</p>
                <Link
                  to="/jobs"
                  className="mt-4 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
                >
                  <Briefcase className="h-4 w-4" /> {t('applications.findJobsNow')}
                </Link>
              </div>
            ) : (
              <div className="space-y-4">
                {filtered.map((app) => (
                  <ApplicationCard key={app.id} t={t} app={app} />
                ))}
              </div>
            )}
          </div>

          {/* RIGHT sidebar */}
          <aside className="space-y-5 self-start lg:sticky lg:top-24">
            {/* CV card */}
            <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
              <div className="flex items-center justify-between">
                <span className="flex items-center gap-2 text-sm font-semibold">
                  <FileText className="h-4 w-4 text-brand-600" /> {t('applications.cvOfYours')}
                </span>
                {profile?.profileCvUrl && (
                  <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                    {t('applications.inUse')}
                  </span>
                )}
              </div>
              {profile?.profileCvUrl ? (
                <>
                  <div className="mt-3 flex items-center gap-3 rounded-xl border border-ink-200 p-3">
                    <div className="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-red-50 text-red-600">
                      <FileIcon className="h-5 w-5" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm font-medium text-ink-800">
                        {profile.cvFileName || t('applications.cv.profileCv')}
                      </div>
                      <div className="text-xs text-ink-400">
                        {(profile.cvFileName?.split('.').pop() || 'CV').toUpperCase()} ·{' '}
                        {t('applications.cv.profileDocument')}
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() =>
                        openDocument(
                          resolveAssetUrl(profile.profileCvUrl),
                          profile.cvFileName || t('applications.cv.profileCv')
                        )
                      }
                      className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-brand-600"
                      title={t('applications.cv.viewCv')}
                      aria-label={t('applications.cv.viewCv')}
                    >
                      <Eye className="h-4 w-4" />
                    </button>
                    <a
                      href={resolveAssetUrl(profile.cvDownloadUrl || profile.profileCvUrl)}
                      download={profile.cvFileName || true}
                      className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-brand-600"
                      title={t('applications.cv.downloadCv')}
                      aria-label={t('applications.cv.downloadCvAria')}
                    >
                      <Download className="h-4 w-4" />
                    </a>
                  </div>
                  <Link
                    to="/candidate/profile?focus=cv"
                    className="mt-3 flex w-full items-center justify-center gap-2 rounded-xl border border-ink-200 px-3 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50"
                  >
                    <UploadCloud className="h-4 w-4" /> {t('applications.updateCv')}
                  </Link>
                </>
              ) : (
                <>
                  <p className="mt-2 text-sm text-ink-500">{t('applications.noCvYet')}</p>
                  <Link
                    to="/candidate/profile?focus=cv"
                    className="mt-3 flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-3 py-2 text-sm font-semibold text-white hover:bg-brand-700"
                  >
                    <UploadCloud className="h-4 w-4" /> {t('applications.uploadCv')}
                  </Link>
                </>
              )}
            </div>

            {/* Upcoming schedule */}
            {nextSchedule && (
              <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
                <div className="flex items-center gap-2 text-sm font-semibold">
                  <CalendarClock className="h-4 w-4 text-brand-600" />{' '}
                  {t('applications.upcomingSchedule')}
                </div>
                {(() => {
                  const info = scheduleInfo(t, nextSchedule.slot.startTime, now)
                  return (
                    <div className="mt-3 rounded-xl border border-brand-200 bg-brand-50/60 p-3">
                      <div className="flex items-start gap-3">
                        <div className="grid h-11 w-11 shrink-0 flex-col place-items-center rounded-lg bg-white text-brand-700 ring-1 ring-brand-200">
                          <span className="text-[10px] font-semibold leading-none">
                            {info.month}/{info.day}
                          </span>
                          <span className="font-display text-base font-extrabold leading-none">
                            {info.day}
                          </span>
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-2">
                            <div className="text-sm font-semibold text-ink-800">
                              {t('applications.roundInterview', {
                                number: nextSchedule.slot.roundNumber,
                              })}
                            </div>
                            <span className="shrink-0 rounded-full bg-brand-100 px-2 py-0.5 text-[10px] font-semibold text-brand-700">
                              {info.rel}
                            </span>
                          </div>
                          <div className="truncate text-xs text-ink-500">
                            {nextSchedule.app.jobTitle}
                          </div>
                          <div className="mt-1 text-xs font-medium capitalize text-ink-600">
                            {info.dateFull}
                          </div>
                          <div className="mt-0.5 inline-flex items-center gap-1 text-xs text-ink-400">
                            <Clock className="h-3.5 w-3.5" />
                            {info.time}
                            {nextSchedule.slot.timezone ? ` (${nextSchedule.slot.timezone})` : ''}
                            {nextSchedule.app.location ? ` · ${nextSchedule.app.location}` : ''}
                          </div>
                        </div>
                      </div>
                    </div>
                  )
                })()}
                <p className="mt-2 text-xs text-ink-400">{t('applications.bringCccd')}</p>
              </div>
            )}

            {/* Profile completeness */}
            {completeness && (
              <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
                <div className="flex items-center justify-between text-sm font-semibold">
                  <span className="flex items-center gap-2">
                    <BadgeCheck className="h-4 w-4 text-brand-600" />{' '}
                    {t('applications.profileCompleteness')}
                  </span>
                  <span className="text-brand-600">{completeness.pct}%</span>
                </div>
                <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-ink-100">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-brand-600 to-ai-600 transition-all"
                    style={{ width: `${completeness.pct}%` }}
                  />
                </div>
                <ul className="mt-4 space-y-2 text-sm">
                  {completeness.checks.map((c) => (
                    <li
                      key={c.label}
                      className={`flex items-center gap-2 ${c.ok ? 'text-ink-500' : 'text-ink-700'}`}
                    >
                      {c.ok ? (
                        <CheckCircle2 className="h-4 w-4 text-emerald-500" />
                      ) : (
                        <Circle className="h-4 w-4 text-ink-300" />
                      )}
                      {c.label}
                    </li>
                  ))}
                </ul>
                {completeness.pct < 100 && (
                  <Link
                    to="/candidate/profile"
                    className="mt-3 block w-full rounded-xl bg-brand-600 px-3 py-2 text-center text-sm font-semibold text-white hover:bg-brand-700"
                  >
                    {t('applications.completeNow')}
                  </Link>
                )}
              </div>
            )}

            {/* AI tip */}
            <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50 to-white p-5 shadow-card">
              <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
                <Sparkles className="h-4 w-4" /> {t('applications.aiTip')}
              </div>
              <p className="mt-2 text-sm text-ink-500">{t('applications.aiTipContent')}</p>
              <Link
                to="/jobs"
                className="mt-3 block w-full rounded-xl border border-ai-300 bg-white px-3 py-2 text-center text-sm font-semibold text-ai-700 hover:bg-ai-50"
              >
                {t('applications.findSuitableJobs')}
              </Link>
            </div>
          </aside>
        </div>
      )}
    </>
  )
}
