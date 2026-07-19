import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuthStore } from '@store/auth/authStore'

interface GuestRouteProps {
  children: ReactNode
}

// Chuẩn hoá role về định dạng backend (snake_case) — đồng bộ với ProtectedRoute
function normalizeRole(role: string): string {
  const r = role.toLowerCase().replace(/\s+/g, '_')
  if (r === 'superadmin') return 'super_admin'
  if (r === 'hradmin') return 'hr_admin'
  return r
}

const ROLE_HOME: Record<string, string> = {
  super_admin: '/super-admin/dashboard',
  hr_admin: '/hr/dashboard',
  recruiter: '/recruiter/dashboard',
  candidate: '/',
}

/**
 * Guard cho các màn chỉ dành cho khách (chưa đăng nhập): login, register...
 * Nếu đã đăng nhập mà cố vào các màn này → chuyển hướng về home theo role,
 * tránh tình trạng đổi URL để quay lại màn đăng nhập khi phiên còn hiệu lực.
 */
export default function GuestRoute({ children }: GuestRouteProps) {
  const { isAuthenticated, isLoading, user } = useAuthStore()

  if (isLoading) {
    return null
  }

  if (isAuthenticated && user) {
    const home = ROLE_HOME[normalizeRole(user.role || '')] || '/'
    return <Navigate to={home} replace />
  }

  return <>{children}</>
}
