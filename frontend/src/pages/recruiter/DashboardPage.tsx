import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Briefcase, Plus, Users, FileText, ArrowRight, MapPin, AlertCircle, Send } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, EmptyState } from '@components/shared'
import { useAuthStore } from '@store/auth/authStore'
import jobService from '@services/job/jobService'
import type { JobPosting } from '@/types/job'
import { jobStatusBadge, jobStatusLabel, formatSalary, timeAgo } from './_jobUi'
import { DashboardSkeleton } from './_skeletons'

export default function RecruiterDashboardPage() {
  const { user } = useAuthStore()
  const { data: jobsData, isLoading: loading, error: fetchError } = useQuery({
    queryKey: ['my-jobs'],
    queryFn: () => jobService.getMyJobPostings(),
    refetchOnWindowFocus: false,
  })

  const jobs = jobsData || []
  const error = (fetchError as any)?.response?.data?.message || (fetchError ? 'Không tải được dữ liệu tin tuyển dụng.' : '')

  const stats = useMemo(() => {
    const by = (s: string) => jobs.filter((j) => j.status === s).length
    const applicants = jobs.reduce((sum, j) => sum + (j.applicantCount ?? 0), 0)
    return { total: jobs.length, active: by('active'), pending: by('pending'), rejected: by('rejected'), draft: by('draft'), applicants }
  }, [jobs])

  const rejected = useMemo(() => jobs.filter((j) => j.status === 'rejected'), [jobs])
  const drafts = useMemo(() => jobs.filter((j) => j.status === 'draft'), [jobs])
  const recent = useMemo(() => [...jobs].slice(0, 6), [jobs])

  if (loading) return <DashboardSkeleton />

  const statCards = [
    { label: 'Tin của tôi', value: stats.total, color: 'text-brand-600' },
    { label: 'Đang đăng', value: stats.active, color: 'text-emerald-600' },
    { label: 'Chờ HR duyệt', value: stats.pending, color: 'text-amber-600' },
    { label: 'Tổng ứng viên', value: stats.applicants, color: 'text-ai-600' },
  ]

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title={`Xin chào, ${user?.name || 'Recruiter'}`}
        description="Tổng quan tin tuyển dụng & ứng viên của bạn"
        actions={[{ label: 'Tạo tin', href: '/recruiter/jobs/create', variant: 'primary' }]}
      />

      {error && <ErrorAlert message={error} />}

      <StatsGrid stats={statCards} />

      {rejected.length > 0 && (
        <div className="mb-6 rounded-2xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-4">
          <div className="flex items-center gap-2 text-sm font-semibold text-red-700 dark:text-red-400">
            <AlertCircle className="h-4 w-4" /> {rejected.length} tin bị từ chối — chỉnh sửa và gửi lại để HR duyệt
          </div>
          <div className="mt-2 space-y-1">
            {rejected.slice(0, 3).map((j) => (
              <Link key={j.id} to={`/recruiter/my-jobs/${j.id}`} className="block text-xs text-red-600 dark:text-red-300 hover:underline">
                {j.title}{j.rejectionReason ? ` — lý do: ${j.rejectionReason}` : ''}
              </Link>
            ))}
          </div>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3 lg:gap-8">
        {/* Main: tin tuyển dụng */}
        <div className="lg:col-span-2">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
              <Briefcase className="h-5 w-5 text-brand-600 dark:text-brand-400" /> Tin tuyển dụng của tôi
            </h2>
            <Link to="/recruiter/my-jobs" className="text-xs font-medium text-brand-600 dark:text-brand-400 hover:underline">
              Xem tất cả
            </Link>
          </div>

          {recent.length === 0 ? (
            <EmptyState
              icon={<Briefcase className="h-8 w-8 text-ink-400" />}
              title="Chưa có tin tuyển dụng"
              description="Tạo tin đầu tiên — upload JD để hệ thống tự điền các trường giúp bạn."
              action={{ label: 'Tạo tin', href: '/recruiter/jobs/create' }}
            />
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              {recent.map((j, i) => (
                <motion.div
                  key={j.id}
                  initial={{ opacity: 0, y: 16 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: i * 0.04 }}
                >
                  <Link
                    to={`/recruiter/my-jobs/${j.id}`}
                    className="group block h-full rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card transition-all hover:border-brand-300 dark:hover:border-brand-500/40 hover:shadow-card-hover"
                  >
                    <div className="mb-3 flex items-start justify-between gap-3">
                      <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-50 dark:bg-brand-500/15 text-brand-600 dark:text-brand-400">
                        <Briefcase className="h-5 w-5" />
                      </span>
                      <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${jobStatusBadge(j.status)}`}>
                        {jobStatusLabel(j.status)}
                      </span>
                    </div>
                    <h3 className="truncate font-semibold text-ink-900 dark:text-white group-hover:text-brand-600 dark:group-hover:text-brand-400">
                      {j.title}
                    </h3>
                    <p className="mt-0.5 flex items-center gap-1 text-xs text-ink-500 dark:text-ink-400">
                      {j.department || 'Chưa phân phòng ban'}
                      {j.location ? <><span>·</span><MapPin className="h-3 w-3" />{j.location}</> : null}
                    </p>
                    <div className="mt-4 flex items-center justify-between border-t border-ink-100 dark:border-white/10 pt-3 text-xs">
                      <span className="flex items-center gap-1.5 font-medium text-ink-600 dark:text-ink-300">
                        <Users className="h-3.5 w-3.5" /> {j.applicantCount ?? 0} ứng viên
                      </span>
                      <span className="text-ink-400">{formatSalary(j)}</span>
                    </div>
                  </Link>
                </motion.div>
              ))}
            </div>
          )}
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <h3 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">Thao tác nhanh</h3>
            <div className="space-y-2">
              {[
                { to: '/recruiter/jobs/create', icon: Plus, label: 'Tạo tin mới', tint: 'text-brand-600 dark:text-brand-400 bg-brand-100 dark:bg-brand-500/20' },
                { to: '/recruiter/my-jobs', icon: Briefcase, label: 'Quản lý tin', tint: 'text-emerald-600 dark:text-emerald-400 bg-emerald-100 dark:bg-emerald-500/20' },
                { to: '/recruiter/candidates', icon: Users, label: 'Xem ứng viên', tint: 'text-ai-600 dark:text-ai-400 bg-ai-100 dark:bg-ai-500/20' },
              ].map((a) => (
                <Link
                  key={a.to}
                  to={a.to}
                  className="flex items-center gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3 hover:border-brand-300 dark:hover:border-brand-500/40 hover:bg-ink-50 dark:hover:bg-white/5"
                >
                  <span className={`grid h-8 w-8 place-items-center rounded-lg ${a.tint}`}>
                    <a.icon className="h-4 w-4" />
                  </span>
                  <span className="flex-1 text-sm text-ink-700 dark:text-ink-200">{a.label}</span>
                  <ArrowRight className="h-4 w-4 text-ink-400" />
                </Link>
              ))}
            </div>
          </div>

          {drafts.length > 0 && (
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
              <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
                <FileText className="h-4 w-4 text-ink-400" /> Bản nháp chờ gửi duyệt
              </h3>
              <div className="space-y-2">
                {drafts.slice(0, 4).map((j) => (
                  <Link
                    key={j.id}
                    to={`/recruiter/my-jobs/${j.id}`}
                    className="flex items-center justify-between gap-2 rounded-xl border border-ink-100 dark:border-white/10 p-3 hover:bg-ink-50 dark:hover:bg-white/5"
                  >
                    <span className="min-w-0 flex-1 truncate text-sm text-ink-700 dark:text-ink-200">{j.title}</span>
                    <span className="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400">
                      <Send className="h-3 w-3" /> {timeAgo(j.createdAt)}
                    </span>
                  </Link>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
