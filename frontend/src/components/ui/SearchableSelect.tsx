import { useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { ChevronDown, Search, Check } from 'lucide-react'

export interface SelectOption {
  value: number
  label: string
}

/** Chuẩn hoá tiếng Việt để tìm không dấu: "Hà Nội" -> "ha noi". */
function normalizeVi(s: string): string {
  return s
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[đ]/g, 'd')
}

/**
 * Dropdown có ô tìm kiếm (gõ để lọc) — thay cho <select> native khi danh sách dài.
 * Không phụ thuộc thư viện ngoài.
 */
export default function SearchableSelect({
  options,
  value,
  onChange,
  placeholder = '— Chọn —',
  disabled = false,
  emptyText = 'Không có kết quả',
  icon,
}: {
  options: SelectOption[]
  value: number | null | undefined
  onChange: (value: number) => void
  placeholder?: string
  disabled?: boolean
  emptyText?: string
  icon?: ReactNode
}) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const rootRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const selected = options.find((o) => o.value === value) || null

  const filtered = useMemo(() => {
    const q = normalizeVi(query.trim())
    if (!q) return options
    return options.filter((o) => normalizeVi(o.label).includes(q))
  }, [options, query])

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [open])

  useEffect(() => {
    if (open) {
      setQuery('')
      const t = setTimeout(() => inputRef.current?.focus(), 0)
      return () => clearTimeout(t)
    }
  }, [open])

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        disabled={disabled}
        onClick={() => !disabled && setOpen((v) => !v)}
        className="flex w-full items-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5 text-left text-sm text-ink-800 outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-100 disabled:opacity-50"
      >
        {icon}
        <span className={`flex-1 truncate ${selected ? '' : 'text-ink-400'}`}>
          {selected ? selected.label : placeholder}
        </span>
        <ChevronDown
          className={`h-4 w-4 shrink-0 text-ink-400 transition ${open ? 'rotate-180' : ''}`}
        />
      </button>

      {open && (
        <div className="absolute z-30 mt-1 w-full overflow-hidden rounded-xl border border-ink-200 bg-white shadow-card-hover">
          <div className="flex items-center gap-2 border-b border-ink-100 px-3 py-2">
            <Search className="h-4 w-4 shrink-0 text-ink-400" />
            <input
              ref={inputRef}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Gõ để tìm..."
              className="w-full bg-transparent text-sm text-ink-800 outline-none placeholder:text-ink-400"
            />
          </div>
          <ul className="max-h-60 overflow-auto py-1">
            {filtered.length === 0 ? (
              <li className="px-3 py-2 text-sm text-ink-400">{emptyText}</li>
            ) : (
              filtered.map((o) => (
                <li key={o.value}>
                  <button
                    type="button"
                    onClick={() => {
                      onChange(o.value)
                      setOpen(false)
                    }}
                    className={`flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-ink-100 ${
                      o.value === value ? 'font-medium text-brand-700' : 'text-ink-700'
                    }`}
                  >
                    <span className="truncate">{o.label}</span>
                    {o.value === value && <Check className="h-4 w-4 shrink-0 text-brand-700" />}
                  </button>
                </li>
              ))
            )}
          </ul>
        </div>
      )}
    </div>
  )
}
