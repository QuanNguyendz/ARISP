import { useState, useEffect, useCallback, useMemo } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
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
import jobService from '@/services/job/jobService'
import { applicationService } from '@/services/application/applicationService'
import { useDocumentViewer } from '@components/document/DocumentViewer'
import { Pagination } from '@components/shared'
import type { JobPosting } from '@/types/job'
import type { HrApplicationItem } from '@/types/application'
import { resolveAssetUrl } from '@/config/constants'
import { appStatusBadge, appStatusLabel, initials, scoreColor } from '../recruiter/_jobUi'

function errMessage(err: unknown, fallback: string): string {
  const e = err as { response?: { data?: { message?: string } } }
  return e?.response?.data?.message || fallback
}

// Chuyển date sang YYYY-MM-DD theo múi giờ LOCAL (tránh lỗi UTC ±7h)
function toLocalDateStr(d: string | Date | number | undefined | null): string {
  if (!d) return ''
  const dt = new Date(d)
  if (isNaN(dt.getTime())) return ''
  const yyyy = dt.getFullYear()
  const mm = String(dt.getMonth() + 1).padStart(2, '0')
  const dd = String(dt.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

const STATUS_LABEL: Record<string, string> = {
  draft: 'Bản nháp',
  pending: 'Chờ bạn duyệt',
  active: 'Đang đăng',
  rejected: 'Đã từ chối',
  closed: 'Đã đóng',
  archived: 'Lưu trữ',
  paused: 'Tạm dừng',
}

const STATUS_BADGE: Record<string, string> = {
  draft: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
  pending: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
  active: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
  rejected: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
  closed: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400',
  archived: 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400',
  paused: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
}

function getDeadlineText(deadlineStr?: string | null): string {
  if (!deadlineStr) return 'Không giới hạn'
  const d = new Date(deadlineStr)
  if (Number.isNaN(d.getTime())) return '—'
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

export default function JobPostingDetailPage() {
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

  // Hành động duyệt / từ chối
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
      console.error('Lỗi khi tải danh sách ứng viên:', err)
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
      setError(
        'Không thể tải chi tiết tin tuyển dụng. Tin có thể không tồn tại hoặc bạn không có quyền xem.'
      )
    } finally {
      setLoading(false)
    }
  }, [id, loadApps])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    setSelectedIds([])
  }, [activeTab, cvDateFilter, cvStatusFilter, cvSortOrder, interviewDateFilter, interviewStatusFilter, interviewSortOrder])

  const handleBatchAccept = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(`Bạn có chắc chắn muốn duyệt ${selectedIds.length} hồ sơ đã chọn và chuyển sang Vòng 1?`)) return
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
      setNotice(`Đã duyệt thành công ${successCount} hồ sơ.${failCount > 0 ? ` Thất bại ${failCount} hồ sơ.` : ''}`)
      setSelectedIds([])
      await loadApps()
    } catch (err) {
      setActionError(errMessage(err, 'Lỗi khi duyệt hàng loạt.'))
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleBatchReject = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(`Bạn có chắc chắn muốn từ chối ${selectedIds.length} hồ sơ đã chọn và gửi thư cảm ơn?`)) return
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
      setNotice(`Đã từ chối thành công ${successCount} hồ sơ.${failCount > 0 ? ` Thất bại ${failCount} hồ sơ.` : ''}`)
      setSelectedIds([])
      await loadApps()
    } catch (err) {
      setActionError(errMessage(err, 'Lỗi khi từ chối hàng loạt.'))
    } finally {
      setBatchProcessing(false)
    }
  }

  const handleBatchInvite = async () => {
    if (selectedIds.length === 0) return
    if (!window.confirm(`Bạn có chắc chắn muốn gửi lời mời phỏng vấn cho ${selectedIds.length} ứng viên đã chọn?`)) return
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
      setNotice(`Đã gửi lời mời thành công cho ${successCount} ứng viên.${failCount > 0 ? ` Thất bại ${failCount} ứng viên.` : ''}`)
      setSelectedIds([])
      await loadApps()
    } catch (err) {
      setActionError(errMessage(err, 'Lỗi khi gửi lời mời hàng loạt.'))
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
      setNotice('Đã duyệt hồ sơ ứng viên và gửi lời mời phỏng vấn Vòng 1 thành công.')
      await loadApps()
    } catch (err) {
      setActionError(errMessage(err, 'Lỗi khi duyệt hồ sơ.'))
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
      setNotice('Đã gửi lời mời (magic link) cho ứng viên.')
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Không thể gửi lời mời.')
    } finally {
      setInvitingId(null)
    }
  }

  const handleReject = async (appId: string) => {
    if (!window.confirm('Bạn có chắc chắn muốn từ chối hồ sơ ứng viên này và gửi thư cảm ơn?')) return
    setProcessingAppId(appId)
    setActionError(null)
    setNotice(null)
    try {
      await applicationService.rejectApplication(appId)
      setNotice('Đã từ chối hồ sơ và gửi email cảm ơn ứng viên thành công.')
      await loadApps()
    } catch (err) {
      setActionError(errMessage(err, 'Lỗi khi từ chối hồ sơ.'))
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

    // Vòng duyệt hồ sơ (chưa qua vòng nào)
    const cvReviewCandidates = apps.filter(
      (a) => !a.currentRound || a.currentRound === 0
    )

    const roundCols: FunnelColumn[] = (job.roundConfigs || [])
      .slice()
      .sort((a, b) => a.roundNumber - b.roundNumber)
      .map((rc) => {
        const candidates = apps.filter((a) => a.currentRound === rc.roundNumber)

        return {
          id: `round_${rc.roundNumber}`,
          title: `Vòng ${rc.roundNumber}`,
          subtitle: rc.roundType === 'technical' ? 'Chuyên môn' : 'Sơ loại',
          candidates,
        }
      })

    return [
      {
        id: 'cv_review',
        title: 'Duyệt Hồ Sơ',
        subtitle: 'CV mới nộp',
        candidates: cvReviewCandidates,
      },
      ...roundCols,
    ]
  }, [job, apps])

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
      // 1. Lọc theo ngày nộp (so sánh giờ local, tránh sai múi giờ)
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
      // 1. Lọc theo ngày thi (so sánh giờ local, tránh sai múi giờ)
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
      setSelectedIds((prev) => prev.filter((id) => !pageSelectableCandidates.some((c) => c.id === id)))
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
      setNotice('Đã duyệt tin tuyển dụng. File JD đã được đóng dấu xác nhận.')
      await load()
    } catch (err: unknown) {
      setActionError(errMessage(err, 'Không thể duyệt tin. Vui lòng thử lại.'))
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
      setNotice('Đã từ chối tin tuyển dụng. Recruiter sẽ thấy lý do để chỉnh sửa.')
      await load()
    } catch (err: unknown) {
      setActionError(errMessage(err, 'Không thể từ chối tin. Vui lòng thử lại.'))
    } finally {
      setBusy(false)
    }
  }

  function formatSalary(job: JobPosting): string {
    if (job.salaryIsNegotiable ||
      (job.salaryMin == null && job.salaryMax == null) ||
      (job.salaryMin === 0 && job.salaryMax === 0)) {
      return 'Thỏa thuận'
    }

    const cur = (job.salaryCurrency || 'VND').toUpperCase()

    const formatVal = (n: number) => {
      if (cur === 'VND') {
        return n.toLocaleString('vi-VN')
      }
      return n.toLocaleString('en-US')
    }

    const unit = cur === 'VND' ? ' ₫' : ` ${cur}`

    if (job.salaryMin != null && job.salaryMax != null && job.salaryMin !== 0 && job.salaryMax !== 0) {
      return `${formatVal(job.salaryMin)} - ${formatVal(job.salaryMax)}${unit}`
    }

    if (job.salaryMin != null && job.salaryMin !== 0) {
      return `Từ ${formatVal(job.salaryMin)}${unit}`
    }

    if (job.salaryMax != null && job.salaryMax !== 0) {
      return `Đến ${formatVal(job.salaryMax)}${unit}`
    }

    return 'Thỏa thuận'
  }

  function formatWorkMode(mode?: string): string {
    if (!mode) return 'Chưa cấu hình'
    const mappings: Record<string, string> = {
      fulltime: 'Toàn thời gian (Full-time)',
      parttime: 'Bán thời gian (Part-time)',
      contract: 'Hợp đồng (Contract)',
      internship: 'Thực tập (Internship)',
    }
    return mappings[mode.toLowerCase()] || mode
  }

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh] gap-3 bg-ink-50 dark:bg-ink-950">
        <Loader2 className="w-10 h-10 text-brand-600 dark:text-brand-400 animate-spin" />
        <p className="text-sm text-ink-500 dark:text-ink-400">
          Đang tải chi tiết tin tuyển dụng...
        </p>
      </div>
    )
  }

  if (error || !job) {
    return (
      <div className="p-6 lg:p-8 max-w-4xl mx-auto text-center py-20 bg-ink-50 dark:bg-ink-950">
        <ShieldAlert className="w-16 h-16 text-red-500 dark:text-red-400 mx-auto mb-4" />
        <h2 className="text-2xl font-bold text-ink-900 dark:text-white mb-2">Đã xảy ra lỗi</h2>
        <p className="text-ink-600 dark:text-ink-400 mb-6">{error || 'Không tìm thấy dữ liệu.'}</p>
        <button
          onClick={() => navigate('/hr/jobs')}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 font-medium hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Quay lại danh sách
        </button>
      </div>
    )
  }

  const isPending = job.status === 'pending'

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <button
          onClick={() => navigate('/hr/jobs')}
          className="inline-flex items-center gap-2 text-sm text-ink-600 dark:text-ink-400 hover:text-brand-600 dark:hover:text-brand-400 mb-6 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Quay lại danh sách
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
                  className={`px-3 py-1 rounded-full text-xs font-semibold ${STATUS_BADGE[job.status] || STATUS_BADGE.closed
                    }`}
                >
                  {STATUS_LABEL[job.status] || job.status}
                </span>
              </div>
              <p className="text-xl text-ink-600 dark:text-ink-400 mb-4">
                {job.department || 'Phòng ban tuyển dụng'}
              </p>
              <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm text-ink-600 dark:text-ink-400">
                <span className="flex items-center gap-1.5">
                  <MapPin className="w-4 h-4 text-brand-600 dark:text-brand-400" />
                  {job.location || 'Chưa cấu hình'}
                </span>
                <span className="flex items-center gap-1.5">
                  {formatSalary(job)}
                </span>
                <span className="flex items-center gap-1.5">
                  <Clock className="w-4 h-4 text-brand-600 dark:text-brand-400" />
                  {formatWorkMode(job.workMode || job.employmentType)}
                </span>
                <span className="flex items-center gap-1.5">
                  <Calendar className="w-4 h-4 text-brand-600 dark:text-brand-400" />
                  Hạn nộp: {getDeadlineText(job.applicationDeadline)}
                </span>
                {job.vacancies != null && job.vacancies > 0 && (
                  <span className="flex items-center gap-1.5">
                    <Target className="w-4 h-4 text-brand-600 dark:text-brand-400" />
                    Chỉ tiêu: {job.vacancies}
                  </span>
                )}
              </div>
            </div>
          </div>
          <button
            type="button"
            onClick={() => navigate(`/hr/jobs/${job.id}/edit`)}
            className="flex items-center justify-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity"
          >
            <Edit2 className="w-4 h-4" />
            Chỉnh sửa
          </button>
        </div>

        {/* Thông báo hành động */}
        {notice && (
          <div className="mb-4 flex items-start justify-between gap-3 rounded-xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
            <span className="flex items-center gap-2">
              <CheckCircle2 className="w-4 h-4" /> {notice}
            </span>
            <button onClick={() => setNotice(null)} className="hover:opacity-70">
              Đóng
            </button>
          </div>
        )}
        {actionError && (
          <div className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-400">
            <AlertCircle className="mt-0.5 w-4 h-4 shrink-0" /> {actionError}
          </div>
        )}

        {/* Bảng phê duyệt của HR Leader — chỉ hiện khi tin đang chờ duyệt */}
        {isPending && (
          <div className="mb-6 rounded-2xl border border-amber-200 dark:border-amber-500/30 bg-amber-50/60 dark:bg-amber-500/10 p-5 shadow-card">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex items-start gap-3">
                <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-amber-100 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400">
                  <ShieldCheck className="h-5 w-5" />
                </span>
                <div>
                  <h3 className="text-base font-semibold text-ink-900 dark:text-white">
                    Tin đang chờ bạn duyệt
                  </h3>
                  <p className="mt-0.5 text-sm text-ink-600 dark:text-ink-400">
                    Recruiter {job.createdByName ? <b>{job.createdByName}</b> : ''} đã gửi tin kèm
                    file JD. Duyệt để đăng tin (JD sẽ được đóng dấu xác nhận) hoặc từ chối kèm lý
                    do.
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
                  )}
                  Duyệt & đóng dấu
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
                  <XCircle className="h-4 w-4" />
                  Từ chối
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Banner: đã từ chối */}
        {job.status === 'rejected' && job.rejectionReason && (
          <div className="mb-6 flex items-start gap-2 rounded-2xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-400">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              <b>Đã từ chối:</b> {job.rejectionReason}
            </span>
          </div>
        )}

        {/* Banner: đã duyệt */}
        {job.approvedAt && (job.status === 'active' || job.status === 'closed') && (
          <div className="mb-6 flex items-center gap-2 rounded-2xl border border-emerald-200 dark:border-emerald-500/20 bg-emerald-50 dark:bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-400">
            <ShieldCheck className="h-4 w-4 shrink-0" />
            <span>
              Đã duyệt bởi <b>{job.approverName || 'HR Leader'}</b> lúc{' '}
              {new Date(job.approvedAt).toLocaleString('vi-VN')}.
            </span>
          </div>
        )}

        <div className="grid lg:grid-cols-3 gap-6">
          {/* Main Info */}
          <div className="lg:col-span-2 space-y-6">
            {/* Tài liệu JD */}
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.05 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4 flex items-center gap-2">
                <FileText className="w-5 h-5 text-brand-600 dark:text-brand-400" />
                Tài liệu JD
              </h2>
              {job.jdFileUrl ? (
                <div className="flex flex-wrap gap-3">
                  <button
                    type="button"
                    onClick={() => openDocument(job.jdFileUrl!, job.jdFileName || `${job.title} - JD`)}
                    className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
                  >
                    <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                    {job.jdFileName || 'JD gốc'}
                    {job.jdFileFormat && (
                      <span className="text-xs uppercase text-ink-400">{job.jdFileFormat}</span>
                    )}
                  </button>
                  {job.signedJdFileUrl && (
                    <button
                      type="button"
                      onClick={() => openDocument(job.signedJdFileUrl!, `${job.title} - JD đã duyệt.pdf`)}
                      className="inline-flex items-center gap-2 rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 px-4 py-2.5 text-sm font-medium text-emerald-700 dark:text-emerald-400 hover:bg-emerald-100 dark:hover:bg-emerald-500/20"
                    >
                      <Download className="h-4 w-4" />
                      JD đã đóng dấu duyệt
                    </button>
                  )}
                </div>
              ) : (
                <p className="text-sm text-ink-500 dark:text-ink-400">
                  Tin này chưa đính kèm file JD.
                </p>
              )}
            </motion.div>

            {/* Description */}
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">
                Mô tả công việc
              </h2>
              <div
                className="text-ink-600 dark:text-ink-400 leading-relaxed ql-editor-display"
                dangerouslySetInnerHTML={{ __html: job.jobDescription }}
              />
            </motion.div>

            {/* Skills */}
            {job.skills && job.skills.length > 0 && (
              <motion.div
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: 0.15 }}
                className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
              >
                <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">
                  Yêu cầu kỹ năng
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

            {/* Persona and Rubric Settings */}
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.2 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4 flex items-center gap-2">
                <Sparkles className="w-5 h-5 text-brand-600 dark:text-brand-400" />
                Cấu hình AI Interviewer Persona
              </h2>
              <div className="grid md:grid-cols-2 gap-6 text-sm">
                <div className="space-y-3">
                  <p className="text-ink-600 dark:text-ink-400">
                    <strong className="text-ink-900 dark:text-white">Ngôn ngữ yêu cầu:</strong>{' '}
                    {job.languageRequirement || 'Mặc định theo hệ thống'}
                  </p>
                  <p className="text-ink-600 dark:text-ink-400">
                    <strong className="text-ink-900 dark:text-white">
                      Ngôn ngữ tự động phát hiện:
                    </strong>{' '}
                    {job.detectedLanguage === 'vi' ? 'Tiếng Việt' : 'Tiếng Anh/Khác'}
                  </p>
                </div>
                <div className="space-y-3">
                  <p className="text-ink-600 dark:text-ink-400">
                    <strong className="text-ink-900 dark:text-white">
                      Giờ phỏng vấn khả dụng:
                    </strong>{' '}
                    Bắt buộc On-site
                  </p>
                  <p className="text-ink-600 dark:text-ink-400">
                    <strong className="text-ink-900 dark:text-white">
                      Hạn đổi lịch phỏng vấn:
                    </strong>{' '}
                    {job.rescheduleDeadlineHours || 24} giờ trước buổi hẹn
                  </p>
                </div>
              </div>
            </motion.div>
          </div>

          {/* Sidebar Round Configs */}
          <div className="space-y-6">
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.25 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
            >
              <h2 className="text-xl font-semibold text-ink-900 dark:text-white mb-4">
                Các vòng phỏng vấn AI ({job.roundConfigs?.length || 0})
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
                          Vòng {round.roundNumber}
                        </span>
                        <span
                          className={`px-2 py-0.5 rounded-full text-xs font-semibold ${round.roundType === 'technical'
                              ? 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400'
                              : 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400'
                            }`}
                        >
                          {round.roundType === 'technical' ? 'Technical' : 'Screening'}
                        </span>
                      </div>
                      <div className="space-y-1.5 text-xs text-ink-600 dark:text-ink-400">
                        <p className="flex items-center gap-1.5">
                          <Languages className="w-3.5 h-3.5 text-brand-600 dark:text-brand-400" />
                          Ngôn ngữ:{' '}
                          {round.interviewLanguage === 'vi' ? 'Tiếng Việt' : 'Tiếng Anh/Khác'}
                        </p>
                        <p className="flex items-center gap-1.5">
                          <Hourglass className="w-3.5 h-3.5 text-brand-600 dark:text-brand-400" />
                          Thời gian: {round.maxDurationMinutes} phút
                        </p>
                        <p className="flex items-center gap-1.5">
                          <Clock className="w-3.5 h-3.5 text-brand-600 dark:text-brand-400" />
                          TTL Code: {round.interviewCodeTtlHours} giờ
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-ink-500 dark:text-ink-400">
                  Chưa cấu hình các vòng phỏng vấn.
                </p>
              )}
            </motion.div>
          </div>
        </div>

        {/* Bảng Theo Dõi Ứng Viên Theo Vòng */}
        <div className="mt-12 space-y-6">
          <div className="flex items-center justify-between border-b border-ink-200 dark:border-white/10 pb-4">
            <div>
              <h2 className="text-2xl font-bold text-ink-900 dark:text-white flex items-center gap-2">
                <Target className="w-6 h-6 text-brand-600 dark:text-brand-400" />
                Tiến trình ứng tuyển của ứng viên
              </h2>
              <p className="text-sm text-ink-500 dark:text-ink-400 mt-1">
                Theo dõi và phê duyệt hồ sơ ứng viên theo từng giai đoạn phỏng vấn
              </p>
            </div>
            <span className="px-3 py-1 bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 rounded-full text-xs font-semibold">
              Tổng cộng: {apps.length} ứng viên
            </span>
          </div>

          {loadingApps ? (
            <div className="flex justify-center items-center py-12">
              <Loader2 className="w-8 h-8 animate-spin text-brand-600 dark:text-brand-400" />
              <span className="ml-2 text-sm text-ink-600 dark:text-ink-400">Đang tải ứng viên...</span>
            </div>
          ) : apps.length === 0 ? (
            <div className="text-center py-12 rounded-2xl border border-dashed border-ink-300 dark:border-white/10 bg-white dark:bg-white/5">
              <p className="text-ink-500 dark:text-ink-400">Chưa có ứng viên nào ứng tuyển cho tin này.</p>
            </div>
          ) : (
            <>
              {/* Tabs phân chia ứng viên theo vòng */}
              <div className="flex flex-wrap gap-2 mb-6">
                {tabs.map((tab) => (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={`px-4 py-2 rounded-xl text-sm font-semibold transition-all duration-200 ${activeTab === tab.id
                        ? 'bg-brand-600 text-white shadow-sm'
                        : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'
                      }`}
                  >
                    {tab.label} ({tab.count})
                  </button>
                ))}
              </div>

              {/* Bộ lọc và sắp xếp */}
              <div className="flex flex-wrap items-end justify-between gap-4 mb-6 p-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
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
                          {/* cv_review: cv_submitted | invited | cv_rejected */}
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
                <div className="flex items-center justify-between p-4 mb-4 rounded-xl border border-brand-200 dark:border-brand-500/30 bg-brand-50/50 dark:bg-brand-500/10 backdrop-blur-sm animate-fade-in">
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

              {processedCandidates.length === 0 ? (
                <div className="text-center py-12 rounded-2xl border border-dashed border-ink-300 dark:border-white/10 bg-white dark:bg-white/5">
                  <p className="text-ink-500 dark:text-ink-400">Không tìm thấy ứng viên nào phù hợp với bộ lọc.</p>
                </div>
              ) : (
                <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 overflow-hidden shadow-card">
                  {/* Tiêu đề cột */}
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

                  {/* Danh sách */}
                  <div className="divide-y divide-ink-100 dark:divide-white/10">
                    {pagedCandidates.map((a: HrApplicationItem) => (
                      <div
                        key={a.id}
                        className={`grid items-center gap-4 px-5 py-3 hover:bg-ink-50/50 dark:hover:bg-white-[0.02] transition-colors ${activeTab === 'cv_review'
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

                        {/* Ứng viên */}
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
                                <span className={`text-sm font-bold ${scoreColor(a.matchScore)}`}>
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

                        {/* Trạng thái */}
                        <div className="text-center flex flex-col items-center justify-center gap-1">
                          <span className={`text-[11px] px-2.5 py-0.5 rounded-full font-medium ${appStatusBadge(a.status)} whitespace-nowrap`}>
                            {appStatusLabel(a.status)}
                          </span>
                        </div>

                        {/* Hành động */}
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
                                className="flex w-full items-center justify-center gap-1 py-1.5 text-xs bg-brand-600 text-white hover:bg-brand-700 rounded-lg transition-colors font-semibold shadow-sm disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
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
                              onClick={() => openDocument(resolveAssetUrl(a.cvFileUrl!), `${a.candidateName} - CV`)}
                              title="Xem CV"
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
                            title="Xem thông tin ứng tuyển & Thư giới thiệu"
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
                    label="ứng viên"
                    onPageChange={setPage}
                  />
                </div>
              )}
            </>
          )}
        </div>
      </motion.div>

      {/* Modal từ chối */}
      {rejectOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-ink-950/50 backdrop-blur-sm">
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="w-full max-w-md rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-6 shadow-card-hover"
          >
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white mb-1">
              Từ chối tin tuyển dụng
            </h3>
            <p className="text-sm text-ink-500 dark:text-ink-400 mb-4">
              Nhập lý do từ chối tin "{job.title}". Recruiter sẽ thấy lý do này để chỉnh sửa và gửi
              duyệt lại.
            </p>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              rows={4}
              placeholder="Lý do từ chối..."
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
                Hủy
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
                )}
                Xác nhận từ chối
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
