import { Navigate } from 'react-router-dom'
import { useAuthStore } from '@ari/shared/store/auth'

// Chuẩn hoá role về định dạng backend (snake_case) — đồng bộ ProtectedRoute / GuestRoute.
function normalizeRole(role: string): string {
  const r = role.toLowerCase().replace(/\s+/g, '_')
  if (r === 'superadmin') return 'super_admin'
  if (r === 'hradmin') return 'hr_admin'
  return r
}

const STAFF_HOME: Record<string, string> = {
  super_admin: '/super-admin/dashboard',
  hr_admin: '/hr/dashboard',
  recruiter: '/recruiter/dashboard',
}

/**
 * Trang gốc `/` của Staff site: đã đăng nhập thì đưa về dashboard theo role,
 * chưa đăng nhập thì về trang đăng nhập nội bộ (ADR-046 — thay cho StaffRedirect cũ).
 */
export default function StaffHomeRedirect() {
  const { isAuthenticated, isLoading, user } = useAuthStore()

  if (isLoading) {
    return null
  }

  if (isAuthenticated && user) {
    const home = STAFF_HOME[normalizeRole(user.role || '')]
    if (home) {
      return <Navigate to={home} replace />
    }
  }

  return <Navigate to="/auth/login" replace />
}
