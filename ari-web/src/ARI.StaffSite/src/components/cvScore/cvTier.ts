/**
 * Màu của điểm CV theo NHÃN KHUYẾN NGHỊ server trả về (ADR-075) — không theo ngưỡng số viết cứng ở màn hình.
 *
 * Trước đây mỗi màn tự tô màu theo ngưỡng riêng (75/50, 80/60) trong khi nhãn server dùng 80/65/50 — cùng một điểm
 * có thể xanh ở màn này, vàng ở màn kia. Nay ngưỡng là công thức HM khai theo tin, nên chỉ server biết điểm nào là
 * "phù hợp"; màn hình chỉ đọc nhãn. Trượt điều kiện bắt buộc thì nhãn đã là "Reject" dù điểm cao.
 */
export type CvTierTone = 'strong' | 'good' | 'caution' | 'reject' | 'none'

export function cvTierTone(recommendation?: string | null): CvTierTone {
  switch (recommendation) {
    case 'Strong Hire':
      return 'strong'
    case 'Hire':
      return 'good'
    case 'Proceed with caution':
      return 'caution'
    case 'Reject':
      return 'reject'
    default:
      return 'none'
  }
}

const TEXT: Record<CvTierTone, string> = {
  strong: 'text-emerald-600 dark:text-emerald-400',
  good: 'text-brand-600 dark:text-brand-400',
  caution: 'text-amber-600 dark:text-amber-400',
  reject: 'text-red-600 dark:text-red-400',
  none: 'text-ink-700 dark:text-ink-200',
}

/** Lớp màu chữ cho con số điểm CV. */
export const cvScoreTextClass = (recommendation?: string | null) => TEXT[cvTierTone(recommendation)]
