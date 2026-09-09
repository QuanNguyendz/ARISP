import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Briefcase, MapPin, Users, FileSignature } from 'lucide-react'
import { PageHeader, EmptyState, ErrorAlert, LoadingSpinner } from '@ari/shared/ui'
import { jobService } from '@ari/shared/fservices/job'
import { formatSalary } from '@/components/hiring/hiringConfig'

const STATUS_CLASSES: Record<string, string> = {
  active: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
  pending: 'bg-indigo-100 text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-400',
  draft: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300',
  closed: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  rejected: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400',
}

/**
 * Tin tuyển dụng mà Hiring Manager được gán vào đội (ADR-061).
 *
 * KHÔNG lọc gì ở client: server chỉ trả về tin trong phạm vi của người đang đăng nhập
 * (JobAccess.ScopedJobIdsAsync). Lọc thêm ở đây sẽ tạo ra một tầng phân quyền thứ hai có thể
 * trôi khỏi tầng thật.
 */
export default function MyTeamJobsPage() {
  const { t } = useTranslation('modules/hm/jobs')

  const {
    data: jobs,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['hm-jobs'],
    queryFn: () => jobService.getAdminJobPostings(),
  })

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="space-y-6">
        <PageHeader title={t('title')} description={t('description')} />

        {error && <ErrorAlert message={t('loadError')} />}

        {isLoading ? (
          <LoadingSpinner message={t('loading')} />
        ) : !jobs || jobs.length === 0 ? (
          <EmptyState
            icon={<Briefcase className="w-8 h-8 text-ink-400" />}
            title={t('emptyTitle')}
            description={t('emptyDescription')}
          />
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {jobs.map((job) => (
              <Link
                key={job.id}
                to={`/hm/jobs/${job.id}`}
                className="group flex h-full flex-col rounded-2xl border border-ink-200 bg-white p-5 shadow-card transition-all hover:border-brand-300 hover:shadow-card-hover dark:border-white/10 dark:bg-white/5 dark:hover:border-brand-500/40"
              >
                {/* Cùng khuôn thẻ với màn tin của Recruiter: ô biểu tượng bên trái, chip trạng thái
                    bên phải, chân thẻ tách bằng đường kẻ. Ba khu vực nhìn khác nhau ở cùng một loại
                    thẻ là thứ khiến người dùng tưởng đang ở nhầm hệ thống. */}
                <div className="mb-3 flex items-start justify-between gap-3">
                  <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-400">
                    <Briefcase className="h-5 w-5" />
                  </span>
                  <span
                    className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                      STATUS_CLASSES[job.status] ?? STATUS_CLASSES.draft
                    }`}
                  >
                    {t(`status.${job.status}`, { defaultValue: job.status })}
                  </span>
                </div>

                <h3 className="line-clamp-2 font-semibold text-ink-900 group-hover:text-brand-600 dark:text-white dark:group-hover:text-brand-400">
                  {job.title}
                </h3>
                <p className="mt-0.5 flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400">
                  {job.department || t('noDepartment')}
                  {job.location ? (
                    <>
                      <span>·</span>
                      <MapPin className="h-3 w-3 shrink-0" />
                      {job.location}
                    </>
                  ) : null}
                </p>

                {/* Tín hiệu quan trọng NHẤT với vai này: tin đang chờ chính họ ký duyệt thì không
                    được lẫn vào đám chip trạng thái chung. */}
                {job.hmSignOffStatus === 'pending' && (
                  <span className="mt-3 inline-flex w-fit items-center gap-1.5 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-700 dark:bg-amber-500/20 dark:text-amber-400">
                    <FileSignature className="h-3 w-3" /> {t('needsSignOff')}
                  </span>
                )}

                <div className="mt-auto flex items-center justify-between gap-2 border-t border-ink-100 pt-3 text-xs dark:border-white/10">
                  <span className="flex items-center gap-1.5 font-medium text-ink-600 dark:text-ink-300">
                    <Users className="h-3.5 w-3.5" />
                    {t('applicantCount', { count: job.applicantCount ?? 0 })}
                  </span>
                  <span className="truncate text-ink-400">
                    {job.salaryIsNegotiable
                      ? t('detail.salaryNegotiable')
                      : formatSalary(job.salaryMin, job.salaryCurrency)}
                  </span>
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
