import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AlertTriangle,
  Calculator,
  ChevronDown,
  ChevronUp,
  CircleAlert,
  CircleCheck,
  CircleX,
  FileX,
  ListChecks,
  Minus,
  Hourglass,
  Quote,
  RefreshCw,
  Scale,
  ShieldCheck,
  Sparkles,
} from 'lucide-react'
import { formatDateTime24 } from '@ari/shared/utils/time24'
import type {
  CvScoreBand,
  CvScoreBreakdown as Breakdown,
  CvScoreCriterion,
  CvScoreGate,
  CvScoreState,
} from '@ari/shared/types/application'
import { tierRanges } from '@ari/shared/fservices/cvRubric'
import type { CvRecommendationKey } from '@ari/shared/fservices/cvRubric'
import { cvRetryTime } from './useCvScoreText'
import { aiPosition, bandPosition } from './bandPosition'
import { cvScoreTextClass } from './cvTier'

const NS = 'modules/staff/cvScoring'

const CARD =
  'rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5 sm:p-6'

const BAND_STYLE: Record<CvScoreBand, { pill: string; bar: string }> = {
  excellent: {
    pill: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
    bar: 'bg-emerald-500',
  },
  good: { pill: 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-400', bar: 'bg-brand-500' },
  fair: { pill: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400', bar: 'bg-amber-500' },
  poor: { pill: 'bg-red-100 text-red-600 dark:bg-red-500/20 dark:text-red-400', bar: 'bg-red-500' },
}

const STATE_ICON: Partial<Record<CvScoreState, typeof Hourglass>> = {
  queued: Hourglass,
  rescoring: RefreshCw,
  scoring_failed: CircleAlert,
  pending_rubric: AlertTriangle,
  invalid_cv: FileX,
  no_cv: FileX,
}

/** Lượt chấm hỏng: vì sao, đã hỏng mấy lần, và khi nào hệ thống tự thử lại — người xem không phải làm gì. */
function FailedScoringText({ score }: { score: Breakdown }) {
  const { t } = useTranslation(NS)
  const retry = cvRetryTime(score.retryAt)
  const reason = t(`breakdown.failureReason.${score.failureReason ?? 'unknown'}`, {
    defaultValue: t('breakdown.failureReason.unknown'),
  })
  return (
    <>
      {t('breakdown.state.scoring_failed', { reason })}{' '}
      {retry === null || retry === 'due'
        ? t('breakdown.retrySoon')
        : t('breakdown.retryAt', { time: retry })}
      {(score.failedAttempts ?? 0) > 1 && ` ${t('breakdown.failedAttempts', { count: score.failedAttempts ?? 0 })}`}
      {score.total != null && ` ${t('breakdown.previousScoreShown')}`}
    </>
  )
}

/**
 * Các cổng của công thức (ADR-075): điều kiện bắt buộc (đạt / không đạt kèm trích dẫn) và điểm tối thiểu của tiêu chí.
 * Trượt cổng chỉ ép nhãn khuyến nghị — điểm vẫn tính, hồ sơ không bị loại tự động; "chưa xác minh" là việc cần người kiểm.
 */
function GatesBlock({
  gates,
  status,
  num,
}: {
  gates: CvScoreGate[]
  status?: string | null
  num: (n?: number | null) => string
}) {
  const { t } = useTranslation(NS)
  const tone =
    status === 'fail'
      ? 'border-red-200 bg-red-50/70 dark:border-red-500/30 dark:bg-red-500/10'
      : status === 'review'
        ? 'border-amber-200 bg-amber-50/70 dark:border-amber-500/30 dark:bg-amber-500/10'
        : 'border-emerald-200 bg-emerald-50/60 dark:border-emerald-500/30 dark:bg-emerald-500/10'

  return (
    <div className={`mt-4 rounded-xl border px-4 py-3 ${tone}`}>
      <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-ink-700 dark:text-ink-200">
        <ShieldCheck className="h-3.5 w-3.5" /> {t('breakdown.gates.title')}
      </p>
      <p className="mt-0.5 text-xs text-ink-600 dark:text-ink-300">{t(`breakdown.gates.status.${status ?? 'pass'}`)}</p>
      <ul className="mt-2 space-y-1.5">
        {gates.map((g) => (
          <li key={`${g.type}-${g.key}`} className="flex items-start gap-1.5 text-sm">
            {g.outcome === 'pass' ? (
              <CircleCheck className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
            ) : g.outcome === 'fail' ? (
              <CircleX className="mt-0.5 h-4 w-4 shrink-0 text-red-500 dark:text-red-400" />
            ) : (
              <CircleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" />
            )}
            <span className="min-w-0">
              <span className="text-ink-900 dark:text-white">
                {g.type === 'knockout'
                  ? g.label
                  : t('breakdown.gates.minScore', { label: g.label, score: num(g.score), min: g.minScore })}
              </span>
              {g.type === 'knockout' && g.outcome === 'pass' && g.evidence && (
                <span className="mt-0.5 block text-xs italic text-ink-500 dark:text-ink-400">“{g.evidence}”</span>
              )}
              {g.reason && (
                <span
                  className={`mt-0.5 block text-xs ${g.outcome === 'fail' ? 'text-red-700 dark:text-red-400' : 'text-amber-700 dark:text-amber-400'}`}
                >
                  {t(`breakdown.gates.reason.${g.reason}`, { defaultValue: g.reason })}
                </span>
              )}
              {g.type === 'knockout' && g.reasoning && g.outcome !== 'pass' && (
                <span className="mt-0.5 block text-xs text-ink-500 dark:text-ink-400">{g.reasoning}</span>
              )}
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** Tách tóm tắt "🌟 Điểm sáng: … \n ⚠️ Điểm cần lưu ý: …" thành hai đoạn. */
const splitSummary = (summary?: string | null) =>
  (summary ?? '')
    .split(/\n+/)
    .map((s) => s.trim())
    .filter(Boolean)

/**
 * Điểm CV KÈM CÁCH TÍNH (ADR-070) — cho Hiring Manager, Recruiter, HR Leader.
 *
 * Không chỉ một con số: phép tính bằng số thật, từng tiêu chí với trọng số · điểm · số điểm góp vào tổng, và
 * bấm mở ra căn cứ — trích dẫn từ CV, lý do chấm, chuẩn chấm và dải điểm doanh nghiệp đã khai. Tiêu chí AI
 * không chấm được hiện riêng để người đọc hiểu vì sao mẫu số nhỏ hơn 100.
 */
export default function CvScoreBreakdown({ score }: { score?: Breakdown | null }) {
  const { t, i18n } = useTranslation(NS)
  const [open, setOpen] = useState<Record<string, boolean>>({})

  if (!score) return null

  const locale = i18n.language?.startsWith('vi') ? 'vi-VN' : 'en-US'
  const num = (n?: number | null) =>
    n == null ? '—' : n.toLocaleString(locale, { maximumFractionDigits: 2 })

  const hasCriteria = score.criteria.length > 0
  const showNumbers = score.total != null && hasCriteria
  const StateIcon = STATE_ICON[score.state]

  const numerator = score.criteria.map((c) => `${num(c.score)}×${num(c.weight)}`).join(' + ')
  const denominator = score.criteria.map((c) => num(c.weight)).join(' + ')

  // Nhãn khuyến nghị kèm khoảng điểm theo CÔNG THỨC CỦA TIN (ADR-075), không phải ngưỡng viết cứng.
  const tiers = tierRanges(score.policy ?? undefined)
  const recLabel = (rec?: string | null) => {
    if (!rec) return null
    const range = tiers[rec as CvRecommendationKey]
    const name = t(`breakdown.recName.${rec}`, { defaultValue: rec })
    return range ? t('breakdown.recRange', { name, from: range.from, to: range.to }) : name
  }
  const gates = score.gates ?? []
  const forcedReject = score.gateStatus === 'fail'

  return (
    <section className={CARD} data-cv-score-state={score.state} data-cv-score={score.total ?? ''}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
            <Scale className="h-5 w-5 text-ai-600 dark:text-ai-400" /> {t('breakdown.title')}
          </h2>
          <p className="mt-0.5 text-xs text-ink-500 dark:text-ink-400">{t('breakdown.subtitle')}</p>
        </div>
        {score.total != null && (
          <div className="text-right">
            <span className={`font-display text-4xl font-extrabold leading-none ${cvScoreTextClass(score.recommendation)}`}>
              {score.total}
            </span>
            <span className="ml-1 text-sm text-ink-400">{t('breakdown.outOf')}</span>
            {score.recommendation && (
              <p className="mt-1 text-xs font-medium text-ink-600 dark:text-ink-300" title={t('breakdown.recommendation')}>
                {forcedReject ? t('breakdown.recForced') : recLabel(score.recommendation)}
              </p>
            )}
            {forcedReject && score.scoreRecommendation && (
              <p className="text-[11px] text-ink-400">{t('breakdown.scoreTier', { tier: recLabel(score.scoreRecommendation) })}</p>
            )}
          </div>
        )}
      </div>

      {score.state !== 'scored' && StateIcon && (
        <div
          className={`mt-4 flex items-start gap-2 rounded-xl border px-3 py-2 text-sm ${
            score.state === 'invalid_cv' || score.state === 'scoring_failed'
              ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400'
              : score.state === 'pending_rubric'
                ? 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300'
                : 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-500/30 dark:bg-blue-500/10 dark:text-blue-300'
          }`}
        >
          <StateIcon
            className={`mt-0.5 h-4 w-4 shrink-0 ${score.state === 'queued' || score.state === 'rescoring' ? 'animate-pulse' : ''}`}
          />
          <span>
            {score.state === 'scoring_failed' ? (
              <FailedScoringText score={score} />
            ) : (
              <>
                {t(`breakdown.state.${score.state}`)}
                {score.state === 'invalid_cv' && score.invalidReason ? ` ${score.invalidReason}` : ''}
              </>
            )}
          </span>
        </div>
      )}

      {showNumbers && (
        <>
          {gates.length > 0 && <GatesBlock gates={gates} status={score.gateStatus} num={num} />}

          {/* Phép tính bằng số thật — người đọc tự cộng lại được. */}
          <div className="mt-4 rounded-xl border border-ai-200 bg-ai-50/60 px-4 py-3 dark:border-ai-500/20 dark:bg-ai-500/10">
            <p className="mb-1 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-ai-700 dark:text-ai-300">
              <Calculator className="h-3.5 w-3.5" /> {t('breakdown.formula')}
            </p>
            <p className="break-words font-mono text-sm leading-relaxed text-ink-800 dark:text-ink-100">
              ({numerator}) ÷ ({denominator}) = {num(score.weightedSum)} ÷ {num(score.totalWeight)} ={' '}
              {num(score.exactTotal)}{' '}
              <span className="text-ink-500 dark:text-ink-400">
                → {t('breakdown.rounded')} <strong className="text-ink-900 dark:text-white">{score.total}</strong>
              </span>
            </p>
          </div>

          <div className="mt-4 hidden grid-cols-[1fr_4.5rem_9rem_5.5rem] gap-3 px-1 text-[11px] font-semibold uppercase tracking-wide text-ink-400 sm:grid">
            <span>{t('breakdown.criterion')}</span>
            <span className="text-right">{t('breakdown.weight')}</span>
            <span>{t('breakdown.score')}</span>
            <span className="text-right">{t('breakdown.contribution')}</span>
          </div>

          <ul className="mt-1 divide-y divide-ink-100 dark:divide-white/10">
            {score.criteria.map((c) => (
              <CriterionRow
                key={c.key}
                c={c}
                num={num}
                open={!!open[c.key]}
                onToggle={() => setOpen((s) => ({ ...s, [c.key]: !s[c.key] }))}
              />
            ))}
          </ul>

          {score.excluded.length > 0 && (
            <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 dark:border-amber-500/30 dark:bg-amber-500/10">
              <p className="flex items-center gap-1.5 text-xs font-semibold text-amber-800 dark:text-amber-300">
                <CircleAlert className="h-3.5 w-3.5" />
                {t('breakdown.excludedTitle', { count: score.excluded.length })}
              </p>
              <p className="mt-0.5 text-xs text-amber-700 dark:text-amber-400">{t('breakdown.excludedHint')}</p>
              <ul className="mt-1.5 flex flex-wrap gap-1.5">
                {score.excluded.map((c) => (
                  <li
                    key={c.key}
                    className="rounded-full bg-white px-2 py-0.5 text-xs text-amber-800 dark:bg-white/10 dark:text-amber-200"
                  >
                    {c.label} · {num(c.weight)}%
                  </li>
                ))}
              </ul>
            </div>
          )}

          <AiNotes score={score} />
        </>
      )}

      {(score.scoredAt || score.rubricSavedAt) && (
        <div className="mt-4 space-y-0.5 border-t border-ink-100 pt-3 text-xs text-ink-400 dark:border-white/10">
          {score.scoredAt &&
            (score.derived ? (
              // ADR-075: HM đổi công thức → điểm được tính lại từ câu trả lời cũ của AI, không có lời gọi AI mới.
              <p>
                {t('breakdown.derived', {
                  time: formatDateTime24(score.scoredAt),
                  observed: formatDateTime24(score.observedAt ?? score.scoredAt),
                  model: score.model ?? 'AI',
                })}
              </p>
            ) : (
              <p>{t('breakdown.meta', { time: formatDateTime24(score.scoredAt), model: score.model ?? 'AI' })}</p>
            ))}
          {score.rubricSavedAt && (
            <p>
              {t('breakdown.rubricMeta', {
                time: formatDateTime24(score.rubricSavedAt),
                name: score.rubricSavedBy ?? '—',
              })}
            </p>
          )}
          {showNumbers && !score.isCurrentRubric && (
            <p className="text-blue-600 dark:text-blue-400">{t('breakdown.oldRubric')}</p>
          )}
        </div>
      )}
    </section>
  )
}

/** Điểm trong dải ra từ đâu: phép tính từ ý kiểm (kèm từng ý đạt/không đạt + trích dẫn), hoặc lời nói rõ là AI ước lượng. */
function InBandPosition({ c, num, band }: { c: CvScoreCriterion; num: (n?: number | null) => string; band: CvScoreBand }) {
  const { t } = useTranslation(NS)
  const pos = bandPosition(c)
  const ai = aiPosition(c)
  const bandLabel = t(`breakdown.band.${band}`)
  const checks = c.checks ?? []

  return (
    <div className="rounded-lg border border-ai-200 bg-white px-3 py-2 dark:border-ai-500/20 dark:bg-white/5">
      <p className="mb-1 flex items-center gap-1 text-[11px] font-semibold uppercase tracking-wide text-ai-700 dark:text-ai-300">
        <ListChecks className="h-3 w-3" /> {t('breakdown.position.title')}
      </p>
      {pos ? (
        <p className="font-mono text-xs text-ink-800 dark:text-ink-100">
          {t(pos.weighted ? 'breakdown.position.checklistWeighted' : 'breakdown.position.checklist', {
            band: bandLabel,
            min: num(pos.min),
            max: num(pos.max),
            met: pos.met,
            answered: pos.answered,
            span: num(pos.span),
            exact: num(pos.exact),
            score: num(c.score),
          })}
        </p>
      ) : ai ? (
        <p className="font-mono text-xs text-ink-800 dark:text-ink-100">
          {t('breakdown.position.aiPosition', {
            band: bandLabel,
            min: num(ai.min),
            max: num(ai.max),
            position: num(ai.position),
            span: num(ai.span),
            exact: num(ai.exact),
            score: num(c.score),
          })}
        </p>
      ) : (
        <p className="text-xs text-ink-600 dark:text-ink-300">
          {c.bandMin != null && c.bandMax != null
            ? t('breakdown.position.ai', { band: bandLabel, min: num(c.bandMin), max: num(c.bandMax) })
            : t('breakdown.position.aiNoBand')}
        </p>
      )}
      {checks.length > 0 && (
        <ul className="mt-2 space-y-1">
          {checks.map((x) => (
            <li key={x.key} className="flex items-start gap-1.5 text-xs">
              {x.met === true ? (
                <CircleCheck className="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-600 dark:text-emerald-400" />
              ) : x.met === false ? (
                <CircleX className="mt-0.5 h-3.5 w-3.5 shrink-0 text-red-500 dark:text-red-400" />
              ) : (
                <Minus className="mt-0.5 h-3.5 w-3.5 shrink-0 text-amber-500" />
              )}
              <span className="min-w-0">
                <span className={x.met === true ? 'text-ink-900 dark:text-white' : 'text-ink-600 dark:text-ink-300'}>
                  {x.text}
                </span>
                {(x.weight ?? 1) !== 1 && (
                  <span className="ml-1 rounded bg-ai-100 px-1 text-[10px] font-semibold text-ai-700 dark:bg-ai-500/20 dark:text-ai-300">
                    ×{x.weight}
                  </span>
                )}
                {x.met === true && x.evidence && (
                  <span className="mt-0.5 block italic text-ink-500 dark:text-ink-400">“{x.evidence}”</span>
                )}
                {x.unsupported && (
                  <span className="mt-0.5 block text-amber-700 dark:text-amber-400">{t('breakdown.checks.unsupported')}</span>
                )}
                {x.met == null && (
                  <span className="mt-0.5 block text-amber-700 dark:text-amber-400">{t('breakdown.checks.unanswered')}</span>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function CriterionRow({
  c,
  num,
  open,
  onToggle,
}: {
  c: CvScoreCriterion
  num: (n?: number | null) => string
  open: boolean
  onToggle: () => void
}) {
  const { t } = useTranslation(NS)
  const band = c.band ?? 'fair'
  const style = BAND_STYLE[band]
  const pct = Math.max(0, Math.min(100, c.score ?? 0))
  const appliedLevel = c.levels?.[band]

  return (
    <li className="py-3">
      <button type="button" onClick={onToggle} className="w-full text-left" aria-expanded={open}>
        <div className="grid grid-cols-[1fr_auto] items-center gap-x-3 gap-y-1.5 sm:grid-cols-[1fr_4.5rem_9rem_5.5rem]">
          <span className="flex min-w-0 items-center gap-1.5 text-sm font-medium text-ink-900 dark:text-white">
            {open ? (
              <ChevronUp className="h-4 w-4 shrink-0 text-ink-400" />
            ) : (
              <ChevronDown className="h-4 w-4 shrink-0 text-ink-400" />
            )}
            <span className="truncate">{c.label}</span>
            <span className={`hidden shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-medium sm:inline ${style.pill}`}>
              {t(`breakdown.band.${band}`)}
            </span>
            {!!c.checksAnswered && (
              <span className="hidden shrink-0 rounded-full bg-ink-100 px-1.5 py-0.5 text-[10px] font-medium text-ink-600 dark:bg-white/10 dark:text-ink-300 sm:inline">
                {t('breakdown.checks.short', { met: c.checksMet ?? 0, total: c.checksAnswered })}
              </span>
            )}
            {c.minScore != null && (
              <span
                className={`hidden shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-medium sm:inline ${
                  c.gate === 'fail'
                    ? 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
                    : 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
                }`}
              >
                ≥ {c.minScore}
              </span>
            )}
          </span>
          <span className="text-right text-xs text-ink-500 dark:text-ink-400 sm:text-sm">{num(c.weight)}%</span>
          <span className="col-span-2 flex items-center gap-2 sm:col-span-1">
            <span className="h-2 flex-1 overflow-hidden rounded-full bg-ink-100 dark:bg-white/10">
              <span className={`block h-full rounded-full ${style.bar}`} style={{ width: `${pct}%` }} />
            </span>
            <span className="w-8 text-right text-sm font-semibold text-ink-900 dark:text-white">{num(c.score)}</span>
          </span>
          <span className="hidden text-right text-sm font-semibold text-ai-700 dark:text-ai-300 sm:block">
            {t('breakdown.contributionValue', { value: num(c.contribution) })}
          </span>
        </div>
      </button>

      {open && (
        <div className="mt-2 space-y-2 rounded-xl bg-ink-50 p-3 text-sm dark:bg-white/[0.03]">
          <p className="text-xs text-ai-700 sm:hidden dark:text-ai-300">
            {t('breakdown.contribution')}: {t('breakdown.contributionValue', { value: num(c.contribution) })}
          </p>
          <InBandPosition c={c} num={num} band={band} />
          <div>
            <p className="mb-1 flex items-center gap-1 text-[11px] font-semibold uppercase tracking-wide text-ink-500 dark:text-ink-400">
              <Quote className="h-3 w-3" /> {t('breakdown.evidence')}
            </p>
            {c.evidence ? (
              <blockquote className="border-l-2 border-ai-400 pl-3 italic text-ink-700 dark:text-ink-200">
                {c.evidence}
              </blockquote>
            ) : (
              <p className="text-xs text-ink-400">{t('breakdown.noEvidence')}</p>
            )}
          </div>
          {c.reasoning && (
            <div>
              <p className="mb-0.5 text-[11px] font-semibold uppercase tracking-wide text-ink-500 dark:text-ink-400">
                {t('breakdown.reasoning')}
              </p>
              <p className="text-ink-700 dark:text-ink-200">{c.reasoning}</p>
            </div>
          )}
          {(c.description || appliedLevel) && (
            <div className="grid gap-2 border-t border-ink-200 pt-2 dark:border-white/10 sm:grid-cols-2">
              {c.description && (
                <div>
                  <p className="mb-0.5 text-[11px] font-semibold uppercase tracking-wide text-ink-500 dark:text-ink-400">
                    {t('breakdown.guide')}
                  </p>
                  <p className="text-xs text-ink-600 dark:text-ink-300">{c.description}</p>
                </div>
              )}
              {appliedLevel && (
                <div>
                  <p className="mb-0.5 text-[11px] font-semibold uppercase tracking-wide text-ink-500 dark:text-ink-400">
                    {t('breakdown.levelApplied')} · {t(`breakdown.band.${band}`)}
                  </p>
                  <p className="text-xs text-ink-600 dark:text-ink-300">{appliedLevel}</p>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </li>
  )
}

function AiNotes({ score }: { score: Breakdown }) {
  const { t } = useTranslation(NS)
  const summary = splitSummary(score.summary)
  const hasAny =
    summary.length > 0 ||
    score.skillsMatched.length > 0 ||
    score.skillsGaps.length > 0 ||
    score.redFlags.length > 0 ||
    !!score.seniorityAlignment ||
    !!score.experienceRelevance
  if (!hasAny) return null

  return (
    <div className="mt-5 space-y-3 border-t border-ink-100 pt-4 dark:border-white/10">
      <p className="flex items-center gap-1.5 text-sm font-semibold text-ink-900 dark:text-white">
        <Sparkles className="h-4 w-4 text-ai-600 dark:text-ai-400" /> {t('breakdown.aiTitle')}
      </p>
      {summary.map((s) => (
        <p key={s} className="text-sm leading-relaxed text-ink-600 dark:text-ink-300">
          {s}
        </p>
      ))}
      <Chips label={t('breakdown.skillsMatched')} items={score.skillsMatched} tone="good" />
      <Chips label={t('breakdown.skillsGaps')} items={score.skillsGaps} tone="warn" />
      {score.redFlags.length > 0 && (
        <div>
          <p className="mb-1 text-xs font-semibold text-ink-500 dark:text-ink-400">{t('breakdown.redFlags')}</p>
          <ul className="list-disc space-y-0.5 pl-5 text-sm text-red-600 dark:text-red-400">
            {score.redFlags.map((f) => (
              <li key={f}>{f}</li>
            ))}
          </ul>
        </div>
      )}
      <dl className="grid gap-3 sm:grid-cols-2">
        {score.seniorityAlignment && (
          <div>
            <dt className="text-xs font-semibold text-ink-500 dark:text-ink-400">{t('breakdown.seniority')}</dt>
            <dd className="mt-0.5 text-sm text-ink-700 dark:text-ink-200">{score.seniorityAlignment}</dd>
          </div>
        )}
        {score.experienceRelevance && (
          <div>
            <dt className="text-xs font-semibold text-ink-500 dark:text-ink-400">{t('breakdown.experience')}</dt>
            <dd className="mt-0.5 text-sm text-ink-700 dark:text-ink-200">{score.experienceRelevance}</dd>
          </div>
        )}
      </dl>
    </div>
  )
}

function Chips({ label, items, tone }: { label: string; items: string[]; tone: 'good' | 'warn' }) {
  if (items.length === 0) return null
  const cls =
    tone === 'good'
      ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-400'
      : 'bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-400'
  return (
    <div>
      <p className="mb-1 text-xs font-semibold text-ink-500 dark:text-ink-400">{label}</p>
      <div className="flex flex-wrap gap-1.5">
        {items.map((s) => (
          <span key={s} className={`rounded-lg px-2 py-1 text-xs font-medium ${cls}`}>
            {s}
          </span>
        ))}
      </div>
    </div>
  )
}
