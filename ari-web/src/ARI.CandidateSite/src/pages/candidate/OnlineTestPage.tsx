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
  XCircle,
} from 'lucide-react'
import { onlineTestService } from '@ari/shared/fservices/onlineTest'
import type { CandidateOnlineTest, OnlineTestSubmitAck } from '@ari/shared/types/onlineTest'
import { HOUR_CYCLE_24, formatTime24 } from '@ari/shared/utils/time24'

/**
 * Đề thi kèm ĐỘ LỆCH giữa giờ server và giờ máy (ms), đo đúng lúc nhận phản hồi.
 *
 * Mọi mốc của bài thi (mở, đóng, đồng hồ đếm ngược) tính theo giờ server cộng độ lệch này. Tính theo
 * giờ máy thì máy chạy nhanh vài phút sẽ báo "đã quá giờ vào làm bài" trong khi server còn chưa mở
 * bài — đúng lỗi đã gặp — và máy chạy chậm sẽ để đồng hồ chạy quá giờ đóng rồi bị server từ chối bài.
 */
type TestWithClock = CandidateOnlineTest & { clockOffsetMs: number }

async function loadTest(applicationId: string): Promise<TestWithClock> {
  const test = await onlineTestService.getTest(applicationId)
  const serverNow = test.serverNow ? new Date(test.serverNow).getTime() : NaN
  return { ...test, clockOffsetMs: Number.isNaN(serverNow) ? 0 : serverNow - Date.now() }
}

/** Hẹn giờ quá xa thì không đặt — setTimeout tràn số ở ~24 ngày, và không ai mở sẵn trang cả ngày. */
const MAX_WAKE_DELAY_MS = 6 * 60 * 60 * 1000

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

/** Giờ hẹn theo múi giờ máy người dùng — dùng ở cả hai thông báo cửa đóng. */
function fmtWhen(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleString(undefined, {
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    ...HOUR_CYCLE_24,
  })
}

export default function CandidateOnlineTestPage() {
  const { t } = useTranslation('modules/candidate/onlineTest')
  const { applicationId } = useParams<{ applicationId: string }>()
  const [answers, setAnswers] = useState<Record<string, number[]>>({})
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState('')
  const [result, setResult] = useState<OnlineTestSubmitAck | null>(null)
  // Chống gian lận nhẹ: đếm số lần ứng viên rời khỏi bài thi (chuyển tab / mất focus cửa sổ).
  // Giá trị "sống" giữ ở ref (gửi khi nộp, kể cả tự nộp lúc hết giờ); state chỉ để hiện cảnh báo.
  const [tabSwitches, setTabSwitches] = useState(0)
  const tabSwitchRef = useRef(0)
  const lastLeaveRef = useRef(0)

  /**
   * Đã bấm "Bắt đầu làm bài" chưa.
   *
   * Vì sao có cửa này thay vì vào thẳng: rời bài thi là TỰ NỘP, nên luật đó phải được nói trước
   * khi vào đề — biết sau khi mất bài thì biết để làm gì.
   *
   * Cửa này KHÔNG giữ đồng hồ lại (ADR-072): bài thi là một đợt thi có giờ đóng chung, đồng hồ đếm
   * tới giờ đóng dù ứng viên đã bấm hay chưa. Cửa nói rõ điều đó và nói còn bao nhiêu phút.
   */
  const [started, setStarted] = useState(false)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['online-test', applicationId],
    queryFn: () => loadTest(applicationId as string),
    enabled: !!applicationId,
    retry: false,
  })

  const questions = useMemo(() => data?.questions ?? [], [data])
  const answeredCount = questions.filter((q) => (answers[q.id]?.length ?? 0) > 0).length
  const allAnswered = questions.length > 0 && answeredCount === questions.length
  const taking = !!data && !data.alreadySubmitted && questions.length > 0 && !result && started

  // ---- Đồng hồ theo giờ SERVER --------------------------------------------------------------
  const clockOffsetMs = data?.clockOffsetMs ?? 0
  const serverNowMs = useCallback(() => Date.now() + clockOffsetMs, [clockOffsetMs])
  // Nhịp đồng hồ chỉ để vẽ lại; "bây giờ" luôn đọc lại giờ thật — không bao giờ lệch theo số nhịp.
  const [, setTick] = useState(0)
  const nowMs = serverNowMs()
  const opensAtMs = data?.opensAt ? new Date(data.opensAt).getTime() : null
  const closesAtMs = data?.closesAt ? new Date(data.closesAt).getTime() : null

  /** Giây còn lại tới giờ đóng bài — chung cho mọi người trong ca, không tính từ lúc bấm Bắt đầu. */
  const secondsLeft =
    closesAtMs != null ? Math.max(0, Math.ceil((closesAtMs - nowMs) / 1000)) : null

  // Tick mỗi giây khi còn gì để đếm: chờ tới giờ mở, hoặc đang trong khung giờ thi. Mỗi nhịp đọc lại
  // giờ thật thay vì trừ dần một biến đếm — tab bị trình duyệt hãm nhịp khi ẩn cũng không làm đồng hồ
  // chạy chậm hơn giờ đóng.
  const ticking = !!data && data.cvPassed && !result && !data.alreadySubmitted && !data.expired
  useEffect(() => {
    if (!ticking) return
    const id = setInterval(() => setTick((n) => n + 1), 1000)
    return () => clearInterval(id)
  }, [ticking])

  /**
   * Tới giờ mở thì tự tải lại để nhận đề; tới giờ đóng mà chưa bắt đầu thì tải lại để nói "hết hạn".
   * Ứng viên đang chờ trước màn hình không phải tự bấm F5 — và không bao giờ bấm được sớm hơn server.
   */
  const reloadedForRef = useRef<string | null>(null)
  useEffect(() => {
    if (!data || result || started) return
    let key: string | null = null
    // Hỏi lại mỗi 5 giây cho tới khi server mở bài: độ lệch giờ đo được có sai số bằng thời gian
    // truyền mạng, nên lượt hỏi đầu có thể tới sớm hơn server vài trăm mili-giây.
    if (!data.canStart && opensAtMs != null && nowMs >= opensAtMs && secondsLeft !== 0)
      key = `open:${data.opensAt}:${Math.floor((nowMs - opensAtMs) / 5000)}`
    else if (data.canStart && secondsLeft === 0) key = `close:${data.closesAt}`
    if (key && reloadedForRef.current !== key) {
      reloadedForRef.current = key
      void refetch()
    }
  }, [data, result, started, nowMs, opensAtMs, secondsLeft, refetch])

  // Chờ ở màn "chưa tới giờ" mà tab bị ẩn thì setInterval có thể bị hãm tới cả phút — đặt thêm một
  // hẹn giờ đúng lúc mở bài để không trễ.
  useEffect(() => {
    if (!data || data.canStart || opensAtMs == null) return
    const delay = opensAtMs - serverNowMs()
    if (delay <= 0 || delay > MAX_WAKE_DELAY_MS) return
    const id = setTimeout(() => setTick((n) => n + 1), delay + 500)
    return () => clearTimeout(id)
  }, [data, opensAtMs, serverNowMs])

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

  // Tới giờ đóng bài → tự nộp (kể cả khi chưa trả lời hết). Server còn nhận thêm ~1 phút cho đường
  // truyền, nên bài nộp đúng lúc này luôn tới kịp.
  const autoSubmittedRef = useRef(false)
  useEffect(() => {
    if (taking && secondsLeft === 0 && !autoSubmittedRef.current) {
      autoSubmittedRef.current = true
      void submit(true)
    }
  }, [taking, secondsLeft, submit])

  /**
   * Rời khỏi bài thi = TỰ NỘP ngay.
   *
   * Trước đây chỉ đếm số lần rời rồi gửi kèm lúc nộp — một con số để nhân sự nhìn, không ngăn
   * được gì. Nay rời là đóng bài. Luật này được nói rõ ở cửa trước khi bắt đầu — một luật phạt
   * mà người bị phạt không được biết trước thì không phải luật.
   *
   * `pagehide` dùng bản keepalive: đóng tab thì request thường bị huỷ giữa chừng.
   */
  useEffect(() => {
    if (!taking) return

    const registerLeave = () => {
      const now = Date.now()
      // Chuyển tab thường bắn cả blur lẫn visibilitychange — khử trùng 500ms.
      if (now - lastLeaveRef.current < 500) return
      lastLeaveRef.current = now
      tabSwitchRef.current += 1
      setTabSwitches(tabSwitchRef.current)
      void submit(true)
    }
    const onVisibility = () => {
      if (document.hidden) registerLeave()
    }
    const onPageHide = () => {
      if (!applicationId) return
      tabSwitchRef.current += 1
      onlineTestService.submitBeacon(applicationId, answers, tabSwitchRef.current)
    }

    document.addEventListener('visibilitychange', onVisibility)
    window.addEventListener('blur', registerLeave)
    window.addEventListener('pagehide', onPageHide)
    return () => {
      document.removeEventListener('visibilitychange', onVisibility)
      window.removeEventListener('blur', registerLeave)
      window.removeEventListener('pagehide', onPageHide)
    }
  }, [taking, submit, applicationId, answers])

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
        ) : data?.expired ? (
          /* HẾT HẠN — kiểm TRƯỚC "đã nộp": bài hệ thống nộp thay khi hết hạn không phải bài của ứng
             viên, và câu "Bạn đã hoàn thành bài thi này" sẽ khiến họ tưởng mình đã thi. */
          <div className="rounded-2xl border border-red-200 bg-red-50/50 p-10 text-center shadow-sm">
            <span className="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-2xl bg-red-100 text-red-500">
              <XCircle className="h-7 w-7" />
            </span>
            <h2 className="text-base font-bold text-ink-800">{t('page.window.expiredTitle')}</h2>
            <p className="mx-auto mt-1 max-w-md text-sm text-ink-600">
              {data.opensAt && data.closesAt
                ? t('page.window.expiredDetail', {
                    time: fmtWhen(data.opensAt),
                    closes: formatTime24(data.closesAt),
                  })
                : t('page.window.expiredDetailNoTime')}
            </p>
            <p className="mx-auto mt-3 max-w-md text-xs text-ink-500">{t('page.window.expiredContact')}</p>
            <Link
              to="/candidate/applications"
              className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-brand-600 hover:underline"
            >
              <ArrowLeft className="h-4 w-4" /> {t('page.back')}
            </Link>
          </div>
        ) : data?.alreadySubmitted ? (
          <SubmittedCard detail={t('page.alreadyDone')} />
        ) : data && data.canStart === false && data.opensAt ? (
          /* Bài chưa mở / đã đóng — phân biệt CHƯA TỚI GIỜ với ĐÃ QUÁ GIỜ, vì hai tình huống ấy dẫn tới
             hai việc khác hẳn: một bên là quay lại sau, một bên là liên hệ nhân sự. So theo GIỜ SERVER:
             so theo giờ máy thì máy chạy nhanh vài phút sẽ báo "đã đóng" cho một bài chưa mở. */
          <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-sm">
            <span className="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-2xl bg-ink-100 text-ink-400">
              <Clock className="h-7 w-7" />
            </span>
            <h2 className="text-base font-bold text-ink-800">
              {opensAtMs != null && opensAtMs > nowMs
                ? t('page.window.notYetTitle')
                : t('page.window.closedTitle')}
            </h2>
            <p className="mx-auto mt-1 max-w-md text-sm text-ink-500">
              {opensAtMs != null && opensAtMs > nowMs
                ? t('page.window.notYetDetail', {
                    time: fmtWhen(data.opensAt),
                    closes: formatTime24(data.closesAt),
                    minutes: data.durationMinutes,
                  })
                : t('page.window.closedDetail', {
                    time: fmtWhen(data.opensAt),
                    closes: formatTime24(data.closesAt),
                  })}
            </p>
            <Link
              to="/candidate/applications"
              className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-brand-600 hover:underline"
            >
              <ArrowLeft className="h-4 w-4" /> {t('page.back')}
            </Link>
          </div>
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
        ) : !started ? (
          /* CỬA TRƯỚC KHI BẮT ĐẦU.
             Luật "rời bài thi là tự nộp" phải được nói Ở ĐÂY, trước khi vào đề — một luật phạt mà
             người bị phạt chỉ biết sau khi mất bài thì không phải luật. Cửa cũng nói thật về đồng hồ:
             nó đang chạy tới giờ đóng chung của ca (ADR-072), không đợi ứng viên bấm. */
          <div className="rounded-2xl border border-ink-200 bg-white p-8 shadow-sm">
            <div className="text-center">
              <span className="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-2xl bg-brand-50 text-brand-600">
                <ClipboardList className="h-7 w-7" />
              </span>
              <h2 className="text-base font-bold text-ink-800">{t('page.gate.title')}</h2>
              <p className="mt-1 text-sm text-ink-500">
                {t('page.gate.summary', {
                  count: questions.length,
                  minutes: data?.durationMinutes ?? 0,
                  closes: formatTime24(data?.closesAt),
                })}
              </p>
              {secondsLeft != null && (
                <p className="mt-3 inline-flex items-center gap-1.5 rounded-lg bg-brand-50 px-3 py-1.5 text-sm font-semibold text-brand-700">
                  <Clock className="h-4 w-4" /> {t('page.gate.remaining', { time: fmtTime(secondsLeft) })}
                </p>
              )}
            </div>

            {/* Vào muộn: nói thẳng là thời gian đã bị hụt, để ứng viên không tưởng mình còn đủ giờ. */}
            {secondsLeft != null &&
              data?.durationMinutes != null &&
              secondsLeft < data.durationMinutes * 60 - 60 && (
                <p className="mx-auto mt-4 flex max-w-md items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>
                    {t('page.gate.lateNotice', {
                      minutes: data.durationMinutes,
                      closes: formatTime24(data.closesAt),
                    })}
                  </span>
                </p>
              )}

            <ul className="mx-auto mt-5 max-w-md space-y-2 text-sm text-ink-700">
              <li className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-red-700">
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>
                  <b>{t('page.gate.leaveRuleTitle')}</b> {t('page.gate.leaveRule')}
                </span>
              </li>
              <li className="flex items-start gap-2 rounded-xl border border-ink-200 px-3 py-2">
                <Clock className="mt-0.5 h-4 w-4 shrink-0 text-ink-400" />
                <span>{t('page.gate.timerRule', { closes: formatTime24(data?.closesAt) })}</span>
              </li>
              <li className="flex items-start gap-2 rounded-xl border border-ink-200 px-3 py-2">
                <Lock className="mt-0.5 h-4 w-4 shrink-0 text-ink-400" />
                <span>{t('page.gate.oneAttemptRule')}</span>
              </li>
            </ul>

            <button
              onClick={() => setStarted(true)}
              className="mx-auto mt-6 flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
            >
              <ClipboardList className="h-4 w-4" /> {t('page.gate.startButton')}
            </button>
            <p className="mt-2 text-center text-xs text-ink-400">{t('page.gate.startHint')}</p>
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
              {secondsLeft !== null && (
                <span
                  className={`inline-flex items-center gap-1 rounded-lg px-2 py-1 font-semibold ${
                    secondsLeft <= 60 ? 'bg-red-50 text-red-600' : 'bg-brand-50 text-brand-700'
                  }`}
                  title={t('page.closesAt', { closes: formatTime24(data?.closesAt) })}
                >
                  <Clock className="h-4 w-4" /> {t('page.timeLeft', { time: fmtTime(secondsLeft) })}
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
