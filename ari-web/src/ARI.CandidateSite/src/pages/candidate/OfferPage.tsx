import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { BadgeCheck, CalendarDays, Clock, MapPin, Briefcase, ArrowLeft } from 'lucide-react'
import { PageHeader, StatsGrid, ErrorAlert, LoadingSpinner, EmptyState } from '@ari/shared/ui'
import { offerService, OFFER_STATUS } from '@ari/shared/fservices/offer'

function formatMoney(amount?: number | null, currency?: string | null): string {
  if (typeof amount !== 'number') return '—'
  return `${amount.toLocaleString('vi-VN')} ${currency ?? 'VND'}`
}

function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('vi-VN')
}

/** Số ngày còn lại tới hạn phản hồi; âm nghĩa là đã quá hạn. */
function daysLeft(iso?: string | null): number | null {
  if (!iso) return null
  const d = new Date(iso).getTime()
  if (Number.isNaN(d)) return null
  return Math.ceil((d - Date.now()) / 86_400_000)
}

export default function OfferPage() {
  const { id } = useParams<{ id: string }>()
  const { t } = useTranslation('modules/candidate/offer')
  const queryClient = useQueryClient()
  const [declining, setDeclining] = useState(false)
  const [note, setNote] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)

  const { data: offer, isLoading, error } = useQuery({
    queryKey: ['candidate-offer', id],
    queryFn: () => offerService.getMyOffer(id!),
    enabled: !!id,
    retry: false,
  })

  const respond = useMutation({
    mutationFn: ({ decision, reason }: { decision: 'accept' | 'decline'; reason?: string }) =>
      offerService.respond(offer!.id, decision, reason),
    onSuccess: () => {
      setActionError(null)
      setDeclining(false)
      queryClient.invalidateQueries({ queryKey: ['candidate-offer', id] })
      queryClient.invalidateQueries({ queryKey: ['candidate-applications'] })
    },
    onError: (e: unknown) => {
      const message =
        (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('actionError')
      setActionError(message)
    },
  })

  if (isLoading) return <LoadingSpinner message={t('loading')} />

  if (error || !offer) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('title')} />
        <EmptyState
          icon={<Briefcase className="w-8 h-8 text-ink-400" />}
          title={t('emptyTitle')}
          description={t('emptyDescription')}
        />
      </div>
    )
  }

  const remaining = daysLeft(offer.expiresAt)
  const isPending = offer.status === OFFER_STATUS.Sent && (remaining === null || remaining >= 0)
  const isExpired = offer.status === OFFER_STATUS.Expired || (remaining !== null && remaining < 0)

  const stats = [
    {
      label: t('stats.salary'),
      value: formatMoney(offer.salaryAmount, offer.salaryCurrency),
      icon: <BadgeCheck className="w-4 h-4" />,
      color: 'text-emerald-600 dark:text-emerald-400',
    },
    {
      label: t('stats.startDate'),
      value: formatDate(offer.startDate),
      icon: <CalendarDays className="w-4 h-4" />,
      color: 'text-blue-600 dark:text-blue-400',
    },
    {
      label: t('stats.deadline'),
      value: remaining !== null && remaining >= 0 ? t('stats.daysLeft', { count: remaining }) : formatDate(offer.expiresAt),
      icon: <Clock className="w-4 h-4" />,
      color: 'text-amber-600 dark:text-amber-400',
    },
  ]

  const primaryButton =
    'inline-flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors'
  const ghostButton =
    'inline-flex items-center justify-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 px-5 py-2.5 text-sm font-semibold text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5 disabled:opacity-50 transition-colors'

  return (
    <div className="space-y-6">
      <Link
        to={`/candidate/applications/${offer.applicationId}`}
        className="inline-flex items-center gap-1.5 text-sm text-ink-600 dark:text-ink-400 hover:text-brand-600 dark:hover:text-brand-400 transition-colors"
      >
        <ArrowLeft className="w-4 h-4" /> {t('backToApplication')}
      </Link>

      <PageHeader
        title={t('title')}
        description={t('description', { position: offer.position ?? offer.jobTitle ?? '' })}
      />

      {actionError && <ErrorAlert message={actionError} onDismiss={() => setActionError(null)} />}

      <StatsGrid stats={stats} />

      <section className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
        <h2 className="text-lg font-semibold text-ink-900 dark:text-white mb-4">{t('details')}</h2>
        <dl className="space-y-3 text-sm">
          {[
            { label: t('fields.position'), value: offer.position ?? offer.jobTitle },
            { label: t('fields.employmentType'), value: offer.employmentType },
            { label: t('fields.workLocation'), value: offer.workLocation, icon: MapPin },
            { label: t('fields.bonus'), value: offer.bonus },
            { label: t('fields.benefits'), value: offer.benefits },
          ]
            .filter((row) => !!row.value)
            .map((row) => (
              <div key={row.label} className="flex flex-wrap gap-2">
                <dt className="w-48 shrink-0 text-ink-500 dark:text-ink-400">{row.label}</dt>
                <dd className="flex-1 font-medium text-ink-900 dark:text-white">{row.value}</dd>
              </div>
            ))}
        </dl>
      </section>

      {/* Trạng thái + hành động */}
      {isPending ? (
        <section className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
          <h2 className="text-lg font-semibold text-ink-900 dark:text-white">{t('respond.title')}</h2>
          <p className="mt-1 text-sm text-ink-600 dark:text-ink-400">{t('respond.description')}</p>

          {!declining ? (
            <div className="mt-5 flex flex-wrap gap-3">
              <button
                type="button"
                className={primaryButton}
                onClick={() => respond.mutate({ decision: 'accept' })}
                disabled={respond.isPending}
              >
                {t('respond.accept')}
              </button>
              <button
                type="button"
                className={ghostButton}
                onClick={() => setDeclining(true)}
                disabled={respond.isPending}
              >
                {t('respond.decline')}
              </button>
            </div>
          ) : (
            <div className="mt-5">
              <label htmlFor="decline-note" className="block text-sm font-medium text-ink-900 dark:text-white mb-2">
                {t('respond.reasonLabel')}
              </label>
              <textarea
                id="decline-note"
                rows={3}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder={t('respond.reasonPlaceholder')}
                className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500"
              />
              <div className="mt-3 flex flex-wrap gap-2">
                <button type="button" className={ghostButton} onClick={() => setDeclining(false)}>
                  {t('respond.cancel')}
                </button>
                <button
                  type="button"
                  className={primaryButton}
                  onClick={() => respond.mutate({ decision: 'decline', reason: note })}
                  disabled={respond.isPending}
                >
                  {t('respond.confirmDecline')}
                </button>
              </div>
            </div>
          )}
        </section>
      ) : (
        <section className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
          <p className="text-sm font-medium text-ink-900 dark:text-white">
            {isExpired
              ? t('status.expired')
              : offer.status === OFFER_STATUS.Accepted
                ? t('status.accepted')
                : offer.status === OFFER_STATUS.Declined
                  ? t('status.declined')
                  : t('status.closed')}
          </p>
          {offer.candidateResponseNote && (
            <p className="mt-2 text-sm text-ink-600 dark:text-ink-400">
              {t('status.yourNote', { note: offer.candidateResponseNote })}
            </p>
          )}
        </section>
      )}
    </div>
  )
}
