import { Skeleton } from '@components/ui/Skeleton'

const card = 'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card'

export function StatsGridSkeleton() {
  return (
    <div className="mb-8 grid grid-cols-2 gap-4 lg:grid-cols-4">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className={`${card} p-5`}>
          <Skeleton className="mb-3 h-4 w-24" />
          <Skeleton className="h-7 w-12" />
        </div>
      ))}
    </div>
  )
}

/** Lưới card tin tuyển dụng giả lập. */
export function JobsGridSkeleton({ count = 4 }: { count?: number }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className={`${card} p-5`}>
          <div className="mb-3 flex items-center gap-3">
            <Skeleton className="h-11 w-11 shrink-0 rounded-xl" />
            <div className="flex-1">
              <Skeleton className="mb-1.5 h-4 w-36" />
              <Skeleton className="h-3 w-24" />
            </div>
            <Skeleton className="h-5 w-20 rounded-full" />
          </div>
          <Skeleton className="mb-2 h-3 w-full" />
          <div className="mt-4 flex items-center justify-between">
            <Skeleton className="h-3 w-28" />
            <Skeleton className="h-3 w-16" />
          </div>
        </div>
      ))}
    </div>
  )
}

export function DashboardSkeleton() {
  return (
    <div className="p-6 lg:p-8">
      <div className="mb-8">
        <Skeleton className="mb-2 h-7 w-64" />
        <Skeleton className="h-4 w-48" />
      </div>
      <StatsGridSkeleton />
      <div className="grid gap-6 lg:grid-cols-3 lg:gap-8">
        <div className="space-y-6 lg:col-span-2">
          <JobsGridSkeleton count={4} />
        </div>
        <div className="space-y-6">
          <div className={`${card} p-5`}>
            <Skeleton className="mb-4 h-4 w-28" />
            <div className="space-y-2">
              {Array.from({ length: 3 }).map((_, i) => (
                <Skeleton key={i} className="h-12 w-full rounded-xl" />
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

/** Skeleton danh sách ứng viên (bảng) cho Job Detail. */
export function ApplicantsSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className={`${card} overflow-hidden`}>
      <div className="divide-y divide-ink-100 dark:divide-white/10">
        {Array.from({ length: rows }).map((_, i) => (
          <div key={i} className="flex items-center gap-4 px-5 py-4">
            <Skeleton className="h-10 w-10 shrink-0 rounded-full" />
            <div className="flex-1">
              <Skeleton className="mb-1.5 h-3.5 w-40" />
              <Skeleton className="h-3 w-52" />
            </div>
            <Skeleton className="hidden h-6 w-12 rounded-full sm:block" />
            <Skeleton className="h-6 w-24 rounded-full" />
          </div>
        ))}
      </div>
    </div>
  )
}

export function JobDetailSkeleton() {
  return (
    <div className="p-6 lg:p-8">
      <Skeleton className="mb-4 h-4 w-32" />
      <div className={`${card} mb-6 p-6`}>
        <Skeleton className="mb-2 h-6 w-64" />
        <Skeleton className="h-4 w-40" />
      </div>
      <StatsGridSkeleton />
      <ApplicantsSkeleton rows={5} />
    </div>
  )
}
