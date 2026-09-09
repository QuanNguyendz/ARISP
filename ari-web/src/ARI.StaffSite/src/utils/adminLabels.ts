// Nhãn hiển thị tiếng Việt cho hành động audit log.
//
// Nhãn và badge của VAI TRÒ đã chuyển sang `@ari/shared/utils/roles` cùng với `normalizeRole`
// và bảng đường dẫn nhà: cả năm thứ đó phải biết cùng một tập vai trò, để rải chúng ra nhiều
// file là bảo đảm sẽ sót một chỗ khi thêm vai trò mới. Re-export để chỗ gọi cũ không phải đổi.
export { roleLabel, roleBadgeClass } from '@ari/shared/utils/roles'

const AUDIT_ACTION_LABELS: Record<string, string> = {
  user_approved: 'Duyệt tài khoản',
  user_role_updated: 'Đổi vai trò',
  staff_account_created: 'Tạo tài khoản staff',
  user_deactivated: 'Khóa tài khoản',
  user_activated: 'Kích hoạt tài khoản',
  user_deleted: 'Xóa tài khoản',
  system_settings_updated: 'Cập nhật cài đặt hệ thống',
}

export function auditActionLabel(action: string): string {
  return AUDIT_ACTION_LABELS[action] || action.replace(/_/g, ' ')
}

/** Khoảng thời gian tương đối kiểu "5 phút trước". */
export function timeAgo(iso: string): string {
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) return '—'
  const diff = Date.now() - then
  const min = Math.floor(diff / 60000)
  if (min < 1) return 'Vừa xong'
  if (min < 60) return `${min} phút trước`
  const hr = Math.floor(min / 60)
  if (hr < 24) return `${hr} giờ trước`
  const day = Math.floor(hr / 24)
  if (day < 30) return `${day} ngày trước`
  return new Date(iso).toLocaleDateString('vi-VN')
}
