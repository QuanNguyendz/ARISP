import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { X, Send, Check, Undo2, Save, ShieldAlert } from 'lucide-react'
import EmailComposerModal from '@ari/shared/ui/EmailComposerModal'
import { offerService, OFFER_STATUS, type StaffOffer } from '@ari/shared/fservices/offer'
import { useAuthStore } from '@ari/shared/store/auth'
import { normalizeRole, ROLE } from '@ari/shared/utils/roles'
import { OFFERS_NS, offerStatusBadgeClass, formatSalary } from '../hiring/hiringConfig'

interface OfferEditorModalProps {
  /** Offer đang sửa. Bỏ trống + `applicationId` = tạo mới. */
  offer?: StaffOffer | null
  applicationId?: string
  onClose: () => void
  onSaved?: () => void
}

const FIELD =
  'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500 disabled:opacity-60'
const LABEL = 'block text-sm font-medium text-ink-900 dark:text-white mb-1.5'
const PRIMARY_BTN =
  'inline-flex items-center justify-center gap-1.5 rounded-xl bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors'
const GHOST_BTN =
  'inline-flex items-center justify-center gap-1.5 rounded-xl border border-ink-200 dark:border-white/10 px-4 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5 disabled:opacity-50 disabled:cursor-not-allowed transition-colors'

/** `2026-09-01T00:00:00+07:00` → `2026-09-01` cho ô nhập ngày. */
function toDateInput(iso?: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/**
 * Hạn phản hồi hết vào CUỐI ngày đã chọn, không phải 00:00 của ngày đó — chọn "05/09" mà hết hạn
 * lúc nửa đêm mở đầu ngày 05 nghĩa là ứng viên mất trọn một ngày so với thứ họ đọc trong thư.
 */
function toExpiryIso(value: string): string | undefined {
  if (!value) return undefined
  return new Date(`${value}T23:59:59`).toISOString()
}

function toStartIso(value: string): string | undefined {
  if (!value) return undefined
  return new Date(`${value}T00:00:00`).toISOString()
}

/**
 * Soạn và điều hành một thư mời nhận việc (ADR-061, phase 5).
 *
 * Vòng đời: nháp → gửi duyệt → Hiring Manager duyệt → gửi ứng viên → ứng viên trả lời. Modal này
 * hiện đúng những nút hợp lệ ở trạng thái hiện tại; server vẫn là nơi kiểm quyền và kiểm chuyển
 * trạng thái, ở đây chỉ là chuyện không bày ra nút không bấm được.
 *
 * Nút Gửi mở TRÌNH SOẠN THẢO thư chứ không gửi thẳng: thư mời nhận việc là văn bản cam kết, và
 * yêu cầu của người dùng là mọi thư gửi ứng viên đều sửa được trước khi rời hệ thống. Việc gửi vẫn
 * nằm trong chính lệnh `send` — huỷ trình soạn = không có gì xảy ra, hồ sơ vẫn ở `pass`.
 */
export default function OfferEditorModal({
  offer,
  applicationId,
  onClose,
  onSaved,
}: OfferEditorModalProps) {
  const { t } = useTranslation(OFFERS_NS)
  const queryClient = useQueryClient()
  const role = normalizeRole(useAuthStore((s) => s.user)?.role)

  const isAdmin = role === ROLE.SuperAdmin || role === ROLE.HRAdmin
  const canDecide = isAdmin || role === ROLE.HiringManager

  const status = offer?.status ?? OFFER_STATUS.Draft
  const editable = !offer || status === OFFER_STATUS.Draft

  const [form, setForm] = useState({
    position: '',
    salaryAmount: '',
    salaryCurrency: 'VND',
    salaryPeriod: 'month',
    bonus: '',
    benefits: '',
    employmentType: '',
    workLocation: '',
    startDate: '',
    expiresAt: '',
    notes: '',
  })
  const [decisionNote, setDecisionNote] = useState('')
  const [rejecting, setRejecting] = useState(false)
  const [withdrawing, setWithdrawing] = useState(false)
  const [composing, setComposing] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    if (!offer) return
    setForm({
      position: offer.position ?? '',
      salaryAmount: offer.salaryAmount != null ? String(offer.salaryAmount) : '',
      salaryCurrency: offer.salaryCurrency ?? 'VND',
      salaryPeriod: offer.salaryPeriod ?? 'month',
      bonus: offer.bonus ?? '',
      benefits: offer.benefits ?? '',
      employmentType: offer.employmentType ?? '',
      workLocation: offer.workLocation ?? '',
      startDate: toDateInput(offer.startDate),
      expiresAt: toDateInput(offer.expiresAt),
      notes: offer.notes ?? '',
    })
  }, [offer])

  const fail = (e: unknown, fallback: string) => {
    const message = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
    setActionError(message ?? fallback)
  }

  const refresh = (close = false) => {
    setActionError(null)
    queryClient.invalidateQueries({ queryKey: ['offers'] })
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    onSaved?.()
    if (close) onClose()
  }

  const payload = () => ({
    position: form.position || undefined,
    salaryAmount: form.salaryAmount ? Number(form.salaryAmount) : undefined,
    salaryCurrency: form.salaryCurrency || undefined,
    salaryPeriod: form.salaryPeriod || undefined,
    bonus: form.bonus || undefined,
    benefits: form.benefits || undefined,
    employmentType: form.employmentType || undefined,
    workLocation: form.workLocation || undefined,
    startDate: toStartIso(form.startDate),
    expiresAt: toExpiryIso(form.expiresAt),
    notes: form.notes || undefined,
  })

  const save = useMutation({
    mutationFn: async () => {
      if (offer) return offerService.update(offer.id, payload())
      return offerService.create({ applicationId: applicationId!, ...payload() })
    },
    onSuccess: () => refresh(true),
    onError: (e) => fail(e, t('errors.save')),
  })

  const submit = useMutation({
    mutationFn: () => offerService.submit(offer!.id),
    onSuccess: () => refresh(true),
    onError: (e) => fail(e, t('errors.submit')),
  })

  const decide = useMutation({
    mutationFn: ({ decision, note }: { decision: 'approved' | 'rejected'; note?: string }) =>
      offerService.decide(offer!.id, decision, note),
    onSuccess: () => refresh(true),
    onError: (e) => fail(e, t('errors.decide')),
  })

  const send = useMutation({
    mutationFn: (override?: { subject: string; bodyHtml: string }) =>
      offerService.send(offer!.id, override),
    onSuccess: () => {
      setComposing(false)
      refresh(true)
    },
    onError: (e) => {
      setComposing(false)
      fail(e, t('errors.send'))
    },
  })

  const withdraw = useMutation({
    mutationFn: (reason: string) => offerService.withdraw(offer!.id, reason),
    onSuccess: () => refresh(true),
    onError: (e) => fail(e, t('errors.withdraw')),
  })

  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const busy =
    save.isPending || submit.isPending || decide.isPending || send.isPending || withdraw.isPending

  // Đủ điều kiện gửi duyệt: mức lương + ngày bắt đầu + hạn phản hồi (server kiểm lại y hệt).
  const readyToSubmit = !!form.salaryAmount && !!form.startDate && !!form.expiresAt

  const canWithdraw =
    isAdmin &&
    offer != null &&
    status !== OFFER_STATUS.Withdrawn &&
    status !== OFFER_STATUS.Declined &&
    status !== OFFER_STATUS.Expired &&
    status !== OFFER_STATUS.Accepted

  return (
    <>
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
        <div className="flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-950 shadow-card">
          <header className="flex items-start justify-between gap-4 border-b border-ink-100 dark:border-white/10 px-6 py-4">
            <div className="min-w-0">
              <h2 className="font-semibold text-ink-900 dark:text-white">
                {offer ? t('editor.title') : t('editor.createTitle')}
              </h2>
              <p className="mt-0.5 truncate text-sm text-ink-500 dark:text-ink-400">
                {offer
                  ? `${offer.candidateName ?? offer.candidateEmail ?? ''} · ${offer.jobTitle ?? ''}`
                  : t('editor.createHint')}
              </p>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              {offer && (
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${offerStatusBadgeClass(status)}`}
                >
                  {t(`status.${status}`)}
                </span>
              )}
              <button
                type="button"
                onClick={onClose}
                aria-label={t('common.close')}
                className="rounded-lg p-1.5 text-ink-500 hover:bg-ink-100 dark:hover:bg-white/10 transition-colors"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
          </header>

          <div className="flex-1 space-y-4 overflow-y-auto px-6 py-4">
            {actionError && (
              <div className="rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-4 py-3 text-sm text-red-700 dark:text-red-400">
                {actionError}
              </div>
            )}

            {/* Góp ý khi Hiring Manager trả thư về bản nháp — thứ cần đọc TRƯỚC khi sửa. */}
            {offer?.rejectedReason && status === OFFER_STATUS.Draft && (
              <div className="rounded-xl border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/10 px-4 py-3 text-sm text-amber-700 dark:text-amber-400">
                {t('editor.returnedReason', { reason: offer.rejectedReason })}
              </div>
            )}
            {offer?.candidateResponseNote && (
              <div className="rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/60 dark:bg-white/5 px-4 py-3 text-sm text-ink-700 dark:text-ink-300">
                {t('editor.candidateNote', { note: offer.candidateResponseNote })}
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="sm:col-span-2">
                <label htmlFor="offer-position" className={LABEL}>
                  {t('fields.position')}
                </label>
                <input
                  id="offer-position"
                  className={FIELD}
                  value={form.position}
                  onChange={set('position')}
                  disabled={!editable}
                  placeholder={t('fields.positionPlaceholder')}
                />
              </div>

              <div>
                <label htmlFor="offer-salary" className={LABEL}>
                  {t('fields.salary')}
                </label>
                <input
                  id="offer-salary"
                  type="number"
                  min={0}
                  className={FIELD}
                  value={form.salaryAmount}
                  onChange={set('salaryAmount')}
                  disabled={!editable}
                />
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label htmlFor="offer-currency" className={LABEL}>
                    {t('fields.currency')}
                  </label>
                  <input
                    id="offer-currency"
                    className={FIELD}
                    value={form.salaryCurrency}
                    onChange={set('salaryCurrency')}
                    disabled={!editable}
                  />
                </div>
                <div>
                  <label htmlFor="offer-period" className={LABEL}>
                    {t('fields.period')}
                  </label>
                  <select
                    id="offer-period"
                    className={FIELD}
                    value={form.salaryPeriod}
                    onChange={set('salaryPeriod')}
                    disabled={!editable}
                  >
                    <option value="month">{t('fields.periods.month')}</option>
                    <option value="year">{t('fields.periods.year')}</option>
                    <option value="hour">{t('fields.periods.hour')}</option>
                  </select>
                </div>
              </div>

              <div>
                <label htmlFor="offer-start" className={LABEL}>
                  {t('fields.startDate')}
                </label>
                <input
                  id="offer-start"
                  type="date"
                  className={FIELD}
                  value={form.startDate}
                  onChange={set('startDate')}
                  disabled={!editable}
                />
              </div>
              <div>
                <label htmlFor="offer-expires" className={LABEL}>
                  {t('fields.expiresAt')}
                </label>
                <input
                  id="offer-expires"
                  type="date"
                  className={FIELD}
                  value={form.expiresAt}
                  onChange={set('expiresAt')}
                  disabled={!editable}
                />
              </div>

              <div>
                <label htmlFor="offer-employment" className={LABEL}>
                  {t('fields.employmentType')}
                </label>
                <input
                  id="offer-employment"
                  className={FIELD}
                  value={form.employmentType}
                  onChange={set('employmentType')}
                  disabled={!editable}
                />
              </div>
              <div>
                <label htmlFor="offer-location" className={LABEL}>
                  {t('fields.workLocation')}
                </label>
                <input
                  id="offer-location"
                  className={FIELD}
                  value={form.workLocation}
                  onChange={set('workLocation')}
                  disabled={!editable}
                />
              </div>

              <div className="sm:col-span-2">
                <label htmlFor="offer-bonus" className={LABEL}>
                  {t('fields.bonus')}
                </label>
                <input
                  id="offer-bonus"
                  className={FIELD}
                  value={form.bonus}
                  onChange={set('bonus')}
                  disabled={!editable}
                />
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="offer-benefits" className={LABEL}>
                  {t('fields.benefits')}
                </label>
                <textarea
                  id="offer-benefits"
                  rows={3}
                  className={FIELD}
                  value={form.benefits}
                  onChange={set('benefits')}
                  disabled={!editable}
                />
              </div>
              <div className="sm:col-span-2">
                <label htmlFor="offer-notes" className={LABEL}>
                  {t('fields.notes')}
                </label>
                <textarea
                  id="offer-notes"
                  rows={2}
                  className={FIELD}
                  value={form.notes}
                  onChange={set('notes')}
                  disabled={!editable}
                  placeholder={t('fields.notesPlaceholder')}
                />
                {/* Nhắc tại chỗ, vì đây là ô duy nhất trong form KHÔNG bao giờ tới tay ứng viên. */}
                <p className="mt-1.5 text-xs text-ink-500 dark:text-ink-400">{t('fields.notesHint')}</p>
              </div>
            </div>

            {/* Ô lý do dùng chung cho trả-về-nháp và thu hồi. */}
            {(rejecting || withdrawing) && (
              <div>
                <label htmlFor="offer-decision-note" className={LABEL}>
                  {rejecting ? t('actions.rejectReasonLabel') : t('actions.withdrawReasonLabel')}
                </label>
                <textarea
                  id="offer-decision-note"
                  rows={3}
                  className={FIELD}
                  value={decisionNote}
                  onChange={(e) => setDecisionNote(e.target.value)}
                />
              </div>
            )}
          </div>

          <footer className="flex flex-wrap items-center justify-end gap-2 border-t border-ink-100 dark:border-white/10 px-6 py-4">
            {offer && status !== OFFER_STATUS.Draft && (
              <span className="mr-auto text-sm text-ink-500 dark:text-ink-400">
                {formatSalary(offer.salaryAmount, offer.salaryCurrency)}
              </span>
            )}

            <button type="button" className={GHOST_BTN} onClick={onClose} disabled={busy}>
              {t('common.close')}
            </button>

            {editable && (
              <button
                type="button"
                className={GHOST_BTN}
                onClick={() => save.mutate()}
                disabled={busy}
              >
                <Save className="h-4 w-4" /> {t('actions.save')}
              </button>
            )}

            {offer && status === OFFER_STATUS.Draft && (
              <button
                type="button"
                className={PRIMARY_BTN}
                onClick={() => submit.mutate()}
                disabled={busy || !readyToSubmit}
                title={readyToSubmit ? undefined : t('actions.submitBlocked')}
              >
                <Send className="h-4 w-4" /> {t('actions.submit')}
              </button>
            )}

            {offer && status === OFFER_STATUS.PendingApproval && canDecide && !rejecting && (
              <>
                <button
                  type="button"
                  className={GHOST_BTN}
                  onClick={() => setRejecting(true)}
                  disabled={busy}
                >
                  <Undo2 className="h-4 w-4" /> {t('actions.returnToDraft')}
                </button>
                <button
                  type="button"
                  className={PRIMARY_BTN}
                  onClick={() => decide.mutate({ decision: 'approved' })}
                  disabled={busy}
                >
                  <Check className="h-4 w-4" /> {t('actions.approve')}
                </button>
              </>
            )}
            {offer && status === OFFER_STATUS.PendingApproval && canDecide && rejecting && (
              <button
                type="button"
                className={PRIMARY_BTN}
                onClick={() => decide.mutate({ decision: 'rejected', note: decisionNote })}
                disabled={busy || decisionNote.trim().length === 0}
              >
                {t('actions.confirmReturn')}
              </button>
            )}

            {offer && status === OFFER_STATUS.Approved && (
              <button
                type="button"
                className={PRIMARY_BTN}
                onClick={() => setComposing(true)}
                disabled={busy}
              >
                <Send className="h-4 w-4" /> {t('actions.send')}
              </button>
            )}

            {canWithdraw && !withdrawing && (
              <button
                type="button"
                className={GHOST_BTN}
                onClick={() => setWithdrawing(true)}
                disabled={busy}
              >
                <ShieldAlert className="h-4 w-4" /> {t('actions.withdraw')}
              </button>
            )}
            {canWithdraw && withdrawing && (
              <button
                type="button"
                className={PRIMARY_BTN}
                onClick={() => withdraw.mutate(decisionNote)}
                disabled={busy || decisionNote.trim().length === 0}
              >
                {t('actions.confirmWithdraw')}
              </button>
            )}
          </footer>
        </div>
      </div>

      {offer && composing && (
        <EmailComposerModal
          open
          templateKey="offer_sent"
          contextId={offer.applicationId}
          title={t('actions.composeTitle')}
          confirmLabel={t('actions.send')}
          sending={send.isPending}
          onCancel={() => setComposing(false)}
          onSend={async (override) => {
            await send.mutateAsync(override)
          }}
        />
      )}
    </>
  )
}
