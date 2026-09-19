import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AlertTriangle } from 'lucide-react'
import EmailComposerModal from '@ari/shared/ui/EmailComposerModal'
import { EMAIL_TEMPLATES, type EmailOverride } from '@ari/shared/fservices/email'
import { applicationService } from '@ari/shared/fservices/application'
import { resolveApiError } from '@ari/shared/utils/apiError'
import type { HrApplicationItem } from '@ari/shared/types/application'

/**
 * Hồ sơ đã KHÉP — khớp `ApplicationStatuses.Terminal` ở backend. `pass` nằm ở đây vì đó là điểm kết thúc
 * thành công trước giai đoạn offer; hồ sơ đang cầm thư mời là `offer` và vẫn còn mở.
 */
const TERMINAL = new Set(['pass', 'not_pass', 'cv_rejected', 'withdrawn', 'hired', 'offer_declined', 'failed'])

interface ClosedJobOpenApplicationsProps {
  jobStatus: string
  applications: HrApplicationItem[]
  /** Chủ tin + quản trị viên loại được hồ sơ; Hiring Manager chỉ xem. */
  canReject: boolean
  candidateHref: (applicationId: string) => string
  offersHref: string
  /** Gọi sau khi loại xong một hồ sơ để màn cha nạp lại danh sách. */
  onChanged: () => void
}

/**
 * Tin đã đóng mà còn hồ sơ chưa khép (ADR-074 — tự đóng tin khi tuyển đủ người).
 *
 * Tin đóng thì Job Board không nhận hồ sơ mới, nhưng người đang giữa phễu vẫn chờ một câu trả lời. Hệ
 * thống cố ý KHÔNG tự loại họ — còn chỗ ở vị trí khác hay giữ làm dự phòng là việc con người quyết — nên
 * banner này đặt họ ngay trước mắt, mỗi dòng một thao tác: loại kèm thư cảm ơn (qua trình soạn, quy tắc 21),
 * hoặc với người đang cầm thư mời thì dẫn về màn thư mời (thu hồi là quyết định của HR, không phải ở đây).
 */
export default function ClosedJobOpenApplications({
  jobStatus,
  applications,
  canReject,
  candidateHref,
  offersHref,
  onChanged,
}: ClosedJobOpenApplicationsProps) {
  const { t } = useTranslation('modules/staff/offers')
  const [rejecting, setRejecting] = useState<HrApplicationItem | null>(null)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const open = useMemo(
    () => applications.filter((a) => !TERMINAL.has((a.status || '').toLowerCase())),
    [applications]
  )

  if (jobStatus !== 'closed' || open.length === 0) return null

  const reject = async (override?: EmailOverride) => {
    if (!rejecting) return
    try {
      setSending(true)
      setError(null)
      await applicationService.rejectApplication(rejecting.id, override)
      setRejecting(null)
      onChanged()
    } catch (e) {
      setRejecting(null)
      setError(resolveApiError(e, t, 'jobFilled.rejectError'))
    } finally {
      setSending(false)
    }
  }

  return (
    <section className="rounded-2xl border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/10 p-4 sm:p-5">
      <div className="flex items-start gap-3">
        <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400" />
        <div className="min-w-0 flex-1">
          <h3 className="font-semibold text-amber-900 dark:text-amber-300">
            {t('jobFilled.title', { count: open.length })}
          </h3>
          <p className="mt-1 text-sm text-amber-800 dark:text-amber-400">{t('jobFilled.hint')}</p>

          {error && (
            <p className="mt-3 rounded-lg border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-3 py-2 text-sm text-red-700 dark:text-red-400">
              {error}
            </p>
          )}

          <ul className="mt-3 divide-y divide-amber-200/70 dark:divide-amber-500/20">
            {open.map((a) => {
              const status = (a.status || '').toLowerCase()
              return (
                <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                  <div className="min-w-0">
                    <Link
                      to={candidateHref(a.id)}
                      className="font-medium text-ink-900 dark:text-white hover:underline"
                    >
                      {a.candidateName || a.candidateEmail}
                    </Link>
                    <span className="ml-2 text-xs text-ink-500 dark:text-ink-400">
                      {t(`jobFilled.status.${status}`, { defaultValue: status })}
                    </span>
                  </div>
                  {status === 'offer' ? (
                    <Link
                      to={offersHref}
                      className="text-sm font-medium text-brand-700 dark:text-brand-400 hover:underline"
                    >
                      {t('jobFilled.viewOffer')}
                    </Link>
                  ) : (
                    canReject && (
                      <button
                        type="button"
                        onClick={() => setRejecting(a)}
                        disabled={sending}
                        className="rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-1.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 disabled:opacity-50"
                      >
                        {t('jobFilled.reject')}
                      </button>
                    )
                  )}
                </li>
              )
            })}
          </ul>
        </div>
      </div>

      <EmailComposerModal
        open={rejecting !== null}
        templateKey={EMAIL_TEMPLATES.ApplicationRejected}
        contextId={rejecting?.id ?? ''}
        title={t('jobFilled.composerTitle', { name: rejecting?.candidateName ?? '' })}
        confirmLabel={t('jobFilled.composerSend')}
        sending={sending}
        onCancel={() => setRejecting(null)}
        onSend={reject}
      />
    </section>
  )
}
