import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ArrowLeft, Briefcase, Mail, Phone, FileText, ClipboardCheck } from 'lucide-react'
import { PageHeader, ErrorAlert, LoadingSpinner } from '@ari/shared/ui'
import { applicationService } from '@ari/shared/fservices/application'
import { evaluationService } from '@/fservices/evaluation/evaluationService'
import ShortlistGatePanel from '@/components/hiring/ShortlistGatePanel'
import EmailHistoryPanel from '@/components/hiring/EmailHistoryPanel'
import CandidateOfferPanel from '@/components/offers/CandidateOfferPanel'

const CARD =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card'

/**
 * Hồ sơ ứng viên dưới góc nhìn Hiring Manager (ADR-061).
 *
 * Chỉ những thứ Hiring Manager cần để RA QUYẾT ĐỊNH: thông tin ứng viên, mức khớp CV, cổng duyệt
 * shortlist, các báo cáo phỏng vấn để chốt kết quả, và thư mời nhận việc. Không có phần vận hành
 * (xếp lịch, cấp mã Kiosk, đổi trạng thái tay) — đó là việc của chủ tin, và bày ra nút mà server
 * sẽ từ chối chỉ làm người dùng tưởng hệ thống hỏng.
 */
export default function HmCandidateDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const { t } = useTranslation('modules/hm/candidateDetail')

  const {
    data: app,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['hr-application', id],
    queryFn: () => applicationService.getHrApplicationById(id),
    enabled: !!id,
  })

  const { data: evaluations = [] } = useQuery({
    queryKey: ['application-evaluations', id],
    queryFn: () => evaluationService.getEvaluationsByApplicationId(id),
    enabled: !!id,
  })

  if (isLoading) return <LoadingSpinner message={t('loading')} />
  if (error || !app) return <ErrorAlert message={t('loadError')} />

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="space-y-6">
        {/* Quay về màn TIN chứ không phải một danh sách hồ sơ riêng: từ ADR-067 cổng duyệt của
            Hiring Manager nằm ngay trong màn tin, cạnh khung đọc CV. */}
        <Link
          to={`/hm/jobs/${app.jobPostingId}`}
          className="inline-flex items-center gap-1.5 text-sm text-ink-600 dark:text-ink-400 hover:text-brand-600 dark:hover:text-brand-400 transition-colors"
        >
          <ArrowLeft className="h-4 w-4" /> {t('back')}
        </Link>

        <PageHeader title={app.candidateName} description={app.jobTitle ?? ''} />

        <div className="grid gap-6 lg:grid-cols-3">
          <div className="space-y-6 lg:col-span-2">
            <section className={CARD}>
              <h2 className="mb-3 text-sm font-semibold text-ink-900 dark:text-white">
                {t('contact')}
              </h2>
              <dl className="space-y-2 text-sm">
                <div className="flex items-center gap-2">
                  <Mail className="h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                  <span className="truncate text-ink-700 dark:text-ink-300">
                    {app.candidateEmail}
                  </span>
                </div>
                {app.candidatePhone && (
                  <div className="flex items-center gap-2">
                    <Phone className="h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                    <span className="text-ink-700 dark:text-ink-300">{app.candidatePhone}</span>
                  </div>
                )}
                <div className="flex items-center gap-2">
                  <Briefcase className="h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                  <span className="truncate text-ink-700 dark:text-ink-300">
                    {app.jobTitle ?? '—'}
                  </span>
                </div>
              </dl>

              {typeof app.matchScore === 'number' && (
                <div className="mt-4 border-t border-ink-100 dark:border-white/10 pt-4">
                  <p className="text-sm font-medium text-ink-900 dark:text-white">
                    {t('matchScore', { score: app.matchScore })}
                  </p>
                  {app.cvJdSummary && (
                    <p className="mt-1.5 text-sm text-ink-600 dark:text-ink-400">
                      {app.cvJdSummary}
                    </p>
                  )}
                </div>
              )}

              {app.cvFileUrl && (
                <a
                  href={app.cvFileUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-4 inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 px-3 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 transition-colors hover:bg-ink-50 dark:hover:bg-white/5"
                >
                  <FileText className="h-4 w-4" /> {t('viewCv')}
                </a>
              )}
            </section>

            <section className={CARD}>
              <h2 className="mb-1 text-sm font-semibold text-ink-900 dark:text-white">
                {t('evaluations.title')}
              </h2>
              <p className="mb-4 text-xs text-ink-500 dark:text-ink-400">
                {t('evaluations.description')}
              </p>
              {evaluations.length === 0 ? (
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('evaluations.empty')}</p>
              ) : (
                <ul className="divide-y divide-ink-100 dark:divide-white/10">
                  {evaluations.map((ev) => (
                    <li key={ev.id}>
                      <Link
                        to={`/hm/evaluations?id=${ev.id}`}
                        className="-mx-2 flex items-center justify-between gap-4 rounded-lg px-2 py-3 transition-colors hover:bg-ink-50 dark:hover:bg-white/5"
                      >
                        <span className="flex items-center gap-2 text-sm text-ink-900 dark:text-white">
                          <ClipboardCheck className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                          {t('evaluations.round', { number: ev.roundNumber ?? 1 })}
                        </span>
                        <span className="text-sm text-ink-500 dark:text-ink-400">
                          {typeof ev.overallScore === 'number' ? `${ev.overallScore}/100` : '—'}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <EmailHistoryPanel applicationId={app.id} />
          </div>

          <div className="space-y-6">
            <ShortlistGatePanel
              applicationId={app.id}
              jobPostingId={app.jobPostingId}
              status={app.status}
              hmDecision={app.hmDecision}
              hmDecisionNote={app.hmDecisionNote}
              onChanged={() => refetch()}
            />
            <CandidateOfferPanel
              applicationId={app.id}
              status={app.status}
              onChanged={() => refetch()}
            />
          </div>
        </div>
      </div>
    </div>
  )
}
