import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuthStore } from '@store/auth/authStore'

// Chuẩn hoá role về định dạng backend (snake_case) — đồng bộ ProtectedRoute / GuestRoute
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
 * Bọc các trang công khai dành cho khách + ứng viên (job board, chi tiết việc làm, ứng tuyển).
 * Nếu phiên đăng nhập là STAFF (Super Admin / HR / Recruiter) → đưa thẳng về workspace của họ,
 * tránh việc staff lạc vào giao diện ứng viên (header ứng viên, "Hồ sơ của tôi"...).
 * Guest và Candidate vẫn xem bình thường.
 */
export default function StaffRedirect({ children }: { children: ReactNode }) {
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

  return <>{children}</>
}
