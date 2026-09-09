import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { FileSignature, Plus } from 'lucide-react'
import { offerService, OFFER_STATUS } from '@ari/shared/fservices/offer'
import { useAuthStore } from '@ari/shared/store/auth'
import { normalizeRole, ROLE } from '@ari/shared/utils/roles'
import { OFFERS_NS, offerStatusBadgeClass, formatSalary, formatDate } from '../hiring/hiringConfig'
import OfferEditorModal from './OfferEditorModal'

interface CandidateOfferPanelProps {
  applicationId: string
  /** Trạng thái hồ sơ — thư mời chỉ mở được sau khi ứng viên qua HẾT các vòng (`pass`). */
  status: string
  onChanged?: () => void
}

const CARD =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card'

/** Thư mời còn "sống" — trùng đúng vị từ của unique index `ux_offers_application_live` phía DB. */
const LIVE = new Set<string>([
  OFFER_STATUS.Draft,
  OFFER_STATUS.PendingApproval,
  OFFER_STATUS.Approved,
  OFFER_STATUS.Sent,
  OFFER_STATUS.Accepted,
])

/**
 * Thư mời nhận việc của MỘT hồ sơ, đặt trên trang chi tiết ứng viên (ADR-061, phase 5).
 *
 * Panel chỉ xuất hiện từ `pass` trở đi: mời nhận việc một người chưa qua hết vòng là đi ngược
 * ADR-053. Nút tạo cũng tắt khi hồ sơ đã có thư còn hiệu lực — server chặn ở tầng DB, nhưng để
 * người dùng bấm rồi mới nhận lỗi thì họ sẽ tưởng hệ thống hỏng chứ không hiểu là đã có thư rồi.
 */
export default function CandidateOfferPanel({
  applicationId,
  status,
  onChanged,
}: CandidateOfferPanelProps) {
  const { t } = useTranslation(OFFERS_NS)
  const role = normalizeRole(useAuthStore((s) => s.user)?.role)
  const [editorOpen, setEditorOpen] = useState(false)
  /** Soạn thư MỚI dù hồ sơ đã có thư cũ đã khép — modal phải mở ở chế độ tạo, không phải sửa. */
  const [replacing, setReplacing] = useState(false)

  const { data: offers = [], refetch } = useQuery({
    queryKey: ['offers'],
    queryFn: () => offerService.list(),
  })

  const mine = offers.filter((o) => o.applicationId === applicationId)
  const live = mine.find((o) => LIVE.has(o.status))
  const latest = live ?? mine[0]

  // Hiring Manager duyệt thư nhưng không soạn thư; ở đây họ vào bằng nút mở thư đã có.
  const canCreate = role !== ROLE.HiringManager

  if (!latest && status !== 'pass') return null

  return (
    <div className={CARD}>
      <h2 className="mb-1 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
        <FileSignature className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('panel.title')}
      </h2>

      {latest ? (
        <>
          <div className="mb-3 mt-2 flex flex-wrap items-center gap-2">
            <span
              className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${offerStatusBadgeClass(latest.status)}`}
            >
              {t(`status.${latest.status}`)}
            </span>
            <span className="text-sm text-ink-600 dark:text-ink-400">
              {formatSalary(latest.salaryAmount, latest.salaryCurrency)}
            </span>
          </div>
          <dl className="space-y-1.5 text-sm">
            <div className="flex justify-between gap-2">
              <dt className="text-ink-500 dark:text-ink-400">{t('list.startDate')}</dt>
              <dd className="font-medium text-ink-900 dark:text-white">{formatDate(latest.startDate)}</dd>
            </div>
            <div className="flex justify-between gap-2">
              <dt className="text-ink-500 dark:text-ink-400">{t('list.expiresAt')}</dt>
              <dd className="font-medium text-ink-900 dark:text-white">{formatDate(latest.expiresAt)}</dd>
            </div>
          </dl>
          <button
            type="button"
            onClick={() => setEditorOpen(true)}
            className="mt-4 inline-flex w-full items-center justify-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 px-3 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5 transition-colors"
          >
            {t('panel.open')}
          </button>
          {/* Thư cũ đã khép (thu hồi / ứng viên từ chối / hết hạn) mà hồ sơ vẫn ở `pass` thì soạn
              lại được — ràng buộc một-thư-sống ở DB cũng cho đúng như vậy. */}
          {!live && status === 'pass' && canCreate && (
            <button
              type="button"
              onClick={() => {
                setReplacing(true)
                setEditorOpen(true)
              }}
              className="mt-2 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-3 py-2.5 text-sm font-medium text-white hover:bg-brand-700 transition-colors"
            >
              <Plus className="h-4 w-4" /> {t('panel.createReplacement')}
            </button>
          )}
        </>
      ) : (
        <>
          <p className="mb-4 mt-1 text-xs text-ink-500 dark:text-ink-400">{t('panel.description')}</p>
          {canCreate && (
            <button
              type="button"
              onClick={() => setEditorOpen(true)}
              className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-3 py-2.5 text-sm font-medium text-white hover:bg-brand-700 transition-colors"
            >
              <Plus className="h-4 w-4" /> {t('panel.create')}
            </button>
          )}
        </>
      )}

      {editorOpen && (
        <OfferEditorModal
          offer={replacing ? null : (latest ?? null)}
          applicationId={applicationId}
          onClose={() => {
            setEditorOpen(false)
            setReplacing(false)
          }}
          onSaved={() => {
            refetch()
            onChanged?.()
          }}
        />
      )}
    </div>
  )
}
