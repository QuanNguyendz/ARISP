import { apiClient } from '@ari/shared/api/apiClient'

/**
 * Đội/bộ phận của công ty (ADR-065).
 *
 * Đây là dữ liệu **tổ chức**, không phải trục phân quyền — ai được quyết định về một tin vẫn do đội
 * tuyển dụng của tin đó trả lời (ADR-061). Bảng này chỉ trả lời "phiếu yêu cầu tuyển dụng của đội nào",
 * và tồn tại vì trước đây phòng ban là chuỗi tự do mà nhân viên còn tự sửa được — nên Hiring Manager
 * của đội A lập được phiếu ghi đội B.
 */

export interface Department {
  id: string
  name: string
  code?: string | null
  description?: string | null
  isActive: boolean

  /** Số tài khoản đang thuộc đội — cảnh báo trước khi tắt một đội còn người. */
  memberCount: number
  createdAt: string
}

export interface DepartmentInput {
  name: string
  code?: string | null
  description?: string | null
  isActive: boolean
}

export const departmentService = {
  /** `activeOnly` khi dùng cho ô chọn: đội đã tắt không được gán mới. */
  async list(activeOnly = false): Promise<Department[]> {
    const { data } = await apiClient.get<Department[]>('/departments', { params: { activeOnly } })
    return data
  },

  async create(input: DepartmentInput): Promise<string> {
    const { data } = await apiClient.post<{ id: string }>('/departments', input)
    return data.id
  },

  async update(id: string, input: DepartmentInput): Promise<void> {
    await apiClient.put(`/departments/${id}`, input)
  },
}
