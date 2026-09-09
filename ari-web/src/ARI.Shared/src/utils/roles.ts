/**
 * Từ vựng vai trò của toàn bộ frontend — chuẩn hoá, đường dẫn nhà, nhãn và badge.
 *
 * VÌ SAO GOM VỀ ĐÂY: `normalizeRole` từng tồn tại ở BA file với BA thân hàm khác nhau
 * (`ProtectedRoute`, `GuestRoute`, `StaffHomeRedirect`) cộng thêm hai bảng đường dẫn nhà gần
 * giống nhau và hai hàm nhãn ở `adminLabels.ts`. Thêm một vai trò nghĩa là sửa năm chỗ và chắc
 * chắn sót một — mà triệu chứng là người dùng đăng nhập xong bị đá về trang login, không có lỗi nào.
 *
 * Backend gửi role ở dạng claim JWT (`Hr_admin`); mọi so sánh ở frontend làm trên dạng snake_case
 * chữ thường (`hr_admin`) — xem `RoleNames` / `AppRoles` phía .NET.
 */

/** Vai trò ở dạng claim, đúng như backend gửi xuống. */
export const ROLE_CLAIMS = {
  SuperAdmin: 'Super_admin',
  HRAdmin: 'Hr_admin',
  Recruiter: 'Recruiter',
  HiringManager: 'Hiring_manager',
  Candidate: 'Candidate',
} as const

/** Vai trò ở dạng chuẩn hoá — dạng dùng cho MỌI phép so sánh trong frontend. */
export const ROLE = {
  SuperAdmin: 'super_admin',
  HRAdmin: 'hr_admin',
  Recruiter: 'recruiter',
  HiringManager: 'hiring_manager',
  Candidate: 'candidate',
} as const

export type NormalizedRole = (typeof ROLE)[keyof typeof ROLE]

/**
 * Vai trò mà giao diện quản lý tài khoản được phép cấp — GƯƠNG của `RoleNames.AssignableStaff`
 * phía .NET. Super Admin cố ý KHÔNG có ở đây: quyền quản trị tối cao không cấp qua màn hình.
 *
 * Vì sao là hằng số dùng chung chứ không viết tay ở từng màn: backend đã nhận `hiring_manager`
 * từ ADR-061 nhưng ba ô select ở màn Super Admin vẫn ghi cứng `['hr_admin', 'recruiter']`, nên
 * vai trò mới **không tạo được** — và tài khoản Hiring Manager tạo bằng đường khác thì ô vai trò
 * ở bảng hiện TRỐNG, vì `Select` không tìm thấy option khớp `value`. Hỏng im lặng, không lỗi nào.
 */
export const ASSIGNABLE_STAFF_ROLES = [ROLE.HRAdmin, ROLE.Recruiter, ROLE.HiringManager] as const

export type AssignableStaffRole = (typeof ASSIGNABLE_STAFF_ROLES)[number]

/** Các cách viết rời rạc từng gặp trong dữ liệu/URL, quy về một dạng. */
const ALIASES: Record<string, string> = {
  superadmin: ROLE.SuperAdmin,
  hradmin: ROLE.HRAdmin,
  hiringmanager: ROLE.HiringManager,
  hm: ROLE.HiringManager,
}

/** Chuẩn hoá role về dạng backend lưu DB (snake_case chữ thường). */
export function normalizeRole(role?: string | null): string {
  const r = (role || '').toLowerCase().trim().replace(/[\s-]+/g, '_')
  return ALIASES[r] ?? r
}

/**
 * Trang chủ theo vai trò. Ứng viên về `/` vì họ ở site khác (CandidateSite);
 * các vai trò nội bộ về dashboard trong StaffSite.
 */
export const ROLE_HOME: Record<string, string> = {
  [ROLE.SuperAdmin]: '/super-admin/dashboard',
  [ROLE.HRAdmin]: '/hr/dashboard',
  [ROLE.Recruiter]: '/recruiter/dashboard',
  [ROLE.HiringManager]: '/hm/dashboard',
  [ROLE.Candidate]: '/',
}

/** Trang chủ của riêng StaffSite — không có ứng viên, vì ứng viên không thuộc site này. */
export const STAFF_HOME: Record<string, string> = {
  [ROLE.SuperAdmin]: ROLE_HOME[ROLE.SuperAdmin],
  [ROLE.HRAdmin]: ROLE_HOME[ROLE.HRAdmin],
  [ROLE.Recruiter]: ROLE_HOME[ROLE.Recruiter],
  [ROLE.HiringManager]: ROLE_HOME[ROLE.HiringManager],
}

/** Đường dẫn nhà theo vai trò, `undefined` nếu vai trò không thuộc bảng đã cho. */
export function homePathForRole(role?: string | null, table: Record<string, string> = ROLE_HOME) {
  return table[normalizeRole(role)]
}

const ROLE_LABELS: Record<string, string> = {
  [ROLE.SuperAdmin]: 'Super Admin',
  [ROLE.HRAdmin]: 'HR Admin',
  [ROLE.Recruiter]: 'Recruiter',
  [ROLE.HiringManager]: 'Hiring Manager',
  [ROLE.Candidate]: 'Ứng viên',
}

export function roleLabel(role?: string | null): string {
  return ROLE_LABELS[normalizeRole(role)] || role || '—'
}

const ROLE_BADGES: Record<string, string> = {
  [ROLE.SuperAdmin]: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400',
  [ROLE.HRAdmin]: 'bg-ai-100 text-ai-700 dark:bg-ai-500/20 dark:text-ai-400',
  [ROLE.Recruiter]: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  [ROLE.HiringManager]: 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-400',
  [ROLE.Candidate]: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
}

export function roleBadgeClass(role?: string | null): string {
  return ROLE_BADGES[normalizeRole(role)] || 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
}
