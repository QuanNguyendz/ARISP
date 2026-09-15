import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { UserPlus, Trash2, Star, ShieldCheck, ShieldAlert, Check, X, ArrowLeftRight, TriangleAlert } from 'lucide-react'
import { hiringTeamService, type HiringTeamMember } from '@ari/shared/fservices/hiringTeam'
import type { HiringManagerState, HmSignOffStatus } from '@ari/shared/types/job'
import { useAuthStore } from '@ari/shared/store/auth'
import { normalizeRole, ROLE } from '@ari/shared/utils/roles'
import { HIRING_NS } from './hiringConfig'

interface HiringTeamPanelProps {
  jobPostingId: string
  /** Trạng thái vòng đời tin — nút ký chỉ có nghĩa khi tin ĐANG chờ ký (`pending`). */
  jobStatus?: string | null
  /** pending | approved | rejected | bypassed. Không có = tin chưa từng gửi Hiring Manager ký. */
  hmSignOffStatus?: HmSignOffStatus | null
  hmSignOffReason?: string | null
  /** Vị trí HM chính (ADR-068): active | inactive | missing. Khác active = mọi cổng duyệt của tin đang đóng. */
  hiringManagerState?: HiringManagerState | null
  /** Chủ tin và quản trị viên thêm/gỡ được thành viên PHỤ; thành viên đội chỉ xem. */
  canManage: boolean
  /** Gọi sau khi ký duyệt/đổi đội để trang cha nạp lại tin (trạng thái ký nằm trên tin). */
  onChanged?: () => void
}

const CARD =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card'
const PRIMARY_BTN =
  'inline-flex items-center justify-center gap-1.5 rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors'
const GHOST_BTN =
  'inline-flex items-center justify-center gap-1.5 rounded-xl border border-ink-200 dark:border-white/10 px-3 py-1.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5 disabled:opacity-50 disabled:cursor-not-allowed transition-colors'
const FIELD =
  'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500'
const LABEL = 'block text-sm font-medium text-ink-900 dark:text-white mb-1.5'

/** Cùng ngưỡng với mọi lý do trả về / vượt cổng khác ở backend. */
const MIN_REASON = 10

/** Vai trò thành viên PHỤ mà chủ tin thêm được — HM chính không đặt ở đây (ADR-068). */
const SECONDARY_ROLES = ['interviewer', 'observer', 'hiring_manager'] as const

/**
 * Đội tuyển dụng của một tin (ADR-061) — nguồn phạm vi dữ liệu DUY NHẤT của Hiring Manager.
 *
 * Panel này dùng chung cho cả ba khu vực (HR / Recruiter / HM) thay vì mỗi trang một bản: trang
 * chi tiết tin của HR đã 1567 dòng và của Recruiter 1215 dòng, chép thêm một khối vào mỗi bên là
 * cách chắc chắn nhất để hai bên trôi khác nhau — đúng bài học ADR-058/059.
 *
 * ADR-068 — mọi tin luôn có đúng một HM chính:
 * - HM chính KHÔNG gỡ được và KHÔNG đặt được bằng nút "thêm thành viên"; chỉ HR Leader / Super Admin
 *   gán hoặc chuyển, kèm lý do. Recruiter không tự chọn được người kiểm mình ở các cổng duyệt.
 * - Tin thiếu HM hay HM bị khoá hiện cảnh báo "cổng đang đóng" — trước đây câu ở trạng thái trống là
 *   "mọi cổng duyệt đều mở", tức giao diện mô tả đúng cái lỗ hổng như một tính năng.
 *
 * Quyền thật vẫn do server kiểm; đây chỉ là chuyện không bày ra nút bấm không được.
 */
export default function HiringTeamPanel({
  jobPostingId,
  jobStatus,
  hmSignOffStatus,
  hmSignOffReason,
  hiringManagerState,
  canManage,
  onChanged,
}: HiringTeamPanelProps) {
  const { t } = useTranslation(HIRING_NS)
  const queryClient = useQueryClient()
  const currentUser = useAuthStore((s) => s.user)
  const role = normalizeRole(currentUser?.role)
  const isAdmin = role === ROLE.HRAdmin || role === ROLE.SuperAdmin

  const [panel, setPanel] = useState<'none' | 'add' | 'transfer'>('none')
  const [selectedUserId, setSelectedUserId] = useState('')
  const [roleOnJob, setRoleOnJob] = useState<(typeof SECONDARY_ROLES)[number]>('interviewer')
  const [transferReason, setTransferReason] = useState('')
  const [signOffRejecting, setSignOffRejecting] = useState(false)
  const [signOffReason, setSignOffReason] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)

  const { data: team = [], isLoading } = useQuery({
    queryKey: ['hiring-team', jobPostingId],
    queryFn: () => hiringTeamService.getTeam(jobPostingId),
    enabled: !!jobPostingId,
  })

  const { data: options = [] } = useQuery({
    queryKey: ['hiring-manager-options', jobPostingId],
    queryFn: () => hiringTeamService.getHiringManagerOptions(jobPostingId),
    enabled: panel !== 'none',
  })

  const fail = (e: unknown, fallback: string) => {
    const message = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
    setActionError(message ?? fallback)
  }

  const closeForms = () => {
    setPanel('none')
    setSelectedUserId('')
    setTransferReason('')
    setRoleOnJob('interviewer')
  }

  const refresh = () => {
    setActionError(null)
    queryClient.invalidateQueries({ queryKey: ['hiring-team', jobPostingId] })
    onChanged?.()
  }

  const addMember = useMutation({
    mutationFn: () => hiringTeamService.addMember(jobPostingId, { userId: selectedUserId, roleOnJob }),
    onSuccess: () => {
      closeForms()
      refresh()
    },
    onError: (e) => fail(e, t('team.addError')),
  })

  const setPrimary = useMutation({
    mutationFn: () => hiringTeamService.setPrimaryHiringManager(jobPostingId, selectedUserId, transferReason.trim()),
    onSuccess: () => {
      closeForms()
      refresh()
    },
    onError: (e) => fail(e, t('transfer.error')),
  })

  const removeMember = useMutation({
    mutationFn: (memberId: string) => hiringTeamService.removeMember(jobPostingId, memberId),
    onSuccess: refresh,
    onError: (e) => fail(e, t('team.removeError')),
  })

  const signOff = useMutation({
    mutationFn: ({ decision, reason }: { decision: 'approved' | 'rejected'; reason?: string }) =>
      hiringTeamService.submitJobSignOff(jobPostingId, decision, reason),
    onSuccess: () => {
      setSignOffRejecting(false)
      setSignOffReason('')
      refresh()
    },
    onError: (e) => fail(e, t('signOff.error')),
  })

  const primary = team.find((m) => m.isPrimary && m.roleOnJob === 'hiring_manager')
  const isTheHiringManager = !!primary && primary.userId === currentUser?.id
  const isHmRole = role === ROLE.HiringManager
  const alreadyOnTeam = new Set(team.map((m) => m.userId))

  const displayName = (m: HiringTeamMember) => m.fullName?.trim() || m.email

  // Thiếu người hành động được ở vị trí HM chính → mọi cổng của tin đóng (ADR-068).
  const gateClosed = hiringManagerState === 'missing' || hiringManagerState === 'inactive' || (!isLoading && !primary)
  const canSign =
    isTheHiringManager && isHmRole && jobStatus === 'pending' && hmSignOffStatus === 'pending'

  const openPanel = (next: 'add' | 'transfer') => {
    setActionError(null)
    setSelectedUserId('')
    setTransferReason('')
    setPanel(next)
  }

  return (
    <div className={CARD}>
      <div className="flex flex-wrap items-start justify-between gap-3 mb-4">
        <div>
          <h2 className="text-lg font-semibold text-ink-900 dark:text-white">{t('team.title')}</h2>
          <p className="mt-0.5 text-sm text-ink-500 dark:text-ink-400">{t('team.description')}</p>
        </div>
        {panel === 'none' && (
          <div className="flex flex-wrap gap-2">
            {isAdmin && (
              <button type="button" className={GHOST_BTN} onClick={() => openPanel('transfer')}>
                <ArrowLeftRight className="w-4 h-4" /> {primary ? t('transfer.change') : t('transfer.assign')}
              </button>
            )}
            {canManage && (
              <button type="button" className={GHOST_BTN} onClick={() => openPanel('add')}>
                <UserPlus className="w-4 h-4" /> {t('team.add')}
              </button>
            )}
          </div>
        )}
      </div>

      {actionError && (
        <div className="mb-4 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-4 py-3 text-sm text-red-700 dark:text-red-400">
          {actionError}
        </div>
      )}

      {/* ADR-068: vị trí HM chính không có người hành động được — nói to lên, kèm việc phải làm. */}
      {gateClosed && !isLoading && (
        <div className="mb-4 flex gap-2 rounded-xl border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/10 px-4 py-3 text-sm text-amber-800 dark:text-amber-300">
          <TriangleAlert className="w-4 h-4 mt-0.5 shrink-0" />
          <p>
            {hiringManagerState === 'inactive' && primary
              ? t('team.inactive', { name: displayName(primary) })
              : t('team.missing')}
          </p>
        </div>
      )}

      {/* Trạng thái ký duyệt mô tả công việc. */}
      {primary && hmSignOffStatus && (
        <div
          className={`mb-4 rounded-xl border px-4 py-3 text-sm ${
            hmSignOffStatus === 'approved'
              ? 'border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-400'
              : hmSignOffStatus === 'rejected'
                ? 'border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-400'
                : 'border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-400'
          }`}
        >
          <p className="flex items-center gap-2 font-medium">
            {hmSignOffStatus === 'approved' ? (
              <ShieldCheck className="w-4 h-4 shrink-0" />
            ) : (
              <ShieldAlert className="w-4 h-4 shrink-0" />
            )}
            {t(`signOff.status.${hmSignOffStatus}`, { name: displayName(primary) })}
          </p>
          {hmSignOffReason && (
            <p className="mt-1.5">
              {hmSignOffStatus === 'bypassed'
                ? t('signOff.bypassReason', { reason: hmSignOffReason })
                : t('signOff.reason', { reason: hmSignOffReason })}
            </p>
          )}
        </div>
      )}

      {/* Chính người đang giữ cổng mới thấy nút ký — và chỉ khi tin ĐANG chờ ký. */}
      {canSign && (
        <div className="mb-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/60 dark:bg-white/5 p-4">
          <p className="text-sm font-medium text-ink-900 dark:text-white">{t('signOff.prompt')}</p>
          {!signOffRejecting ? (
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                type="button"
                className={PRIMARY_BTN}
                onClick={() => signOff.mutate({ decision: 'approved' })}
                disabled={signOff.isPending}
              >
                <Check className="w-4 h-4" /> {t('signOff.approve')}
              </button>
              <button
                type="button"
                className={GHOST_BTN}
                onClick={() => setSignOffRejecting(true)}
                disabled={signOff.isPending}
              >
                <X className="w-4 h-4" /> {t('signOff.requestChanges')}
              </button>
            </div>
          ) : (
            <div className="mt-3">
              <label htmlFor="signoff-reason" className={LABEL}>
                {t('signOff.reasonLabel')}
              </label>
              <textarea
                id="signoff-reason"
                rows={3}
                value={signOffReason}
                onChange={(e) => setSignOffReason(e.target.value)}
                placeholder={t('signOff.reasonPlaceholder')}
                className={FIELD}
              />
              <p className="mt-1.5 text-xs text-ink-500 dark:text-ink-400">{t('signOff.reasonHint')}</p>
              <div className="mt-2 flex justify-end gap-2">
                <button type="button" className={GHOST_BTN} onClick={() => setSignOffRejecting(false)}>
                  {t('common.cancel')}
                </button>
                <button
                  type="button"
                  className={PRIMARY_BTN}
                  onClick={() => signOff.mutate({ decision: 'rejected', reason: signOffReason.trim() })}
                  disabled={signOffReason.trim().length < MIN_REASON || signOff.isPending}
                >
                  {t('signOff.confirmRequestChanges')}
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-ink-500 dark:text-ink-400">{t('common.loading')}</p>
      ) : team.length === 0 ? (
        <p className="text-sm text-ink-500 dark:text-ink-400">{t('team.empty')}</p>
      ) : (
        <ul className="divide-y divide-ink-100 dark:divide-white/10">
          {team.map((member) => (
            <li key={member.id} className="flex items-center justify-between gap-3 py-3">
              <div className="min-w-0">
                <p className="flex flex-wrap items-center gap-1.5 font-medium text-ink-900 dark:text-white">
                  <span className="truncate">{displayName(member)}</span>
                  {member.isPrimary ? (
                    <span
                      title={t('team.primaryHint')}
                      className="inline-flex shrink-0 items-center gap-1 rounded-full bg-sky-100 dark:bg-sky-500/20 px-2 py-0.5 text-xs font-semibold text-sky-700 dark:text-sky-400"
                    >
                      <Star className="w-3 h-3" /> {t('team.primary')}
                    </span>
                  ) : (
                    <span className="inline-flex shrink-0 rounded-full bg-ink-100 dark:bg-white/10 px-2 py-0.5 text-xs font-medium text-ink-600 dark:text-ink-300">
                      {t(`team.roles.${member.roleOnJob}`, { defaultValue: member.roleOnJob })}
                    </span>
                  )}
                </p>
                <p className="truncate text-sm text-ink-500 dark:text-ink-400">
                  {member.email}
                  {member.department ? ` · ${member.department}` : ''}
                </p>
              </div>
              {/* HM chính không gỡ được — chỉ chuyển (ADR-068). */}
              {canManage && !member.isPrimary && (
                <button
                  type="button"
                  aria-label={t('team.remove')}
                  title={t('team.remove')}
                  className="shrink-0 rounded-lg p-1.5 text-ink-500 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400 transition-colors"
                  onClick={() => removeMember.mutate(member.id)}
                  disabled={removeMember.isPending}
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {panel !== 'none' && (
        <div className="mt-4 border-t border-ink-100 dark:border-white/10 pt-4">
          <label htmlFor="hm-select" className={LABEL}>
            {panel === 'transfer' ? t('transfer.selectLabel') : t('team.selectLabel')}
          </label>
          <select
            id="hm-select"
            value={selectedUserId}
            onChange={(e) => setSelectedUserId(e.target.value)}
            className={FIELD}
          >
            <option value="">{t('team.selectPlaceholder')}</option>
            {options
              .filter((o) => (panel === 'transfer' ? o.id !== primary?.userId : !alreadyOnTeam.has(o.id)))
              .map((o) => (
                <option key={o.id} value={o.id}>
                  {o.fullName?.trim() || o.email}
                  {o.department ? ` — ${o.department}` : ''}
                  {/* Cùng phòng ban với tin được server xếp lên đầu; đây chỉ là GỢI Ý, phòng ban
                      không phải điều kiện quyền (ADR-061). */}
                  {o.matchesJobDepartment ? ` ${t('team.sameDepartment')}` : ''}
                </option>
              ))}
          </select>
          <p className="mt-1.5 text-xs text-ink-500 dark:text-ink-400">{t('team.selectHint')}</p>

          {panel === 'add' ? (
            <div className="mt-3">
              <label htmlFor="role-on-job" className={LABEL}>
                {t('team.roleLabel')}
              </label>
              <select
                id="role-on-job"
                value={roleOnJob}
                onChange={(e) => setRoleOnJob(e.target.value as (typeof SECONDARY_ROLES)[number])}
                className={FIELD}
              >
                {SECONDARY_ROLES.map((r) => (
                  <option key={r} value={r}>
                    {t(`team.roles.${r}`)}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <div className="mt-3">
              <label htmlFor="transfer-reason" className={LABEL}>
                {t('transfer.reasonLabel')}
              </label>
              <textarea
                id="transfer-reason"
                rows={2}
                value={transferReason}
                onChange={(e) => setTransferReason(e.target.value)}
                placeholder={t('transfer.reasonPlaceholder')}
                className={FIELD}
              />
              <p className="mt-1.5 text-xs text-ink-500 dark:text-ink-400">{t('transfer.hint')}</p>
            </div>
          )}

          <div className="mt-3 flex justify-end gap-2">
            <button type="button" className={GHOST_BTN} onClick={closeForms}>
              {t('common.cancel')}
            </button>
            {panel === 'add' ? (
              <button
                type="button"
                className={PRIMARY_BTN}
                onClick={() => addMember.mutate()}
                disabled={!selectedUserId || addMember.isPending}
              >
                {t('team.confirmAdd')}
              </button>
            ) : (
              <button
                type="button"
                className={PRIMARY_BTN}
                onClick={() => setPrimary.mutate()}
                disabled={!selectedUserId || transferReason.trim().length < MIN_REASON || setPrimary.isPending}
              >
                {t('transfer.confirm')}
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
