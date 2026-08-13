import { useEffect, useMemo, useState, useCallback } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  ArrowLeft,
  Pencil,
  Send,
  XCircle,
  FileText,
  Users,
  MapPin,
  Briefcase,
  Mail,
  Loader2,
  CheckCircle2,
  AlertCircle,
  Layers,
  Target,
  CalendarClock,
  Check,
  X,
  ScrollText,
  Sparkles,
} from 'lucide-react'
import { ErrorAlert, Pagination, Select } from '@ari/shared/ui'
import { useAuthStore } from '@ari/shared/store/auth'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import jobService from '@ari/shared/fservices/job'
import { applicationService } from '@ari/shared/fservices/application'
import InviteAndScheduleModal from '../../components/InviteAndScheduleModal'
import type { JobPosting } from '@ari/shared/types/job'
import type { HrApplicationItem } from '@ari/shared/types/application'
import {
  jobStatusBadge,
  jobStatusLabel,
  appStatusBadge,
  appStatusLabel,
  formatSalary,
  initials,
  timeAgo,
} from './_jobUi'
import { JobDetailSkeleton } from './_skeletons'
import { resolveAssetUrl } from '@ari/shared/config/constants'

function getDeadlineText(
  deadlineStr: string | null | undefined,
  t: (key: string, options?: Record<string, unknown>) => string
): string {
  if (!deadlineStr) return ''
  const d = new Date(deadlineStr)
  if (Number.isNaN(d.getTime())) return ''
  const formattedDate = d.toLocaleDateString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const target = new Date(d)
  target.setHours(0, 0, 0, 0)
  const diffDays = Math.round((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))
  if (diffDays < 0) return `${formattedDate} (${t('deadlineExpired')})`
  if (diffDays === 0) return `${formattedDate} (${t('deadlineToday')})`
  return `${formattedDate} (${t('deadlineDays', { days: diffDays })})`
}

function toLocalDateStr(d: string | Date | number | undefined | null): string {
  if (!d) return ''
  const dt = new Date(d)
  if (isNaN(dt.getTime())) return ''
  const yyyy = dt.getFullYear()
  const mm = String(dt.getMonth() + 1).padStart(2, '0')
  const dd = String(dt.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

const matchColor = (s?: number | null) =>
  s == null
    ? 'text-ink-400'
    : s >= 75
      ? 'text-emerald-600 dark:text-emerald-400'
      : s >= 50
        ? 'text-amber-600 dark:text-amber-400'
        : 'text-red-600 dark:text-red-400'

export default function RecruiterJobDetailPage() {
  const { t } = useTranslation('modules/recruiter/jobDetail')
  const { id } = useParams<{ id: string }>()
  const { openDocument } = useDocumentViewer()
  const queryClient = useQueryClient()
  const user = useAuthStore((state) => state.user)

  const {
    data: job,
    isLoading: loadingJob,
    error: jobError,
    refetch: refetchJob,
  } = useQuery({
    queryKey: ['job', id],
    queryFn: () => jobService.getJobPostingById(id!),
    enabled: !!id,
    retry: false,
  })

  const {
    data: appsData,
    isLoading: loadingApps,
    refetch: refetchApps,
  } = useQuery({
    queryKey: ['job', id, 'applications'],
    queryFn: () => jobService.getJobApplications(id!).catch(() => [] as HrApplicationItem[]),
    enabled: !!id,
  })

  const apps = appsData || []
  const loading = loadingJob || loadingApps

  const [mutationError, setMutationError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [processingAppId, setProcessingAppId] = useState<string | null>(null)
  const [page, setPage] = useState(1)

  const error =
    mutationError ||
    (jobError as any)?.response?.data?.message ||
    (jobError ? t('loadingError') : '')
  const load = refetchApps

  const funnel = useMemo(() => {
    const by = (s: string) => apps.filter((a) => a.status === s).length
    return [
      { key: 'cv_submitted', label: t('funnel.cvSubmitted'), count: by('cv_submitted') },
      { key: 'screening', label: t('funnel.screening'), count: by('screening') },
      { key: 'interview', label: t('funnel.interview'), count: by('interview') },
      { key: 'pass', label: t('funnel.pass'), count: by('pass') },
    ]
  }, [apps, t])

  const hired = useMemo(() => apps.filter((a) => a.status === 'pass').length, [apps])
  const isFull = job?.vacancies != null && job.vacancies > 0 && hired >= job.vacancies

  const [activeTab, setActiveTab] = useState<string>('cv_review')
  const [selectedCoverLetter, setSelectedCoverLetter] = useState<{
    candidateName: string
    text: string
    email: string
    phone?: string
    noticePeriod?: string
    cvJdSummary?: string
    matchScore?: number | null
    cvFileUrl?: string | null
  } | null>(null)

  const [cvDateFilter, setCvDateFilter] = useState<string>('')
  const [cvStatusFilter, setCvStatusFilter] = useState<string>('all')
  const [cvSortOrder, setCvSortOrder] = useState<string>('desc')
  const [interviewDateFilter, setInterviewDateFilter] = useState<string>('')
  const [interviewStatusFilter, setInterviewStatusFilter] = useState<string>('all')
  const [interviewSortOrder, setInterviewSortOrder] = useState<string>('desc')
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [batchProcessing, setBatchProcessing] = useState<boolean>(false)
  const [inviteModalTarget, setInviteModalTarget] = useState<{ id: string; name: string; targetRound: number } | null>(null)

  const activeRoundNumber = useMemo(
    () => (activeTab.startsWith('round_') ? parseInt(activeTab.replace('round_', ''), 10) : 0),
    [activeTab]
  )

  const tabs = useMemo(() => {
    if (!job) return []
    const cvReviewCount = apps.filter((a) => !a.currentRound || a.currentRound === 0).length
    const roundTabs = (job.roundConfigs || [])
      .slice()
      .sort((a, b) => a.roundNumber - b.roundNumber)
      .map((rc) => {
        const count = apps.filter((a) => a.currentRound === rc.roundNumber).length
        const roundTypeStr = rc.roundType
          ? rc.roundType.toLowerCase() === 'screening'
            ? t('roundScreening')
            : rc.roundType.toLowerCase() === 'technical'
              ? t('roundTechnical')
              : rc.roundType.toLowerCase() === 'online_test'
                ? t('roundOnlineTest')
                : rc.roundType
          : ''
        const typeText = roundTypeStr ? ` (${roundTypeStr})` : ''
        return {
          id: `round_${rc.roundNumber}`,
          label: `${t('tabs.round')} ${rc.roundNumber}${typeText}`,
          count,
        }
      })
    return [{ id: 'cv_review', label: t('tabs.cvReview'), count: cvReviewCount }, ...roundTabs]
  }, [job, apps, t])

  const activeCandidates = useMemo(() => {
    if (activeTab === 'cv_review')
      return apps.filter((a) => !a.currentRound || a.currentRound === 0)
    if (activeTab.startsWith('round_')) {
      const roundNumber = parseInt(activeTab.replace('round_', ''), 10)
      return apps.filter((a) => a.currentRound === roundNumber)
    }
    return apps
  }, [apps, activeTab])

  const processedApps = useMemo(() => {
    let result = [...activeCandidates]
    if (activeTab === 'cv_review') {
      if (cvDateFilter) result = result.filter((a) => toLocalDateStr(a.createdAt) === cvDateFilter)
      if (cvStatusFilter && cvStatusFilter !== 'all')
        result = result.filter((a) => a.status === cvStatusFilter)
      if (cvSortOrder === 'desc') result.sort((a, b) => (b.matchScore ?? 0) - (a.matchScore ?? 0))
      else if (cvSortOrder === 'asc')
        result.sort((a, b) => (a.matchScore ?? 0) - (b.matchScore ?? 0))
    } else {
      if (interviewDateFilter)
        result = result.filter((a) => toLocalDateStr(a.interviewDate) === interviewDateFilter)
      if (interviewStatusFilter && interviewStatusFilter !== 'all')
        result = result.filter((a) => a.status === interviewStatusFilter)
      if (interviewSortOrder === 'desc')
        result.sort((a, b) => (b.interviewScore ?? 0) - (a.interviewScore ?? 0))
      else if (interviewSortOrder === 'asc')
        result.sort((a, b) => (a.interviewScore ?? 0) - (b.interviewScore ?? 0))
    }
    return result
  }, [
    activeCandidates,
    activeTab,
    cvDateFilter,
    cvStatusFilter,
    cvSortOrder,
    interviewDateFilter,
    interviewStatusFilter,
    interviewSortOrder,
  ])

  const isSelectable = useCallback(
    (a: HrApplicationItem) => {
      if (activeTab === 'cv_review') return a.status === 'cv_submitted' || a.status === 'invited'
      return !(
        a.status === 'rejected' ||
        a.status === 'failed' ||
        a.status === 'not_pass' ||
        a.status === 'cv_rejected' ||
        a.status === 'pass' ||
        (a.currentRound != null && activeRoundNumber > 0 && a.currentRound > activeRoundNumber)
      )
    },
    [activeTab, activeRoundNumber]
  )

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(processedApps.length / PAGE_SIZE))
  const pagedApps = useMemo(
    () => processedApps.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [processedApps, page]
  )
  const pageSelectableCandidates = useMemo(
    () => pagedApps.filter(isSelectable),
    [pagedApps, isSelectable]
  )
  const isAllSelected = useMemo(
    () =>
      pageSelectableCandidates.length > 0 &&
      pageSelectableCandidates.every((c) => selectedIds.includes(c.id)),
    [pageSelectableCandidates, selectedIds]
  )

  const handleSelectAll = useCallback(() => {
    if (isAllSelected)
      setSelectedIds((prev) =>
        prev.filter((id) => !pageSelectableCandidates.some((c) => c.id === id))
      )
    else
      setSelectedIds((prev) => [
        ...prev,
        ...pageSelectableCandidates.map((c) => c.id).filter((id) => !prev.includes(id)),
      ])
  }, [isAllSelected, pageSelectableCandidates])

  useEffect(() => {
    setPage(1)
  }, [activeTab])
  useEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])
  useEffect(() => {
    setSelectedIds([])
  }, [
    activeTab,
    cvDateFilter,
    cvStatusFilter,
    cvSortOrder,
    interviewDateFilter,
    interviewStatusFilter,
    interviewSortOrder,
  ])

  const handleBatchAccept = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(t('confirm.approve', { count: selectedIds.length }))) return
    setBatchProcessing(true)
    setMutationError('')
    setNotice('')
    try {
      let successCount = 0,
        failCount = 0
      const results = await Promise.allSettled(
        selectedIds.map((id) => applicationService.acceptApplication(id))
      )
      results.forEach((res) => {
        if (res.status === 'fulfilled') successCount++
        else failCount++
      })
      setNotice(
        t('messages.approvedSuccess', { count: successCount }) +
          (failCount > 0 ? ` ${t('messages.approvedFailed', { count: failCount })}` : '')
      )
      setSelectedIds([])
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || t('messages.approveError'))
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleBatchReject = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(t('confirm.reject', { count: selectedIds.length }))) return
    setBatchProcessing(true)
    setMutationError('')
    setNotice('')
    try {
      let successCount = 0,
        failCount = 0
      const results = await Promise.allSettled(
        selectedIds.map((id) => applicationService.rejectApplication(id))
      )
      results.forEach((res) => {
        if (res.status === 'fulfilled') successCount++
        else failCount++
      })
      setNotice(
        t('messages.rejectedSuccess', { count: successCount }) +
          (failCount > 0 ? ` ${t('messages.rejectedFailed', { count: failCount })}` : '')
      )
      setSelectedIds([])
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || t('messages.rejectError'))
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleBatchInvite = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(t('confirm.invite', { count: selectedIds.length }))) return
    setBatchProcessing(true)
    setMutationError('')
    setNotice('')
    try {
      let successCount = 0,
        failCount = 0
      const results = await Promise.allSettled(
        selectedIds.map((id) => applicationService.sendInvite(id))
      )
      results.forEach((res) => {
        if (res.status === 'fulfilled') successCount++
        else failCount++
      })
      setNotice(
        t('messages.inviteSentSuccess', { count: successCount }) +
          (failCount > 0 ? ` ${t('messages.inviteSentFailed', { count: failCount })}` : '')
      )
      setSelectedIds([])
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || t('messages.inviteError'))
    } finally {
      setBatchProcessing(false)
    }
  }

  const changeStatus = async (status: JobPosting['status']) => {
    if (!id) return
    setBusy(true)
    setMutationError('')
    setNotice('')
    try {
      await jobService.updateJobStatus(id, status)
      setNotice(
        status === 'pending'
          ? t('jobSubmittedForApproval')
          : status === 'closed'
            ? t('jobClosed')
            : t('statusUpdated')
      )
      await load()
      await refetchJob()
    } catch (e: any) {
      setMutationError(e?.response?.data?.message || t('statusError'))
    } finally {
      setBusy(false)
    }
  }

  const handleAccept = async (appId: string) => {
    setProcessingAppId(appId)
    setMutationError('')
    setNotice('')
    try {
      await applicationService.acceptApplication(appId)
      setNotice(t('messages.singleApproved'))
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || t('messages.approveError'))
    } finally {
      setProcessingAppId(null)
    }
  }

  const handleReject = async (appId: string) => {
    if (!window.confirm('Bạn có chắc chắn muốn từ chối hồ sơ ứng viên này và gửi thư cảm ơn?'))
      return
    setProcessingAppId(appId)
    setMutationError('')
    setNotice('')
    try {
      await applicationService.rejectApplication(appId)
      setNotice(t('messages.singleRejected'))
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || t('messages.rejectError'))
    } finally {
      setProcessingAppId(null)
    }
  }

  const handleToggleDisplay = async (field: 'isUrgent' | 'isPublicListing', value: boolean) => {
    if (!id || !job) return
    const originalValue = job[field]
    if (originalValue === value) return

    queryClient.setQueryData(['job', id], (old: any) => (old ? { ...old, [field]: value } : old))
    try {
      await jobService.updateJobDisplay(id, { [field]: value })
    } catch (err: unknown) {
      queryClient.setQueryData(['job', id], (old: any) => (old ? { ...old, [field]: originalValue } : old))
      setMutationError(t(`messages.toggleFailed`))
    }
  }

  if (loading) return <JobDetailSkeleton />
  if (!job || (user?.role === 'recruiter' && job.createdByUserId && job.createdByUserId !== user.id)) {
    return (
      <div className="p-4 sm:p-6 lg:p-8">
        <ErrorAlert message="Bạn không có quyền truy cập tin tuyển dụng này hoặc tin không tồn tại." />
        <Link
          to="/recruiter/my-jobs"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← {t('backToList')}
        </Link>
      </div>
    )
  }

  const canSubmit = job.status === 'draft' || job.status === 'rejected'
  const canClose = job.status === 'active'
  // Chỉ cho Recruiter sửa tin khi còn nháp hoặc bị HR từ chối — đã gửi duyệt (pending) / đã duyệt (active...) thì khoá.
  const canEdit = job.status === 'draft' || job.status === 'rejected'

  return (
    <div className="p-6 lg:p-8">
      <Link
        to="/recruiter/my-jobs"
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('backToList')}
      </Link>

      {error && <ErrorAlert message={error} onDismiss={() => setMutationError('')} />}
      {notice && (
        <div className="mb-6 flex items-start justify-between gap-3 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
          <span className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4" /> {notice}
          </span>
          <button onClick={() => setNotice('')} className="hover:opacity-70">
            {t('coverLetterModal.close')}
          </button>
        </div>
      )}

      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        className="mb-6 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
      >
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="flex items-start gap-4">
            <span className="grid h-12 w-12 shrink-0 place-items-center rounded-xl bg-brand-50 dark:bg-brand-500/15 text-brand-600 dark:text-brand-400">
              <Briefcase className="h-6 w-6" />
            </span>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="text-xl font-bold text-ink-900 dark:text-white">{job.title}</h1>
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${jobStatusBadge(job.status, job.applicationDeadline)}`}
                >
                  {jobStatusLabel(job.status, job.applicationDeadline)}
                </span>
              </div>
              <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-2">
                <span className="text-sm font-medium text-ink-700 dark:text-ink-300">
                  {job.department || t('noDepartment')}
                </span>
                <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-ink-700 dark:text-ink-300 bg-white dark:bg-white/5 px-2.5 py-1 rounded-lg border border-ink-200 dark:border-white/10 shadow-sm transition-colors hover:bg-ink-50 dark:hover:bg-white/10">
                  <input type="checkbox" checked={job.isUrgent} onChange={(e) => handleToggleDisplay('isUrgent', e.target.checked)} className="accent-brand-600" />
                  Tuyển gấp
                </label>
              </div>
              <p className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
                {job.location && (
                  <span className="flex items-center gap-1">
                    <MapPin className="h-3.5 w-3.5" />
                    {job.location}
                  </span>
                )}
                <span>{formatSalary(job)}</span>
                {job.vacancies != null && job.vacancies > 0 && (
                  <span
                    className={`flex items-center gap-1 font-medium ${isFull ? 'text-emerald-600 dark:text-emerald-400' : 'text-ink-600 dark:text-ink-300'}`}
                  >
                    <Target className="h-3.5 w-3.5" />{' '}
                    {t('hiringProgress', { hired, total: job.vacancies })}
                  </span>
                )}
                {job.applicationDeadline && (
                  <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400 font-medium">
                    <CalendarClock className="h-3.5 w-3.5" />
                    {t('deadline')}: {getDeadlineText(job.applicationDeadline, t)}
                  </span>
                )}
                <span className="text-ink-400">
                  · {t('created')} {timeAgo(job.createdAt)}
                </span>
              </p>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {job.jdFileUrl && (
              <button
                type="button"
                onClick={() =>
                  openDocument(
                    job.signedJdFileUrl || job.jdFileUrl!,
                    job.jdFileName || `${job.title} - JD`,
                  )
                }
                className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
              >
                <FileText className="h-4 w-4" /> {t('jdFile')}
              </button>
            )}
            <Link
              to={`/recruiter/my-jobs/${job.id}/schedule`}
              className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
            >
              <CalendarClock className="h-4 w-4" /> {t('interviewSchedule')}
            </Link>
            {job.roundConfigs?.some((r) => (r.roundType || '').toLowerCase() === 'online_test') && (
              <Link
                to={`/recruiter/my-jobs/${job.id}/online-test`}
                className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
              >
                <ScrollText className="h-4 w-4" /> {t('onlineTestBank')}
              </Link>
            )}
            {canEdit && (
              <Link
                to={`/recruiter/my-jobs/${job.id}/edit`}
                className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
              >
                <Pencil className="h-4 w-4" /> {t('editJob')}
              </Link>
            )}
            {canSubmit && (
              <button
                onClick={() => changeStatus('pending')}
                disabled={busy}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3.5 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}{' '}
                {t('submitForApproval')}
              </button>
            )}
            {canClose && (
              <button
                onClick={() => changeStatus('closed')}
                disabled={busy}
                className="inline-flex items-center gap-2 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-3.5 py-2 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-500/20 disabled:opacity-50"
              >
                {busy ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <XCircle className="h-4 w-4" />
                )}{' '}
                {t('closeJob')}
              </button>
            )}
          </div>
        </div>

        {job.status === 'rejected' && job.rejectionReason && (
          <div className="mt-4 flex items-start gap-2 rounded-xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-400">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              <b>{t('hrRejected')}:</b> {t('hrRejectedHint', { reason: job.rejectionReason })}
            </span>
          </div>
        )}

        {isFull && canClose && (
          <div className="mt-4 flex items-center justify-between gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-400">
            <span className="flex items-center gap-2">
              <Target className="h-4 w-4" />{' '}
              {t('hiringGoalReached', { hired, total: job.vacancies })}
            </span>
            <button
              onClick={() => changeStatus('closed')}
              disabled={busy}
              className="rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              {t('closeJob')}
            </button>
          </div>
        )}
      </motion.div>

      {/* Candidate Funnel */}
        <div className="mb-6 grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-5">
        <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
          <span className="flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
            <Users className="h-4 w-4" /> {t('stats.totalCandidates')}
          </span>
          <div className="mt-2 text-2xl font-bold text-brand-600 dark:text-brand-400">
            {apps.length}
          </div>
        </div>
        {funnel.map((f) => (
          <div
            key={f.key}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
          >
            <span className="text-sm text-ink-500 dark:text-ink-400">{f.label}</span>
            <div className="mt-2 text-2xl font-bold text-ink-900 dark:text-white">{f.count}</div>
          </div>
        ))}
      </div>

      {/* Interview Rounds Config */}
      {job.roundConfigs?.length > 0 && (
        <div className="mb-6 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
          <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
            <Layers className="h-4 w-4 text-ai-600 dark:text-ai-400" /> {t('interviewRounds')}{' '}
            {t('interviewRoundsCount', { count: job.roundConfigs.length })}
          </h2>
          <div className="flex flex-wrap gap-2">
            {job.roundConfigs.map((r) => (
              <span
                key={r.roundNumber}
                className="rounded-lg border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 px-3 py-1.5 text-xs text-ink-700 dark:text-ink-200"
              >
                {t('tabs.round')} {r.roundNumber}:{' '}
                {r.roundType === 'technical'
                  ? t('roundTechnical')
                  : r.roundType === 'online_test'
                    ? t('roundOnlineTest')
                    : t('roundScreening')}{' '}
                · {r.maxDurationMinutes}′
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Candidates List */}
      <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
        <div className="flex items-center justify-between border-b border-ink-100 dark:border-white/10 px-5 py-4">
          <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
            <Users className="h-5 w-5 text-brand-600 dark:text-brand-400" /> {t('candidateFunnel')}{' '}
            ({apps.length})
          </h2>
        </div>

        {apps.length > 0 && (
          <div className="border-b border-ink-100 dark:border-white/10 px-5 py-4">
            <div className="flex flex-wrap gap-2">
              {tabs.map((tab) => (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all duration-200 ${activeTab === tab.id ? 'bg-brand-600 text-white shadow-sm' : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'}`}
                >
                  {tab.label} ({tab.count})
                </button>
              ))}
            </div>
          </div>
        )}

        {apps.length === 0 ? (
          <div className="flex flex-col items-center gap-2 py-12 text-center">
            <Users className="h-8 w-8 text-ink-300" />
            <p className="text-sm text-ink-500 dark:text-ink-400">{t('noCandidatesForJob')}</p>
          </div>
        ) : (
          <>
            {/* Filters */}
            <div className="mx-5 mb-6 flex flex-wrap items-end justify-between gap-4 p-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
              <div className="flex flex-wrap items-center gap-4">
                <div className="flex flex-col gap-1">
                  <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                    {activeTab === 'cv_review'
                      ? t('filters.filterByDateCv')
                      : t('filters.filterByDateInterview')}
                  </span>
                  <input
                    type="date"
                    value={activeTab === 'cv_review' ? cvDateFilter : interviewDateFilter}
                    onChange={(e) =>
                      activeTab === 'cv_review'
                        ? setCvDateFilter(e.target.value)
                        : setInterviewDateFilter(e.target.value)
                    }
                    className="px-3 py-1.5 text-xs rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 text-ink-900 dark:text-white focus:outline-none focus:border-brand-500"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                    {t('filters.filterByStatus')}
                  </span>
                  <Select
                    value={activeTab === 'cv_review' ? cvStatusFilter : interviewStatusFilter}
                    onChange={(v) =>
                      activeTab === 'cv_review' ? setCvStatusFilter(v) : setInterviewStatusFilter(v)
                    }
                    ariaLabel={t('filters.filterByStatus')}
                    className="min-w-[11rem]"
                    buttonClassName="rounded-lg px-3 py-1.5 text-xs"
                    options={[
                      { value: 'all', label: t('filters.allStatuses') },
                      ...(activeTab === 'cv_review'
                        ? [
                            { value: 'cv_submitted', label: t('filters.cvSubmitted') },
                            { value: 'invited', label: t('filters.invited') },
                            { value: 'cv_rejected', label: t('filters.cvRejected') },
                          ]
                        : [
                            { value: 'invited', label: t('filters.waitingForInvite') },
                            { value: 'screening', label: t('filters.invitedWaitingSchedule') },
                            { value: 'interview', label: t('filters.scheduledInterview') },
                            { value: 'pass', label: t('funnel.pass') },
                            { value: 'not_pass', label: t('funnel.screening') },
                            { value: 'withdrawn', label: t('filters.withdrawn') },
                          ]),
                    ]}
                  />
                </div>
              </div>
              <div className="flex flex-col gap-1">
                <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                  {activeTab === 'cv_review'
                    ? t('filters.sortByRelevance')
                    : t('filters.sortByScore')}
                </span>
                <Select
                  value={activeTab === 'cv_review' ? cvSortOrder : interviewSortOrder}
                  onChange={(v) =>
                    activeTab === 'cv_review' ? setCvSortOrder(v) : setInterviewSortOrder(v)
                  }
                  className="min-w-[10rem]"
                  buttonClassName="rounded-lg px-3 py-1.5 text-xs"
                  options={[
                    { value: 'desc', label: t('filters.sortDesc') },
                    { value: 'asc', label: t('filters.sortAsc') },
                    { value: 'default', label: t('filters.sortDefault') },
                  ]}
                />
              </div>
            </div>

            {/* Batch Actions */}
            {selectedIds.length > 0 && (
              <div className="mx-5 flex items-center justify-between p-4 mb-4 rounded-xl border border-brand-200 dark:border-brand-500/30 bg-brand-50/50 dark:bg-brand-500/10 backdrop-blur-sm animate-fade-in">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-semibold text-brand-900 dark:text-brand-400">
                    {t('batch.selected', { count: selectedIds.length })}
                  </span>
                </div>
                <div className="flex gap-2">
                  {activeTab === 'cv_review' ? (
                    <>
                      <button
                        disabled={batchProcessing}
                        onClick={handleBatchAccept}
                        className="flex items-center gap-1.5 px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold shadow-sm transition-all disabled:opacity-50"
                      >
                        {batchProcessing ? (
                          <Loader2 className="w-3.5 h-3.5 animate-spin" />
                        ) : (
                          <Check className="w-3.5 h-3.5" />
                        )}
                        {t('batch.approveAll')}
                      </button>
                      <button
                        disabled={batchProcessing}
                        onClick={handleBatchReject}
                        className="flex items-center gap-1.5 px-4 py-2 rounded-xl border border-red-200 dark:border-red-500/30 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 text-xs font-semibold transition-all disabled:opacity-50"
                      >
                        <X className="w-3.5 h-3.5" />
                        {t('batch.rejectAll')}
                      </button>
                    </>
                  ) : (
                    <button
                      disabled={batchProcessing}
                      onClick={handleBatchInvite}
                      className="flex items-center gap-1.5 px-4 py-2 rounded-xl bg-brand-600 hover:bg-brand-700 text-white text-xs font-semibold shadow-sm transition-all disabled:opacity-50"
                    >
                      {batchProcessing ? (
                        <Loader2 className="w-3.5 h-3.5 animate-spin" />
                      ) : (
                        <Send className="w-3.5 h-3.5" />
                      )}
                      {t('batch.inviteAll')}
                    </button>
                  )}
                </div>
              </div>
            )}

            {processedApps.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-12 text-center">
                <Users className="h-8 w-8 text-ink-300" />
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('noResults')}</p>
              </div>
            ) : (
              <>
                {/* Table Headers */}
                {activeTab === 'cv_review' ? (
                  <div className="grid grid-cols-[40px_minmax(180px,1.5fr)_110px_110px_100px_120px_230px] gap-4 px-5 py-3 border-b border-ink-200 dark:border-white/10 bg-ink-50/80 dark:bg-white/5 text-xs font-semibold text-ink-500 dark:text-ink-400 items-center">
                    <div className="flex items-center justify-center">
                      <input
                        type="checkbox"
                        checked={isAllSelected}
                        disabled={pageSelectableCandidates.length === 0}
                        onChange={handleSelectAll}
                        className="rounded border-ink-300 dark:border-white/10 text-brand-600 focus:ring-brand-500 w-4 h-4 cursor-pointer disabled:opacity-50"
                      />
                    </div>
                    <div>{t('tableHeaders.candidate')}</div>
                    <div className="text-center">{t('tableHeaders.submittedDate')}</div>
                    <div className="text-center">{t('tableHeaders.noticePeriod')}</div>
                    <div className="text-center">{t('tableHeaders.matchScore')}</div>
                    <div className="text-center">{t('tableHeaders.status')}</div>
                    <div className="text-center">{t('tableHeaders.actions')}</div>
                  </div>
                ) : (
                  <div className="grid grid-cols-[40px_minmax(180px,1.5fr)_180px_110px_120px_230px] gap-4 px-5 py-3 border-b border-ink-200 dark:border-white/10 bg-ink-50/80 dark:bg-white/5 text-xs font-semibold text-ink-500 dark:text-ink-400 items-center">
                    <div className="flex items-center justify-center">
                      <input
                        type="checkbox"
                        checked={isAllSelected}
                        disabled={pageSelectableCandidates.length === 0}
                        onChange={handleSelectAll}
                        className="rounded border-ink-300 dark:border-white/10 text-brand-600 focus:ring-brand-500 w-4 h-4 cursor-pointer disabled:opacity-50"
                      />
                    </div>
                    <div>{t('tableHeaders.candidate')}</div>
                    <div className="text-center">{t('tableHeaders.interviewSchedule')}</div>
                    <div className="text-center">{t('tableHeaders.evaluationScore')}</div>
                    <div className="text-center">{t('tableHeaders.status')}</div>
                    <div className="text-center">{t('tableHeaders.actions')}</div>
                  </div>
                )}

                <div className="divide-y divide-ink-100 dark:divide-white/10">
                  {pagedApps.map((a) => (
                    <div
                      key={a.id}
                      className={`grid items-center gap-4 px-5 py-3 hover:bg-ink-50/50 dark:hover:bg-white-[0.02] transition-colors ${activeTab === 'cv_review' ? 'grid-cols-[40px_minmax(180px,1.5fr)_110px_110px_100px_120px_230px]' : 'grid-cols-[40px_minmax(180px,1.5fr)_180px_110px_120px_230px]'}`}
                    >
                      <div className="flex items-center justify-center">
                        <input
                          type="checkbox"
                          checked={selectedIds.includes(a.id)}
                          disabled={!isSelectable(a) || batchProcessing}
                          onChange={(e) => {
                            if (e.target.checked) setSelectedIds((prev) => [...prev, a.id])
                            else setSelectedIds((prev) => prev.filter((id) => id !== a.id))
                          }}
                          className="rounded border-ink-300 dark:border-white/10 text-brand-600 focus:ring-brand-500 w-4 h-4 cursor-pointer disabled:opacity-30 disabled:cursor-not-allowed"
                        />
                      </div>
                      <Link
                        to={`/recruiter/candidates/${a.id}`}
                        className="flex min-w-0 items-center gap-3 group cursor-pointer"
                      >
                        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                          {initials(a.candidateName || a.candidateEmail)}
                        </span>
                        <div className="min-w-0">
                          <h4 className="truncate text-sm font-semibold text-ink-900 dark:text-white group-hover:text-brand-600 dark:group-hover:text-brand-400 transition-colors">
                            {a.candidateName || t('candidate')}
                          </h4>
                          <p className="flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400 mt-0.5">
                            <Mail className="h-3 w-3" /> {a.candidateEmail}
                          </p>
                        </div>
                      </Link>
                      {activeTab === 'cv_review' ? (
                        <>
                          <div className="text-center text-xs text-ink-600 dark:text-ink-300">
                            {new Date(a.createdAt).toLocaleDateString('vi-VN')}
                          </div>
                          <div className="text-center text-xs text-ink-600 dark:text-ink-300 truncate">
                            {a.noticePeriod || '—'}
                          </div>
                          <div className="text-center">
                            {a.matchScore != null ? (
                              <span className={`text-sm font-bold ${matchColor(a.matchScore)}`}>
                                {a.matchScore}%
                              </span>
                            ) : (
                              <span className="text-ink-400 text-sm font-medium">—</span>
                            )}
                          </div>
                        </>
                      ) : (
                        <>
                          <div className="text-center text-xs text-ink-600 dark:text-ink-300">
                            {a.interviewDate ? (
                              new Date(a.interviewDate).toLocaleString('vi-VN', {
                                dateStyle: 'short',
                                timeStyle: 'short',
                              })
                            ) : (
                              <span className="text-ink-400 italic">
                                {t('filters.scheduledInterview')}
                              </span>
                            )}
                          </div>
                          <div className="text-center">
                            {a.interviewScore != null ? (
                              <span className="text-sm font-bold text-brand-600 dark:text-brand-400">
                                {a.interviewScore}
                              </span>
                            ) : (
                              <span className="text-ink-400 text-sm font-medium">—</span>
                            )}
                          </div>
                        </>
                      )}
                      <div className="text-center flex flex-col items-center justify-center gap-1">
                        <span
                          className={`rounded-full px-2.5 py-0.5 text-[11px] font-medium ${appStatusBadge(a.status)} whitespace-nowrap`}
                        >
                          {appStatusLabel(a.status)}
                        </span>
                      </div>
                      <div className="flex items-center justify-center gap-1 shrink-0">
                        <div className="w-auto min-w-[144px] flex gap-2 shrink-0 justify-center">
                          {!a.currentRound || a.currentRound === 0 ? (
                            <>
                              <button
                                disabled={
                                  processingAppId != null ||
                                  (a.status !== 'cv_submitted' && a.status !== 'invited') ||
                                  batchProcessing
                                }
                                onClick={() => handleAccept(a.id)}
                                title={t('actions.approve')}
                                className="flex flex-1 items-center justify-center gap-1 py-1.5 rounded-lg bg-emerald-600 text-white text-xs font-semibold hover:bg-emerald-500 transition-colors disabled:opacity-50 disabled:cursor-not-allowed shadow-sm whitespace-nowrap"
                              >
                                {processingAppId === a.id ? (
                                  <Loader2 className="w-3 h-3 animate-spin" />
                                ) : (
                                  <Check className="w-3 h-3" />
                                )}
                                {t('actions.approve')}
                              </button>
                              <button
                                disabled={
                                  processingAppId != null ||
                                  (a.status !== 'cv_submitted' && a.status !== 'invited') ||
                                  batchProcessing
                                }
                                onClick={() => handleReject(a.id)}
                                title={t('actions.reject')}
                                className="flex flex-1 items-center justify-center gap-1 py-1.5 rounded-lg border border-red-200 dark:border-red-500/30 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 text-xs font-semibold transition-colors disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
                              >
                                <X className="w-3 h-3" />
                                {t('actions.reject')}
                              </button>
                            </>
                          ) : a.status === 'not_pass' || a.status === 'cv_rejected' || a.status === 'failed' || a.status === 'rejected' ? (
                            <span className="px-3 py-1 bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-300 font-medium text-xs rounded-lg whitespace-nowrap">
                              Đã loại (Không đạt)
                            </span>
                          ) : a.status === 'pass' ? (
                            <span className="px-3 py-1 bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-300 font-medium text-xs rounded-lg whitespace-nowrap">
                              Trúng tuyển
                            </span>
                          ) : (
                            <div className="flex gap-2 w-full">
                              <button
                                onClick={() => {
                                  const targetRound = activeRoundNumber > 0 ? activeRoundNumber : (a.currentRound && a.currentRound > 0 ? a.currentRound : 1)
                                  setInviteModalTarget({ id: a.id, name: a.candidateName || t('candidate'), targetRound })
                                }}
                                disabled={
                                  inviteModalTarget?.id === a.id ||
                                  (a.currentRound != null &&
                                    activeRoundNumber > 0 &&
                                    a.currentRound > activeRoundNumber) ||
                                  batchProcessing
                                }
                                className="flex flex-1 items-center justify-center gap-1 py-1.5 text-xs font-semibold text-white bg-brand-600 hover:bg-brand-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed shadow-sm transition-colors whitespace-nowrap"
                              >
                                {inviteModalTarget?.id === a.id ? (
                                  <Loader2 className="w-3 h-3 animate-spin" />
                                ) : (
                                  <Send className="w-3 h-3" />
                                )}
                                {t('actions.invite')}
                              </button>
                              <button
                                disabled={
                                  processingAppId != null ||
                                  batchProcessing
                                }
                                onClick={() => handleReject(a.id)}
                                className="flex flex-1 items-center justify-center gap-1 py-1.5 rounded-lg border border-red-200 dark:border-red-500/30 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 text-xs font-semibold transition-colors disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
                                title="Loại ứng viên khỏi quy trình tuyển dụng"
                              >
                                <X className="w-3 h-3" />
                                {t('actions.reject')}
                              </button>
                            </div>
                          )}
                        </div>
                        {a.cvFileUrl ? (
                          <button
                            type="button"
                            onClick={() =>
                              openDocument(
                                resolveAssetUrl(a.cvFileUrl!),
                                `${a.candidateName || t('candidate')} - CV`
                              )
                            }
                            title={t('actions.viewCv')}
                            className="p-2 text-ink-400 hover:text-brand-600 hover:bg-ink-100 dark:hover:bg-white/10 rounded-lg transition-colors shrink-0"
                          >
                            <FileText className="h-4 w-4" />
                          </button>
                        ) : (
                          <div className="w-8 h-8 shrink-0" />
                        )}
                        <button
                          type="button"
                          onClick={() =>
                            setSelectedCoverLetter({
                              candidateName: a.candidateName,
                              text: a.coverLetter || '',
                              email: a.candidateEmail,
                              phone: a.candidatePhone,
                              noticePeriod: a.noticePeriod,
                              cvJdSummary: a.cvJdSummary,
                              matchScore: a.matchScore,
                              cvFileUrl: a.cvFileUrl,
                            })
                          }
                          title={t('actions.viewApplication')}
                          className="p-2 text-ink-400 hover:text-brand-600 hover:bg-ink-100 dark:hover:bg-white/10 rounded-lg transition-colors shrink-0"
                        >
                          <ScrollText className="h-4 w-4" />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </>
            )}
          </>
        )}
      </div>

      {processedApps.length > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          total={processedApps.length}
          label={t('paginationLabel')}
          onPageChange={setPage}
        />
      )}

      {selectedCoverLetter && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-ink-950/50 backdrop-blur-sm">
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="w-full max-w-lg rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-6 shadow-card-hover text-left flex flex-col max-h-[90vh]"
          >
            <div className="flex items-center justify-between border-b border-ink-100 dark:border-white/10 pb-3 mb-4 shrink-0">
              <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                {t('coverLetterModal.title', { name: selectedCoverLetter.candidateName })}
              </h3>
              <button
                onClick={() => setSelectedCoverLetter(null)}
                className="p-1 rounded-lg text-ink-400 hover:text-ink-600 hover:bg-ink-50 dark:hover:bg-white/5 transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto space-y-4 pr-1">
              {selectedCoverLetter.cvJdSummary ? (
                <div className="rounded-xl border border-ai-100 bg-ai-50/50 p-4 dark:border-ai-500/20 dark:bg-ai-500/10">
                  <div className="flex items-center justify-between mb-2">
                    <span className="flex items-center gap-1.5 text-xs font-semibold text-ai-700 dark:text-ai-400 uppercase tracking-wider">
                      <Sparkles className="h-3.5 w-3.5 text-ai-500 animate-pulse" />{' '}
                      {t('coverLetterModal.aiSummary')}
                    </span>
                    {selectedCoverLetter.matchScore != null && (
                      <span className="rounded-full bg-ai-100 px-2 py-0.5 text-xs font-bold text-ai-800 dark:bg-ai-500/30 dark:text-ai-300">
                        {t('coverLetterModal.matchScore')}: {selectedCoverLetter.matchScore}%
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-ai-900 dark:text-ai-100 leading-relaxed whitespace-pre-line">
                    {selectedCoverLetter.cvJdSummary}
                  </p>
                </div>
              ) : (
                <div className="rounded-xl border border-amber-200 bg-amber-50/50 p-4 dark:border-amber-500/20 dark:bg-amber-500/10">
                  <span className="flex items-center gap-1.5 text-xs font-semibold text-amber-700 dark:text-amber-400 uppercase tracking-wider">
                    <Sparkles className="h-3.5 w-3.5 text-amber-500" />{' '}
                    {t('coverLetterModal.noAiData')}
                  </span>
                  <p className="mt-1 text-xs text-amber-900 dark:text-amber-100 leading-relaxed">
                    {t('coverLetterModal.noAiDataHint')}
                  </p>
                </div>
              )}
              <div className="rounded-xl border border-ink-100 bg-ink-50/40 p-4 dark:border-white/10 dark:bg-white/5">
                <h4 className="text-xs font-semibold text-ink-500 dark:text-ink-400 uppercase tracking-wider mb-2">
                  {t('coverLetterModal.contactInfo')}
                </h4>
                <div className="grid grid-cols-2 gap-3 text-xs">
                  <div>
                    <span className="text-ink-400 block mb-0.5">
                      {t('coverLetterModal.fullName')}
                    </span>
                    <span className="font-medium text-ink-950 dark:text-white">
                      {selectedCoverLetter.candidateName}
                    </span>
                  </div>
                  <div>
                    <span className="text-ink-400 block mb-0.5">{t('coverLetterModal.phone')}</span>
                    <span className="font-medium text-ink-950 dark:text-white">
                      {selectedCoverLetter.phone || '—'}
                    </span>
                  </div>
                  <div>
                    <span className="text-ink-400 block mb-0.5">{t('coverLetterModal.email')}</span>
                    <span className="font-medium text-ink-950 dark:text-white truncate block">
                      {selectedCoverLetter.email}
                    </span>
                  </div>
                  <div>
                    <span className="text-ink-400 block mb-0.5">
                      {t('coverLetterModal.noticePeriod')}
                    </span>
                    <span className="font-medium text-ink-950 dark:text-white">
                      {selectedCoverLetter.noticePeriod || '—'}
                    </span>
                  </div>
                </div>
              </div>
              <div className="rounded-xl border border-ink-100 bg-ink-50/20 p-4 dark:border-white/5 dark:bg-white-[0.02]">
                <h4 className="text-xs font-semibold text-ink-500 dark:text-ink-400 uppercase tracking-wider mb-2">
                  {t('coverLetterModal.coverLetter')}
                </h4>
                <p className="text-xs text-ink-700 dark:text-ink-300 leading-relaxed whitespace-pre-line">
                  {selectedCoverLetter.text || t('coverLetterModal.noCoverLetter')}
                </p>
              </div>
            </div>
            <div className="flex justify-end mt-4 pt-3 border-t border-ink-100 dark:border-white/10 shrink-0">
              <button
                onClick={() => setSelectedCoverLetter(null)}
                className="px-4 py-2 text-sm font-semibold text-ink-700 dark:text-ink-200 bg-ink-100 dark:bg-white/10 hover:bg-ink-200 dark:hover:bg-white/20 rounded-xl transition-colors"
              >
                {t('coverLetterModal.close')}
              </button>
            </div>
          </motion.div>
        </div>
      )}

      {inviteModalTarget && id && (
        <InviteAndScheduleModal
          applicationId={inviteModalTarget.id}
          candidateName={inviteModalTarget.name}
          jobPostingId={id}
          targetRoundNumber={inviteModalTarget.targetRound}
          onClose={() => setInviteModalTarget(null)}
          onSuccess={(msg) => {
            setNotice(msg)
            void load()
          }}
        />
      )}
    </div>
  )
}
