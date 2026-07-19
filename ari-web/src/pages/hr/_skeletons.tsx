import { Skeleton } from '@components/ui/Skeleton'

/**
 * Bộ skeleton loading cho khu HR Leader — khung "giả lập" có shimmer chuyển động,
 * khớp đúng bố cục từng màn (stats grid + list/table) để khi data về không bị nhảy layout.
 */

const card =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card'

/** 4 ô chỉ số — khớp `StatsGrid` (grid-cols-2 lg:grid-cols-4 gap-4 mb-8). */
export function HrStatsSkeleton() {
  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className={`${card} p-5`}>
          <Skeleton className="mb-3 h-4 w-24" />
          <Skeleton className="h-7 w-12" />
        </div>
      ))}
    </div>
  )
}

/** Danh sách tin tuyển dụng (JobsPage / PendingJobsPage) — các hàng card ngang. */
export function JobListSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-4">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className={`${card} p-6`}>
          <div className="flex items-center justify-between gap-4">
            <div className="flex min-w-0 items-center gap-4">
              <Skeleton className="h-12 w-12 shrink-0 rounded-xl" />
              <div className="min-w-0">
                <div className="mb-2 flex items-center gap-3">
                  <Skeleton className="h-5 w-44" />
                  <Skeleton className="h-5 w-20 rounded-full" />
                </div>
                <div className="flex flex-wrap items-center gap-3">
                  <Skeleton className="h-3.5 w-24" />
                  <Skeleton className="h-3.5 w-20" />
                  <Skeleton className="h-3.5 w-28" />
                </div>
              </div>
            </div>
            <Skeleton className="h-9 w-24 shrink-0 rounded-xl" />
          </div>
        </div>
      ))}
    </div>
  )
}

/** Bảng ứng viên (CandidatesPage) — header + các hàng có avatar. */
export function CandidatesTableSkeleton({ rows = 6 }: { rows?: number }) {
  return (
    <div className={`${card} p-2 sm:p-4`}>
      <div className="divide-y divide-ink-100 dark:divide-white/5">
        {Array.from({ length: rows }).map((_, i) => (
          <div key={i} className="flex items-center gap-4 px-4 py-4">
            <Skeleton className="h-10 w-10 shrink-0 rounded-full" />
            <div className="min-w-0 flex-1">
              <Skeleton className="mb-1.5 h-4 w-40" />
              <Skeleton className="h-3 w-52" />
            </div>
            <Skeleton className="hidden h-4 w-28 sm:block" />
            <Skeleton className="h-6 w-24 rounded-full" />
            <Skeleton className="hidden h-4 w-8 md:block" />
            <Skeleton className="hidden h-4 w-20 lg:block" />
            <Skeleton className="h-8 w-16 rounded-lg" />
          </div>
        ))}
      </div>
    </div>
  )
}

/** Danh sách phiên phỏng vấn (InterviewSessionsPage) — card có avatar + badges. */
export function SessionListSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-4">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className={`${card} p-5 lg:p-6`}>
          <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-center">
            <div className="flex min-w-0 items-center gap-4">
              <Skeleton className="h-12 w-12 shrink-0 rounded-full" />
              <div className="min-w-0">
                <div className="mb-2 flex items-center gap-2">
                  <Skeleton className="h-4 w-40" />
                  <Skeleton className="h-4 w-14 rounded-md" />
                </div>
                <Skeleton className="h-3.5 w-32" />
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-3 lg:justify-end">
              <Skeleton className="h-4 w-20" />
              <Skeleton className="h-6 w-20 rounded-full" />
              <Skeleton className="h-8 w-20 rounded-xl" />
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

/** Danh sách đánh giá (EvaluationReviewPage) — card có avatar vuông + meta. */
export function EvaluationListSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-4">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className={`${card} p-6`}>
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="flex items-center gap-4">
              <Skeleton className="h-14 w-14 shrink-0 rounded-2xl" />
              <div>
                <Skeleton className="mb-2 h-5 w-40" />
                <div className="flex items-center gap-3">
                  <Skeleton className="h-3.5 w-24" />
                  <Skeleton className="h-3.5 w-20" />
                </div>
              </div>
            </div>
            <div className="flex items-center gap-3">
              <Skeleton className="h-6 w-20 rounded-full" />
              <Skeleton className="h-10 w-10 rounded-xl" />
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

/** Lưới card (PlaybooksPage) — 6 card cao. */
export function CardGridSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className={`${card} p-5`}>
          <div className="mb-3 flex items-start justify-between">
            <Skeleton className="h-11 w-11 rounded-xl" />
            <Skeleton className="h-5 w-24 rounded-full" />
          </div>
          <Skeleton className="mb-2 h-4 w-32" />
          <Skeleton className="h-3 w-40" />
          <div className="mt-4 flex items-center justify-between border-t border-ink-100 pt-3 dark:border-white/10">
            <Skeleton className="h-5 w-20 rounded-full" />
            <Skeleton className="h-3 w-10" />
          </div>
        </div>
      ))}
    </div>
  )
}

/** Danh sách yêu cầu tài khoản (TeamPage) — hàng có avatar + badges. */
export function RequestListSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className={`${card} p-4`}>
          <div className="flex items-center justify-between gap-3">
            <div className="flex min-w-0 items-center gap-3">
              <Skeleton className="h-11 w-11 shrink-0 rounded-full" />
              <div className="min-w-0">
                <Skeleton className="mb-1.5 h-4 w-36" />
                <Skeleton className="h-3 w-48" />
              </div>
            </div>
            <div className="flex items-center gap-3">
              <Skeleton className="h-5 w-20 rounded-full" />
              <Skeleton className="h-5 w-24 rounded-full" />
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

/**
 * Thân Dashboard HR (KPI + phễu/biểu đồ + danh sách) — KHÔNG kèm header/padding
 * vì trang đã tự render greeting header + wrapper `space-y-6`.
 */
export function HrDashboardSkeleton() {
  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className={`${card} p-5`}>
            <Skeleton className="mb-3 h-9 w-9 rounded-xl" />
            <Skeleton className="mb-2 h-7 w-12" />
            <Skeleton className="h-3.5 w-24" />
          </div>
        ))}
      </div>
      <div className="grid gap-6 lg:grid-cols-3">
        <div className={`${card} p-6 lg:col-span-2`}>
          <Skeleton className="mb-5 h-5 w-40" />
          <div className="space-y-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="flex items-center gap-4">
                <Skeleton className="h-4 w-28" />
                <Skeleton className="h-3 flex-1 rounded-full" />
                <Skeleton className="h-4 w-10" />
              </div>
            ))}
          </div>
        </div>
        <div className={`${card} p-6`}>
          <Skeleton className="mb-5 h-5 w-32" />
          <div className="flex h-40 items-end gap-1.5">
            {[
              'h-1/3',
              'h-1/2',
              'h-2/3',
              'h-1/4',
              'h-3/4',
              'h-1/2',
              'h-full',
              'h-2/3',
              'h-1/3',
              'h-1/2',
              'h-3/4',
              'h-1/4',
              'h-2/3',
              'h-1/2',
            ].map((h, i) => (
              <Skeleton key={i} className={`flex-1 ${h}`} />
            ))}
          </div>
        </div>
      </div>
      <div className="grid gap-6 lg:grid-cols-2">
        <JobListSkeleton rows={3} />
        <div className={`${card} p-6`}>
          <Skeleton className="mb-5 h-5 w-36" />
          <div className="space-y-3">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full rounded-xl" />
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
