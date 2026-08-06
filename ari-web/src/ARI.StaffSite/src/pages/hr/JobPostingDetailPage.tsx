import { useState, useEffect, useCallback, useMemo } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  MapPin,
  Clock,
  Calendar,
  ArrowLeft,
  Edit2,
  Loader2,
  Sparkles,
  Languages,
  Hourglass,
  ShieldAlert,
  ShieldCheck,
  Target,
  FileText,
  Download,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Check,
  X,
  Send,
  ScrollText,
} from 'lucide-react'
import jobService from '@ari/shared/fservices/job'
import { applicationService } from '@ari/shared/fservices/application'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import { Pagination } from '@ari/shared/ui'
import { STAFF_NOTIF_REFRESH_EVENT } from '@ari/shared/fservices/notification/notificationService'
import type { JobPosting } from '@ari/shared/types/job'
import type { HrApplicationItem } from '@ari/shared/types/application'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import { appStatusBadge, appStatusLabel, initials, scoreColor } from '../recruiter/_jobUi'

function toLocalDateStr(d: string | Date | number | undefined | null): string {
  if (!d) return ''
  const dt = new Date(d)
  if (isNaN(dt.getTime())) return ''
  const yyyy = dt.getFullYear()
  const mm = String(dt.getMonth() + 1).padStart(2, '0')
  const dd = String(dt.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

export default function JobPostingDetailPage() {
  const { t } = useTranslation('modules/hr/jobPostingDetail')
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { openDocument } = useDocumentViewer()
  const [job, setJob] = useState<JobPosting | null>(null)
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)
  const [apps, setApps] = useState<HrApplicationItem[]>([])
  const [loadingApps, setLoadingApps] = useState(false)
  const [processingAppId, setProcessingAppId] = useState<string | null>(null)
  const [invitingId, setInvitingId] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<string>('cv_review')
  const [page, setPage] = useState<number>(1)
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

  const activeRoundNumber = useMemo(() => {
    return activeTab.startsWith('round_') ? parseInt(activeTab.replace('round_', ''), 10) : 0
  }, [activeTab])

  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [rejectOpen, setRejectOpen] = useState(false)
  const [rejectReason, setRejectReason] = useState('')

  const loadApps = useCallback(async () => {
    if (!id) return
    try {
      setLoadingApps(true)
      const appsData = await jobService.getJobApplications(id)
      setApps(appsData)
    } catch (err) {
      console.error(err)
    } finally {
      setLoadingApps(false)
    }
  }, [id])

  const load = useCallback(async () => {
    if (!id) return
    try {
      setLoading(true)
      const data = await jobService.getJobPostingById(id)
      setJob(data)
      await loadApps()
    } catch (err) {
      console.error(err)
      setError(t('errors.loadJob'))
    } finally {
      setLoading(false)
    }
  }, [id, loadApps, t])

  useEffect(() => {
    void load()

    const onRefresh = () => {
      void load()
    }
    window.addEventListener(STAFF_NOTIF_REFRESH_EVENT, onRefresh)
    return () => {
      window.removeEventListener(STAFF_NOTIF_REFRESH_EVENT, onRefresh)
    }
  }, [load])

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
    if (!window.confirm(t('confirmDialogs.batchAccept', { count: selectedIds.length }))) return
    setBatchProcessing(true)
    setActionError(null)
    setNotice(null)
    try {
      let successCount = 0
      let failCount = 0
      const results = await Promise.allSettled(
        selectedIds.map((id) => applicationService.acceptApplication(id))
      )
      results.forEach((res) => {
        if (res.status === 'fulfilled') successCount++
        else failCount++
      })
      setNotice(t('notices.batchAcceptSuccess', { success: successCount, failed: failCount }))
      setSelectedIds([])
      await loadApps()
    } catch (err) {
      setActionError(t('errors.batchAccept'))
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleBatchReject = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(t('confirmDialogs.batchReject', { count: selectedIds.length }))) return
    setBatchProcessing(true)
    setActionError(null)
    setNotice(null)
    try {
      let successCount = 0
      let failCount = 0
      const results = await Promise.allSettled(
        selectedIds.map((id) => applicationService.rejectApplication(id))
      )
      results.forEach((res) => {
        if (res.status === 'fulfilled') successCount++
        else failCount++
      })
      setNotice(t('notices.batchRejectSuccess', { success: successCount, failed: failCount }))
      setSelectedIds([])
      await loadApps()
    } catch (err) {
      setActionError(t('errors.batchReject'))
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleBatchInvite = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(t('confirmDialogs.batchInvite', { count: selectedIds.length }))) return
    setBatchProcessing(true)
    setActionError(null)
    setNotice(null)
    try {
      let successCount = 0
      let failCount = 0
      const results = await Promise.allSettled(
        selectedIds.map((id) => applicationService.sendInvite(id))
      )
      results.forEach((res) => {
        if (res.status === 'fulfilled') successCount++
        else failCount++
      })
      setNotice(t('notices.batchInviteSuccess', { success: successCount, failed: failCount }))
      setSelectedIds([])
      await loadApps()
    } catch (err) {
      setActionError(t('errors.batchInvite'))
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleAccept = async (appId: string) => {
    setProcessingAppId(appId)
    setActionError(null)
    setNotice(null)
    try {
      await applicationService.acceptApplication(appId)
      setNotice(t('notices.acceptSuccess'))
      await loadApps()
    } catch (err) {
      setActionError(t('errors.acceptApplication'))
    } finally {
      setProcessingAppId(null)
    }
  }

  const sendInvite = async (appId: string) => {
    setInvitingId(appId)
    setActionError(null)
    setNotice(null)
    try {
      await applicationService.sendInvite(appId)
      setNotice(t('notices.inviteSuccess'))
    } catch (e: any) {
      setActionError(e?.response?.data?.message || t('errors.sendInvite'))
    } finally {
      setInvitingId(null)
    }
  }

  const handleReject = async (appId: string) => {
    if (!window.confirm(t('confirmDialogs.rejectApplication'))) return
    setProcessingAppId(appId)
    setActionError(null)
    setNotice(null)
    try {
      await applicationService.rejectApplication(appId)
      setNotice(t('notices.rejectSuccess'))
      await loadApps()
    } catch (err) {
      setActionError(t('errors.rejectApplication'))
    } finally {
      setProcessingAppId(null)
    }
  }

  interface FunnelColumn {
    id: string
    title: string
    subtitle?: string
    candidates: HrApplicationItem[]
  }

  const columns = useMemo<FunnelColumn[]>(() => {
    if (!job) return []

    const cvReviewCandidates = apps.filter((a) => !a.currentRound || a.currentRound === 0)

    const roundCols: FunnelColumn[] = (job.roundConfigs || [])
      .slice()
      .sort((a, b) => a.roundNumber - b.roundNumber)
      .map((rc) => {
        const candidates = apps.filter((a) => a.currentRound === rc.roundNumber)
        const roundTypeStr = rc.roundType
          ? rc.roundType.toLowerCase() === 'screening'
            ? t('rounds.types.screening')
            : rc.roundType.toLowerCase() === 'technical'
              ? t('rounds.types.technical')
              : rc.roundType.toLowerCase() === 'online_test'
                ? t('rounds.types.onlineTest')
                : rc.roundType
          : ''
        const typeText = roundTypeStr ? ` (${roundTypeStr})` : ''
        return {
          id: `round_${rc.roundNumber}`,
          title: `${t('rounds.round', { number: rc.roundNumber })}${typeText}`,
          subtitle:
            rc.roundType === 'technical'
              ? t('rounds.types.technical')
              : rc.roundType === 'online_test'
                ? t('rounds.types.onlineTest')
                : t('rounds.types.screening'),
          candidates,
        }
      })

    return [
      {
        id: 'cv_review',
        title: t('funnel.cvReview'),
        subtitle: t('funnel.cvReviewSubtitle'),
        candidates: cvReviewCandidates,
      },
      ...roundCols,
    ]
  }, [job, apps, t])

  const tabs = useMemo(() => {
    return columns.map((col) => ({
      id: col.id,
      label: col.title,
      count: col.candidates.length,
    }))
  }, [columns])

  const activeCandidates = useMemo(() => {
    const col = columns.find((c) => c.id === activeTab)
    return col ? col.candidates : []
  }, [columns, activeTab])

  const processedCandidates = useMemo(() => {
    let result = [...activeCandidates]

    if (activeTab === 'cv_review') {
      if (cvDateFilter) {
        result = result.filter((a) => toLocalDateStr(a.createdAt) === cvDateFilter)
      }
      if (cvStatusFilter && cvStatusFilter !== 'all') {
        result = result.filter((a) => a.status === cvStatusFilter)
      }
      if (cvSortOrder === 'desc') {
        result.sort((a, b) => (b.matchScore ?? 0) - (a.matchScore ?? 0))
      } else if (cvSortOrder === 'asc') {
        result.sort((a, b) => (a.matchScore ?? 0) - (b.matchScore ?? 0))
      }
    } else {
      if (interviewDateFilter) {
        result = result.filter((a) => toLocalDateStr(a.interviewDate) === interviewDateFilter)
      }
      if (interviewStatusFilter && interviewStatusFilter !== 'all') {
        result = result.filter((a) => a.status === interviewStatusFilter)
      }
      if (interviewSortOrder === 'desc') {
        result.sort((a, b) => (b.interviewScore ?? 0) - (a.interviewScore ?? 0))
      } else if (interviewSortOrder === 'asc') {
        result.sort((a, b) => (a.interviewScore ?? 0) - (b.interviewScore ?? 0))
      }
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
      if (activeTab === 'cv_review') {
        return a.status === 'cv_submitted' || a.status === 'invited'
      } else {
        return !(
          a.status === 'rejected' ||
          a.status === 'failed' ||
          a.status === 'not_pass' ||
          a.status === 'cv_rejected' ||
          a.status === 'pass' ||
          (a.currentRound != null && activeRoundNumber > 0 && a.currentRound > activeRoundNumber)
        )
      }
    },
    [activeTab, activeRoundNumber]
  )

  const PAGE_SIZE = 12
  const totalPages = Math.max(1, Math.ceil(processedCandidates.length / PAGE_SIZE))
  const pagedCandidates = useMemo(() => {
    return processedCandidates.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)
  }, [processedCandidates, page])

  const pageSelectableCandidates = useMemo(() => {
    return pagedCandidates.filter(isSelectable)
  }, [pagedCandidates, isSelectable])

  const isAllSelected = useMemo(() => {
    if (pageSelectableCandidates.length === 0) return false
    return pageSelectableCandidates.every((c) => selectedIds.includes(c.id))
  }, [pageSelectableCandidates, selectedIds])

  const handleSelectAll = useCallback(() => {
    if (isAllSelected) {
      setSelectedIds((prev) =>
        prev.filter((id) => !pageSelectableCandidates.some((c) => c.id === id))
      )
    } else {
      setSelectedIds((prev) => {
        const newIds = pageSelectableCandidates.map((c) => c.id).filter((id) => !prev.includes(id))
        return [...prev, ...newIds]
      })
    }
  }, [isAllSelected, pageSelectableCandidates])

  useEffect(() => {
    setPage(1)
  }, [activeTab])

  useEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])

  const approve = async () => {
    if (!id) return
    setBusy(true)
    setActionError(null)
    setNotice(null)
    try {
      await jobService.updateJobStatus(id, 'active')
      setNotice(t('notices.jobApproveSuccess'))
      await load()
    } catch (err: unknown) {
      setActionError(t('errors.approveJob'))
    } finally {
      setBusy(false)
    }
  }

  const reject = async () => {
    if (!id || !rejectReason.trim()) return
    setBusy(true)
    setActionError(null)
    setNotice(null)
    try {
      await jobService.updateJobStatus(id, 'rejected', rejectReason.trim())
      setRejectOpen(false)
      setRejectReason('')
      setNotice(t('notices.jobRejectSuccess'))
      await load()
    } catch (err: unknown) {
      setActionError(t('errors.rejectJob'))
    } finally {
      setBusy(false)
    }
  }

  const handleToggleDisplay = async (field: 'isUrgent' | 'isPublicListing', value: boolean) => {
    if (!id || !job) return
    const originalValue = job[field]
    if (originalValue === value) return

    setJob({ ...job, [field]: value })
    try {
      await jobService.updateJobDisplay(id, { [field]: value })
    } catch (err: unknown) {
      setJob({ ...job, [field]: originalValue }) // Revert on failure
      setActionError(t(`errors.toggleFailed`))
    }
  }

  const getDeadlineText = (deadlineStr?: string | null): string => {
    if (!deadlineStr) return t('deadline.noLimit')
    const d = new Date(deadlineStr)
    if (Number.isNaN(d.getTime())) return '—'
    const formattedDate = d.toLocaleDateString()
    const today = new Date()
    today.setHours(0, 0, 0, 0)
    const target = new Date(d)
    target.setHours(0, 0, 0, 0)
    const diffDays = Math.round((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))
    if (diffDays < 0) return `${formattedDate} (${t('deadline.expired')})`
    if (diffDays === 0) return `${formattedDate} (${t('deadline.today')})`
    return `${formattedDate} (${t('deadline.daysLeft', { count: diffDays })})`
  }

  const formatSalary = (job: JobPosting): string => {
    if (
      job.salaryIsNegotiable ||
      (job.salaryMin == null && job.salaryMax == null) ||
      (job.salaryMin === 0 && job.salaryMax === 0)
    ) {
      return t('salary.negotiable')
    }

    const cur = (job.salaryCurrency || 'VND').toUpperCase()
    const formatVal = (n: number) => {
      if (cur === 'VND') return n.toLocaleString('vi-VN')
      return n.toLocaleString('en-US')
    }
    const unit = cur === 'VND' ? ' ₫' : ` ${cur}`

    if (
      job.salaryMin != null &&
      job.salaryMax != null &&
      job.salaryMin !== 0 &&
      job.salaryMax !== 0
    ) {
      return `${formatVal(job.salaryMin)} - ${formatVal(job.salaryMax)}${unit}`
    }
    if (job.salaryMin != null && job.salaryMin !== 0) {
      return `${t('salary.from')} ${formatVal(job.salaryMin)}${unit}`
    }
    if (job.salaryMax != null && job.salaryMax !== 0) {
      return `${t('salary.to')} ${formatVal(job.salaryMax)}${unit}`
    }
    return t('salary.negotiable')
  }

  const formatWorkMode = (mode?: string): string => {
    if (!mode) return t('noLocation')
    const mappings: Record<string, string> = {
      fulltime: t('workModes.fulltime'),
      parttime: t('workModes.parttime'),
      contract: t('workModes.contract'),
      internship: t('workModes.internship'),
    }
    return mappings[mode.toLowerCase()] || mode
  }

  const getStatusLabel = (status: string, deadline?: string | null): string => {
    if (status === 'active' && deadline && new Date(deadline).getTime() < Date.now()) {
      return 'Hết hạn (Đã đóng)'
    }
    return t(`statusLabels.${status}`) || status
  }

  const getStatusBadge = (status: string, deadline?: string | null): string => {
    if (status === 'active' && deadline && new Date(deadline).getTime() < Date.now()) {
      return 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400'
    }
    const badges: Record<string, string> = {
      draft: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
      pending: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
      active: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
      rejected: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
      closed: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400',
      archived: 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400',
      paused: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    }
    return badges[status] || badges.closed
  }

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh] gap-3 bg-ink-50 dark:bg-ink-950">
        <Loader2 className="w-10 h-10 text-brand-600 dark:text-brand-400 animate-spin" />
        <p className="text-sm text-ink-500 dark:text-ink-400">{t('loading')}</p>
      </div>
    )
  }

  if (error || !job) {
    return (
      <div className="p-6 lg:p-8 max-w-4xl mx-auto text-center py-20 bg-ink-50 dark:bg-ink-950">
        <ShieldAlert className="w-16 h-16 text-red-500 dark:text-red-400 mx-auto mb-4" />
        <h2 className="text-2xl font-bold text-ink-900 dark:text-white mb-2">{t('errorTitle')}</h2>
        <p className="text-ink-600 dark:text-ink-400 mb-6">{error || t('errorNotFound')}</p>
        <button
          onClick={() => navigate('/hr/jobs')}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 font-medium hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" /> {t('back')}
        </button>
      </div>
    )
  }

  const isPending = job.status === 'pending'

  return (
    <div className="p-4 sm:p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <button
          onClick={() => navigate('/hr/jobs')}
          className="inline-flex items-center gap-2 text-sm text-ink-600 dark:text-ink-400 hover:text-brand-600 dark:hover:text-brand-400 mb-6 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" /> {t('back')}
        </button>

        <div className="flex flex-col md:flex-row md:items-start justify-between gap-6 mb-8">
          <div className="flex items-start gap-6">
            <div className="w-20 h-20 rounded-xl bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-2xl font-bold text-white">
              {job.title.substring(0, 2).toUpperCase()}
            </div>
            <div>
              <div className="flex items-center gap-3 mb-2">
                <h1 className="text-3xl font-semibold text-ink-900 dark:text-white">{job.title}</h1>
                <span
                  className={`px-3 py-1 rounded-full text-xs font-semibold ${getStatusBadge(job.status, job.applicationDeadline)}`}
                >
                  {getStatusLabel(job.status, job.applicationDeadline)}
                </span>
              </div>
              <div className="flex items-center gap-4 mb-4 flex-wrap">
                <p className="text-xl text-ink-600 dark:text-ink-400">
                  {job.department || t('department')}
                </p>
                
                <div className="flex items-center gap-4 border-l border-ink-200 dark:border-white/10 pl-4">
                  <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-ink-700 dark:text-ink-300 bg-white dark:bg-white/5 px-2.5 py-1 rounded-lg border border-ink-200 dark:border-white/10 shadow-sm transition-colors hover:bg-ink-50 dark:hover:bg-white/10">
                    <input type="checkbox" checked={job.isUrgent} onChange={(e) => handleToggleDisplay('isUrgent', e.target.checked)} className="accent-brand-600" />
                    Tuyển gấp
                  </label>
                  <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-ink-700 dark:text-ink-300 bg-white dark:bg-white/5 px-2.5 py-1 rounded-lg border border-ink-200 dark:border-white/10 shadow-sm transition-colors hover:bg-ink-50 dark:hover:bg-white/10">
                    <input type="checkbox" checked={job.isPublicListing} onChange={(e) => handleToggleDisplay('isPublicListing', e.target.checked)} className="accent-brand-600" />
                    Public Website
                  </label>
                </div>
              </div>
              <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm text-ink-600 dark:text-ink-400">
                <span className="flex items-center gap-1.5">
                  <MapPin className="w-4 h-4 text-brand-600 dark:text-brand-400" />
                  {job.location || t('noLocation')}
                </span>
                <span className="flex items-center gap-1.5">{formatSalary(job)}</span>
                <span className="flex items-center gap-1.5">
                  <Clock className="w-4 h-4 text-brand-600 dark:text-brand-400" />
                  {formatWorkMode(job.workMode || job.employmentType)}
                </span>
                <span className="flex items-center gap-1.5">
                  <Calendar className="w-4 h-4 text-brand-600 dark:text-brand-400" />
                  {t('deadline.label')} {getDeadlineText(job.applicationDeadline)}
                </span>
                {job.vacancies != null && job.vacancies > 0 && (
                  <span className="flex items-center gap-1.5">
                    <Target className="w-4 h-4 text-brand-600 dark:text-brand-400" />
                    {t('vacancies')} {job.vacancies}
                  </span>
                )}
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <button
              type="button"
              onClick={() => navigate(`/hr/jobs/${job.id}/online-test`)}
              className="flex items-center justify-center gap-2 px-5 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 font-medium hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
            >
              <ScrollText className="w-4 h-4" /> {t('onlineTestBank')}
            </button>
            <button
              type="button"
              onClick={() => navigate(`/hr/jobs/${job.id}/edit`)}
              className="flex items-center justify-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity"
            >
              <Edit2 className="w-4 h-4" /> {t('edit')}
            </button>
          </div>
        </div>

        {notice && (
          <div className="mb-4 flex items-start justify-between gap-3 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
            <span className="flex items-center gap-2">
              <CheckCircle2 className="w-4 h-4" /> {notice}
            </span>
            <button onClick={() => setNotice(null)} className="hover:opacity-70">
              {t('notices.close')}
            </button>
          </div>
        )}
        {actionError && (
          <div className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-400">
            <AlertCircle className="mt-0.5 w-4 h-4 shrink-0" /> {actionError}
          </div>
        )}

        {isPending && (
          <div className="mb-6 rounded-2xl border border-amber-200 dark:border-amber-500/30 bg-amber-50/60 dark:bg-amber-500/10 p-5 shadow-card">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex items-start gap-3">
                <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-amber-100 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400">
                  <ShieldCheck className="h-5 w-5" />
                </span>
                <div>
                  <h3 className="text-base font-semibold text-ink-900 dark:text-white">
                    {t('pendingApproval.title')}
                  </h3>
                  <p className="mt-0.5 text-sm text-ink-600 dark:text-ink-400">
                    {t('pendingApproval.description', { name: job.createdByName || '' })}
                  </p>
                </div>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <button
                  type="button"
                  disabled={busy}
                  onClick={approve}
                  className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-500 disabled:opacity-50"
                >
                  {busy ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <CheckCircle2 className="h-4 w-4" />
                  )}{' '}
                  {t('pendingApproval.approve')}
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => {
                    setRejectOpen(true)
                    setRejectReason('')
                  }}
                  className="inline-flex items-center gap-2 rounded-xl border border-red-200 dark:border-red-500/30 bg-white dark:bg-white/5 px-4 py-2.5 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 disabled:opacity-50"
                >
                  <XCircle className="h-4 w-4" /> {t('pendingApproval.reject')}
                </button>
              </div>
            </div>
          </div>
        )}
        
        {job.status === 'draft' && (
          <div className="mb-6 rounded-2xl border border-blue-200 dark:border-blue-500/30 bg-blue-50/60 dark:bg-blue-500/10 p-5 shadow-card">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex items-start gap-3">
                <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-blue-100 dark:bg-blue-500/20 text-blue-600 dark:text-blue-400">
                  <Edit2 className="h-5 w-5" />
                </span>
                <div>
                  <h3 className="text-base font-semibold text-ink-900 dark:text-white">
                    Bản nháp của bạn
                  </h3>
                  <p className="mt-0.5 text-sm text-ink-600 dark:text-ink-400">
                    Đây là tin tuyển dụng nháp. Bạn có thể kích hoạt trực tiếp mà không cần duyệt, hoặc tiếp tục chỉnh sửa.
                  </p>
                </div>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <button
                  type="button"
                  disabled={busy}
                  onClick={approve}
                  className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-500 disabled:opacity-50"
                >
                  {busy ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Target className="h-4 w-4" />
                  )}{' '}
                  Kích hoạt luôn
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => navigate(`/hr/jobs/${job.id}/edit`)}
                  className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/30 bg-white dark:bg-white/5 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 disabled:opacity-50"
                >
                  <Edit2 className="h-4 w-4" /> Sửa tin
                </button>
              </div>
            </div>
          </div>
        )}

        {job.status === 'rejected' && job.rejectionReason && (
          <div className="mb-6 flex items-start gap-2 rounded-2xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-400">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              <b>{t('rejectedBanner.label')}</b> {job.rejectionReason}
            </span>
          </div>
        )}

        {job.approvedAt && (job.status === 'active' || job.status === 'closed') && (
          <div className="mb-6 flex items-center gap-2 rounded-2xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
            <ShieldCheck className="h-4 w-4 shrink-0" />
            <span>
              {t('approvedBanner.label')} <b>{job.approverName || 'HR Leader'}</b>{' '}
              {t('approvedBanner.at')} {new Date(job.approvedAt).toLocaleString()}.
            </span>
          </div>
        )}

        <div className="grid lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-6">
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.05 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4 flex items-center gap-2">
                <FileText className="w-5 h-5 text-brand-600 dark:text-brand-400" />{' '}
                {t('jdSection.title')}
              </h2>
              {job.jdFileUrl ? (
                <div className="flex flex-wrap gap-3">
                  <button
                    type="button"
                    onClick={() =>
                      openDocument(job.jdFileUrl!, job.jdFileName || `${job.title} - JD`)
                    }
                    className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
                  >
                    <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                    {job.jdFileName || t('jdSection.originalJd')}
                    {job.jdFileFormat && (
                      <span className="text-xs uppercase text-ink-400">{job.jdFileFormat}</span>
                    )}
                  </button>
                  {job.signedJdFileUrl && (
                    <button
                      type="button"
                      onClick={() => openDocument(job.signedJdFileUrl!, `${job.title} - JD`)}
                      className="inline-flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 px-4 py-2.5 text-sm font-medium text-emerald-700 dark:text-emerald-400 hover:bg-emerald-100 dark:hover:bg-emerald-500/20"
                    >
                      <Download className="h-4 w-4" /> {t('jdSection.approvedJd')}
                    </button>
                  )}
                </div>
              ) : (
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('jdSection.noJd')}</p>
              )}
            </motion.div>

            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">
                {t('description')}
              </h2>
              <div
                className="text-ink-600 dark:text-ink-400 leading-relaxed ql-editor-display"
                dangerouslySetInnerHTML={{ __html: job.jobDescription }}
              />
            </motion.div>

            {job.skills && job.skills.length > 0 && (
              <motion.div
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: 0.15 }}
                className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
              >
                <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">
                  {t('skills')}
                </h2>
                <div className="flex flex-wrap gap-2">
                  {job.skills.map((skill) => (
                    <span
                      key={skill}
                      className="px-3 py-1.5 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-700 dark:text-ink-200 text-sm"
                    >
                      {skill}
                    </span>
                  ))}
                </div>
              </motion.div>
            )}

            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.2 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4 flex items-center gap-2">
                <Sparkles className="w-5 h-5 text-brand-600 dark:text-brand-400" />{' '}
                {t('aiConfig.title')}
              </h2>
              <div className="grid grid-cols-1 gap-6 text-sm sm:grid-cols-2">
                <div className="space-y-3">
                  <p className="text-ink-600 dark:text-ink-400">
                    <strong className="text-ink-900 dark:text-white">
                      {t('aiConfig.requiredLanguage')}
                    </strong>{' '}
                    {job.languageRequirement || t('aiConfig.defaultLang')}
                  </p>
                  <p className="text-ink-600 dark:text-ink-400">
                    <strong className="text-ink-900 dark:text-white">
                      {t('aiConfig.autoDetect')}
                    </strong>{' '}
                    {job.detectedLanguage === 'vi'
                      ? t('aiConfig.autoDetectLabels.vi')
                      : t('aiConfig.autoDetectLabels.other')}
                  </p>
                </div>
                <div className="space-y-3">
                  <p className="text-ink-600 dark:text-ink-400">
                    <strong className="text-ink-900 dark:text-white">
                      {t('aiConfig.availableHours')}
                    </strong>{' '}
                    {t('aiConfig.onsiteRequired')}
                  </p>
                  <p className="text-ink-600 dark:text-ink-400">
                    <strong className="text-ink-900 dark:text-white">
                      {t('aiConfig.rescheduleDeadline')}
                    </strong>{' '}
                    {job.rescheduleDeadlineHours || 24} {t('aiConfig.hoursBefore')}
                  </p>
                </div>
              </div>
            </motion.div>
          </div>

          <div className="space-y-6">
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.25 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">
                {t('rounds.title', { count: job.roundConfigs?.length || 0 })}
              </h2>
              {job.roundConfigs && job.roundConfigs.length > 0 ? (
                <div className="space-y-4">
                  {job.roundConfigs.map((round) => (
                    <div
                      key={round.roundNumber}
                      className="p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5 space-y-2"
                    >
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-semibold text-ink-900 dark:text-white">
                          {t('rounds.round', { number: round.roundNumber })}
                        </span>
                        <span
                          className={`px-2 py-0.5 rounded-full text-xs font-semibold ${
                            round.roundType === 'technical'
                              ? 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400'
                              : round.roundType === 'online_test'
                                ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                                : 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400'
                          }`}
                        >
                          {round.roundType === 'technical'
                            ? t('rounds.types.technical')
                            : round.roundType === 'online_test'
                              ? t('rounds.types.onlineTest')
                              : t('rounds.types.screening')}
                        </span>
                      </div>
                      <div className="space-y-1.5 text-xs text-ink-600 dark:text-ink-400">
                        <p className="flex items-center gap-1.5">
                          <Languages className="w-3.5 h-3.5 text-brand-600 dark:text-brand-400" />{' '}
                          {t('rounds.language')}{' '}
                          {round.interviewLanguage === 'vi'
                            ? t('rounds.languages.vi')
                            : t('rounds.languages.other')}
                        </p>
                        <p className="flex items-center gap-1.5">
                          <Hourglass className="w-3.5 h-3.5 text-brand-600 dark:text-brand-400" />{' '}
                          {t('rounds.duration')}{' '}
                          {t('rounds.minutes', { count: round.maxDurationMinutes })}
                        </p>
                        <p className="flex items-center gap-1.5">
                          <Clock className="w-3.5 h-3.5 text-brand-600 dark:text-brand-400" />{' '}
                          {t('rounds.codeTtl')} {round.interviewCodeTtlHours} {t('rounds.hours')}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('rounds.noConfig')}</p>
              )}
            </motion.div>
          </div>
        </div>

        <div className="mt-12 space-y-6">
          <div className="flex items-center justify-between border-b border-ink-200 dark:border-white/10 pb-4">
            <div>
              <h2 className="text-2xl font-bold text-ink-900 dark:text-white flex items-center gap-2">
                <Target className="w-6 h-6 text-brand-600 dark:text-brand-400" />{' '}
                {t('funnel.title')}
              </h2>
              <p className="text-sm text-ink-500 dark:text-ink-400 mt-1">
                {t('funnel.description')}
              </p>
            </div>
            <span className="px-3 py-1 bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 rounded-full text-xs font-semibold">
              {t('funnel.total', { count: apps.length })}
            </span>
          </div>

          {loadingApps ? (
            <div className="flex justify-center items-center py-12">
              <Loader2 className="w-8 h-8 animate-spin text-brand-600 dark:text-brand-400" />
              <span className="ml-2 text-sm text-ink-600 dark:text-ink-400">{t('loading')}</span>
            </div>
          ) : apps.length === 0 ? (
            <div className="text-center py-12 rounded-2xl border border-dashed border-ink-300 dark:border-white/10 bg-white dark:bg-white/5">
              <p className="text-ink-500 dark:text-ink-400">{t('noCandidates')}</p>
            </div>
          ) : (
            <>
              <div className="flex flex-wrap gap-2 mb-6">
                {tabs.map((tab) => (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={`px-4 py-2 rounded-xl text-sm font-semibold transition-all duration-200 ${activeTab === tab.id ? 'bg-brand-600 text-white shadow-sm' : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'}`}
                  >
                    {tab.label} ({tab.count})
                  </button>
                ))}
              </div>

              <div className="flex flex-wrap items-end justify-between gap-4 mb-6 p-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
                <div className="flex flex-wrap items-center gap-4">
                  <div className="flex flex-col gap-1">
                    <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                      {activeTab === 'cv_review'
                        ? t('filters.byDate')
                        : t('filters.byInterviewDate')}
                    </span>
                    <input
                      type="date"
                      value={activeTab === 'cv_review' ? cvDateFilter : interviewDateFilter}
                      onChange={(e) => {
                        if (activeTab === 'cv_review') setCvDateFilter(e.target.value)
                        else setInterviewDateFilter(e.target.value)
                      }}
                      className="px-3 py-1.5 text-xs rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 text-ink-900 dark:text-white focus:outline-none focus:border-brand-500"
                    />
                  </div>
                  <div className="flex flex-col gap-1">
                    <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                      {t('filters.byStatus')}
                    </span>
                    <select
                      value={activeTab === 'cv_review' ? cvStatusFilter : interviewStatusFilter}
                      onChange={(e) => {
                        if (activeTab === 'cv_review') setCvStatusFilter(e.target.value)
                        else setInterviewStatusFilter(e.target.value)
                      }}
                      className="px-3 py-1.5 text-xs rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 text-ink-900 dark:text-white focus:outline-none focus:border-brand-500"
                    >
                      <option value="all">{t('filters.allStatus')}</option>
                      {activeTab === 'cv_review' ? (
                        <>
                          <option value="cv_submitted">{t('funnel.cvSubmitted')}</option>
                          <option value="invited">{t('funnel.invited')}</option>
                          <option value="cv_rejected">{t('funnel.cvRejected')}</option>
                        </>
                      ) : (
                        <>
                          <option value="invited">{t('funnel.waiting')}</option>
                          <option value="screening">{t('funnel.waitingSchedule')}</option>
                          <option value="interview">{t('funnel.interview')}</option>
                          <option value="pass">{t('funnel.pass')}</option>
                          <option value="not_pass">{t('funnel.notPass')}</option>
                          <option value="withdrawn">{t('funnel.withdrawn')}</option>
                        </>
                      )}
                    </select>
                  </div>
                  <div className="flex flex-col gap-1">
                    <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                      {activeTab === 'cv_review'
                        ? t('filters.sortByMatch')
                        : t('filters.sortByScore')}
                    </span>
                    <select
                      value={activeTab === 'cv_review' ? cvSortOrder : interviewSortOrder}
                      onChange={(e) => {
                        if (activeTab === 'cv_review') setCvSortOrder(e.target.value)
                        else setInterviewSortOrder(e.target.value)
                      }}
                      className="px-3 py-1.5 text-xs rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 text-ink-900 dark:text-white focus:outline-none focus:border-brand-500"
                    >
                      <option value="desc">{t('filters.sortOptions.desc')}</option>
                      <option value="asc">{t('filters.sortOptions.asc')}</option>
                      <option value="default">{t('filters.sortOptions.default')}</option>
                    </select>
                  </div>
                </div>
              </div>

              {selectedIds.length > 0 && (
                <div className="flex items-center justify-between p-4 mb-4 rounded-xl border border-brand-200 dark:border-brand-500/30 bg-brand-50/50 dark:bg-brand-500/10 backdrop-blur-sm animate-fade-in">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-semibold text-brand-900 dark:text-brand-400">
                      {t('batchActions.selected', { count: selectedIds.length })}
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
                          )}{' '}
                          {t('batchActions.acceptBatch')}
                        </button>
                        <button
                          disabled={batchProcessing}
                          onClick={handleBatchReject}
                          className="flex items-center gap-1.5 px-4 py-2 rounded-xl border border-red-200 dark:border-red-500/30 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 text-xs font-semibold transition-all disabled:opacity-50"
                        >
                          <X className="w-3.5 h-3.5" /> {t('batchActions.rejectBatch')}
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
                        )}{' '}
                        {t('batchActions.inviteBatch')}
                      </button>
                    )}
                  </div>
                </div>
              )}

              {processedCandidates.length === 0 ? (
                <div className="text-center py-12 rounded-2xl border border-dashed border-ink-300 dark:border-white/10 bg-white dark:bg-white/5">
                  <p className="text-ink-500 dark:text-ink-400">{t('noFilterResults')}</p>
                </div>
              ) : (
                <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 overflow-hidden shadow-card">
                  <div
                    className={`grid gap-4 px-5 py-3 border-b border-ink-200 dark:border-white/10 bg-ink-50/80 dark:bg-white/5 text-xs font-semibold text-ink-500 dark:text-ink-400 items-center ${activeTab === 'cv_review' ? 'grid-cols-[40px_minmax(180px,1.5fr)_110px_110px_100px_120px_230px]' : 'grid-cols-[40px_minmax(180px,1.5fr)_180px_110px_120px_230px]'}`}
                  >
                    <div className="flex items-center justify-center">
                      <input
                        type="checkbox"
                        checked={isAllSelected}
                        disabled={pageSelectableCandidates.length === 0}
                        onChange={handleSelectAll}
                        className="rounded border-ink-300 dark:border-white/10 text-brand-600 focus:ring-brand-500 w-4 h-4 cursor-pointer disabled:opacity-50"
                      />
                    </div>
                    <div>{t('table.candidate')}</div>
                    {activeTab === 'cv_review' ? (
                      <div className="text-center">{t('table.appliedDate')}</div>
                    ) : (
                      <div className="text-center">{t('table.interviewSchedule')}</div>
                    )}
                    {activeTab === 'cv_review' ? (
                      <div className="text-center">{t('table.noticePeriod')}</div>
                    ) : (
                      <div className="text-center">{t('table.interviewScore')}</div>
                    )}
                    {activeTab === 'cv_review' ? (
                      <div className="text-center">{t('table.matchScore')}</div>
                    ) : null}
                    <div className="text-center">{t('table.status')}</div>
                    <div className="text-center">{t('table.actions')}</div>
                  </div>

                  <div className="divide-y divide-ink-100 dark:divide-white/10">
                    {pagedCandidates.map((a: HrApplicationItem) => (
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
                        <div
                          onClick={() => navigate(`/hr/candidates/${a.id}`)}
                          className="flex items-center gap-3 min-w-0 cursor-pointer group"
                        >
                          <div className="w-10 h-10 rounded-full bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 flex items-center justify-center text-sm font-bold shrink-0">
                            {initials(a.candidateName)}
                          </div>
                          <div className="min-w-0">
                            <h4 className="font-semibold text-sm text-ink-900 dark:text-white truncate group-hover:text-brand-600 dark:group-hover:text-brand-400 transition-colors">
                              {a.candidateName}
                            </h4>
                            <p className="text-xs text-ink-500 dark:text-ink-400 truncate mt-0.5">
                              {a.candidateEmail}
                            </p>
                          </div>
                        </div>
                        {activeTab === 'cv_review' ? (
                          <>
                            <div className="text-center text-xs text-ink-600 dark:text-ink-300">
                              {new Date(a.createdAt).toLocaleDateString()}
                            </div>
                            <div
                              className="text-center text-xs text-ink-600 dark:text-ink-300 truncate"
                              title={a.noticePeriod || undefined}
                            >
                              {a.noticePeriod || t('table.notProvided')}
                            </div>
                            <div className="text-center">
                              {a.matchScore != null ? (
                                <span className={`text-sm font-bold ${scoreColor(a.matchScore)}`}>
                                  {a.matchScore}%
                                </span>
                              ) : (
                                <span className="text-ink-400 text-sm font-medium">
                                  {t('table.notProvided')}
                                </span>
                              )}
                            </div>
                          </>
                        ) : (
                          <>
                            <div className="text-center text-xs text-ink-600 dark:text-ink-300">
                              {a.interviewDate ? (
                                new Date(a.interviewDate).toLocaleString(undefined, {
                                  dateStyle: 'short',
                                  timeStyle: 'short',
                                })
                              ) : (
                                <span className="text-ink-400 italic">
                                  {t('table.notScheduled')}
                                </span>
                              )}
                            </div>
                            <div className="text-center">
                              {a.interviewScore != null ? (
                                <span className="text-sm font-bold text-brand-600 dark:text-brand-400">
                                  {a.interviewScore}
                                </span>
                              ) : (
                                <span className="text-ink-400 text-sm font-medium">
                                  {t('table.notProvided')}
                                </span>
                              )}
                            </div>
                          </>
                        )}
                        <div className="text-center flex flex-col items-center justify-center gap-1">
                          <span
                            className={`text-[11px] px-2.5 py-0.5 rounded-full font-medium ${appStatusBadge(a.status)} whitespace-nowrap`}
                          >
                            {appStatusLabel(a.status)}
                          </span>
                        </div>
                        <div className="flex items-center justify-center gap-1 shrink-0">
                          <div className="w-36 flex gap-2 shrink-0 justify-center">
                            {!a.currentRound || a.currentRound === 0 ? (
                              <>
                                <button
                                  disabled={
                                    processingAppId != null ||
                                    (a.status !== 'cv_submitted' && a.status !== 'invited') ||
                                    batchProcessing
                                  }
                                  onClick={() => handleAccept(a.id)}
                                  title={t('actions.accept')}
                                  className="flex flex-1 items-center justify-center gap-1 py-1.5 rounded-lg bg-emerald-600 text-white text-xs font-semibold hover:bg-emerald-500 transition-colors disabled:opacity-50 disabled:cursor-not-allowed shadow-sm whitespace-nowrap"
                                >
                                  {processingAppId === a.id ? (
                                    <Loader2 className="w-3 h-3 animate-spin" />
                                  ) : (
                                    <Check className="w-3 h-3" />
                                  )}{' '}
                                  {t('actions.accept')}
                                </button>
                                <button
                                  disabled={
                                    processingAppId != null ||
                                    (a.status !== 'cv_submitted' && a.status !== 'invited') ||
                                    batchProcessing
                                  }
                                  onClick={() => handleReject(a.id)}
                                  className="flex flex-1 items-center justify-center gap-1 py-1.5 rounded-lg border border-red-200 dark:border-red-500/30 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 text-xs font-semibold transition-colors disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
                                >
                                  <X className="w-3 h-3" /> {t('actions.reject')}
                                </button>
                              </>
                            ) : (
                              <button
                                onClick={() => sendInvite(a.id)}
                                disabled={
                                  invitingId === a.id ||
                                  a.status === 'rejected' ||
                                  a.status === 'failed' ||
                                  a.status === 'not_pass' ||
                                  a.status === 'cv_rejected' ||
                                  a.status === 'pass' ||
                                  (a.currentRound != null &&
                                    activeRoundNumber > 0 &&
                                    a.currentRound > activeRoundNumber) ||
                                  batchProcessing
                                }
                                className="flex w-full items-center justify-center gap-1 py-1.5 text-xs bg-brand-600 text-white hover:bg-brand-700 rounded-lg transition-colors font-semibold shadow-sm disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
                              >
                                {invitingId === a.id ? (
                                  <Loader2 className="w-3 h-3 animate-spin" />
                                ) : (
                                  <Send className="w-3 h-3" />
                                )}{' '}
                                {t('actions.invite')}
                              </button>
                            )}
                          </div>
                          {a.cvFileUrl ? (
                            <button
                              onClick={() =>
                                openDocument(
                                  resolveAssetUrl(a.cvFileUrl!),
                                  `${a.candidateName} - CV`
                                )
                              }
                              title={t('actions.viewCv')}
                              className="p-2 text-ink-400 hover:text-brand-600 hover:bg-ink-100 dark:hover:bg-white/10 rounded-lg transition-colors shrink-0"
                            >
                              <FileText className="w-4 h-4" />
                            </button>
                          ) : (
                            <div className="w-8 h-8 shrink-0" />
                          )}
                          <button
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
                            title={t('actions.viewCoverLetter')}
                            className="p-2 text-ink-400 hover:text-brand-600 hover:bg-ink-100 dark:hover:bg-white/10 rounded-lg transition-colors shrink-0"
                          >
                            <ScrollText className="w-4 h-4" />
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {processedCandidates.length > 0 && (
                <div className="mt-6">
                  <Pagination
                    page={page}
                    totalPages={totalPages}
                    total={activeCandidates.length}
                    label={t('paginationLabel')}
                    onPageChange={setPage}
                  />
                </div>
              )}
            </>
          )}
        </div>
      </motion.div>

      {rejectOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-ink-950/50 backdrop-blur-sm">
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="w-full max-w-md rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-6 shadow-card-hover"
          >
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white mb-1">
              {t('confirmRejectModal.title')}
            </h3>
            <p className="text-sm text-ink-500 dark:text-ink-400 mb-4">
              {t('confirmRejectModal.description', { title: job.title })}
            </p>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              rows={4}
              placeholder={t('confirmRejectModal.placeholder')}
              className="w-full px-3 py-2.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-900 dark:text-white placeholder:text-ink-400 text-sm focus:outline-none focus:ring-2 focus:ring-red-500/40 resize-none"
            />
            <div className="flex items-center justify-end gap-2 mt-4">
              <button
                type="button"
                onClick={() => {
                  setRejectOpen(false)
                  setRejectReason('')
                }}
                className="px-4 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 text-sm font-medium transition-colors"
              >
                {t('confirmRejectModal.cancel')}
              </button>
              <button
                type="button"
                disabled={!rejectReason.trim() || busy}
                onClick={reject}
                className="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-600 text-white text-sm font-medium hover:bg-red-500 transition-colors disabled:opacity-50"
              >
                {busy ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <XCircle className="w-4 h-4" />
                )}{' '}
                {t('confirmRejectModal.confirm')}
              </button>
            </div>
          </motion.div>
        </div>
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
                        {t('coverLetterModal.matchScore', {
                          score: selectedCoverLetter.matchScore,
                        })}
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
                <div className="grid grid-cols-1 gap-3 text-xs sm:grid-cols-2">
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
                      {selectedCoverLetter.phone || t('table.notProvided')}
                    </span>
                  </div>
                  <div>
                    <span className="text-ink-400 block mb-0.5">{t('coverLetterModal.email')}</span>
                    <span
                      className="font-medium text-ink-950 dark:text-white truncate block"
                      title={selectedCoverLetter.email}
                    >
                      {selectedCoverLetter.email}
                    </span>
                  </div>
                  <div>
                    <span className="text-ink-400 block mb-0.5">
                      {t('coverLetterModal.noticePeriod')}
                    </span>
                    <span className="font-medium text-ink-950 dark:text-white">
                      {selectedCoverLetter.noticePeriod || t('table.notProvided')}
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
    </div>
  )
}
