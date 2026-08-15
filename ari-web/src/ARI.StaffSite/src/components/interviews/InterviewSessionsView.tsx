import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQueries, useQuery } from '@tanstack/react-query'
import { Briefcase, CalendarDays, ChevronLeft, ChevronRight, Filter, Search, X } from 'lucide-react'
import { EmptyState, ErrorAlert, PageHeader, Select } from '@ari/shared/ui'
import { interviewService, type InterviewSlotDetail } from '@ari/shared/fservices/interview'
import { JobCard } from './JobCard'
import { interviewKeys } from './interviewQueryKeys'
import { fmtDate, isSameDay } from './format'
import { INTERVIEWS_NS, WORKSPACES, type WorkspaceVariant } from './workspaceConfig'

const PAGE_SIZE = 5

/**
 * Màn "Phỏng vấn" dùng chung cho HR và Recruiter.
 *
 * Trước đây là HAI file ~1200 dòng chép của nhau và đã trôi khỏi nhau: bản Recruiter không lọc ca
 * đích theo sức chứa và coi mọi lần gọi không ném là thành công (báo "đã dời" cả khi server từ
 * chối), còn bản HR thì thiếu hẳn banner lý do từ chối. `variant` chỉ diễn đạt khác biệt về câu chữ
 * và đường dẫn — xem `workspaceConfig.ts`.
 */
export function InterviewSessionsView({ variant }: { variant: WorkspaceVariant }) {
  const { t } = useTranslation(INTERVIEWS_NS)
  const workspace = WORKSPACES[variant]
  const [searchParams, setSearchParams] = useSearchParams()

  const currentPage = Number(searchParams.get('page')) || 1
  const search = searchParams.get('search') || ''
  const jobStatusFilter = (searchParams.get('status') as 'active' | 'closed') || 'active'
  const dateFilter = searchParams.get('date') || ''

  const updateParam = (key: string, value: string) => {
    setSearchParams(
      (prev) => {
        const p = new URLSearchParams(prev)
        if (!value || value === 'active') p.delete(key)
        else p.set(key, value)
        p.delete('page')
        return p
      },
      { replace: true }
    )
  }

  const handlePageChange = (newPage: number) => {
    setSearchParams(
      (prev) => {
        const p = new URLSearchParams(prev)
        if (newPage > 1) p.set('page', String(newPage))
        else p.delete('page')
        return p
      },
      { replace: true }
    )
  }

  // Một khoá cho cả hai vai trò: server đã lọc theo quyền nên không cần gọi thêm API hồ sơ để tự
  // lọc phía giao diện như bản Recruiter cũ (nhánh dự phòng của nó còn cho thấy TẤT CẢ tin).
  const {
    data: jobs = [],
    isLoading: loading,
    error: fetchError,
  } = useQuery({
    queryKey: interviewKeys.jobs,
    queryFn: () => interviewService.getInterviewJobs(),
    staleTime: 60_000,
    placeholderData: keepPreviousData,
  })

  const error = fetchError ? t('view.loadError') : null

  const statusFiltered = useMemo(() => {
    let list = jobs
    if (jobStatusFilter === 'active') {
      list = list.filter((j) => j.jobStatus === 'active' || j.jobStatus === 'published')
    } else {
      list = list.filter((j) => j.jobStatus !== 'active' && j.jobStatus !== 'published')
    }
    const q = search.trim().toLowerCase()
    return q ? list.filter((j) => j.jobTitle.toLowerCase().includes(q)) : list
  }, [jobs, search, jobStatusFilter])

  // Lọc theo ngày cần biết ca của từng tin. Dùng CHUNG khoá với JobCard nên mở thẻ ra là có sẵn,
  // không gọi lại lần nữa.
  const slotQueries = useQueries({
    queries: statusFiltered.map((j) => ({
      queryKey: interviewKeys.slots(j.jobId),
      queryFn: () => interviewService.getSlotsForJob(j.jobId),
      enabled: !!dateFilter && j.totalSlots > 0,
      staleTime: 30_000,
    })),
  })

  const loadingSlotsForFilter = !!dateFilter && slotQueries.some((q) => q.isLoading)

  const filtered = useMemo(() => {
    if (!dateFilter) return statusFiltered
    return statusFiltered.filter((j, i) => {
      const slots = slotQueries[i]?.data as InterviewSlotDetail[] | undefined
      if (slots) return slots.some((s) => isSameDay(s.startTime, dateFilter))
      // Chưa tải xong thì giữ lại tin để danh sách không nhấp nháy.
      return j.nextSlotTime ? isSameDay(j.nextSlotTime, dateFilter) : true
    })
  }, [statusFiltered, dateFilter, slotQueries])

  const totalPages = Math.ceil(filtered.length / PAGE_SIZE) || 1
  const paginatedJobs = useMemo(
    () => filtered.slice((currentPage - 1) * PAGE_SIZE, (currentPage - 1) * PAGE_SIZE + PAGE_SIZE),
    [filtered, currentPage]
  )

  const totalSlots = jobs.reduce((s, j) => s + j.totalSlots, 0)
  const totalBooked = jobs.reduce((s, j) => s + j.totalBooked, 0)
  const totalConfirmed = jobs.reduce((s, j) => s + j.totalConfirmed, 0)

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title={t(workspace.titleKey)} description={t(workspace.descriptionKey)} />

      {error && <ErrorAlert message={error} />}

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        {[
          { label: t(workspace.jobsStatKey), value: jobs.length, color: 'text-blue-600 dark:text-blue-400' },
          { label: t('stats.slots'), value: totalSlots, color: 'text-brand-600 dark:text-brand-400' },
          { label: t('stats.booked'), value: totalBooked, color: 'text-ai-600 dark:text-ai-400' },
          { label: t('stats.confirmed'), value: totalConfirmed, color: 'text-emerald-600 dark:text-emerald-400' },
        ].map((stat) => (
          <div
            key={stat.label}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
          >
            <p className={`text-2xl font-extrabold ${stat.color}`}>{loading ? '—' : stat.value}</p>
            <p className="text-sm font-medium text-ink-500 dark:text-ink-400 mt-1">{stat.label}</p>
          </div>
        ))}
      </div>

      <div className="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-ink-400" />
          <input
            value={search}
            onChange={(e) => updateParam('search', e.target.value)}
            placeholder={t(workspace.searchPlaceholderKey)}
            className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-900 dark:text-white placeholder:text-ink-400 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500/40"
          />
        </div>

        <div className="flex items-center gap-2 flex-wrap shrink-0">
          <div className="relative flex items-center">
            <CalendarDays className="absolute left-3 w-4 h-4 text-ink-400 pointer-events-none" />
            <input
              type="date"
              value={dateFilter}
              onChange={(e) => updateParam('date', e.target.value)}
              className="pl-9 pr-8 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-800 dark:text-ink-100 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-brand-500/40"
            />
            {dateFilter && (
              <button
                onClick={() => updateParam('date', '')}
                title={t('view.clearDateTitle')}
                className="absolute right-2 p-1 text-ink-400 hover:text-ink-600 dark:hover:text-white"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>

          <div className="flex items-center gap-1.5">
            <Filter className="w-4 h-4 text-ink-400" />
            <Select
              value={jobStatusFilter}
              onChange={(v) => updateParam('status', v)}
              options={[
                { value: 'active', label: t('view.statusActive') },
                { value: 'closed', label: t('view.statusClosed') },
              ]}
              className="min-w-[12rem]"
              buttonClassName="px-3 py-2 text-xs font-semibold"
            />
          </div>
        </div>
      </div>

      {dateFilter && (
        <div className="mb-4 p-3 rounded-xl bg-brand-50 dark:bg-brand-500/10 border border-brand-200 dark:border-brand-500/20 text-xs text-brand-700 dark:text-brand-300 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CalendarDays className="w-4 h-4 shrink-0" />
            <span>
              {t('view.dateBanner')}{' '}
              <strong className="underline">{fmtDate(dateFilter)}</strong>
            </span>
            {loadingSlotsForFilter && (
              <span className="text-[11px] italic animate-pulse">{t('view.scanning')}</span>
            )}
          </div>
          <button onClick={() => updateParam('date', '')} className="font-semibold text-brand-800 hover:underline">
            {t('view.clearDate')}
          </button>
        </div>
      )}

      {loading ? (
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 animate-pulse"
            >
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-xl bg-ink-100 dark:bg-white/10" />
                <div className="flex-1 space-y-2">
                  <div className="h-4 bg-ink-100 dark:bg-white/10 rounded w-48" />
                  <div className="h-3 bg-ink-100 dark:bg-white/10 rounded w-72" />
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={<Briefcase className="w-7 h-7 text-ink-400" />}
          title={jobs.length === 0 ? t(workspace.emptyTitleKey) : t('view.noMatchTitle')}
          description={
            jobs.length === 0 ? t(workspace.emptyDescriptionKey) : t('view.noMatchDescription')
          }
        />
      ) : (
        <>
          <div className="space-y-4">
            {paginatedJobs.map((job) => (
              <JobCard key={job.jobId} job={job} dateFilter={dateFilter} workspace={workspace} />
            ))}
          </div>

          {totalPages > 1 && (
            <div className="mt-8 flex flex-col sm:flex-row items-center justify-between gap-4 pt-4 border-t border-ink-200 dark:border-white/10">
              <p className="text-xs text-ink-500 dark:text-ink-400 font-medium">
                {t('view.pagination', {
                  shown: paginatedJobs.length,
                  total: filtered.length,
                  page: currentPage,
                  pages: totalPages,
                })}
              </p>
              <div className="flex items-center gap-1.5">
                <button
                  disabled={currentPage === 1}
                  onClick={() => handlePageChange(Math.max(1, currentPage - 1))}
                  className="p-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 disabled:opacity-40 transition-colors"
                  title={t('view.prevPage')}
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>

                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <button
                    key={page}
                    onClick={() => handlePageChange(page)}
                    className={`w-8 h-8 rounded-xl text-xs font-bold transition-all ${
                      currentPage === page
                        ? 'bg-brand-600 text-white shadow-sm'
                        : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10'
                    }`}
                  >
                    {page}
                  </button>
                ))}

                <button
                  disabled={currentPage === totalPages}
                  onClick={() => handlePageChange(Math.min(totalPages, currentPage + 1))}
                  className="p-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10 disabled:opacity-40 transition-colors"
                  title={t('view.nextPage')}
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  )
}
