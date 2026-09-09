import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuthStore } from '@ari/shared/store/auth'
import { ROLE, ROLE_HOME, normalizeRole } from '@ari/shared/utils/roles'

interface ProtectedRouteProps {
  allowedRoles: string[]
  children: ReactNode
}

export default function ProtectedRoute({ allowedRoles, children }: ProtectedRouteProps) {
  const location = useLocation()
  const { isAuthenticated, isLoading, user } = useAuthStore()

  if (isLoading) {
    return null
  }

  const normalizedAllowedRoles = allowedRoles.map((role) => normalizeRole(role))
  const userRole = normalizeRole(user?.role || '')

  // Get appropriate login path based on route type
  const isCandidateRoute = normalizedAllowedRoles.includes(ROLE.Candidate)
  const loginPath = isCandidateRoute ? '/auth/candidate-login' : '/auth/login'

  // Not authenticated
  if (!isAuthenticated || !userRole) {
    return <Navigate to={loginPath} replace state={{ from: location }} />
  }

  // Check if user has permission
  const isAllowed = normalizedAllowedRoles.includes(userRole)
  if (!isAllowed) {
    // Redirect to user's own dashboard based on their role
    const userDashboard = ROLE_HOME[userRole] || '/auth/login'
    return <Navigate to="/403" replace state={{ from: location, redirectTo: userDashboard }} />
  }

  return <>{children}</>
}
