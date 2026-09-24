import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { ShieldCheck } from 'lucide-react'
import { isKnockout } from '@ari/shared/fservices/cvRubric'
import type { CvRubricCriterion } from '@ari/shared/fservices/cvRubric'

interface CvRubricChipsProps {
  criteria: CvRubricCriterion[]
  /** Bấm vào dải chip để mở bản đầy đủ. */
  onExpand: () => void
  title: string
  /** Phần đuôi dòng (vd "đang chấm lại N hồ sơ"). */
  trailing?: ReactNode
}

/**
 * Bộ tiêu chí ở dạng thu gọn: một dòng tên tiêu chí + trọng số — đủ để nhớ tin chấm theo gì mà không chiếm
 * chỗ. Dùng chung cho panel ở màn tin và khối chỉ đọc ở màn tạo tin để hai nơi thu gọn trông như nhau.
 * Điều kiện bắt buộc hiện nhãn "Bắt buộc" thay cho trọng số; tiêu chí có điểm tối thiểu kèm "≥ N" (ADR-075).
 */
export default function CvRubricChips({ criteria, onExpand, title, trailing }: CvRubricChipsProps) {
  const { t } = useTranslation('modules/staff/cvScoring')
  return (
    <button type="button" onClick={onExpand} className="flex w-full flex-wrap items-center gap-1.5 text-left" title={title}>
      {criteria.map((c, i) => (
        <span
          key={c.key ?? i}
          className="inline-flex items-center gap-1 rounded-full bg-ink-100 px-2 py-0.5 text-xs text-ink-700 dark:bg-white/10 dark:text-ink-200"
        >
          {isKnockout(c) && <ShieldCheck className="h-3 w-3 text-amber-600 dark:text-amber-400" />}
          {c.name}
          {isKnockout(c) ? (
            <span className="font-semibold text-amber-700 dark:text-amber-300">{t('editor.kind.badge')}</span>
          ) : (
            <span className="font-semibold text-ai-700 dark:text-ai-300">
              {c.weight}%{c.minScore != null && ` · ≥ ${c.minScore}`}
            </span>
          )}
        </span>
      ))}
      {trailing}
    </button>
  )
}
