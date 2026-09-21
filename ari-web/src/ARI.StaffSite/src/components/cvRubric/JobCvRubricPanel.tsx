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
  Download,
  Loader2,
  Pencil,
  Scale,
  ScanSearch,
  X,
} from 'lucide-react'
import { clonePolicy, cvPolicyProblems, cvRubricProblems, cvRubricService } from '@ari/shared/fservices/cvRubric'
import type { CvRubricCriterion, CvRubricPreview, CvScoringPolicy, JobCvRubric } from '@ari/shared/fservices/cvRubric'
import { formatDateTime24 } from '@ari/shared/utils/time24'
import CvRubricEditor, { CV_SCORING_NS, ReadOnlyRubric } from './CvRubricEditor'
import CvRubricChips from './CvRubricChips'
import CvRubricPreviewPanel from './CvRubricPreviewPanel'

type ApiError = { response?: { data?: { message?: string } } }
const apiMessage = (e: unknown) => (e as ApiError)?.response?.data?.message

const stripHtml = (html?: string | null) =>
  (html ?? '').replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim()

/**
 * Thu gọn là tiện ích của RIÊNG người xem (bộ tiêu chí dài chiếm cả màn tin) — nhớ theo trình duyệt, dùng chung
 * cho mọi tin. Storage có thể bị chặn (chế độ riêng tư) nên mọi lần đọc/ghi đều bọc try/catch.
 */
const COLLAPSED_KEY = 'ari:job-cv-rubric:collapsed'

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
    /* không nhớ được thì thôi — vẫn thu gọn trong phiên này */
  }
}

interface JobCvRubricPanelProps {
  jobPostingId: string
  /** Nội dung tin để AI gợi ý bộ tiêu chí. */
  job: {
    title: string
    jobDescription?: string | null
    experienceLevel?: string | null
    skills?: string[] | null
  }
}

/** Câu báo sau khi lưu — nói đúng việc hệ thống sẽ làm (ADR-075: đổi công thức thì tính lại, không gọi AI). */
const savedNoticeKey = (saved: JobCvRubric) =>
  saved.saveOutcome?.mode === 'recompute'
    ? 'panel.savedRecompute'
    : saved.saveOutcome?.mode === 'unchanged'
      ? 'panel.savedUnchanged'
      : 'panel.saved'

/**
 * Bộ tiêu chí + công thức chấm CV của tin, ngay trong màn tin (ADR-070/075).
 *
 * Mọi thành viên đội đọc được; quyền sửa do SERVER quyết (`canEdit` — HM chính hoặc quản trị viên). Trước khi lưu,
 * HM "Xem trước tác động": điểm / khuyến nghị của từng hồ sơ sẽ đổi thế nào, và bao nhiêu hồ sơ phải hỏi AI lại.
 */
export default function JobCvRubricPanel({ jobPostingId, job }: JobCvRubricPanelProps) {
  const { t } = useTranslation(CV_SCORING_NS)
  const queryClient = useQueryClient()
  const [draft, setDraftState] = useState<CvRubricCriterion[] | null>(null)
  const [draftPolicy, setDraftPolicyState] = useState<CvScoringPolicy | null>(null)
  const [preview, setPreview] = useState<CvRubricPreview | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [tried, setTried] = useState(false)
  const [collapsed, setCollapsed] = useState(readCollapsed)

  const toggleCollapsed = () =>
    setCollapsed((prev) => {
      writeCollapsed(!prev)
      return !prev
    })

  const key = ['job-cv-rubric', jobPostingId]
  const { data, isLoading, isError } = useQuery({
    queryKey: key,
    queryFn: () => cvRubricService.getForJob(jobPostingId),
    enabled: !!jobPostingId,
  })

  // Sửa bản nháp thì bản xem trước cũ không còn đúng — bỏ đi để HM không đọc nhầm số của bản trước.
  const setDraft = (next: CvRubricCriterion[] | null) => {
    setDraftState(next)
    setPreview(null)
  }
  const setDraftPolicy = (next: CvScoringPolicy | null) => {
    setDraftPolicyState(next)
    setPreview(null)
  }

  const save = useMutation({
    mutationFn: ({ criteria, policy }: { criteria: CvRubricCriterion[]; policy: CvScoringPolicy }) =>
      cvRubricService.saveForJob(jobPostingId, criteria, policy),
    onSuccess: (saved) => {
      queryClient.setQueryData(key, saved)
      queryClient.invalidateQueries({ queryKey: ['job', jobPostingId] })
      queryClient.invalidateQueries({ queryKey: ['applications'] })
      setDraft(null)
      setDraftPolicy(null)
      setTried(false)
      setError(null)
      setNotice(t(savedNoticeKey(saved), { count: saved.saveOutcome?.affected ?? 0 }))
    },
    onError: (e) => setError(apiMessage(e) ?? t('editor.error')),
  })

  const previewRun = useMutation({
    mutationFn: ({ criteria, policy }: { criteria: CvRubricCriterion[]; policy: CvScoringPolicy }) =>
      cvRubricService.previewForJob(jobPostingId, criteria, policy),
    onSuccess: (result) => {
      setError(null)
      setPreview(result)
    },
    onError: (e) => setError(apiMessage(e) ?? t('editor.error')),
  })

  const editing = draft !== null
  const criteria = data?.criteria ?? []
  const hasRubric = criteria.length > 0
  const draftValid =
    !!draft && !!draftPolicy && cvRubricProblems(draft).length === 0 && cvPolicyProblems(draftPolicy).length === 0

  const startEdit = () => {
    setNotice(null)
    setError(null)
    setDraft(
      criteria.map((c) => ({
        ...c,
        levels: c.levels ? { ...c.levels } : null,
        checks: c.checks ? c.checks.map((x) => ({ ...x })) : [],
      }))
    )
    setDraftPolicy(clonePolicy(data?.policy))
  }

  const cancel = () => {
    setDraft(null)
    setDraftPolicy(null)
    setTried(false)
    setError(null)
  }

  const submit = () => {
    if (!draft || !draftPolicy) return
    setTried(true)
    if (!draftValid) return
    save.mutate({ criteria: draft, policy: draftPolicy })
  }

  const runPreview = () => {
    if (!draft || !draftPolicy) return
    setTried(true)
    if (!draftValid) return
    previewRun.mutate({ criteria: draft, policy: draftPolicy })
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

  return (
    <section
      id="cv-rubric"
      className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5"
    >
      <div className="mb-2 flex items-start justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
          <Scale className="h-4 w-4 text-ai-600 dark:text-ai-400" /> {t('panel.title')}
          {hasRubric && !editing && (
            <span className="whitespace-nowrap rounded-full bg-ai-100 px-2 py-0.5 text-[10px] font-semibold text-ai-700 dark:bg-ai-500/20 dark:text-ai-300">
              {t('panel.criteriaCount', { count: criteria.length })}
            </span>
          )}
        </h2>
        <div className="flex shrink-0 items-center gap-1.5">
          {data?.canEdit && !editing && (
            <button
              type="button"
              onClick={startEdit}
              className="inline-flex shrink-0 items-center gap-1 rounded-lg bg-brand-600 px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-brand-700"
            >
              <Pencil className="h-3.5 w-3.5" /> {hasRubric ? t('panel.edit') : t('panel.create')}
            </button>
          )}
          {hasRubric && (
            <button
              type="button"
              onClick={toggleCollapsed}
              aria-expanded={!collapsed}
              aria-controls="cv-rubric-body"
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
      ) : isError ? (
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

          {!hasRubric && !editing && (
            <div className="mb-3 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300">
              <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {t('panel.missing')}
            </div>
          )}

          {/* Thu gọn: một dòng tên tiêu chí + trọng số, đủ để nhớ tin chấm theo gì mà không chiếm chỗ. */}
          {hasRubric && collapsed && (
            <CvRubricChips
              criteria={criteria}
              onExpand={toggleCollapsed}
              title={t('panel.expand')}
              trailing={
                (data?.pendingCount ?? 0) > 0 && (
                  <span className="inline-flex items-center gap-1 text-xs text-ai-700 dark:text-ai-300">
                    <Loader2 className="h-3 w-3 animate-spin" />
                    {t('panel.pending', { count: data?.pendingCount ?? 0 })}
                  </span>
                )
              }
            />
          )}

          {hasRubric && !collapsed && (
            <div id="cv-rubric-body">
              <ReadOnlyRubric criteria={criteria} policy={data?.policy ?? null} />
              <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs text-ink-500 dark:text-ink-400">
                <span>
                  {data?.savedAt &&
                    t('panel.savedBy', { time: formatDateTime24(data.savedAt), name: data.savedBy ?? '—' })}
                  {(data?.pendingCount ?? 0) > 0 && (
                    <span className="ml-2 inline-flex items-center gap-1 text-ai-700 dark:text-ai-300">
                      <Loader2 className="h-3 w-3 animate-spin" />
                      {t('panel.pending', { count: data?.pendingCount ?? 0 })}
                    </span>
                  )}
                </span>
                <button
                  type="button"
                  onClick={() => void cvRubricService.downloadForJob(jobPostingId).catch(() => setError(t('editor.error')))}
                  className="inline-flex items-center gap-1 font-medium text-brand-600 hover:underline dark:text-brand-400"
                >
                  <Download className="h-3.5 w-3.5" /> {t('panel.download')}
                </button>
              </div>
            </div>
          )}

          {!data?.canEdit && !editing && !(hasRubric && collapsed) && (
            <p className="mt-3 text-xs text-ink-400">{t('panel.readOnlyHint')}</p>
          )}
        </>
      )}
      {/* Soạn trong hộp thoại rộng: panel thường nằm ở cột hẹp bên phải màn tin, không đủ chỗ cho
          bốn ô mức neo của từng tiêu chí. Portal ra body để `fixed` không bị thẻ cha có transform giữ lại. */}
      {editing && draft && draftPolicy && createPortal(
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-ink-950/60 backdrop-blur-sm" onClick={cancel} />
          <div className="relative max-h-[90vh] w-full max-w-4xl overflow-y-auto rounded-2xl border border-ink-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-ink-900">
            <div className="mb-4 flex items-start justify-between gap-3">
              <div>
                <h3 className="flex items-center gap-2 text-lg font-semibold text-ink-900 dark:text-white">
                  <Scale className="h-5 w-5 text-ai-600 dark:text-ai-400" /> {t('panel.title')}
                </h3>
                <p className="mt-0.5 text-sm text-ink-500 dark:text-ink-400">{job.title}</p>
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
              <CvRubricEditor
                value={draft}
                onChange={setDraft}
                suggestSource={suggestSource}
                showProblems={tried}
                policy={draftPolicy}
                onPolicyChange={setDraftPolicy}
              />

              {(data?.applicationCount ?? 0) > 0 && hasRubric && !preview && (
                <div className="flex items-start gap-2 rounded-xl border border-ai-200 bg-ai-50 px-3 py-2 text-xs text-ai-800 dark:border-ai-500/30 dark:bg-ai-500/10 dark:text-ai-200">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  {t('panel.rescoreWarning', { count: data?.applicationCount ?? 0 })}
                </div>
              )}

              {preview && <CvRubricPreviewPanel preview={preview} />}

              {error && (
                <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
                  <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {error}
                </div>
              )}

              <div className="flex flex-wrap justify-end gap-2 border-t border-ink-100 pt-4 dark:border-white/10">
                {hasRubric && (data?.applicationCount ?? 0) > 0 && (
                  <button
                    type="button"
                    onClick={runPreview}
                    disabled={previewRun.isPending || save.isPending}
                    className="mr-auto inline-flex items-center gap-1.5 rounded-xl border border-ai-300 px-4 py-2 text-sm font-medium text-ai-700 hover:bg-ai-50 disabled:opacity-50 dark:border-ai-500/40 dark:text-ai-300 dark:hover:bg-ai-500/10"
                  >
                    {previewRun.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <ScanSearch className="h-4 w-4" />}
                    {t('panel.preview')}
                  </button>
                )}
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
        document.body,
      )}
    </section>
  )
}
