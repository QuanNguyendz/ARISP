import OffersView from '@/components/offers/OffersView'

/**
 * Màn thư mời nhận việc của Hiring Manager — nơi duyệt mức lương và điều kiện trước khi thư rời
 * hệ thống. Cùng một component với HR Lead và Recruiter; nút nào hiện là do trạng thái thư và vai
 * trò người đang xem quyết định, không phải do trang.
 */
export default function HmOffersPage() {
  return <OffersView variant="hm" />
}
