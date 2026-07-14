import { useEffect, useMemo, useState, useCallback } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
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
import { ErrorAlert, Pagination } from '@components/shared'
import { useDocumentViewer } from '@components/document/DocumentViewer'
import jobService from '@services/job/jobService'
import { applicationService } from '@services/application/applicationService'
import type { JobPosting } from '@/types/job'
import type { HrApplicationItem } from '@/types/application'
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
import { resolveAssetUrl } from '@/config/constants'

function getDeadlineText(deadlineStr?: string | null): string {
  if (!deadlineStr) return ''
  const d = new Date(deadlineStr)
  if (Number.isNaN(d.getTime())) return ''
  const formattedDate = d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const target = new Date(d)
  target.setHours(0, 0, 0, 0)
  const diffDays = Math.round((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))
  if (diffDays < 0) return `${formattedDate} (Đã hết hạn)`
  if (diffDays === 0) return `${formattedDate} (Hết hạn hôm nay)`
  return `${formattedDate} (Còn ${diffDays} ngày)`
}

// Chuyển date sang YYYY-MM-DD theo múi giờ LOCAL (tránh lỗi UTC +/-7)
function toLocalDateStr(d: string | Date | number | undefined | null): string {
  if (!d) return ''
  const dt = new Date(d)
  if (isNaN(dt.getTime())) return ''
  const yyyy = dt.getFullYear()
  const mm = String(dt.getMonth() + 1).padStart(2, '0')
  const dd = String(dt.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

const FUNNEL: { key: string; label: string }[] = [
  { key: 'cv_submitted', label: 'Mới ứng tuyển' },
  { key: 'screening', label: 'Đang sơ loại' },
  { key: 'interview', label: 'Phỏng vấn' },
  { key: 'pass', label: 'Đạt' },
]

const matchColor = (s?: number | null) =>
  s == null
    ? 'text-ink-400'
    : s >= 75
      ? 'text-emerald-600 dark:text-emerald-400'
      : s >= 50
        ? 'text-amber-600 dark:text-amber-400'
        : 'text-red-600 dark:text-red-400'

export default function RecruiterJobDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { openDocument } = useDocumentViewer()
  const { data: job, isLoading: loadingJob, error: jobError } = useQuery({
    queryKey: ['job', id],
    queryFn: () => jobService.getJobPostingById(id!),
    enabled: !!id,
    retry: false
  })

  const { data: appsData, isLoading: loadingApps, refetch: refetchApps } = useQuery({
    queryKey: ['job', id, 'applications'],
    queryFn: () => jobService.getJobApplications(id!).catch(() => [] as HrApplicationItem[]),
    enabled: !!id,
  })

  const apps = appsData || []
  const loading = loadingJob || loadingApps
  
  // Keep error state for mutations like status change
  const [mutationError, setMutationError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [invitingId, setInvitingId] = useState<string | null>(null)
  const [processingAppId, setProcessingAppId] = useState<string | null>(null)
  const [page, setPage] = useState(1)

  const error = mutationError || (jobError as any)?.response?.data?.message || (jobError ? 'Không tải được chi tiết tin tuyển dụng.' : '')

  const load = refetchApps

  const funnel = useMemo(() => {
    const by = (s: string) => apps.filter((a) => a.status === s).length
    return FUNNEL.map((f) => ({ ...f, count: by(f.key) }))
  }, [apps])

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

  // Trạng thái lọc & sắp xếp cho Duyệt Hồ Sơ
  const [cvDateFilter, setCvDateFilter] = useState<string>('')
  const [cvStatusFilter, setCvStatusFilter] = useState<string>('all')
  const [cvSortOrder, setCvSortOrder] = useState<string>('desc') // desc, asc, default

  // Trạng thái lọc & sắp xếp cho Vòng phỏng vấn
  const [interviewDateFilter, setInterviewDateFilter] = useState<string>('')
  const [interviewStatusFilter, setInterviewStatusFilter] = useState<string>('all')
  const [interviewSortOrder, setInterviewSortOrder] = useState<string>('desc') // desc, asc, default

  // Trạng thái chọn hàng loạt
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [batchProcessing, setBatchProcessing] = useState<boolean>(false)

  const activeRoundNumber = useMemo(() => {
    return activeTab.startsWith('round_') ? parseInt(activeTab.replace('round_', ''), 10) : 0
  }, [activeTab])

  const tabs = useMemo(() => {
    if (!job) return []

    // Vòng duyệt hồ sơ (chưa qua vòng nào)
    const cvReviewCount = apps.filter(
      (a) => !a.currentRound || a.currentRound === 0
    ).length

    const roundTabs = (job.roundConfigs || [])
      .slice()
      .sort((a, b) => a.roundNumber - b.roundNumber)
      .map((rc) => {
        const count = apps.filter((a) => a.currentRound === rc.roundNumber).length
        return {
          id: `round_${rc.roundNumber}`,
          label: `Vòng ${rc.roundNumber}`,
          count,
        }
      })

    return [
      { id: 'cv_review', label: 'Duyệt Hồ Sơ', count: cvReviewCount },
      ...roundTabs,
    ]
  }, [job, apps])

  const activeCandidates = useMemo(() => {
    if (activeTab === 'cv_review') {
      return apps.filter((a) => !a.currentRound || a.currentRound === 0)
    }

    if (activeTab.startsWith('round_')) {
      const roundNumber = parseInt(activeTab.replace('round_', ''), 10)
      return apps.filter((a) => a.currentRound === roundNumber)
    }

    return apps
  }, [apps, activeTab])

  const processedApps = useMemo(() => {
    let result = [...activeCandidates]

    if (activeTab === 'cv_review') {
      // 1. Lọc theo ngày nộp (so sánh theo múi giờ local)
      if (cvDateFilter) {
        result = result.filter((a) => toLocalDateStr(a.createdAt) === cvDateFilter)
      }
      // 2. Lọc theo trạng thái
      if (cvStatusFilter && cvStatusFilter !== 'all') {
        result = result.filter((a) => a.status === cvStatusFilter)
      }
      // 3. Sắp xếp theo độ phù hợp
      if (cvSortOrder === 'desc') {
        result.sort((a, b) => (b.matchScore ?? 0) - (a.matchScore ?? 0))
      } else if (cvSortOrder === 'asc') {
        result.sort((a, b) => (a.matchScore ?? 0) - (b.matchScore ?? 0))
      }
    } else {
      // 1. Lọc theo ngày thi (so sánh theo múi giờ local)
      if (interviewDateFilter) {
        result = result.filter((a) => toLocalDateStr(a.interviewDate) === interviewDateFilter)
      }
      // 2. Lọc theo trạng thái
      if (interviewStatusFilter && interviewStatusFilter !== 'all') {
        result = result.filter((a) => a.status === interviewStatusFilter)
      }
      // 3. Sắp xếp theo điểm đánh giá
      if (interviewSortOrder === 'desc') {
        result.sort((a, b) => (b.interviewScore ?? 0) - (a.interviewScore ?? 0))
      } else if (interviewSortOrder === 'asc') {
        result.sort((a, b) => (a.interviewScore ?? 0) - (b.interviewScore ?? 0))
      }
    }

    return result
  }, [activeCandidates, activeTab, cvDateFilter, cvStatusFilter, cvSortOrder, interviewDateFilter, interviewStatusFilter, interviewSortOrder])

  const isSelectable = useCallback((a: HrApplicationItem) => {
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
  }, [activeTab, activeRoundNumber])

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(processedApps.length / PAGE_SIZE))
  const pagedApps = useMemo(
    () => processedApps.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [processedApps, page]
  )

  const pageSelectableCandidates = useMemo(() => {
    return pagedApps.filter(isSelectable)
  }, [pagedApps, isSelectable])

  const isAllSelected = useMemo(() => {
    if (pageSelectableCandidates.length === 0) return false
    return pageSelectableCandidates.every((c) => selectedIds.includes(c.id))
  }, [pageSelectableCandidates, selectedIds])

  const handleSelectAll = useCallback(() => {
    if (isAllSelected) {
      setSelectedIds((prev) => prev.filter((id) => !pageSelectableCandidates.some((c) => c.id === id)))
    } else {
      setSelectedIds((prev) => {
        const newIds = pageSelectableCandidates.map((c) => c.id).filter((id) => !prev.includes(id))
        return [...prev, ...newIds]
      })
    }
  }, [isAllSelected, pageSelectableCandidates])

  // Reset page when tab changes
  useEffect(() => {
    setPage(1)
  }, [activeTab])

  // Kẹp trang khi danh sách ứng viên thay đổi
  useEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])

  useEffect(() => {
    setSelectedIds([])
  }, [activeTab, cvDateFilter, cvStatusFilter, cvSortOrder, interviewDateFilter, interviewStatusFilter, interviewSortOrder])

  const handleBatchAccept = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(`Bạn có chắc chắn muốn duyệt ${selectedIds.length} hồ sơ đã chọn và chuyển sang Vòng 1?`)) return
    setBatchProcessing(true)
    setMutationError('')
    setNotice('')
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
      setNotice(`Đã duyệt thành công ${successCount} hồ sơ.${failCount > 0 ? ` Thất bại ${failCount} hồ sơ.` : ''}`)
      setSelectedIds([])
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || 'Lỗi khi duyệt hàng loạt.')
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleBatchReject = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(`Bạn có chắc chắn muốn từ chối ${selectedIds.length} hồ sơ đã chọn và gửi thư cảm ơn?`)) return
    setBatchProcessing(true)
    setMutationError('')
    setNotice('')
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
      setNotice(`Đã từ chối thành công ${successCount} hồ sơ.${failCount > 0 ? ` Thất bại ${failCount} hồ sơ.` : ''}`)
      setSelectedIds([])
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || 'Lỗi khi từ chối hàng loạt.')
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleBatchInvite = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(`Bạn có chắc chắn muốn gửi lời mời phỏng vấn cho ${selectedIds.length} ứng viên đã chọn?`)) return
    setBatchProcessing(true)
    setMutationError('')
    setNotice('')
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
      setNotice(`Đã gửi lời mời thành công cho ${successCount} ứng viên.${failCount > 0 ? ` Thất bại ${failCount} ứng viên.` : ''}`)
      setSelectedIds([])
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || 'Lỗi khi gửi lời mời hàng loạt.')
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
          ? 'Đã gửi tin cho HR Leader duyệt.'
          : status === 'closed'
            ? 'Đã đóng tin tuyển dụng.'
            : 'Đã cập nhật trạng thái.'
      )
      await load()
    } catch (e: any) {
      setMutationError(e?.response?.data?.message || 'Không thể cập nhật trạng thái tin.')
    } finally {
      setBusy(false)
    }
  }

  const sendInvite = async (appId: string) => {
    setInvitingId(appId)
    setMutationError('')
    setNotice('')
    try {
      await applicationService.sendInvite(appId)
      setNotice('Đã gửi lời mời (magic link) cho ứng viên.')
    } catch (e: any) {
      setMutationError(e?.response?.data?.message || 'Không thể gửi lời mời.')
    } finally {
      setInvitingId(null)
    }
  }

  const handleAccept = async (appId: string) => {
    setProcessingAppId(appId)
    setMutationError('')
    setNotice('')
    try {
      await applicationService.acceptApplication(appId)
      setNotice('Đã duyệt hồ sơ ứng viên và chuyển sang Vòng 1 thành công.')
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || 'Lỗi khi duyệt hồ sơ.')
    } finally {
      setProcessingAppId(null)
    }
  }

  const handleReject = async (appId: string) => {
    if (!window.confirm('Bạn có chắc chắn muốn từ chối hồ sơ ứng viên này và gửi thư cảm ơn?')) return
    setProcessingAppId(appId)
    setMutationError('')
    setNotice('')
    try {
      await applicationService.rejectApplication(appId)
      setNotice('Đã từ chối hồ sơ và gửi email cảm ơn ứng viên thành công.')
      await load()
    } catch (err: any) {
      setMutationError(err?.response?.data?.message || 'Lỗi khi từ chối hồ sơ.')
    } finally {
      setProcessingAppId(null)
    }
  }

  if (loading) return <JobDetailSkeleton />
  if (!job) {
    return (
      <div className="p-6 lg:p-8">
        <ErrorAlert message={error || 'Không tìm thấy tin tuyển dụng.'} />
        <Link
          to="/recruiter/my-jobs"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← Quay lại danh sách
        </Link>
      </div>
    )
  }

  const canSubmit = job.status === 'draft' || job.status === 'rejected'
  const canClose = job.status === 'active'
  const canEdit = job.status !== 'archived'

  return (
    <div className="p-6 lg:p-8">
      <Link
        to="/recruiter/my-jobs"
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> Quay lại danh sách
      </Link>

      {error && <ErrorAlert message={error} onDismiss={() => setMutationError('')} />}
      {notice && (
        <div className="mb-6 flex items-start justify-between gap-3 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
          <span className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4" /> {notice}
          </span>
          <button onClick={() => setNotice('')} className="hover:opacity-70">
            Đóng
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
                  className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${jobStatusBadge(job.status)}`}
                >
                  {jobStatusLabel(job.status)}
                </span>
              </div>
              <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
                <span>{job.department || 'Chưa phân phòng ban'}</span>
                {job.location ? (
                  <span className="flex items-center gap-1">
                    <MapPin className="h-3.5 w-3.5" />
                    {job.location}
                  </span>
                ) : null}
                <span>{formatSalary(job)}</span>
                {job.vacancies != null && job.vacancies > 0 && (
                  <span
                    className={`flex items-center gap-1 font-medium ${isFull ? 'text-emerald-600 dark:text-emerald-400' : 'text-ink-600 dark:text-ink-300'}`}
                  >
                    <Target className="h-3.5 w-3.5" /> Tuyển {hired}/{job.vacancies}
                  </span>
                )}
                {job.applicationDeadline && (
                  <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400 font-medium">
                    <CalendarClock className="h-3.5 w-3.5" />
                    Hạn nộp: {getDeadlineText(job.applicationDeadline)}
                  </span>
                )}
                <span className="text-ink-400">· tạo {timeAgo(job.createdAt)}</span>
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {job.jdFileUrl && (
              <button
                type="button"
                onClick={() => openDocument(job.jdFileUrl!, job.jdFileName || `${job.title} - JD`)}
                className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
              >
                <FileText className="h-4 w-4" /> JD gốc
              </button>
            )}
            <Link
              to={`/recruiter/my-jobs/${job.id}/schedule`}
              className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
            >
              <CalendarClock className="h-4 w-4" /> Lịch phỏng vấn
            </Link>
            {canEdit && (
              <Link
                to={`/recruiter/my-jobs/${job.id}/edit`}
                className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
              >
                <Pencil className="h-4 w-4" /> Sửa tin
              </Link>
            )}
            {canSubmit && (
              <button
                onClick={() => changeStatus('pending')}
                disabled={busy}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3.5 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}{' '}
                Gửi HR duyệt
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
                Đóng tin
              </button>
            )}
          </div>
        </div>

        {job.status === 'rejected' && job.rejectionReason && (
          <div className="mt-4 flex items-start gap-2 rounded-xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-400">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              <b>HR từ chối:</b> {job.rejectionReason}. Sửa tin và gửi duyệt lại.
            </span>
          </div>
        )}

        {isFull && canClose && (
          <div className="mt-4 flex items-center justify-between gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-400">
            <span className="flex items-center gap-2">
              <Target className="h-4 w-4" /> Đã tuyển đủ chỉ tiêu ({hired}/{job.vacancies}) — cân
              nhắc đóng tin.
            </span>
            <button
              onClick={() => changeStatus('closed')}
              disabled={busy}
              className="rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              Đóng tin
            </button>
          </div>
        )}
      </motion.div>

      {/* Phễu ứng viên */}
      <div className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-5">
        <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
          <span className="flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
            <Users className="h-4 w-4" /> Tổng UV
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

      {/* Cấu hình vòng phỏng vấn */}
      {job.roundConfigs?.length > 0 && (
        <div className="mb-6 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
          <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
            <Layers className="h-4 w-4 text-ai-600 dark:text-ai-400" /> Vòng phỏng vấn (
            {job.roundConfigs.length})
          </h2>
          <div className="flex flex-wrap gap-2">
            {job.roundConfigs.map((r) => (
              <span
                key={r.roundNumber}
                className="rounded-lg border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 px-3 py-1.5 text-xs text-ink-700 dark:text-ink-200"
              >
                Vòng {r.roundNumber}: {r.roundType === 'technical' ? 'Chuyên môn' : 'Sơ loại'} ·{' '}
                {r.maxDurationMinutes}′
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Danh sách ứng viên của job */}
      <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
        <div className="flex items-center justify-between border-b border-ink-100 dark:border-white/10 px-5 py-4">
          <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
            <Users className="h-5 w-5 text-brand-600 dark:text-brand-400" /> Ứng viên ({apps.length})
          </h2>
        </div>

        {apps.length > 0 && (
          <div className="border-b border-ink-100 dark:border-white/10 px-5 py-4">
            <div className="flex flex-wrap gap-2">
              {tabs.map((tab) => (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all duration-200 ${
                    activeTab === tab.id
                      ? 'bg-brand-600 text-white shadow-sm'
                      : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'
                  }`}
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
            <p className="text-sm text-ink-500 dark:text-ink-400">
              Chưa có ứng viên nào cho tin này.
            </p>
          </div>
        ) : (
          <>
            {/* Bộ lọc và sắp xếp */}
            <div className="mx-5 mb-6 flex flex-wrap items-end justify-between gap-4 p-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
              <div className="flex flex-wrap items-center gap-4">
                {/* Lọc theo ngày */}
                <div className="flex flex-col gap-1">
                  <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                    {activeTab === 'cv_review' ? 'Lọc theo ngày nộp' : 'Lọc theo ngày thi'}
                  </span>
                  <input
                    type="date"
                    value={activeTab === 'cv_review' ? cvDateFilter : interviewDateFilter}
                    onChange={(e) => {
                      if (activeTab === 'cv_review') {
                        setCvDateFilter(e.target.value)
                      } else {
                        setInterviewDateFilter(e.target.value)
                      }
                    }}
                    className="px-3 py-1.5 text-xs rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 text-ink-900 dark:text-white focus:outline-none focus:border-brand-500"
                  />
                </div>

                {/* Lọc theo trạng thái */}
                <div className="flex flex-col gap-1">
                  <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                    Lọc theo trạng thái
                  </span>
                  <select
                    value={activeTab === 'cv_review' ? cvStatusFilter : interviewStatusFilter}
                    onChange={(e) => {
                      if (activeTab === 'cv_review') {
                        setCvStatusFilter(e.target.value)
                      } else {
                        setInterviewStatusFilter(e.target.value)
                      }
                    }}
                    className="px-3 py-1.5 text-xs rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 text-ink-900 dark:text-white focus:outline-none focus:border-brand-500"
                  >
                    <option value="all">Tất cả trạng thái</option>
                    {activeTab === 'cv_review' ? (
                      <>
                        {/* cv_submitted: nộp qua job board | invited: được mời, chưa nộp CV */}
                        <option value="cv_submitted">Mới ứng tuyển</option>
                        <option value="invited">Được mời (chưa nộp CV)</option>
                        <option value="cv_rejected">CV bị từ chối</option>
                      </>
                    ) : (
                      <>
                        {/* Luồng: acceptApplication → screening → (book slot) → interview → pass/not_pass */}
                        <option value="invited">Chờ gửi lời mời</option>
                        <option value="screening">Đã mời, chờ đặt lịch</option>
                        <option value="interview">Đã đặt lịch / Đang phỏng vấn</option>
                        <option value="pass">Đạt</option>
                        <option value="not_pass">Không đạt</option>
                        <option value="withdrawn">Đã rút</option>
                      </>
                    )}
                  </select>
                </div>
              </div>

              {/* Sắp xếp */}
              <div className="flex flex-col gap-1">
                <span className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">
                  {activeTab === 'cv_review' ? 'Sắp xếp theo độ phù hợp' : 'Sắp xếp theo điểm đánh giá'}
                </span>
                <select
                  value={activeTab === 'cv_review' ? cvSortOrder : interviewSortOrder}
                  onChange={(e) => {
                    if (activeTab === 'cv_review') {
                      setCvSortOrder(e.target.value)
                    } else {
                      setInterviewSortOrder(e.target.value)
                    }
                  }}
                  className="px-3 py-1.5 text-xs rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 text-ink-900 dark:text-white focus:outline-none focus:border-brand-500"
                >
                  <option value="desc">Giảm dần (Cao nhất trước)</option>
                  <option value="asc">Tăng dần (Thấp nhất trước)</option>
                  <option value="default">Mặc định (Thời gian nộp)</option>
                </select>
              </div>
            </div>

            {/* Thanh hành động hàng loạt (Batch Actions Bar) */}
            {selectedIds.length > 0 && (
              <div className="mx-5 flex items-center justify-between p-4 mb-4 rounded-xl border border-brand-200 dark:border-brand-500/30 bg-brand-50/50 dark:bg-brand-500/10 backdrop-blur-sm animate-fade-in">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-semibold text-brand-900 dark:text-brand-400">
                    Đã chọn {selectedIds.length} ứng viên
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
                        Duyệt hàng loạt
                      </button>
                      <button
                        disabled={batchProcessing}
                        onClick={handleBatchReject}
                        className="flex items-center gap-1.5 px-4 py-2 rounded-xl border border-red-200 dark:border-red-500/30 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 text-xs font-semibold transition-all disabled:opacity-50"
                      >
                        <X className="w-3.5 h-3.5" />
                        Từ chối hàng loạt
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
                      Mời phỏng vấn hàng loạt
                    </button>
                  )}
                </div>
              </div>
            )}

            {processedApps.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-12 text-center">
                <Users className="h-8 w-8 text-ink-300" />
                <p className="text-sm text-ink-500 dark:text-ink-400">
                  Không tìm thấy ứng viên nào phù hợp với bộ lọc.
                </p>
              </div>
            ) : (
              <>
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
                    <div>Ứng viên</div>
                    <div className="text-center">Ngày nộp</div>
                    <div className="text-center">Báo trước</div>
                    <div className="text-center">Độ Phù Hợp</div>
                    <div className="text-center">Trạng Thái</div>
                    <div className="text-center">Hành động</div>
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
                    <div>Ứng viên</div>
                    <div className="text-center">Lịch thi/phỏng vấn</div>
                    <div className="text-center">Điểm Đánh Giá</div>
                    <div className="text-center">Trạng Thái</div>
                    <div className="text-center">Hành động</div>
                  </div>
                )}

                <div className="divide-y divide-ink-100 dark:divide-white/10">
                  {pagedApps.map((a) => (
                    <div
                      key={a.id}
                      className={`grid items-center gap-4 px-5 py-3 hover:bg-ink-50/50 dark:hover:bg-white-[0.02] transition-colors ${
                        activeTab === 'cv_review'
                          ? 'grid-cols-[40px_minmax(180px,1.5fr)_110px_110px_100px_120px_230px]'
                          : 'grid-cols-[40px_minmax(180px,1.5fr)_180px_110px_120px_230px]'
                      }`}
                    >
                      {/* Checkbox chọn hàng loạt */}
                      <div className="flex items-center justify-center">
                        <input
                          type="checkbox"
                          checked={selectedIds.includes(a.id)}
                          disabled={!isSelectable(a) || batchProcessing}
                          onChange={(e) => {
                            if (e.target.checked) {
                              setSelectedIds((prev) => [...prev, a.id])
                            } else {
                              setSelectedIds((prev) => prev.filter((id) => id !== a.id))
                            }
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
                            {a.candidateName || 'Ứng viên'}
                          </h4>
                          <p className="flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400 mt-0.5">
                            <Mail className="h-3 w-3" /> {a.candidateEmail}
                          </p>
                        </div>
                      </Link>

                      {/* Cột 2 & 3 thay đổi tùy theo tab */}
                      {activeTab === 'cv_review' ? (
                        <>
                          {/* Ngày nộp */}
                          <div className="text-center text-xs text-ink-600 dark:text-ink-300">
                            {new Date(a.createdAt).toLocaleDateString('vi-VN')}
                          </div>

                          {/* Báo trước */}
                          <div className="text-center text-xs text-ink-600 dark:text-ink-300 truncate" title={a.noticePeriod || undefined}>
                            {a.noticePeriod || '—'}
                          </div>

                          {/* Độ phù hợp */}
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
                          {/* Lịch thi/phỏng vấn */}
                          <div className="text-center text-xs text-ink-600 dark:text-ink-300">
                            {a.interviewDate ? (
                              new Date(a.interviewDate).toLocaleString('vi-VN', {
                                dateStyle: 'short',
                                timeStyle: 'short',
                              })
                            ) : (
                              <span className="text-ink-400 italic">Chưa đặt lịch</span>
                            )}
                          </div>

                          {/* Điểm đánh giá */}
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
                        <div className="w-36 flex gap-2 shrink-0 justify-center">
                          {(!a.currentRound || a.currentRound === 0) ? (
                            <>
                              <button
                                disabled={processingAppId != null || (a.status !== 'cv_submitted' && a.status !== 'invited') || batchProcessing}
                                onClick={() => handleAccept(a.id)}
                                title="Duyệt hồ sơ & Mời phỏng vấn"
                                className="flex flex-1 items-center justify-center gap-1 py-1.5 rounded-lg bg-emerald-600 text-white text-xs font-semibold hover:bg-emerald-500 transition-colors disabled:opacity-50 disabled:cursor-not-allowed shadow-sm whitespace-nowrap"
                              >
                                {processingAppId === a.id ? (
                                  <Loader2 className="w-3 h-3 animate-spin" />
                                ) : (
                                  <Check className="w-3 h-3" />
                                )}
                                Duyệt
                              </button>
                              <button
                                disabled={processingAppId != null || (a.status !== 'cv_submitted' && a.status !== 'invited') || batchProcessing}
                                onClick={() => handleReject(a.id)}
                                title="Từ chối hồ sơ"
                                className="flex flex-1 items-center justify-center gap-1 py-1.5 rounded-lg border border-red-200 dark:border-red-500/30 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-500/10 text-xs font-semibold transition-colors disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
                              >
                                <X className="w-3 h-3" />
                                Từ chối
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
                                (a.currentRound != null && activeRoundNumber > 0 && a.currentRound > activeRoundNumber) ||
                                batchProcessing
                              }
                              className="flex w-full items-center justify-center gap-1 py-1.5 text-xs font-semibold text-white bg-brand-600 hover:bg-brand-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed shadow-sm transition-colors whitespace-nowrap"
                            >
                              {invitingId === a.id ? (
                                <Loader2 className="w-3 h-3 animate-spin" />
                              ) : (
                                <Send className="w-3 h-3" />
                              )}
                              Mời
                            </button>
                          )}
                        </div>
                        
                        {a.cvFileUrl ? (
                          <button
                            type="button"
                            onClick={() =>
                              openDocument(resolveAssetUrl(a.cvFileUrl!), `${a.candidateName || 'Ứng viên'} - CV`)
                            }
                            className="p-2 text-ink-400 hover:text-brand-600 hover:bg-ink-100 dark:hover:bg-white/10 rounded-lg transition-colors shrink-0"
                            title="Xem CV"
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
                            className="p-2 text-ink-400 hover:text-brand-600 hover:bg-ink-100 dark:hover:bg-white/10 rounded-lg transition-colors shrink-0"
                            title="Xem thông tin ứng tuyển & Thư giới thiệu"
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
          label="ứng viên"
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
                Thông tin hồ sơ - {selectedCoverLetter.candidateName}
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
                      <Sparkles className="h-3.5 w-3.5 text-ai-500 animate-pulse" /> Tóm tắt CV & Đánh giá AI
                    </span>
                    {selectedCoverLetter.matchScore != null && (
                      <span className="rounded-full bg-ai-100 px-2 py-0.5 text-xs font-bold text-ai-800 dark:bg-ai-500/30 dark:text-ai-300">
                        Độ phù hợp: {selectedCoverLetter.matchScore}%
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
                    <Sparkles className="h-3.5 w-3.5 text-amber-500" /> Không có dữ liệu đánh giá AI
                  </span>
                  <p className="mt-1 text-xs text-amber-900 dark:text-amber-100 leading-relaxed">
                    Hồ sơ này chưa có kết quả đánh giá AI (do là hồ sơ thử nghiệm hoặc không có tệp CV hợp lệ).
                  </p>
                </div>
              )}

              {/* 2. Thông tin liên hệ */}
              <div className="rounded-xl border border-ink-100 bg-ink-50/40 p-4 dark:border-white/10 dark:bg-white/5">
                <h4 className="text-xs font-semibold text-ink-500 dark:text-ink-400 uppercase tracking-wider mb-2">
                  Thông tin liên hệ
                </h4>
                <div className="grid grid-cols-2 gap-3 text-xs">
                  <div>
                    <span className="text-ink-400 block mb-0.5">Họ và tên</span>
                    <span className="font-medium text-ink-950 dark:text-white">{selectedCoverLetter.candidateName}</span>
                  </div>
                  <div>
                    <span className="text-ink-400 block mb-0.5">Số điện thoại</span>
                    <span className="font-medium text-ink-950 dark:text-white">{selectedCoverLetter.phone || '—'}</span>
                  </div>
                  <div>
                    <span className="text-ink-400 block mb-0.5">Email</span>
                    <span className="font-medium text-ink-950 dark:text-white truncate block" title={selectedCoverLetter.email}>{selectedCoverLetter.email}</span>
                  </div>
                  <div>
                    <span className="text-ink-400 block mb-0.5">Báo trước khi nghỉ việc</span>
                    <span className="font-medium text-ink-950 dark:text-white">{selectedCoverLetter.noticePeriod || '—'}</span>
                  </div>
                </div>
              </div>

              {/* 3. Thư giới thiệu */}
              <div className="rounded-xl border border-ink-100 bg-ink-50/20 p-4 dark:border-white/5 dark:bg-white-[0.02]">
                <h4 className="text-xs font-semibold text-ink-500 dark:text-ink-400 uppercase tracking-wider mb-2">
                  Thư giới thiệu
                </h4>
                <p className="text-xs text-ink-700 dark:text-ink-300 leading-relaxed whitespace-pre-line">
                  {selectedCoverLetter.text || 'Ứng viên không gửi thư giới thiệu.'}
                </p>
              </div>
            </div>

            <div className="flex justify-end mt-4 pt-3 border-t border-ink-100 dark:border-white/10 shrink-0">
              <button
                onClick={() => setSelectedCoverLetter(null)}
                className="px-4 py-2 text-sm font-semibold text-ink-700 dark:text-ink-200 bg-ink-100 dark:bg-white/10 hover:bg-ink-200 dark:hover:bg-white/20 rounded-xl transition-colors"
              >
                Đóng
              </button>
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
