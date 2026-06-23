import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  Bookmark,
  MapPin,
  Briefcase,
  DollarSign,
  TrendingUp,
  Loader2,
  AlertCircle,
  ChevronRight,
  Code2,
  Server,
  BrainCircuit,
} from 'lucide-react'
import { savedJobService } from '@/services/job/savedJobService'
import type { SavedJobItem } from '@/services/job/savedJobService'

function formatSalary(job: SavedJobItem): string {
  if (job.salaryIsNegotiable) return 'Thỏa thuận'
  if (job.salaryMin && job.salaryMax)
    return `$${job.salaryMin.toLocaleString()} - $${job.salaryMax.toLocaleString()}`
  if (job.salaryMin) return `Từ $${job.salaryMin.toLocaleString()}`
  if (job.salaryMax) return `Đến $${job.salaryMax.toLocaleString()}`
  return 'Thỏa thuận'
}

function formatWorkMode(mode?: string): string {
  if (!mode) return 'Full-time'
  const mappings: Record<string, string> = {
    fulltime: 'Full-time',
    parttime: 'Part-time',
    contract: 'Hợp đồng',
    internship: 'Thực tập',
  }
  return mappings[mode.toLowerCase()] || mode
}

function formatSavedDate(dateStr: string): string {
  try {
    const diffDays = Math.ceil(
      Math.abs(new Date().getTime() - new Date(dateStr).getTime()) / (1000 * 60 * 60 * 24)
    )
    if (diffDays <= 1) return 'Hôm nay'
    if (diffDays <= 2) return 'Hôm qua'
    return `${diffDays} ngày trước`
  } catch {
    return 'Gần đây'
  }
}

function getJobIcon(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai') || dept.includes('ml'))
    return <BrainCircuit className="h-6 w-6" />
  if (dept.includes('backend') || dept.includes('server')) return <Server className="h-6 w-6" />
  return <Code2 className="h-6 w-6" />
}

function getIconColor(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai')) return 'bg-ai-50 text-ai-600'
  if (dept.includes('backend') || dept.includes('server')) return 'bg-emerald-50 text-emerald-600'
  return 'bg-brand-50 text-brand-600'
}

function SavedJobCard({
  job,
  onUnsave,
}: {
  job: SavedJobItem
  onUnsave: (jobId: string) => void
}) {
  const navigate = useNavigate()
  return (
    <article
      className="group cursor-pointer rounded-2xl border border-ink-200 bg-white p-5 shadow-card transition hover:border-brand-200 hover:shadow-card-hover"
      onClick={() => navigate(`/jobs/${job.id}`)}
    >
      <div className="flex gap-4">
        <div
          className={`grid h-12 w-12 shrink-0 place-items-center rounded-xl ${getIconColor(job.department)}`}
        >
          {getJobIcon(job.department)}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h3 className="font-semibold text-ink-900 group-hover:text-brand-700">{job.title}</h3>
              <div className="mt-0.5 flex items-center gap-1.5 text-sm text-ink-500">
                <MapPin className="h-3.5 w-3.5" />
                {job.location || 'Remote'} · {job.department || 'Engineering'}
              </div>
            </div>
            <button
              onClick={(e) => {
                e.stopPropagation()
                onUnsave(job.id)
              }}
              title="Bỏ lưu việc làm"
              className="shrink-0 rounded-lg p-2 text-ai-600 transition-colors hover:text-red-500"
            >
              <Bookmark className="h-5 w-5 fill-current" />
            </button>
          </div>
          <div className="mt-3 flex flex-wrap gap-2 text-xs">
            <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
              <Briefcase className="h-3.5 w-3.5" />
              {formatWorkMode(job.workMode || job.employmentType)}
            </span>
            <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
              <DollarSign className="h-3.5 w-3.5" />
              {formatSalary(job)}
            </span>
            {job.experienceLevel && (
              <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
                <TrendingUp className="h-3.5 w-3.5" />
                {job.experienceLevel}
              </span>
            )}
          </div>
          <div className="mt-3 flex items-center justify-between">
            <span className="text-xs text-ink-400">Đã lưu {formatSavedDate(job.savedAt)}</span>
            <button
              onClick={(e) => {
                e.stopPropagation()
                navigate(`/jobs/${job.id}`)
              }}
              className="rounded-xl border border-brand-200 bg-brand-50 px-4 py-1.5 text-sm font-semibold text-brand-700 hover:bg-brand-100"
            >
              Ứng tuyển
            </button>
          </div>
        </div>
      </div>
    </article>
  )
}

export default function SavedJobsPage() {
  const [jobs, setJobs] = useState<SavedJobItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    setLoading(true)
    savedJobService
      .getSavedJobs()
      .then((data) => active && setJobs(data))
      .catch((err: any) => active && setError(err?.message || 'Không tải được danh sách việc đã lưu.'))
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [])

  // Bỏ lưu — cập nhật lạc quan, rollback nếu API lỗi.
  const handleUnsave = (jobId: string) => {
    const prev = jobs
    setJobs((list) => list.filter((j) => j.id !== jobId))
    savedJobService.unsaveJob(jobId).catch(() => setJobs(prev))
  }

  return (
    <>
      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/jobs" className="hover:text-brand-600">
            Trang chủ
          </Link>
          <ChevronRight className="h-4 w-4" />
          <span className="font-medium text-ink-600">Việc đã lưu</span>
        </div>
      </div>

      <div className="mx-auto max-w-6xl px-6 py-6">
        <div className="mb-6 flex items-center justify-between gap-3">
          <div>
            <h1 className="font-display text-2xl font-extrabold leading-tight">Việc đã lưu</h1>
            <p className="mt-1 text-sm text-ink-500">
              {loading ? 'Đang tải…' : `${jobs.length} việc làm bạn đã lưu để xem lại sau.`}
            </p>
          </div>
          <Link
            to="/jobs"
            className="shrink-0 rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
          >
            Tìm việc làm
          </Link>
        </div>

        {loading ? (
          <div className="flex items-center justify-center rounded-2xl border border-ink-200 bg-white py-16 text-ink-400 shadow-card">
            <Loader2 className="h-6 w-6 animate-spin" />
          </div>
        ) : error ? (
          <div className="flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <AlertCircle className="h-4 w-4" /> {error}
          </div>
        ) : jobs.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-ink-300 bg-white p-10 text-center shadow-card">
            <span className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-ai-50 text-ai-600">
              <Bookmark className="h-6 w-6" />
            </span>
            <h3 className="mt-4 font-semibold text-ink-900">Chưa có việc làm nào được lưu</h3>
            <p className="mx-auto mt-1 max-w-md text-sm text-ink-500">
              Bấm biểu tượng <Bookmark className="mb-0.5 inline h-3.5 w-3.5" /> trên thẻ việc làm để
              lưu lại và xem nhanh tại đây.
            </p>
            <Link
              to="/jobs"
              className="mt-4 inline-block rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
            >
              Khám phá việc làm
            </Link>
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2">
            {jobs.map((job) => (
              <SavedJobCard key={job.id} job={job} onUnsave={handleUnsave} />
            ))}
          </div>
        )}
      </div>
    </>
  )
}
