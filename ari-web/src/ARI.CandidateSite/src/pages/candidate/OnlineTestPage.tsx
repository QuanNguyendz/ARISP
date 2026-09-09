import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  ClipboardList,
  Loader2,
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  ArrowLeft,
  Send,
  Clock,
  Lock,
  ShieldAlert,
} from 'lucide-react'
import { onlineTestService } from '@ari/shared/fservices/onlineTest'
import type { OnlineTestSubmitAck } from '@ari/shared/types/onlineTest'

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

/**
 * Thẻ báo ĐÃ NỘP BÀI — cố ý không có điểm, điểm sàn hay kết quả đạt/trượt.
 *
 * Điểm sàn là thông tin nội bộ của bộ phận tuyển dụng, và kết quả chỉ công bố khi cả vòng đã chốt.
 * Server cũng không gửi các số đó xuống nữa (`CandidateOnlineTestDto`), nên đây không phải một lớp
 * che mắt — không còn gì để che.
 */
function SubmittedCard({ detail }: { detail?: string }) {
  const { t } = useTranslation('modules/candidate/onlineTest')
  return (
    <div className="rounded-2xl border border-brand-200 bg-brand-50 p-6 text-center shadow-sm">
      <CheckCircle2 className="mx-auto mb-2 h-12 w-12 text-brand-600" />
      <h2 className="text-lg font-bold text-brand-800">{t('page.submittedTitle')}</h2>
      <p className="mt-1 text-sm text-ink-600">{t('page.submittedHint')}</p>
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
  const [result, setResult] = useState<OnlineTestSubmitAck | null>(null)
  const [timeLeft, setTimeLeft] = useState<number | null>(null)
  // Chống gian lận nhẹ: đếm số lần ứng viên rời khỏi bài thi (chuyển tab / mất focus cửa sổ).
  // Giá trị "sống" giữ ở ref (gửi khi nộp, kể cả tự nộp lúc hết giờ); state chỉ để hiện cảnh báo.
  const [tabSwitches, setTabSwitches] = useState(0)
  const tabSwitchRef = useRef(0)
  const lastLeaveRef = useRef(0)

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
        const res = await onlineTestService.submit(applicationId, answers, tabSwitchRef.current)
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

  // Chống gian lận: bắt sự kiện rời khỏi bài thi (ẩn tab hoặc mất focus cửa sổ). Chỉ theo dõi
  // khi đang làm bài. Gộp blur + visibilitychange xảy ra sát nhau (chuyển tab thường bắn cả hai)
  // bằng cửa sổ khử trùng 500ms để không đếm gấp đôi 1 hành động.
  useEffect(() => {
    if (!taking) return
    const registerLeave = () => {
      const now = Date.now()
      if (now - lastLeaveRef.current < 500) return
      lastLeaveRef.current = now
      tabSwitchRef.current += 1
      setTabSwitches(tabSwitchRef.current)
    }
    const onVisibility = () => {
      if (document.hidden) registerLeave()
    }
    document.addEventListener('visibilitychange', onVisibility)
    window.addEventListener('blur', registerLeave)
    return () => {
      document.removeEventListener('visibilitychange', onVisibility)
      window.removeEventListener('blur', registerLeave)
    }
  }, [taking])

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
          <SubmittedCard
            detail={t('page.submittedCount', { total: result.totalQuestions })}
          />
        ) : data?.alreadySubmitted ? (
          <SubmittedCard detail={t('page.alreadyDone')} />
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

            {/* Nhắc nhở chống gian lận — luôn hiển thị trong lúc làm bài. */}
            <div className="mb-3 flex items-start gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-xs text-ink-500">
              <ShieldAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              {t('page.antiCheatHint')}
            </div>

            {/* Cảnh báo leo thang khi ứng viên đã rời khỏi bài thi. */}
            {tabSwitches > 0 && (
              <div className="mb-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm font-medium text-amber-800">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                {t('page.tabSwitchWarning', { count: tabSwitches })}
              </div>
            )}

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
