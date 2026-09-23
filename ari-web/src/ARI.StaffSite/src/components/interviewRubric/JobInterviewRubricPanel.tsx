import { useState } from 'react'
import { createPortal } from 'react-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Hourglass,
  Loader2,
  MessagesSquare,
  Pencil,
  Plus,
  Trash2,
  X,
} from 'lucide-react'
import { cvRubricProblems } from '@ari/shared/fservices/cvRubric'
import type { CvRubricCriterion } from '@ari/shared/fservices/cvRubric'
import { interviewRubricService } from '@ari/shared/fservices/interviewRubric'
import type { InterviewRubricSet } from '@ari/shared/fservices/interviewRubric'
import { formatDateTime24 } from '@ari/shared/utils/time24'
import CvRubricEditor, { CV_SCORING_NS, ReadOnlyRubric } from '@/components/cvRubric/CvRubricEditor'
import CvRubricChips from '@/components/cvRubric/CvRubricChips'

type ApiError = { response?: { data?: { message?: string } } }
const apiMessage = (e: unknown) => (e as ApiError)?.response?.data?.message

const stripHtml = (html?: string | null) =>
  (html ?? '').replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim()

/** Thu gọn là tiện ích riêng của người xem — nhớ theo trình duyệt; storage có thể bị chặn nên bọc try/catch. */
const COLLAPSED_KEY = 'ari:job-interview-rubric:collapsed'
const readCollapsed = (): boolean => {
  try {
    return window.localStorage.getItem(COLLAPSED_KEY) === '1'
  } catch {
    return false
  }
}
const writeCollapsed = (collapsed: boolean) => {
  try {
    window.localStorage.setItem(COLLAPSED_KEY, collapsed ? '1' : '0')
  } catch {
    /* không nhớ được thì thôi */
  }
}

const cloneCriteria = (criteria: CvRubricCriterion[]): CvRubricCriterion[] =>
  criteria.map((c) => ({ ...c, levels: c.levels ? { ...c.levels } : null, checks: null }))

interface JobInterviewRubricPanelProps {
  jobPostingId: string
  /** Nội dung tin để AI gợi ý bộ tiêu chí. */
  job: {
    title: string
    jobDescription?: string | null
    experienceLevel?: string | null
    skills?: string[] | null
  }
}

/** Đang soạn bộ nào: bộ chung (`round` null) hay bộ riêng của một vòng. */
type Editing = { round: number | null; criteria: CvRubricCriterion[] }

/**
 * Bộ tiêu chí chấm PHỎNG VẤN của tin, ngay trong màn tin (ADR-073).
 *
 * Hiring Manager khai cho từng tin — không có bộ công ty dự phòng. Một bộ CHUNG áp mọi vòng hội thoại; vòng cần
 * chấm khác thì có bộ RIÊNG. Thiếu bộ tiêu chí thì buổi phỏng vấn của vòng đó không ra báo cáo (và tin không đăng
 * được), nên panel nói rõ vòng nào đang thiếu và bao nhiêu buổi đang chờ. Khai xong là các buổi đó tự được chấm.
 */
export default function JobInterviewRubricPanel({ jobPostingId, job }: JobInterviewRubricPanelProps) {
  const { t } = useTranslation(CV_SCORING_NS)
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Editing | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [tried, setTried] = useState(false)
  const [collapsed, setCollapsed] = useState(readCollapsed)

  const toggleCollapsed = () =>
    setCollapsed((prev) => {
      writeCollapsed(!prev)
      return !prev
    })

  const key = ['job-interview-rubric', jobPostingId]
  const { data, isLoading, isError } = useQuery({
    queryKey: key,
    queryFn: () => interviewRubricService.getForJob(jobPostingId),
    enabled: !!jobPostingId,
  })

  const afterChange = (next: Awaited<ReturnType<typeof interviewRubricService.getForJob>>, message: string) => {
    queryClient.setQueryData(key, next)
    queryClient.invalidateQueries({ queryKey: ['job', jobPostingId] })
    // Khối "Kết quả phỏng vấn" nằm dưới tiền tố ['evaluations'] — buổi "chờ bộ tiêu chí" chuyển sang "AI đang chấm".
    queryClient.invalidateQueries({ queryKey: ['evaluations'] })
    setEditing(null)
    setTried(false)
    setError(null)
    setNotice(message)
  }

  const save = useMutation({
    mutationFn: (e: Editing) => interviewRubricService.save(jobPostingId, e.round, e.criteria),
    onSuccess: (next) =>
      afterChange(next, next.waitingSessionCount > 0 ? t('interviewPanel.savedWithWaiting') : t('interviewPanel.saved')),
    onError: (e) => setError(apiMessage(e) ?? t('editor.error')),
  })

  const removeRound = useMutation({
    mutationFn: (round: number) => interviewRubricService.removeRound(jobPostingId, round),
    onSuccess: (next) => afterChange(next, t('interviewPanel.roundRemoved')),
    onError: (e) => setError(apiMessage(e) ?? t('editor.error')),
  })

  const jobCriteria = data?.jobLevel.criteria ?? []
  const hasJobLevel = jobCriteria.length > 0
  const roundSet = (round: number): InterviewRubricSet | undefined =>
    data?.roundSets.find((s) => s.roundNumber === round)
  const missing = data?.missingRounds ?? []
  const waiting = data?.waitingSessionCount ?? 0
  const busy = save.isPending || removeRound.isPending

  const startEdit = (round: number | null) => {
    setNotice(null)
    setError(null)
    const source = round == null ? jobCriteria : (roundSet(round)?.criteria ?? jobCriteria)
    setEditing({ round, criteria: cloneCriteria(source) })
  }

  const cancel = () => {
    setEditing(null)
    setTried(false)
    setError(null)
  }

  const submit = () => {
    if (!editing) return
    setTried(true)
    if (cvRubricProblems(editing.criteria).length > 0) return
    save.mutate(editing)
  }

  const suggestSource = () =>
    job.title.trim()
      ? {
          title: job.title,
          description: stripHtml(job.jobDescription),
          requirements: null,
          experienceLevel: job.experienceLevel ?? null,
          skills: job.skills ?? null,
        }
      : null

  const roundLabel = (round: number, type?: string) =>
    t('interviewPanel.roundLabel', { n: round, type: t(`interviewPanel.roundType.${type ?? 'screening'}`) })

  const editingTitle = editing
    ? editing.round == null
      ? t('interviewPanel.jobLevelTitle')
      : roundLabel(editing.round, data?.rounds.find((r) => r.roundNumber === editing.round)?.roundType)
    : ''

  return (
    <section
      id="interview-rubric"
      className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5"
    >
      <div className="mb-2 flex items-start justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
          <MessagesSquare className="h-4 w-4 text-ai-600 dark:text-ai-400" /> {t('interviewPanel.title')}
          {hasJobLevel && (
            <span className="whitespace-nowrap rounded-full bg-ai-100 px-2 py-0.5 text-[10px] font-semibold text-ai-700 dark:bg-ai-500/20 dark:text-ai-300">
              {t('panel.criteriaCount', { count: jobCriteria.length })}
            </span>
          )}
        </h2>
        <div className="flex shrink-0 items-center gap-1.5">
          {data?.canEdit && (
            <button
              type="button"
              onClick={() => startEdit(null)}
              disabled={busy}
              className="inline-flex shrink-0 items-center gap-1 rounded-lg bg-brand-600 px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
            >
              <Pencil className="h-3.5 w-3.5" /> {hasJobLevel ? t('panel.edit') : t('panel.create')}
            </button>
          )}
          {hasJobLevel && (
            <button
              type="button"
              onClick={toggleCollapsed}
              aria-expanded={!collapsed}
              aria-controls="interview-rubric-body"
              aria-label={collapsed ? t('panel.expand') : t('panel.collapse')}
              title={collapsed ? t('panel.expand') : t('panel.collapse')}
              className="grid h-7 w-7 shrink-0 place-items-center rounded-lg border border-ink-200 text-ink-500 hover:bg-ink-50 hover:text-ink-700 dark:border-white/10 dark:text-ink-400 dark:hover:bg-white/10 dark:hover:text-ink-200"
            >
              {collapsed ? <ChevronDown className="h-4 w-4" /> : <ChevronUp className="h-4 w-4" />}
            </button>
          )}
        </div>
      </div>

      {isLoading ? (
        <p className="flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
          <Loader2 className="h-4 w-4 animate-spin" />
        </p>
      ) : isError || !data ? (
        <p className="text-sm text-red-600 dark:text-red-400">{t('panel.loadError')}</p>
      ) : (
        <>
          {notice && (
            <div className="mb-3 flex items-start gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-400">
              <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {notice}
            </div>
          )}

          {error && !editing && (
            <div className="mb-3 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
              <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {error}
            </div>
          )}

          {missing.length > 0 && (
            <div className="mb-3 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300">
              <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              <span>{t('interviewPanel.missing', { rounds: missing.join(', ') })}</span>
            </div>
          )}

          {waiting > 0 && (
            <div className="mb-3 flex items-start gap-2 rounded-xl border border-ai-200 bg-ai-50 px-3 py-2 text-xs text-ai-800 dark:border-ai-500/30 dark:bg-ai-500/10 dark:text-ai-200">
              <Hourglass className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              <span>{t('interviewPanel.waiting', { count: waiting })}</span>
            </div>
          )}

          {hasJobLevel && collapsed && (
            <CvRubricChips criteria={jobCriteria} onExpand={toggleCollapsed} title={t('panel.expand')} />
          )}

          {!collapsed && (
            <div id="interview-rubric-body" className="space-y-4">
              {hasJobLevel && (
                <div>
                  <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-ink-500 dark:text-ink-400">
                    {t('interviewPanel.jobLevelTitle')}
                  </p>
                  <ReadOnlyRubric criteria={jobCriteria} />
                  {data.jobLevel.savedAt && (
                    <p className="mt-2 text-xs text-ink-500 dark:text-ink-400">
                      {t('panel.savedBy', { time: formatDateTime24(data.jobLevel.savedAt), name: data.jobLevel.savedBy ?? '—' })}
                    </p>
                  )}
                </div>
              )}

              {data.rounds.length > 0 && (
                <div>
                  <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-ink-500 dark:text-ink-400">
                    {t('interviewPanel.roundsTitle')}
                  </p>
                  <ul className="space-y-2">
                    {data.rounds.map((r) => {
                      const own = roundSet(r.roundNumber)
                      return (
                        <li
                          key={r.roundNumber}
                          className="rounded-xl border border-ink-100 px-3 py-2 dark:border-white/10"
                        >
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <span className="text-sm font-medium text-ink-900 dark:text-white">
                              {roundLabel(r.roundNumber, r.roundType)}
                            </span>
                            <span className="flex items-center gap-2">
                              <span
                                className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                                  !r.covered
                                    ? 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300'
                                    : 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
                                }`}
                              >
                                {!r.covered
                                  ? t('interviewPanel.roundMissing')
                                  : own
                                    ? t('interviewPanel.roundOwn', { count: own.criteria.length })
                                    : t('interviewPanel.roundUsesJob')}
                              </span>
                              {data.canEdit && (
                                <>
                                  <button
                                    type="button"
                                    onClick={() => startEdit(r.roundNumber)}
                                    disabled={busy}
                                    className="inline-flex items-center gap-1 text-xs font-medium text-brand-600 hover:underline disabled:opacity-50 dark:text-brand-400"
                                  >
                                    {own ? <Pencil className="h-3.5 w-3.5" /> : <Plus className="h-3.5 w-3.5" />}
                                    {own ? t('interviewPanel.editOwn') : t('interviewPanel.createOwn')}
                                  </button>
                                  {own && (
                                    <button
                                      type="button"
                                      onClick={() => removeRound.mutate(r.roundNumber)}
                                      disabled={busy}
                                      className="inline-flex items-center gap-1 text-xs font-medium text-red-600 hover:underline disabled:opacity-50 dark:text-red-400"
                                    >
                                      <Trash2 className="h-3.5 w-3.5" /> {t('interviewPanel.removeOwn')}
                                    </button>
                                  )}
                                </>
                              )}
                            </span>
                          </div>
                          {own && (
                            <div className="mt-2">
                              <ReadOnlyRubric criteria={own.criteria} />
                            </div>
                          )}
                        </li>
                      )
                    })}
                  </ul>
                </div>
              )}

              {!hasJobLevel && (data.roundSets.length === 0) && (
                <p className="text-xs text-ink-500 dark:text-ink-400">{t('interviewPanel.empty')}</p>
              )}
            </div>
          )}

          {!data.canEdit && !(hasJobLevel && collapsed) && (
            <p className="mt-3 text-xs text-ink-400">{t('panel.readOnlyHint')}</p>
          )}
        </>
      )}

      {/* Soạn trong hộp thoại rộng — cùng lý do với bộ tiêu chí chấm CV: bốn ô mức neo không vừa cột hẹp. */}
      {editing &&
        createPortal(
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-ink-950/60 backdrop-blur-sm" onClick={cancel} />
            <div className="relative max-h-[90vh] w-full max-w-4xl overflow-y-auto rounded-2xl border border-ink-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-ink-900">
              <div className="mb-4 flex items-start justify-between gap-3">
                <div>
                  <h3 className="flex items-center gap-2 text-lg font-semibold text-ink-900 dark:text-white">
                    <MessagesSquare className="h-5 w-5 text-ai-600 dark:text-ai-400" /> {t('interviewPanel.title')}
                  </h3>
                  <p className="mt-0.5 text-sm text-ink-500 dark:text-ink-400">
                    {job.title} · {editingTitle}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={cancel}
                  aria-label={t('panel.cancel')}
                  className="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>

              <div className="space-y-3">
                {editing.round != null && (
                  <p className="rounded-xl border border-ink-100 bg-ink-50 px-3 py-2 text-xs text-ink-600 dark:border-white/10 dark:bg-white/5 dark:text-ink-300">
                    {t('interviewPanel.roundEditHint')}
                  </p>
                )}
                <CvRubricEditor
                  mode="interview"
                  value={editing.criteria}
                  onChange={(criteria) => setEditing((prev) => (prev ? { ...prev, criteria } : prev))}
                  suggestSource={suggestSource}
                  showProblems={tried}
                />

                {error && (
                  <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
                    <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {error}
                  </div>
                )}

                <div className="flex justify-end gap-2 border-t border-ink-100 pt-4 dark:border-white/10">
                  <button
                    type="button"
                    onClick={cancel}
                    className="rounded-xl border border-ink-200 px-4 py-2 text-sm font-medium text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/10"
                  >
                    {t('panel.cancel')}
                  </button>
                  <button
                    type="button"
                    onClick={submit}
                    disabled={save.isPending}
                    className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
                  >
                    {save.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                    {save.isPending ? t('panel.saving') : t('panel.save')}
                  </button>
                </div>
              </div>
            </div>
          </div>,
          document.body
        )}
    </section>
  )
}
