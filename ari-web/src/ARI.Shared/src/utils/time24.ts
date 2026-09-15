/**
 * Giờ 24 tiếng — một nguồn duy nhất cho mọi ô nhập giờ và mọi chỗ hiển thị giờ.
 *
 * <b>Vì sao không để trình duyệt tự định dạng.</b> `<input type="time">` và `datetime-local` lấy khuôn
 * giờ từ CÀI ĐẶT VÙNG CỦA HỆ ĐIỀU HÀNH, không phải từ trang: Windows đặt 12 giờ thì ô hiện "11:30 CH",
 * và không thuộc tính HTML nào ép lại được. Hậu quả đã gặp thật: người dùng nhập "11:30 CH → 12:01 SA"
 * định nói 11:30 → 12:01 trưa, nhưng máy hiểu 23:30 → 00:01 và báo "giờ kết thúc phải sau giờ bắt đầu".
 * `toLocaleTimeString(undefined, …)` cũng thế — theo ngôn ngữ trình duyệt, en-US ra "3:00 PM".
 *
 * Các hàm ở đây tự ghép `HH:mm` từ giờ/phút thay vì nhờ Intl, nên kết quả không phụ thuộc máy nào.
 */

const pad2 = (n: number) => String(n).padStart(2, '0')

/**
 * Trộn vào tuỳ chọn Intl khi vẫn cần Intl viết phần NGÀY (thứ, tên tháng theo ngôn ngữ).
 * `hourCycle: 'h23'` chứ không `hour12: false`: tổ hợp sau in "24:05" cho nửa đêm ở vài bản Chrome.
 */
export const HOUR_CYCLE_24 = { hourCycle: 'h23' } as const

function toDate(value: Date | string | number | null | undefined): Date | null {
  if (value == null || value === '') return null
  const d = value instanceof Date ? value : new Date(value)
  return Number.isNaN(d.getTime()) ? null : d
}

/** `15:00` — giờ của một mốc theo giờ máy người xem. Mốc hỏng/trống → chuỗi rỗng. */
export function formatTime24(value: Date | string | number | null | undefined): string {
  const d = toDate(value)
  return d ? `${pad2(d.getHours())}:${pad2(d.getMinutes())}` : ''
}

/** `14/09/2026 15:00` — ngày + giờ 24h. Mốc hỏng/trống → chuỗi rỗng. */
export function formatDateTime24(value: Date | string | number | null | undefined): string {
  const d = toDate(value)
  if (!d) return ''
  return `${pad2(d.getDate())}/${pad2(d.getMonth() + 1)}/${d.getFullYear()} ${formatTime24(d)}`
}

/**
 * Đọc giờ người dùng GÕ TAY, trả `HH:mm` hoặc `null` nếu không hiểu.
 *
 * Nhận mọi cách viết hay gặp, để ô nhập không bắt người dùng học một cú pháp:
 * `15` → 15:00 · `1530` / `930` → 15:30 / 09:30 · `15:30`, `15h30`, `15h`, `15g30`, `9.05`.
 * Hậu tố 12 giờ (`3ch`, `3pm`, `11sa`, `12am`) vẫn được đổi đúng — người quen gõ kiểu cũ không
 * bị từ chối, và thứ hiện lại trên ô luôn là dạng 24 giờ.
 */
export function parseTime24(text: string | null | undefined): string | null {
  let s = (text ?? '').trim().toLowerCase().replace(/\s+/g, '')
  if (!s) return null

  let meridiem: 'am' | 'pm' | null = null
  const suffix = s.match(/(sa|ch|am|pm|a|p)$/)
  if (suffix) {
    meridiem = suffix[1] === 'ch' || suffix[1].startsWith('p') ? 'pm' : 'am'
    s = s.slice(0, -suffix[1].length)
  }

  let hour: number
  let minute: number
  const withSeparator = s.match(/^(\d{1,2})(?:[:h.g](\d{1,2})?)?$/)
  const digitsOnly = s.match(/^(\d{1,2})(\d{2})$/)
  if (withSeparator) {
    hour = Number(withSeparator[1])
    minute = withSeparator[2] ? Number(withSeparator[2]) : 0
  } else if (digitsOnly) {
    hour = Number(digitsOnly[1])
    minute = Number(digitsOnly[2])
  } else {
    return null
  }

  if (meridiem) {
    if (hour < 1 || hour > 12) return null
    hour = (hour % 12) + (meridiem === 'pm' ? 12 : 0)
  }

  if (hour > 23 || minute > 59) return null
  return `${pad2(hour)}:${pad2(minute)}`
}

/** Các mốc `HH:mm` trong ngày cách nhau `stepMinutes` phút — danh sách gợi ý của ô nhập giờ. */
export function timeSlots(stepMinutes = 15): string[] {
  const step = Math.min(Math.max(Math.floor(stepMinutes), 1), 60)
  const out: string[] = []
  for (let m = 0; m < 24 * 60; m += step) out.push(`${pad2(Math.floor(m / 60))}:${pad2(m % 60)}`)
  return out
}

// ---- Chuỗi ngày-giờ LOCAL dạng `YYYY-MM-DDTHH:mm` (khuôn của `datetime-local`) ----------------

const LOCAL_DATE_TIME = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/

/**
 * Đủ cả ngày lẫn giờ chưa. Ô ngày-giờ tự dựng được phép giữ giá trị DỞ (`2026-09-15T` khi mới chọn
 * ngày, `T15:00` khi mới chọn giờ) để không mất phần người dùng đã nhập — nên chỗ dùng phải hỏi câu
 * này trước khi đổi sang mốc thời gian.
 */
export const isCompleteLocalDateTime = (value: string | null | undefined): boolean =>
  !!value && LOCAL_DATE_TIME.test(value)

/** Tách `YYYY-MM-DDTHH:mm` (kể cả dạng dở) thành hai phần. */
export function splitLocalDateTime(value: string | null | undefined): { date: string; time: string } {
  const [date = '', time = ''] = (value ?? '').split('T')
  return { date, time: time.slice(0, 5) }
}

/** Ghép lại; cả hai phần đều trống thì trả chuỗi rỗng (không phải `"T"`). */
export function joinLocalDateTime(date: string, time: string): string {
  return date || time ? `${date}T${time}` : ''
}
