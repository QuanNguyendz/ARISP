import { useCallback, useEffect, useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, CalendarClock, Check, Loader2, Trash2 } from 'lucide-react'
import { scheduleService, type HmAvailabilityWindow } from '@ari/shared/fservices/schedule'
import { isOnlineTestRound } from '@ari/shared/utils/roundTypes'
import { formatTime24 } from '@ari/shared/utils/time24'
import HmAvailabilityFields, {
  hasBlockingIssue,
  toLocalInput,
  toPayload,
  type DraftWindow,
} from './HmAvailabilityFields'

/** `id` của thẻ này — hộp thoại duyệt hồ sơ cuộn tới đây khi HM chưa khai lịch vòng 1. */
export const HM_AVAILABILITY_ANCHOR = 'hm-availability'

/**
 * Nơi DUY NHẤT Hiring Manager khai và sửa lịch mình có mặt được, theo từng vòng (ADR-067, sửa
 * 2026-09-14).
 *
 * <b>Vì sao lịch nằm ở đây chứ không đi kèm lệnh duyệt hồ sơ.</b> Trước đó HM phải gửi lịch vòng 1
 * mỗi lần bấm duyệt, và đường đó chỉ biết THÊM: khai nhầm hay có việc đột xuất thì không sửa được ở
 * đó, vòng 2–3 phải sang chỗ khác khai, và hai nơi cùng ghi một dữ liệu. Lịch là của NGƯỜI cho cả đợt,
 * không phải của từng hồ sơ — nên duyệt chỉ còn là quyết định chuyên môn, còn lịch khai ở đúng một nơi.
 *
 * <b>Hai loại khung, hai cách xử lý — cố ý.</b> Khung CHƯA BẮT ĐẦU sửa thoải mái theo lối "khai lại
 * cả danh sách": đó là câu trả lời cho "sắp tới tôi rảnh những lúc nào", mà câu trả lời đó thay đổi
 * nguyên khối. Khung ĐANG DIỄN RA không gửi lại được (luật đòi giờ bắt đầu ở tương lai) nên lệnh khai
 * lại cố ý không đụng tới nó; muốn rút nó thì bấm nút xoá riêng — một hành động có chủ đích, không
 * phải tác dụng phụ của việc sửa dòng khác.
 *
 * <b>Việc quan trọng nhất màn này làm: NÓI RA hậu quả.</b> Luật "ca phải nằm trọn trong khung HM
 * rảnh" chỉ chạy lúc GÁN ca. Nên sửa lịch xong, những buổi đã hẹn trước đó không bị chặn ở đâu cả —
 * chúng lặng lẽ thành buổi mà người bắt buộc phải dự đã báo là không dự được. Server đếm và trả về
 * con số đó; màn này đưa nó lên ngay tại chỗ vừa bấm, kèm câu nói rõ ai phải xử lý.
 */
interface Props {
  jobPostingId: string
  rounds: { roundNumber: number; roundType?: string | null }[]
  /** Không phải HM của tin (hoặc quản trị viên) thì chỉ xem. */
  canEdit: boolean
}

const CARD = 'rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5'

function fmtWindow(w: HmAvailabilityWindow): string {
  const s = new Date(w.startTime)
  const e = new Date(w.endTime)
  const day = s.toLocaleDateString(undefined, { weekday: 'short', day: '2-digit', month: '2-digit' })
  return `${day} · ${formatTime24(s)} – ${formatTime24(e)}`
}

export default function HmAvailabilityPanel({ jobPostingId, rounds, canEdit }: Props) {
  const { t } = useTranslation('modules/hm/jobs')
  const queryClient = useQueryClient()

  /**
   * Nơi khác đọc cùng dữ liệu qua react-query (hộp thoại duyệt hồ sơ, màn cấu hình lịch của
   * Recruiter nếu mở cùng trình duyệt) — lưu xong mà không báo thì chúng hiện lịch cũ.
   */
  const syncOtherViews = () =>
    queryClient.invalidateQueries({ queryKey: ['hm-availability', jobPostingId] })

  /**
   * Vòng TRẮC NGHIỆM không có mặt trong danh sách: ứng viên làm bài trực tuyến, không ai ngồi cùng.
   * Hỏi HM "vòng trắc nghiệm anh rảnh lúc nào" là hỏi một câu không có tác dụng gì.
   */
  const hmRounds = useMemo(
    () =>
      [...rounds]
        .filter((r) => !isOnlineTestRound(r.roundType))
        .sort((a, b) => a.roundNumber - b.roundNumber),
    [rounds]
  )

  /** Vòng bị ẩn vì là trắc nghiệm — nói ra, nếu không HM thấy danh sách bắt đầu từ "Vòng 2" mà không hiểu vì sao. */
  const testRounds = useMemo(
    () =>
      rounds
        .filter((r) => isOnlineTestRound(r.roundType))
        .map((r) => r.roundNumber)
        .sort((a, b) => a - b),
    [rounds]
  )

  const [round, setRound] = useState<number | null>(null)
  const [windows, setWindows] = useState<HmAvailabilityWindow[]>([])
  const [rows, setRows] = useState<DraftWindow[]>([])
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState<{ windowCount: number; affectedBookings: number } | null>(null)

  useEffect(() => {
    if (round == null && hmRounds.length > 0) setRound(hmRounds[0].roundNumber)
  }, [hmRounds, round])

  const load = useCallback(
    async (r: number) => {
      setLoading(true)
      setError('')
      try {
        const data = await scheduleService.getHmAvailability(jobPostingId, r)
        setWindows(data)
        // Chỉ nạp khung CHƯA BẮT ĐẦU vào ô sửa: khung đang chạy gửi lại sẽ bị luật "giờ bắt đầu
        // phải ở tương lai" từ chối, và người dùng không hiểu vì sao một khung đang hợp lệ lại đỏ.
        const now = Date.now()
        setRows(
          data
            .filter((w) => new Date(w.startTime).getTime() > now)
            .map((w) => ({
              start: toLocalInput(w.startTime),
              end: toLocalInput(w.endTime),
              note: w.note || '',
            }))
        )
      } catch {
        setError(t('availability.loadError'))
      } finally {
        setLoading(false)
      }
    },
    [jobPostingId, t]
  )

  useEffect(() => {
    if (round != null) void load(round)
  }, [round, load])

  const running = useMemo(() => {
    const now = Date.now()
    return windows.filter((w) => new Date(w.startTime).getTime() <= now)
  }, [windows])

  const save = async () => {
    if (round == null) return
    setBusy(true)
    setError('')
    setSaved(null)
    try {
      const res = await scheduleService.setHmAvailability(jobPostingId, round, toPayload(rows))
      setSaved(res)
      syncOtherViews()
      await load(round)
    } catch (e) {
      const x = e as { response?: { data?: { message?: string } } }
      setError(x?.response?.data?.message || t('availability.saveError'))
    } finally {
      setBusy(false)
    }
  }

  const drop = async (w: HmAvailabilityWindow) => {
    if (round == null) return
    if (!window.confirm(t('availability.confirmDrop', { window: fmtWindow(w) }))) return
    setBusy(true)
    setError('')
    setSaved(null)
    try {
      const res = await scheduleService.deleteHmAvailability(w.id)
      setSaved(res)
      syncOtherViews()
      await load(round)
    } catch (e) {
      const x = e as { response?: { data?: { message?: string } } }
      setError(x?.response?.data?.message || t('availability.dropError'))
    } finally {
      setBusy(false)
    }
  }

  if (hmRounds.length === 0) return null

  return (
    <section id={HM_AVAILABILITY_ANCHOR} className={`${CARD} scroll-mt-24`}>
      <h2 className="mb-1 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
        <CalendarClock className="h-4 w-4 text-brand-600 dark:text-brand-400" />
        {t('availability.title')}
      </h2>
      <p className="mb-3 text-xs text-ink-500 dark:text-ink-400">{t('availability.subtitle')}</p>
      {testRounds.length > 0 && (
        <p className="-mt-1.5 mb-3 text-xs text-ink-500 dark:text-ink-400">
          {t('availability.testRoundsNote', {
            rounds: testRounds.join(', '),
            count: testRounds.length,
          })}
        </p>
      )}

      {hmRounds.length > 1 && (
        <div className="mb-3 flex flex-wrap gap-1.5">
          {hmRounds.map((r) => (
            <button
              key={r.roundNumber}
              type="button"
              onClick={() => {
                setRound(r.roundNumber)
                setSaved(null)
              }}
              className={`rounded-lg px-2.5 py-1 text-xs font-medium transition-colors ${
                round === r.roundNumber
                  ? 'bg-brand-600 text-white'
                  : 'border border-ink-200 text-ink-600 hover:bg-ink-50 dark:border-white/10 dark:text-ink-300 dark:hover:bg-white/10'
              }`}
            >
              {t('availability.round', { number: r.roundNumber })}
            </button>
          ))}
        </div>
      )}

      {loading ? (
        <div className="flex items-center gap-2 py-4 text-sm text-ink-500">
          <Loader2 className="h-4 w-4 animate-spin" /> {t('availability.loading')}
        </div>
      ) : (
        <div className="space-y-3">
          {/* Khung ĐANG DIỄN RA: chỉ đọc, nhưng rút được. Đây chính là ô cho tình huống "đang trong
              giờ rảnh thì có việc đột xuất". */}
          {running.length > 0 && (
            <div className="rounded-xl border border-ink-100 bg-ink-50 p-3 dark:border-white/5 dark:bg-white/5">
              <p className="mb-1.5 text-xs font-semibold text-ink-700 dark:text-ink-200">
                {t('availability.runningTitle')}
              </p>
              <ul className="space-y-1.5">
                {running.map((w) => (
                  <li key={w.id} className="flex items-center justify-between gap-2">
                    <span className="truncate text-xs text-ink-600 dark:text-ink-300">
                      {fmtWindow(w)}
                      {w.note ? ` · ${w.note}` : ''}
                    </span>
                    {canEdit && (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => void drop(w)}
                        className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-red-200 px-2 py-1 text-[11px] font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-500/30 dark:text-red-400 dark:hover:bg-red-500/10"
                      >
                        <Trash2 className="h-3 w-3" /> {t('availability.drop')}
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {canEdit ? (
            <>
              {/* Vòng chưa có khung nào (cả đang chạy lẫn sắp tới) nghĩa là Recruiter KHÔNG xếp được
                  ca nào cho vòng này. Duyệt hồ sơ không còn buộc khai lịch, nên đây là chỗ HM thấy
                  mình đang là người hồ sơ phải chờ. */}
              {windows.length === 0 && (
                <p className="flex items-start gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/25 dark:bg-amber-500/10 dark:text-amber-300">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <span>{t('availability.emptyEditable', { number: round })}</span>
                </p>
              )}

              <HmAvailabilityFields rows={rows} onChange={setRows} disabled={busy} />

              {error && (
                <p className="flex items-start gap-1.5 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <span>{error}</span>
                </p>
              )}

              {saved && (
                <div className="space-y-2">
                  <p className="flex items-start gap-1.5 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-400">
                    <Check className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                    <span>{t('availability.saved', { count: saved.windowCount })}</span>
                  </p>

                  {/* Con số này là lý do cả màn tồn tại: luật khớp giờ chỉ chạy lúc gán ca, nên
                      những buổi đã hẹn KHÔNG bị chặn ở đâu khi lịch đổi. */}
                  {saved.affectedBookings > 0 && (
                    <p className="flex items-start gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/25 dark:bg-amber-500/10 dark:text-amber-300">
                      <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                      <span>{t('availability.affected', { count: saved.affectedBookings })}</span>
                    </p>
                  )}
                </div>
              )}

              <button
                type="button"
                onClick={() => void save()}
                disabled={busy || hasBlockingIssue(rows)}
                className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
              >
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
                {t('availability.save')}
              </button>

              <p className="text-xs text-ink-400">{t('availability.replaceHint')}</p>
            </>
          ) : windows.length === 0 ? (
            <p className="text-xs text-ink-500 dark:text-ink-400">{t('availability.empty')}</p>
          ) : (
            <ul className="space-y-1">
              {windows.map((w) => (
                <li key={w.id} className="text-xs text-ink-600 dark:text-ink-300">
                  {fmtWindow(w)}
                  {w.note ? ` · ${w.note}` : ''}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </section>
  )
}
