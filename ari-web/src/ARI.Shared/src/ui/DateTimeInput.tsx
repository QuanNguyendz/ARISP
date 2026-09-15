import { joinLocalDateTime, splitLocalDateTime } from '@ari/shared/utils/time24'
import TimeInput from './TimeInput'

interface DateTimeInputProps {
  /**
   * `YYYY-MM-DDTHH:mm` — cùng khuôn với `<input type="datetime-local">`. Được phép DỞ (`2026-09-15T`
   * khi mới chọn ngày, `T15:00` khi mới chọn giờ) để không mất phần đã nhập; kiểm đủ bằng
   * `isCompleteLocalDateTime` trước khi đổi sang mốc thời gian.
   */
  value: string
  onChange: (value: string) => void
  /** Ngày sớm nhất chọn được, `YYYY-MM-DD`. */
  minDate?: string
  disabled?: boolean
  invalid?: boolean
  className?: string
  dateAriaLabel?: string
  timeAriaLabel?: string
}

/**
 * Ô ngày + giờ 24 tiếng — thay cho `<input type="datetime-local">`, vốn hiện SA/CH theo cài đặt vùng
 * của hệ điều hành giống hệt ô giờ gốc (xem `TimeInput`).
 *
 * Ngày vẫn dùng ô gốc: định dạng ngày không có chuyện nhầm sáng/chiều, và bộ lịch của trình duyệt là
 * thứ người dùng đã quen.
 */
export default function DateTimeInput({
  value,
  onChange,
  minDate,
  disabled,
  invalid,
  className = '',
  dateAriaLabel,
  timeAriaLabel,
}: DateTimeInputProps) {
  const { date, time } = splitLocalDateTime(value)
  const border = invalid ? 'border-red-300 dark:border-red-500/40' : 'border-ink-200 dark:border-white/10'

  return (
    <div className={`flex min-w-0 items-center gap-1.5 ${className}`}>
      <input
        type="date"
        value={date}
        min={minDate}
        disabled={disabled}
        aria-label={dateAriaLabel}
        onChange={(e) => onChange(joinLocalDateTime(e.target.value, time))}
        className={`min-w-0 flex-1 rounded-lg border ${border} bg-white px-2.5 py-1.5 text-sm text-ink-900 outline-none focus:ring-2 focus:ring-brand-500 disabled:opacity-50 dark:bg-white/5 dark:text-white`}
      />
      <TimeInput
        value={time}
        onChange={(t) => onChange(joinLocalDateTime(date, t))}
        disabled={disabled}
        invalid={invalid}
        ariaLabel={timeAriaLabel}
        className="w-[5.75rem] shrink-0"
        inputClassName="rounded-lg py-1.5 pl-2.5 text-sm"
      />
    </div>
  )
}
