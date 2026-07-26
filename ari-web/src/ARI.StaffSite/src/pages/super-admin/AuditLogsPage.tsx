import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import { Activity, ChevronLeft, ChevronRight } from 'lucide-react'
import { PageHeader, EmptyState, ErrorAlert } from '@ari/shared/ui'
import { adminService, type AuditLogEntry } from '@/fservices/admin'
import { auditActionLabel, timeAgo } from '@/utils/adminLabels'
import { LogListSkeleton } from './_skeletons'

const PAGE_SIZE = 20

function actionDotClass(action: string): string {
  if (action.includes('deleted') || action.includes('deactivated')) return 'bg-red-500'
  if (action.includes('approved') || action.includes('activated') || action.includes('created'))
    return 'bg-emerald-500'
  if (action.includes('settings')) return 'bg-ai-500'
  return 'bg-brand-500'
}

function prettyMeta(metadata?: string | null): string {
  if (!metadata) return ''
  try {
    const obj = JSON.parse(metadata)
    return Object.entries(obj)
      .map(([k, v]) => `${k}: ${v}`)
      .join(' · ')
  } catch {
    return metadata
  }
}

const ACTION_KEYS: Record<string, string> = {
  staff_account_created: 'staffAccountCreated',
  user_approved: 'userApproved',
  user_role_updated: 'userRoleUpdated',
  user_activated: 'userActivated',
  user_deactivated: 'userDeactivated',
  user_deleted: 'userDeleted',
  system_settings_updated: 'systemSettingsUpdated',
}

export default function AuditLogsPage() {
  const { t } = useTranslation('modules/super-admin/auditLogs')
  const [logs, setLogs] = useState<AuditLogEntry[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [action, setAction] = useState('all')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await adminService.getAuditLogs({
        action: action === 'all' ? undefined : action,
        page,
        pageSize: PAGE_SIZE,
      })
      setLogs(res.items)
      setTotal(res.totalCount)
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.loadFailed'))
    } finally {
      setLoading(false)
    }
  }, [action, page, t])

  useEffect(() => {
    load()
  }, [load])

  const actionOptions = [
    { value: 'all', label: t('filters.all') },
    ...Object.entries(ACTION_KEYS).map(([value, key]) => ({ value, label: t(`filters.${key}`) })),
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader title={t('title')} description={t('description')} />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <div className="mb-6">
        <select
          value={action}
          onChange={(e) => {
            setPage(1)
            setAction(e.target.value)
          }}
          className="rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm text-ink-900 dark:text-white outline-none focus:border-brand-400"
        >
          {actionOptions.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </div>

      {loading ? (
        <LogListSkeleton rows={10} />
      ) : logs.length === 0 ? (
        <EmptyState
          icon={<Activity className="h-8 w-8 text-ink-400" />}
          title={t('empty.title')}
          description={t('empty.description')}
        />
      ) : (
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          className="overflow-hidden rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card"
        >
          <ul className="divide-y divide-ink-100 dark:divide-white/10">
            {logs.map((log) => (
              <li
                key={log.id}
                className="flex items-start gap-4 px-6 py-4 hover:bg-ink-50 dark:hover:bg-white/5"
              >
                <span className="mt-1.5 flex h-2.5 w-2.5 shrink-0">
                  <span className={`h-2.5 w-2.5 rounded-full ${actionDotClass(log.action)}`} />
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-medium text-ink-900 dark:text-white">
                      {auditActionLabel(log.action)}
                    </p>
                    {log.entityType && (
                      <span className="rounded-md bg-ink-100 dark:bg-white/10 px-1.5 py-0.5 text-[11px] font-medium text-ink-500 dark:text-ink-300">
                        {log.entityType}
                      </span>
                    )}
                  </div>
                  {prettyMeta(log.metadata) && (
                    <p className="mt-0.5 truncate text-xs text-ink-500 dark:text-ink-400">
                      {prettyMeta(log.metadata)}
                    </p>
                  )}
                  <p className="mt-0.5 text-xs text-ink-400">{t('by', { name: log.actorName })}</p>
                </div>
                <span
                  className="shrink-0 text-xs text-ink-400"
                  title={new Date(log.createdAt).toLocaleString('vi-VN')}
                >
                  {timeAgo(log.createdAt)}
                </span>
              </li>
            ))}
          </ul>

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
    </div>
  )
}
