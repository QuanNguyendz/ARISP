import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import {
  Bell,
  Check,
  Clock,
  Copy,
  Eye,
  KeyRound,
  MessageSquare,
  RefreshCw,
  UserX,
} from 'lucide-react'
import { applicationService } from '@ari/shared/fservices/application'
import { interviewService, type SlotCandidate } from '@ari/shared/fservices/interview'
import { RescheduleModal } from './RescheduleModal'
import { canReschedule, declineReasonLabelKey, isRejected, stateOf } from './candidateState'
import { fmtDur, initials } from './format'
import { INTERVIEWS_NS, type WorkspaceConfig } from './workspaceConfig'

export function CandidateRow({
  c,
  jobId,
  currentSlotId,
  currentRoundNumber,
  isSelected,
  onToggleSelect,
  onReload,
  workspace,
}: {
  c: SlotCandidate
  jobId: string
  currentSlotId: string
  currentRoundNumber: number
  isSelected: boolean
  onToggleSelect: () => void
  onReload: () => void
  workspace: WorkspaceConfig
}) {
  const { t } = useTranslation(INTERVIEWS_NS)
  const navigate = useNavigate()
  const [sendingReminder, setSendingReminder] = useState(false)
  const [rejecting, setRejecting] = useState(false)
  const [generatingCode, setGeneratingCode] = useState(false)
  const [copied, setCopied] = useState(false)
  const [toast, setToast] = useState<string | null>(null)
  const [showReschedule, setShowReschedule] = useState(false)

  const state = stateOf(c)
  const rejected = isRejected(c)
  // Lịch đã đóng → không cấp mã / nhắc lịch nữa. Nhưng VẪN dời lịch được (trừ khi hồ sơ bị loại):
  // xếp lại cho người báo bận hoặc quá hạn chính là công dụng chính của nút "Dời lịch".
  const closed = state.closed

  const handleGenCode = async () => {
    setGeneratingCode(true)
    try {
      await interviewService.generateCode(c.applicationId, c.roundNumber)
      onReload()
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setToast(err?.response?.data?.message || t('candidate.genCodeError'))
      setTimeout(() => setToast(null), 3000)
    } finally {
      setGeneratingCode(false)
    }
  }

  const handleCopyCode = async (code: string) => {
    try {
      await navigator.clipboard.writeText(code)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      /* clipboard bị chặn — bỏ qua */
    }
  }

  const handleRemind = async () => {
    setSendingReminder(true)
    try {
      const res = await interviewService.sendBookingReminder(c.bookingId)
      setToast(res.message || t('candidate.remindSuccess'))
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setToast(err?.response?.data?.message || t('candidate.remindError'))
    } finally {
      setSendingReminder(false)
      setTimeout(() => setToast(null), 3000)
    }
  }

  const handleReject = async () => {
    if (!window.confirm(t('candidate.rejectConfirm', { name: c.candidateName }))) return
    setRejecting(true)
    try {
      await applicationService.rejectApplication(c.applicationId)
      onReload()
    } catch {
      setToast(t('candidate.rejectError'))
      setTimeout(() => setToast(null), 3000)
    } finally {
      setRejecting(false)
    }
  }

  const verdictCls =
    c.verdict?.toLowerCase() === 'pass'
      ? 'bg-emerald-50 text-emerald-600 border border-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-400 dark:border-emerald-500/20'
      : 'bg-red-50 text-red-600 border border-red-200 dark:bg-red-500/10 dark:text-red-400 dark:border-red-500/20'

  const reasonLabelKey = declineReasonLabelKey(c)

  return (
    <div
      className={`py-3 px-4 rounded-xl transition-colors space-y-2 ${
        isSelected ? 'bg-brand-50/60 dark:bg-brand-500/10' : 'hover:bg-ink-50 dark:hover:bg-white/5'
      }`}
    >
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <input
            type="checkbox"
            disabled={rejected}
            checked={isSelected}
            onChange={onToggleSelect}
            className="w-4 h-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500 cursor-pointer shrink-0 disabled:opacity-40 disabled:cursor-not-allowed"
          />
          <div className="w-9 h-9 shrink-0 rounded-full bg-gradient-to-br from-brand-500 to-ai-600 flex items-center justify-center text-xs font-bold text-white shadow-sm">
            {initials(c.candidateName)}
          </div>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-ink-900 dark:text-white truncate">{c.candidateName}</p>
            <p className="text-xs text-ink-400 truncate">{c.candidateEmail}</p>
          </div>
        </div>

        <div className="flex items-center gap-2 flex-shrink-0 flex-wrap justify-end">
          <span className={`px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${state.chip}`}>
            {t(state.labelKey)}
          </span>

          {c.verdict && (
            <span className={`px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${verdictCls}`}>
              {c.verdict === 'pass' ? t('candidate.pass') : t('candidate.notPass')}
              {c.overallScore != null && ` · ${t('candidate.score', { score: c.overallScore })}`}
            </span>
          )}

          {/* Huy hiệu phiên: chỉ khi lịch còn hiệu lực. Phiên đã lọc theo VÒNG ở backend nên không
              còn cảnh phiên vòng 2 hiện trên dòng vòng 1; đây là lớp phòng vệ thứ hai. */}
          {c.sessionStatus && !closed && (
            <span
              className={`px-2.5 py-0.5 rounded-full text-[11px] font-medium ${
                c.sessionStatus === 'completed'
                  ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                  : c.sessionStatus === 'active'
                    ? 'bg-brand-100 text-brand-700 dark:bg-brand-500/20 dark:text-brand-400 animate-pulse'
                    : 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
              }`}
            >
              {c.sessionStatus === 'completed'
                ? t('candidate.sessionCompleted')
                : c.sessionStatus === 'active'
                  ? t('candidate.sessionActive')
                  : c.sessionStatus}
            </span>
          )}

          {fmtDur(c.durationSeconds) && (
            <span className="hidden sm:flex items-center gap-1 text-xs text-ink-400">
              <Clock className="w-3 h-3" />
              {fmtDur(c.durationSeconds)}
            </span>
          )}

          {c.interviewCode ? (
            <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-violet-50 dark:bg-violet-500/15 border border-violet-200 dark:border-violet-500/30 text-xs font-mono font-bold text-violet-700 dark:text-violet-300">
              <KeyRound className="w-3.5 h-3.5 text-violet-500 shrink-0" />
              <span>{c.interviewCode}</span>
              <button
                type="button"
                onClick={() => handleCopyCode(c.interviewCode!)}
                className="p-1 hover:bg-violet-100 dark:hover:bg-violet-500/20 rounded transition-colors text-violet-600 dark:text-violet-300"
                title={t('candidate.copyCode')}
              >
                {copied ? <Check className="w-3.5 h-3.5 text-emerald-500" /> : <Copy className="w-3.5 h-3.5" />}
              </button>
            </div>
          ) : (
            <button
              disabled={generatingCode || closed}
              onClick={handleGenCode}
              title={
                closed
                  ? t('candidate.closedTitle', { state: t(state.labelKey).toLowerCase() })
                  : t('candidate.genCodeTitle')
              }
              className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-violet-200 dark:border-violet-500/20 bg-violet-50/50 dark:bg-violet-500/10 text-xs font-medium text-violet-700 dark:text-violet-300 hover:bg-violet-100 dark:hover:bg-violet-500/20 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <KeyRound className="w-3.5 h-3.5" />
              <span>{generatingCode ? t('candidate.generating') : t('candidate.genCode')}</span>
            </button>
          )}

          <button
            disabled={sendingReminder || closed}
            onClick={handleRemind}
            title={
              closed
                ? t('candidate.closedTitle', { state: t(state.labelKey).toLowerCase() })
                : t('candidate.remindTitle')
            }
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-amber-200 dark:border-amber-500/20 bg-amber-50/50 dark:bg-amber-500/10 text-xs font-medium text-amber-700 dark:text-amber-400 hover:bg-amber-100 dark:hover:bg-amber-500/20 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <Bell className="w-3.5 h-3.5" />
            <span className="hidden md:inline">
              {sendingReminder ? t('candidate.sending') : t('candidate.remind')}
            </span>
          </button>

          {/* Dời lịch chỉ mở cho người ĐÃ BÁO BẬN — xem canReschedule. */}
          <button
            disabled={!canReschedule(c)}
            onClick={() => setShowReschedule(true)}
            title={
              canReschedule(c)
                ? t('candidate.rescheduleTitle')
                : t('candidate.rescheduleDisabledTitle')
            }
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-brand-200 dark:border-brand-500/20 bg-brand-50/50 dark:bg-brand-500/10 text-xs font-medium text-brand-700 dark:text-brand-400 hover:bg-brand-100 dark:hover:bg-brand-500/20 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            <span className="hidden md:inline">{t('candidate.reschedule')}</span>
          </button>

          <button
            disabled={rejecting || rejected}
            onClick={handleReject}
            title={rejected ? t('candidate.rejectedTitle') : t('candidate.rejectTitle')}
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-red-200 dark:border-red-500/20 bg-red-50/50 dark:bg-red-500/10 text-xs font-medium text-red-700 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-500/20 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <UserX className="w-3.5 h-3.5" />
            <span className="hidden md:inline">
              {rejecting
                ? t('candidate.rejecting')
                : rejected
                  ? t('candidate.rejected')
                  : t('candidate.reject')}
            </span>
          </button>

          <button
            onClick={() =>
              navigate(
                c.evaluationId
                  ? workspace.evaluationHref(c.evaluationId)
                  : workspace.candidateHref(c.applicationId)
              )
            }
            className="flex items-center gap-1 px-3 py-1.5 rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-xs font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
          >
            <Eye className="w-3.5 h-3.5" /> {t('candidate.view')}
          </button>
        </div>
      </div>

      {/* Quá hạn xác nhận: nói thẳng chuyện gì xảy ra, không in lại câu "[Hệ thống] …" của backend. */}
      {c.candidateState === 'expired_no_response' && (
        <div className="mt-1 text-xs bg-orange-50 dark:bg-orange-500/10 text-orange-700 dark:text-orange-300 p-2.5 rounded-xl border border-orange-200 dark:border-orange-500/20 flex items-start gap-2">
          <MessageSquare className="w-4 h-4 shrink-0 mt-0.5 text-orange-500" />
          <span>{t('candidate.expiredBanner')}</span>
        </div>
      )}

      {reasonLabelKey && c.declineReason && (
        <div className="mt-1 text-xs bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-300 p-2.5 rounded-xl border border-red-200 dark:border-red-500/20 flex items-start gap-2">
          <MessageSquare className="w-4 h-4 shrink-0 mt-0.5 text-red-500" />
          <div className="min-w-0">
            <span className="font-bold">{t(reasonLabelKey)} </span>
            <span className="break-words">&quot;{c.declineReason}&quot;</span>
          </div>
        </div>
      )}

      {toast && (
        <p className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-500/10 px-3 py-1 rounded-lg">
          {toast}
        </p>
      )}

      {showReschedule && (
        <RescheduleModal
          candidates={[c]}
          jobId={jobId}
          currentSlotId={currentSlotId}
          currentRoundNumber={currentRoundNumber}
          onClose={() => setShowReschedule(false)}
          onSuccess={onReload}
        />
      )}
    </div>
  )
}
