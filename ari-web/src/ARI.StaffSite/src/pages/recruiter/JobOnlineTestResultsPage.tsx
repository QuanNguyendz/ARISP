import { useCallback, useEffect, useState } from 'react'
import { useParams, useLocation, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import {
  ArrowLeft,
  BarChart3,
  Loader2,
  AlertCircle,
  CheckCircle2,
  XCircle,
  Users,
  Award,
  ScrollText,
  Download,
} from 'lucide-react'
import { ErrorAlert } from '@ari/shared/ui'
import { onlineTestService } from '@ari/shared/fservices/onlineTest'
import { STAFF_ONLINE_TEST_REFRESH_EVENT } from '@ari/shared/fservices/notification/notificationService'
import type { OnlineTestJobResults } from '@ari/shared/types/onlineTest'

function errMsg(e: unknown, fallback: string): string {
  const x = e as { response?: { data?: { message?: string } } }
  return x?.response?.data?.message || fallback
}

function fmtDate(iso: string, lang: string): string {
  const d = new Date(iso)
  return d.toLocaleString(lang === 'en' ? 'en-GB' : 'vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function StatCard({
  icon: Icon,
  label,
  value,
  tone = 'ink',
}: {
  icon: typeof Users
  label: string
  value: string
  tone?: 'ink' | 'emerald' | 'brand' | 'red'
}) {
  const toneCls: Record<string, string> = {
    ink: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300',
    emerald: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/15 dark:text-emerald-400',
    brand: 'bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-400',
    red: 'bg-red-50 text-red-500 dark:bg-red-500/15 dark:text-red-400',
  }
  return (
    <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card">
      <div className="flex items-center gap-3">
        <span className={`grid h-10 w-10 shrink-0 place-items-center rounded-xl ${toneCls[tone]}`}>
          <Icon className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <p className="text-xs text-ink-500 dark:text-ink-400">{label}</p>
          <p className="text-lg font-bold text-ink-900 dark:text-white">{value}</p>
        </div>
      </div>
    </div>
  )
}

export default function JobOnlineTestResultsPage() {
  const { t, i18n } = useTranslation('modules/recruiter/onlineTest')
  const { id: jobId } = useParams<{ id: string }>()
  const location = useLocation()
  const isHr = location.pathname.startsWith('/hr')
  const backTo = isHr ? `/hr/jobs/${jobId}/online-test` : `/recruiter/my-jobs/${jobId}/online-test`

  const [data, setData] = useState<OnlineTestJobResults | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [exporting, setExporting] = useState(false)

  const doExport = async () => {
    if (!jobId) return
    setExporting(true)
    setError('')
    try {
      const { blob, fileName } = await onlineTestService.exportJobResults(jobId)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = fileName
      document.body.appendChild(a)
      a.click()
      a.remove()
      URL.revokeObjectURL(url)
    } catch (e) {
      setError(errMsg(e, t('results.exportError')))
    } finally {
      setExporting(false)
    }
  }

  const load = useCallback(async () => {
    if (!jobId) return
    try {
      setLoading(true)
      setData(await onlineTestService.getJobResults(jobId))
    } catch (e) {
      setError(errMsg(e, t('results.loadError')))
    } finally {
      setLoading(false)
    }
  }, [jobId, t])

  useEffect(() => {
    void load()
  }, [load])

  // Live update khi có ứng viên nộp bài (SignalR → window event) — refetch nền, không nháy spinner.
  useEffect(() => {
    const handler = async () => {
      if (!jobId) return
      try {
        setData(await onlineTestService.getJobResults(jobId))
      } catch {
        /* giữ nguyên bảng hiện tại nếu refetch nền lỗi */
      }
    }
    window.addEventListener(STAFF_ONLINE_TEST_REFRESH_EVENT, handler)
    return () => window.removeEventListener(STAFF_ONLINE_TEST_REFRESH_EVENT, handler)
  }, [jobId])

  const passRate =
    data && data.submissionCount > 0
      ? Math.round((data.passedCount / data.submissionCount) * 100)
      : 0

  return (
    <div className="p-6 lg:p-8">
      <Link
        to={backTo}
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('results.back')}
      </Link>

      <motion.div
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        className="mb-6 flex flex-wrap items-start justify-between gap-3"
      >
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-bold text-ink-900 dark:text-white">
            <BarChart3 className="h-6 w-6 text-brand-600 dark:text-brand-400" /> {t('results.title')}
          </h1>
          <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
            {data?.jobTitle ? t('results.jobLabel', { title: data.jobTitle }) : t('results.subtitle')}
          </p>
        </div>
        {data && data.rows.length > 0 && (
          <button
            type="button"
            onClick={doExport}
            disabled={exporting}
            className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-3.5 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}{' '}
            {t('results.export')}
          </button>
        )}
      </motion.div>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {loading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-brand-600 dark:text-brand-400" />
        </div>
      ) : !data ? null : data.totalQuestions === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 py-16 text-center shadow-card">
          <ScrollText className="h-8 w-8 text-ink-300" />
          <p className="text-sm text-ink-500 dark:text-ink-400">{t('results.noQuestions')}</p>
          <Link to={backTo} className="text-sm font-semibold text-brand-600 dark:text-brand-400 hover:underline">
            {t('results.createLink')}
          </Link>
        </div>
      ) : (
        <>
          {/* Thẻ thống kê */}
          <div className="mb-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard
              icon={Users}
              label={t('results.stats.taken')}
              value={t('results.stats.takenValue', { count: data.submissionCount })}
              tone="brand"
            />
            <StatCard
              icon={CheckCircle2}
              label={t('results.stats.passed', { score: data.passScore })}
              value={t('results.stats.passedValue', { count: data.passedCount, rate: passRate })}
              tone="emerald"
            />
            <StatCard
              icon={XCircle}
              label={t('results.stats.notPassed')}
              value={`${data.notPassedCount}`}
              tone="red"
            />
            <StatCard
              icon={Award}
              label={t('results.stats.avgHigh')}
              value={t('results.stats.avgHighValue', {
                avg: data.averageScore,
                high: Math.round(data.highestScore),
              })}
            />
          </div>

          {/* Bảng điểm */}
          <div className="overflow-hidden rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
            {data.rows.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-16 text-center">
                <AlertCircle className="h-8 w-8 text-ink-300" />
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('results.empty')}</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[720px] text-left text-sm">
                  <thead>
                    <tr className="border-b border-ink-100 dark:border-white/10 text-xs uppercase tracking-wide text-ink-400">
                      <th className="px-5 py-3 font-semibold">{t('results.table.index')}</th>
                      <th className="px-5 py-3 font-semibold">{t('results.table.candidate')}</th>
                      <th className="px-5 py-3 font-semibold">{t('results.table.round')}</th>
                      <th className="px-5 py-3 font-semibold">{t('results.table.correct')}</th>
                      <th className="px-5 py-3 font-semibold">{t('results.table.score')}</th>
                      <th className="px-5 py-3 font-semibold">{t('results.table.result')}</th>
                      <th className="px-5 py-3 font-semibold">{t('results.table.submittedAt')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-ink-100 dark:divide-white/10">
                    {data.rows.map((r, idx) => (
                      <tr key={`${r.applicationId}-${r.roundNumber}`} className="hover:bg-ink-50/60 dark:hover:bg-white/5">
                        <td className="px-5 py-3 text-ink-400">{idx + 1}</td>
                        <td className="px-5 py-3">
                          <Link
                            to={
                              isHr
                                ? `/hr/candidates/${r.applicationId}`
                                : `/recruiter/candidates/${r.applicationId}`
                            }
                            className="font-medium text-ink-900 dark:text-white hover:text-brand-600 dark:hover:text-brand-400"
                          >
                            {r.candidateName}
                          </Link>
                          <p className="text-xs text-ink-400">{r.candidateEmail}</p>
                        </td>
                        <td className="px-5 py-3 text-ink-600 dark:text-ink-300">{r.roundNumber}</td>
                        <td className="px-5 py-3 text-ink-600 dark:text-ink-300">
                          {r.correctCount}/{r.totalQuestions}
                        </td>
                        <td className="px-5 py-3 font-semibold text-ink-900 dark:text-white">
                          {Math.round(r.score)}/100
                        </td>
                        <td className="px-5 py-3">
                          {r.isPassed ? (
                            <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400">
                              <CheckCircle2 className="h-3 w-3" /> {t('results.passed')}
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2 py-0.5 text-xs font-semibold text-red-600 dark:bg-red-500/15 dark:text-red-400">
                              <XCircle className="h-3 w-3" /> {t('results.notPassed')}
                            </span>
                          )}
                        </td>
                        <td className="px-5 py-3 text-xs text-ink-500 dark:text-ink-400">
                          {fmtDate(r.submittedAt, i18n.language)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  )
}
