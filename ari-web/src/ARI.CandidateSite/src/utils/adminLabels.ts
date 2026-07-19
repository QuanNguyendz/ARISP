// Nhãn hiển thị tiếng Việt cho role nội bộ + hành động audit log.

export function roleLabel(role?: string | null): string {
  const r = (role || '').toLowerCase().replace(/\s+/g, '_')
  switch (r) {
    case 'super_admin':
      return 'Super Admin'
    case 'hr_admin':
      return 'HR Admin'
    case 'recruiter':
      return 'Recruiter'
    case 'candidate':
      return 'Ứng viên'
    default:
      return role || '—'
  }
}

export function roleBadgeClass(role?: string | null): string {
  const r = (role || '').toLowerCase().replace(/\s+/g, '_')
  switch (r) {
    case 'super_admin':
      return 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
    case 'hr_admin':
      return 'bg-ai-100 text-ai-700 dark:bg-ai-500/20 dark:text-ai-400'
    case 'recruiter':
      return 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400'
    case 'candidate':
      return 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
    default:
      return 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
  }
}

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
