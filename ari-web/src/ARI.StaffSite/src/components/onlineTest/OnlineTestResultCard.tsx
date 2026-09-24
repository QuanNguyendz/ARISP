import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ClipboardList } from 'lucide-react'
import { formatScore } from '@ari/shared/utils/format'
import OnlineTestAnswerSheetModal from '@/components/jobCandidates/OnlineTestAnswerSheetModal'

/**
 * Kết quả vòng trắc nghiệm trên màn hồ sơ ứng viên của nhân sự.
 *
 * <b>Vì sao có file này.</b> Bảng phễu của tin hiện "Chưa đạt · 40/100" kèm lối vào bài làm, nhưng mở
 * đúng hồ sơ đó ra thì không còn dấu vết nào của bài thi — cả ba màn chi tiết (HR / Recruiter / HM)
 * đều không kê nó. Mà vòng trắc nghiệm KHÔNG sinh `Evaluation`, nên khối "Kết quả phỏng vấn" cũng
 * trống ở đó: người chốt giữ hay loại đang nhìn một màn hình không có con số nào về bài thi.
 *
 * Một component dùng chung cho cả ba vai — chép thành ba bản là mở đường cho lần sửa sau chỉ sửa một.
 * Không tự dựng lại giao diện bài làm: mở đúng `OnlineTestAnswerSheetModal` mà bảng phễu vẫn dùng.
 */
export default function OnlineTestResultCard({
  applicationId,
  candidateName,
  score,
  passed,
  expired,
  className = '',
}: {
  applicationId: string
  candidateName: string
  score?: number | null
  passed?: boolean | null
  expired?: boolean | null
  className?: string
}) {
  const { t } = useTranslation('modules/staff/candidatePipeline')
  const [showSheet, setShowSheet] = useState(false)

  // Chưa nộp bài (hoặc vòng hiện tại không phải trắc nghiệm) thì KHÔNG dựng thẻ rỗng: một ô ghi
  // "chưa có dữ liệu" trên mọi hồ sơ chỉ làm loãng màn hình.
  if (score == null) return null

  return (
    <section className={className}>
      <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
        <ClipboardList className="h-4 w-4 text-brand-600 dark:text-brand-400" />
        {t('pipeline.roundTypes.onlineTest')}
      </h2>

      <div className="flex flex-wrap items-center gap-2">
        {/* Điểm và KẾT QUẢ đi cùng nhau — một con số trần bắt người đọc tự nhớ điểm sàn rồi tự so. */}
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold ${
            passed
              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
              : 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
          }`}
        >
          {passed ? t('pipeline.testPassed') : t('pipeline.testFailed')} · {formatScore(score)}
        </span>

        {/* "0 điểm do không làm" khác hẳn "0 điểm do làm sai hết" với người quyết định giữ hay loại. */}
        {expired && (
          <span className="inline-flex items-center rounded-full bg-ink-100 px-2.5 py-1 text-[11px] font-semibold text-ink-600 dark:bg-white/10 dark:text-ink-300">
            {t('pipeline.testExpiredAuto')}
          </span>
        )}

        <button
          type="button"
          onClick={() => setShowSheet(true)}
          className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
        >
          {t('pipeline.viewAnswers')}
        </button>
      </div>

      {showSheet && (
        <OnlineTestAnswerSheetModal
          applicationId={applicationId}
          candidateName={candidateName}
          onClose={() => setShowSheet(false)}
        />
      )}
    </section>
  )
}
