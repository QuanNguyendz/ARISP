import { type ReactNode } from 'react'
import { Loader2 } from 'lucide-react'

interface LoadingButtonProps {
  children: ReactNode
  loading?: boolean
  disabled?: boolean
  type?: 'button' | 'submit' | 'reset'
  onClick?: () => void
  fullWidth?: boolean
  /** Tailwind classes điều khiển giao diện (màu nền, chữ...). Ghi đè mặc định. */
  className?: string
}

export default function LoadingButton({
  children,
  loading = false,
  disabled = false,
  type = 'button',
  onClick,
  fullWidth = false,
  className = 'bg-brand-600 text-white hover:bg-brand-700',
}: LoadingButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || loading}
      onClick={onClick}
      className={`inline-flex items-center justify-center gap-2 rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50 ${
        fullWidth ? 'w-full' : ''
      } ${className}`}
    >
      {loading && <Loader2 className="h-5 w-5 animate-spin" />}
      {children}
    </button>
  )
}
