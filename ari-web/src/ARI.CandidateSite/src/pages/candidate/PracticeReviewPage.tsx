import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  ArrowRightCircle,
  Bot,
  Check,
  ChevronRight,
  Clock,
  Copy,
  FileText,
  Info,
  Languages,
  Lightbulb,
  Quote,
  Sparkles,
  User,
  XCircle,
} from 'lucide-react'
import { interviewService } from '@ari/shared/fservices/interview'
import { Skeleton } from '@ari/shared/ui/Skeleton'
import CriterionBar from '@components/CriterionBar'
import { formatDate, formatDuration, scoreColor, type TFn } from './_reportUi'
import type { MyPracticeReview, MyPracticeTurn } from '@ari/shared/types/application'

/** ISO giờ → HH:mm cho dòng meta của từng lượt. */
function turnTime(iso?: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

/** Thanh điểm 0–10 cho chỉ số năng lực ngôn ngữ — cùng thang cỡ chữ với CriterionBar. */
function LanguageMetric({ label, value }: { label: string; value: number }) {
  const pct = Math.max(0, Math.min(100, Math.round(value * 10)))
  return (
    <div>
      <div className="mb-1 flex items-center justify-between text-sm">
        <span className="font-medium text-ink-700">{label}</span>
        <span className="font-semibold text-ink-900">{value}/10</span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-white/70">
        <div className={`h-full rounded-full ${scoreColor(pct)}`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}

/** Nhận xét AI của đúng lượt hỏi–đáp này (BE ghép theo sequenceNumber — ADR-051). */
function TurnNote({ turn, t }: { turn: MyPracticeTurn; t: TFn }) {
  // Điểm chỉ có ý nghĩa khi model thực sự chấm. Báo cáo cũ không có điểm → BE trả null; nếu vẫn
  // vẽ chip thì thành "0/100 · Cần cải thiện" trong khi nhận xét lại khen.
  const score = typeof turn.score === 'number' && turn.score > 0 ? Math.round(turn.score) : null
  const hasNote = !!turn.analysis || !!turn.feedback
  if (!hasNote && score === null) return null

  const good = score !== null && score >= 70
  const mid = score !== null && score >= 50 && score < 70
  const weak = score !== null && score < 50
  const tone = good
    ? 'border-emerald-200 bg-emerald-50/50'
    : mid
      ? 'border-amber-200 bg-amber-50/50'
      : weak
        ? 'border-red-200 bg-red-50/40'
        : 'border-ink-200 bg-ink-50'
  const chip = good
    ? 'bg-emerald-100 text-emerald-700'
    : mid
      ? 'bg-amber-100 text-amber-700'
      : 'bg-red-100 text-red-700'

  return (
    <div className={`ml-11 rounded-xl border p-3 ${tone}`}>
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <span className="inline-flex items-center gap-1 text-[11px] font-semibold uppercase tracking-wide text-ink-500">
          <Sparkles className="h-3 w-3" /> {t('turnNote.title')}
        </span>
        {score !== null && (
          <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${chip}`}>
            {score}/100 ·{' '}
            {good
              ? t('questionAnalysis.good')
              : mid
                ? t('questionAnalysis.fair')
                : t('questionAnalysis.needsImprovement')}
          </span>
        )}
      </div>
      {turn.analysis && <p className="text-sm text-ink-700">{turn.analysis}</p>}
      {turn.feedback && (
        <p className="mt-2 flex gap-2 text-sm text-ink-600">
          <Lightbulb className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" />
          <span>{turn.feedback}</span>
        </p>
      )}
    </div>
  )
}

function ReviewSkeleton() {
  return (
    <div className="mx-auto max-w-6xl space-y-6 px-4 py-6 sm:px-6">
      <Skeleton className="h-28 w-full rounded-2xl" />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,360px)_minmax(0,1fr)]">
        <div className="space-y-4">
          <Skeleton className="h-56 w-full rounded-2xl" />
          <Skeleton className="h-48 w-full rounded-2xl" />
        </div>
        <Skeleton className="h-[32rem] w-full rounded-2xl" />
      </div>
    </div>
  )
}

/**
 * Xem lại buổi phỏng vấn THỬ: transcript + nhận xét AI (ADR-051).
 * Bố cục 2 cột — cột trái là tổng quan (điểm, tiêu chí, ngôn ngữ), cột phải là hội thoại kèm
 * nhận xét ngay dưới từng câu trả lời, để không phải cuộn qua lại giữa hai phần.
 * Không hiển thị verdict Pass/Not Pass — buổi thử chỉ để luyện tập.
 */
export default function PracticeReviewPage() {
  const { t, i18n } = useTranslation('modules/candidate/practiceReview')
  const { sessionId } = useParams<{ sessionId: string }>()
  const [review, setReview] = useState<MyPracticeReview | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!sessionId) return
    let active = true
    setLoading(true)
    setError('')
    interviewService
      .getMyPracticeReview(sessionId)
      .then((d) => active && setReview(d))
      .catch((err: unknown) => {
        if (!active) return
        const message =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ||
          (err as { message?: string })?.message ||
          t('loadError')
        setError(message)
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [sessionId, t])

  const transcriptText = useMemo(() => {
    if (!review) return ''
    const lines = review.turns.flatMap((turn) => {
      const block = [`${t('transcript.aiLabel')}: ${turn.question}`]
      if (turn.answer) block.push(`${t('transcript.youLabel')}: ${turn.answer}`)
      return block
    })
    if (review.closingText) lines.push(`${t('transcript.aiLabel')}: ${review.closingText}`)
    return lines.join('\n\n')
  }, [review, t])

  const copyTranscript = async () => {
    try {
      await navigator.clipboard.writeText(transcriptText)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2000)
    } catch {
      /* clipboard bị chặn — ứng viên vẫn bôi đen copy tay được */
    }
  }

  const ev = review?.evaluation ?? null
  const lang = ev?.languageAssessment ?? null
  const reportLanguageMismatch =
    !!review?.reportLanguage && review.reportLanguage !== i18n.language
  const score = typeof ev?.overallScore === 'number' ? Math.round(ev.overallScore) : null
  const answeredCount = review?.turns.filter((turn) => !!turn.answer).length ?? 0

  const roundTypeLabels: Record<string, string> = {
    screening: t('round.screening'),
    technical: t('round.technical'),
    online_test: t('round.online_test'),
    final: t('round.final'),
  }
  const roundType =
    roundTypeLabels[(review?.roundType || '').toLowerCase()] || review?.roundType || ''

  return (
    <>
      <div className="mx-auto max-w-6xl px-4 pt-6 sm:px-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/candidate/applications" className="hover:text-brand-600">
            {t('breadcrumb.applications')}
          </Link>
          <ChevronRight className="h-4 w-4" />
          {review ? (
            <Link
              to={`/candidate/applications/${review.applicationId}`}
              className="truncate hover:text-brand-600"
            >
              {review.jobTitle || t('position')}
            </Link>
          ) : (
            <span className="truncate">...</span>
          )}
          <ChevronRight className="h-4 w-4" />
          <span className="font-medium text-ink-600">{t('breadcrumb.current')}</span>
        </div>
      </div>

      {loading ? (
        <ReviewSkeleton />
      ) : error ? (
        <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6">
          <div className="flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <XCircle className="h-4 w-4" /> {error}
          </div>
        </div>
      ) : !review ? null : (
        <main className="mx-auto max-w-6xl space-y-6 px-4 py-6 sm:px-6">
          {/* Header gọn, chiếm hết chiều ngang */}
          <div className="rounded-2xl border border-ink-200 bg-gradient-to-r from-ai-50 to-white p-4 shadow-card sm:p-5">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1 rounded-full bg-ai-50 px-2.5 py-0.5 text-xs font-semibold text-ai-700 ring-1 ring-ai-200">
                    <Sparkles className="h-3 w-3" /> {t('badge.practice')}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-semibold text-brand-700">
                    {t('roundBadge', {
                      number: review.roundNumber,
                      type: roundType ? ` (${roundType})` : '',
                    })}
                  </span>
                </div>
                <h1 className="mt-2 truncate font-display text-xl font-extrabold">
                  {review.jobTitle || t('position')}
                </h1>
                <p className="text-sm text-ink-500">
                  {formatDate(review.endedAt || review.startedAt)}
                  {formatDuration(review.durationSeconds)
                    ? ` · ${formatDuration(review.durationSeconds)}`
                    : ''}
                  {` · ${t('meta.turns', { answered: answeredCount, total: review.turns.length })}`}
                </p>
              </div>
              <div className="flex items-center gap-4">
                {score !== null && (
                  <div className="text-center">
                    <div className="font-display text-3xl font-extrabold text-ink-900">
                      {score}
                      <span className="text-base text-ink-400">/100</span>
                    </div>
                    <div className="text-xs text-ink-500">{t('meta.referenceScore')}</div>
                  </div>
                )}
                <Link
                  to={`/candidate/applications/${review.applicationId}`}
                  className="hidden rounded-xl border border-ink-200 bg-white px-3 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50 sm:block"
                >
                  {t('backToApplication')}
                </Link>
              </div>
            </div>
            <p className="mt-3 flex items-start gap-2 border-t border-ink-100 pt-3 text-sm text-ink-600">
              <Info className="mt-0.5 h-4 w-4 shrink-0 text-ai-600" />
              {t('notice')}
            </p>
          </div>

          <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,360px)_minmax(0,1fr)]">
            {/* ===== Cột trái: tổng quan (dính khi cuộn) ===== */}
            <div className="space-y-4 lg:sticky lg:top-6 lg:max-h-[calc(100vh-3rem)] lg:overflow-y-auto lg:pb-2 lg:pr-1">
              {review.evaluationPending ? (
                <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
                  <div className="flex items-center gap-2 text-sm font-semibold text-ink-700">
                    <Clock className="h-4 w-4 text-amber-500" /> {t('evaluation.pendingTitle')}
                  </div>
                  <p className="mt-1 text-sm text-ink-500">{t('evaluation.pendingDescription')}</p>
                  <div className="mt-4 space-y-3">
                    <Skeleton className="h-4 w-2/3 rounded-full" />
                    <Skeleton className="h-4 w-full rounded-full" />
                    <Skeleton className="h-4 w-5/6 rounded-full" />
                  </div>
                </div>
              ) : !ev ? (
                <div className="rounded-2xl border border-ink-200 bg-white p-5 text-center shadow-card">
                  <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
                    <FileText className="h-6 w-6" />
                  </div>
                  <p className="mt-3 font-semibold text-ink-700">{t('evaluation.emptyTitle')}</p>
                  <p className="mt-1 text-sm text-ink-500">{t('evaluation.emptyDescription')}</p>
                </div>
              ) : (
                <>
                  {ev.reasoning && (
                    <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
                      <h2 className="mb-2 font-display text-base font-bold">
                        {t('evaluation.title')}
                      </h2>
                      <p className="text-sm leading-relaxed text-ink-600">{ev.reasoning}</p>
                      {/* Báo cáo viết bằng ngôn ngữ lúc bắt đầu phiên — nói rõ khi lệch với UI hiện tại */}
                      {reportLanguageMismatch && (
                        <p className="mt-2 border-t border-ink-100 pt-2 text-xs text-ink-400">
                          {t('evaluation.writtenIn', {
                            lang: t(`language.${review.reportLanguage}`, {
                              defaultValue: review.reportLanguage ?? '',
                            }),
                          })}
                        </p>
                      )}
                    </div>
                  )}

                  {ev.criterionScores.length > 0 && (
                    <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
                      <h2 className="mb-3 font-display text-base font-bold">
                        {t('evaluation.scoresByCriteria')}
                      </h2>
                      <div className="space-y-3">
                        {ev.criterionScores.map((c, i) => (
                          <CriterionBar key={i} c={c} t={t} />
                        ))}
                      </div>
                    </div>
                  )}

                  {lang && (
                    <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-4 shadow-card">
                      <div className="flex items-center justify-between gap-2">
                        <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
                          <Languages className="h-4 w-4" />
                          {t('languageAssessment.title', {
                            lang:
                              lang.language?.toUpperCase() ||
                              t('languageAssessment.fallbackCode'),
                          })}
                        </div>
                        {lang.cefrLevel && (
                          <span className="rounded-lg bg-white px-2.5 py-1 font-display text-lg font-extrabold text-ai-700 ring-1 ring-ai-200">
                            {lang.cefrLevel}
                          </span>
                        )}
                      </div>
                      <div className="mt-3 space-y-2.5">
                        <LanguageMetric
                          label={t('languageAssessment.fluency')}
                          value={lang.fluency}
                        />
                        <LanguageMetric
                          label={t('languageAssessment.grammar')}
                          value={lang.grammar}
                        />
                        <LanguageMetric
                          label={t('languageAssessment.vocabulary')}
                          value={lang.vocabulary}
                        />
                        <LanguageMetric
                          label={t('languageAssessment.comprehension')}
                          value={lang.comprehension}
                        />
                      </div>
                      {lang.languageAdherence && (
                        <p className="mt-3 border-t border-ai-100 pt-3 text-sm text-ink-600">
                          {lang.languageAdherence}
                        </p>
                      )}
                      {lang.evidence && (
                        <p className="mt-2 flex gap-2 rounded-lg bg-white/70 p-2 text-xs italic text-ink-500">
                          <Quote className="mt-0.5 h-3.5 w-3.5 shrink-0 text-ai-500" />
                          <span>{lang.evidence}</span>
                        </p>
                      )}
                      <p className="mt-2 text-[11px] text-ink-400">
                        {t('languageAssessment.basedOnAnswers')}
                      </p>
                    </div>
                  )}

                  {ev.recommendedNextStep && (
                    <div className="rounded-2xl border border-brand-200 bg-brand-50/60 p-4 shadow-card">
                      <div className="flex items-center gap-2 text-sm font-semibold text-brand-700">
                        <ArrowRightCircle className="h-4 w-4" /> {t('nextStep.title')}
                      </div>
                      <p className="mt-2 text-sm text-ink-600">{ev.recommendedNextStep}</p>
                    </div>
                  )}
                </>
              )}
            </div>

            {/* ===== Cột phải: hội thoại + nhận xét từng câu ===== */}
            <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card sm:p-5">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                <div>
                  <h2 className="font-display text-lg font-bold">{t('transcript.title')}</h2>
                  <p className="text-xs text-ink-400">{t('transcript.subtitle')}</p>
                </div>
                {review.turns.length > 0 && (
                  <button
                    onClick={copyTranscript}
                    className="inline-flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-1.5 text-sm font-semibold text-ink-700 hover:bg-ink-50"
                  >
                    {copied ? (
                      <>
                        <Check className="h-4 w-4 text-emerald-600" /> {t('transcript.copied')}
                      </>
                    ) : (
                      <>
                        <Copy className="h-4 w-4" /> {t('transcript.copy')}
                      </>
                    )}
                  </button>
                )}
              </div>

              {review.turns.length === 0 ? (
                <p className="text-sm text-ink-500">{t('transcript.empty')}</p>
              ) : (
                <div className="space-y-5">
                  {review.turns.map((turn) => (
                    <div key={turn.sequenceNumber} className="space-y-3">
                      <div className="flex gap-3">
                        <div className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-50 text-brand-600">
                          <Bot className="h-4 w-4" />
                        </div>
                        <div className="min-w-0 flex-1 rounded-2xl rounded-tl-sm bg-ink-50 p-3">
                          <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-ink-400">
                            {t('transcript.aiLabel')} · {t('transcript.question')}{' '}
                            {turn.sequenceNumber}
                            {turnTime(turn.askedAt) ? ` · ${turnTime(turn.askedAt)}` : ''}
                          </div>
                          <p className="whitespace-pre-wrap text-sm text-ink-700">
                            {turn.question}
                          </p>
                        </div>
                      </div>

                      {turn.answer ? (
                        <div className="flex flex-row-reverse gap-3">
                          <div className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-ai-50 text-ai-700">
                            <User className="h-4 w-4" />
                          </div>
                          <div className="min-w-0 flex-1 rounded-2xl rounded-tr-sm bg-ai-50/50 p-3 ring-1 ring-ai-100">
                            <div className="mb-1 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-400">
                              {turnTime(turn.answeredAt) ? `${turnTime(turn.answeredAt)} · ` : ''}
                              {t('transcript.youLabel')}
                            </div>
                            <p className="whitespace-pre-wrap text-sm text-ink-700">
                              {turn.answer}
                            </p>
                          </div>
                        </div>
                      ) : (
                        <div className="ml-11 text-xs italic text-ink-400">
                          {t('transcript.noAnswer')}
                        </div>
                      )}

                      <TurnNote turn={turn} t={t} />
                    </div>
                  ))}

                  {review.closingText && (
                    <div className="flex gap-3">
                      <div className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-50 text-brand-600">
                        <Bot className="h-4 w-4" />
                      </div>
                      <div className="min-w-0 flex-1 rounded-2xl rounded-tl-sm bg-ink-50 p-3">
                        <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-ink-400">
                          {t('transcript.closing')}
                        </div>
                        <p className="whitespace-pre-wrap text-sm text-ink-700">
                          {review.closingText}
                        </p>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3">
            <Link
              to={`/candidate/applications/${review.applicationId}`}
              className="rounded-xl border border-ink-200 bg-white px-4 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50 sm:hidden"
            >
              {t('backToApplication')}
            </Link>
            <p className="text-xs text-ink-400">{t('footer')}</p>
          </div>
        </main>
      )}
    </>
  )
}
