import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import {
  Users,
  Search,
  UserPlus,
  Lock,
  Unlock,
  Trash2,
  X,
  Loader2,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert } from '@components/shared'
import { useAuthStore } from '@store/auth/authStore'
import { adminService, type AdminUser, type AdminStats, type CreateStaffPayload } from '@services/admin'
import { roleLabel, roleBadgeClass } from '@utils/adminLabels'
import { StatsGridSkeleton, TableSkeleton } from './_skeletons'

const PAGE_SIZE = 10

const initials = (name?: string | null) =>
  (name || 'U')
    .trim()
    .split(/\s+/)
    .map((n) => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

export default function UsersPage() {
  const currentUserId = useAuthStore((s) => s.user?.id)
  const [searchParams, setSearchParams] = useSearchParams()

  const [users, setUsers] = useState<AdminUser[]>([])
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [roleFilter, setRoleFilter] = useState('all')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [lockTarget, setLockTarget] = useState<AdminUser | null>(null)

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const loadStats = useCallback(async () => {
    try {
      setStats(await adminService.getStats())
    } catch {
      /* stats không chặn bảng */
    }
  }, [])

  const loadUsers = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await adminService.listUsers({
        search: search || undefined,
        role: roleFilter === 'all' ? undefined : roleFilter,
        page,
        pageSize: PAGE_SIZE,
      })
      setUsers(res.items)
      setTotal(res.totalCount)
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không tải được danh sách người dùng.')
    } finally {
      setLoading(false)
    }
  }, [search, roleFilter, page])

  useEffect(() => {
    loadUsers()
  }, [loadUsers])

  useEffect(() => {
    loadStats()
  }, [loadStats])

  // Mở modal tạo staff khi có ?create=1
  useEffect(() => {
    if (searchParams.get('create') === '1') {
      setShowCreate(true)
      searchParams.delete('create')
      setSearchParams(searchParams, { replace: true })
    }
  }, [searchParams, setSearchParams])

  const submitSearch = (e: React.FormEvent) => {
    e.preventDefault()
    setPage(1)
    setSearch(searchInput.trim())
  }

  const refreshAll = async () => {
    await Promise.all([loadUsers(), loadStats()])
  }

  // Mở khóa: làm ngay. Khóa: yêu cầu nhập lý do qua modal.
  const handleToggleActive = async (u: AdminUser) => {
    if (u.isActive) {
      setLockTarget(u)
      return
    }
    setBusyId(u.id)
    setError('')
    try {
      await adminService.activateUser(u.id)
      await refreshAll()
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể mở khóa.')
    } finally {
      setBusyId(null)
    }
  }

  const doLock = async (reason: string) => {
    if (!lockTarget) return
    const u = lockTarget
    setBusyId(u.id)
    setError('')
    try {
      await adminService.deactivateUser(u.id, reason)
      setLockTarget(null)
      await refreshAll()
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể khóa tài khoản.')
    } finally {
      setBusyId(null)
    }
  }

  const handleChangeRole = async (u: AdminUser, role: 'hr_admin' | 'recruiter') => {
    setBusyId(u.id)
    setError('')
    try {
      await adminService.updateRole(u.id, role)
      await refreshAll()
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể đổi vai trò.')
    } finally {
      setBusyId(null)
    }
  }

  const handleDelete = async (u: AdminUser) => {
    if (!window.confirm(`Xóa tài khoản "${u.fullName || u.email}"? Hành động này không thể hoàn tác.`)) return
    setBusyId(u.id)
    setError('')
    try {
      await adminService.deleteUser(u.id)
      await refreshAll()
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể xóa tài khoản.')
    } finally {
      setBusyId(null)
    }
  }

  const statCards = useMemo(
    () => [
      { label: 'Tổng người dùng', value: stats?.totalUsers ?? 0, color: 'text-brand-600' },
      { label: 'HR Admin', value: stats?.hrAdmins ?? 0, color: 'text-ai-600' },
      { label: 'Recruiter', value: stats?.recruiters ?? 0, color: 'text-amber-600' },
      { label: 'Bị khóa', value: stats?.lockedUsers ?? 0, color: 'text-red-600' },
    ],
    [stats]
  )

  const isSuperAdmin = (role: string) => role.toLowerCase().replace(/\s+/g, '_') === 'super_admin'

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Quản lý người dùng"
        description="Quản lý tài khoản nội bộ và phân quyền"
        actions={[{ label: 'Thêm staff', onClick: () => setShowCreate(true), variant: 'primary', icon: <UserPlus className="w-4 h-4" /> }]}
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {stats ? <StatsGrid stats={statCards} /> : <StatsGridSkeleton />}

      {/* Filters */}
      <div className="mb-6 flex flex-col gap-3 sm:flex-row">
        <form onSubmit={submitSearch} className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-400" />
          <input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Tìm theo tên hoặc email..."
            className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 py-2.5 pl-10 pr-4 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
          />
        </form>
        <select
          value={roleFilter}
          onChange={(e) => {
            setPage(1)
            setRoleFilter(e.target.value)
          }}
          className="rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm text-ink-900 dark:text-white outline-none focus:border-brand-400"
        >
          <option value="all">Tất cả vai trò</option>
          <option value="super_admin">Super Admin</option>
          <option value="hr_admin">HR Admin</option>
          <option value="recruiter">Recruiter</option>
        </select>
      </div>

      {loading ? (
        <TableSkeleton rows={8} cols={5} />
      ) : users.length === 0 ? (
        <EmptyState
          icon={<Users className="h-8 w-8 text-ink-400" />}
          title="Không tìm thấy người dùng"
          description="Không có tài khoản nào khớp với điều kiện lọc."
          action={{ label: 'Thêm staff', onClick: () => setShowCreate(true) }}
        />
      ) : (
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          className="overflow-hidden rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card"
        >
          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-ink-100 dark:border-white/10 text-xs uppercase tracking-wider text-ink-400">
                  <th className="px-6 py-3 font-medium">Người dùng</th>
                  <th className="px-6 py-3 font-medium">Vai trò</th>
                  <th className="px-6 py-3 font-medium">Trạng thái</th>
                  <th className="px-6 py-3 font-medium">Ngày tạo</th>
                  <th className="px-6 py-3 text-right font-medium">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-ink-100 dark:divide-white/10">
                {users.map((u) => {
                  const self = u.id === currentUserId
                  const superAdmin = isSuperAdmin(u.role)
                  return (
                    <tr key={u.id} className="hover:bg-ink-50 dark:hover:bg-white/5">
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                            {initials(u.fullName || u.email)}
                          </span>
                          <div className="min-w-0">
                            <p className="truncate text-sm font-medium text-ink-900 dark:text-white">
                              {u.fullName || '—'} {self && <span className="text-xs text-ink-400">(bạn)</span>}
                            </p>
                            <p className="truncate text-xs text-ink-500 dark:text-ink-400">{u.email}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        {superAdmin || self ? (
                          <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${roleBadgeClass(u.role)}`}>
                            {roleLabel(u.role)}
                          </span>
                        ) : (
                          <select
                            value={u.role.toLowerCase().replace(/\s+/g, '_')}
                            disabled={busyId === u.id}
                            onChange={(e) => handleChangeRole(u, e.target.value as 'hr_admin' | 'recruiter')}
                            className="rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-2 py-1 text-xs text-ink-700 dark:text-ink-200 outline-none focus:border-brand-400 disabled:opacity-50"
                          >
                            <option value="hr_admin">HR Admin</option>
                            <option value="recruiter">Recruiter</option>
                          </select>
                        )}
                      </td>
                      <td className="px-6 py-4">
                        {u.isActive ? (
                          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-medium text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400">
                            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" /> Hoạt động
                          </span>
                        ) : (
                          <div className="flex flex-col gap-1">
                            <span className="inline-flex w-fit items-center gap-1.5 rounded-full bg-red-100 px-2.5 py-1 text-xs font-medium text-red-700 dark:bg-red-500/20 dark:text-red-400">
                              <span className="h-1.5 w-1.5 rounded-full bg-red-500" /> Bị khóa
                            </span>
                            {u.lockReason && (
                              <span className="max-w-[200px] truncate text-xs text-ink-400" title={u.lockReason}>
                                Lý do: {u.lockReason}
                              </span>
                            )}
                          </div>
                        )}
                      </td>
                      <td className="px-6 py-4 text-sm text-ink-500 dark:text-ink-400">
                        {new Date(u.createdAt).toLocaleDateString('vi-VN')}
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center justify-end gap-1">
                          {busyId === u.id ? (
                            <Loader2 className="h-4 w-4 animate-spin text-ink-400" />
                          ) : (
                            <>
                              {!self && (
                                <button
                                  onClick={() => handleToggleActive(u)}
                                  title={u.isActive ? 'Khóa tài khoản' : 'Kích hoạt tài khoản'}
                                  className="grid h-8 w-8 place-items-center rounded-lg text-ink-500 hover:bg-ink-100 dark:hover:bg-white/10"
                                >
                                  {u.isActive ? (
                                    <Lock className="h-4 w-4 text-amber-500" />
                                  ) : (
                                    <Unlock className="h-4 w-4 text-emerald-500" />
                                  )}
                                </button>
                              )}
                              {!self && !superAdmin && (
                                <button
                                  onClick={() => handleDelete(u)}
                                  title="Xóa tài khoản"
                                  className="grid h-8 w-8 place-items-center rounded-lg text-ink-500 hover:bg-red-50 dark:hover:bg-red-500/10"
                                >
                                  <Trash2 className="h-4 w-4 text-red-500" />
                                </button>
                              )}
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-ink-100 dark:border-white/10 px-6 py-3 text-sm">
              <span className="text-ink-500 dark:text-ink-400">
                Trang {page}/{totalPages} · {total} tài khoản
              </span>
              <div className="flex items-center gap-1">
                <button
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="grid h-8 w-8 place-items-center rounded-lg border border-ink-200 dark:border-white/10 text-ink-600 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 disabled:opacity-40"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
                <button
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  className="grid h-8 w-8 place-items-center rounded-lg border border-ink-200 dark:border-white/10 text-ink-600 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 disabled:opacity-40"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
              </div>
            </div>
          )}
        </motion.div>
      )}

      {showCreate && (
        <CreateStaffModal
          onClose={() => setShowCreate(false)}
          onCreated={async () => {
            setShowCreate(false)
            await refreshAll()
          }}
        />
      )}

      {lockTarget && (
        <LockReasonModal
          userName={lockTarget.fullName || lockTarget.email}
          submitting={busyId === lockTarget.id}
          onCancel={() => setLockTarget(null)}
          onConfirm={doLock}
        />
      )}
    </div>
  )
}

// ===== Lock Reason Modal =====
function LockReasonModal({
  userName,
  submitting,
  onCancel,
  onConfirm,
}: {
  userName: string
  submitting: boolean
  onCancel: () => void
  onConfirm: (reason: string) => void
}) {
  const [reason, setReason] = useState('')
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onCancel} />
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="relative w-full max-w-md rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-6 shadow-xl"
      >
        <div className="mb-4 flex items-start justify-between">
          <div>
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white">Khóa tài khoản</h3>
            <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">
              Nhập lý do khóa "{userName}". Lý do sẽ được lưu lại và hiển thị cho người bị khóa.
            </p>
          </div>
          <button onClick={onCancel} className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10">
            <X className="h-4 w-4" />
          </button>
        </div>
        <textarea
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          rows={3}
          autoFocus
          placeholder="Vd: Vi phạm chính sách, nghỉ việc..."
          className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
        />
        <div className="mt-4 flex justify-end gap-2">
          <button
            onClick={onCancel}
            className="rounded-xl border border-ink-200 dark:border-white/10 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
          >
            Hủy
          </button>
          <button
            disabled={!reason.trim() || submitting}
            onClick={() => onConfirm(reason.trim())}
            className="flex items-center gap-2 rounded-xl bg-red-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-50"
          >
            {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Lock className="h-4 w-4" />}
            Khóa tài khoản
          </button>
        </div>
      </motion.div>
    </div>
  )
}

// ===== Create Staff Modal =====
function CreateStaffModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [form, setForm] = useState<CreateStaffPayload>({ email: '', fullName: '', role: 'recruiter', department: '' })
  const [submitting, setSubmitting] = useState(false)
  const [err, setErr] = useState('')

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErr('')
    setSubmitting(true)
    try {
      await adminService.createStaff({
        email: form.email.trim(),
        fullName: form.fullName.trim(),
        role: form.role,
        department: form.department?.trim() || undefined,
      })
      onCreated()
    } catch (e: any) {
      setErr(e?.response?.data?.message || 'Không thể tạo tài khoản.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="relative w-full max-w-md rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-6 shadow-xl"
      >
        <div className="mb-5 flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white">Tạo tài khoản staff</h3>
            <p className="text-xs text-ink-500 dark:text-ink-400">Mật khẩu tạm sẽ được gửi qua email cho nhân viên.</p>
          </div>
          <button onClick={onClose} className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10">
            <X className="h-4 w-4" />
          </button>
        </div>

        {err && <ErrorAlert message={err} onDismiss={() => setErr('')} />}

        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">Họ và tên</label>
            <input
              required
              value={form.fullName}
              onChange={(e) => setForm({ ...form, fullName: e.target.value })}
              placeholder="Nguyễn Văn A"
              className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">Email công ty</label>
            <input
              required
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              placeholder="ban@congty.com"
              className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">Vai trò</label>
              <select
                value={form.role}
                onChange={(e) => setForm({ ...form, role: e.target.value as 'hr_admin' | 'recruiter' })}
                className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none focus:border-brand-400"
              >
                <option value="recruiter">Recruiter</option>
                <option value="hr_admin">HR Admin</option>
              </select>
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">Phòng ban</label>
              <input
                value={form.department}
                onChange={(e) => setForm({ ...form, department: e.target.value })}
                placeholder="Tùy chọn"
                className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
              />
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-ink-200 dark:border-white/10 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <UserPlus className="h-4 w-4" />}
              Tạo tài khoản
            </button>
          </div>
        </form>
      </motion.div>
    </div>
  )
}
