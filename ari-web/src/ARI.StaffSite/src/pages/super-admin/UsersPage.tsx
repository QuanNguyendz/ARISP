import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
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
import { PageHeader, StatsGrid, EmptyState, ErrorAlert, Select } from '@ari/shared/ui'
import { useAuthStore } from '@ari/shared/store/auth'
import {
  adminService,
  type AdminUser,
  type AdminStats,
  type CreateStaffPayload,
} from '@/fservices/admin'
import { roleLabel, roleBadgeClass } from '@/utils/adminLabels'
import {
  ASSIGNABLE_STAFF_ROLES,
  ROLE,
  type AssignableStaffRole,
} from '@ari/shared/utils/roles'
import { departmentService, type Department } from '@ari/shared/fservices/department'
import { StatsGridSkeleton, TableSkeleton } from './_skeletons'

const PAGE_SIZE = 10

/**
 * Nhãn i18n cho từng vai trò. Kiểu của bảng này ràng buộc theo chính `ASSIGNABLE_STAFF_ROLES`,
 * nên thêm một vai trò vào hằng số dùng chung mà quên nhãn ở đây là **lỗi biên dịch** — thay vì
 * một ô select trống lặng lẽ, đúng triệu chứng mà Hiring Manager vừa gây ra.
 */
const ROLE_I18N_KEY: Record<AssignableStaffRole | typeof ROLE.SuperAdmin, string> = {
  [ROLE.HRAdmin]: 'filters.hrAdmin',
  [ROLE.Recruiter]: 'filters.recruiter',
  [ROLE.HiringManager]: 'filters.hiringManager',
  [ROLE.SuperAdmin]: 'filters.superAdmin',
}

/** Các vai trò Super Admin cấp được — dùng chung cho ô lọc, ô đổi vai trò và form tạo tài khoản. */
const assignableRoleOptions = (t: (key: string) => string) =>
  ASSIGNABLE_STAFF_ROLES.map((role) => ({ value: role, label: t(ROLE_I18N_KEY[role]) }))

const initials = (name?: string | null) =>
  (name || 'U')
    .trim()
    .split(/\s+/)
    .map((n) => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

export default function UsersPage() {
  const { t } = useTranslation('modules/super-admin/users')
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

  // Chỉ đội đang hoạt động: đội đã tắt vẫn hiện được TÊN ở dòng cũ (server trả kèm), nhưng không
  // gán mới vào được.
  const [departments, setDepartments] = useState<Department[]>([])
  useEffect(() => {
    void departmentService.list(true).then(setDepartments).catch(() => setDepartments([]))
  }, [])

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
      setError(e?.response?.data?.message || t('errors.loadFailed'))
    } finally {
      setLoading(false)
    }
  }, [search, roleFilter, page, t])

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

  // Nhận từ khoá tìm kiếm từ ô search ở header (?search=) → áp vào ô tìm + query, rồi dọn URL.
  useEffect(() => {
    const q = searchParams.get('search')
    if (q !== null) {
      setSearch(q)
      setSearchInput(q)
      setPage(1)
      searchParams.delete('search')
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
      setError(e?.response?.data?.message || t('errors.unlockFailed'))
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
      setError(e?.response?.data?.message || t('errors.lockFailed'))
    } finally {
      setBusyId(null)
    }
  }

  const handleChangeDepartment = async (u: AdminUser, departmentId: string | null) => {
    setBusyId(u.id)
    setError('')
    try {
      await adminService.updateDepartment(u.id, departmentId)
      await loadUsers()
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.departmentChangeFailed'))
    } finally {
      setBusyId(null)
    }
  }

  const handleChangeRole = async (u: AdminUser, role: AssignableStaffRole) => {
    setBusyId(u.id)
    setError('')
    try {
      await adminService.updateRole(u.id, role)
      await refreshAll()
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.roleChangeFailed'))
    } finally {
      setBusyId(null)
    }
  }

  const handleDelete = async (u: AdminUser) => {
    if (!window.confirm(t('deleteConfirm', { name: u.fullName || u.email }))) return
    setBusyId(u.id)
    setError('')
    try {
      await adminService.deleteUser(u.id)
      await refreshAll()
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.deleteFailed'))
    } finally {
      setBusyId(null)
    }
  }

  const statCards = useMemo(
    () => [
      { label: t('statCards.totalUsers'), value: stats?.totalUsers ?? 0, color: 'text-brand-600' },
      { label: t('statCards.hrAdmin'), value: stats?.hrAdmins ?? 0, color: 'text-ai-600' },
      { label: t('statCards.recruiter'), value: stats?.recruiters ?? 0, color: 'text-amber-600' },
      {
        label: t('statCards.hiringManager'),
        value: stats?.hiringManagers ?? 0,
        color: 'text-sky-600',
      },
      { label: t('statCards.lockedUsers'), value: stats?.lockedUsers ?? 0, color: 'text-red-600' },
    ],
    [stats, t]
  )

  const isSuperAdmin = (role: string) => role.toLowerCase().replace(/\s+/g, '_') === 'super_admin'

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={[
          {
            label: t('addStaff'),
            onClick: () => setShowCreate(true),
            variant: 'primary',
            icon: <UserPlus className="w-4 h-4" />,
          },
        ]}
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
            placeholder={t('searchPlaceholder')}
            className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 py-2.5 pl-10 pr-4 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
          />
        </form>
        <Select
          value={roleFilter}
          onChange={(v) => {
            setPage(1)
            setRoleFilter(v)
          }}
          ariaLabel={t('filters.allRoles')}
          className="min-w-[11rem]"
          buttonClassName="px-4 py-2.5 text-sm"
          options={[
            { value: 'all', label: t('filters.allRoles') },
            { value: ROLE.SuperAdmin, label: t(ROLE_I18N_KEY[ROLE.SuperAdmin]) },
            ...assignableRoleOptions(t),
          ]}
        />
      </div>

      {loading ? (
        <TableSkeleton rows={8} cols={5} />
      ) : users.length === 0 ? (
        <EmptyState
          icon={<Users className="h-8 w-8 text-ink-400" />}
          title={t('empty.title')}
          description={t('empty.description')}
          action={{ label: t('empty.action'), onClick: () => setShowCreate(true) }}
        />
      ) : (
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          className="overflow-hidden rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card"
        >
          <div className="overflow-x-auto">
            <div className="min-w-[700px]">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-ink-100 dark:border-white/10 text-xs uppercase tracking-wider text-ink-400">
                  <th className="px-4 py-3 font-medium sm:px-6">{t('table.headers.user')}</th>
                  <th className="px-4 py-3 font-medium sm:px-6">{t('table.headers.role')}</th>
                  <th className="px-4 py-3 font-medium sm:px-6">{t('table.headers.department')}</th>
                  <th className="px-4 py-3 font-medium sm:px-6">{t('table.headers.status')}</th>
                  <th className="px-4 py-3 font-medium sm:px-6">{t('table.headers.createdAt')}</th>
                  <th className="px-4 py-3 text-right font-medium sm:px-6">
                    {t('table.headers.actions')}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-ink-100 dark:divide-white/10">
                {users.map((u) => {
                  const self = u.id === currentUserId
                  const superAdmin = isSuperAdmin(u.role)
                  return (
                    <tr key={u.id} className="hover:bg-ink-50 dark:hover:bg-white/5">
                      <td className="px-4 py-4 sm:px-6">
                        <div className="flex items-center gap-3">
                          <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                            {initials(u.fullName || u.email)}
                          </span>
                          <div className="min-w-0">
                            <p className="truncate text-sm font-medium text-ink-900 dark:text-white">
                              {u.fullName || t('table.emptyName')}{' '}
                              {self && (
                                <span className="text-xs text-ink-400">{t('table.you')}</span>
                              )}
                            </p>
                            <p className="truncate text-xs text-ink-500 dark:text-ink-400">
                              {u.email}
                            </p>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-4 sm:px-6">
                        {superAdmin || self ? (
                          <span
                            className={`inline-flex items-center whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-medium ${roleBadgeClass(u.role)}`}
                          >
                            {roleLabel(u.role)}
                          </span>
                        ) : (
                          <Select
                            value={u.role.toLowerCase().replace(/\s+/g, '_')}
                            disabled={busyId === u.id}
                            onChange={(v) => handleChangeRole(u, v as AssignableStaffRole)}
                            className="w-full max-w-[160px]"
                            buttonClassName="rounded-lg px-2 py-1 text-xs"
                            options={assignableRoleOptions(t)}
                          />
                        )}
                      </td>
                      <td className="px-4 py-4 sm:px-6">
                        {/* Super Admin là người DUY NHẤT đặt được đội — nhân viên không còn tự sửa
                            ở trang Cài đặt (ADR-065). Chính tài khoản Super Admin thì không cần đội. */}
                        {superAdmin ? (
                          <span className="text-xs text-ink-400">—</span>
                        ) : (
                          <Select
                            value={u.departmentId ?? ''}
                            disabled={busyId === u.id}
                            onChange={(v) => handleChangeDepartment(u, v || null)}
                            ariaLabel={t('table.headers.department')}
                            className="w-full max-w-[180px]"
                            buttonClassName="rounded-lg px-2 py-1 text-xs"
                            placeholder={t('table.noDepartment')}
                            options={[
                              { value: '', label: t('table.noDepartment') },
                              ...departments.map((d) => ({ value: d.id, label: d.name })),
                            ]}
                          />
                        )}
                      </td>
                      <td className="px-4 py-4 sm:px-6">
                        {u.isActive ? (
                          <span className="inline-flex items-center gap-1.5 whitespace-nowrap rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-medium text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400">
                            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />{' '}
                            {t('table.status.active')}
                          </span>
                        ) : (
                          <div className="flex flex-col gap-1">
                            <span className="inline-flex w-fit items-center gap-1.5 whitespace-nowrap rounded-full bg-red-100 px-2.5 py-1 text-xs font-medium text-red-700 dark:bg-red-500/20 dark:text-red-400">
                              <span className="h-1.5 w-1.5 rounded-full bg-red-500" />{' '}
                              {t('table.status.locked')}
                            </span>
                            {u.lockReason && (
                              <span
                                className="max-w-[200px] truncate text-xs text-ink-400"
                                title={u.lockReason}
                              >
                                {t('table.lockReason', { reason: u.lockReason })}
                              </span>
                            )}
                          </div>
                        )}
                      </td>
                      <td className="px-4 py-4 text-sm text-ink-500 dark:text-ink-400 whitespace-nowrap sm:px-6">
                        {new Date(u.createdAt).toLocaleDateString('vi-VN')}
                      </td>
                      <td className="px-4 py-4 sm:px-6">
                        <div className="flex items-center justify-end gap-1">
                          {busyId === u.id ? (
                            <Loader2 className="h-4 w-4 animate-spin text-ink-400" />
                          ) : (
                            <>
                              {!self && (
                                <button
                                  onClick={() => handleToggleActive(u)}
                                  title={
                                    u.isActive ? t('table.actions.lock') : t('table.actions.unlock')
                                  }
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
                                  title={t('table.actions.delete')}
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
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex flex-col gap-2 border-t border-ink-100 px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6 dark:border-white/10">
              <span className="text-ink-500 dark:text-ink-400">
                {t('pagination.summary', { page, totalPages, total })}
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
  const { t } = useTranslation('modules/super-admin/users')
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
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
              {t('lockModal.title')}
            </h3>
            <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">
              {t('lockModal.description', { name: userName })}
            </p>
          </div>
          <button
            onClick={onCancel}
            className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        <textarea
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          rows={3}
          autoFocus
          placeholder={t('lockModal.placeholder')}
          className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
        />
        <div className="mt-4 flex justify-end gap-2">
          <button
            onClick={onCancel}
            className="rounded-xl border border-ink-200 dark:border-white/10 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
          >
            {t('lockModal.cancel')}
          </button>
          <button
            disabled={!reason.trim() || submitting}
            onClick={() => onConfirm(reason.trim())}
            className="flex items-center gap-2 rounded-xl bg-red-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-50"
          >
            {submitting ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Lock className="h-4 w-4" />
            )}
            {t('lockModal.confirm')}
          </button>
        </div>
      </motion.div>
    </div>
  )
}

// ===== Create Staff Modal =====
function CreateStaffModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const { t } = useTranslation('modules/super-admin/users')
  const [form, setForm] = useState<CreateStaffPayload>({
    email: '',
    fullName: '',
    role: 'recruiter',
    departmentId: undefined,
  })
  const [submitting, setSubmitting] = useState(false)
  const [err, setErr] = useState('')

  // Chỉ đội ĐANG HOẠT ĐỘNG: gán vào đội đã tắt thì tài khoản đó không lập được phiếu mà cũng không
  // có lỗi nào chỉ ra vì sao (server cũng chặn lại).
  const [departments, setDepartments] = useState<Department[]>([])
  useEffect(() => {
    void departmentService.list(true).then(setDepartments).catch(() => setDepartments([]))
  }, [])

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErr('')
    setSubmitting(true)
    try {
      await adminService.createStaff({
        email: form.email.trim(),
        fullName: form.fullName.trim(),
        role: form.role,
        departmentId: form.departmentId,
      })
      onCreated()
    } catch (e: any) {
      setErr(e?.response?.data?.message || t('errors.createFailed'))
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
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
              {t('createModal.title')}
            </h3>
            <p className="text-xs text-ink-500 dark:text-ink-400">{t('createModal.description')}</p>
          </div>
          <button
            onClick={onClose}
            className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {err && <ErrorAlert message={err} onDismiss={() => setErr('')} />}

        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
              {t('createModal.fullName')}
            </label>
            <input
              required
              value={form.fullName}
              onChange={(e) => setForm({ ...form, fullName: e.target.value })}
              placeholder={t('createModal.fullNamePlaceholder')}
              className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
              {t('createModal.email')}
            </label>
            <input
              required
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              placeholder={t('createModal.emailPlaceholder')}
              className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400"
            />
          </div>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                {t('createModal.role')}
              </label>
              <Select
                value={form.role}
                onChange={(v) => setForm({ ...form, role: v as AssignableStaffRole })}
                className="w-full"
                buttonClassName="px-3 py-2.5 text-sm"
                options={assignableRoleOptions(t)}
              />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                {t('createModal.department')}
              </label>
              <Select
                value={form.departmentId ?? ''}
                onChange={(v) => setForm({ ...form, departmentId: v || undefined })}
                ariaLabel={t('createModal.department')}
                className="w-full"
                buttonClassName="px-3 py-2.5 text-sm"
                placeholder={t('createModal.departmentPlaceholder')}
                options={departments.map((d) => ({ value: d.id, label: d.name }))}
              />
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-ink-200 dark:border-white/10 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
            >
              {t('createModal.cancel')}
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
            >
              {submitting ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <UserPlus className="h-4 w-4" />
              )}
              {t('createModal.submit')}
            </button>
          </div>
        </form>
      </motion.div>
    </div>
  )
}
