import { apiClient } from '@ari/shared/api/apiClient'
import type { AssignableStaffRole } from '@ari/shared/utils/roles'

// ===== Types =====
export interface AdminStats {
  totalUsers: number
  activeUsers: number
  lockedUsers: number
  pendingRequests: number
  superAdmins: number
  hrAdmins: number
  recruiters: number
  hiringManagers: number
  candidates: number
}

export interface AdminUser {
  id: string
  email: string
  fullName?: string | null
  role: string
  isActive: boolean
  lockReason?: string | null
  createdAt: string

  /** ADR-065 — `departmentId` cho ô chọn, `department` là tên để hiển thị. */
  departmentId?: string | null
  department?: string | null
}

export interface AccountRequest {
  id: string
  batchId?: string | null
  email: string
  fullName: string
  role: string
  department?: string | null
  status: string
  reviewReason?: string | null
  requestedBy: string
  createdAt: string
  reviewedAt?: string | null
}

export interface PagedResult<T> {
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  items: T[]
}

export interface AuditLogEntry {
  id: string
  action: string
  entityType?: string | null
  entityId?: string | null
  metadata?: string | null
  actorName: string
  createdAt: string
}

export interface SystemSettingItem {
  key: string
  value: string
  description?: string | null
  updatedAt?: string
}

export interface CreateStaffPayload {
  email: string
  fullName: string
  role: AssignableStaffRole

  /** ADR-065: khoá đội, không còn chuỗi gõ tay. */
  departmentId?: string | null
}

export interface ListUsersParams {
  search?: string
  role?: string
  isActive?: boolean
  page?: number
  pageSize?: number
}

export const adminService = {
  // ----- Dashboard -----
  async getStats(): Promise<AdminStats> {
    const { data } = await apiClient.get<AdminStats>('/admin/stats')
    return data
  },

  // ----- Users -----
  async listUsers(params: ListUsersParams = {}): Promise<PagedResult<AdminUser>> {
    const { data } = await apiClient.get<PagedResult<AdminUser>>('/admin/users', { params })
    return data
  },

  async createStaff(payload: CreateStaffPayload): Promise<void> {
    await apiClient.post('/admin/users', payload)
  },

  async updateRole(id: string, role: AssignableStaffRole): Promise<void> {
    await apiClient.put(`/admin/users/${id}/role`, { role })
  },

  /** Gán/đổi đội (ADR-065). `null` = gỡ khỏi đội. */
  async updateDepartment(id: string, departmentId: string | null): Promise<void> {
    await apiClient.put(`/admin/users/${id}/department`, { departmentId })
  },

  /** Mở khóa tài khoản đã bị khóa. */
  async activateUser(id: string): Promise<void> {
    await apiClient.post(`/admin/users/${id}/activate`)
  },

  /** Khóa tài khoản — bắt buộc kèm lý do. */
  async deactivateUser(id: string, reason: string): Promise<void> {
    await apiClient.post(`/admin/users/${id}/deactivate`, { reason })
  },

  async deleteUser(id: string): Promise<void> {
    await apiClient.delete(`/admin/users/${id}`)
  },

  // ----- Account creation requests (Super Admin duyệt) -----
  async getAccountRequests(status = 'pending'): Promise<AccountRequest[]> {
    const { data } = await apiClient.get<AccountRequest[]>('/admin/account-requests', { params: { status } })
    return data
  },

  async approveAccountRequest(id: string): Promise<void> {
    await apiClient.post(`/admin/account-requests/${id}/approve`)
  },

  async rejectAccountRequest(id: string, reason: string): Promise<void> {
    await apiClient.post(`/admin/account-requests/${id}/reject`, { reason })
  },

  // ----- Audit logs -----
  async getAuditLogs(params: {
    action?: string
    entityType?: string
    page?: number
    pageSize?: number
  } = {}): Promise<PagedResult<AuditLogEntry>> {
    const { data } = await apiClient.get<PagedResult<AuditLogEntry>>('/admin/audit-logs', { params })
    return data
  },

  // ----- System settings -----
  async getSettings(): Promise<SystemSettingItem[]> {
    const { data } = await apiClient.get<SystemSettingItem[]>('/admin/settings')
    return data
  },

  async updateSettings(items: SystemSettingItem[]): Promise<void> {
    await apiClient.put('/admin/settings', items)
  },
}

export default adminService
