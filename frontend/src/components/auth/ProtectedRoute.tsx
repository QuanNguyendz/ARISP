import { Navigate, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { useAuthStore } from '@store/auth/authStore';

interface ProtectedRouteProps {
  allowedRoles: string[];
  children: ReactNode;
}

// Normalize role to match backend format (with underscore)
function normalizeRole(role: string): string {
  const r = role.toLowerCase().replace(/\s+/g, '_');
  // Handle common variations
  if (r === 'superadmin') return 'super_admin';
  if (r === 'hradmin') return 'hr_admin';
  if (r === 'recruiter') return 'recruiter';
  if (r === 'candidate') return 'candidate';
  return r;
}

const ROLE_DASHBOARD_MAP: Record<string, string> = {
  super_admin: '/super-admin/dashboard',
  hr_admin: '/hr/dashboard',
  recruiter: '/recruiter/dashboard',
  candidate: '/candidate/portal',
};

export default function ProtectedRoute({ allowedRoles, children }: ProtectedRouteProps) {
  const location = useLocation();
  const { isAuthenticated, isLoading, user } = useAuthStore();

  if (isLoading) {
    return null;
  }

  const normalizedAllowedRoles = allowedRoles.map((role) => normalizeRole(role));
  const userRole = normalizeRole(user?.role || '');
  
  // Get appropriate login path based on route type
  const isCandidateRoute = normalizedAllowedRoles.includes('candidate');
  const loginPath = isCandidateRoute ? '/auth/candidate-login' : '/auth/login';

  // Not authenticated
  if (!isAuthenticated || !userRole) {
    return <Navigate to={loginPath} replace state={{ from: location }} />;
  }

  // Check if user has permission
  const isAllowed = normalizedAllowedRoles.includes(userRole);
  if (!isAllowed) {
    // Redirect to user's own dashboard based on their role
    const userDashboard = ROLE_DASHBOARD_MAP[userRole] || '/auth/login';
    return <Navigate to="/403" replace state={{ from: location, redirectTo: userDashboard }} />;
  }

  return <>{children}</>;
}
