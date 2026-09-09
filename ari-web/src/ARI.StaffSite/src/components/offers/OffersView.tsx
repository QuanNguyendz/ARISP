import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { FileSignature, Search } from 'lucide-react'
import { PageHeader, StatsGrid, EmptyState, ErrorAlert, LoadingSpinner } from '@ari/shared/ui'
import { offerService, OFFER_STATUS, type StaffOffer } from '@ari/shared/fservices/offer'
import {
  AREAS,
  OFFERS_NS,
  type StaffArea,
  offerStatusBadgeClass,
  formatSalary,
  formatDate,
} from '../hiring/hiringConfig'
import OfferEditorModal from './OfferEditorModal'

interface OffersViewProps {
  variant: StaffArea
}

/** Thứ tự lọc bám theo vòng đời, để hàng chờ việc nằm bên trái. */
const FILTERS = [
  'all',
  OFFER_STATUS.Draft,
  OFFER_STATUS.PendingApproval,
  OFFER_STATUS.Approved,
  OFFER_STATUS.Sent,
  OFFER_STATUS.Accepted,
  OFFER_STATUS.Declined,
] as const

/**
 * Màn thư mời nhận việc, dùng chung cho HR / Recruiter / Hiring Manager (ADR-061).
 *
 * Ba khu vực chỉ khác nhau ở ĐƯỜNG DẪN sang trang ứng viên; phạm vi dữ liệu do server quyết định
 * (`GET /api/offers` đã lọc theo tin mà người gọi được phép thấy), nên ở đây không có nhánh nào
 * lọc theo vai trò — thêm một nhánh như thế là tự dựng lớp phân quyền thứ hai ở phía client, và
 * lớp đó chỉ cần lệch một lần là rò dữ liệu.
 */
export default function OffersView({ variant }: OffersViewProps) {
  const { t } = useTranslation(OFFERS_NS)
  const area = AREAS[variant]

  const [filter, setFilter] = useState<string>('all')
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<StaffOffer | null>(null)

  const {
    data: offers = [],
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['offers'],
    queryFn: () => offerService.list(),
  })

  const stats = useMemo(
    () => [
      {
        label: t('stats.awaitingApproval'),
        value: offers.filter((o) => o.status === OFFER_STATUS.PendingApproval).length,
        icon: <FileSignature className="h-4 w-4" />,
        color: 'text-amber-600 dark:text-amber-400',
      },
      {
        label: t('stats.awaitingResponse'),
        value: offers.filter((o) => o.status === OFFER_STATUS.Sent).length,
        icon: <FileSignature className="h-4 w-4" />,
        color: 'text-sky-600 dark:text-sky-400',
      },
      {
        label: t('stats.accepted'),
        value: offers.filter((o) => o.status === OFFER_STATUS.Accepted).length,
        icon: <FileSignature className="h-4 w-4" />,
        color: 'text-emerald-600 dark:text-emerald-400',
      },
      {
        label: t('stats.declined'),
        value: offers.filter(
          (o) => o.status === OFFER_STATUS.Declined || o.status === OFFER_STATUS.Expired
        ).length,
        icon: <FileSignature className="h-4 w-4" />,
        color: 'text-red-600 dark:text-red-400',
      },
    ],
    [offers, t]
  )

  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase()
    return offers.filter((o) => {
      if (filter !== 'all' && o.status !== filter) return false
      if (!needle) return true
      return (
        (o.candidateName ?? '').toLowerCase().includes(needle) ||
        (o.candidateEmail ?? '').toLowerCase().includes(needle) ||
        (o.jobTitle ?? '').toLowerCase().includes(needle)
      )
    })
  }, [offers, filter, search])

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="space-y-6">
        <PageHeader title={t('title')} description={t('description')} />

        {error && <ErrorAlert message={t('loadError')} />}

        {isLoading ? (
          <LoadingSpinner message={t('common.loading')} />
        ) : (
          <>
            <StatsGrid stats={stats} />

            <div className="flex flex-wrap items-center gap-2">
              <div className="relative min-w-[220px] flex-1">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-400" />
                <input
                  type="search"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder={t('searchPlaceholder')}
                  className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 py-2 pl-9 pr-3 text-sm text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500"
                />
              </div>
              <div className="flex flex-wrap gap-1.5">
                {FILTERS.map((f) => (
                  <button
                    key={f}
                    type="button"
                    onClick={() => setFilter(f)}
                    className={`rounded-xl px-3 py-1.5 text-sm font-medium transition-colors ${
                      filter === f
                        ? 'bg-brand-600 text-white'
                        : 'border border-ink-200 dark:border-white/10 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5'
                    }`}
                  >
                    {f === 'all' ? t('filters.all') : t(`status.${f}`)}
                  </button>
                ))}
              </div>
            </div>

            {visible.length === 0 ? (
              <EmptyState
                icon={<FileSignature className="h-8 w-8 text-ink-400" />}
                title={t('emptyTitle')}
                description={t('emptyDescription')}
              />
            ) : (
              <ul className="space-y-3">
                {visible.map((offer) => (
                  <li
                    key={offer.id}
                    className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
                  >
                    <div className="flex flex-wrap items-start justify-between gap-4">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <h3 className="truncate font-semibold text-ink-900 dark:text-white">
                            {offer.candidateName || offer.candidateEmail || '—'}
                          </h3>
                          <span
                            className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${offerStatusBadgeClass(offer.status)}`}
                          >
                            {t(`status.${offer.status}`)}
                          </span>
                        </div>
                        <p className="mt-1 truncate text-sm text-ink-600 dark:text-ink-400">
                          {offer.position || offer.jobTitle || '—'}
                        </p>
                        <dl className="mt-2 flex flex-wrap gap-x-6 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
                          <div>
                            <dt className="inline">{t('list.salary')} </dt>
                            <dd className="inline font-medium text-ink-900 dark:text-white">
                              {formatSalary(offer.salaryAmount, offer.salaryCurrency)}
                            </dd>
                          </div>
                          <div>
                            <dt className="inline">{t('list.startDate')} </dt>
                            <dd className="inline">{formatDate(offer.startDate)}</dd>
                          </div>
                          <div>
                            <dt className="inline">{t('list.expiresAt')} </dt>
                            <dd className="inline">{formatDate(offer.expiresAt)}</dd>
                          </div>
                        </dl>
                      </div>

                      <div className="flex shrink-0 items-center gap-2">
                        <Link
                          to={area.candidateHref(offer.applicationId)}
                          className="rounded-xl border border-ink-200 dark:border-white/10 px-3 py-1.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5 transition-colors"
                        >
                          {t('list.viewCandidate')}
                        </Link>
                        <button
                          type="button"
                          onClick={() => setEditing(offer)}
                          className="rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 transition-colors"
                        >
                          {t('list.open')}
                        </button>
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}

        {editing && (
          <OfferEditorModal
            offer={editing}
            onClose={() => setEditing(null)}
            onSaved={() => refetch()}
          />
        )}
      </div>
    </div>
  )
}
