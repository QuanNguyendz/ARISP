import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuthStore } from '@ari/shared/store/auth'
import { homePathForRole } from '@ari/shared/utils/roles'

interface GuestRouteProps {
  children: ReactNode
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
    return <Navigate to={homePathForRole(user.role) || '/'} replace />
  }

  return <>{children}</>
}
