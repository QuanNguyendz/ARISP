import { useState, useMemo, useRef, useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  UserPlus,
  Mail,
  Plus,
  Trash2,
  X,
  Loader2,
  Download,
  Upload,
  Clock,
  CheckCircle2,
  XCircle,
  AlertCircle,
} from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState, Pagination } from '@ari/shared/ui'
import { RequestListSkeleton } from './_skeletons'
import {
  accountRequestService,
  type NewAccountRequestItem,
} from '@services/hr/accountRequestService'

export default function HrTeamPage() {
  const { t } = useTranslation('modules/hr/team')

  const roleLabel = (r: string) =>
    r === 'hr_admin' ? t('roles.hrAdmin') : r === 'recruiter' ? t('roles.recruiter') : r

  const statusMeta = (s: string): { label: string; cls: string; Icon: typeof Clock } =>
    s === 'approved'
      ? {
          label: t('status.approved'),
          cls: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
          Icon: CheckCircle2,
        }
      : s === 'rejected'
        ? {
            label: t('status.rejected'),
            cls: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
            Icon: XCircle,
          }
        : {
            label: t('status.pending'),
            cls: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
            Icon: Clock,
          }

  const initials = (n: string) =>
    n
      .trim()
      .split(/\s+/)
      .map((x) => x[0])
      .slice(0, 2)
      .join('')
      .toUpperCase()

  const [showModal, setShowModal] = useState(false)
  const [notice, setNotice] = useState('')
  const [page, setPage] = useState(1)

  const {
    data: requestsData,
    isLoading: loading,
    error: queryError,
    refetch,
  } = useQuery({
    queryKey: ['my-account-requests'],
    queryFn: () => accountRequestService.getMine(),
    refetchOnWindowFocus: false,
    staleTime: 1000 * 60,
  })

  const requests = requestsData || []
  const errorMsg = queryError instanceof Error ? queryError.message : ''

  const stats = useMemo(() => {
    const by = (s: string) => requests.filter((r) => r.status === s).length
    return {
      total: requests.length,
      pending: by('pending'),
      approved: by('approved'),
      rejected: by('rejected'),
    }
  }, [requests])

  const statCards = [
    { label: t('stats.total'), value: stats.total, color: 'text-brand-600' },
    { label: t('stats.pending'), value: stats.pending, color: 'text-amber-600' },
    { label: t('stats.approved'), value: stats.approved, color: 'text-emerald-600' },
    { label: t('stats.rejected'), value: stats.rejected, color: 'text-red-600' },
  ]

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(requests.length / PAGE_SIZE))
  const pagedRequests = useMemo(
    () => requests.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [requests, page]
  )

  useEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])

  return (
    <div className="min-h-screen bg-ink-50 p-6 dark:bg-ink-950 lg:p-8">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={[
          { label: t('sendRequest'), onClick: () => setShowModal(true), variant: 'primary' },
        ]}
      />

      {errorMsg && <ErrorAlert message={errorMsg} onDismiss={() => {}} />}
      {notice && (
        <div className="mb-6 flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-400">
          <CheckCircle2 className="h-4 w-4" /> {notice}
        </div>
      )}

      <StatsGrid stats={statCards} />

      {loading ? (
        <RequestListSkeleton rows={5} />
      ) : requests.length === 0 ? (
        <EmptyState
          icon={<UserPlus className="h-8 w-8 text-ink-400" />}
          title={t('noRequests')}
          description={t('noRequestsHint')}
          action={{ label: t('sendRequestBtn'), onClick: () => setShowModal(true) }}
        />
      ) : (
        <div className="space-y-3">
          {pagedRequests.map((r, i) => {
            const meta = statusMeta(r.status)
            return (
              <motion.div
                key={r.id}
                initial={{ opacity: 0, y: 12 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(i, 10) * 0.03 }}
                className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card"
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-3">
                    <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                      {initials(r.fullName || r.email)}
                    </span>
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-ink-900 dark:text-white">
                        {r.fullName}
                      </p>
                      <p className="flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400">
                        <Mail className="h-3 w-3" /> {r.email}
                        {r.department ? ` · ${r.department}` : ''}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="rounded-full bg-ink-100 px-2.5 py-0.5 text-xs font-medium text-ink-600 dark:bg-white/10 dark:text-ink-300">
                      {roleLabel(r.role)}
                    </span>
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ${meta.cls}`}
                    >
                      <meta.Icon className="h-3 w-3" /> {meta.label}
                    </span>
                  </div>
                </div>
                {r.status === 'rejected' && r.reviewReason && (
                  <p className="mt-2 rounded-lg bg-red-50 px-3 py-1.5 text-xs text-red-600 dark:bg-red-500/10 dark:text-red-400">
                    {t('rejectionReason')}: {r.reviewReason}
                  </p>
                )}
              </motion.div>
            )
          })}
        </div>
      )}

      {!loading && requests.length > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          total={requests.length}
          label={t('paginationLabel')}
          onPageChange={setPage}
        />
      )}

      {showModal && (
        <RequestModal
          t={t}
          onClose={() => setShowModal(false)}
          onDone={(count) => {
            setShowModal(false)
            setNotice(t('requestSent', { count }))
            refetch()
          }}
        />
      )}
    </div>
  )
}

function RequestModal({
  t,
  onClose,
  onDone,
}: {
  t: (key: string, options?: Record<string, unknown>) => string
  onClose: () => void
  onDone: (count: number) => void
}) {
  const csvRef = useRef<HTMLInputElement>(null)
  const [rows, setRows] = useState<NewAccountRequestItem[]>([
    { email: '', fullName: '', role: 'recruiter', department: '' },
  ])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  const update = (i: number, field: keyof NewAccountRequestItem, value: string) => {
    const u = [...rows]
    u[i] = { ...u[i], [field]: value }
    setRows(u)
  }
  const addRow = () =>
    setRows([...rows, { email: '', fullName: '', role: 'recruiter', department: '' }])
  const removeRow = (i: number) =>
    setRows(rows.length > 1 ? rows.filter((_, idx) => idx !== i) : rows)

  const downloadTemplate = () => {
    const csv =
      'email,fullName,role,department\nvidu@congty.com,Nguyen Van A,recruiter,Tuyen dung\n'
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'account-request-template.csv'
    a.click()
    URL.revokeObjectURL(url)
  }

  const importCsv = async (file: File) => {
    setError('')
    const text = await file.text()
    const lines = text
      .split(/\r?\n/)
      .map((l) => l.trim())
      .filter(Boolean)
    if (lines.length < 2) {
      setError(t('emptyCsvError'))
      return
    }
    const header = lines[0].toLowerCase()
    const hasHeader = header.includes('email')
    const dataLines = hasHeader ? lines.slice(1) : lines
    const parsed: NewAccountRequestItem[] = dataLines
      .map((l) => {
        const [email = '', fullName = '', role = 'recruiter', department = ''] = l
          .split(',')
          .map((c) => c.trim())
        return {
          email,
          fullName,
          role: role.toLowerCase() === 'hr_admin' ? 'hr_admin' : 'recruiter',
          department,
        }
      })
      .filter((r) => r.email)
    if (parsed.length === 0) {
      setError(t('noValidRowsError'))
      return
    }
    setRows(parsed)
  }

  const submit = async () => {
    const cleaned = rows
      .map((r) => ({
        ...r,
        email: r.email.trim(),
        fullName: r.fullName.trim(),
        department: r.department?.trim() || undefined,
      }))
      .filter((r) => r.email || r.fullName)
    if (cleaned.length === 0) {
      setError(t('atLeastOneError'))
      return
    }
    for (const r of cleaned) {
      if (!r.email.includes('@')) {
        setError(t('invalidEmail', { email: r.email || t('empty') }))
        return
      }
      if (!r.fullName) {
        setError(t('missingName', { email: r.email }))
        return
      }
    }
    setSubmitting(true)
    setError('')
    try {
      const res = await accountRequestService.create(cleaned)
      onDone(res.count)
    } catch (e: any) {
      setError(e?.response?.data?.message || t('submitError'))
    } finally {
      setSubmitting(false)
    }
  }

  const inputCls =
    'w-full rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-2.5 py-2 text-sm text-ink-900 dark:text-white outline-none focus:border-brand-400'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="relative max-h-[88vh] w-full max-w-3xl overflow-y-auto rounded-2xl border border-ink-200 dark:border-white/10 bg-white p-6 shadow-xl dark:bg-ink-900"
      >
        <div className="mb-4 flex items-start justify-between">
          <div>
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
              {t('modalTitle')}
            </h3>
            <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">{t('modalDescription')}</p>
          </div>
          <button
            onClick={onClose}
            className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {error && (
          <div className="mb-4 flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
            <AlertCircle className="h-4 w-4" /> {error}
          </div>
        )}

        <div className="mb-3 flex flex-wrap gap-2">
          <button
            onClick={downloadTemplate}
            className="inline-flex items-center gap-2 rounded-lg border border-ink-200 px-3 py-1.5 text-xs font-medium text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/10"
          >
            <Download className="h-3.5 w-3.5" /> {t('downloadTemplate')}
          </button>
          <input
            ref={csvRef}
            type="file"
            accept=".csv"
            className="hidden"
            onChange={(e) => {
              const f = e.target.files?.[0]
              if (f) importCsv(f)
            }}
          />
          <button
            onClick={() => csvRef.current?.click()}
            className="inline-flex items-center gap-2 rounded-lg border border-ink-200 px-3 py-1.5 text-xs font-medium text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/10"
          >
            <Upload className="h-3.5 w-3.5" /> {t('importCsv')}
          </button>
        </div>

        <div className="space-y-2">
          <div className="hidden grid-cols-[1fr_1fr_120px_1fr_36px] gap-2 px-1 text-xs font-medium text-ink-400 sm:grid">
            <span>{t('tableHeader.email')}</span>
            <span>{t('tableHeader.fullName')}</span>
            <span>{t('tableHeader.role')}</span>
            <span>{t('tableHeader.department')}</span>
            <span />
          </div>
          {rows.map((r, i) => (
            <div key={i} className="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_1fr_120px_1fr_36px]">
              <input
                value={r.email}
                onChange={(e) => update(i, 'email', e.target.value)}
                placeholder={t('placeholder.email')}
                className={inputCls}
              />
              <input
                value={r.fullName}
                onChange={(e) => update(i, 'fullName', e.target.value)}
                placeholder={t('placeholder.fullName')}
                className={inputCls}
              />
              <select
                value={r.role}
                onChange={(e) => update(i, 'role', e.target.value)}
                className={inputCls}
              >
                <option value="recruiter">{t('roles.recruiter')}</option>
                <option value="hr_admin">{t('roles.hrAdmin')}</option>
              </select>
              <input
                value={r.department || ''}
                onChange={(e) => update(i, 'department', e.target.value)}
                placeholder={t('placeholder.department')}
                className={inputCls}
              />
              <button
                onClick={() => removeRow(i)}
                className="grid h-9 w-9 place-items-center rounded-lg text-ink-400 hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-500/10"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          ))}
        </div>

        <button
          onClick={addRow}
          className="mt-3 inline-flex items-center gap-1.5 text-sm font-medium text-brand-600 hover:underline dark:text-brand-400"
        >
          <Plus className="h-4 w-4" /> {t('addRow')}
        </button>

        <div className="mt-5 flex justify-end gap-2">
          <button
            onClick={onClose}
            className="rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-medium text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/10"
          >
            {t('cancel')}
          </button>
          <button
            onClick={submit}
            disabled={submitting}
            className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
          >
            {submitting ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <UserPlus className="h-4 w-4" />
            )}
            {rows.length > 1 ? t('submitMultiple', { count: rows.length }) : t('submitSingle')}
          </button>
        </div>
      </motion.div>
    </div>
  )
}
