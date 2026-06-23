import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Users, Activity, UserCheck, Shield, UserPlus, ArrowRight, CheckCircle2 } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert } from '@components/shared'
import { useAuthStore } from '@store/auth/authStore'
import { adminService, type AdminStats, type AccountRequest, type AuditLogEntry } from '@services/admin'
import { auditActionLabel, roleLabel, roleBadgeClass, timeAgo } from '@utils/adminLabels'
import { DashboardSkeleton } from './_skeletons'

const initials = (name?: string | null) =>
  (name || 'U')
    .trim()
    .split(/\s+/)
    .map((n) => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

export default function SuperAdminDashboardPage() {
  const { user } = useAuthStore()
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [requests, setRequests] = useState<AccountRequest[]>([])
  const [logs, setLogs] = useState<AuditLogEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [approvingId, setApprovingId] = useState<string | null>(null)

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      const [s, p, l] = await Promise.all([
        adminService.getStats(),
        adminService.getAccountRequests('pending'),
        adminService.getAuditLogs({ page: 1, pageSize: 6 }),
      ])
      setStats(s)
      setRequests(p)
      setLogs(l.items)
    } catch (e: any) {
      setError(e?.response?.data?.message || e?.message || 'Không tải được dữ liệu tổng quan.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  const handleApprove = async (id: string) => {
    setApprovingId(id)
    try {
      await adminService.approveAccountRequest(id)
      await load()
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể duyệt yêu cầu.')
    } finally {
      setApprovingId(null)
    }
  }

  if (loading) return <DashboardSkeleton />

  const statCards = [
    { label: 'Tổng người dùng', value: stats?.totalUsers ?? 0, color: 'text-brand-600' },
    { label: 'YC chờ duyệt', value: stats?.pendingRequests ?? 0, color: 'text-amber-600' },
    { label: 'Bị khóa', value: stats?.lockedUsers ?? 0, color: 'text-red-600' },
    { label: 'Recruiter', value: stats?.recruiters ?? 0, color: 'text-emerald-600' },
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title={`Xin chào, ${user?.name || 'Super Admin'}`}
        description="Tổng quan quản trị hệ thống ARISP"
        actions={[{ label: 'Cài đặt hệ thống', href: '/super-admin/settings', variant: 'secondary' }]}
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <StatsGrid stats={statCards} />

      <div className="grid lg:grid-cols-3 gap-6 lg:gap-8">
        {/* Main column */}
        <div className="lg:col-span-2 space-y-6 lg:space-y-8">
          {/* Pending users */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
          >
            <div className="flex items-center justify-between mb-5">
              <div className="flex items-center gap-3">
                <span className="grid h-10 w-10 place-items-center rounded-xl bg-amber-100 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400">
                  <UserCheck className="w-5 h-5" />
                </span>
                <div>
                  <h2 className="text-base font-semibold text-ink-900 dark:text-white">Yêu cầu tạo tài khoản</h2>
                  <p className="text-xs text-ink-500 dark:text-ink-400">HR Leader gửi · chờ bạn duyệt</p>
                </div>
              </div>
              <Link to="/super-admin/users/pending" className="text-xs font-medium text-brand-600 dark:text-brand-400 hover:underline">
                Xem tất cả
              </Link>
            </div>

            {requests.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <CheckCircle2 className="w-8 h-8 text-emerald-500" />
                <p className="text-sm text-ink-500 dark:text-ink-400">Không có yêu cầu nào chờ duyệt</p>
              </div>
            ) : (
              <div className="space-y-3">
                {requests.slice(0, 4).map((r) => (
                  <div
                    key={r.id}
                    className="flex items-center justify-between gap-3 rounded-xl border border-ink-100 dark:border-white/10 bg-ink-50 dark:bg-white/5 p-3"
                  >
                    <div className="flex min-w-0 items-center gap-3">
                      <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                        {initials(r.fullName || r.email)}
                      </span>
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-ink-900 dark:text-white">{r.fullName || r.email}</p>
                        <p className="truncate text-xs text-ink-500 dark:text-ink-400">{r.email} · bởi {r.requestedBy}</p>
                      </div>
                    </div>
                    <div className="flex shrink-0 items-center gap-3">
                      <span className={`hidden sm:inline rounded-full px-2.5 py-0.5 text-xs font-medium ${roleBadgeClass(r.role)}`}>
                        {roleLabel(r.role)}
                      </span>
                      <button
                        onClick={() => handleApprove(r.id)}
                        disabled={approvingId === r.id}
                        className="rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
                      >
                        {approvingId === r.id ? 'Đang duyệt...' : 'Duyệt'}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </motion.div>

          {/* Recent audit logs */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
          >
            <div className="flex items-center justify-between mb-5">
              <div className="flex items-center gap-3">
                <span className="grid h-10 w-10 place-items-center rounded-xl bg-brand-100 dark:bg-brand-500/20 text-brand-600 dark:text-brand-400">
                  <Activity className="w-5 h-5" />
                </span>
                <div>
                  <h2 className="text-base font-semibold text-ink-900 dark:text-white">Nhật ký hoạt động</h2>
                  <p className="text-xs text-ink-500 dark:text-ink-400">Thay đổi gần đây trong hệ thống</p>
                </div>
              </div>
              <Link to="/super-admin/audit-logs" className="text-xs font-medium text-brand-600 dark:text-brand-400 hover:underline">
                Xem tất cả
              </Link>
            </div>

            {logs.length === 0 ? (
              <p className="py-8 text-center text-sm text-ink-500 dark:text-ink-400">Chưa có hoạt động nào</p>
            ) : (
              <div className="space-y-2">
                {logs.map((log) => (
                  <div
                    key={log.id}
                    className="flex items-center justify-between gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3"
                  >
                    <div className="flex min-w-0 items-center gap-3">
                      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-ink-100 dark:bg-white/5 text-ink-500 dark:text-ink-400">
                        <Activity className="w-4 h-4" />
                      </span>
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-ink-900 dark:text-white">{auditActionLabel(log.action)}</p>
                        <p className="truncate text-xs text-ink-500 dark:text-ink-400">bởi {log.actorName}</p>
                      </div>
                    </div>
                    <span className="shrink-0 text-xs text-ink-400">{timeAgo(log.createdAt)}</span>
                  </div>
                ))}
              </div>
            )}
          </motion.div>
        </div>

        {/* Sidebar column */}
        <div className="space-y-6">
          {/* Quick actions */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.15 }}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
          >
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">Thao tác nhanh</h2>
            <div className="space-y-2">
              {[
                { to: '/super-admin/users?create=1', icon: UserPlus, label: 'Tạo tài khoản staff', tint: 'text-emerald-600 dark:text-emerald-400 bg-emerald-100 dark:bg-emerald-500/20' },
                { to: '/super-admin/users/pending', icon: UserCheck, label: 'Duyệt user mới', tint: 'text-amber-600 dark:text-amber-400 bg-amber-100 dark:bg-amber-500/20' },
                { to: '/super-admin/users', icon: Users, label: 'Quản lý người dùng', tint: 'text-brand-600 dark:text-brand-400 bg-brand-100 dark:bg-brand-500/20' },
                { to: '/super-admin/audit-logs', icon: Activity, label: 'Xem audit logs', tint: 'text-ai-600 dark:text-ai-400 bg-ai-100 dark:bg-ai-500/20' },
              ].map((a) => (
                <Link
                  key={a.to}
                  to={a.to}
                  className="flex items-center gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3 hover:border-brand-300 dark:hover:border-brand-500/40 hover:bg-ink-50 dark:hover:bg-white/5"
                >
                  <span className={`grid h-8 w-8 place-items-center rounded-lg ${a.tint}`}>
                    <a.icon className="w-4 h-4" />
                  </span>
                  <span className="flex-1 text-sm text-ink-700 dark:text-ink-200">{a.label}</span>
                  <ArrowRight className="w-4 h-4 text-ink-400" />
                </Link>
              ))}
            </div>
          </motion.div>

          {/* System status */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
          >
            <div className="mb-4 flex items-center gap-2">
              <Shield className="w-4 h-4 text-ink-400" />
              <h2 className="text-sm font-semibold text-ink-900 dark:text-white">Phân bổ tài khoản</h2>
            </div>
            <div className="space-y-3 text-sm">
              {[
                { label: 'Super Admin', value: stats?.superAdmins ?? 0 },
                { label: 'HR Admin', value: stats?.hrAdmins ?? 0 },
                { label: 'Recruiter', value: stats?.recruiters ?? 0 },
                { label: 'Ứng viên', value: stats?.candidates ?? 0 },
              ].map((row) => (
                <div key={row.label} className="flex items-center justify-between">
                  <span className="text-ink-500 dark:text-ink-400">{row.label}</span>
                  <span className="font-semibold text-ink-900 dark:text-white">{row.value}</span>
                </div>
              ))}
            </div>
          </motion.div>
        </div>
      </div>
    </div>
  )
}
