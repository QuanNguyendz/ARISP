/** Namespace i18n dùng chung cho khối "Kết quả phỏng vấn" và mục "Buổi vừa kết thúc" (ADR-069). */
export const INTERVIEW_RESULTS_NS = 'modules/staff/interviewResults'

/**
 * Khoá query nằm DƯỚI `['evaluations']`: realtime (phiên phỏng vấn / báo cáo đổi) đã làm mới tiền tố đó
 * trong `useAppNotifications`, nên "AI đang chấm" tự thành "Xem đánh giá" mà không cần thêm nhánh nào.
 */
export const interviewResultsKey = (applicationId: string) => ['evaluations', 'interviews', applicationId]

export const recentInterviewsKey = ['evaluations', 'recent-interviews'] as const
