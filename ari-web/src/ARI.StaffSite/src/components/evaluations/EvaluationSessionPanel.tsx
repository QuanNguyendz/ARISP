import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CalendarClock, Clock, PlayCircle, ScrollText, ChevronDown } from 'lucide-react'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import { formatDateTime24, formatTime24 } from '@ari/shared/utils/time24'
import type { EvaluationReport } from '@ari/shared/types/evaluation'

/** Transcript dài hơn ngần này thì mở sẵn vài lượt đầu, còn lại bấm "Xem toàn bộ". */
const PREVIEW_TURNS = 4

/**
 * Buổi phỏng vấn mà báo cáo đang chấm (ADR-069): ca đã gán · lúc bắt đầu / kết thúc · video · transcript đầy
 * đủ theo thứ tự.
 *
 * Trước đây khối này chỉ có video, mà tiêu đề lại ghi "Bản ghi buổi phỏng vấn": transcript không có ở đâu, và
 * phần "phân tích theo câu" chỉ gồm những câu AI chấm — câu ứng viên bỏ trống biến mất, người duyệt không đối
 * chiếu được nhận xét của AI với chính lời ứng viên. Dùng chung cho màn đánh giá của HR / HM / Recruiter.
 */
export default function EvaluationSessionPanel({ evaluation }: { evaluation: EvaluationReport }) {
  const { t } = useTranslation('modules/hr/evaluations')
  const [showAll, setShowAll] = useState(false)

  const turns = evaluation.transcript ?? []
  const visible = showAll ? turns : turns.slice(0, PREVIEW_TURNS)
  const minutes = evaluation.durationSeconds ? Math.max(1, Math.round(evaluation.durationSeconds / 60)) : null

  return (
    <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card dark:border-white/10 dark:bg-white/5 sm:p-6">
      <h2 className="mb-3 font-display text-lg font-bold text-ink-900 dark:text-white">
        {t('recordingTranscript')}
      </h2>

      {/* Buổi nào, lúc nào, bao lâu — đọc báo cáo tách khỏi buổi thì không biết đang xem ca nào. */}
      <dl className="mb-4 flex flex-wrap gap-x-5 gap-y-1 text-sm text-ink-600 dark:text-ink-300">
        <div className="inline-flex items-center gap-1.5">
          <CalendarClock className="h-4 w-4 text-brand-600 dark:text-brand-400" />
          <dt className="sr-only">{t('session.slot')}</dt>
          <dd>
            {evaluation.slotStartTime
              ? t('session.slotValue', {
                  date: formatDateTime24(evaluation.slotStartTime).split(' ')[0],
                  from: formatTime24(evaluation.slotStartTime),
                  to: formatTime24(evaluation.slotEndTime),
                })
              : t('session.noSlot')}
          </dd>
        </div>
        {evaluation.sessionStartedAt && (
          <div className="inline-flex items-center gap-1.5">
            <PlayCircle className="h-4 w-4 text-brand-600 dark:text-brand-400" />
            <dt className="sr-only">{t('session.started')}</dt>
            <dd>
              {t('session.startedValue', {
                from: formatTime24(evaluation.sessionStartedAt),
                to: evaluation.sessionEndedAt ? formatTime24(evaluation.sessionEndedAt) : '—',
              })}
            </dd>
          </div>
        )}
        {minutes != null && (
          <div className="inline-flex items-center gap-1.5">
            <Clock className="h-4 w-4 text-brand-600 dark:text-brand-400" />
            <dt className="sr-only">{t('session.duration')}</dt>
            <dd>{t('session.durationValue', { minutes })}</dd>
          </div>
        )}
      </dl>

      {/* Bản ghi hình buổi phỏng vấn thật (ADR-052) — tự xoá khi hết hạn lưu. */}
      {evaluation.recordingUrl ? (
        <>
          <video
            src={resolveAssetUrl(evaluation.recordingUrl)}
            controls
            className="aspect-video w-full rounded-xl bg-ink-900"
          />
          {evaluation.recordingExpiresAt && (
            <p className="mt-2 text-xs text-ink-500 dark:text-ink-400">
              {t('recordingExpiresAt', { date: formatDateTime24(evaluation.recordingExpiresAt) })}
            </p>
          )}
        </>
      ) : (
        <div className="grid aspect-video place-items-center rounded-xl bg-ink-100 px-6 text-center text-sm text-ink-500 dark:bg-white/5 dark:text-ink-400">
          {evaluation.recordingDeletedAt
            ? t('recordingDeleted', { date: formatDateTime24(evaluation.recordingDeletedAt).split(' ')[0] })
            : t('recordingNone')}
        </div>
      )}

      {/* Transcript đầy đủ — lấy từ DB, không phải bản AI chép lại; có cả câu ứng viên bỏ trống. */}
      <div className="mt-6">
        <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
          <ScrollText className="h-4 w-4 text-brand-600 dark:text-brand-400" />
          {t('session.transcriptTitle', { count: turns.length })}
        </h3>
        {turns.length === 0 ? (
          <p className="text-sm text-ink-500 dark:text-ink-400">{t('session.transcriptEmpty')}</p>
        ) : (
          <ol className="space-y-3">
            {visible.map((turn) => (
              <li key={turn.sequenceNumber} className="rounded-xl border border-ink-100 p-3 dark:border-white/10">
                <div className="mb-1 flex items-center justify-between gap-2 text-xs text-ink-400">
                  <span className="font-semibold uppercase tracking-wide">
                    {t('session.aiAsked', { number: turn.sequenceNumber })}
                  </span>
                  <span>{formatTime24(turn.askedAt)}</span>
                </div>
                <p className="text-sm font-medium text-ink-800 dark:text-ink-100">{turn.question}</p>
                <div className="mt-2 border-l-2 border-brand-200 pl-3 dark:border-brand-500/40">
                  <div className="mb-0.5 flex items-center justify-between gap-2 text-xs text-ink-400">
                    <span className="font-semibold uppercase tracking-wide">{t('session.candidateSaid')}</span>
                    {turn.answeredAt && <span>{formatTime24(turn.answeredAt)}</span>}
                  </div>
                  {turn.answer ? (
                    <p className="whitespace-pre-wrap text-sm text-ink-700 dark:text-ink-300">{turn.answer}</p>
                  ) : (
                    <p className="text-sm italic text-ink-400">{t('noAnswer')}</p>
                  )}
                </div>
              </li>
            ))}
          </ol>
        )}
        {turns.length > PREVIEW_TURNS && (
          <button
            type="button"
            onClick={() => setShowAll((v) => !v)}
            className="mt-3 inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:underline dark:text-brand-400"
          >
            <ChevronDown className={`h-4 w-4 transition ${showAll ? 'rotate-180' : ''}`} />
            {showAll ? t('session.collapse') : t('session.showAll', { count: turns.length })}
          </button>
        )}
      </div>
    </div>
  )
}
