import { useState, useEffect, useCallback } from 'react'
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
  CheckCircle2,
  XCircle,
  AlertCircle,
  X,
  ScrollText,
} from 'lucide-react'
import jobService from '@ari/shared/fservices/job'
import { applicationService } from '@ari/shared/fservices/application'
import { hiringTeamService } from '@ari/shared/fservices/hiringTeam'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import InviteAndScheduleModal from '../../components/InviteAndScheduleModal'
import CandidatePipeline from '@/components/jobCandidates/CandidatePipeline'
import { STAFF_NOTIF_REFRESH_EVENT } from '@ari/shared/fservices/notification/notificationService'
import type { JobPosting } from '@ari/shared/types/job'
import type { HrApplicationItem } from '@ari/shared/types/application'
import { appStatusLabel } from '../recruiter/_jobUi'
import HiringTeamPanel from '@/components/hiring/HiringTeamPanel'

/**
 * Trạng thái mà nút Duyệt / Loại ở vòng CV còn thao tác được — giống hệt màn Recruiter.
 * `hm_review` bắt buộc có: cổng duyệt của Hiring Manager (ADR-061) không đổi trạng thái hồ sơ,
 * nên thiếu nó là hồ sơ kẹt ở cổng đã mở, không còn nút nào gọi được `AcceptApplicationCommand`.
 */

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

  // Duyệt hàng loạt = gửi cả nhóm sang bàn của Hiring Manager (ADR-067). Tuần tự để một hồ sơ
  // hỏng không kéo đổ cả lô, và đếm riêng số thành công / thất bại.
  const handleBatchAccept = async () => {
    if (selectedIds.length === 0) return
    setBatchProcessing(true)
    setActionError(null)
    setNotice(null)
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
      setNotice(t('notices.sentToHiringManagerBatch', { success: successCount, failed: failCount }))
      setSelectedIds([])
      await loadApps()
    } catch {
      setActionError(t('errors.acceptApplication'))
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

  // "Mời" hàng loạt = xếp lịch hàng loạt: thư mời chỉ có nghĩa khi kèm giờ hẹn (ADR-059).
  const handleBatchInvite = (roundNumber: number) => {
    if (selectedIds.length === 0) return
    setActionError(null)
    setNotice(null)
    setInviteModalTarget({
      applications: selectedIds.map((appId) => ({
        id: appId,
        name: apps.find((a) => a.id === appId)?.candidateName || t('candidate'),
      })),
      targetRound: roundNumber > 0 ? roundNumber : 1,
    })
  }

  /**
   * "Duyệt hồ sơ" ở bước sàng CV = đẩy hồ sơ sang bàn của Hiring Manager (ADR-067).
   *
   * KHÔNG kèm chọn ca nữa, và KHÔNG báo gì cho ứng viên: giờ hẹn chỉ xếp được sau khi HM duyệt và
   * gửi khung giờ họ có mặt được. Tin chưa gán HM thì server tự đẩy thẳng sang bước chờ xếp lịch.
   */
  const handleAccept = async (appId: string) => {
    setProcessingAppId(appId)
    setActionError(null)
    setNotice(null)
    try {
      await hiringTeamService.requestHmApproval(appId)
      setNotice(t('notices.sentToHiringManager'))
      await loadApps()
    } catch (err) {
      const e = err as { response?: { data?: { message?: string } } }
      setActionError(e?.response?.data?.message || t('errors.acceptApplication'))
    } finally {
      setProcessingAppId(null)
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

  /**
   * Bỏ chọn khi danh sách hồ sơ đổi (vừa duyệt/loại xong). Trả lại CHÍNH `prev` khi không bỏ ai —
   * `filter` luôn sinh mảng mới, mà mảng mới là một lần render nữa: đủ để thành vòng lặp vô hạn.
   */
  useEffect(() => {
    setSelectedIds((prev) => {
      const next = prev.filter((cid) => apps.some((a) => a.id === cid))
      return next.length === prev.length ? prev : next
    })
  }, [apps])

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
      // Ưu tiên thông báo của server: nó nói rõ vì sao không duyệt được (vd ngân hàng đề trắc
      // nghiệm chưa đủ câu), còn chuỗi mặc định chỉ nói "duyệt thất bại".
      const e = err as { response?: { data?: { message?: string } } }
      setActionError(e?.response?.data?.message || t('errors.approveJob'))
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
              onClick={() => navigate(`/hr/jobs/${job.id}/schedule`)}
              className="flex items-center justify-center gap-2 px-5 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 font-medium hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
            >
              <Calendar className="w-4 h-4 text-brand-600 dark:text-brand-400" /> Lịch phỏng vấn
            </button>
            {job.roundConfigs?.some((r) => (r.roundType || '').toLowerCase() === 'online_test') && (
              <button
                type="button"
                onClick={() => navigate(`/hr/jobs/${job.id}/online-test`)}
                className="flex items-center justify-center gap-2 px-5 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 font-medium hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
              >
                <ScrollText className="w-4 h-4" /> {t('onlineTestBank')}
              </button>
            )}
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
                  {/* Đã duyệt → mở bản JD đã đóng dấu (có chữ ký số); chưa duyệt → mở file gốc. */}
                  <button
                    type="button"
                    onClick={() =>
                      openDocument(
                        job.signedJdFileUrl || job.jdFileUrl!,
                        job.jdFileName || `${job.title} - JD`,
                      )
                    }
                    className={`inline-flex items-center gap-2 rounded-xl border px-4 py-2.5 text-sm font-medium ${
                      job.signedJdFileUrl
                        ? 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-400 dark:hover:bg-emerald-500/20'
                        : 'border-ink-200 bg-white text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:bg-white/5 dark:text-ink-200 dark:hover:bg-white/10'
                    }`}
                  >
                    <FileText
                      className={`h-4 w-4 ${job.signedJdFileUrl ? '' : 'text-brand-600 dark:text-brand-400'}`}
                    />
                    {job.jdFileName || t('jdSection.originalJd')}
                    {job.signedJdFileUrl && (
                      <span className="rounded-full bg-emerald-100 px-1.5 py-0.5 text-[10px] font-semibold text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300">
                        {t('jdSection.signedBadge')}
                      </span>
                    )}
                    {job.jdFileFormat && (
                      <span className="text-xs uppercase text-ink-400">{job.jdFileFormat}</span>
                    )}
                  </button>
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

            {/* Đội tuyển dụng + ký duyệt mô tả công việc (ADR-061). HR Admin quản lý được đội của
                mọi tin, nên canManage cố định true ở khu vực này. */}
            <HiringTeamPanel
              jobPostingId={job.id}
              hmSignOffStatus={job.hmSignOffStatus}
              hmSignOffReason={job.hmSignOffReason}
              canManage
              onChanged={() => void load()}
            />
          </div>
        </div>

        <div className="mt-12 space-y-6">
          <div className="flex items-center justify-between border-b border-ink-200 pb-4 dark:border-white/10">
            <div>
              <h2 className="flex items-center gap-2 text-2xl font-bold text-ink-900 dark:text-white">
                <Target className="h-6 w-6 text-brand-600 dark:text-brand-400" /> {t('funnel.title')}
              </h2>
              <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">{t('funnel.description')}</p>
            </div>
            <span className="rounded-full bg-brand-100 px-3 py-1 text-xs font-semibold text-brand-700 dark:bg-brand-500/20 dark:text-brand-400">
              {t('funnel.total', { count: apps.length })}
            </span>
          </div>

          {/*
            Cùng khối quy trình với màn tin của Recruiter và Hiring Manager. Ba màn nhìn cùng một
            phễu là điều kiện để ba vai trò bàn về cùng một bức tranh — và là một chỗ để sửa thay vì
            ba (bài học gộp hai màn Phỏng vấn ở ADR-058).

            HR Leader có đủ thao tác vận hành ở đây vì họ là người chốt dự phòng khi tin chưa gán
            Hiring Manager (ADR-061); server vẫn kiểm lại từng lệnh.
          */}
          <CandidatePipeline
            apps={apps}
            rounds={job.roundConfigs || []}
            loading={loadingApps}
            processingAppId={processingAppId}
            onApprove={(a) => handleAccept(a.id)}
            onReject={(a) => void handleReject(a.id)}
            onInvite={(a) =>
              setInviteModalTarget({
                applications: [{ id: a.id, name: a.candidateName || t('candidate') }],
                          targetRound: a.currentRound && a.currentRound > 0 ? a.currentRound : 1,
              })
            }
            isInvitePending={(a) =>
              inviteModalTarget?.applications.some((x) => x.id === a.id) ?? false
            }
            candidateHref={(a) => `/hr/candidates/${a.id}`}
            statusLabel={appStatusLabel}
            selectedIds={selectedIds}
            onToggleSelect={(cid) =>
              setSelectedIds((prev) =>
                prev.includes(cid) ? prev.filter((x) => x !== cid) : [...prev, cid]
              )
            }
            onToggleSelectAll={(idsOnScreen, allSelected) =>
              setSelectedIds((prev) =>
                allSelected
                  ? prev.filter((cid) => !idsOnScreen.includes(cid))
                  : [...prev, ...idsOnScreen.filter((cid) => !prev.includes(cid))]
              )
            }
            onBatchApprove={handleBatchAccept}
            onBatchReject={() => void handleBatchReject()}
            onBatchInvite={handleBatchInvite}
            batchBusy={batchProcessing}
          />
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

      {inviteModalTarget && id && (
        <InviteAndScheduleModal
          applications={inviteModalTarget.applications}
          jobPostingId={id}
          targetRoundNumber={inviteModalTarget.targetRound}
          onClose={() => setInviteModalTarget(null)}
          onSuccess={(msg) => {
            setNotice(msg)
            setSelectedIds([])
            void loadApps()
          }}
        />
      )}
    </div>
  )
}
