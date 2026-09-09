import RecruitmentRequestsView from '@/components/recruitmentRequests/RecruitmentRequestsView'

/**
 * Phiếu yêu cầu tuyển dụng (ADR-063). Server đã lọc phạm vi theo vai trò người gọi và trả kèm cờ
 * quyền từng phiếu, nên ba khu vực dùng chung một view — xem chú thích ở component.
 */
export default function RecruitmentRequestsPage() {
  return <RecruitmentRequestsView />
}
