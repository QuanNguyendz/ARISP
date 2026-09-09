import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { UserCheck, ShieldAlert, Send, Check, X } from 'lucide-react'
import { hiringTeamService } from '@ari/shared/fservices/hiringTeam'
import jobService from '@ari/shared/fservices/job'
import { scheduleService } from '@ari/shared/fservices/schedule'
import { useAuthStore } from '@ari/shared/store/auth'
import { normalizeRole, ROLE } from '@ari/shared/utils/roles'
import { HIRING_NS, hmDecisionBadgeClass } from './hiringConfig'
import HmAvailabilityFields, {
  toLocalInput,
  toPayload,
  hasBlockingIssue,
  type DraftWindow,
} from './HmAvailabilityFields'

interface ShortlistGatePanelProps {
  applicationId: string
  jobPostingId: string
  /** Trạng thái hồ sơ — quyết định giai đoạn nào của cổng được hiện. */
  status: string
  /** pending | approved | rejected | bypassed. Không có = chưa từng qua cổng. */
  hmDecision?: string | null
  hmDecisionNote?: string | null
  onChanged?: () => void
}

const CARD =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card'
const PRIMARY_BTN =
  'inline-flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-3 py-2.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors'
const GHOST_BTN =
  'inline-flex items-center justify-center gap-1.5 rounded-xl border border-ink-200 dark:border-white/10 px-3 py-1.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5 disabled:opacity-50 disabled:cursor-not-allowed transition-colors'

/**
 * Cổng duyệt shortlist của Hiring Manager trên MỘT hồ sơ (ADR-061, phase 3a).
 *
 * Panel tự biến mất khi tin chưa gán Hiring Manager — không cổng, không banner, không nút: đó là
 * đường mặc định của mọi tin cũ và phải nhìn y hệt như trước khi vai trò này tồn tại.
 *
 * Ba nhân vật, ba thứ nhìn thấy:
 *  - chủ tin/quản trị viên, hồ sơ còn ở giai đoạn CV → nút "Gửi HM duyệt";
 *  - Hiring Manager chính, hồ sơ đang chờ → nút duyệt/từ chối;
 *  - quản trị viên, hồ sơ đang chờ → nút vượt cổng KÈM ô lý do bắt buộc.
 * Quyền thật do server kiểm; ở đây chỉ là chuyện không bày ra nút bấm không được.
 */
export default function ShortlistGatePanel({
  applicationId,
  jobPostingId,
  status,
  hmDecision,
  hmDecisionNote,
  onChanged,
}: ShortlistGatePanelProps) {
  const { t } = useTranslation(HIRING_NS)
  const queryClient = useQueryClient()
  const currentUser = useAuthStore((s) => s.user)
  const role = normalizeRole(currentUser?.role)

  const [rejecting, setRejecting] = useState(false)
  const [bypassing, setBypassing] = useState(false)
  const [note, setNote] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)

  // Duyệt kèm khai lịch rảnh (ADR-067) — mở form thay vì duyệt ngay, vì khung giờ là ĐIỀU KIỆN của
  // việc duyệt chứ không phải một việc để làm sau.
  const [approving, setApproving] = useState(false)
  const [windows, setWindows] = useState<DraftWindow[]>([])

  const { data: job } = useQuery({
    queryKey: ['job', jobPostingId],
    queryFn: () => jobService.getJobPostingById(jobPostingId),
    enabled: !!jobPostingId,
  })

  const fail = (e: unknown, fallback: string) => {
    const message = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
    setActionError(message ?? fallback)
  }

  /**
   * Khung giờ đã khai cho vòng 1 của tin này. Nạp lại để HM thấy mình đã gửi gì — duyệt người thứ
   * hai trong cùng đợt thì không phải nhập lại, và sửa được nếu lịch đổi.
   */
  const { data: existingWindows } = useQuery({
    queryKey: ['hm-availability', jobPostingId, 1],
    queryFn: () => scheduleService.getHmAvailability(jobPostingId, 1),
    enabled: !!jobPostingId,
  })

  const done = () => {
    setActionError(null)
    setRejecting(false)
    setBypassing(false)
    setApproving(false)
    setNote('')
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    queryClient.invalidateQueries({ queryKey: ['hm-availability', jobPostingId, 1] })
    onChanged?.()
  }

  /**
   * Chỉ nạp khung CHƯA bắt đầu vào ô sửa được: luật "giờ bắt đầu phải ở tương lai" sẽ báo sai ngay
   * cho một khung đang diễn ra mà hoàn toàn hợp lệ. Khung đang chạy vẫn còn hiệu lực ở server (lệnh
   * duyệt THÊM khung chứ không thay cả danh sách), nên bỏ khỏi ô nhập là không mất gì.
   */
  const runningCount = (existingWindows ?? []).filter(
    (w) => new Date(w.startTime).getTime() <= Date.now()
  ).length

  const openApprove = () => {
    setActionError(null)
    setWindows(
      (existingWindows ?? [])
        .filter((w) => new Date(w.startTime).getTime() > Date.now())
        .map((w) => ({
          start: toLocalInput(w.startTime),
          end: toLocalInput(w.endTime),
          note: w.note ?? '',
        }))
    )
    setApproving(true)
  }

  const request = useMutation({
    mutationFn: () => hiringTeamService.requestHmApproval(applicationId),
    onSuccess: done,
    onError: (e) => fail(e, t('gate.requestError')),
  })

  const decide = useMutation({
    mutationFn: ({
      decision,
      reason,
      availabilities,
    }: {
      decision: 'approved' | 'rejected'
      reason?: string
      availabilities?: { startTime: string; endTime: string; note?: string | null }[]
    }) => hiringTeamService.submitHmDecision(applicationId, decision, reason, availabilities),
    onSuccess: done,
    onError: (e) => fail(e, t('gate.decideError')),
  })

  const bypass = useMutation({
    mutationFn: (reason: string) => hiringTeamService.bypassHmApproval(applicationId, reason),
    onSuccess: done,
    onError: (e) => fail(e, t('gate.bypassError')),
  })

  // Tin chưa gán Hiring Manager → không có cổng nào để hiện.
  if (!job?.requiresHmApproval) return null

  const isAdmin = role === ROLE.SuperAdmin || role === ROLE.HRAdmin
  const isTheHiringManager = job.hiringManagerUserId === currentUser?.id
  const canRequest = isAdmin || job.createdByUserId === currentUser?.id
  // CHỈ `cv_submitted` — server (`RequestHmApprovalCommandHandler`) từ chối mọi trạng thái khác.
  // Trước đây nhận cả `invited`, nên với ứng viên được mời mà chưa nộp CV thì nút hiện ra sáng
  // nhưng bấm lần nào cũng lỗi, kèm câu thông báo không nói được phải làm gì.
  const inCvPhase = status === 'cv_submitted'
  const awaitingDecision = status === 'hm_review' && hmDecision === 'pending'
  const hmName = job.hiringManagerName || t('gate.theHiringManager')

  return (
    <div className={CARD}>
      <h2 className="mb-1 flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
        <UserCheck className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('gate.title')}
      </h2>
      <p className="mb-4 text-xs text-ink-500 dark:text-ink-400">
        {t('gate.description', { name: hmName })}
      </p>

      {actionError && (
        <div className="mb-3 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-3 py-2 text-sm text-red-700 dark:text-red-400">
          {actionError}
        </div>
      )}

      {hmDecision && (
        <div className="mb-3 flex flex-wrap items-center gap-2">
          <span
            className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${hmDecisionBadgeClass(hmDecision)}`}
          >
            {t(`gate.decision.${hmDecision}`)}
          </span>
          {hmDecisionNote && (
            <span className="text-xs text-ink-500 dark:text-ink-400">{hmDecisionNote}</span>
          )}
        </div>
      )}

      {/* Chủ tin gửi hồ sơ sang bàn của Hiring Manager. */}
      {canRequest && inCvPhase && (
        <button
          type="button"
          className={PRIMARY_BTN}
          onClick={() => request.mutate()}
          disabled={request.isPending}
        >
          <Send className="h-4 w-4" /> {t('gate.request')}
        </button>
      )}

      {/* Hiring Manager quyết định. */}
      {awaitingDecision && isTheHiringManager && (
        <div className="space-y-3">
          {approving ? (
            /* Duyệt = đồng ý CHUYÊN MÔN + gửi giờ mình có mặt được. Hai thứ đi cùng một thao tác vì
               thiếu vế sau thì hồ sơ về hàng chờ xếp lịch mà không có giờ nào xếp được. */
            <div>
              <p className="mb-1.5 text-sm font-medium text-ink-900 dark:text-white">
                {t('gate.availabilityLabel')}
              </p>
              <p className="mb-2 text-xs text-ink-500 dark:text-ink-400">
                {t('gate.availabilityHint')}
              </p>
              <HmAvailabilityFields
                rows={windows}
                onChange={setWindows}
                disabled={decide.isPending}
              />
              <div className="mt-3 flex justify-end gap-2">
                <button type="button" className={GHOST_BTN} onClick={() => setApproving(false)}>
                  {t('common.cancel')}
                </button>
                <button
                  type="button"
                  className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 transition-colors"
                  onClick={() =>
                    decide.mutate({ decision: 'approved', availabilities: toPayload(windows) })
                  }
                  disabled={
                    decide.isPending ||
                    hasBlockingIssue(windows) ||
                    (toPayload(windows).length === 0 && runningCount === 0)
                  }
                >
                  <Check className="h-4 w-4" /> {t('gate.confirmApprove')}
                </button>
              </div>
            </div>
          ) : !rejecting ? (
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 transition-colors"
                onClick={openApprove}
                disabled={decide.isPending}
              >
                <Check className="h-4 w-4" /> {t('gate.approve')}
              </button>
              <button
                type="button"
                className={GHOST_BTN}
                onClick={() => setRejecting(true)}
                disabled={decide.isPending}
              >
                <X className="h-4 w-4" /> {t('gate.reject')}
              </button>
            </div>
          ) : (
            <div>
              <label
                htmlFor="hm-reject-note"
                className="mb-1.5 block text-sm font-medium text-ink-900 dark:text-white"
              >
                {t('gate.rejectReasonLabel')}
              </label>
              <textarea
                id="hm-reject-note"
                rows={3}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder={t('gate.rejectReasonPlaceholder')}
                className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500"
              />
              <div className="mt-2 flex justify-end gap-2">
                <button type="button" className={GHOST_BTN} onClick={() => setRejecting(false)}>
                  {t('common.cancel')}
                </button>
                <button
                  type="button"
                  className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 transition-colors"
                  onClick={() => decide.mutate({ decision: 'rejected', reason: note })}
                  disabled={note.trim().length === 0 || decide.isPending}
                >
                  {t('gate.confirmReject')}
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Quản trị viên vượt cổng — lý do bắt buộc, được ghi audit log và BÁO CHO chính người bị vượt. */}
      {awaitingDecision && isAdmin && !isTheHiringManager && (
        <div className="mt-3 border-t border-ink-100 dark:border-white/10 pt-3">
          {!bypassing ? (
            <button
              type="button"
              className={GHOST_BTN}
              onClick={() => setBypassing(true)}
              disabled={bypass.isPending}
            >
              <ShieldAlert className="h-4 w-4" /> {t('gate.bypass')}
            </button>
          ) : (
            <div>
              <label
                htmlFor="hm-bypass-reason"
                className="mb-1.5 block text-sm font-medium text-ink-900 dark:text-white"
              >
                {t('gate.bypassReasonLabel')}
              </label>
              <textarea
                id="hm-bypass-reason"
                rows={3}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder={t('gate.bypassReasonPlaceholder')}
                className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500"
              />
              <p className="mt-1.5 text-xs text-ink-500 dark:text-ink-400">{t('gate.bypassHint')}</p>
              <div className="mt-2 flex justify-end gap-2">
                <button type="button" className={GHOST_BTN} onClick={() => setBypassing(false)}>
                  {t('common.cancel')}
                </button>
                <button
                  type="button"
                  className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 transition-colors"
                  onClick={() => bypass.mutate(note)}
                  disabled={note.trim().length < 10 || bypass.isPending}
                >
                  {t('gate.confirmBypass')}
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Đang chờ mà mình không phải người quyết cũng không phải quản trị viên → chỉ báo trạng thái. */}
      {awaitingDecision && !isTheHiringManager && !isAdmin && (
        <p className="text-sm text-ink-600 dark:text-ink-400">{t('gate.waiting', { name: hmName })}</p>
      )}
    </div>
  )
}
