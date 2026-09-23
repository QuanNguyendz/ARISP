import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertCircle, BookOpen, Calculator, ChevronDown, ChevronUp, ExternalLink, RotateCcw } from 'lucide-react'
import {
  CV_RECOMMENDATIONS,
  CV_RUBRIC_LEVEL_KEYS,
  DEFAULT_CV_SCORING_POLICY,
  bandRanges,
  clonePolicy,
  cvPolicyProblems,
  samePolicy,
  tierRanges,
} from '@ari/shared/fservices/cvRubric'
import type { CvRubricCriterion, CvScoringPolicy } from '@ari/shared/fservices/cvRubric'
import { gateSummary, hasGates, weightedExpression } from './rubricFormula'

const NS = 'modules/staff/cvScoring'

/**
 * Tài liệu tuyển dụng làm cơ sở cho các tham số công thức (ADR-075). Chung một khung: loại theo điều kiện bắt buộc →
 * chấm theo thang có neo → cộng có trọng số → so với ngưỡng; không nguồn nào khuyên để người chấm tự gõ biểu thức.
 */
export const CV_FORMULA_SOURCES = [
  {
    key: 'opm',
    url: 'https://www.opm.gov/policy-data-oversight/assessment-and-selection/other-assessment-methods/training-and-experience-evaluations/',
  },
  { key: 'uwyo', url: 'https://www.uwyo.edu/hr/hiring-toolkit/matrix-instructions.html' },
  { key: 'fourCorner', url: 'https://www.4cornerresources.com/blog/resume-screening-scorecard/' },
  {
    key: 'zythr',
    url: 'https://zythr.com/resources/candidate-scoring-model-in-recruiting-what-it-is-and-how-to-build-one/how-to-build-a-weighted-candidate-scoring-matrix-stepbystep-template-and-examples',
  },
  {
    key: 'libretexts',
    url: 'https://biz.libretexts.org/Courses/Prince_Georges_Community_College/BMT_2610:_Human_Resource_Management_(Duru_2021)/04:_Selection/4.05:_Testing_and_Selecting',
  },
  { key: 'ticnote', url: 'https://ticnote.com/en/blog/how-to-score-resumes' },
] as const

const NUM =
  'w-20 rounded-lg border border-ink-200 bg-white px-2 py-1 text-right text-sm text-ink-900 focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/5 dark:text-white'

const BAND_CHIP: Record<(typeof CV_RUBRIC_LEVEL_KEYS)[number], string> = {
  excellent: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
  good: 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-400',
  fair: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  poor: 'bg-red-100 text-red-600 dark:bg-red-500/20 dark:text-red-400',
}

const TIER_CHIP: Record<string, string> = {
  'Strong Hire': 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
  Hire: 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-400',
  'Proceed with caution': 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  Reject: 'bg-red-100 text-red-600 dark:bg-red-500/20 dark:text-red-400',
}

/** Số nguyên từ ô nhập; ô trống giữ NaN để bộ kiểm báo lỗi thay vì âm thầm thành 0. */
const toInt = (v: string) => (v.trim() === '' ? Number.NaN : Math.trunc(Number(v)))

interface CvScoringFormulaEditorProps {
  policy: CvScoringPolicy
  onChange: (next: CvScoringPolicy) => void
  /** Bộ tiêu chí đang soạn — để in công thức sống bằng đúng tên + trọng số. */
  criteria: CvRubricCriterion[]
  showProblems?: boolean
}

/**
 * Công thức chấm CV do Hiring Manager quyết định (ADR-075) — phần CẤP TIN: ngưỡng bốn dải điểm và ngưỡng nhãn khuyến
 * nghị, cùng công thức sống viết bằng tên tiêu chí thật. Phần cấp TIÊU CHÍ (điều kiện bắt buộc, điểm tối thiểu, trọng
 * số ý kiểm) khai ngay trên từng dòng tiêu chí.
 */
export default function CvScoringFormulaEditor({ policy, onChange, criteria, showProblems = false }: CvScoringFormulaEditorProps) {
  const { t } = useTranslation(NS)
  const [sourcesOpen, setSourcesOpen] = useState(false)
  const problems = cvPolicyProblems(policy)
  const isDefault = samePolicy(policy, DEFAULT_CV_SCORING_POLICY)
  const bands = bandRanges(policy)
  const tiers = tierRanges(policy)
  const expr = weightedExpression(criteria)
  const gates = gateSummary(criteria)

  const setBand = (k: keyof CvScoringPolicy['bands'], v: string) =>
    onChange({ ...policy, bands: { ...policy.bands, [k]: toInt(v) } })
  const setTier = (k: keyof CvScoringPolicy['tiers'], v: string) =>
    onChange({ ...policy, tiers: { ...policy.tiers, [k]: toInt(v) } })

  const bandsValid = !problems.includes('badBands')
  const tiersValid = !problems.includes('badTiers')

  return (
    <section className="rounded-2xl border border-ai-200 bg-ai-50/40 p-4 dark:border-ai-500/20 dark:bg-ai-500/5">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h4 className="flex items-center gap-1.5 text-sm font-semibold text-ink-900 dark:text-white">
            <Calculator className="h-4 w-4 text-ai-600 dark:text-ai-400" /> {t('formula.title')}
          </h4>
          <p className="mt-0.5 max-w-2xl text-xs leading-relaxed text-ink-500 dark:text-ink-400">{t('formula.hint')}</p>
        </div>
        {isDefault ? (
          <span className="rounded-full bg-ink-100 px-2 py-0.5 text-[11px] font-medium text-ink-600 dark:bg-white/10 dark:text-ink-300">
            {t('formula.isDefault')}
          </span>
        ) : (
          <button
            type="button"
            onClick={() => onChange(clonePolicy(DEFAULT_CV_SCORING_POLICY))}
            className="inline-flex items-center gap-1 text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
          >
            <RotateCcw className="h-3.5 w-3.5" /> {t('formula.reset')}
          </button>
        )}
      </div>

      {/* Công thức sống — HM đọc lại được đúng thứ hệ thống sẽ tính. */}
      <div className="mt-3 space-y-1 rounded-xl border border-ai-200 bg-white px-3 py-2 font-mono text-xs leading-relaxed text-ink-800 dark:border-ai-500/20 dark:bg-white/5 dark:text-ink-100">
        <p>
          <span className="font-semibold">{t('formula.total')}</span> = {expr || '…'}
        </p>
        <p className="text-ink-600 dark:text-ink-300">{t('formula.inBand')}</p>
      </div>
      <div className="mt-2 text-xs leading-relaxed text-ink-600 dark:text-ink-300">
        {hasGates(gates) ? (
          <>
            <p className="font-medium">{t('formula.modelHybrid')}</p>
            <ul className="mt-0.5 list-disc pl-5">
              {gates.knockouts.map((name) => (
                <li key={`k-${name}`}>{t('formula.gateKnockout', { name })}</li>
              ))}
              {gates.minScores.map((g) => (
                <li key={`m-${g.name}`}>{t('formula.gateMin', { name: g.name, min: g.min })}</li>
              ))}
            </ul>
            <p className="mt-0.5 text-ink-500 dark:text-ink-400">{t('formula.gatesNote')}</p>
          </>
        ) : (
          <p>{t('formula.modelCompensatory')}</p>
        )}
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <div>
          <p className="text-xs font-semibold text-ink-700 dark:text-ink-200">{t('formula.bandsTitle')}</p>
          <p className="mb-2 text-[11px] text-ink-500 dark:text-ink-400">{t('formula.bandsHint')}</p>
          <div className="flex flex-wrap gap-3">
            {(['excellentFrom', 'goodFrom', 'fairFrom'] as const).map((k) => (
              <label key={k} className="flex items-center gap-1.5 text-xs text-ink-600 dark:text-ink-300">
                {t(`formula.bandFrom.${k}`)}
                <input
                  type="number"
                  min={1}
                  max={99}
                  step={1}
                  value={Number.isNaN(policy.bands[k]) ? '' : policy.bands[k]}
                  onChange={(e) => setBand(k, e.target.value)}
                  className={NUM}
                />
              </label>
            ))}
          </div>
          {bandsValid && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {CV_RUBRIC_LEVEL_KEYS.map((k) => (
                <span key={k} className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${BAND_CHIP[k]}`}>
                  {t(`breakdown.band.${k}`)} {bands[k].min}–{bands[k].max}
                </span>
              ))}
            </div>
          )}
        </div>

        <div>
          <p className="text-xs font-semibold text-ink-700 dark:text-ink-200">{t('formula.tiersTitle')}</p>
          <p className="mb-2 text-[11px] text-ink-500 dark:text-ink-400">{t('formula.tiersHint')}</p>
          <div className="flex flex-wrap gap-3">
            {(['strongHireFrom', 'hireFrom', 'cautionFrom'] as const).map((k) => (
              <label key={k} className="flex items-center gap-1.5 text-xs text-ink-600 dark:text-ink-300">
                {t(`formula.tierFrom.${k}`)}
                <input
                  type="number"
                  min={1}
                  max={100}
                  step={1}
                  value={Number.isNaN(policy.tiers[k]) ? '' : policy.tiers[k]}
                  onChange={(e) => setTier(k, e.target.value)}
                  className={NUM}
                />
              </label>
            ))}
          </div>
          {tiersValid && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {CV_RECOMMENDATIONS.map((k) => (
                <span key={k} className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${TIER_CHIP[k]}`}>
                  {t(`breakdown.recName.${k}`)} {tiers[k].from}–{tiers[k].to}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>

      {showProblems && problems.length > 0 && (
        <ul className="mt-3 space-y-0.5 text-xs text-red-600 dark:text-red-400">
          {problems.map((p) => (
            <li key={p} className="flex items-center gap-1.5">
              <AlertCircle className="h-3.5 w-3.5 shrink-0" /> {t(`formula.problems.${p}`)}
            </li>
          ))}
        </ul>
      )}

      <div className="mt-3 border-t border-ai-200/70 pt-2 dark:border-ai-500/20">
        <button
          type="button"
          onClick={() => setSourcesOpen((o) => !o)}
          aria-expanded={sourcesOpen}
          className="inline-flex items-center gap-1 text-xs font-medium text-ai-700 hover:underline dark:text-ai-300"
        >
          <BookOpen className="h-3.5 w-3.5" /> {t('formula.sourcesTitle')}
          {sourcesOpen ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
        </button>
        {sourcesOpen && (
          <div className="mt-1.5 text-xs text-ink-600 dark:text-ink-300">
            <p className="mb-1">{t('formula.sourcesHint')}</p>
            <ul className="space-y-1">
              {CV_FORMULA_SOURCES.map((s) => (
                <li key={s.key}>
                  <a
                    href={s.url}
                    target="_blank"
                    rel="noreferrer noopener"
                    className="inline-flex items-start gap-1 text-brand-600 hover:underline dark:text-brand-400"
                  >
                    <ExternalLink className="mt-0.5 h-3 w-3 shrink-0" />
                    <span>{t(`formula.sources.${s.key}`)}</span>
                  </a>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </section>
  )
}

/** Tóm tắt công thức ở chế độ chỉ đọc (màn tin, màn duyệt phiếu, màn tạo tin). */
export function FormulaSummary({ policy, criteria }: { policy?: CvScoringPolicy | null; criteria: CvRubricCriterion[] }) {
  const { t } = useTranslation(NS)
  const p = policy ?? DEFAULT_CV_SCORING_POLICY
  const bands = bandRanges(p)
  const tiers = tierRanges(p)
  const gates = gateSummary(criteria)
  const expr = weightedExpression(criteria)

  return (
    <div className="rounded-xl border border-ai-200 bg-ai-50/40 px-3 py-2 text-[11px] leading-relaxed text-ink-600 dark:border-ai-500/20 dark:bg-ai-500/5 dark:text-ink-300">
      <p className="flex items-center gap-1 font-semibold text-ink-700 dark:text-ink-200">
        <Calculator className="h-3 w-3 text-ai-600 dark:text-ai-400" /> {t('formula.title')}
        {samePolicy(p, DEFAULT_CV_SCORING_POLICY) && (
          <span className="font-normal text-ink-400">· {t('formula.isDefault')}</span>
        )}
      </p>
      {expr && (
        <p className="mt-0.5 font-mono">
          {t('formula.total')} = {expr}
        </p>
      )}
      <p className="mt-0.5">
        {t('formula.summaryBands', {
          excellent: `${bands.excellent.min}–${bands.excellent.max}`,
          good: `${bands.good.min}–${bands.good.max}`,
          fair: `${bands.fair.min}–${bands.fair.max}`,
          poor: `${bands.poor.min}–${bands.poor.max}`,
        })}
      </p>
      <p>
        {t('formula.summaryTiers', {
          strong: tiers['Strong Hire'].from,
          hire: tiers.Hire.from,
          caution: tiers['Proceed with caution'].from,
        })}
      </p>
      {hasGates(gates) && (
        <p>
          {t('formula.summaryGates', {
            list: [
              ...gates.knockouts.map((name) => t('formula.gateKnockout', { name })),
              ...gates.minScores.map((g) => t('formula.gateMin', { name: g.name, min: g.min })),
            ].join('; '),
          })}
        </p>
      )}
    </div>
  )
}
