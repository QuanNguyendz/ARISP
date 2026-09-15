import { useEffect, useId, useMemo, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { Clock } from 'lucide-react'
import { parseTime24, timeSlots } from '@ari/shared/utils/time24'

interface TimeInputProps {
  /** `HH:mm` (24 giờ) hoặc chuỗi rỗng — cùng khuôn giá trị với `<input type="time">`. */
  value: string
  onChange: (value: string) => void
  disabled?: boolean
  /** Viền đỏ cho lỗi do nơi dùng phát hiện (vd. giờ kết thúc trước giờ bắt đầu). */
  invalid?: boolean
  /** Khoảng cách giữa các gợi ý trong danh sách (phút). Gõ tay vẫn nhận mọi phút. */
  step?: number
  placeholder?: string
  /** Bọc ngoài — dùng để chỉnh bề rộng. */
  className?: string
  /** Bo góc + padding + cỡ chữ của ô. Phần đệm phải dành cho icon đồng hồ đã có sẵn. */
  inputClassName?: string
  id?: string
  ariaLabel?: string
  /** Ô còn trống thì danh sách mở ở mốc này — mặc định đầu giờ làm việc. */
  emptyAnchor?: string
}

/**
 * Ô nhập giờ luôn 24 tiếng — thay cho `<input type="time">`.
 *
 * <b>Vì sao không dùng ô gốc.</b> Ô gốc lấy khuôn giờ từ cài đặt vùng của hệ điều hành: Windows đặt
 * 12 giờ thì ô hiện "11:30 CH", và không thuộc tính nào ép lại được. Người dùng đọc nhầm SA/CH là
 * xếp nhầm ca — "11:30 CH → 12:01 SA" thực ra là 23:30 → 00:01 hôm sau.
 *
 * Hai cách nhập, cùng một ô: GÕ (`15`, `1530`, `15:30`, `15h30` — xem `parseTime24`) hoặc CHỌN trong
 * danh sách mốc cách đều. Gõ không hiểu được thì trả về giá trị cũ khi rời ô, không đoán.
 *
 * Bàn phím: ↑/↓ mở và di chuyển trong danh sách, Enter chọn (mục đang trỏ nếu vừa dùng mũi tên,
 * không thì lấy chữ đã gõ), Esc đóng và bỏ phần đang gõ.
 */
export default function TimeInput({
  value,
  onChange,
  disabled = false,
  invalid = false,
  step = 15,
  placeholder = '--:--',
  className = '',
  inputClassName = 'rounded-xl py-2 pl-3 text-sm',
  id,
  ariaLabel,
  emptyAnchor = '08:00',
}: TimeInputProps) {
  const options = useMemo(() => timeSlots(step), [step])
  const [draft, setDraft] = useState(value)
  const [focused, setFocused] = useState(false)
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)
  /** Đang chọn bằng mũi tên — Enter lấy mục đang trỏ thay vì chữ đã gõ. */
  const [navigating, setNavigating] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)
  /**
   * Bấm chuột vào ô đang KHÔNG focus → bôi đen cả giờ cũ để gõ đè. Phải làm lúc NHẢ chuột: bôi đen
   * lúc focus thì Chrome/Edge đặt lại con trỏ khi nhả chuột, và "1530" gõ vào thành "11:301530".
   */
  const selectOnMouseUpRef = useRef(false)
  const generatedId = useId()
  const listboxId = `${id ?? generatedId}-listbox`

  // Giá trị từ ngoài đổi (nạp lại, đặt lại form) thì hiện theo — trừ lúc đang gõ, để không đè chữ.
  useEffect(() => {
    if (!focused) setDraft(value)
  }, [value, focused])

  /** Gợi ý đầu tiên không sớm hơn `hhmm` — so chuỗi `HH:mm` là so đúng thứ tự thời gian. */
  const indexFor = (hhmm: string) => {
    const i = options.findIndex((o) => o >= hhmm)
    return i === -1 ? options.length - 1 : i
  }

  const openList = (anchor?: string | null) => {
    setActiveIndex(indexFor(anchor || value || emptyAnchor))
    setOpen(true)
  }

  const closeList = () => {
    setOpen(false)
    setNavigating(false)
  }

  // Mới mở: đưa mốc đang chọn vào GIỮA danh sách; di chuyển sau đó chỉ cuộn vừa đủ thấy.
  useEffect(() => {
    if (!open) return
    listRef.current
      ?.querySelector<HTMLElement>(`[data-index="${activeIndex}"]`)
      ?.scrollIntoView({ block: 'center' })
    // Chỉ chạy lúc mở — `activeIndex` đổi sau đó do effect bên dưới lo.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  useEffect(() => {
    if (!open || activeIndex < 0) return
    listRef.current
      ?.querySelector<HTMLElement>(`[data-index="${activeIndex}"]`)
      ?.scrollIntoView({ block: 'nearest' })
  }, [open, activeIndex])

  /** Chốt chữ đã gõ: trống → xoá giá trị; hiểu được → chuẩn hoá `HH:mm`; không hiểu → giữ giá trị cũ. */
  const commitDraft = () => {
    const text = draft.trim()
    if (!text) {
      if (value !== '') onChange('')
      setDraft('')
      return
    }
    const parsed = parseTime24(text)
    if (parsed) {
      if (parsed !== value) onChange(parsed)
      setDraft(parsed)
    } else {
      setDraft(value)
    }
  }

  const choose = (option: string) => {
    if (option !== value) onChange(option)
    setDraft(option)
    closeList()
  }

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (disabled) return
    switch (e.key) {
      case 'ArrowDown':
      case 'ArrowUp': {
        e.preventDefault()
        setNavigating(true)
        if (!open) {
          openList(parseTime24(draft))
          return
        }
        const dir = e.key === 'ArrowDown' ? 1 : -1
        setActiveIndex((i) => (i < 0 ? 0 : (i + dir + options.length) % options.length))
        return
      }
      case 'Enter':
        e.preventDefault()
        if (open && navigating && activeIndex >= 0) {
          choose(options[activeIndex])
        } else {
          commitDraft()
          closeList()
        }
        return
      case 'Escape':
        if (open) {
          e.preventDefault()
          setDraft(value)
          closeList()
        }
        return
    }
  }

  const draftUnreadable = focused && draft.trim() !== '' && parseTime24(draft) === null
  const bad = invalid || draftUnreadable

  return (
    <div className={`relative ${className}`}>
      <input
        ref={inputRef}
        id={id}
        type="text"
        inputMode="numeric"
        autoComplete="off"
        role="combobox"
        aria-expanded={open}
        aria-controls={open ? listboxId : undefined}
        aria-autocomplete="list"
        aria-activedescendant={open && activeIndex >= 0 ? `${listboxId}-${activeIndex}` : undefined}
        aria-label={ariaLabel}
        aria-invalid={bad || undefined}
        placeholder={placeholder}
        maxLength={8}
        value={draft}
        disabled={disabled}
        onFocus={(e) => {
          setFocused(true)
          e.target.select()
        }}
        onMouseDown={() => {
          selectOnMouseUpRef.current = document.activeElement !== inputRef.current
        }}
        onMouseUp={(e) => {
          if (!selectOnMouseUpRef.current) return
          selectOnMouseUpRef.current = false
          e.preventDefault()
          e.currentTarget.select()
        }}
        onClick={() => {
          if (!open) openList(parseTime24(draft))
        }}
        onChange={(e) => {
          const text = e.target.value
          setDraft(text)
          setNavigating(false)
          const parsed = parseTime24(text)
          if (parsed) setActiveIndex(indexFor(parsed))
          if (!open) setOpen(true)
        }}
        onBlur={() => {
          setFocused(false)
          commitDraft()
          closeList()
        }}
        onKeyDown={onKeyDown}
        className={`w-full border bg-white pr-9 tabular-nums text-ink-900 outline-none transition placeholder:text-ink-400 focus:ring-2 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-white/5 dark:text-white dark:placeholder:text-ink-500 ${
          bad
            ? 'border-red-300 focus:border-red-400 focus:ring-red-100 dark:border-red-500/40 dark:focus:ring-red-500/20'
            : 'border-ink-200 focus:border-brand-500 focus:ring-brand-100 dark:border-white/10 dark:focus:ring-brand-500/30'
        } ${inputClassName}`}
      />

      {/* Không nhận focus: bấm vào là mở/đóng danh sách và trả focus về ô chữ. */}
      <button
        type="button"
        tabIndex={-1}
        aria-hidden="true"
        disabled={disabled}
        onMouseDown={(e) => e.preventDefault()}
        onClick={() => {
          inputRef.current?.focus()
          if (open) closeList()
          else openList(parseTime24(draft))
        }}
        className="absolute inset-y-0 right-0 flex w-9 items-center justify-center text-ink-400 transition hover:text-ink-600 disabled:cursor-not-allowed dark:hover:text-ink-200"
      >
        <Clock className="h-4 w-4" />
      </button>

      {open && (
        <ul
          ref={listRef}
          id={listboxId}
          role="listbox"
          aria-label={ariaLabel}
          // Chặn mất focus ở ô chữ khi bấm vào danh sách — nếu không, `onBlur` chốt chữ đang gõ
          // và đóng danh sách TRƯỚC khi cú bấm chọn kịp tới mục.
          onMouseDown={(e) => e.preventDefault()}
          className="absolute left-0 z-50 mt-1 max-h-56 w-full min-w-[6.5rem] overflow-auto rounded-xl border border-ink-200 bg-white py-1 shadow-card-hover dark:border-white/10 dark:bg-ink-900"
        >
          {options.map((option, index) => {
            const isSelected = option === value
            const isActive = index === activeIndex
            return (
              <li
                key={option}
                id={`${listboxId}-${index}`}
                role="option"
                aria-selected={isSelected}
                data-index={index}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => choose(option)}
                className={`cursor-pointer px-3 py-1.5 text-sm tabular-nums transition ${
                  isSelected
                    ? 'font-semibold text-brand-700 dark:text-brand-300'
                    : 'text-ink-700 dark:text-ink-200'
                } ${isActive ? 'bg-ink-100 dark:bg-white/10' : ''}`}
              >
                {option}
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
