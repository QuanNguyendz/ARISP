/**
 * Từ vựng dùng chung cho các trường phân loại của tin tuyển dụng — hình thức làm việc, nơi làm việc
 * và cấp bậc.
 *
 * VÌ SAO GOM VỀ ĐÂY: ba danh sách này vốn ghi cứng trong `CreateJobPostingPage`. ADR-064 cần chúng ở
 * thêm hai màn nữa (phiếu yêu cầu tuyển dụng và trình soạn JD) — chép sang là có bốn bản, và lần
 * thêm một giá trị mới thì chắc chắn sót một chỗ. Đây là lần thứ tư trong dự án gặp đúng kiểu này
 * (`normalizeRole`, `statusMeta`, danh sách vai trò), nên gom ngay.
 *
 * `value` phải khớp đúng chuỗi backend lưu trong DB. `label` là nhãn hiển thị — cố ý để nguyên văn
 * như bản đang chạy để không đổi giao diện đã quen.
 *
 * ⚠️ Nhãn in vào FILE JD do **server** quyết định (`JdLayout.DisplayLabel`), không phải bảng này:
 * file được dựng ở backend nên không đọc được TypeScript. Hai nơi phải khớp nhau về nội dung.
 */

export interface JobOption {
  value: string
  label: string
}

export const EMPLOYMENT_TYPES: JobOption[] = [
  { value: 'full_time', label: 'Toàn thời gian' },
  { value: 'part_time', label: 'Bán thời gian' },
  { value: 'contract', label: 'Hợp đồng' },
  { value: 'internship', label: 'Thực tập' },
  { value: 'freelance', label: 'Freelance' },
]

export const WORK_MODES: JobOption[] = [
  { value: 'onsite', label: 'On-site' },
  { value: 'hybrid', label: 'Hybrid' },
  { value: 'remote', label: 'Remote' },
]

export const EXPERIENCE_LEVELS: JobOption[] = [
  { value: 'intern', label: 'Intern' },
  { value: 'fresher', label: 'Fresher' },
  { value: 'junior', label: 'Junior' },
  { value: 'middle', label: 'Middle' },
  { value: 'senior', label: 'Senior' },
  { value: 'lead', label: 'Lead' },
  { value: 'manager', label: 'Manager' },
]

/** Nhãn hiển thị của một giá trị; không nhận ra thì trả lại nguyên giá trị thay vì để trống. */
export function jobOptionLabel(options: JobOption[], value?: string | null): string {
  if (!value) return ''
  return options.find((o) => o.value === value)?.label ?? value
}
