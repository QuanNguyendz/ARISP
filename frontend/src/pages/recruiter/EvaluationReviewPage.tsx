import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { ClipboardList, Search, X, Loader2, Lock } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState } from '@components/shared'
import { evaluationService } from '@services/evaluation/evaluationService'
import { applicationService } from '@services/application/applicationService'
import type { EvaluationReport } from '@/types/evaluation'
import { verdictBadge, verdictLabel, scoreColor, initials, timeAgo } from './_jobUi'
import { StatsGridSkeleton, ApplicantsSkeleton } from './_skeletons'

// Recruiter chỉ XEM báo cáo (không có quyền Confirm/Override — đó là quyền HR Leader).
const INTERVIEWED = new Set(['screening', 'interview', 'pass', 'not_pass'])

export default function RecruiterEvaluationReviewPage() {
  const [evals, setEvals] = useState<EvaluationReport[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [q, setQ] = useState('')
  const [filter, setFilter] = useState('all')
  const [detail, setDetail] = useState<EvaluationReport | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)

  useEffect(() => {
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        const myApps = await applicationService.getApplications(true)
        const targets = myApps.filter((a) => INTERVIEWED.has(a.status))
        const lists = await Promise.all(
          targets.map((a) =>
            evaluationService
              .getEvaluationsByApplicationId(a.id)
              .catch(() => [] as EvaluationReport[])
          )
        )
        const flat = lists
          .flat()
          .sort((x, y) => new Date(y.createdAt).getTime() - new Date(x.createdAt).getTime())
        setEvals(flat)
      } catch (e: any) {
        setError(e?.response?.data?.message || 'Không tải được danh sách đánh giá.')
      } finally {
        setLoading(false)
      }
    })()
  }, [])

  const counts = useMemo(() => {
    const pass = evals.filter((e) => (e.finalVerdict ?? e.aiVerdict) === 'pass').length
    const high = evals.filter((e) => (e.overallScore ?? 0) >= 80).length
    const reviewed = evals.filter((e) => !!e.hrReview).length
    return { total: evals.length, pass, high, reviewed }
  }, [evals])

  const filtered = useMemo(() => {
    const t = q.trim().toLowerCase()
    return evals
      .filter((e) => (filter === 'all' ? true : (e.finalVerdict ?? e.aiVerdict) === filter))
      .filter((e) =>
        t ? ((e.candidateName || '') + (e.jobTitle || '')).toLowerCase().includes(t) : true
      )
  }, [evals, q, filter])

  const openDetail = async (id: string) => {
    setDetailLoading(true)
    setDetail(null)
    try {
      setDetail(await evaluationService.getEvaluationById(id))
    } catch {
      setError('Không tải được chi tiết đánh giá.')
    } finally {
      setDetailLoading(false)
    }
  }

  const statCards = [
    { label: 'Tổng đánh giá', value: counts.total, color: 'text-brand-600' },
    { label: 'Verdict Đạt', value: counts.pass, color: 'text-emerald-600' },
    { label: 'Điểm ≥ 80', value: counts.high, color: 'text-ai-600' },
    { label: 'HR đã duyệt', value: counts.reviewed, color: 'text-amber-600' },
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Đánh giá"
        description="Xem báo cáo đánh giá AI của ứng viên (chỉ xem — duyệt kết quả do HR Leader thực hiện)"
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {loading ? (
        <>
          <StatsGridSkeleton />
          <ApplicantsSkeleton rows={6} />
        </>
      ) : (
        <>
          <StatsGrid stats={statCards} />

          <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 sm:max-w-xs sm:flex-1">
              <Search className="h-4 w-4 text-ink-400" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Tìm ứng viên, vị trí..."
                className="w-full bg-transparent text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400"
              />
            </div>
            <div className="flex flex-wrap gap-2">
              {[
                { value: 'all', label: 'Tất cả' },
                { value: 'pass', label: 'Đạt' },
                { value: 'not_pass', label: 'Không đạt' },
              ].map((f) => (
                <button
                  key={f.value}
                  onClick={() => setFilter(f.value)}
                  className={`rounded-lg px-3 py-1.5 text-xs font-medium transition-colors ${
                    filter === f.value
                      ? 'bg-brand-600 text-white'
                      : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'
                  }`}
                >
                  {f.label}
                </button>
              ))}
            </div>
          </div>

          {filtered.length === 0 ? (
            <EmptyState
              icon={<ClipboardList className="h-8 w-8 text-ink-400" />}
              title="Chưa có đánh giá"
              description="Báo cáo đánh giá sau phỏng vấn của ứng viên sẽ xuất hiện ở đây."
            />
          ) : (
            <div className="space-y-3">
              {filtered.map((ev, i) => (
                <motion.button
                  key={ev.id}
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: Math.min(i, 12) * 0.02 }}
                  onClick={() => openDetail(ev.id)}
                  className="flex w-full items-center gap-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 text-left shadow-card hover:border-brand-300 dark:hover:border-brand-500/40"
                >
                  <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-xs font-bold text-white">
                    {initials(ev.candidateName)}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-ink-900 dark:text-white">
                      {ev.candidateName || 'Ứng viên'}
                    </p>
                    <p className="truncate text-xs text-ink-500 dark:text-ink-400">
                      {ev.jobTitle || 'Vị trí'} · Vòng {ev.roundNumber} · {timeAgo(ev.createdAt)}
                    </p>
                  </div>
                  {ev.overallScore != null && (
                    <span className={`text-xl font-bold ${scoreColor(ev.overallScore)}`}>
                      {ev.overallScore}
                    </span>
                  )}
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${verdictBadge(ev.finalVerdict ?? ev.aiVerdict)}`}
                  >
                    {verdictLabel(ev.finalVerdict ?? ev.aiVerdict)}
                  </span>
                  {ev.hrReview && (
                    <span className="hidden rounded-full bg-emerald-100 dark:bg-emerald-500/20 px-2 py-0.5 text-xs text-emerald-700 dark:text-emerald-400 sm:inline">
                      HR duyệt
                    </span>
                  )}
                </motion.button>
              ))}
            </div>
          )}
        </>
      )}

      {/* Detail modal (read-only) */}
      {(detail || detailLoading) && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div
            className="absolute inset-0 bg-black/40 backdrop-blur-sm"
            onClick={() => setDetail(null)}
          />
          <div className="relative max-h-[88vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 p-6 shadow-xl">
            <button
              onClick={() => setDetail(null)}
              className="absolute right-4 top-4 grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
            >
              <X className="h-4 w-4" />
            </button>

            {detailLoading ? (
              <div className="flex flex-col items-center gap-3 py-16">
                <Loader2 className="h-8 w-8 animate-spin text-brand-600 dark:text-brand-400" />
                <p className="text-sm text-ink-500 dark:text-ink-400">Đang tải chi tiết...</p>
              </div>
            ) : detail ? (
              <div>
                <div className="mb-5 pr-10">
                  <h2 className="text-lg font-bold text-ink-900 dark:text-white">
                    Chi tiết đánh giá
                  </h2>
                  <p className="text-sm text-ink-500 dark:text-ink-400">
                    {detail.candidateName} · {detail.jobTitle} · Vòng {detail.roundNumber}
                  </p>
                </div>

                <div className="mb-5 grid grid-cols-3 gap-3">
                  <div className="rounded-xl border border-ink-100 dark:border-white/10 p-3">
                    <p className="mb-1 text-xs text-ink-400">AI Verdict</p>
                    <p className="text-sm font-semibold text-ink-900 dark:text-white">
                      {verdictLabel(detail.aiVerdict)}
                    </p>
                  </div>
                  <div className="rounded-xl border border-ink-100 dark:border-white/10 p-3">
                    <p className="mb-1 text-xs text-ink-400">Điểm tổng</p>
                    <p className={`text-sm font-semibold ${scoreColor(detail.overallScore)}`}>
                      {detail.overallScore ?? '—'}
                    </p>
                  </div>
                  <div className="rounded-xl border border-ink-100 dark:border-white/10 p-3">
                    <p className="mb-1 text-xs text-ink-400">HR review</p>
                    <p className="text-sm font-semibold text-ink-900 dark:text-white">
                      {detail.hrReview ? 'Đã duyệt' : 'Chờ duyệt'}
                    </p>
                  </div>
                </div>

                {detail.criterionScores && Object.keys(detail.criterionScores).length > 0 && (
                  <section className="mb-5">
                    <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">
                      Điểm theo tiêu chí
                    </h3>
                    <div className="grid grid-cols-2 gap-2">
                      {Object.entries(detail.criterionScores).map(([k, v]) => (
                        <div
                          key={k}
                          className="flex items-center justify-between rounded-lg border border-ink-100 dark:border-white/10 px-3 py-2"
                        >
                          <span className="text-sm text-ink-600 dark:text-ink-300">{k}</span>
                          <span className={`text-sm font-bold ${scoreColor(v)}`}>{v}</span>
                        </div>
                      ))}
                    </div>
                  </section>
                )}

                {detail.reasoning && (
                  <section className="mb-5">
                    <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">
                      Nhận định
                    </h3>
                    <p className="whitespace-pre-wrap rounded-xl border border-ink-100 dark:border-white/10 p-3 text-sm leading-7 text-ink-700 dark:text-ink-200">
                      {detail.reasoning}
                    </p>
                  </section>
                )}

                {detail.questionAnalyses && detail.questionAnalyses.length > 0 && (
                  <section className="mb-5">
                    <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-400">
                      Phân tích từng câu hỏi
                    </h3>
                    <div className="space-y-2">
                      {detail.questionAnalyses.map((qa, idx) => (
                        <div
                          key={idx}
                          className="rounded-xl border border-ink-100 dark:border-white/10 p-3"
                        >
                          <p className="mb-1 text-sm font-medium text-ink-900 dark:text-white">
                            {idx + 1}. {qa.question}
                          </p>
                          <p className="mb-1 text-sm text-ink-600 dark:text-ink-300">
                            {qa.analysis}
                          </p>
                          <p className="text-xs text-ink-400">Điểm: {qa.score}</p>
                        </div>
                      ))}
                    </div>
                  </section>
                )}

                <div className="flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 p-3 text-xs text-ink-500 dark:text-ink-400">
                  <Lock className="h-3.5 w-3.5" /> Việc xác nhận / đổi kết quả (Override) do HR
                  Leader thực hiện.
                </div>
              </div>
            ) : null}
          </div>
        </div>
      )}
    </div>
  )
}
