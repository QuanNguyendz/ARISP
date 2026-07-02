import { apiClient } from '../apiClient'

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
