import { useTranslation } from 'react-i18next'
import { ArrowRight, Calculator, Sparkles } from 'lucide-react'
import type { CvRubricPreview } from '@ari/shared/fservices/cvRubric'
import { cvScoreTextClass } from '@/components/cvScore/cvTier'

const NS = 'modules/staff/cvScoring'

/** Số dòng hiện trong bảng — server đã sắp hồ sơ đổi khuyến nghị lên đầu. */
const MAX_ROWS = 50

/**
 * Kết quả "Xem trước tác động" (ADR-075): bản nháp bộ tiêu chí / công thức sẽ đổi điểm và khuyến nghị của từng hồ sơ
 * thế nào — tính bằng chính công thức sẽ dùng, trên câu trả lời AI đã có, không ghi gì. Đây là bước "chấm thử trên hồ
 * sơ cũ rồi mới chốt ngưỡng" mà tài liệu tuyển dụng khuyên làm trước khi dùng một bảng điểm.
 */
export default function CvRubricPreviewPanel({ preview }: { preview: CvRubricPreview }) {
  const { t } = useTranslation(NS)
  const rec = (r?: string | null) => (r ? t(`breakdown.recName.${r}`, { defaultValue: r }) : '—')
  const rows = preview.items.slice(0, MAX_ROWS)

  return (
    <div className="rounded-xl border border-ai-200 bg-white p-3 dark:border-ai-500/20 dark:bg-white/5">
      <p className="flex items-center gap-1.5 text-xs font-semibold text-ink-800 dark:text-ink-100">
        <Calculator className="h-3.5 w-3.5 text-ai-600 dark:text-ai-400" /> {t('preview.title')}
      </p>

      {preview.applicationCount === 0 ? (
        <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">{t('preview.noApplications')}</p>
      ) : (
        <>
          <ul className="mt-1.5 space-y-0.5 text-xs text-ink-600 dark:text-ink-300">
            {preview.recomputeCount > 0 && (
              <li>
                {t('preview.recompute', {
                  count: preview.recomputeCount,
                  scoreChanged: preview.scoreChangedCount,
                  recChanged: preview.recommendationChangedCount,
                })}
              </li>
            )}
            {preview.gateFailCount + preview.gateReviewCount > 0 && (
              <li>{t('preview.gates', { fail: preview.gateFailCount, review: preview.gateReviewCount })}</li>
            )}
            {preview.aiRescoreCount > 0 && (
              <li className="flex items-start gap-1 text-ai-700 dark:text-ai-300">
                <Sparkles className="mt-0.5 h-3 w-3 shrink-0" />
                {t('preview.aiRescore', { count: preview.aiRescoreCount })}
              </li>
            )}
            {preview.pendingCount > 0 && <li>{t('preview.pending', { count: preview.pendingCount })}</li>}
          </ul>

          {rows.length > 0 && (
            <div className="mt-2 max-h-64 overflow-y-auto rounded-lg border border-ink-100 dark:border-white/10">
              <table className="w-full text-xs">
                <thead className="sticky top-0 bg-ink-50 text-[11px] uppercase tracking-wide text-ink-500 dark:bg-ink-900 dark:text-ink-400">
                  <tr>
                    <th className="px-2 py-1.5 text-left font-semibold">{t('preview.candidate')}</th>
                    <th className="px-2 py-1.5 text-left font-semibold">{t('preview.before')}</th>
                    <th className="px-2 py-1.5" />
                    <th className="px-2 py-1.5 text-left font-semibold">{t('preview.after')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-ink-100 dark:divide-white/10">
                  {rows.map((r) => (
                    <tr key={r.applicationId}>
                      <td className="max-w-[12rem] truncate px-2 py-1.5 text-ink-800 dark:text-ink-100">{r.candidateName || '—'}</td>
                      <td className="whitespace-nowrap px-2 py-1.5">
                        <span className={`font-semibold ${cvScoreTextClass(r.oldRecommendation)}`}>{r.oldScore ?? '—'}</span>{' '}
                        <span className="text-ink-500 dark:text-ink-400">{rec(r.oldRecommendation)}</span>
                      </td>
                      <td className="px-1 text-ink-300">
                        <ArrowRight className="h-3 w-3" />
                      </td>
                      <td className="whitespace-nowrap px-2 py-1.5">
                        {r.requiresAi ? (
                          <span className="text-ai-700 dark:text-ai-300">{t('preview.needsAi')}</span>
                        ) : (
                          <>
                            <span className={`font-semibold ${cvScoreTextClass(r.newRecommendation)}`}>{r.newScore ?? '—'}</span>{' '}
                            <span className="text-ink-500 dark:text-ink-400">{rec(r.newRecommendation)}</span>
                            {r.newGateStatus === 'fail' && (
                              <span className="ml-1 rounded-full bg-red-100 px-1.5 text-[10px] font-medium text-red-700 dark:bg-red-500/20 dark:text-red-400">
                                {t('status.gate.fail')}
                              </span>
                            )}
                            {r.newGateStatus === 'review' && (
                              <span className="ml-1 rounded-full bg-amber-100 px-1.5 text-[10px] font-medium text-amber-700 dark:bg-amber-500/20 dark:text-amber-400">
                                {t('status.gate.review')}
                              </span>
                            )}
                          </>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {preview.items.length > MAX_ROWS && (
            <p className="mt-1 text-[11px] text-ink-400">{t('preview.more', { count: preview.items.length - MAX_ROWS })}</p>
          )}
        </>
      )}
    </div>
  )
}
