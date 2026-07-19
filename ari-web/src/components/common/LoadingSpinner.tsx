import { Loader2 } from 'lucide-react'

interface LoadingSpinnerProps {
  size?: number
  fullScreen?: boolean
}

export default function LoadingSpinner({ size = 40, fullScreen = false }: LoadingSpinnerProps) {
  if (fullScreen) {
    return (
      <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/50">
        <Loader2 className="animate-spin text-white" style={{ width: size, height: size }} />
      </div>
    )
  }

  return (
    <div className="flex justify-center p-4">
      <Loader2 className="animate-spin text-brand-600" style={{ width: size, height: size }} />
    </div>
  )
}
