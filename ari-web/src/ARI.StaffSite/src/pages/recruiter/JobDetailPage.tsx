import { useEffect, useMemo, useState } from 'react'
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
  Loader2,
  CheckCircle2,
  AlertCircle,
  Layers,
  Target,
  CalendarClock,
  X,
  ScrollText,
  Sparkles,
} from 'lucide-react'
import { ErrorAlert } from '@ari/shared/ui'
import { useAuthStore } from '@ari/shared/store/auth'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import jobService from '@ari/shared/fservices/job'
import { applicationService } from '@ari/shared/fservices/application'
import { hiringTeamService } from '@ari/shared/fservices/hiringTeam'
import { formatScore } from '@ari/shared/utils/format'
import InviteAndScheduleModal from '../../components/InviteAndScheduleModal'
import CandidatePipeline from '@/components/jobCandidates/CandidatePipeline'
import type { JobPosting } from '@ari/shared/types/job'
import type { HrApplicationItem } from '@ari/shared/types/application'
import {
  jobStatusBadge,
  jobStatusLabel,
  appStatusBadge,
  appStatusLabel,
  formatSalary,
  timeAgo,
} from './_jobUi'
import { JobDetailSkeleton } from './_skeletons'

/**
 * Trạng thái mà nút Duyệt / Loại ở vòng CV còn thao tác được.
 *
 * `hm_review` BẮT BUỘC có mặt: cổng duyệt của Hiring Manager (ADR-061) giữ nguyên trạng thái hồ sơ
 * và chỉ mở cột `hmDecision`, nên sau khi HM duyệt xong hồ sơ vẫn nằm ở `hm_review`. Thiếu nó thì
 * hai nút tắt vĩnh viễn và KHÔNG còn đường nào gọi `AcceptApplicationCommand` — hồ sơ kẹt ở cổng
 * đã mở, đúng thứ mà test `ShortlistGateTests` phía backend khẳng định là không được xảy ra.
 */
import HiringTeamPanel from '@/components/hiring/HiringTeamPanel'

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

  /**
   * `useMemo` chứ không `appsData || []`: khi chưa có dữ liệu, dạng viết kia sinh ra MỘT MẢNG MỚI mỗi
   * lần render, kéo theo mọi `useEffect`/`useMemo` phụ thuộc nó chạy lại vô tận.
   */
  const apps = useMemo(() => appsData ?? [], [appsData])
  const loading = loadingJob || loadingApps

  const [mutationError, setMutationError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [processingAppId, setProcessingAppId] = useState<string | null>(null)

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

  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [batchProcessing, setBatchProcessing] = useState<boolean>(false)
  // Modal chọn ca — nay chỉ còn MỘT việc: xếp lịch (ADR-067 tách duyệt CV khỏi xếp lịch).
  const [inviteModalTarget, setInviteModalTarget] = useState<{
    applications: { id: string; name: string }[]
    targetRound: number
  } | null>(null)

  /**
   * Bỏ chọn khi danh sách hồ sơ đổi (vừa duyệt/loại xong): giữ lại id cũ thì thanh thao tác hàng
   * loạt vẫn đếm những người không còn trên màn.
   */
  useEffect(() => {
    setSelectedIds((prev) => {
      const next = prev.filter((id) => apps.some((a) => a.id === id))
      // Trả LẠI CHÍNH `prev` khi không bỏ ai: `filter` luôn sinh mảng mới, mà mảng mới là một lần
      // render nữa — đủ để thành vòng lặp vô hạn nếu `apps` cũng đổi danh tính theo.
      return next.length === prev.length ? prev : next
    })
  }, [apps])

  // Duyệt hàng loạt = gửi cả nhóm sang bàn của Hiring Manager. Tuần tự để một hồ sơ hỏng không
  // kéo đổ cả lô, và đếm riêng số thành công / thất bại.
  const handleBatchAccept = async () => {
    if (selectedIds.length === 0) return
    setBatchProcessing(true)
    setMutationError('')
    setNotice('')
    try {
      let successCount = 0
      let failCount = 0
      for (const appId of selectedIds) {
        try {
          await hiringTeamService.requestHmApproval(appId)
          successCount++
        } catch {
          failCount++
        }
      }
      setNotice(
        t('messages.sentToHiringManagerBatch', { count: successCount }) +
          (failCount > 0 ? ` ${t('messages.rejectedFailed', { count: failCount })}` : '')
      )
      setSelectedIds([])
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || t('messages.acceptError'))
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

  // "Mời" hàng loạt = xếp lịch hàng loạt: thư mời chỉ có nghĩa khi kèm giờ hẹn (ADR-059).
  const handleBatchInvite = (roundNumber: number) => {
    if (selectedIds.length === 0) return
    setMutationError('')
    setNotice('')
    setInviteModalTarget({
      applications: selectedIds.map((appId) => ({
        id: appId,
        name: apps.find((a) => a.id === appId)?.candidateName || t('candidate'),
      })),
      targetRound: roundNumber > 0 ? roundNumber : 1,
    })
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

  /**
   * "Duyệt hồ sơ" ở bước sàng CV = đẩy hồ sơ sang bàn của Hiring Manager (ADR-067).
   *
   * KHÔNG kèm chọn ca nữa, và KHÔNG báo gì cho ứng viên: giờ hẹn chỉ xếp được sau khi HM duyệt
   * và gửi khung giờ họ có mặt được — chọn ca tại đây là chọn một giờ chưa ai xác nhận dự được.
   * Tin chưa gán HM thì server tự đẩy thẳng sang bước chờ xếp lịch.
   */
  const handleAccept = async (appId: string) => {
    setProcessingAppId(appId)
    setMutationError('')
    setNotice('')
    try {
      await hiringTeamService.requestHmApproval(appId)
      setNotice(t('messages.sentToHiringManager'))
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || t('messages.acceptError'))
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

      {/* Đội tuyển dụng + ký duyệt mô tả công việc (ADR-061). Đây là màn tin CỦA CHÍNH mình
          (`/recruiter/my-jobs/:id`) nên chủ tin quản lý được đội; server vẫn kiểm lại. */}
      <div className="mb-6">
        <HiringTeamPanel
          jobPostingId={job.id}
          hmSignOffStatus={job.hmSignOffStatus}
          hmSignOffReason={job.hmSignOffReason}
          canManage
          onChanged={() => void refetchJob()}
        />
      </div>

      {/*
        Khối ứng viên theo lối ATS (thanh bước quy trình → danh sách → hồ sơ + CV).

        Trang này CHỈ truyền dữ liệu và các thao tác nghiệp vụ xuống; luật duyệt/loại/mời lịch vẫn
        nằm nguyên ở đây và ở server. Bố cục nằm trong `CandidatePipeline` để màn tin của HR Leader
        và Hiring Manager dùng lại được — ba bản sao của cùng một bảng là ba chỗ phải nhớ sửa.
      */}
      <CandidatePipeline
        apps={apps}
        rounds={job.roundConfigs || []}
        loading={loadingApps}
        processingAppId={processingAppId}
        onApprove={(a) => handleAccept(a.id)}
        onReject={(a) => void handleReject(a.id)}
        onInvite={(a) => {
          setInviteModalTarget({
            applications: [{ id: a.id, name: a.candidateName || t('candidate') }],
                  targetRound: a.currentRound && a.currentRound > 0 ? a.currentRound : 1,
          })
        }}
        isInvitePending={(a) =>
          inviteModalTarget?.applications.some((x) => x.id === a.id) ?? false
        }
        selectedIds={selectedIds}
        onToggleSelect={(id) =>
          setSelectedIds((prev) =>
            prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
          )
        }
        onToggleSelectAll={(idsOnScreen, allSelected) =>
          setSelectedIds((prev) =>
            allSelected
              ? prev.filter((id) => !idsOnScreen.includes(id))
              : [...prev, ...idsOnScreen.filter((id) => !prev.includes(id))]
          )
        }
        onBatchApprove={handleBatchAccept}
        onBatchReject={() => void handleBatchReject()}
        onBatchInvite={handleBatchInvite}
        batchBusy={batchProcessing}
        candidateHref={(a) => `/recruiter/candidates/${a.id}`}
        statusLabel={appStatusLabel}
        statusBadge={appStatusBadge}
      />

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
                        {t('coverLetterModal.matchScore')}: {formatScore(selectedCoverLetter.matchScore)}
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
          applications={inviteModalTarget.applications}
          jobPostingId={id}
          targetRoundNumber={inviteModalTarget.targetRound}
          onClose={() => setInviteModalTarget(null)}
          onSuccess={(msg) => {
            setNotice(msg)
            setSelectedIds([])
            void load()
          }}
        />
      )}
    </div>
  )
}
