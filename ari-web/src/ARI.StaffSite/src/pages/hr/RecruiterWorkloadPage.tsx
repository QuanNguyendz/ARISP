import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  Users,
  Briefcase,
  ArrowRightLeft,
  AlertTriangle,
  Clock,
  Loader2,
  X,
  CheckCircle2,
  Lock,
} from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert, Select } from '@ari/shared/ui'
import { HrStatsSkeleton } from './_skeletons'
import {
  profileService,
  type RecruiterOverview,
  type RecruiterJobBrief,
} from '@/fservices/profile/profileService'

/** Ngưỡng SLA (ngày) để tô màu tuổi chờ — quá hạn thì đỏ, gần hạn thì hổ phách. */
const SLA_WARN_DAYS = 3
const SLA_BREACH_DAYS = 7

function ageClass(days: number) {
  if (days >= SLA_BREACH_DAYS) return 'text-red-600 dark:text-red-400'
  if (days >= SLA_WARN_DAYS) return 'text-amber-600 dark:text-amber-400'
  return 'text-ink-500 dark:text-ink-400'
}

/** Một nút thắt: số việc đang tồn + tuổi chờ của mục cũ nhất. */
function Bottleneck({
  label,
  count,
  oldestDays,
  href,
  hint,
}: {
  label: string
  count: number
  oldestDays?: number
  href?: string
  hint: string
}) {
  const idle = count === 0
  const body = (
    <div
      title={hint}
      className={`rounded-xl border p-3 transition-colors ${
        idle
          ? 'border-ink-200 dark:border-white/10 bg-ink-50/60 dark:bg-white/5'
          : 'border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 hover:border-brand-300 dark:hover:border-brand-500/40'
      }`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-xs font-medium text-ink-500 dark:text-ink-400">{label}</span>
        <span
          className={`font-display text-lg font-extrabold ${idle ? 'text-ink-300 dark:text-ink-600' : 'text-ink-900 dark:text-white'}`}
        >
          {count}
        </span>
      </div>
      {!idle && oldestDays !== undefined && oldestDays > 0 && (
        <div className={`mt-1 flex items-center gap-1 text-xs ${ageClass(oldestDays)}`}>
          <Clock className="h-3 w-3" />
          {oldestDays}
        </div>
      )}
    </div>
  )
  return href && !idle ? (
    <Link to={href} className="block">
      {body}
    </Link>
  ) : (
    body
  )
}

export default function RecruiterWorkloadPage() {
  const { t } = useTranslation('modules/hr/recruiters')
  const queryClient = useQueryClient()

  const [handover, setHandover] = useState<RecruiterOverview | null>(null)
  const [selectedJobId, setSelectedJobId] = useState('')
  const [targetId, setTargetId] = useState('')
  const [reason, setReason] = useState('')
  const [formError, setFormError] = useState('')
  const [doneMessage, setDoneMessage] = useState('')

  const {
    data: recruiters = [],
    isLoading,
    error,
  } = useQuery({
    queryKey: ['staff-recruiters'],
    queryFn: () => profileService.getRecruiters(),
    refetchOnWindowFocus: false,
  })

  const { data: handoverJobs = [], isLoading: loadingJobs } = useQuery({
    queryKey: ['recruiter-jobs', handover?.id],
    queryFn: () => profileService.getRecruiterJobs(handover!.id),
    enabled: Boolean(handover),
  })

  const reassign = useMutation({
    mutationFn: (payload: { jobId: string; toRecruiterId: string; reason?: string }) =>
      profileService.reassignJob(payload.jobId, {
        toRecruiterId: payload.toRecruiterId,
        reason: payload.reason,
      }),
    onSuccess: (res) => {
      setDoneMessage(res.message)
      closeHandover()
      queryClient.invalidateQueries({ queryKey: ['staff-recruiters'] })
    },
    onError: (err: unknown) => {
      setFormError(err instanceof Error ? err.message : t('handover.error'))
    },
  })

  const stats = useMemo(() => {
    const totalActiveJobs = recruiters.reduce((sum, r) => sum + r.jobsActive, 0)
    const totalPipeline = recruiters.reduce((sum, r) => sum + r.activePipeline, 0)
    const breaching = recruiters.filter((r) => r.oldestBottleneckDays >= SLA_BREACH_DAYS).length
    return [
      {
        label: t('stats.recruiters'),
        value: recruiters.length,
        color: 'text-blue-600 dark:text-blue-400',
      },
      {
        label: t('stats.activeJobs'),
        value: totalActiveJobs,
        color: 'text-violet-600 dark:text-violet-400',
      },
      {
        label: t('stats.pipeline'),
        value: totalPipeline,
        color: 'text-emerald-600 dark:text-emerald-400',
      },
      {
        label: t('stats.breaching'),
        value: breaching,
        color: 'text-red-600 dark:text-red-400',
      },
    ]
  }, [recruiters, t])

  const openHandover = (recruiter: RecruiterOverview) => {
    setHandover(recruiter)
    setSelectedJobId('')
    setTargetId('')
    setReason('')
    setFormError('')
    setDoneMessage('')
  }

  const closeHandover = () => {
    setHandover(null)
    setSelectedJobId('')
    setTargetId('')
    setReason('')
    setFormError('')
  }

  const submitHandover = () => {
    setFormError('')
    if (!selectedJobId) return setFormError(t('handover.pickJob'))
    if (!targetId) return setFormError(t('handover.pickTarget'))
    reassign.mutate({ jobId: selectedJobId, toRecruiterId: targetId, reason: reason.trim() || undefined })
  }

  // Không cho chuyển về chính người đang giao, và không cho chuyển vào tài khoản đang khoá —
  // tin sẽ rơi vào trạng thái không ai xử lý được.
  const targetOptions = useMemo(
    () =>
      recruiters
        .filter((r) => r.id !== handover?.id && r.isActive)
        .map((r) => ({
          value: r.id,
          label: `${r.fullName} · ${t('handover.targetLoad', { jobs: r.jobsActive, pipeline: r.activePipeline })}`,
        })),
    [recruiters, handover, t]
  )

  const jobOptions = useMemo(
    () =>
      handoverJobs.map((job: RecruiterJobBrief) => ({
        value: job.id,
        label: `${job.title} · ${t('handover.jobCandidates', { count: job.candidates })}`,
      })),
    [handoverJobs, t]
  )

  return (
    <div className="min-h-screen bg-ink-50 dark:bg-ink-950 p-4 sm:p-6 lg:p-8">
      <PageHeader title={t('title')} description={t('subtitle')} />

      {doneMessage && (
        <motion.div
          initial={{ opacity: 0, y: -8 }}
          animate={{ opacity: 1, y: 0 }}
          className="mb-6 flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-400"
        >
          <CheckCircle2 className="h-4 w-4 shrink-0" />
          {doneMessage}
        </motion.div>
      )}

      {isLoading ? <HrStatsSkeleton /> : <StatsGrid stats={stats} />}

      {!isLoading && error && <ErrorAlert message={t('loadError')} />}

      {!isLoading && !error && recruiters.length === 0 && (
        <EmptyState
          icon={<Users className="h-8 w-8 text-ink-400" />}
          title={t('empty.title')}
          description={t('empty.description')}
          action={{ label: t('empty.action'), href: '/hr/team' }}
        />
      )}

      {!isLoading && !error && recruiters.length > 0 && (
        <div className="space-y-4">
          {recruiters.map((r, i) => (
            <motion.div
              key={r.id}
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.2 + Math.min(i * 0.04, 0.3) }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-5 shadow-card"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex min-w-0 items-center gap-3">
                  <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-gradient-to-br from-brand-500 to-ai-600 text-sm font-extrabold text-white">
                    {r.fullName
                      .split(' ')
                      .filter(Boolean)
                      .slice(0, 2)
                      .map((p) => p[0]?.toUpperCase())
                      .join('')}
                  </span>
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="font-display text-base font-bold text-ink-900 dark:text-white">
                        {r.fullName}
                      </h3>
                      {!r.isActive && (
                        <span className="inline-flex items-center gap-1 rounded-full bg-ink-100 dark:bg-white/10 px-2 py-0.5 text-xs font-medium text-ink-600 dark:text-ink-400">
                          <Lock className="h-3 w-3" />
                          {t('locked')}
                        </span>
                      )}
                      {r.oldestBottleneckDays >= SLA_BREACH_DAYS && (
                        <span className="inline-flex items-center gap-1 rounded-full bg-red-100 dark:bg-red-500/20 px-2 py-0.5 text-xs font-semibold text-red-700 dark:text-red-400">
                          <AlertTriangle className="h-3 w-3" />
                          {t('slaBreach', { days: r.oldestBottleneckDays })}
                        </span>
                      )}
                    </div>
                    <p className="truncate text-xs text-ink-500 dark:text-ink-400">
                      {r.email}
                      {r.department ? ` · ${r.department}` : ''}
                    </p>
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1.5 rounded-full bg-brand-50 dark:bg-brand-500/20 px-3 py-1 text-xs font-semibold text-brand-700 dark:text-brand-400">
                    <Briefcase className="h-3.5 w-3.5" />
                    {t('reqLoad', { active: r.jobsActive, total: r.jobsTotal })}
                  </span>
                  <span className="inline-flex items-center gap-1.5 rounded-full bg-ink-100 dark:bg-white/10 px-3 py-1 text-xs font-medium text-ink-600 dark:text-ink-300">
                    {t('pipelineCount', { count: r.activePipeline })}
                  </span>
                  <button
                    type="button"
                    onClick={() => openHandover(r)}
                    disabled={r.jobsTotal === 0}
                    className="inline-flex items-center gap-1.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-1.5 text-xs font-semibold text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 disabled:opacity-40"
                  >
                    <ArrowRightLeft className="h-3.5 w-3.5" />
                    {t('handover.open')}
                  </button>
                </div>
              </div>

              <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
                <Bottleneck
                  label={t('bottleneck.drafts')}
                  count={r.draftsAwaitingApproval}
                  oldestDays={r.draftsOldestDays}
                  href="/hr/jobs?status=draft"
                  hint={t('bottleneck.draftsHint')}
                />
                <Bottleneck
                  label={t('bottleneck.unscreened')}
                  count={r.applicationsUnscreened}
                  oldestDays={r.unscreenedOldestDays}
                  href="/hr/candidates"
                  hint={t('bottleneck.unscreenedHint')}
                />
                <Bottleneck
                  label={t('bottleneck.scheduling')}
                  count={r.awaitingScheduling}
                  oldestDays={r.awaitingSchedulingOldestDays}
                  href="/hr/candidates"
                  hint={t('bottleneck.schedulingHint')}
                />
                <Bottleneck
                  label={t('bottleneck.declined')}
                  count={r.declinedNeedRebooking}
                  href="/hr/candidates"
                  hint={t('bottleneck.declinedHint')}
                />
                <Bottleneck
                  label={t('bottleneck.reviews')}
                  count={r.pendingReviews}
                  oldestDays={r.pendingReviewsOldestDays}
                  href="/hr/evaluations?status=pending"
                  hint={t('bottleneck.reviewsHint')}
                />
              </div>
            </motion.div>
          ))}
        </div>
      )}

      {/* Hộp thoại chuyển giao — thao tác cân tải thật sự của HR Lead */}
      {handover && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4">
          <motion.div
            initial={{ opacity: 0, scale: 0.96 }}
            animate={{ opacity: 1, scale: 1 }}
            className="w-full max-w-lg rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-5 shadow-xl"
          >
            <div className="mb-4 flex items-start justify-between gap-3">
              <div>
                <h3 className="font-display text-lg font-bold text-ink-900 dark:text-white">
                  {t('handover.title')}
                </h3>
                <p className="mt-0.5 text-sm text-ink-500 dark:text-ink-400">
                  {t('handover.subtitle', { name: handover.fullName })}
                </p>
              </div>
              <button
                onClick={closeHandover}
                className="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-ink-500 hover:bg-ink-100 dark:hover:bg-white/10"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="space-y-4">
              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                  {t('handover.jobLabel')}
                </label>
                {loadingJobs ? (
                  <div className="flex items-center gap-2 text-sm text-ink-500">
                    <Loader2 className="h-4 w-4 animate-spin" /> {t('handover.loadingJobs')}
                  </div>
                ) : (
                  <Select
                    value={selectedJobId}
                    onChange={setSelectedJobId}
                    options={jobOptions}
                    placeholder={t('handover.jobPlaceholder')}
                  />
                )}
              </div>

              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                  {t('handover.targetLabel')}
                </label>
                <Select
                  value={targetId}
                  onChange={setTargetId}
                  options={targetOptions}
                  placeholder={t('handover.targetPlaceholder')}
                />
                {targetOptions.length === 0 && (
                  <p className="mt-1.5 text-xs text-amber-600 dark:text-amber-400">
                    {t('handover.noTarget')}
                  </p>
                )}
              </div>

              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                  {t('handover.reasonLabel')}
                </label>
                <textarea
                  rows={2}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder={t('handover.reasonPlaceholder')}
                  className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white outline-none focus:border-brand-500"
                />
                <p className="mt-1.5 text-xs text-ink-400">{t('handover.reasonHint')}</p>
              </div>

              {formError && (
                <div className="rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-400">
                  {formError}
                </div>
              )}

              <div className="flex justify-end gap-2">
                <button
                  onClick={closeHandover}
                  className="rounded-xl border border-ink-200 dark:border-white/10 px-4 py-2 text-sm font-semibold text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
                >
                  {t('handover.cancel')}
                </button>
                <button
                  onClick={submitHandover}
                  disabled={reassign.isPending}
                  className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
                >
                  {reassign.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                  {t('handover.submit')}
                </button>
              </div>
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
