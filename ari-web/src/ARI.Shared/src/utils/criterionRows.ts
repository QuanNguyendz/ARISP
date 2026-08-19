import type { EvaluationReport } from '../types/evaluation/evaluation.types';

/** Một dòng điểm tiêu chí đã chuẩn hoá để vẽ thanh điểm. */
export interface CriterionRow {
  key: string;
  /** Tên hiển thị: nhãn doanh nghiệp đặt nếu có, không thì khoá đã làm đẹp. */
  label: string;
  score: number;
  /** Trọng số (%) — chỉ có ở bản chấm theo rubric doanh nghiệp. */
  weight?: number | null;
}

/** "problem_solving" → "Problem solving" (chỉ dùng khi không có nhãn doanh nghiệp). */
function humanize(key: string): string {
  const spaced = key.replace(/_/g, ' ').trim();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/**
 * Chuẩn hoá điểm tiêu chí của một bản đánh giá để hiển thị (ADR-060).
 *
 * Ưu tiên `criterionDetails` (có nhãn doanh nghiệp + trọng số); bản đánh giá chấm TRƯỚC khi tin khai
 * bộ tiêu chí chỉ có `criterionScores` dạng phẳng nên vẫn phải đọc được — nếu không, mở lại hồ sơ cũ
 * là bảng điểm trống trơn.
 */
export function toCriterionRows(
  evaluation?: Pick<EvaluationReport, 'criterionScores' | 'criterionDetails'> | null
): CriterionRow[] {
  if (!evaluation) return [];

  if (evaluation.criterionDetails?.length) {
    return evaluation.criterionDetails.map((c) => ({
      key: c.key,
      label: c.label?.trim() || humanize(c.key),
      score: c.score,
      weight: c.weight,
    }));
  }

  return Object.entries(evaluation.criterionScores ?? {}).map(([key, score]) => ({
    key,
    label: humanize(key),
    score,
  }));
}
