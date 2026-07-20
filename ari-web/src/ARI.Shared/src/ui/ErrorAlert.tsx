import { useState } from 'react'
import { AlertCircle, X } from 'lucide-react'

interface ErrorAlertProps {
  message: string
  title?: string
  severity?: 'error' | 'warning' | 'info' | 'success'
}

const SEVERITY_STYLES: Record<NonNullable<ErrorAlertProps['severity']>, string> = {
  error: 'bg-red-50 border-red-200 text-red-700',
  warning: 'bg-amber-50 border-amber-200 text-amber-700',
  info: 'bg-blue-50 border-blue-200 text-blue-700',
  success: 'bg-emerald-50 border-emerald-200 text-emerald-700',
}

export default function ErrorAlert({ message, title, severity = 'error' }: ErrorAlertProps) {
  const [open, setOpen] = useState(true)
  if (!open) return null

  return (
    <div className={`flex items-start gap-2 rounded-xl border p-3 ${SEVERITY_STYLES[severity]}`}>
      <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
      <div className="flex-1">
        {title && <p className="font-semibold">{title}</p>}
        <p className="text-sm">{message}</p>
      </div>
      <button
        type="button"
        aria-label="close"
        onClick={() => setOpen(false)}
        className="shrink-0 opacity-70 transition hover:opacity-100"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  )
}
