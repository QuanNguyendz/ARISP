import { useEffect, useState } from 'react'
import { motion } from 'framer-motion'
import { Clock, XCircle, CheckCircle, Loader2, X, Mail } from 'lucide-react'
import { PageHeader, EmptyState, ErrorAlert } from '@components/shared'
import { adminService, type AccountRequest } from '@services/admin'
import { roleLabel, roleBadgeClass } from '@utils/adminLabels'
import { CardListSkeleton } from './_skeletons'

const initials = (name?: string | null) =>
  (name || 'U')
    .trim()
    .split(/\s+/)
    .map((n) => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

export default function PendingUsersPage() {
  const [requests, setRequests] = useState<AccountRequest[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const [rejectTarget, setRejectTarget] = useState<AccountRequest | null>(null)

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      setRequests(await adminService.getAccountRequests('pending'))
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không tải được danh sách yêu cầu.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  const handleApprove = async (r: AccountRequest) => {
    setBusyId(r.id)
    setError('')
    try {
      await adminService.approveAccountRequest(r.id)
      setRequests((prev) => prev.filter((x) => x.id !== r.id))
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể duyệt yêu cầu.')
    } finally {
      setBusyId(null)
    }
  }

  const handleReject = async (reason: string) => {
    if (!rejectTarget) return
    const r = rejectTarget
    setBusyId(r.id)
    setError('')
    try {
      await adminService.rejectAccountRequest(r.id, reason)
      setRequests((prev) => prev.filter((x) => x.id !== r.id))
      setRejectTarget(null)
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể từ chối yêu cầu.')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Duyệt tài khoản mới"
        description="Yêu cầu tạo tài khoản do HR Leader gửi — duyệt để tạo tài khoản hoạt động"
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {loading ? (
        <CardListSkeleton count={4} />
      ) : requests.length === 0 ? (
        <EmptyState
          icon={<CheckCircle className="h-8 w-8 text-emerald-500" />}
          title="Không có yêu cầu chờ duyệt"
          description="Khi HR Leader gửi yêu cầu tạo tài khoản, chúng sẽ xuất hiện ở đây."
        />
      ) : (
        <div className="space-y-3">
          {requests.map((r, index) => (
            <motion.div
              key={r.id}
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: index * 0.04 }}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
            >
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-start gap-4">
                  <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-sm font-bold text-white">
                    {initials(r.fullName || r.email)}
                  </span>
                  <div className="min-w-0">
                    <h3 className="text-base font-semibold text-ink-900 dark:text-white">{r.fullName || r.email}</h3>
                    <p className="mb-2 flex items-center gap-1.5 text-sm text-ink-500 dark:text-ink-400">
                      <Mail className="h-3.5 w-3.5" /> {r.email}
                      {r.department ? <span className="text-ink-400">· {r.department}</span> : null}
                    </p>
                    <div className="flex flex-wrap items-center gap-3">
                      <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${roleBadgeClass(r.role)}`}>
                        {roleLabel(r.role)}
                      </span>
                      <span className="flex items-center gap-1 text-xs text-ink-400">
                        <Clock className="h-3 w-3" />
                        {new Date(r.createdAt).toLocaleString('vi-VN', {
                          day: '2-digit',
                          month: '2-digit',
                          year: 'numeric',
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </span>
                      <span className="text-xs text-ink-400">Yêu cầu bởi {r.requestedBy}</span>
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {busyId === r.id ? (
                    <span className="flex items-center gap-2 px-4 py-2 text-sm text-ink-400">
                      <Loader2 className="h-4 w-4 animate-spin" /> Đang xử lý...
                    </span>
                  ) : (
                    <>
                      <button
                        onClick={() => handleApprove(r)}
                        className="flex items-center gap-2 rounded-xl bg-emerald-600 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-700"
                      >
                        <CheckCircle className="h-4 w-4" /> Duyệt
                      </button>
                      <button
                        onClick={() => setRejectTarget(r)}
                        className="flex items-center gap-2 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-4 py-2 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-500/20"
                      >
                        <XCircle className="h-4 w-4" /> Từ chối
                      </button>
                    </>
                  )}
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      )}

      {rejectTarget && (
        <ReasonModal
          title="Từ chối yêu cầu"
          description={`Nhập lý do từ chối tạo tài khoản cho "${rejectTarget.fullName || rejectTarget.email}".`}
          confirmLabel="Từ chối"
          submitting={busyId === rejectTarget.id}
          onCancel={() => setRejectTarget(null)}
          onConfirm={handleReject}
        />
      )}
    </div>
  )
}

// Modal nhập lý do (dùng chung cho từ chối)
function ReasonModal({
  title,
  description,
  confirmLabel,
  submitting,
  onCancel,
  onConfirm,
}: {
  title: string
  description: string
  confirmLabel: string
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
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white">{title}</h3>
            <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">{description}</p>
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
          placeholder="Nhập lý do..."
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
            {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <XCircle className="h-4 w-4" />}
            {confirmLabel}
          </button>
        </div>
      </motion.div>
    </div>
  )
}
