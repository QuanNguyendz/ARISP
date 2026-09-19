import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  ClipboardCheck,
  CalendarClock,
  Video,
  VideoOff,
  ScrollText,
  Loader2,
  ArrowRight,
  Clock,
  RotateCcw,
} from 'lucide-react'
import { roundTypeKey } from '@ari/shared/utils/roundTypes'
import { formatDateTime24, formatTime24 } from '@ari/shared/utils/time24'
import { evaluationService } from '@/fservices/evaluation/evaluationService'
import type { InterviewResultRow, InterviewResultState } from '@ari/shared/types/evaluation'
import { INTERVIEW_RESULTS_NS, interviewResultsKey } from './interviewResultsConfig'

/** Buổi còn đang chạy tới báo cáo — tự hỏi lại để "AI đang chấm" thành "Xem đánh giá" mà không cần F5. */
const IN_FLIGHT: InterviewResultState[] = ['waiting', 'in_progress', 'evaluating']

const STATE_TONE: Record<InterviewResultState, string> = {
  scheduled: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300',
  overdue: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300',
  waiting: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300',
  in_progress: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300',
  evaluating: 'bg-ai-100 text-ai-700 dark:bg-ai-500/20 dark:text-ai-300',
  needs_rubric: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300',
  evaluation_failed: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-300',
  pending_review: 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300',
  reviewed: 'bg-brand-100 text-brand-700 dark:bg-brand-500/20 dark:text-brand-300',
  aborted: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-300',
}

interface InterviewResultsCardProps {
  applicationId: string
  /** Đường tới màn đánh giá của khu vực đang đứng (`/hr|/recruiter|/hm/evaluations?id=`). */
  evaluationHref: (evaluationId: string) => string
  /** Bỏ khung thẻ — khi nhúng vào một khối đã có khung (khối ứng viên của màn tin). */
  bare?: boolean
  /** Không có buổi nào thì không vẽ gì (thay vì dòng "chưa có") — dùng ở chỗ chật. */
  hideWhenEmpty?: boolean
}

/**
 * Các buổi phỏng vấn THẬT của một hồ sơ, theo vòng (ADR-069): ca đã gán, diễn biến, báo cáo AI, có video /
 * transcript hay không — và nút tới đúng báo cáo.
 *
 * Sinh ra vì sau buổi phỏng vấn không có chỗ nào dẫn tới báo cáo: khối ứng viên ở màn tin chỉ có CV, còn
 * danh sách đánh giá im lặng trong lúc AI đang chấm (người dùng tưởng buổi đó không để lại gì). Một component
 * cho cả ba vai và cả hai chỗ (màn tin, màn hồ sơ) — khác nhau duy nhất ở đường tới màn đánh giá.
 */
export default function InterviewResultsCard({
  applicationId,
  evaluationHref,
  bare,
  hideWhenEmpty,
}: InterviewResultsCardProps) {
  const { t } = useTranslation(INTERVIEW_RESULTS_NS)

  const { data: rows = [], isLoading, error } = useQuery({
    queryKey: interviewResultsKey(applicationId),
    queryFn: () => evaluationService.getApplicationInterviews(applicationId),
    enabled: !!applicationId,
    refetchInterval: (q) =>
      (q.state.data ?? []).some((r) => IN_FLIGHT.includes(r.state)) ? 15_000 : false,
  })

  if (hideWhenEmpty && !isLoading && rows.length === 0) return null

  const body = (
    <>
      <h2 className="mb-1 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
        <ClipboardCheck className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('title')}
      </h2>
      <p className="mb-3 text-xs text-ink-500 dark:text-ink-400">{t('description')}</p>

      {isLoading ? (
        <p className="flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
          <Loader2 className="h-4 w-4 animate-spin" /> {t('loading')}
        </p>
      ) : error ? (
        <p className="text-sm text-red-600 dark:text-red-400">{t('loadError')}</p>
      ) : rows.length === 0 ? (
        <p className="text-sm text-ink-500 dark:text-ink-400">{t('empty')}</p>
      ) : (
        <ul className="space-y-2">
          {rows.map((r) => (
            <ResultRow key={`${r.roundNumber}-${r.sessionId ?? 'slot'}`} row={r} evaluationHref={evaluationHref} />
          ))}
        </ul>
      )}
    </>
  )

  return bare ? (
    <div>{body}</div>
  ) : (
    <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5">
      {body}
    </section>
  )
}

export function ResultRow({
  row,
  evaluationHref,
  showCandidate,
}: {
  row: InterviewResultRow
  evaluationHref: (evaluationId: string) => string
  /** Hiện tên ứng viên + tin — khi danh sách trộn nhiều hồ sơ (màn Phòng phỏng vấn). */
  showCandidate?: boolean
}) {
  const { t } = useTranslation(INTERVIEW_RESULTS_NS)
  const verdict = row.finalVerdict ?? row.aiVerdict
  const minutes = row.durationSeconds ? Math.max(1, Math.round(row.durationSeconds / 60)) : null

  return (
    <li className="rounded-xl border border-ink-100 p-3 dark:border-white/10">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          {showCandidate && (
            <p className="truncate text-sm font-semibold text-ink-900 dark:text-white">
              {row.candidateName ?? '—'}
              <span className="font-normal text-ink-500 dark:text-ink-400"> · {row.jobTitle ?? '—'}</span>
            </p>
          )}
          <p className="text-sm font-medium text-ink-800 dark:text-ink-100">
            {t('round', { number: row.roundNumber })} · {t(`roundTypes.${roundTypeKey(row.roundType)}`)}
          </p>
          <p className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-ink-500 dark:text-ink-400">
            <span className="inline-flex items-center gap-1">
              <CalendarClock className="h-3.5 w-3.5" />
              {row.slotStartTime
                ? t('slot', {
                    date: formatDateTime24(row.slotStartTime).split(' ')[0],
                    from: formatTime24(row.slotStartTime),
                    to: formatTime24(row.slotEndTime),
                  })
                : t('noSlot')}
            </span>
            {minutes != null && (
              <span className="inline-flex items-center gap-1">
                <Clock className="h-3.5 w-3.5" /> {t('duration', { minutes })}
              </span>
            )}
          </p>
        </div>
        <span className={`whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATE_TONE[row.state]}`}>
          {row.state === 'evaluating' && <Loader2 className="mr-1 inline h-3 w-3 animate-spin" />}
          {t(`states.${row.state}`)}
        </span>
      </div>

      {row.sessionId && (
        <div className="mt-2 flex flex-wrap items-center gap-2 text-xs">
          {row.evaluationId && verdict && (
            <span
              className={`rounded-full px-2 py-0.5 font-semibold ${
                verdict === 'pass'
                  ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300'
                  : 'bg-red-50 text-red-700 dark:bg-red-500/10 dark:text-red-300'
              }`}
            >
              {row.finalVerdict ? t(`verdict.${verdict}`, { defaultValue: verdict }) : t('aiVerdict', {
                verdict: t(`verdict.${verdict}`, { defaultValue: verdict }),
              })}
              {row.overallScore != null && ` · ${t('score', { score: row.overallScore })}`}
            </span>
          )}
          {/* Có gì để xem — nói trước, để người dùng không mở báo cáo ra rồi mới biết video đã bị xoá. */}
          <span className="inline-flex items-center gap-1 text-ink-500 dark:text-ink-400">
            {row.hasRecording ? (
              <>
                <Video className="h-3.5 w-3.5 text-brand-600 dark:text-brand-400" /> {t('video')}
              </>
            ) : (
              <>
                <VideoOff className="h-3.5 w-3.5" />
                {row.recordingDeletedAt ? t('videoDeleted') : t('noVideo')}
              </>
            )}
          </span>
          <span className="inline-flex items-center gap-1 text-ink-500 dark:text-ink-400">
            <ScrollText className="h-3.5 w-3.5" />
            {row.transcriptTurns > 0 ? t('transcript', { count: row.transcriptTurns }) : t('noTranscript')}
          </span>
        </div>
      )}

      {row.evaluationId ? (
        <Link
          to={evaluationHref(row.evaluationId)}
          className="mt-3 inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700"
        >
          {t('viewReport')} <ArrowRight className="h-4 w-4" />
        </Link>
      ) : row.state === 'evaluating' ? (
        <p className="mt-2 text-xs text-ink-500 dark:text-ink-400">{t('evaluatingHint')}</p>
      ) : row.state === 'needs_rubric' ? (
        <p className="mt-2 text-xs text-amber-700 dark:text-amber-300">{t('needsRubricHint')}</p>
      ) : row.state === 'evaluation_failed' && row.sessionId ? (
        <RetryScoring sessionId={row.sessionId} reason={row.evaluationError} />
      ) : row.state === 'aborted' ? (
        <p className="mt-2 text-xs text-ink-500 dark:text-ink-400">{t('abortedHint')}</p>
      ) : null}
    </li>
  )
}

/**
 * AI hỏng hết số lượt thử tự động (ADR-073) — nhân sự bấm để đưa buổi này vào hàng chấm lần nữa. Quyền do server
 * quyết (chủ tin, HM chính, quản trị viên); việc chấm chạy nền và realtime làm mới khối này khi xong.
 */
function RetryScoring({ sessionId, reason }: { sessionId: string; reason?: string | null }) {
  const { t } = useTranslation(INTERVIEW_RESULTS_NS)
  const queryClient = useQueryClient()
  const [message, setMessage] = useState<string | null>(null)

  const retry = useMutation({
    mutationFn: () => evaluationService.retrySessionEvaluation(sessionId),
    onSuccess: () => {
      setMessage(t('retryQueued'))
      queryClient.invalidateQueries({ queryKey: ['evaluations'] })
    },
    onError: (e) =>
      setMessage((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('retryError')),
  })

  return (
    <div className="mt-2 space-y-1.5">
      <p className="text-xs text-red-700 dark:text-red-300">
        {t('failedHint', { reason: reason ? ` (${reason})` : '' })}
      </p>
      <button
        type="button"
        onClick={() => retry.mutate()}
        disabled={retry.isPending || retry.isSuccess}
        className="inline-flex items-center gap-1.5 rounded-xl border border-ink-200 bg-white px-3 py-1.5 text-xs font-semibold text-ink-700 hover:bg-ink-50 disabled:opacity-50 dark:border-white/10 dark:bg-white/5 dark:text-ink-200 dark:hover:bg-white/10"
      >
        {retry.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RotateCcw className="h-3.5 w-3.5" />}
        {retry.isPending ? t('retrying') : t('retry')}
      </button>
      {message && <p className="text-xs text-ink-500 dark:text-ink-400">{message}</p>}
    </div>
  )
}
