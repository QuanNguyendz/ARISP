import { Plus, Trash2, AlertCircle } from 'lucide-react'
import { DateTimeInput } from '@ari/shared/ui'
import {
  isCompleteLocalDateTime,
  joinLocalDateTime,
  splitLocalDateTime,
} from '@ari/shared/utils/time24'

/**
 * Một khung giờ đang soạn. `start`/`end` giữ khuôn `YYYY-MM-DDTHH:mm` và được phép DỞ (`2026-09-15T`)
 * trong lúc nhập — ô ngày và ô giờ là hai ô riêng (giờ 24 tiếng, xem `DateTimeInput`).
 */
export interface DraftWindow {
  start: string
  end: string
  note: string
}

/**
 * Trần độ dài một khung. Khung dài hơn một ngày làm việc thì ràng buộc "ca phải nằm trong giờ HM
 * rảnh" mất hết ý nghĩa — khai "rảnh từ 10/09 đến 25/09" nghĩa là Recruiter xếp được cả ca 3 giờ
 * sáng. Rảnh nhiều ngày thì khai nhiều khung, mỗi ngày một khung.
 */
export const MAX_WINDOW_HOURS = 12

/**
 * Ô nhập khung giờ Hiring Manager có mặt được (ADR-067).
 *
 * <b>Vì sao gửi MỐC THỜI GIAN chứ không phải chuỗi trần.</b> `datetime-local` trả `"2026-09-12T14:00"`
 * không kèm múi giờ; để nguyên thì ASP.NET hiểu theo giờ LOCAL CỦA MÁY CHỦ, mà dev chạy UTC+7 còn
 * container production chạy UTC — cùng một ô nhập ra hai mốc khác nhau. Đây đúng là lỗi 500 đã gặp ở
 * ADR-065 (`expected_start_date`); `new Date(...).toISOString()` cắt đứt hẳn phụ thuộc đó.
 */
export function toInstant(local: string): string | null {
  if (!isCompleteLocalDateTime(local)) return null
  const d = new Date(local)
  return Number.isNaN(d.getTime()) ? null : d.toISOString()
}

/** Chuyển mốc ISO về dạng `datetime-local` để nạp lại khung đã khai. */
export function toLocalInput(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** Mốc "bây giờ" ở dạng `YYYY-MM-DDTHH:mm`. */
export const nowLocalInput = () => toLocalInput(new Date().toISOString())

export type WindowIssue =
  | 'empty'
  | 'incomplete'
  | 'past'
  | 'endBeforeStart'
  | 'tooLong'
  | 'overlap'
  | 'overlapOther'
  | null

/**
 * Khoảng thời gian Hiring Manager ĐÃ khai ở nơi khác (vòng khác của cùng tin) — không sửa được từ
 * màn này, nhưng khung mới không được đè lên nó.
 *
 * `label` là thứ hiện ra cho người dùng ("Vòng 2"), vì "chồng lấn" mà không nói chồng với cái gì thì
 * người ta phải mở từng tab đi dò.
 */
export interface BusyWindow {
  start: number
  end: number
  label: string
}

/**
 * Lỗi của MỘT dòng, tính bằng ĐÚNG các luật mà server áp (`HmAvailabilitySupport.Sanitize`).
 *
 * Vì sao kiểm ở client dù server đã kiểm: server trả 400 kèm thông báo, nhưng thông báo đó hiện ở
 * cấp TRANG — nằm dưới lớp phủ của hộp thoại, người dùng không nhìn thấy. Bấm "Duyệt" rồi không có
 * gì xảy ra là kiểu hỏng tệ nhất. Client chặn trước và nói ngay tại dòng sai; server vẫn là chốt
 * chặn thật.
 */
export function issueOf(r: DraftWindow): WindowIssue {
  if (!r.start && !r.end) return 'empty'
  // Mới chọn ngày mà chưa chọn giờ (hoặc ngược lại) cũng là "chưa đủ" — không phải lỗi giờ.
  if (!isCompleteLocalDateTime(r.start) || !isCompleteLocalDateTime(r.end)) return 'incomplete'

  const start = new Date(r.start)
  const end = new Date(r.end)
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return 'incomplete'

  // Giờ BẮT ĐẦU phải ở tương lai: một khung đã trôi qua thì không xếp được ca nào vào đó nữa.
  if (start.getTime() <= Date.now()) return 'past'
  if (end.getTime() <= start.getTime()) return 'endBeforeStart'
  if (end.getTime() - start.getTime() > MAX_WINDOW_HOURS * 3600_000) return 'tooLong'
  return null
}

const ISSUE_TEXT: Record<Exclude<WindowIssue, null | 'empty'>, string> = {
  incomplete: 'Cần chọn đủ ngày và giờ cho cả lúc bắt đầu lẫn lúc kết thúc.',
  past: 'Giờ bắt đầu phải ở tương lai.',
  endBeforeStart: 'Giờ kết thúc phải sau giờ bắt đầu.',
  tooLong: `Mỗi khung tối đa ${MAX_WINDOW_HOURS} tiếng — rảnh nhiều ngày thì tách thành nhiều khung.`,
  overlap: 'Khung này chồng lên một khung khác. Hãy gộp thành một khung hoặc tách rời nhau.',
  overlapOther:
    'Khung này chồng lên lịch rảnh đã khai ở vòng khác. Cùng một khoảng thời gian không thể vừa dành cho vòng này vừa dành cho vòng kia.',
}

/**
 * Lỗi của TẤT CẢ các dòng — `issueOf` chỉ nhìn được một dòng, mà "chồng lấn" là quan hệ giữa hai dòng.
 *
 * Gương của `HmAvailabilitySupport.Sanitize` phía server: dòng nào tự nó đã sai thì giữ nguyên lỗi
 * của nó (chưa biết giờ thật thì chưa so được với ai), dòng nào hợp lệ mới đem so với các dòng hợp
 * lệ khác. Chạm đầu–cuối (10:00–11:00 rồi 11:00–12:00) KHÔNG tính là chồng lấn — đó là một buổi rảnh
 * liên tục viết thành hai dòng.
 */
export function issuesOf(rows: DraftWindow[], busy: BusyWindow[] = []): WindowIssue[] {
  const own = rows.map(issueOf)
  const span = rows.map((r, i) =>
    own[i] === null ? { start: new Date(r.start).getTime(), end: new Date(r.end).getTime() } : null
  )

  return own.map((issue, i) => {
    if (issue !== null) return issue
    const a = span[i]!
    const clash = span.some((b, j) => j !== i && b !== null && a.start < b.end && b.start < a.end)
    if (clash) return 'overlap'
    // Lịch rảnh là lịch của MỘT NGƯỜI: khung ở vòng khác cũng chiếm chỗ y như khung trong danh sách
    // này. Trước đây phép so dừng lại ở phạm vi một vòng, nên vòng 2 và vòng 3 khai trùng giờ vẫn lọt.
    return busy.some((b) => a.start < b.end && b.start < a.end) ? 'overlapOther' : null
  })
}

/** Khung ở nơi khác mà dòng này đang đè lên — để nói rõ "chồng với VÒNG NÀO". */
export function busyClashOf(row: DraftWindow, busy: BusyWindow[]): BusyWindow | undefined {
  if (issueOf(row) !== null) return undefined
  const start = new Date(row.start).getTime()
  const end = new Date(row.end).getTime()
  return busy.find((b) => start < b.end && b.start < end)
}

/** Các dòng hợp lệ, đã đổi sang mốc thời gian để gửi lên server. */
export function toPayload(rows: DraftWindow[], busy: BusyWindow[] = []) {
  // Lọc theo `issuesOf` chứ không phải `issueOf`: dòng chồng lấn tự nó hợp lệ, lọc bằng luật một dòng
  // thì nó vẫn lọt lên server và bị 400 — trong khi giao diện đã tô đỏ nó rồi.
  const issues = issuesOf(rows, busy)
  return rows
    .filter((_, i) => issues[i] === null)
    .map((r) => ({
      startTime: toInstant(r.start)!,
      endTime: toInstant(r.end)!,
      note: r.note.trim() || null,
    }))
}

/** Có dòng nào đang SAI không (dòng trống hoàn toàn không tính — người dùng chỉ chưa gõ). */
export function hasBlockingIssue(rows: DraftWindow[], busy: BusyWindow[] = []) {
  return issuesOf(rows, busy).some((issue) => issue !== null && issue !== 'empty')
}

interface Props {
  rows: DraftWindow[]
  onChange: (rows: DraftWindow[]) => void
  disabled?: boolean
  /** Giờ đã khai ở vòng khác — chiếm chỗ y như các dòng đang soạn ở đây. */
  busy?: BusyWindow[]
}

const LABEL = 'w-8 shrink-0 text-xs font-medium text-ink-500 dark:text-ink-400'

export default function HmAvailabilityFields({ rows, onChange, disabled, busy = [] }: Props) {
  const set = (i: number, patch: Partial<DraftWindow>) =>
    onChange(rows.map((r, idx) => (idx === i ? { ...r, ...patch } : r)))

  /**
   * Đổi lúc bắt đầu. Ngày KẾT THÚC đi theo ngày bắt đầu cho tới khi người dùng tự đổi nó: khung giờ
   * gần như luôn nằm trong một ngày (tối đa 12 tiếng), bắt chọn cùng một ngày hai lần là thừa.
   * "Tự đổi" nhận ra được mà không cần cờ riêng — ngày kết thúc khác ngày bắt đầu CŨ.
   */
  const setStart = (i: number, start: string) => {
    const r = rows[i]
    const oldStartDate = splitLocalDateTime(r.start).date
    const newStartDate = splitLocalDateTime(start).date
    const { date: endDate, time: endTime } = splitLocalDateTime(r.end)
    const follows = newStartDate && (!endDate || endDate === oldStartDate)
    set(i, { start, end: follows ? joinLocalDateTime(newStartDate, endTime) : r.end })
  }

  // Chặn chọn ngày đã qua ngay trên bộ lịch của trình duyệt, trước cả khi có thông báo lỗi nào.
  const today = splitLocalDateTime(nowLocalInput()).date

  const issues = issuesOf(rows, busy)

  return (
    <div className="space-y-2">
      {rows.map((r, i) => {
        const issue = issues[i]
        const bad = issue !== null && issue !== 'empty'
        // "Chưa nhập xong" chỉ là nhắc, không phải lỗi — tô đỏ một dòng đang gõ dở là la người dùng
        // vì chưa kịp làm xong. Đỏ chỉ dành cho giờ SAI thật (quá khứ, ngược chiều, quá dài).
        const wrong = bad && issue !== 'incomplete'
        const startDate = splitLocalDateTime(r.start).date

        return (
          <div
            key={i}
            className={`space-y-2 rounded-xl border p-2.5 ${
              wrong
                ? 'border-red-200 bg-red-50/40 dark:border-red-500/30 dark:bg-red-500/5'
                : 'border-ink-200 dark:border-white/10'
            }`}
          >
            <div className="flex items-center gap-2">
              <span className={LABEL}>Từ</span>
              <DateTimeInput
                value={r.start}
                onChange={(v) => setStart(i, v)}
                minDate={today}
                disabled={disabled}
                invalid={wrong}
                className="flex-1"
                dateAriaLabel="Ngày bắt đầu"
                timeAriaLabel="Giờ bắt đầu"
              />
            </div>
            <div className="flex items-center gap-2">
              <span className={LABEL}>Đến</span>
              <DateTimeInput
                value={r.end}
                onChange={(v) => set(i, { end: v })}
                minDate={startDate || today}
                disabled={disabled}
                invalid={wrong}
                className="flex-1"
                dateAriaLabel="Ngày kết thúc"
                timeAriaLabel="Giờ kết thúc"
              />
            </div>
            <div className="flex items-center gap-2">
              <input
                type="text"
                value={r.note}
                disabled={disabled}
                onChange={(e) => set(i, { note: e.target.value })}
                placeholder="Ghi chú (tuỳ chọn)"
                className="min-w-0 flex-1 rounded-lg border border-ink-200 bg-white px-2.5 py-1.5 text-sm text-ink-900 focus:outline-none focus:ring-2 focus:ring-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
              />
              <button
                type="button"
                disabled={disabled}
                onClick={() => onChange(rows.filter((_, idx) => idx !== i))}
                className="rounded-lg p-1.5 text-ink-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-50 dark:hover:bg-red-500/10"
                aria-label="Xoá khung giờ"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>

            {bad && (
              <p
                className={`flex items-center gap-1.5 text-xs ${
                  wrong ? 'text-red-600 dark:text-red-400' : 'text-amber-700 dark:text-amber-400'
                }`}
              >
                <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                {/* Chồng với vòng khác thì NÓI RÕ vòng nào — người dùng đang ở tab vòng này, không
                    nhìn thấy khung bên kia, mà "chồng lấn" trơn thì phải mở từng tab đi dò. */}
                {issue === 'overlapOther' && busyClashOf(r, busy)
                  ? `Khung này chồng lên lịch rảnh đã khai ở ${busyClashOf(r, busy)!.label}. Cùng một khoảng thời gian không thể vừa dành cho vòng này vừa dành cho vòng kia.`
                  : ISSUE_TEXT[issue as Exclude<WindowIssue, null | 'empty'>]}
              </p>
            )}
          </div>
        )
      })}

      <button
        type="button"
        disabled={disabled}
        onClick={() => onChange([...rows, { start: '', end: '', note: '' }])}
        className="inline-flex items-center gap-1.5 rounded-xl border border-dashed border-ink-300 px-3 py-1.5 text-sm text-ink-600 hover:bg-ink-50 disabled:opacity-50 dark:border-white/20 dark:text-ink-300 dark:hover:bg-white/5"
      >
        <Plus className="h-4 w-4" /> Thêm khung giờ
      </button>

      <p className="pt-1 text-xs text-ink-400">
        Mỗi khung tối đa {MAX_WINDOW_HOURS} tiếng và phải bắt đầu ở tương lai. Rảnh nhiều ngày thì
        thêm mỗi ngày một khung.
      </p>
    </div>
  )
}
