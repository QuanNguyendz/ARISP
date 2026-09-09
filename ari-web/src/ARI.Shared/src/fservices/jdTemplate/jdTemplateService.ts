import { apiClient } from '@ari/shared/api/apiClient'

/**
 * Mẫu bản mô tả công việc của công ty (ADR-064).
 *
 * Là **cấu hình có cấu trúc**, không phải một file .docx tải lên: hệ thống dựng bố cục cố định từ
 * các trường ở đây nên file xuất ra luôn hợp lệ và luôn có logo. HR Leader sửa được tiêu đề, thứ tự,
 * bật/tắt từng mục — riêng `key` là bất biến vì nội dung JD đã soạn tra theo đúng khoá đó.
 */

/** Khoá mục — phải khớp `JdSectionKeys` phía .NET. */
export const JD_SECTION_KEYS = [
  'description',
  'requirements',
  'niceToHave',
  'benefits',
  'workingTime',
  'process',
  'contact',
] as const

export type JdSectionKey = (typeof JD_SECTION_KEYS)[number]

export interface JdTemplateSection {
  key: JdSectionKey
  title: string
  hint?: string | null
  enabled: boolean
}

export interface JdTemplate {
  id?: string | null
  companyName: string
  companyAddress?: string | null
  companyWebsite?: string | null
  companyEmail?: string | null

  /** Tiêu đề canh giữa đầu văn bản, vd "THÔNG TIN TUYỂN DỤNG" — không phải tên vị trí. */
  documentTitle: string
  logoStorageKey?: string | null

  /** URL mở được trên trình duyệt — server tính, đừng dựng từ storageKey. */
  logoUrl?: string | null

  accentColor: string
  fontFamily: string
  footerNote?: string | null
  sections: JdTemplateSection[]
  updatedAt?: string | null
}

export type UpdateJdTemplateInput = Omit<
  JdTemplate,
  'id' | 'logoStorageKey' | 'logoUrl' | 'updatedAt'
>

export const jdTemplateService = {
  async get(): Promise<JdTemplate> {
    const { data } = await apiClient.get<JdTemplate>('/jd-template')
    return data
  },

  async update(input: UpdateJdTemplateInput): Promise<void> {
    await apiClient.put('/jd-template', input)
  },

  /**
   * Chỉ PNG/JPG — hai định dạng mà cả bộ dựng .docx lẫn .pdf đều đọc được.
   *
   * PHẢI khai `Content-Type` tường minh: `apiClient` đặt mặc định `application/json`, mà axios không
   * ghi đè default cho `FormData` — thiếu dòng này thì request multipart đi ra dưới dạng JSON và
   * `[Consumes("multipart/form-data")]` phía server trả **415**. Mọi upload khác trong repo đều khai
   * đúng như vậy.
   */
  async uploadLogo(file: File): Promise<string> {
    const form = new FormData()
    form.append('file', file)
    const { data } = await apiClient.post<{ logoUrl: string }>('/jd-template/logo', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data.logoUrl
  },
}
