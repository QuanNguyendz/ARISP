import type { MyEvalCriterion } from '@ari/shared/types/application'
import { scoreColor, type TFn } from '@pages/candidate/_reportUi'

/**
 * Thanh điểm một tiêu chí đánh giá (0–100). Nhãn tiêu chí lấy từ `criterionLabels.*`
 * của namespace i18n mà page gọi truyền vào qua `t`.
 */
export default function CriterionBar({ c, t }: { c: MyEvalCriterion; t: TFn }) {
  const pct = Math.max(0, Math.min(100, Math.round(c.score)))
  // Bộ khoá chuẩn (prompt đã ghim) + các biến thể model hay trả về ("Technical Skills",
  // "Cultural Fit"…) — thiếu alias là nhãn rơi về tiếng Anh thô giữa màn tiếng Việt.
  const criterionLabels: Record<string, string> = {
    technical: t('criterionLabels.technical'),
    technical_knowledge: t('criterionLabels.technical_knowledge'),
    technical_skills: t('criterionLabels.technical'),
    communication: t('criterionLabels.communication'),
    communication_skills: t('criterionLabels.communication'),
    problem_solving: t('criterionLabels.problem_solving'),
    problem_solving_ability: t('criterionLabels.problem_solving'),
    problem_solving_skills: t('criterionLabels.problem_solving'),
    culture_fit: t('criterionLabels.culture_fit'),
    cultural_fit: t('criterionLabels.culture_fit'),
    experience: t('criterionLabels.experience'),
    practical_experience: t('criterionLabels.practical_experience'),
    language: t('criterionLabels.language'),
    language_proficiency: t('criterionLabels.language'),
    attitude: t('criterionLabels.attitude'),
    teamwork: t('criterionLabels.teamwork'),
  }
  const key = c.name.trim().toLowerCase().replace(/\s+/g, '_')
  const label =
    criterionLabels[key] ||
    c.name.replace(/_/g, ' ').replace(/^\w/, (ch: string) => ch.toUpperCase())

  return (
    <div>
      <div className="mb-1 flex items-center justify-between text-sm">
        <span className="font-medium text-ink-700">{label}</span>
        <span className="font-semibold text-ink-900">{pct}/100</span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-ink-100">
        <div className={`h-full rounded-full ${scoreColor(pct)}`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}
