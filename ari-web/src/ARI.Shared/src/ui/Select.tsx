import { useEffect, useId, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { Check, ChevronDown } from 'lucide-react'

export interface SelectItem {
  value: string
  label: string
  disabled?: boolean
}

interface SelectProps {
  value: string
  onChange: (value: string) => void
  options: SelectItem[]
  /** Hiện khi `value` không khớp option nào (mặc định để trống). */
  placeholder?: string
  disabled?: boolean
  /** Class cho bọc ngoài — dùng để chỉnh bề rộng (`w-full`, `min-w-[12rem]`…). */
  className?: string
  /** Ghi đè kích thước nút (padding + cỡ chữ). Mặc định `px-3 py-2 text-sm`. */
  buttonClassName?: string
  /** Neo panel về phải khi nút nằm sát mép phải. */
  align?: 'left' | 'right'
  ariaLabel?: string
  id?: string
  icon?: ReactNode
}

/**
 * Dropdown thay cho `<select>` native.
 *
 * Lý do tồn tại: `<select>` để trình duyệt/HĐH tự vẽ danh sách chọn, nên phần bung ra
 * KHÔNG nhận CSS của app — ra nền trắng, viền vuông, dòng highlight xanh hệ thống, và
 * không theo dark mode. Nút thì hợp style còn danh sách thì lạc hẳn.
 *
 * Ở đây tự render danh sách bằng markup thường nên bám đúng token (ink/brand, rounded-xl,
 * shadow-card-hover) và có `dark:` đầy đủ.
 *
 * Bàn phím: Enter/Space/Alt+Down mở, ↑/↓ di chuyển (tự nhảy qua mục disabled),
 * Home/End về đầu–cuối, Enter chọn, Esc đóng và trả focus về nút.
 */
export default function Select({
  value,
  onChange,
  options,
  placeholder,
  disabled = false,
  className = '',
  buttonClassName = 'px-3 py-2 text-sm',
  align = 'left',
  ariaLabel,
  id,
  icon,
}: SelectProps) {
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const listRef = useRef<HTMLUListElement>(null)
  const generatedId = useId()
  const listboxId = `${id ?? generatedId}-listbox`

  const selected = useMemo(() => options.find((o) => o.value === value) ?? null, [options, value])
  const selectableIndexes = useMemo(
    () => options.map((o, i) => (o.disabled ? -1 : i)).filter((i) => i >= 0),
    [options]
  )

  // Đóng khi bấm ra ngoài.
  useEffect(() => {
    if (!open) return
    const onPointerDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onPointerDown)
    return () => document.removeEventListener('mousedown', onPointerDown)
  }, [open])

  // Mở ra thì đặt con trỏ vào mục đang chọn (hoặc mục chọn được đầu tiên).
  useEffect(() => {
    if (!open) return
    const current = options.findIndex((o) => o.value === value && !o.disabled)
    setActiveIndex(current >= 0 ? current : (selectableIndexes[0] ?? -1))
  }, [open, options, value, selectableIndexes])

  // Giữ mục đang trỏ luôn nằm trong vùng nhìn thấy khi cuộn bằng bàn phím.
  useEffect(() => {
    if (!open || activeIndex < 0) return
    listRef.current?.querySelector<HTMLElement>(`[data-index="${activeIndex}"]`)?.scrollIntoView({
      block: 'nearest',
    })
  }, [open, activeIndex])

  const commit = (item: SelectItem) => {
    if (item.disabled) return
    onChange(item.value)
    setOpen(false)
    buttonRef.current?.focus()
  }

  const moveActive = (direction: 1 | -1) => {
    if (selectableIndexes.length === 0) return
    const pos = selectableIndexes.indexOf(activeIndex)
    const next =
      pos === -1
        ? selectableIndexes[direction === 1 ? 0 : selectableIndexes.length - 1]
        : selectableIndexes[(pos + direction + selectableIndexes.length) % selectableIndexes.length]
    setActiveIndex(next)
  }

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (disabled) return

    if (!open) {
      if (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault()
        setOpen(true)
      }
      return
    }

    switch (e.key) {
      case 'Escape':
        e.preventDefault()
        setOpen(false)
        buttonRef.current?.focus()
        break
      case 'ArrowDown':
        e.preventDefault()
        moveActive(1)
        break
      case 'ArrowUp':
        e.preventDefault()
        moveActive(-1)
        break
      case 'Home':
        e.preventDefault()
        setActiveIndex(selectableIndexes[0] ?? -1)
        break
      case 'End':
        e.preventDefault()
        setActiveIndex(selectableIndexes[selectableIndexes.length - 1] ?? -1)
        break
      case 'Enter':
      case ' ': {
        e.preventDefault()
        const item = options[activeIndex]
        if (item) commit(item)
        break
      }
      case 'Tab':
        setOpen(false)
        break
    }
  }

  return (
    <div ref={rootRef} className={`relative ${className}`} onKeyDown={onKeyDown}>
      <button
        ref={buttonRef}
        id={id}
        type="button"
        role="combobox"
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-controls={open ? listboxId : undefined}
        aria-label={ariaLabel}
        disabled={disabled}
        onClick={() => !disabled && setOpen((v) => !v)}
        className={`flex w-full items-center gap-2 rounded-xl border border-ink-200 bg-white text-left text-ink-900 outline-none transition focus:border-brand-500 focus:ring-2 focus:ring-brand-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-white/10 dark:bg-white/5 dark:text-white dark:focus:ring-brand-500/30 ${buttonClassName}`}
      >
        {icon}
        <span className={`flex-1 truncate ${selected ? '' : 'text-ink-400 dark:text-ink-500'}`}>
          {selected ? selected.label : (placeholder ?? '')}
        </span>
        <ChevronDown
          className={`h-4 w-4 shrink-0 text-ink-400 transition-transform ${open ? 'rotate-180' : ''}`}
        />
      </button>

      {open && (
        <ul
          ref={listRef}
          id={listboxId}
          role="listbox"
          aria-label={ariaLabel}
          tabIndex={-1}
          className={`absolute z-50 mt-1 max-h-60 min-w-full overflow-auto rounded-xl border border-ink-200 bg-white py-1 shadow-card-hover dark:border-white/10 dark:bg-ink-900 ${
            align === 'right' ? 'right-0' : 'left-0'
          }`}
        >
          {options.map((option, index) => {
            const isSelected = option.value === value
            const isActive = index === activeIndex
            return (
              <li key={option.value} role="none">
                <button
                  type="button"
                  role="option"
                  data-index={index}
                  aria-selected={isSelected}
                  disabled={option.disabled}
                  onClick={() => commit(option)}
                  onMouseEnter={() => !option.disabled && setActiveIndex(index)}
                  className={`flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm transition ${
                    option.disabled
                      ? 'cursor-not-allowed text-ink-400 dark:text-ink-600'
                      : isSelected
                        ? 'font-medium text-brand-700 dark:text-brand-300'
                        : 'text-ink-700 dark:text-ink-200'
                  } ${isActive && !option.disabled ? 'bg-ink-100 dark:bg-white/10' : ''}`}
                >
                  <span className="truncate">{option.label}</span>
                  {isSelected && (
                    <Check className="h-4 w-4 shrink-0 text-brand-700 dark:text-brand-300" />
                  )}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
