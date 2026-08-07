import { useMemo, useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  Users,
  MapPin,
  Briefcase,
  Building2,
  Calendar,
  Languages,
  Zap,
  User,
  Search,
  Filter,
  X,
} from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert, Pagination } from '@ari/shared/ui'
import { HrStatsSkeleton, JobListSkeleton } from './_skeletons'
import { jobService } from '@ari/shared/fservices/job'
import { useAuthStore } from '@ari/shared/store/auth'

function formatDate(iso?: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function getDeadlineText(
  deadlineStr: string | null | undefined,
  t: (key: string, options?: Record<string, unknown>) => string
): string {
  if (!deadlineStr) return ''
  const d = new Date(deadlineStr)
  if (Number.isNaN(d.getTime())) return ''
  const formattedDate = d.toLocaleDateString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const target = new Date(d)
  target.setHours(0, 0, 0, 0)
  const diffDays = Math.round((target.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))
  if (diffDays < 0) return `${formattedDate} (${t('deadline.expired')})`
  if (diffDays === 0) return `${formattedDate} (${t('deadline.today')})`
  return `${formattedDate} (${t('deadline.daysLeft', { count: diffDays })})`
}

export default function HrJobsPage() {
  const { t } = useTranslation('modules/hr/jobs')
  const user = useAuthStore((s) => s.user)
  const {
    data: jobsData,
    isLoading: loading,
    error: fetchError,
  } = useQuery({
    queryKey: ['admin-jobs'],
    queryFn: () => jobService.getAdminJobPostings(),
    refetchOnWindowFocus: false,
  })

  const jobs = jobsData || []
  const error =
    (fetchError as any)?.response?.data?.message || (fetchError ? t('loadingError') : '')

  const [searchParams, setSearchParams] = useSearchParams()

  const page = Number(searchParams.get('page')) || 1
  const searchTerm = searchParams.get('search') || ''
  const selectedStatus = searchParams.get('status') || 'all'
  const selectedEmployee = searchParams.get('employee') || 'all'
  const selectedUrgent = searchParams.get('urgent') || 'all'
  const fromDate = searchParams.get('fromDate') || ''
  const toDate = searchParams.get('toDate') || ''

  const updateParam = (key: string, value: string) => {
    setSearchParams((prev) => {
      const p = new URLSearchParams(prev)
      if (!value || value === 'all') {
        p.delete(key)
      } else {
        p.set(key, value)
      }
      p.delete('page')
      return p
    }, { replace: true })
  }

  const handlePageChange = (newPage: number) => {
    setSearchParams((prev) => {
      const p = new URLSearchParams(prev)
      if (newPage > 1) p.set('page', String(newPage))
      else p.delete('page')
      return p
    }, { replace: true })
  }

  const statusMeta: Record<string, { label: string; badge: string }> = {
    active: {
      label: t('status.active'),
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    pending: {
      label: t('status.pending'),
      badge: 'bg-indigo-100 dark:bg-indigo-500/20 text-indigo-700 dark:text-indigo-400',
    },
    draft: {
      label: t('status.draft'),
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    paused: {
      label: t('status.paused'),
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    closed: {
      label: t('status.closed'),
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400',
    },
    rejected: {
      label: t('status.rejected'),
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
  }

  // Extract unique creator/recruiter names for filter dropdown
  const uniqueEmployees = useMemo(() => {
    const set = new Set<string>()
    jobs.forEach((j) => {
      if (j.createdByName) set.add(j.createdByName)
    })
    return Array.from(set)
  }, [jobs])

  const getEffectiveStatus = useCallback((j: any) => {
    if (j.status === 'active' && j.applicationDeadline && new Date(j.applicationDeadline).getTime() < Date.now()) {
      return 'closed'
    }
    return j.status
  }, [])

  const stats = useMemo(() => {
    // Bao gồm các tin không phải nháp VÀ các tin nháp của chính HR Admin này
    const validJobs = jobs.filter(
      (j) => getEffectiveStatus(j) !== 'draft' || j.createdByUserId === user?.id
    )
    const count = (s: string) => validJobs.filter((j) => getEffectiveStatus(j) === s).length
    return [
      { label: t('stats.total'), value: validJobs.length, color: 'text-blue-600 dark:text-blue-400' },
      {
        label: t('status.active'),
        value: count('active'),
        color: 'text-emerald-600 dark:text-emerald-400',
      },
      {
        label: t('status.pending'),
        value: count('pending'),
        color: 'text-indigo-600 dark:text-indigo-400',
      },
      {
        label: t('status.draft'),
        value: count('draft'),
        color: 'text-ink-500 dark:text-ink-400',
      },
      {
        label: t('status.closed'),
        value: count('closed'),
        color: 'text-ink-600 dark:text-ink-400',
      },
    ]
  }, [jobs, t, getEffectiveStatus, user?.id])

  const filtered = useMemo(() => {
    return jobs.filter((j) => {
      const effectiveStatus = getEffectiveStatus(j)

      // Chỉ hiển thị tin nháp của chính mình
      if (effectiveStatus === 'draft' && j.createdByUserId !== user?.id) return false

      // 1. Search term
      if (searchTerm.trim()) {
        const q = searchTerm.toLowerCase().trim()
        const titleMatch = j.title?.toLowerCase().includes(q)
        const deptMatch = j.department?.toLowerCase().includes(q)
        const locMatch = j.location?.toLowerCase().includes(q)
        const creatorMatch = j.createdByName?.toLowerCase().includes(q)
        if (!titleMatch && !deptMatch && !locMatch && !creatorMatch) return false
      }

      // 2. Status filter
      if (selectedStatus !== 'all' && effectiveStatus !== selectedStatus) return false

      // 3. Employee filter
      if (selectedEmployee !== 'all' && j.createdByName !== selectedEmployee) return false

      // 4. Urgent filter
      if (selectedUrgent === 'urgent' && !j.isUrgent) return false
      if (selectedUrgent === 'normal' && j.isUrgent) return false

      // 5. Date range filter
      if (fromDate) {
        const createdDate = new Date(j.createdAt)
        const start = new Date(fromDate)
        start.setHours(0, 0, 0, 0)
        if (createdDate < start) return false
      }
      if (toDate) {
        const createdDate = new Date(j.createdAt)
        const end = new Date(toDate)
        end.setHours(23, 59, 59, 999)
        if (createdDate > end) return false
      }

      return true
    })
  }, [jobs, searchTerm, selectedStatus, selectedEmployee, selectedUrgent, fromDate, toDate])

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const paged = useMemo(
    () => filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [filtered, page]
  )

  const hasActiveFilters =
    searchTerm ||
    selectedStatus !== 'all' ||
    selectedEmployee !== 'all' ||
    selectedUrgent !== 'all' ||
    fromDate ||
    toDate

  const clearFilters = () => {
    setSearchParams((prev) => {
      const p = new URLSearchParams(prev)
      p.delete('search')
      p.delete('status')
      p.delete('employee')
      p.delete('urgent')
      p.delete('fromDate')
      p.delete('toDate')
      p.delete('page')
      return p
    }, { replace: true })
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title={t('title')} description={t('subtitle')} />

      {loading && <HrStatsSkeleton />}
      {!loading && !error && <StatsGrid stats={stats} />}

      {/* Unified Filter Bar */}
      {!loading && !error && jobs.length > 0 && (
        <div className="mb-6 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
              <Filter className="w-4 h-4 text-brand-600 dark:text-brand-400" />
              <span>Bộ lọc tin tuyển dụng</span>
            </div>
            {hasActiveFilters && (
              <button
                type="button"
                onClick={clearFilters}
                className="flex items-center gap-1.5 text-xs font-medium text-red-600 dark:text-red-400 hover:underline"
              >
                <X className="w-3.5 h-3.5" /> Xóa bộ lọc
              </button>
            )}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-3">
            {/* Search Input */}
            <div className="relative xl:col-span-2">
              <Search className="w-4 h-4 absolute left-3 top-3 text-ink-400" />
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => updateParam('search', e.target.value)}
                placeholder={t('filters.search')}
                className="w-full pl-9 pr-3 py-2 text-sm rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              />
            </div>

            {/* Status Filter */}
            <div>
              <select
                value={selectedStatus}
                onChange={(e) => updateParam('status', e.target.value)}
                className="w-full px-3 py-2 text-sm rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              >
                <option value="all">Tất cả trạng thái</option>
                <option value="active">{t('status.active')}</option>
                <option value="pending">{t('status.pending')}</option>
                <option value="draft">{t('status.draft')}</option>
                <option value="paused">{t('status.paused')}</option>
                <option value="closed">{t('status.closed')}</option>
                <option value="rejected">{t('status.rejected')}</option>
              </select>
            </div>

            {/* Employee Filter */}
            <div>
              <select
                value={selectedEmployee}
                onChange={(e) => updateParam('employee', e.target.value)}
                className="w-full px-3 py-2 text-sm rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              >
                <option value="all">Lọc theo người phụ trách</option>
                {uniqueEmployees.map((emp) => (
                  <option key={emp} value={emp}>
                    {emp}
                  </option>
                ))}
              </select>
            </div>

            {/* Urgent Filter */}
            <div>
              <select
                value={selectedUrgent}
                onChange={(e) => updateParam('urgent', e.target.value)}
                className="w-full px-3 py-2 text-sm rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              >
                <option value="all">Tất cả mức độ</option>
                <option value="urgent">Tin tuyển gấp</option>
                <option value="normal">Bình thường</option>
              </select>
            </div>

            {/* Date Range: From Date */}
            <div>
              <input
                type="date"
                value={fromDate}
                title="Từ ngày tạo"
                onChange={(e) => updateParam('fromDate', e.target.value)}
                className="w-full px-3 py-2 text-sm rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              />
            </div>
          </div>
        </div>
      )}

      {loading && <JobListSkeleton rows={5} />}
      {!loading && error && <ErrorAlert message={error} />}

      {!loading && !error && filtered.length === 0 && (
        <EmptyState
          icon={<Briefcase className="w-8 h-8 text-ink-400" />}
          title={jobs.length === 0 ? t('noJobs') : t('noMatchingJobs')}
          description={jobs.length === 0 ? t('noJobsHint') : t('noMatchingJobsHint')}
        />
      )}

      {!loading && !error && filtered.length > 0 && (
        <div className="space-y-4">
          {paged.map((job, index) => {
            const meta = statusMeta[job.status] ?? statusMeta.draft
            return (
              <motion.div
                key={job.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(index * 0.04, 0.3) }}
                className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card hover:shadow-card-hover transition-all"
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
                  <div className="flex items-center gap-3 min-w-0 sm:gap-4">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center text-white font-semibold shrink-0 sm:w-12 sm:h-12">
                      {job.title.charAt(0).toUpperCase()}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 mb-1 flex-wrap sm:gap-3">
                        <h3 className="text-base font-semibold text-ink-900 dark:text-white truncate sm:text-lg">
                          {job.title}
                        </h3>
                        <span
                          className={`px-2.5 py-0.5 rounded-full text-xs font-medium ${
                            job.status === 'active' && job.applicationDeadline && new Date(job.applicationDeadline).getTime() < Date.now()
                              ? 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400'
                              : meta.badge
                          }`}
                        >
                          {job.status === 'active' && job.applicationDeadline && new Date(job.applicationDeadline).getTime() < Date.now()
                            ? 'Hết hạn (Đã đóng)'
                            : meta.label}
                        </span>
                        {job.isUrgent && (
                          <span className="px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-100 dark:bg-red-500/20 text-red-600 dark:text-red-400 flex items-center gap-1">
                            <Zap className="w-3 h-3" /> {t('urgent')}
                          </span>
                        )}
                      </div>
                      <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-sm text-ink-600 dark:text-ink-400">
                        {/* Employee Column / Badge */}
                        <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-lg bg-brand-50 dark:bg-brand-500/10 text-brand-700 dark:text-brand-300 font-medium text-xs">
                          <User className="w-3.5 h-3.5 text-brand-600 dark:text-brand-400" />
                          <span>Thực hiện: {job.createdByName || '—'}</span>
                        </span>

                        {job.department && (
                          <span className="flex items-center gap-1">
                            <Building2 className="w-4 h-4" />
                            {job.department}
                          </span>
                        )}
                        {job.location && (
                          <span className="flex items-center gap-1">
                            <MapPin className="w-4 h-4" />
                            {job.location}
                          </span>
                        )}
                        {job.languageRequirement && (
                          <span className="flex items-center gap-1">
                            <Languages className="w-4 h-4" />
                            {job.languageRequirement}
                          </span>
                        )}
                        <span className="flex items-center gap-1">
                          <Users className="w-4 h-4" />
                          {job.applicantCount ?? 0} {t('applicants')}
                        </span>
                        {job.applicationDeadline && (
                          <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400 font-medium">
                            <Calendar className="w-4 h-4" />
                            {t('deadlineLabel')}: {getDeadlineText(job.applicationDeadline, t)}
                          </span>
                        )}
                        <span className="flex items-center gap-1">
                          <Calendar className="w-4 h-4" />
                          {formatDate(job.createdAt)}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    <Link
                      to={`/hr/jobs/${job.id}`}
                      className="px-4 py-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors text-sm font-medium"
                    >
                      {t('viewDetails')}
                    </Link>
                  </div>
                </div>
              </motion.div>
            )
          })}
        </div>
      )}

      {!loading && !error && filtered.length > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          total={filtered.length}
          label={t('paginationLabel')}
          onPageChange={handlePageChange}
        />
      )}
    </div>
  )
}
