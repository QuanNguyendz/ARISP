import { Skeleton } from '@components/ui/Skeleton'

const card = 'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card'

/** 4 thẻ KPI giả lập — khớp StatsGrid (grid 2/4 cột). */
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

function ListRowsSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex items-center justify-between gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3">
          <div className="flex min-w-0 items-center gap-3">
            <Skeleton className="h-10 w-10 shrink-0 rounded-full" />
            <div>
              <Skeleton className="mb-1.5 h-3.5 w-32" />
              <Skeleton className="h-3 w-44" />
            </div>
          </div>
          <Skeleton className="h-7 w-16 rounded-lg" />
        </div>
      ))}
    </div>
  )
}

function PanelSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className={`${card} p-6`}>
      <div className="mb-5 flex items-center gap-3">
        <Skeleton className="h-10 w-10 rounded-xl" />
        <div>
          <Skeleton className="mb-1.5 h-4 w-40" />
          <Skeleton className="h-3 w-28" />
        </div>
      </div>
      <ListRowsSkeleton rows={rows} />
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
        <div className="space-y-6 lg:col-span-2 lg:space-y-8">
          <PanelSkeleton rows={4} />
          <PanelSkeleton rows={5} />
        </div>
        <div className="space-y-6">
          <div className={`${card} p-5`}>
            <Skeleton className="mb-4 h-4 w-28" />
            <div className="space-y-2">
              {Array.from({ length: 4 }).map((_, i) => (
                <Skeleton key={i} className="h-12 w-full rounded-xl" />
              ))}
            </div>
          </div>
          <div className={`${card} p-5`}>
            <Skeleton className="mb-4 h-4 w-32" />
            <div className="space-y-3">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="flex items-center justify-between">
                  <Skeleton className="h-3.5 w-24" />
                  <Skeleton className="h-3.5 w-8" />
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

/** Bảng giả lập — dùng cho Users (header + N hàng). */
export function TableSkeleton({ rows = 8, cols = 5 }: { rows?: number; cols?: number }) {
  return (
    <div className={`${card} overflow-hidden`}>
      <div className="border-b border-ink-100 px-6 py-3 dark:border-white/10">
        <Skeleton className="h-3 w-full max-w-md" />
      </div>
      <div className="divide-y divide-ink-100 dark:divide-white/10">
        {Array.from({ length: rows }).map((_, r) => (
          <div key={r} className="flex items-center gap-4 px-6 py-4">
            <Skeleton className="h-10 w-10 shrink-0 rounded-full" />
            <div className="flex-1">
              <Skeleton className="mb-1.5 h-3.5 w-40" />
              <Skeleton className="h-3 w-52" />
            </div>
            {Array.from({ length: cols - 2 }).map((_, c) => (
              <Skeleton key={c} className="hidden h-6 w-20 rounded-full sm:block" />
            ))}
          </div>
        ))}
      </div>
    </div>
  )
}

/** Danh sách card giả lập — dùng cho Pending (cards lớn). */
export function CardListSkeleton({ count = 4 }: { count?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className={`${card} p-5`}>
          <div className="flex items-center justify-between gap-4">
            <div className="flex items-center gap-4">
              <Skeleton className="h-12 w-12 shrink-0 rounded-full" />
              <div>
                <Skeleton className="mb-2 h-4 w-44" />
                <Skeleton className="mb-2 h-3.5 w-56" />
                <Skeleton className="h-5 w-24 rounded-full" />
              </div>
            </div>
            <div className="hidden items-center gap-2 sm:flex">
              <Skeleton className="h-9 w-24 rounded-xl" />
              <Skeleton className="h-9 w-24 rounded-xl" />
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

/** Danh sách dòng nhật ký giả lập — dùng cho Audit Logs. */
export function LogListSkeleton({ rows = 10 }: { rows?: number }) {
  return (
    <div className={`${card} overflow-hidden`}>
      <div className="divide-y divide-ink-100 dark:divide-white/10">
        {Array.from({ length: rows }).map((_, i) => (
          <div key={i} className="flex items-start gap-4 px-6 py-4">
            <Skeleton className="mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full" />
            <div className="min-w-0 flex-1">
              <Skeleton className="mb-1.5 h-3.5 w-48" />
              <Skeleton className="h-3 w-64" />
            </div>
            <Skeleton className="h-3 w-16 shrink-0" />
          </div>
        ))}
      </div>
    </div>
  )
}

export function SettingsSkeleton() {
  return (
    <div className="flex flex-col gap-6 lg:flex-row lg:gap-8">
      <div className="lg:w-64 lg:shrink-0">
        <div className={`${card} space-y-2 p-2`}>
          {Array.from({ length: 2 }).map((_, i) => (
            <Skeleton key={i} className="h-11 w-full rounded-xl" />
          ))}
        </div>
      </div>
      <div className={`${card} flex-1 space-y-5 p-6`}>
        <div>
          <Skeleton className="mb-2 h-5 w-48" />
          <Skeleton className="h-4 w-80 max-w-full" />
        </div>
        <Skeleton className="h-11 w-full rounded-xl" />
        <div className="flex flex-wrap gap-2">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-8 w-28 rounded-full" />
          ))}
        </div>
      </div>
    </div>
  )
}
