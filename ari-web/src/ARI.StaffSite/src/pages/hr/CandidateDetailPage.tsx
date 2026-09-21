import { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, FileText, ExternalLink, Mail, Phone, Briefcase, UserCheck } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ErrorAlert } from '@ari/shared/ui'
import AssignSchedulePanel from '@ari/shared/ui/AssignSchedulePanel'
import { affectsApplication, onApplicationsChanged } from '@ari/shared/realtime/applicationRealtime'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import { applicationService } from '@ari/shared/fservices/application'
import InterviewResultsCard from '@/components/evaluations/InterviewResultsCard'
import CvScoreBreakdown from '@/components/cvScore/CvScoreBreakdown'
import CvScoreBadge from '@/components/cvScore/CvScoreBadge'
import InterviewCodeCard from '@/components/jobCandidates/InterviewCodeCard'
import type { HrApplicationItem } from '@ari/shared/types/application'
import { CandidateOnlineProfileModal } from './CandidatesPage'
import {
  appStatusBadge,
  appStatusLabel,
  initials,
  scoreColor,
  timeAgo,
} from '../recruiter/_jobUi'
import { JobDetailSkeleton } from '../recruiter/_skeletons'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import { formatScore } from '@ari/shared/utils/format'
import ShortlistGatePanel from '@/components/hiring/ShortlistGatePanel'
import EmailHistoryPanel from '@/components/hiring/EmailHistoryPanel'
import CandidateOfferPanel from '@/components/offers/CandidateOfferPanel'

function apiErr(e: unknown, fallback: string): string {
  return (e as { response?: { data?: { message?: string } } })?.response?.data?.message || fallback
}

export default function HrCandidateDetailPage() {
  const { t } = useTranslation('modules/hr/candidateDetail')
  const { id } = useParams<{ id: string }>()
  const { openDocument } = useDocumentViewer()
  const [app, setApp] = useState<HrApplicationItem | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [showProfileModal, setShowProfileModal] = useState(false)

  useEffect(() => {
    if (!id) return
      ; (async () => {
        setLoading(true)
        setError('')
        try {
          setApp(await applicationService.getHrApplicationById(id))
        } catch (e) {
          setError(apiErr(e, t('loadingError')))
        } finally {
          setLoading(false)
        }
      })()
  }, [id, t])

  // Realtime: hồ sơ này đổi (duyệt, xếp lịch, điểm CV chấm nền xong…) → tải lại ngầm, không bật khung tải.
  useEffect(() => {
    if (!id) return
    let active = true
    const off = onApplicationsChanged((detail) => {
      if (!affectsApplication(detail, id)) return
      applicationService
        .getHrApplicationById(id)
        .then((data) => {
          if (active) setApp(data)
        })
        .catch(() => {
          /* giữ hồ sơ đang hiển thị nếu lượt tải ngầm lỗi */
        })
    })
    return () => {
      active = false
      off()
    }
  }, [id])

  const refreshApp = async () => {
    if (!id) return
    try {
      setApp(await applicationService.getHrApplicationById(id))
    } catch {
      /* giữ nguyên hồ sơ hiện tại nếu refetch lỗi */
    }
  }

  if (loading) return <JobDetailSkeleton />
  if (!app) {
    return (
      <div className="p-6 lg:p-8">
        <ErrorAlert message={error || t('notFound')} />
        <Link
          to="/hr/candidates"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← {t('back')}
        </Link>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-ink-50 p-4 sm:p-6 dark:bg-ink-950 lg:p-8">
      <Link
        to="/hr/candidates"
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('backToList')}
      </Link>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <div className="mb-6 flex flex-col gap-3 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3 sm:gap-4">
          <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-base sm:text-lg font-bold text-white sm:h-16 sm:w-16">
            {initials(app.candidateName || app.candidateEmail)}
          </span>
          <div className="min-w-0">
            <h1 className="text-xl font-bold text-ink-900 dark:text-white">
              {app.candidateName || t('candidate')}
            </h1>
            <p className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
              <span className="flex items-center gap-1">
                <Mail className="h-3.5 w-3.5" />
                {app.candidateEmail}
              </span>
              {app.candidatePhone ? (
                <span className="flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5" />
                  {app.candidatePhone}
                </span>
              ) : null}
              <span className="flex items-center gap-1">
                <Briefcase className="h-3.5 w-3.5" />
                {app.jobTitle || t('position')}
              </span>
            </p>
            <div className="mt-2 flex items-center gap-2">
              <span
                className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${appStatusBadge(app.status)}`}
              >
                {appStatusLabel(app.status)}
              </span>
              <span className="text-xs text-ink-400">
                {t('applied')} {timeAgo(app.createdAt)}
              </span>
            </div>
          </div>
        </div>
        {(app.matchScore != null || app.cvScoreStatus) && (
          <div className="text-center">
            {app.matchScore != null ? (
              <div className={`text-3xl font-bold ${scoreColor(app.matchScore)}`}>
                {formatScore(app.matchScore)}
              </div>
            ) : (
              <CvScoreBadge status={app.cvScoreStatus} retryAt={app.cvScoreRetryAt} />
            )}
            <div className="text-xs text-ink-400">{t('matchCVJD')}</div>
          </div>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          {/* Điểm CV kèm cách tính (ADR-070): từng tiêu chí, trọng số, bằng chứng — không chỉ một con số. */}
          <CvScoreBreakdown score={app.cvScore} />

          {/* Kết quả phỏng vấn theo vòng (ADR-069): ca đã gán · diễn biến · báo cáo AI · video · transcript,
              kèm nút tới đúng báo cáo. Thay cho hai khối cũ (danh sách đánh giá + danh sách phiên) vốn không
              có video/transcript, không nói được "AI đang chấm", và một khối không bấm vào đâu được. */}
          <InterviewResultsCard
            applicationId={app.id}
            evaluationHref={(evaluationId) => `/hr/evaluations?id=${evaluationId}`}
          />
        </div>

        <div className="space-y-6">
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">
              {t('actions')}
            </h2>
            <div className="space-y-2">
              <button
                type="button"
                onClick={() => setShowProfileModal(true)}
                className="flex w-full items-center gap-3 rounded-xl border border-purple-200 dark:border-purple-500/20 bg-purple-50/50 dark:bg-purple-500/10 px-3 py-3 text-sm font-semibold text-purple-700 dark:text-purple-300 hover:bg-purple-100 dark:hover:bg-purple-500/20 transition-colors cursor-pointer"
              >
                <UserCheck className="h-4 w-4 text-purple-600 dark:text-purple-400" /> Xem Profile Online
              </button>
              {app.cvFileUrl && (
                <button
                  type="button"
                  onClick={() =>
                    openDocument(
                      resolveAssetUrl(app.cvFileUrl),
                      `${app.candidateName || t('candidate')} - CV`
                    )
                  }
                  className="flex w-full items-center gap-3 rounded-xl border border-ink-100 dark:border-white/10 p-3 text-sm text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/5"
                >
                  <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('viewCV')}{' '}
                  <ExternalLink className="ml-auto h-3.5 w-3.5 text-ink-400" />
                </button>
              )}
              {/* Cùng thẻ cấp mã với danh sách ứng viên của tin — vòng do server chọn theo lịch. */}
              <InterviewCodeCard applicationId={app.id} className="" />
            </div>
          </div>

          {/* Cổng duyệt của Hiring Manager (ADR-061) — đứng TRƯỚC panel xếp lịch vì đó đúng
              là thứ tự thao tác: cổng mở rồi mới xếp được lịch. */}
          <ShortlistGatePanel
            applicationId={app.id}
            jobPostingId={app.jobPostingId}
            status={app.status}
            hmDecision={app.hmDecision}
            hmDecisionNote={app.hmDecisionNote}
            onChanged={refreshApp}
          />

          <CandidateOfferPanel applicationId={app.id} status={app.status} onChanged={refreshApp} />

          {/* Xếp lịch phỏng vấn (HR gán cứng 1 giờ cho ứng viên — ADR-048) */}
          <AssignSchedulePanel
            applicationId={app.id}
            jobPostingId={app.jobPostingId}
            round={app.currentRound || 1}
            hasScheduled={!!app.hasScheduledInterview}
            scheduledAt={app.interviewDate}
            confirmationStatus={app.scheduleConfirmationStatus}
            declineReason={app.scheduleDeclineReason}
            status={app.status}
            onAssigned={refreshApp}
          />

          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">{t('info')}</h2>
            <dl className="space-y-3 text-sm">
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('source')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.source === 'job_board' ? t('jobBoard') : t('invited')}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('practiceUsed')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.practiceSessionUsed ? t('used') : t('notUsed')}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('appliedDate')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {new Date(app.createdAt).toLocaleDateString()}
                </dd>
              </div>
            </dl>
          </div>

          <EmailHistoryPanel applicationId={app.id} />
        </div>
      </div>

      {showProfileModal && (
        <CandidateOnlineProfileModal
          app={app}
          onClose={() => setShowProfileModal(false)}
        />
      )}
    </div>
  )
}
