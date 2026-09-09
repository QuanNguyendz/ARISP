import { apiClient } from '@ari/shared/api/apiClient'
import type { JdSectionKey } from '@ari/shared/fservices/jdTemplate'

/**
 * Bản mô tả công việc soạn theo mẫu công ty, gắn với MỘT phiếu yêu cầu tuyển dụng (ADR-064).
 *
 * Vòng đời: mở lần đầu → server trả bản khởi tạo **từ chính phiếu** (chưa lưu DB) → sửa → lưu →
 * xuất file → file gắn thẳng vào tin nháp, không phải tải xuống rồi tải lên lại.
 */

export interface JdDocument {
  id?: string | null
  recruitmentRequestId: string
  title: string
  department?: string | null
  employmentType?: string | null
  workMode?: string | null
  location?: string | null
  experienceLevel?: string | null
  vacancies?: number | null
  salaryMin?: number | null
  salaryMax?: number | null
  salaryCurrency?: string | null
  applicationDeadline?: string | null

  /** Nội dung từng mục, khoá là `key` của mục trong mẫu. */
  sections: Partial<Record<JdSectionKey, string>>

  generatedFileStorageKey?: string | null
  generatedFileName?: string | null
  generatedFormat?: string | null
  generatedFileViewUrl?: string | null
  generatedAt?: string | null
}

export type JdDocumentInput = Omit<
  JdDocument,
  | 'id'
  | 'recruitmentRequestId'
  | 'generatedFileStorageKey'
  | 'generatedFileName'
  | 'generatedFormat'
  | 'generatedFileViewUrl'
  | 'generatedAt'
>

export interface JdGeneratedFile {
  /** storageKey — gửi NGUYÊN TRẠNG trong payload tạo tin, không dùng để hiển thị. */
  storageKey: string
  /** URL mở được trên trình duyệt. */
  viewUrl: string
  fileName: string
  format: 'pdf' | 'docx'
}

export const jdDocumentService = {
  async get(requestId: string): Promise<JdDocument> {
    const { data } = await apiClient.get<JdDocument>(`/recruitment-requests/${requestId}/jd`)
    return data
  },

  async save(requestId: string, input: JdDocumentInput): Promise<void> {
    await apiClient.put(`/recruitment-requests/${requestId}/jd`, input)
  },

  /**
   * Gợi ý kỹ năng cho màn tạo tin, suy ra từ chính bản JD đã soạn.
   *
   * `POST` vì mỗi lượt là một lượt Gemini có tính phí. Danh sách rỗng KHÔNG phải lỗi — bản JD có thể
   * không nêu công nghệ nào cụ thể, và màn tạo tin chỉ việc để người dùng tự gõ như trước.
   */
  async suggestSkills(requestId: string): Promise<{ skills: string[]; jobCategory?: string | null }> {
    const { data } = await apiClient.post<{ skills: string[]; jobCategory?: string | null }>(
      `/recruitment-requests/${requestId}/jd/skills`
    )
    return data
  },

  async generate(requestId: string, format: 'pdf' | 'docx'): Promise<JdGeneratedFile> {
    const { data } = await apiClient.post<JdGeneratedFile>(
      `/recruitment-requests/${requestId}/jd/generate`,
      null,
      { params: { format } }
    )
    return data
  },
}
