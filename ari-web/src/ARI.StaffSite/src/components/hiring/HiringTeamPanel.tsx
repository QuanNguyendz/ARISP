import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { UserPlus, Trash2, Star, ShieldCheck, ShieldAlert, Check, X } from 'lucide-react'
import { hiringTeamService, type HiringTeamMember } from '@ari/shared/fservices/hiringTeam'
import { useAuthStore } from '@ari/shared/store/auth'
import { normalizeRole, ROLE } from '@ari/shared/utils/roles'
import { HIRING_NS } from './hiringConfig'

interface HiringTeamPanelProps {
  jobPostingId: string
  /** Trạng thái ký duyệt JD của tin: pending | approved | rejected. Không có = tin chưa gán ai. */
  hmSignOffStatus?: string | null
  hmSignOffReason?: string | null
  /** Chủ tin và quản trị viên mới sửa được đội; thành viên đội chỉ xem. */
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

/**
 * Đội tuyển dụng của một tin (ADR-061) — nguồn phạm vi dữ liệu DUY NHẤT của Hiring Manager.
 *
 * Panel này dùng chung cho cả ba khu vực (HR / Recruiter / HM) thay vì mỗi trang một bản: trang
 * chi tiết tin của HR đã 1567 dòng và của Recruiter 1215 dòng, chép thêm một khối vào mỗi bên là
 * cách chắc chắn nhất để hai bên trôi khác nhau — đúng bài học ADR-058/059.
 *
 * Ai thấy gì: mọi thành viên đội đọc được danh sách; chỉ chủ tin/quản trị viên thêm–gỡ được
 * (`canManage`); riêng nút ký duyệt JD chỉ hiện cho đúng người đang là Hiring Manager chính của
 * tin — quyền thật vẫn do server kiểm, đây chỉ là chuyện không bày ra nút bấm không được.
 */
export default function HiringTeamPanel({
  jobPostingId,
  hmSignOffStatus,
  hmSignOffReason,
  canManage,
  onChanged,
}: HiringTeamPanelProps) {
  const { t } = useTranslation(HIRING_NS)
  const queryClient = useQueryClient()
  const currentUser = useAuthStore((s) => s.user)

  const [adding, setAdding] = useState(false)
  const [selectedUserId, setSelectedUserId] = useState('')
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
    enabled: adding,
  })

  const fail = (e: unknown, fallback: string) => {
    const message = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
    setActionError(message ?? fallback)
  }

  const refresh = () => {
    setActionError(null)
    queryClient.invalidateQueries({ queryKey: ['hiring-team', jobPostingId] })
    onChanged?.()
  }

  const addMember = useMutation({
    mutationFn: (userId: string) =>
      hiringTeamService.addMember(jobPostingId, { userId, isPrimary: true }),
    onSuccess: () => {
      setAdding(false)
      setSelectedUserId('')
      refresh()
    },
    onError: (e) => fail(e, t('team.addError')),
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
  const isHmRole = normalizeRole(currentUser?.role) === ROLE.HiringManager
  const alreadyOnTeam = new Set(team.map((m) => m.userId))

  const displayName = (m: HiringTeamMember) => m.fullName?.trim() || m.email

  return (
    <div className={CARD}>
      <div className="flex items-start justify-between gap-3 mb-4">
        <div>
          <h2 className="text-lg font-semibold text-ink-900 dark:text-white">{t('team.title')}</h2>
          <p className="mt-0.5 text-sm text-ink-500 dark:text-ink-400">{t('team.description')}</p>
        </div>
        {canManage && !adding && (
          <button type="button" className={GHOST_BTN} onClick={() => setAdding(true)}>
            <UserPlus className="w-4 h-4" /> {t('team.add')}
          </button>
        )}
      </div>

      {actionError && (
        <div className="mb-4 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 px-4 py-3 text-sm text-red-700 dark:text-red-400">
          {actionError}
        </div>
      )}

      {/* Trạng thái ký duyệt mô tả công việc — chỉ có ý nghĩa khi tin đã gán Hiring Manager. */}
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
          {hmSignOffReason && <p className="mt-1.5">{t('signOff.reason', { reason: hmSignOffReason })}</p>}
        </div>
      )}

      {/* Chính người đang giữ cổng mới thấy nút ký. */}
      {isTheHiringManager && isHmRole && hmSignOffStatus === 'pending' && (
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
              <label
                htmlFor="signoff-reason"
                className="block text-sm font-medium text-ink-900 dark:text-white mb-1.5"
              >
                {t('signOff.reasonLabel')}
              </label>
              <textarea
                id="signoff-reason"
                rows={3}
                value={signOffReason}
                onChange={(e) => setSignOffReason(e.target.value)}
                placeholder={t('signOff.reasonPlaceholder')}
                className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500"
              />
              <div className="mt-2 flex justify-end gap-2">
                <button type="button" className={GHOST_BTN} onClick={() => setSignOffRejecting(false)}>
                  {t('common.cancel')}
                </button>
                <button
                  type="button"
                  className={PRIMARY_BTN}
                  onClick={() => signOff.mutate({ decision: 'rejected', reason: signOffReason })}
                  disabled={signOffReason.trim().length === 0 || signOff.isPending}
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
                <p className="flex items-center gap-1.5 font-medium text-ink-900 dark:text-white">
                  <span className="truncate">{displayName(member)}</span>
                  {member.isPrimary && (
                    <span
                      title={t('team.primaryHint')}
                      className="inline-flex shrink-0 items-center gap-1 rounded-full bg-sky-100 dark:bg-sky-500/20 px-2 py-0.5 text-xs font-semibold text-sky-700 dark:text-sky-400"
                    >
                      <Star className="w-3 h-3" /> {t('team.primary')}
                    </span>
                  )}
                </p>
                <p className="truncate text-sm text-ink-500 dark:text-ink-400">
                  {member.email}
                  {member.department ? ` · ${member.department}` : ''}
                </p>
              </div>
              {canManage && (
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

      {adding && (
        <div className="mt-4 border-t border-ink-100 dark:border-white/10 pt-4">
          <label
            htmlFor="hm-select"
            className="block text-sm font-medium text-ink-900 dark:text-white mb-1.5"
          >
            {t('team.selectLabel')}
          </label>
          <select
            id="hm-select"
            value={selectedUserId}
            onChange={(e) => setSelectedUserId(e.target.value)}
            className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <option value="">{t('team.selectPlaceholder')}</option>
            {options
              .filter((o) => !alreadyOnTeam.has(o.id))
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
          <div className="mt-3 flex justify-end gap-2">
            <button
              type="button"
              className={GHOST_BTN}
              onClick={() => {
                setAdding(false)
                setSelectedUserId('')
              }}
            >
              {t('common.cancel')}
            </button>
            <button
              type="button"
              className={PRIMARY_BTN}
              onClick={() => addMember.mutate(selectedUserId)}
              disabled={!selectedUserId || addMember.isPending}
            >
              {t('team.confirmAdd')}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
