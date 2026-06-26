import { useState, useEffect, useCallback } from 'react'
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
} from 'lucide-react'
import jobService from '@/services/job/jobService'
import { useDocumentViewer } from '@components/document/DocumentViewer'
import type { JobPosting } from '@/types/job'

function errMessage(err: unknown, fallback: string): string {
  const e = err as { response?: { data?: { message?: string } } }
  return e?.response?.data?.message || fallback
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

  // Hành động duyệt / từ chối
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [rejectOpen, setRejectOpen] = useState(false)
  const [rejectReason, setRejectReason] = useState('')

  const load = useCallback(async () => {
    if (!id) return
    try {
      setLoading(true)
      const data = await jobService.getJobPostingById(id)
      setJob(data)
    } catch (err) {
      console.error(err)
      setError(
        'Không thể tải chi tiết tin tuyển dụng. Tin có thể không tồn tại hoặc bạn không có quyền xem.'
      )
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => {
    void load()
  }, [load])

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
                  className={`px-3 py-1 rounded-full text-xs font-semibold ${
                    STATUS_BADGE[job.status] || STATUS_BADGE.closed
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
                          className={`px-2 py-0.5 rounded-full text-xs font-semibold ${
                            round.roundType === 'technical'
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
    </div>
  )
}
