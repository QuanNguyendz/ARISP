import { useTranslation } from 'react-i18next'
import { formatScore } from '@ari/shared/utils/format'
import { formatDateTime24, formatTime24 } from '@ari/shared/utils/time24'
import type { CvScoreState } from '@ari/shared/types/application'

const NS = 'modules/staff/cvScoring'

/**
 * Mốc thử lại của một lượt chấm hỏng: `null` khi không có mốc, `due` khi mốc đã qua (đang chờ lượt quét kế),
 * còn lại là giờ hiển thị — chỉ giờ nếu cùng ngày, kèm ngày nếu khác ngày.
 */
export function cvRetryTime(retryAt?: string | null, now: Date = new Date()): string | 'due' | null {
  if (!retryAt) return null
  const at = new Date(retryAt)
  if (Number.isNaN(at.getTime())) return null
  if (at.getTime() <= now.getTime()) return 'due'
  return at.toDateString() === now.toDateString() ? formatTime24(at) : formatDateTime24(at)
}

/** Chữ thuần cho chỗ chỉ nhận chuỗi (dòng thông tin). */
export function useCvScoreText() {
  const { t } = useTranslation(NS)
  return (score?: number | null, status?: CvScoreState | null): string | null => {
    if (score != null) return formatScore(score)
    if (!status || status === 'scored') return null
    return t(`status.${status}`)
  }
}
