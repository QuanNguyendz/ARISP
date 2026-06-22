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

export default notificationService
