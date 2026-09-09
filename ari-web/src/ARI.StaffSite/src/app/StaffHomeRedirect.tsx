import { Navigate } from 'react-router-dom'
import { useAuthStore } from '@ari/shared/store/auth'
import { STAFF_HOME, homePathForRole } from '@ari/shared/utils/roles'

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
    const home = homePathForRole(user.role, STAFF_HOME)
    if (home) {
      return <Navigate to={home} replace />
    }
  }

  return <Navigate to="/auth/login" replace />
}
