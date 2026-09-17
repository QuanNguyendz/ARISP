import { useTranslation } from 'react-i18next'
import { formatScore } from '@ari/shared/utils/format'
import type { CvScoreState } from '@ari/shared/types/application'
import { cvRetryTime } from './useCvScoreText'

const NS = 'modules/staff/cvScoring'

const STATE_STYLE: Record<CvScoreState, string> = {
  scored: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
  rescoring: 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-400',
  queued: 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-400',
  scoring_failed: 'bg-red-100 text-red-600 dark:bg-red-500/20 dark:text-red-400',
  pending_rubric: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  invalid_cv: 'bg-red-100 text-red-600 dark:bg-red-500/20 dark:text-red-400',
  no_cv: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-400',
}

/**
 * Điểm CV trong danh sách (ADR-070): có điểm thì hiện điểm (kèm dấu "đang chấm lại" nếu điểm theo bộ tiêu
 * chí cũ); không có thì nói VÌ SAO — thay cho dấu "—" không phân biệt được "chưa chấm", "chờ HM khai bộ
 * tiêu chí" hay "file không phải CV".
 */
export default function CvScoreBadge({
  score,
  status,
  retryAt,
  className = 'text-sm font-semibold text-ink-900 dark:text-white',
}: {
  score?: number | null
  status?: CvScoreState | null
  /** Khi `status = scoring_failed`: lúc hệ thống tự chấm lại (hiện ở tooltip). */
  retryAt?: string | null
  className?: string
}) {
  const { t } = useTranslation(NS)
  const retry = status === 'scoring_failed' ? cvRetryTime(retryAt) : null
  const hint =
    status === 'scoring_failed'
      ? retry === 'due' || retry === null
        ? t('status.retryingSoon')
        : t('status.retryAt', { time: retry })
      : undefined
  // Trạng thái đọc được từ DOM — để kiểm thử end-to-end biết điểm đã tự cập nhật hay chưa.
  const stateAttrs = { 'data-cv-score-state': status ?? '', 'data-cv-score': score ?? '' }

  if (score != null) {
    return (
      <span className="inline-flex items-center gap-1.5" {...stateAttrs}>
        <span className={className}>{formatScore(score)}</span>
        {(status === 'rescoring' || status === 'scoring_failed') && (
          <span className={`rounded-full px-1.5 py-0.5 text-[10px] font-medium ${STATE_STYLE[status]}`} title={hint}>
            {t(`status.${status}`)}
          </span>
        )}
      </span>
    )
  }

  if (!status || status === 'scored')
    return (
      <span className="text-sm text-ink-300" {...stateAttrs}>
        —
      </span>
    )

  return (
    <span
      className={`whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${STATE_STYLE[status]}`}
      title={hint}
      {...stateAttrs}
    >
      {t(`status.${status}`)}
    </span>
  )
}
