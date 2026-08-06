import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  ClipboardList,
  Loader2,
  AlertCircle,
  CheckCircle2,
  XCircle,
  ArrowLeft,
  Send,
  Clock,
  Lock,
} from 'lucide-react'
import { onlineTestService } from '@ari/shared/fservices/onlineTest'
import type { OnlineTestResult } from '@ari/shared/types/onlineTest'

function errMsg(e: unknown, fallback: string, unauthorized: string): string {
  const x = e as { response?: { data?: { message?: string }; status?: number } }
  if (x?.response?.status === 401) return unauthorized
  return x?.response?.data?.message || fallback
}

function fmtTime(totalSeconds: number): string {
  const s = Math.max(0, totalSeconds)
  const m = Math.floor(s / 60)
  const sec = s % 60
  return `${m}:${String(sec).padStart(2, '0')}`
}

/** Thẻ hiển thị kết quả đạt/không đạt. */
function ResultCard({
  score,
  isPassed,
  passScore,
  detail,
}: {
  score: number
  isPassed: boolean
  passScore: number
  detail?: string
}) {
  const { t } = useTranslation('modules/candidate/onlineTest')
  return (
    <div
      className={`rounded-2xl border p-6 text-center shadow-sm ${
        isPassed ? 'border-emerald-200 bg-emerald-50' : 'border-red-200 bg-red-50'
      }`}
    >
      {isPassed ? (
        <CheckCircle2 className="mx-auto mb-2 h-12 w-12 text-emerald-600" />
      ) : (
        <XCircle className="mx-auto mb-2 h-12 w-12 text-red-500" />
      )}
      <h2 className={`text-lg font-bold ${isPassed ? 'text-emerald-700' : 'text-red-600'}`}>
        {isPassed ? t('passed') : t('notPassed')}
      </h2>
      <p className="mt-1 text-3xl font-extrabold text-ink-900">{Math.round(score)}/100</p>
      <p className="mt-1 text-sm text-ink-500">{t('page.passScoreLine', { score: passScore })}</p>
      {detail && <p className="mt-1 text-sm text-ink-500">{detail}</p>}
      <Link
        to="/candidate/applications"
        className="mt-4 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
      >
        <ArrowLeft className="h-4 w-4" /> {t('page.back')}
      </Link>
    </div>
  )
}

export default function CandidateOnlineTestPage() {
  const { t } = useTranslation('modules/candidate/onlineTest')
  const { applicationId } = useParams<{ applicationId: string }>()
  const [answers, setAnswers] = useState<Record<string, number[]>>({})
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState('')
  const [result, setResult] = useState<OnlineTestResult | null>(null)
  const [timeLeft, setTimeLeft] = useState<number | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['online-test', applicationId],
    queryFn: () => onlineTestService.getTest(applicationId as string),
    enabled: !!applicationId,
    retry: false,
  })

  const questions = useMemo(() => data?.questions ?? [], [data])
  const answeredCount = questions.filter((q) => (answers[q.id]?.length ?? 0) > 0).length
  const allAnswered = questions.length > 0 && answeredCount === questions.length
  const taking = !!data && !data.alreadySubmitted && questions.length > 0 && !result

  const select = (questionId: string, optionIndex: number, multiple: boolean) => {
    setAnswers((prev) => {
      const current = prev[questionId] ?? []
      if (!multiple) return { ...prev, [questionId]: [optionIndex] }
      const next = current.includes(optionIndex)
        ? current.filter((i) => i !== optionIndex)
        : [...current, optionIndex].sort((a, b) => a - b)
      return { ...prev, [questionId]: next }
    })
  }

  const submit = useCallback(
    async (auto = false) => {
      if (!applicationId || submitting || result) return
      if (!auto && !allAnswered) return
      setSubmitting(true)
      setSubmitError('')
      try {
        const res = await onlineTestService.submit(applicationId, answers)
        setResult(res)
        window.scrollTo({ top: 0, behavior: 'smooth' })
      } catch (e) {
        setSubmitError(errMsg(e, t('page.submitError'), t('page.unauthorized')))
      } finally {
        setSubmitting(false)
      }
    },
    [applicationId, submitting, result, allAnswered, answers, t]
  )

  // Khởi tạo đồng hồ đếm ngược một lần khi có đề (chưa nộp).
  useEffect(() => {
    if (data && !data.alreadySubmitted && data.questions.length > 0) {
      setTimeLeft((prev) => (prev === null ? data.durationMinutes * 60 : prev))
    }
  }, [data])

  // Tick mỗi giây.
  useEffect(() => {
    if (!taking || timeLeft === null || timeLeft <= 0) return
    const id = setInterval(() => setTimeLeft((s) => (s !== null ? s - 1 : s)), 1000)
    return () => clearInterval(id)
  }, [taking, timeLeft])

  // Hết giờ → tự nộp (kể cả khi chưa trả lời hết).
  const autoSubmittedRef = useRef(false)
  useEffect(() => {
    if (taking && timeLeft === 0 && !autoSubmittedRef.current) {
      autoSubmittedRef.current = true
      void submit(true)
    }
  }, [taking, timeLeft, submit])

  return (
    <div className="min-h-screen bg-ink-50 px-4 py-10">
      <div className="mx-auto max-w-3xl">
        <div className="mb-6 flex items-center gap-3">
          <span className="grid h-11 w-11 place-items-center rounded-xl bg-brand-600 text-white">
            <ClipboardList className="h-6 w-6" />
          </span>
          <div>
            <h1 className="text-xl font-bold text-ink-900">{t('page.title')}</h1>
            <p className="text-sm text-ink-500">{data?.jobTitle ? data.jobTitle : t('page.subtitle')}</p>
          </div>
        </div>

        {isLoading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="h-7 w-7 animate-spin text-brand-600" />
          </div>
        ) : error ? (
          <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />{' '}
            {errMsg(error, t('page.loadError'), t('page.unauthorized'))}
          </div>
        ) : data && !data.cvPassed ? (
          <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-sm">
            <span className="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-2xl bg-ink-100 text-ink-400">
              <Lock className="h-7 w-7" />
            </span>
            <h2 className="text-base font-bold text-ink-800">{t('page.lockedTitle')}</h2>
            <p className="mx-auto mt-1 max-w-sm text-sm text-ink-500">{t('page.lockedDetail')}</p>
            <Link
              to="/candidate/applications"
              className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-brand-600 hover:underline"
            >
              <ArrowLeft className="h-4 w-4" /> {t('page.back')}
            </Link>
          </div>
        ) : result ? (
          <ResultCard
            score={result.score}
            isPassed={result.isPassed}
            passScore={result.passScore}
            detail={t('page.resultDetail', { correct: result.correctCount, total: result.totalQuestions })}
          />
        ) : data?.alreadySubmitted ? (
          <ResultCard
            score={data.score ?? 0}
            isPassed={!!data.isPassed}
            passScore={data.passScore}
            detail={t('page.alreadyDone')}
          />
        ) : questions.length === 0 ? (
          <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-sm">
            <AlertCircle className="mx-auto mb-3 h-12 w-12 text-ink-300" />
            <p className="text-sm text-ink-600">{t('page.noTest')}</p>
            <Link
              to="/candidate/applications"
              className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-brand-600 hover:underline"
            >
              <ArrowLeft className="h-4 w-4" /> {t('page.back')}
            </Link>
          </div>
        ) : (
          <>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-ink-200 bg-white px-4 py-3 text-sm shadow-sm">
              <span className="text-ink-600">
                {t('page.progress', {
                  answered: answeredCount,
                  total: questions.length,
                  pass: data?.passScore,
                })}
              </span>
              {timeLeft !== null && (
                <span
                  className={`inline-flex items-center gap-1 rounded-lg px-2 py-1 font-semibold ${
                    timeLeft <= 60 ? 'bg-red-50 text-red-600' : 'bg-brand-50 text-brand-700'
                  }`}
                >
                  <Clock className="h-4 w-4" /> {t('page.timeLeft', { time: fmtTime(timeLeft) })}
                </span>
              )}
            </div>

            {submitError && (
              <div className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {submitError}
              </div>
            )}

            <div className="space-y-4">
              {questions.map((q, idx) => {
                const multiple = q.questionType === 'multiple'
                const picked = answers[q.id] ?? []
                return (
                  <div key={q.id} className="rounded-2xl border border-ink-200 bg-white p-5 shadow-sm">
                    <p className="mb-3 text-sm font-semibold text-ink-900">
                      <span className="mr-1.5 text-ink-400">{t('page.question', { n: idx + 1 })}</span>
                      {q.questionText}
                      {multiple && (
                        <span className="ml-2 rounded bg-ai-50 px-1.5 py-0.5 text-[10px] font-semibold text-ai-700">
                          {t('page.multiHint')}
                        </span>
                      )}
                    </p>
                    <div className="space-y-2">
                      {q.options.map((opt, oi) => {
                        const selected = picked.includes(oi)
                        return (
                          <label
                            key={oi}
                            className={`flex cursor-pointer items-center gap-3 rounded-xl border px-4 py-2.5 text-sm transition ${
                              selected
                                ? 'border-brand-400 bg-brand-50 text-brand-800'
                                : 'border-ink-200 text-ink-700 hover:border-brand-200 hover:bg-ink-50'
                            }`}
                          >
                            <input
                              type={multiple ? 'checkbox' : 'radio'}
                              name={q.id}
                              checked={selected}
                              onChange={() => select(q.id, oi, multiple)}
                              className="h-4 w-4 accent-brand-600"
                            />
                            <span className="font-medium text-ink-400">
                              {String.fromCharCode(65 + oi)}.
                            </span>
                            {opt}
                          </label>
                        )
                      })}
                    </div>
                  </div>
                )
              })}
            </div>

            <div className="sticky bottom-4 mt-6">
              <button
                type="button"
                onClick={() => submit(false)}
                disabled={!allAnswered || submitting}
                className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-5 py-3 text-sm font-semibold text-white shadow-lg hover:bg-brand-700 disabled:opacity-50"
              >
                {submitting ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Send className="h-4 w-4" />
                )}
                {allAnswered
                  ? t('page.submit')
                  : t('page.remaining', { n: questions.length - answeredCount })}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
