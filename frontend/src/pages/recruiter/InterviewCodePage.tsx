import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  KeyRound,
  Search,
  Loader2,
  Copy,
  Check,
  Clock,
  Mail,
  ShieldCheck,
  CalendarClock,
} from 'lucide-react'
import { PageHeader, ErrorAlert, EmptyState, Pagination } from '@components/shared'
import { applicationService } from '@services/application/applicationService'
import { interviewService } from '@services/interview/interviewService'
import type { HrApplicationItem } from '@/types/application'
import { appStatusBadge, appStatusLabel, initials } from './_jobUi'
import { ApplicantsSkeleton } from './_skeletons'

interface IssuedCode {
  code: string
  expiresAt: string
}

export default function RecruiterInterviewCodePage() {
  const { t } = useTranslation('modules/recruiter/interviewCode')
  const [apps, setApps] = useState<HrApplicationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [q, setQ] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const [issued, setIssued] = useState<Record<string, IssuedCode>>({})
  const [copied, setCopied] = useState<string | null>(null)
  const [page, setPage] = useState(1)

  useEffect(() => {
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        setApps(await applicationService.getApplications(true))
      } catch (e: any) {
        setError(e?.response?.data?.message || t('loadError'))
      } finally {
        setLoading(false)
      }
    })()
  }, [t])

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase()
    const list = apps.filter((a) =>
      query
        ? (a.candidateName + a.candidateEmail + (a.jobTitle || '')).toLowerCase().includes(query)
        : true
    )
    const rank = (a: HrApplicationItem) =>
      a.hasScheduledInterview
        ? 0
        : a.status === 'interview'
          ? 1
          : a.status === 'screening'
            ? 2
            : a.status === 'cv_submitted'
              ? 3
              : 4
    return [...list].sort((x, y) => rank(x) - rank(y))
  }, [apps, q])

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const paged = useMemo(
    () => filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [filtered, page]
  )

  useEffect(() => {
    setPage(1)
  }, [q])

  const expiresIn = (iso: string): string => {
    const ms = new Date(iso).getTime() - Date.now()
    if (ms <= 0) return t('expired')
    const mins = Math.round(ms / 60000)
    if (mins < 60) return t('minutes', { count: mins })
    return t('hours', { count: Math.round(mins / 60) })
  }

  const generate = async (appId: string) => {
    setBusyId(appId)
    setError('')
    try {
      const r = await interviewService.generateCode(appId)
      setIssued((prev) => ({ ...prev, [appId]: { code: r.code, expiresAt: r.expiresAt } }))
    } catch (e: any) {
      setError(e?.response?.data?.message || t('generateError'))
    } finally {
      setBusyId(null)
    }
  }

  const copy = async (code: string) => {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(code)
      setTimeout(() => setCopied((c) => (c === code ? null : c)), 1500)
    } catch {
      /* ignore */
    }
  }

  return (
    <div className="p-6 lg:p-8">
      <PageHeader title={t('title')} description={t('description')} />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <div className="mb-6 flex items-start gap-2 rounded-2xl border border-ai-200 dark:border-ai-500/30 bg-gradient-to-b from-ai-50/60 dark:from-ai-500/10 to-white dark:to-white/5 p-4 text-sm text-ai-700 dark:text-ai-300">
        <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" />
        <span dangerouslySetInnerHTML={{ __html: t('infoText') }} />
      </div>

      <div className="mb-5 flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 max-w-md">
        <Search className="h-4 w-4 text-ink-400" />
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder={t('searchPlaceholder')}
          className="w-full bg-transparent text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400"
        />
      </div>

      {loading ? (
        <ApplicantsSkeleton rows={6} />
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={<KeyRound className="h-8 w-8 text-ink-400" />}
          title={apps.length === 0 ? t('emptyState.title') : t('noResults')}
          description={apps.length === 0 ? t('emptyState.description') : t('noResultsHint')}
        />
      ) : (
        <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
          <div className="divide-y divide-ink-100 dark:divide-white/10">
            {paged.map((a, i) => {
              const code = issued[a.id]
              return (
                <motion.div
                  key={a.id}
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: Math.min(i, 10) * 0.02 }}
                  className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="flex min-w-0 items-center gap-3">
                    <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                      {initials(a.candidateName || a.candidateEmail)}
                    </span>
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-ink-900 dark:text-white">
                        {a.candidateName || t('applicant')}
                      </p>
                      <p className="flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400">
                        <Mail className="h-3 w-3" /> {a.candidateEmail} ·{' '}
                        {a.jobTitle || t('position')}
                      </p>
                    </div>
                  </div>

                  <div className="flex shrink-0 items-center gap-3">
                    <span
                      className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${appStatusBadge(a.status)}`}
                    >
                      {appStatusLabel(a.status)}
                    </span>
                    {code ? (
                      <div className="flex items-center gap-2">
                        <button
                          onClick={() => copy(code.code)}
                          className="inline-flex items-center gap-2 rounded-lg border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 px-3 py-1.5 font-mono text-sm font-bold tracking-wider text-emerald-700 dark:text-emerald-400"
                          title={t('copyCode')}
                        >
                          {code.code}
                          {copied === code.code ? (
                            <Check className="h-3.5 w-3.5" />
                          ) : (
                            <Copy className="h-3.5 w-3.5" />
                          )}
                        </button>
                        <span className="flex items-center gap-1 text-xs text-ink-400">
                          <Clock className="h-3 w-3" /> {expiresIn(code.expiresAt)}
                        </span>
                      </div>
                    ) : a.hasScheduledInterview ? (
                      <button
                        onClick={() => generate(a.id)}
                        disabled={busyId === a.id}
                        className="inline-flex items-center gap-1.5 rounded-lg bg-brand-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
                      >
                        {busyId === a.id ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <KeyRound className="h-3.5 w-3.5" />
                        )}{' '}
                        {t('generateCode')}
                      </button>
                    ) : (
                      <span
                        className="inline-flex items-center gap-1.5 rounded-lg border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 px-3 py-1.5 text-xs font-medium text-ink-400"
                        title={t('notScheduled')}
                      >
                        <CalendarClock className="h-3.5 w-3.5" /> {t('notScheduled')}
                      </span>
                    )}
                  </div>
                </motion.div>
              )
            })}
          </div>
        </div>
      )}

      {!loading && filtered.length > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          total={filtered.length}
          label={t('paginationLabel')}
          onPageChange={setPage}
        />
      )}
    </div>
  )
}
