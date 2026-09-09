/**
 * Từ vựng dùng chung cho cụm cổng Hiring Manager + thư mời nhận việc (ADR-061).
 *
 * Ba khu vực nhân sự (HR / Recruiter / HM) render CÙNG các component này, nên namespace i18n phải
 * là namespace CHUNG — đúng bài học ADR-059: màn Phỏng vấn từng được gộp thành component dùng
 * chung nhưng để lại chuỗi viết cứng, và trang có nút đổi ngôn ngữ mà không đổi được gì.
 *
 * File này là module thuần (không gọi được hook), nên mọi thứ ở đây là KHOÁ i18n — component nhận
 * khoá rồi tự dịch.
 */

export const HIRING_NS = 'modules/staff/hiring'
export const OFFERS_NS = 'modules/staff/offers'

/** Khu vực đang render — chỉ đổi câu chữ và đường dẫn, không đổi hành vi. */
export type StaffArea = 'hr' | 'recruiter' | 'hm'

export interface AreaConfig {
  candidateHref: (applicationId: string) => string
  jobHref: (jobId: string) => string
  offersHref: string
}

export const AREAS: Record<StaffArea, AreaConfig> = {
  hr: {
    candidateHref: (id) => `/hr/candidates/${id}`,
    jobHref: (id) => `/hr/jobs/${id}`,
    offersHref: '/hr/offers',
  },
  recruiter: {
    // `/recruiter/jobs/:id` KHÔNG tồn tại — đường đúng là `/recruiter/my-jobs/:id` (ADR-058).
    candidateHref: (id) => `/recruiter/candidates/${id}`,
    jobHref: (id) => `/recruiter/my-jobs/${id}`,
    offersHref: '/recruiter/offers',
  },
  hm: {
    candidateHref: (id) => `/hm/candidates/${id}`,
    jobHref: (id) => `/hm/jobs/${id}`,
    offersHref: '/hm/offers',
  },
}

/**
 * Quyết định của Hiring Manager ở cổng shortlist. `bypassed` = quản trị viên đã vượt cổng có ghi
 * lý do — cố ý hiện thành một nhãn RIÊNG chứ không gộp vào "đã duyệt": vượt cổng im lặng thì
 * không ai biết cổng đã bị vượt, mà đó mới là thứ cần nhìn thấy khi rà lại về sau.
 */
export const HM_DECISION_BADGES: Record<string, string> = {
  pending: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  approved: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
  rejected: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400',
  bypassed: 'bg-violet-100 text-violet-700 dark:bg-violet-500/20 dark:text-violet-400',
}

export function hmDecisionBadgeClass(decision?: string | null): string {
  return (
    HM_DECISION_BADGES[decision ?? ''] ||
    'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
  )
}

/** Trạng thái thư mời nhận việc — khớp `OfferStatus` phía backend. */
export const OFFER_STATUS_BADGES: Record<string, string> = {
  draft: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300',
  pending_approval: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  approved: 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-400',
  sent: 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-400',
  accepted: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
  declined: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400',
  withdrawn: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300',
  expired: 'bg-orange-100 text-orange-700 dark:bg-orange-500/20 dark:text-orange-400',
}

export function offerStatusBadgeClass(status?: string | null): string {
  return (
    OFFER_STATUS_BADGES[status ?? ''] || 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
  )
}

/** Tiền tệ hiển thị theo kiểu Việt Nam; `null` an toàn vì lương là trường không bắt buộc lúc nháp. */
export function formatSalary(amount?: number | null, currency?: string | null): string {
  if (typeof amount !== 'number') return '—'
  return `${amount.toLocaleString('vi-VN')} ${currency ?? 'VND'}`
}

export function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('vi-VN')
}
