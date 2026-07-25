import { apiClient } from '@ari/shared/api/apiClient'

export interface NotificationItem {
  id: string
  type: string // invite | result | pending | schedule | applied | system
  title: string
  body?: string | null
  link?: string | null
  isRead: boolean
  createdAt: string
}

export interface NotificationListResult {
  items: NotificationItem[]
  unreadCount: number
}

/**
 * Sự kiện DOM phát khi có push SignalR liên quan tới thông báo nhân sự
 * (ứng viên mới, đánh giá chờ duyệt, duyệt/từ chối tin...). Layout nhân sự
 * lắng nghe sự kiện này để tải lại chuông tức thời thay vì chỉ khi đổi route.
 */
export const STAFF_NOTIF_REFRESH_EVENT = 'staff-notifications:refresh'

/**
 * Sự kiện DOM phát khi ứng viên nhận push SignalR (`ReceiveUserNotification`) — ví dụ HR cấp mã
 * phỏng vấn, đặt lịch, kết quả vòng. Các trang candidate dùng state cục bộ (không qua react-query,
 * ví dụ `candidate/ApplicationsPage`) lắng nghe để refetch nền, cập nhật bảng tức thời.
 */
export const CANDIDATE_DATA_REFRESH_EVENT = 'candidate-data:refresh'

/**
 * Sự kiện DOM phát khi ứng viên nộp bài thi trắc nghiệm (push SignalR `ReceiveOnlineTestSubmitted`).
 * Trang bảng điểm trắc nghiệm của nhân sự (`JobOnlineTestResultsPage`, dùng state cục bộ) lắng nghe
 * để tải lại danh sách điểm tức thời.
 */
export const STAFF_ONLINE_TEST_REFRESH_EVENT = 'staff-online-test:refresh'

/**
 * Chuẩn hoá link thông báo về route hiện hành. Xử lý dữ liệu tồn đọng của thông báo "Phân tích CV
 * hoàn tất": các link cũ `/candidate/find-jobs/{id}/apply` (route không tồn tại) và `/jobs/{id}/apply`
 * (trang nộp đơn — không hiển thị kết quả phân tích) → đưa về `/jobs/{id}` (trang chi tiết tin, nơi
 * hiện kết quả CV-JD).
 */
export function resolveNotifLink(link?: string | null): string | undefined {
  if (!link) return undefined
  const cvAnalysis = link.match(/^\/(?:candidate\/find-jobs|jobs)\/([^/]+)\/apply$/)
  if (cvAnalysis) return `/jobs/${cvAnalysis[1]}`
  return link
}

export const notificationService = {
  async list(): Promise<NotificationListResult> {
    const { data } = await apiClient.get<NotificationListResult>('/portal/notifications')
    return data
  },
  async markAllRead(): Promise<void> {
    await apiClient.post('/portal/notifications/read-all')
  },
  async markRead(id: string): Promise<void> {
    await apiClient.post(`/portal/notifications/${id}/read`)
  },
  async remove(id: string): Promise<void> {
    await apiClient.delete(`/portal/notifications/${id}`)
  },
  async clearAll(): Promise<void> {
    await apiClient.delete('/portal/notifications')
  },
}

/** Thông báo cho nhân sự nội bộ (HR Admin / Recruiter / Super Admin) — endpoint tách riêng. */
export const staffNotificationService = {
  async list(): Promise<NotificationListResult> {
    const { data } = await apiClient.get<NotificationListResult>('/staff/notifications')
    return data
  },
  async markAllRead(): Promise<void> {
    await apiClient.post('/staff/notifications/read-all')
  },
  async markRead(id: string): Promise<void> {
    await apiClient.post(`/staff/notifications/${id}/read`)
  },
  async remove(id: string): Promise<void> {
    await apiClient.delete(`/staff/notifications/${id}`)
  },
  async clearAll(): Promise<void> {
    await apiClient.delete('/staff/notifications')
  },
}

export default notificationService
