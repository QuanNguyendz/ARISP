/**
 * Helper hiển thị dùng chung cho các màn "báo cáo AI" của ứng viên: chi tiết hồ sơ ứng tuyển
 * (ApplicationDetailPage) và xem lại buổi phỏng vấn thử (PracticeReviewPage — ADR-051).
 */

/** Hàm dịch truyền xuống component con (mỗi page dùng namespace riêng của mình). */
export type TFn = (key: string, opts?: Record<string, unknown>) => string

export function scoreColor(score: number): string {
  if (score >= 80) return 'bg-emerald-500'
  if (score >= 60) return 'bg-amber-500'
  return 'bg-red-500'
}

export function formatDate(iso?: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return d.toLocaleDateString()
}

export function formatDuration(seconds?: number | null): string {
  if (!seconds || seconds <= 0) return ''
  const m = Math.round(seconds / 60)
  return `${m}m`
}

export function langLevel(overall: number): string {
  if (overall >= 9) return 'C1+'
  if (overall >= 8) return 'B2+'
  if (overall >= 6.5) return 'B2'
  if (overall >= 5) return 'B1'
  return 'A2'
}
