import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  AlertCircle,
  ChevronDown,
  ChevronUp,
  Download,
  FileSpreadsheet,
  LayoutTemplate,
  ListChecks,
  Loader2,
  Plus,
  Scale,
  ShieldCheck,
  Sparkles,
  SquareCheck,
  Trash2,
  Upload,
  X,
} from 'lucide-react'
import {
  CV_RUBRIC_LEVEL_KEYS,
  CV_RUBRIC_MAX_CHECKS,
  CV_RUBRIC_MAX_KNOCKOUTS,
  bandRanges,
  checkCount,
  clonePolicy,
  cvPolicyProblems,
  cvRubricProblems,
  cvRubricService,
  isKnockout,
  totalWeight,
} from '@ari/shared/fservices/cvRubric'
import type {
  CvRubricCheck,
  CvRubricCriterion,
  CvRubricDraft,
  CvRubricLevelKey,
  CvRubricSuggestionInput,
  CvScoringPolicy,
} from '@ari/shared/fservices/cvRubric'
import CvScoringFormulaEditor, { FormulaSummary } from './CvScoringFormulaEditor'
import { interviewRubricService } from '@ari/shared/fservices/interviewRubric'
import { playbookService } from '@/fservices/playbook/playbookService'

export const CV_SCORING_NS = 'modules/staff/cvScoring'

type ApiError = { response?: { data?: { message?: string } } }
const apiMessage = (e: unknown) => (e as ApiError)?.response?.data?.message

const INPUT =
  'w-full rounded-xl border border-ink-200 bg-white px-3 py-2 text-sm text-ink-900 placeholder:text-ink-400 focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/5 dark:text-white'
const TOOL_BTN =
  'inline-flex items-center gap-1.5 rounded-xl border border-ink-200 bg-white px-3 py-1.5 text-xs font-semibold text-ink-700 hover:bg-ink-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-white/10 dark:bg-white/5 dark:text-ink-200 dark:hover:bg-white/10'

let uidSeed = 0
const nextUid = () => `c${++uidSeed}`

const levelCount = (c: CvRubricCriterion) =>
  CV_RUBRIC_LEVEL_KEYS.filter((k) => !!c.levels?.[k]?.trim()).length

/** Bộ tiêu chí chấm CV (ADR-070) hay chấm PHỎNG VẤN (ADR-073) — cùng trình soạn, khác nguồn điền nhanh và lời gợi ý. */
export type RubricEditorMode = 'cv' | 'interview'

/** Chuỗi có bản riêng cho bộ tiêu chí phỏng vấn (khoá `interviewEditor.*`); còn lại dùng chung `editor.*`. */
const INTERVIEW_TEXT = new Set([
  'hint',
  'namePlaceholder',
  'guidePlaceholder',
  'levelsHint',
  'levelPlaceholder.excellent',
  'levelPlaceholder.good',
  'levelPlaceholder.fair',
  'levelPlaceholder.poor',
  'noTemplates',
  'empty',
  'aiNeedsContent',
])

const MODE_API = {
  cv: {
    templatesKey: 'cv-rubric-templates',
    templates: () => cvRubricService.templates(),
    suggest: (input: CvRubricSuggestionInput) => cvRubricService.suggest(input),
    exportName: 'bo-tieu-chi-cham-cv.xlsx',
    blankType: 'cv_rubric',
  },
  interview: {
    templatesKey: 'interview-rubric-templates',
    templates: () => interviewRubricService.templates(),
    suggest: (input: CvRubricSuggestionInput) => interviewRubricService.suggest(input),
    exportName: 'bo-tieu-chi-cham-phong-van.xlsx',
    blankType: 'interview_rubric',
  },
} as const

interface CvRubricEditorProps {
  value: CvRubricCriterion[]
  onChange: (next: CvRubricCriterion[]) => void
  readOnly?: boolean
  /**
   * Nội dung để AI gợi ý (phiếu hoặc tin). Không truyền → ẩn nút "AI gợi ý". Trả `null` khi chưa đủ nội
   * dung — trình soạn nhắc người dùng điền trước.
   */
  suggestSource?: () => CvRubricSuggestionInput | null
  /** Hiện lỗi hợp lệ (tổng ≠ 100…) ngay cả khi người dùng chưa chạm vào — dùng lúc bấm Gửi/Lưu. */
  showProblems?: boolean
  /** Mặc định `cv`. `interview`: không có ý kiểm, mẫu + AI gợi ý lấy từ nguồn của bộ tiêu chí phỏng vấn. */
  mode?: RubricEditorMode
  /**
   * Công thức cấp tin (ADR-075) — chỉ bộ CV. Truyền cả `onPolicyChange` thì trình soạn hiện khối "Công thức chấm";
   * Excel / mẫu công ty mang công thức theo thì điền luôn công thức.
   */
  policy?: CvScoringPolicy
  onPolicyChange?: (next: CvScoringPolicy) => void
}

/** Trọng số ý kiểm cho phép (ADR-075) — ×1 / ×2 / ×3. */
const CHECK_WEIGHTS = [1, 2, 3] as const

/**
 * Trình soạn bộ tiêu chí chấm CV (ADR-070) — một component cho form phiếu của HM, panel ở màn tin, và
 * khối chỉ đọc ở màn tạo tin.
 *
 * Mã tiêu chí KHÔNG hiện cho người dùng: server tự sinh từ tên, và giữ nguyên mã cũ khi sửa tên — nên
 * editor luôn gửi lại `key` đã nhận. Mọi cách điền nhanh (AI, mẫu công ty, Excel) chỉ thay nội dung trình
 * soạn; người dùng xem lại rồi mới lưu.
 */
export default function CvRubricEditor({
  value,
  onChange,
  readOnly = false,
  suggestSource,
  showProblems = false,
  mode = 'cv',
  policy,
  onPolicyChange,
}: CvRubricEditorProps) {
  const { t } = useTranslation(CV_SCORING_NS)
  const api = MODE_API[mode]
  const forInterview = mode === 'interview'
  /** Khoá chuỗi theo chế độ — xem INTERVIEW_TEXT. */
  const tk = (key: string) => (forInterview && INTERVIEW_TEXT.has(key) ? `interviewEditor.${key}` : `editor.${key}`)
  const fileRef = useRef<HTMLInputElement>(null)
  const [uids, setUids] = useState<string[]>(() => value.map(nextUid))
  const [openLevels, setOpenLevels] = useState<Record<string, boolean>>({})
  const [touched, setTouched] = useState(false)
  const [busy, setBusy] = useState<null | 'suggest' | 'import' | 'export' | 'blank'>(null)
  const [error, setError] = useState<string | null>(null)
  const [warnings, setWarnings] = useState<string[]>([])
  const [pending, setPending] = useState<CvRubricDraft | null>(null)
  const [templatesOpen, setTemplatesOpen] = useState(false)

  // Giá trị thay từ bên ngoài (tải xong, huỷ sửa) → khoá dòng mới cho khớp độ dài.
  useEffect(() => {
    if (uids.length !== value.length) setUids(value.map(nextUid))
  }, [value, uids.length])

  const templates = useQuery({
    queryKey: [api.templatesKey],
    queryFn: () => api.templates(),
    enabled: templatesOpen,
    staleTime: 60_000,
  })

  const commit = (next: CvRubricCriterion[], nextUids: string[]) => {
    setTouched(true)
    setUids(nextUids)
    onChange(next)
  }

  const patch = (i: number, change: Partial<CvRubricCriterion>) =>
    commit(value.map((c, idx) => (idx === i ? { ...c, ...change } : c)), uids)

  const patchLevel = (i: number, level: CvRubricLevelKey, text: string) =>
    patch(i, { levels: { ...(value[i].levels ?? {}), [level]: text } })

  const patchChecks = (i: number, update: (checks: CvRubricCheck[]) => CvRubricCheck[]) =>
    patch(i, { checks: update(value[i].checks ?? []) })

  const add = () =>
    commit(
      [...value, { name: '', weight: 0, description: '', levels: null, checks: [], kind: null, minScore: null }],
      [...uids, nextUid()]
    )

  const remove = (i: number) =>
    commit(value.filter((_, idx) => idx !== i), uids.filter((_, idx) => idx !== i))

  const move = (i: number, dir: -1 | 1) => {
    const j = i + dir
    if (j < 0 || j >= value.length) return
    const next = [...value]
    const nextUids = [...uids]
    ;[next[i], next[j]] = [next[j], next[i]]
    ;[nextUids[i], nextUids[j]] = [nextUids[j], nextUids[i]]
    commit(next, nextUids)
  }

  /** Chia đều 100 theo phần dư lớn nhất — chỉ trên tiêu chí chấm điểm (điều kiện bắt buộc không mang trọng số). */
  const balance = () => {
    const n = value.filter((c) => !isKnockout(c)).length
    if (n === 0) return
    const base = Math.floor(100 / n)
    let rest = 100 - base * n
    commit(value.map((c) => (isKnockout(c) ? c : { ...c, weight: base + (rest-- > 0 ? 1 : 0) })), uids)
  }

  /** Bản nháp từ AI / mẫu / Excel: danh sách trống thì thay luôn, có sẵn thì hỏi trước. */
  const offer = (draft: CvRubricDraft) => {
    setError(null)
    if (value.some((c) => c.name.trim())) {
      setPending(draft)
      return
    }
    apply(draft)
  }

  const apply = (draft: CvRubricDraft) => {
    setPending(null)
    setWarnings(draft.warnings ?? [])
    // Bản từ nguồn khác không mang mã của tin này — bỏ mã để server sinh lại, tránh đè lên tiêu chí cũ.
    // Bộ tiêu chí phỏng vấn không có ý kiểm, điều kiện bắt buộc, điểm tối thiểu: có trong file / mẫu CV thì bỏ ở đây.
    const criteria = draft.criteria.map((c) =>
      forInterview ? { ...c, key: null, checks: null, kind: null, minScore: null } : { ...c, key: null }
    )
    commit(criteria, criteria.map(nextUid))
    // File Excel / mẫu công ty mang công thức theo → điền luôn (ADR-075). Không có thì giữ công thức đang soạn.
    if (!forInterview && draft.policy && onPolicyChange) onPolicyChange(clonePolicy(draft.policy))
  }

  const run = async (kind: NonNullable<typeof busy>, action: () => Promise<void>) => {
    setBusy(kind)
    setError(null)
    try {
      await action()
    } catch (e) {
      setError(apiMessage(e) ?? t('editor.error'))
    } finally {
      setBusy(null)
    }
  }

  const suggest = () => {
    const source = suggestSource?.()
    if (!source) {
      setError(t(tk('aiNeedsContent')))
      return
    }
    void run('suggest', async () => offer(await api.suggest(source)))
  }

  const importFile = (file: File | undefined) => {
    if (fileRef.current) fileRef.current.value = ''
    if (!file) return
    void run('import', async () => offer(await cvRubricService.parseSheet(file, mode)))
  }

  const problems = cvRubricProblems(value)
  const total = totalWeight(value)
  const totalOk = Math.abs(total - 100) <= 0.01
  const withFormula = !forInterview && !!policy && !!onPolicyChange
  // Nhãn dải in đúng ngưỡng của công thức đang soạn (bộ phỏng vấn dùng ngưỡng mặc định).
  const ranges = bandRanges(!forInterview && policy && cvPolicyProblems(policy).length === 0 ? policy : undefined)
  const knockoutCount = value.filter(isKnockout).length

  if (readOnly) return <ReadOnlyRubric criteria={value} policy={forInterview ? undefined : policy} />

  return (
    <div className="space-y-3">
      <p className="text-xs leading-relaxed text-ink-500 dark:text-ink-400">{t(tk('hint'))}</p>

      <div className="flex flex-wrap items-center gap-2">
        {suggestSource && (
          <button
            type="button"
            onClick={suggest}
            disabled={busy !== null}
            className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-1.5 text-xs font-semibold text-white hover:opacity-90 disabled:opacity-50"
          >
            {busy === 'suggest' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
            {busy === 'suggest' ? t('editor.aiSuggesting') : t('editor.aiSuggest')}
          </button>
        )}

        <div className="relative">
          <button
            type="button"
            onClick={() => setTemplatesOpen((o) => !o)}
            disabled={busy !== null}
            className={TOOL_BTN}
            aria-expanded={templatesOpen}
          >
            <LayoutTemplate className="h-3.5 w-3.5" /> {t('editor.templates')}
            <ChevronDown className="h-3 w-3" />
          </button>
          {templatesOpen && (
            <div className="absolute left-0 z-20 mt-1 w-72 rounded-xl border border-ink-200 bg-white p-1 shadow-card-hover dark:border-white/10 dark:bg-ink-900">
              {templates.isLoading ? (
                <p className="flex items-center gap-2 px-3 py-2 text-xs text-ink-500">
                  <Loader2 className="h-3.5 w-3.5 animate-spin" /> {t('editor.templatesLoading')}
                </p>
              ) : (templates.data ?? []).length === 0 ? (
                <p className="px-3 py-2 text-xs text-ink-500 dark:text-ink-400">{t(tk('noTemplates'))}</p>
              ) : (
                (templates.data ?? []).map((tpl) => (
                  <button
                    key={tpl.id}
                    type="button"
                    onClick={() => {
                      setTemplatesOpen(false)
                      offer({ criteria: tpl.criteria, warnings: [], policy: tpl.policy ?? null })
                    }}
                    className="block w-full truncate rounded-lg px-3 py-2 text-left text-sm text-ink-700 hover:bg-ink-50 dark:text-ink-200 dark:hover:bg-white/10"
                  >
                    {t('editor.templateItem', { name: tpl.name, count: tpl.criteria.length })}
                  </button>
                ))
              )}
            </div>
          )}
        </div>

        <input
          ref={fileRef}
          type="file"
          accept=".xlsx"
          className="hidden"
          onChange={(e) => importFile(e.target.files?.[0])}
        />
        <button type="button" onClick={() => fileRef.current?.click()} disabled={busy !== null} className={TOOL_BTN}>
          {busy === 'import' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Upload className="h-3.5 w-3.5" />}
          {t('editor.importExcel')}
        </button>
        <button
          type="button"
          onClick={() => void run('export', () => cvRubricService.downloadDraft(value, api.exportName, policy, mode))}
          disabled={busy !== null || value.length === 0}
          className={TOOL_BTN}
        >
          {busy === 'export' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <FileSpreadsheet className="h-3.5 w-3.5" />}
          {t('editor.exportExcel')}
        </button>
        <button
          type="button"
          onClick={() => void run('blank', () => playbookService.downloadRubricTemplate(api.blankType))}
          disabled={busy !== null}
          className="inline-flex items-center gap-1 px-1 text-xs font-medium text-brand-600 hover:underline disabled:opacity-50 dark:text-brand-400"
        >
          <Download className="h-3.5 w-3.5" /> {t('editor.downloadBlank')}
        </button>
      </div>

      {error && (
        <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
          <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {error}
        </div>
      )}

      {pending && (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-ai-200 bg-ai-50 px-3 py-2 text-sm text-ai-800 dark:border-ai-500/30 dark:bg-ai-500/10 dark:text-ai-200">
          <span>{t('editor.replaceConfirm')}</span>
          <span className="flex gap-2">
            <button
              type="button"
              onClick={() => setPending(null)}
              className="rounded-lg px-2.5 py-1 text-xs font-semibold text-ink-600 hover:bg-white/70 dark:text-ink-300 dark:hover:bg-white/10"
            >
              {t('panel.cancel')}
            </button>
            <button
              type="button"
              onClick={() => apply(pending)}
              className="rounded-lg bg-ai-600 px-2.5 py-1 text-xs font-semibold text-white hover:bg-ai-700"
            >
              {t('editor.replace')}
            </button>
          </span>
        </div>
      )}

      {warnings.length > 0 && (
        <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300">
          <p className="font-semibold">{t('editor.warnings')}</p>
          <ul className="mt-1 list-disc space-y-0.5 pl-4">
            {warnings.map((w) => (
              <li key={w}>{w}</li>
            ))}
          </ul>
        </div>
      )}

      {value.length === 0 ? (
        <p className="rounded-xl border border-dashed border-ink-200 px-3 py-5 text-center text-xs text-ink-500 dark:border-white/10 dark:text-ink-400">
          {t(tk('empty'))}
        </p>
      ) : (
        <ol className="space-y-2.5">
          {value.map((c, i) => {
            const uid = uids[i] ?? `idx${i}`
            const open = openLevels[uid] ?? levelCount(c) > 0
            const knockout = !forInterview && isKnockout(c)
            return (
              <li
                key={uid}
                className="rounded-2xl border border-ink-200 bg-white p-3 dark:border-white/10 dark:bg-white/5"
              >
                <div className="flex items-start gap-2">
                  <span className="mt-2 grid h-6 w-6 shrink-0 place-items-center rounded-lg bg-ai-100 text-[11px] font-bold text-ai-700 dark:bg-ai-500/20 dark:text-ai-300">
                    {i + 1}
                  </span>
                  <div className="flex min-w-0 flex-1 flex-wrap gap-2">
                    <input
                      value={c.name}
                      onChange={(e) => patch(i, { name: e.target.value })}
                      placeholder={t(tk('namePlaceholder'))}
                      aria-label={t('editor.name')}
                      maxLength={120}
                      className={`${INPUT} min-w-[12rem] flex-1`}
                    />
                    {knockout ? (
                      // Điều kiện bắt buộc không mang trọng số — không vào trung bình (ADR-075).
                      <span className="inline-flex shrink-0 items-center gap-1 rounded-xl bg-amber-100 px-3 py-2 text-xs font-semibold text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
                        <ShieldCheck className="h-3.5 w-3.5" /> {t('editor.kind.badge')}
                      </span>
                    ) : (
                      <div className="relative w-28 shrink-0">
                        <input
                          type="number"
                          min={0}
                          max={100}
                          step={1}
                          value={Number.isFinite(c.weight) ? c.weight : 0}
                          onChange={(e) => patch(i, { weight: Number(e.target.value) })}
                          aria-label={t('editor.weight')}
                          className={`${INPUT} pr-7 text-right`}
                        />
                        <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-xs text-ink-400">%</span>
                      </div>
                    )}
                  </div>
                  <div className="flex shrink-0 items-center">
                    <IconButton label={t('editor.moveUp')} onClick={() => move(i, -1)} disabled={i === 0}>
                      <ChevronUp className="h-4 w-4" />
                    </IconButton>
                    <IconButton label={t('editor.moveDown')} onClick={() => move(i, 1)} disabled={i === value.length - 1}>
                      <ChevronDown className="h-4 w-4" />
                    </IconButton>
                    <IconButton label={t('editor.remove')} onClick={() => remove(i)} danger>
                      <Trash2 className="h-4 w-4" />
                    </IconButton>
                  </div>
                </div>

                <div className="mt-2 pl-8">
                  {/* Công thức cấp tiêu chí (ADR-075): điều kiện bắt buộc · điểm tối thiểu. Chỉ bộ chấm CV. */}
                  {!forInterview && (
                    <div className="mb-2 flex flex-wrap items-center gap-x-4 gap-y-1.5">
                      <label
                        className="inline-flex cursor-pointer items-center gap-1.5 text-xs text-ink-700 dark:text-ink-200"
                        title={t('editor.kind.knockoutHint')}
                      >
                        <input
                          type="checkbox"
                          checked={knockout}
                          disabled={!knockout && knockoutCount >= CV_RUBRIC_MAX_KNOCKOUTS}
                          onChange={(e) => patch(i, { kind: e.target.checked ? 'knockout' : null })}
                          className="h-3.5 w-3.5 rounded border-ink-300 text-amber-600 focus:ring-amber-500 disabled:opacity-40"
                        />
                        <ShieldCheck className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
                        {t('editor.kind.knockout')}
                      </label>
                      {!knockout && (
                        <label
                          className="inline-flex items-center gap-1.5 text-xs text-ink-600 dark:text-ink-300"
                          title={t('editor.minScoreHint')}
                        >
                          {t('editor.minScore')}
                          <input
                            type="number"
                            min={1}
                            max={100}
                            step={1}
                            value={c.minScore ?? ''}
                            placeholder="—"
                            onChange={(e) =>
                              patch(i, { minScore: e.target.value === '' ? null : Math.trunc(Number(e.target.value)) })
                            }
                            aria-label={t('editor.minScore')}
                            className={`${INPUT} w-20 py-1 text-right text-xs`}
                          />
                        </label>
                      )}
                    </div>
                  )}
                  {knockout && (
                    <p className="mb-2 rounded-lg bg-amber-50 px-2.5 py-1.5 text-[11px] leading-relaxed text-amber-800 dark:bg-amber-500/10 dark:text-amber-300">
                      {t('editor.kind.knockoutHint')}
                    </p>
                  )}
                  <textarea
                    rows={2}
                    value={c.description ?? ''}
                    onChange={(e) => patch(i, { description: e.target.value })}
                    placeholder={knockout ? t('editor.kind.notePlaceholder') : t(tk('guidePlaceholder'))}
                    aria-label={t('editor.guide')}
                    maxLength={1000}
                    className={`${INPUT} resize-y`}
                  />

                  {/* Ý kiểm: quyết định vị trí điểm TRONG dải — AI chỉ trả lời có/không từng ý. Chỉ có ở bộ chấm CV. */}
                  {!forInterview && !knockout && (
                  <div className="mt-2 rounded-xl border border-dashed border-ink-200 p-2.5 dark:border-white/10">
                    <p className="flex items-center gap-1.5 text-[11px] font-semibold text-ink-700 dark:text-ink-200">
                      <ListChecks className="h-3.5 w-3.5 text-ai-600 dark:text-ai-400" />
                      {t('editor.checks.title', { count: checkCount(c) })}
                    </p>
                    <p className="mt-0.5 text-[11px] leading-relaxed text-ink-500 dark:text-ink-400">
                      {checkCount(c) > 0 ? t('editor.checks.hint') : t('editor.checks.emptyHint')}
                    </p>
                    {(c.checks ?? []).map((chk, j) => (
                      <div key={j} className="mt-1.5 flex items-center gap-1.5">
                        <SquareCheck className="h-3.5 w-3.5 shrink-0 text-ink-400" />
                        <input
                          value={chk.text}
                          onChange={(e) =>
                            patchChecks(i, (checks) => checks.map((x, k) => (k === j ? { ...x, text: e.target.value } : x)))
                          }
                          placeholder={t('editor.checks.placeholder')}
                          aria-label={t('editor.checks.item', { n: j + 1 })}
                          maxLength={200}
                          className={`${INPUT} py-1.5 text-xs`}
                        />
                        {/* Trọng số ý (ADR-075): ý quan trọng hơn kéo điểm trong dải nhiều hơn. */}
                        <div
                          role="group"
                          aria-label={t('editor.checks.weight')}
                          className="flex shrink-0 overflow-hidden rounded-lg border border-ink-200 dark:border-white/10"
                        >
                          {CHECK_WEIGHTS.map((w) => {
                            const active = (chk.weight ?? 1) === w
                            return (
                              <button
                                key={w}
                                type="button"
                                aria-pressed={active}
                                title={t('editor.checks.weightTitle', { n: w })}
                                onClick={() =>
                                  patchChecks(i, (checks) =>
                                    checks.map((x, k) => (k === j ? { ...x, weight: w === 1 ? null : w } : x))
                                  )
                                }
                                className={`px-1.5 py-1 text-[11px] font-semibold ${
                                  active
                                    ? 'bg-ai-600 text-white'
                                    : 'text-ink-500 hover:bg-ink-50 dark:text-ink-400 dark:hover:bg-white/10'
                                }`}
                              >
                                ×{w}
                              </button>
                            )
                          })}
                        </div>
                        <IconButton
                          label={t('editor.checks.remove')}
                          onClick={() => patchChecks(i, (checks) => checks.filter((_, k) => k !== j))}
                          danger
                        >
                          <X className="h-3.5 w-3.5" />
                        </IconButton>
                      </div>
                    ))}
                    <button
                      type="button"
                      onClick={() => patchChecks(i, (checks) => [...checks, { key: null, text: '' }])}
                      disabled={(c.checks ?? []).length >= CV_RUBRIC_MAX_CHECKS}
                      className="mt-1.5 inline-flex items-center gap-1 text-xs font-medium text-brand-600 hover:underline disabled:opacity-40 dark:text-brand-400"
                    >
                      <Plus className="h-3.5 w-3.5" /> {t('editor.checks.add')}
                    </button>
                  </div>
                  )}

                  {!knockout && (
                  <button
                    type="button"
                    onClick={() => setOpenLevels((s) => ({ ...s, [uid]: !open }))}
                    className="mt-1.5 inline-flex items-center gap-1 text-xs font-medium text-ai-700 hover:underline dark:text-ai-300"
                  >
                    <Scale className="h-3.5 w-3.5" />
                    {open ? t('editor.hideLevels') : t('editor.showLevels', { count: levelCount(c) })}
                  </button>
                  )}
                  {open && !knockout && (
                    <div className="mt-2 space-y-2">
                      <p className="text-[11px] text-ink-500 dark:text-ink-400">{t(tk('levelsHint'))}</p>
                      <div className="grid gap-2 sm:grid-cols-2">
                        {CV_RUBRIC_LEVEL_KEYS.map((level) => (
                          <label key={level} className="block">
                            <span className="mb-1 block text-[11px] font-semibold text-ink-600 dark:text-ink-300">
                              {t(`editor.level.${level}`, ranges[level])}
                            </span>
                            <textarea
                              rows={2}
                              value={c.levels?.[level] ?? ''}
                              onChange={(e) => patchLevel(i, level, e.target.value)}
                              placeholder={t(tk(`levelPlaceholder.${level}`))}
                              maxLength={1000}
                              className={`${INPUT} resize-y text-xs`}
                            />
                          </label>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              </li>
            )
          })}
        </ol>
      )}

      <div className="flex flex-wrap items-center justify-between gap-2">
        <button
          type="button"
          onClick={add}
          disabled={value.length >= 20}
          className="inline-flex items-center gap-1.5 rounded-xl border border-dashed border-ink-300 px-3 py-2 text-sm font-medium text-ink-600 hover:border-brand-400 hover:text-brand-600 disabled:opacity-50 dark:border-white/20 dark:text-ink-300"
        >
          <Plus className="h-4 w-4" /> {t('editor.addCriterion')}
        </button>
        {value.length > 0 && (
          <div className="flex items-center gap-3">
            <button type="button" onClick={balance} className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400">
              {t('editor.balance')}
            </button>
            <span className="text-xs text-ink-500 dark:text-ink-400">{t('editor.total')}</span>
            <span
              className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${
                totalOk
                  ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                  : 'bg-red-100 text-red-600 dark:bg-red-500/20 dark:text-red-400'
              }`}
            >
              {total}%
            </span>
          </div>
        )}
      </div>

      {value.length > 0 && (
        <div className="h-1.5 overflow-hidden rounded-full bg-ink-100 dark:bg-white/10">
          <div
            className={`h-full rounded-full transition-all ${totalOk ? 'bg-emerald-500' : total > 100 ? 'bg-red-500' : 'bg-amber-500'}`}
            style={{ width: `${Math.min(100, Math.max(0, total))}%` }}
          />
        </div>
      )}

      {(touched || showProblems) && problems.length > 0 && (
        <ul className="space-y-0.5 text-xs text-red-600 dark:text-red-400">
          {problems.map((p) => (
            <li key={p} className="flex items-center gap-1.5">
              <AlertCircle className="h-3.5 w-3.5 shrink-0" />
              {t(`editor.problems.${p}`, { total, max: CV_RUBRIC_MAX_KNOCKOUTS })}
            </li>
          ))}
        </ul>
      )}

      {withFormula && (
        <CvScoringFormulaEditor
          policy={policy!}
          onChange={onPolicyChange!}
          criteria={value}
          showProblems={touched || showProblems}
        />
      )}
    </div>
  )
}

function IconButton({
  label,
  onClick,
  disabled,
  danger,
  children,
}: {
  label: string
  onClick: () => void
  disabled?: boolean
  danger?: boolean
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      title={label}
      className={`grid h-8 w-8 place-items-center rounded-lg text-ink-400 disabled:opacity-30 ${
        danger
          ? 'hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-500/10'
          : 'hover:bg-ink-100 hover:text-ink-700 dark:hover:bg-white/10 dark:hover:text-white'
      }`}
    >
      {children}
    </button>
  )
}

/** Hiển thị chỉ đọc — màn tạo tin của Recruiter, màn duyệt phiếu của HR Leader, panel khi không có quyền sửa. */
export function ReadOnlyRubric({
  criteria,
  policy,
}: {
  criteria: CvRubricCriterion[]
  /**
   * Công thức của bộ CV (ADR-075) — truyền vào thì hiện tóm tắt công thức + nhãn dải theo ngưỡng của tin. Bộ phỏng vấn
   * không truyền (ngưỡng mặc định, không có khối công thức).
   */
  policy?: CvScoringPolicy | null
}) {
  const { t } = useTranslation(CV_SCORING_NS)
  if (criteria.length === 0)
    return <p className="text-xs text-ink-500 dark:text-ink-400">{t('editor.readOnlyEmpty')}</p>

  const ranges = bandRanges(policy && cvPolicyProblems(policy).length === 0 ? policy : undefined)

  return (
    <div className="space-y-2">
    <ol className="space-y-2">
      {criteria.map((c, i) => (
        <li
          key={c.key ?? i}
          className="rounded-xl border border-ink-100 px-3 py-2 dark:border-white/10"
        >
          <div className="flex items-center justify-between gap-2">
            <span className="text-sm font-medium text-ink-900 dark:text-white">
              {i + 1}. {c.name}
            </span>
            <span className="flex shrink-0 items-center gap-1">
              {!isKnockout(c) && c.minScore != null && (
                <span
                  className="rounded-full bg-red-50 px-2 py-0.5 text-[11px] font-semibold text-red-700 dark:bg-red-500/10 dark:text-red-400"
                  title={t('editor.minScoreHint')}
                >
                  {t('editor.minScoreChip', { min: c.minScore })}
                </span>
              )}
              {isKnockout(c) ? (
                <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
                  <ShieldCheck className="h-3 w-3" /> {t('editor.kind.badge')}
                </span>
              ) : (
                <span className="rounded-full bg-ai-100 px-2 py-0.5 text-xs font-semibold text-ai-700 dark:bg-ai-500/20 dark:text-ai-300">
                  {c.weight}%
                </span>
              )}
            </span>
          </div>
          {c.description && (
            <p className="mt-1 text-xs leading-relaxed text-ink-600 dark:text-ink-400">{c.description}</p>
          )}
          {!isKnockout(c) && levelCount(c) > 0 && (
            <ul className="mt-1.5 space-y-0.5 text-[11px] leading-relaxed">
              {CV_RUBRIC_LEVEL_KEYS.filter((k) => !!c.levels?.[k]?.trim()).map((k) => (
                <li key={k} className="text-ink-600 dark:text-ink-300">
                  <span className="font-semibold text-ink-500 dark:text-ink-400">{t(`editor.level.${k}`, ranges[k])}:</span>{' '}
                  {c.levels?.[k]}
                </li>
              ))}
            </ul>
          )}
          {!isKnockout(c) && checkCount(c) > 0 && (
            <div className="mt-1.5">
              <p className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">{t('editor.checks.readOnlyTitle')}</p>
              <ul className="mt-0.5 space-y-0.5 text-[11px] leading-relaxed text-ink-600 dark:text-ink-300">
                {(c.checks ?? [])
                  .filter((x) => !!x.text?.trim())
                  .map((x, j) => (
                    <li key={x.key ?? j} className="flex items-start gap-1.5">
                      <SquareCheck className="mt-0.5 h-3 w-3 shrink-0 text-ink-400" />
                      <span>{x.text}</span>
                      {(x.weight ?? 1) !== 1 && (
                        <span className="shrink-0 rounded bg-ai-100 px-1 text-[10px] font-semibold text-ai-700 dark:bg-ai-500/20 dark:text-ai-300">
                          ×{x.weight}
                        </span>
                      )}
                    </li>
                  ))}
              </ul>
            </div>
          )}
        </li>
      ))}
    </ol>
    {policy !== undefined && <FormulaSummary policy={policy} criteria={criteria} />}
    </div>
  )
}
