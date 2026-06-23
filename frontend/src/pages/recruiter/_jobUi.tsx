import type { JobPosting } from '@/types/job'

// ===== Trạng thái Job (khớp backend: draft|pending|active|rejected|closed|archived) =====
export type JobStatus = JobPosting['status']

export const jobStatusLabel = (s: string): string =>
  ({
    draft: 'Nháp',
    pending: 'Chờ HR duyệt',
    active: 'Đang đăng',
    rejected: 'Bị từ chối',
    closed: 'Đã đóng',
    archived: 'Lưu trữ',
    paused: 'Tạm dừng',
  } as Record<string, string>)[s] || s

export const jobStatusBadge = (s: string): string =>
  ({
    draft: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    pending: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    active: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    rejected: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    closed: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    archived: 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400',
    paused: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
  } as Record<string, string>)[s] || 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300'

// ===== Trạng thái Application (khớp backend: invited|cv_submitted|screening|interview|pass|not_pass|withdrawn) =====
export const appStatusLabel = (s: string): string =>
  ({
    invited: 'Đã mời',
    cv_submitted: 'Mới ứng tuyển',
    screening: 'Đang sơ loại',
    interview: 'Đang phỏng vấn',
    pass: 'Đạt',
    not_pass: 'Không đạt',
    withdrawn: 'Đã rút',
  } as Record<string, string>)[s] || s

export const appStatusBadge = (s: string): string =>
  ({
    invited: 'bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400',
    cv_submitted: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    screening: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    interview: 'bg-ai-100 dark:bg-ai-500/20 text-ai-700 dark:text-ai-400',
    pass: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    not_pass: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    withdrawn: 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400',
  } as Record<string, string>)[s] || 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300'

// ===== Format lương =====
export function formatSalary(job: Pick<JobPosting, 'salaryIsNegotiable' | 'salaryMin' | 'salaryMax' | 'salaryCurrency'>): string {
  if (job.salaryIsNegotiable || (job.salaryMin == null && job.salaryMax == null)) return 'Thỏa thuận'
  const cur = job.salaryCurrency || 'VND'
  const fmt = (n: number) => (cur === 'VND' ? `${(n / 1_000_000).toLocaleString('vi-VN')}tr` : n.toLocaleString('en-US'))
  const sym = cur === 'USD' ? '$' : cur === 'VND' ? '' : cur + ' '
  if (job.salaryMin != null && job.salaryMax != null) return `${sym}${fmt(job.salaryMin)} - ${sym}${fmt(job.salaryMax)}`
  if (job.salaryMin != null) return `Từ ${sym}${fmt(job.salaryMin)}`
  return `Đến ${sym}${fmt(job.salaryMax!)}`
}

// ===== Verdict đánh giá (pass | not_pass) =====
export const verdictLabel = (v?: string | null): string =>
  v === 'pass' ? 'Đạt' : v === 'not_pass' ? 'Không đạt' : 'Chưa rõ'

export const verdictBadge = (v?: string | null): string =>
  v === 'pass'
    ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
    : v === 'not_pass'
      ? 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400'
      : 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400'

// ===== Trạng thái phiên phỏng vấn (pending|active|completed|aborted|error) =====
export const sessionStatusLabel = (s: string): string =>
  ({ pending: 'Chờ', active: 'Đang diễn ra', completed: 'Hoàn thành', aborted: 'Đã hủy', error: 'Lỗi' } as Record<string, string>)[s] || s

export const sessionStatusBadge = (s: string): string =>
  ({
    pending: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    active: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    completed: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    aborted: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    error: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
  } as Record<string, string>)[s] || 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300'

export const scoreColor = (s?: number | null) =>
  s == null ? 'text-ink-400' : s >= 75 ? 'text-emerald-600 dark:text-emerald-400' : s >= 50 ? 'text-amber-600 dark:text-amber-400' : 'text-red-600 dark:text-red-400'

export const initials = (name?: string | null) =>
  (name || 'U')
    .trim()
    .split(/\s+/)
    .map((n) => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

export function timeAgo(iso: string): string {
  const d = new Date(iso).getTime()
  const diff = Date.now() - d
  const m = Math.floor(diff / 60000)
  if (m < 1) return 'vừa xong'
  if (m < 60) return `${m} phút trước`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h} giờ trước`
  const days = Math.floor(h / 24)
  if (days < 30) return `${days} ngày trước`
  return new Date(iso).toLocaleDateString('vi-VN')
}
