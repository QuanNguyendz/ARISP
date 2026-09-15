import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import { AlertTriangle, Check, Loader2, X } from 'lucide-react'
import { onlineTestService } from '@ari/shared/fservices/onlineTest'
import type { OnlineTestAnswerSheet } from '@ari/shared/types/onlineTest'

/**
 * Bài làm chi tiết của một ứng viên ở vòng trắc nghiệm.
 *
 * <b>Vì sao cần màn này.</b> Điểm tổng chỉ nói "30/100" — nó không trả lời được câu mà người sàng
 * lọc thật sự hỏi: *sai ở đâu*. Một ứng viên trượt vì sai hết phần thuật toán khác hẳn người trượt
 * rải đều, và khác hẳn người bỏ trắng nửa bài. Không có màn này thì quyết định giữ hay loại chỉ dựa
 * vào một con số.
 *
 * Đáp án đúng chỉ đi qua đường NHÂN SỰ — endpoint gác bằng cổng quản lý ngân hàng đề.
 */
interface Props {
  applicationId: string
  candidateName: string
  onClose: () => void
}

const OPTION_LABEL = 'ABCDEFGH'

export default function OnlineTestAnswerSheetModal({ applicationId, candidateName, onClose }: Props) {
  const { t } = useTranslation('modules/staff/candidatePipeline')
  const [sheet, setSheet] = useState<OnlineTestAnswerSheet | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let alive = true
    ;(async () => {
      try {
        const data = await onlineTestService.getAnswerSheet(applicationId)
        if (alive) setSheet(data)
      } catch (e) {
        const x = e as { response?: { data?: { message?: string } } }
        if (alive) setError(x?.response?.data?.message || t('answerSheet.loadError'))
      } finally {
        if (alive) setLoading(false)
      }
    })()
    return () => {
      alive = false
    }
  }, [applicationId, t])

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 sm:p-8"
      onClick={onClose}
    >
      <motion.div
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        onClick={(e) => e.stopPropagation()}
        className="w-full max-w-3xl rounded-2xl border border-ink-200 bg-white shadow-xl dark:border-white/10 dark:bg-ink-900"
      >
        <div className="flex items-start justify-between gap-3 border-b border-ink-100 px-5 py-4 dark:border-white/10">
          <div className="min-w-0">
            <h2 className="text-base font-semibold text-ink-900 dark:text-white">
              {t('answerSheet.title')}
            </h2>
            <p className="truncate text-sm text-ink-500 dark:text-ink-400">{candidateName}</p>
          </div>
          <button
            onClick={onClose}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
            aria-label={t('answerSheet.close')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="max-h-[70vh] overflow-y-auto px-5 py-4">
          {loading ? (
            <div className="flex items-center gap-2 py-10 text-sm text-ink-500">
              <Loader2 className="h-4 w-4 animate-spin" /> {t('answerSheet.loading')}
            </div>
          ) : error ? (
            <p className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
              {error}
            </p>
          ) : !sheet ? (
            <p className="py-10 text-center text-sm text-ink-500 dark:text-ink-400">
              {t('answerSheet.notTaken')}
            </p>
          ) : (
            <>
              {/* Tóm tắt: điểm, đạt/chưa đạt so với điểm sàn, số câu đúng. */}
              <div className="mb-4 flex flex-wrap items-center gap-2">
                <span
                  className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-semibold ${
                    sheet.isPassed
                      ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                      : 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
                  }`}
                >
                  {sheet.isPassed ? t('answerSheet.passed') : t('answerSheet.notPassed')} ·{' '}
                  {Math.round(sheet.score)}/100
                </span>
                <span className="text-xs text-ink-500 dark:text-ink-400">
                  {t('answerSheet.passScore', { score: sheet.passScore })}
                </span>
                <span className="text-xs text-ink-500 dark:text-ink-400">
                  {t('answerSheet.correctCount', {
                    correct: sheet.correctCount,
                    total: sheet.totalQuestions,
                  })}
                </span>
                {sheet.tabSwitchCount > 0 && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-1 text-xs font-medium text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
                    <AlertTriangle className="h-3 w-3" />
                    {t('answerSheet.tabSwitch', { count: sheet.tabSwitchCount })}
                  </span>
                )}
              </div>

              {/* Bài hệ thống nộp thay khi hết hạn: mọi câu đều trống vì ứng viên CHƯA HỀ vào làm —
                  nói trước khi người đọc lướt xuống một danh sách toàn "bỏ trắng" và tưởng họ bỏ bài. */}
              {sheet.expired && (
                <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-800 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">
                  {t('answerSheet.expiredNotice')}
                </div>
              )}

              <ol className="space-y-3">
                {sheet.items.map((item, idx) => {
                  const blank = item.selectedOptions.length === 0
                  return (
                    <li
                      key={item.questionId}
                      className={`rounded-xl border p-3 ${
                        item.isCorrect
                          ? 'border-emerald-200 bg-emerald-50/40 dark:border-emerald-500/25 dark:bg-emerald-500/5'
                          : 'border-red-200 bg-red-50/40 dark:border-red-500/25 dark:bg-red-500/5'
                      }`}
                    >
                      <div className="mb-2 flex items-start gap-2">
                        <span className="text-xs font-bold text-ink-400">{idx + 1}.</span>
                        <p className="min-w-0 flex-1 text-sm font-medium text-ink-900 dark:text-white">
                          {item.questionText}
                        </p>
                        <span
                          className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                            item.isCorrect
                              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                              : 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
                          }`}
                        >
                          {item.isCorrect
                            ? t('answerSheet.right')
                            : blank
                              ? /* Bỏ trắng KHÁC chọn sai — và phân biệt được hai thứ đó là
                                   nửa lý do màn này tồn tại. */
                                t('answerSheet.blank')
                              : t('answerSheet.wrong')}
                        </span>
                      </div>

                      <ul className="space-y-1 pl-6">
                        {item.options.map((opt, i) => {
                          const picked = item.selectedOptions.includes(i)
                          const right = item.correctOptions.includes(i)
                          return (
                            <li
                              key={i}
                              className={`flex items-center gap-2 rounded-lg px-2 py-1 text-sm ${
                                right
                                  ? 'bg-emerald-100/70 text-emerald-800 dark:bg-emerald-500/15 dark:text-emerald-300'
                                  : picked
                                    ? 'bg-red-100/70 text-red-800 dark:bg-red-500/15 dark:text-red-300'
                                    : 'text-ink-600 dark:text-ink-300'
                              }`}
                            >
                              <span className="w-4 shrink-0 text-xs font-bold">
                                {OPTION_LABEL[i] ?? i + 1}
                              </span>
                              <span className="min-w-0 flex-1">{opt}</span>
                              {picked && (
                                <span className="shrink-0 text-[11px] font-semibold">
                                  {t('answerSheet.picked')}
                                </span>
                              )}
                              {right && <Check className="h-3.5 w-3.5 shrink-0" />}
                            </li>
                          )
                        })}
                      </ul>
                    </li>
                  )
                })}
              </ol>
            </>
          )}
        </div>
      </motion.div>
    </div>
  )
}
