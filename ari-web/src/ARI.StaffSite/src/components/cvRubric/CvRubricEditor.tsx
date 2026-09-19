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
  Sparkles,
  SquareCheck,
  Trash2,
  Upload,
  X,
} from 'lucide-react'
import {
  CV_RUBRIC_LEVEL_KEYS,
  CV_RUBRIC_MAX_CHECKS,
  checkCount,
  cvRubricProblems,
  cvRubricService,
  totalWeight,
} from '@ari/shared/fservices/cvRubric'
import type {
  CvRubricCheck,
  CvRubricCriterion,
  CvRubricDraft,
  CvRubricLevelKey,
  CvRubricSuggestionInput,
} from '@ari/shared/fservices/cvRubric'
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
}

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
    commit([...value, { name: '', weight: 0, description: '', levels: null, checks: [] }], [...uids, nextUid()])

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

  /** Chia đều 100 theo phần dư lớn nhất — cùng cách server làm với bản AI gợi ý. */
  const balance = () => {
    const n = value.length
    if (n === 0) return
    const base = Math.floor(100 / n)
    let rest = 100 - base * n
    commit(value.map((c) => ({ ...c, weight: base + (rest-- > 0 ? 1 : 0) })), uids)
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
    // Bộ tiêu chí phỏng vấn không có ý kiểm: file Excel / mẫu CV có ý kiểm thì bỏ đi ngay ở đây.
    const criteria = draft.criteria.map((c) => ({ ...c, key: null, checks: forInterview ? null : c.checks }))
    commit(criteria, criteria.map(nextUid))
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
    void run('import', async () => offer(await cvRubricService.parseSheet(file)))
  }

  const problems = cvRubricProblems(value)
  const total = totalWeight(value)
  const totalOk = Math.abs(total - 100) <= 0.01

  if (readOnly) return <ReadOnlyRubric criteria={value} />

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
                      offer({ criteria: tpl.criteria, warnings: [] })
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
          onClick={() => void run('export', () => cvRubricService.downloadDraft(value, api.exportName))}
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
                  <textarea
                    rows={2}
                    value={c.description ?? ''}
                    onChange={(e) => patch(i, { description: e.target.value })}
                    placeholder={t(tk('guidePlaceholder'))}
                    aria-label={t('editor.guide')}
                    maxLength={1000}
                    className={`${INPUT} resize-y`}
                  />

                  {/* Ý kiểm: quyết định vị trí điểm TRONG dải — AI chỉ trả lời có/không từng ý. Chỉ có ở bộ chấm CV. */}
                  {!forInterview && (
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

                  <button
                    type="button"
                    onClick={() => setOpenLevels((s) => ({ ...s, [uid]: !open }))}
                    className="mt-1.5 inline-flex items-center gap-1 text-xs font-medium text-ai-700 hover:underline dark:text-ai-300"
                  >
                    <Scale className="h-3.5 w-3.5" />
                    {open ? t('editor.hideLevels') : t('editor.showLevels', { count: levelCount(c) })}
                  </button>
                  {open && (
                    <div className="mt-2 space-y-2">
                      <p className="text-[11px] text-ink-500 dark:text-ink-400">{t(tk('levelsHint'))}</p>
                      <div className="grid gap-2 sm:grid-cols-2">
                        {CV_RUBRIC_LEVEL_KEYS.map((level) => (
                          <label key={level} className="block">
                            <span className="mb-1 block text-[11px] font-semibold text-ink-600 dark:text-ink-300">
                              {t(`editor.level.${level}`)}
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
              {t(`editor.problems.${p}`, { total })}
            </li>
          ))}
        </ul>
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
export function ReadOnlyRubric({ criteria }: { criteria: CvRubricCriterion[] }) {
  const { t } = useTranslation(CV_SCORING_NS)
  if (criteria.length === 0)
    return <p className="text-xs text-ink-500 dark:text-ink-400">{t('editor.readOnlyEmpty')}</p>

  return (
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
            <span className="shrink-0 rounded-full bg-ai-100 px-2 py-0.5 text-xs font-semibold text-ai-700 dark:bg-ai-500/20 dark:text-ai-300">
              {c.weight}%
            </span>
          </div>
          {c.description && (
            <p className="mt-1 text-xs leading-relaxed text-ink-600 dark:text-ink-400">{c.description}</p>
          )}
          {levelCount(c) > 0 && (
            <ul className="mt-1.5 space-y-0.5 text-[11px] leading-relaxed">
              {CV_RUBRIC_LEVEL_KEYS.filter((k) => !!c.levels?.[k]?.trim()).map((k) => (
                <li key={k} className="text-ink-600 dark:text-ink-300">
                  <span className="font-semibold text-ink-500 dark:text-ink-400">{t(`editor.level.${k}`)}:</span>{' '}
                  {c.levels?.[k]}
                </li>
              ))}
            </ul>
          )}
          {checkCount(c) > 0 && (
            <div className="mt-1.5">
              <p className="text-[11px] font-semibold text-ink-500 dark:text-ink-400">{t('editor.checks.readOnlyTitle')}</p>
              <ul className="mt-0.5 space-y-0.5 text-[11px] leading-relaxed text-ink-600 dark:text-ink-300">
                {(c.checks ?? [])
                  .filter((x) => !!x.text?.trim())
                  .map((x, j) => (
                    <li key={x.key ?? j} className="flex items-start gap-1.5">
                      <SquareCheck className="mt-0.5 h-3 w-3 shrink-0 text-ink-400" />
                      <span>{x.text}</span>
                    </li>
                  ))}
              </ul>
            </div>
          )}
        </li>
      ))}
    </ol>
  )
}
