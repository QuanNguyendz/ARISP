import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Briefcase,
  Users,
  ClipboardCheck,
  FileSignature,
  DoorOpen,
  ArrowRight,
  AlertCircle,
  ClipboardList,
} from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert, LoadingSpinner } from '@ari/shared/ui'
import { jobService } from '@ari/shared/fservices/job'
import { applicationService } from '@ari/shared/fservices/application'
import { offerService, OFFER_STATUS } from '@ari/shared/fservices/offer'
import { interviewService } from '@ari/shared/fservices/interview'
import { formatScore } from '@ari/shared/utils/format'
import { useAuthStore } from '@ari/shared/store/auth'
import type { HrApplicationItem } from '@ari/shared/types/application'

const CARD =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card'

/**
 * Tổng quan của Hiring Manager (ADR-061/067).
 *
 * Phạm vi dữ liệu do SERVER quyết định: các endpoint chỉ trả về tin mà HM được gán vào đội tuyển
 * dụng, nên trang này không cần (và không được) tự lọc gì thêm — chưa được gán tin nào thì mọi
 * danh sách đều rỗng, đó là hành vi đúng.
 *
 * **Trang này là HÀNG CHỜ VIỆC, không phải bảng thống kê.** Bốn ô đếm chỉ để định vị; phần chiếm
 * chỗ là các danh sách BẤM VÀO LÀM ĐƯỢC NGAY, xếp theo mức cấp bách:
 *  1. ứng viên đang ngồi chờ được cho vào phòng phỏng vấn (có người đang đợi thật, tính bằng phút);
 *  2. hồ sơ chờ chính người này duyệt — dẫn thẳng tới màn tin, nơi có CV để đọc trước khi quyết định;
 *  3. tin chờ chính người này ký duyệt mô tả công việc, và thư mời chờ duyệt.
 *
 * Mọi con số đếm từ dữ liệu thật — một số ghi cứng trên bảng điều khiển tệ hơn là không có ô nào,
 * vì người dùng tin vào nó rồi bỏ sót việc.
 */
export default function HmDashboardPage() {
  const { t } = useTranslation('modules/hm/dashboard')
  const user = useAuthStore((s) => s.user)

  const {
    data: jobs,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['hm-jobs'],
    queryFn: () => jobService.getAdminJobPostings(),
  })

  const { data: applications = [] } = useQuery({
    queryKey: ['applications'],
    queryFn: () => applicationService.getApplications(),
  })

  const { data: offers = [] } = useQuery({
    queryKey: ['offers'],
    queryFn: () => offerService.list(),
  })

  // Phòng chờ đổi trạng thái do một thao tác NGOÀI trình duyệt này (ứng viên nhập mã ở quầy lễ
  // tân), nên phải tự hỏi lại — cùng nhịp 10 giây với màn Phòng phỏng vấn.
  const { data: rooms = [] } = useQuery({
    queryKey: ['interview-waiting-rooms'],
    queryFn: () => interviewService.getWaitingRooms(),
    refetchInterval: 10_000,
  })

  const activeJobs = jobs?.filter((j) => j.status === 'active').length ?? 0

  // Tin đang chờ CHÍNH NGƯỜI NÀY ký duyệt mô tả công việc — không phải mọi tin ở trạng thái chờ.
  const jobsNeedingSignOff =
    jobs?.filter((j) => j.hmSignOffStatus === 'pending' && j.hiringManagerUserId === user?.id) ?? []

  const awaitingShortlist = applications.filter(
    (a: HrApplicationItem) => a.status === 'hm_review' && a.hmDecision === 'pending'
  )

  const awaitingOfferApproval = offers.filter((o) => o.status === OFFER_STATUS.PendingApproval)

  const waitingRooms = rooms.filter((r) => r.status === 'waiting')

  const stats = [
    {
      label: t('stats.awaitingShortlist'),
      value: awaitingShortlist.length,
      icon: <Users className="w-4 h-4" />,
      color: 'text-sky-600 dark:text-sky-400',
    },
    {
      label: t('stats.awaitingSignOff'),
      value: jobsNeedingSignOff.length,
      icon: <FileSignature className="w-4 h-4" />,
      color: 'text-amber-600 dark:text-amber-400',
    },
    {
      label: t('stats.awaitingOfferApproval'),
      value: awaitingOfferApproval.length,
      icon: <ClipboardCheck className="w-4 h-4" />,
      color: 'text-indigo-600 dark:text-indigo-400',
    },
    {
      label: t('stats.activeJobs'),
      value: activeJobs,
      icon: <Briefcase className="w-4 h-4" />,
      color: 'text-emerald-600 dark:text-emerald-400',
    },
  ]

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="space-y-6">
        <PageHeader
          title={t('title', { name: user?.name || user?.email || '' })}
          description={t('description')}
        />

        {error && <ErrorAlert message={t('loadError')} />}

        {isLoading ? (
          <LoadingSpinner message={t('loading')} />
        ) : (
          <>
            <StatsGrid stats={stats} />

            {/* Cấp bách nhất: có người đang đứng chờ ngoài cửa phòng phỏng vấn. Đưa lên đầu và tô
                khác hẳn — đây là việc tính bằng phút, không phải bằng ngày. */}
            {waitingRooms.length > 0 && (
              <section className="rounded-2xl border border-amber-200 bg-amber-50 p-5 dark:border-amber-500/25 dark:bg-amber-500/10">
                <h2 className="flex items-center gap-2 text-sm font-semibold text-amber-800 dark:text-amber-300">
                  <AlertCircle className="h-4 w-4" />
                  {t('waitingRooms.title', { count: waitingRooms.length })}
                </h2>
                <ul className="mt-3 space-y-2">
                  {waitingRooms.slice(0, 4).map((r) => (
                    <li key={r.sessionId}>
                      <Link
                        to="/hm/interview-rooms"
                        className="flex items-center justify-between gap-3 rounded-xl bg-white/70 px-3 py-2.5 text-sm transition hover:bg-white dark:bg-white/5 dark:hover:bg-white/10"
                      >
                        <span className="min-w-0">
                          <span className="block truncate font-medium text-ink-900 dark:text-white">
                            {r.candidateName ?? t('waitingRooms.unknownCandidate')}
                          </span>
                          <span className="block truncate text-xs text-ink-500 dark:text-ink-400">
                            {r.jobTitle ?? '—'} ·{' '}
                            {t('waitingRooms.round', { number: r.roundNumber })}
                          </span>
                        </span>
                        <span className="inline-flex shrink-0 items-center gap-1.5 text-xs font-semibold text-amber-700 dark:text-amber-300">
                          <DoorOpen className="h-3.5 w-3.5" /> {t('waitingRooms.action')}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            <div className="grid gap-6 lg:grid-cols-3">
              <div className="space-y-6 lg:col-span-2">
                {/* Hồ sơ chờ duyệt: KHÔNG còn màn danh sách riêng (ADR-067) — mỗi dòng dẫn thẳng
                    tới màn tin, nơi có CV để đọc trước khi quyết định. */}
                <section className={CARD}>
                  <div className="mb-4 flex items-center justify-between gap-3">
                    <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
                      <Users className="h-5 w-5 text-brand-600 dark:text-brand-400" />
                      {t('shortlist.title')}
                    </h2>
                    <span className="text-xs text-ink-400">
                      {t('shortlist.count', { count: awaitingShortlist.length })}
                    </span>
                  </div>

                  {awaitingShortlist.length === 0 ? (
                    <EmptyState
                      icon={<Users className="h-8 w-8 text-ink-400" />}
                      title={t('shortlist.emptyTitle')}
                      description={t('shortlist.emptyDescription')}
                    />
                  ) : (
                    <ul className="divide-y divide-ink-100 dark:divide-white/10">
                      {awaitingShortlist.slice(0, 6).map((a: HrApplicationItem) => (
                        <li key={a.id}>
                          <Link
                            to={`/hm/jobs/${a.jobPostingId}`}
                            className="-mx-2 flex items-center justify-between gap-4 rounded-lg px-2 py-3 transition-colors hover:bg-ink-50 dark:hover:bg-white/5"
                          >
                            <div className="min-w-0">
                              <p className="truncate font-medium text-ink-900 dark:text-white">
                                {a.candidateName}
                              </p>
                              <p className="truncate text-sm text-ink-500 dark:text-ink-400">
                                {a.jobTitle ?? '—'}
                              </p>
                            </div>
                            <div className="flex shrink-0 items-center gap-3">
                              {typeof a.matchScore === 'number' && (
                                <span className="text-sm font-semibold text-ink-600 dark:text-ink-300">
                                  {formatScore(a.matchScore)}
                                </span>
                              )}
                              <ArrowRight className="h-4 w-4 text-ink-400" />
                            </div>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>

                <section className={CARD}>
                  <div className="mb-4 flex items-center justify-between gap-3">
                    <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
                      <Briefcase className="h-5 w-5 text-brand-600 dark:text-brand-400" />
                      {t('jobs.title')}
                    </h2>
                    <Link
                      to="/hm/jobs"
                      className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
                    >
                      {t('jobs.viewAll')}
                    </Link>
                  </div>
                  <p className="mb-4 text-sm text-ink-600 dark:text-ink-400">
                    {t('jobs.description')}
                  </p>

                  {!jobs || jobs.length === 0 ? (
                    <EmptyState
                      icon={<Briefcase className="h-8 w-8 text-ink-400" />}
                      title={t('jobs.emptyTitle')}
                      description={t('jobs.emptyDescription')}
                    />
                  ) : (
                    <ul className="divide-y divide-ink-100 dark:divide-white/10">
                      {jobs.slice(0, 6).map((job) => (
                        <li key={job.id}>
                          <Link
                            to={`/hm/jobs/${job.id}`}
                            className="-mx-2 flex items-center justify-between gap-4 rounded-lg px-2 py-3 transition-colors hover:bg-ink-50 dark:hover:bg-white/5"
                          >
                            <div className="min-w-0">
                              <p className="truncate font-medium text-ink-900 dark:text-white">
                                {job.title}
                              </p>
                              <p className="truncate text-sm text-ink-500 dark:text-ink-400">
                                {job.department || t('jobs.noDepartment')}
                              </p>
                            </div>
                            <div className="flex shrink-0 items-center gap-3">
                              {job.hmSignOffStatus === 'pending' && (
                                <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-700 dark:bg-amber-500/20 dark:text-amber-400">
                                  {t('jobs.needsSignOff')}
                                </span>
                              )}
                              <span className="text-sm text-ink-500 dark:text-ink-400">
                                {t('jobs.applicantCount', { count: job.applicantCount ?? 0 })}
                              </span>
                            </div>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>
              </div>

              <div className="space-y-6">
                <section className={CARD}>
                  <h3 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">
                    {t('quickActions.title')}
                  </h3>
                  <div className="space-y-2">
                    {[
                      {
                        to: '/hm/recruitment-requests',
                        icon: ClipboardList,
                        label: t('quickActions.newRequest'),
                        tint: 'text-brand-600 dark:text-brand-400 bg-brand-100 dark:bg-brand-500/20',
                      },
                      {
                        to: '/hm/interview-rooms',
                        icon: DoorOpen,
                        label: t('quickActions.interviewRooms'),
                        tint: 'text-amber-600 dark:text-amber-400 bg-amber-100 dark:bg-amber-500/20',
                      },
                      {
                        to: '/hm/evaluations',
                        icon: ClipboardCheck,
                        label: t('quickActions.evaluations'),
                        tint: 'text-ai-600 dark:text-ai-400 bg-ai-100 dark:bg-ai-500/20',
                      },
                      {
                        to: '/hm/offers',
                        icon: FileSignature,
                        label: t('quickActions.offers'),
                        tint: 'text-emerald-600 dark:text-emerald-400 bg-emerald-100 dark:bg-emerald-500/20',
                      },
                    ].map((a) => (
                      <Link
                        key={a.to}
                        to={a.to}
                        className="flex items-center gap-3 rounded-xl border border-ink-100 p-3 hover:border-brand-300 hover:bg-ink-50 dark:border-white/10 dark:hover:border-brand-500/40 dark:hover:bg-white/5"
                      >
                        <span className={`grid h-8 w-8 place-items-center rounded-lg ${a.tint}`}>
                          <a.icon className="h-4 w-4" />
                        </span>
                        <span className="flex-1 text-sm text-ink-700 dark:text-ink-200">
                          {a.label}
                        </span>
                        <ArrowRight className="h-4 w-4 text-ink-400" />
                      </Link>
                    ))}
                  </div>
                </section>

                {jobsNeedingSignOff.length > 0 && (
                  <section className={CARD}>
                    <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
                      <FileSignature className="h-4 w-4 text-amber-500" />
                      {t('signOff.title')}
                    </h3>
                    <p className="mb-3 text-xs text-ink-500 dark:text-ink-400">{t('signOff.hint')}</p>
                    <div className="space-y-2">
                      {jobsNeedingSignOff.slice(0, 4).map((j) => (
                        <Link
                          key={j.id}
                          to={`/hm/jobs/${j.id}`}
                          className="flex items-center justify-between gap-2 rounded-xl border border-ink-100 p-3 hover:bg-ink-50 dark:border-white/10 dark:hover:bg-white/5"
                        >
                          <span className="min-w-0 flex-1 truncate text-sm text-ink-700 dark:text-ink-200">
                            {j.title}
                          </span>
                          <ArrowRight className="h-4 w-4 shrink-0 text-ink-400" />
                        </Link>
                      ))}
                    </div>
                  </section>
                )}

                {awaitingOfferApproval.length > 0 && (
                  <section className={CARD}>
                    <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
                      <ClipboardCheck className="h-4 w-4 text-indigo-500" />
                      {t('offers.title')}
                    </h3>
                    <Link
                      to="/hm/offers"
                      className="flex items-center justify-between gap-2 rounded-xl border border-ink-100 p-3 text-sm hover:bg-ink-50 dark:border-white/10 dark:hover:bg-white/5"
                    >
                      <span className="text-ink-700 dark:text-ink-200">
                        {t('offers.count', { count: awaitingOfferApproval.length })}
                      </span>
                      <ArrowRight className="h-4 w-4 text-ink-400" />
                    </Link>
                  </section>
                )}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
