import { Navigate, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { useAuthStore } from '@store/auth/authStore';

interface ProtectedRouteProps {
  allowedRoles: string[];
  children: ReactNode;
}

const ADMIN_ROLES = ['super_admin', 'hr_admin', 'recruiter'];

export default function ProtectedRoute({ allowedRoles, children }: ProtectedRouteProps) {
  const location = useLocation();
  const { isAuthenticated, isLoading, user } = useAuthStore();

  if (isLoading) {
    return null;
  }

  const normalizedAllowedRoles = allowedRoles.map((role) => role.toLowerCase());
  const userRole = user?.role?.toLowerCase() ?? '';
  const isCandidateRoute = normalizedAllowedRoles.includes('candidate');
  const loginPath = isCandidateRoute ? '/auth/candidate-login' : '/auth/login';

  if (!isAuthenticated || !userRole) {
    return <Navigate to={loginPath} replace state={{ from: location }} />;
  }

  const isAllowed = normalizedAllowedRoles.includes(userRole);
  if (!isAllowed) {
    const defaultRedirect = ADMIN_ROLES.includes(userRole) ? '/admin/dashboard' : '/candidate/portal';
    return <Navigate to="/403" replace state={{ from: location, redirectTo: defaultRedirect }} />;
  }

  return <>{children}</>;
}
