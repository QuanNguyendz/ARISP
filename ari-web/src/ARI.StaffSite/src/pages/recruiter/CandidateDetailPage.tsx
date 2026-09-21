import { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, FileText, ExternalLink, Mail, Phone, Briefcase } from 'lucide-react'
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
import {
  appStatusBadge,
  appStatusLabel,
  initials,
  scoreColor,
  timeAgo,
} from './_jobUi'
import { JobDetailSkeleton } from './_skeletons'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import { formatScore } from '@ari/shared/utils/format'
import ShortlistGatePanel from '@/components/hiring/ShortlistGatePanel'
import EmailHistoryPanel from '@/components/hiring/EmailHistoryPanel'
import CandidateOfferPanel from '@/components/offers/CandidateOfferPanel'

function apiErr(e: unknown, fallback: string): string {
  return (e as { response?: { data?: { message?: string } } })?.response?.data?.message || fallback
}

export default function RecruiterCandidateDetailPage() {
  const { t } = useTranslation('modules/recruiter/candidateDetail')
  const { id } = useParams<{ id: string }>()
  const { openDocument } = useDocumentViewer()
  const [app, setApp] = useState<HrApplicationItem | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

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
          to="/recruiter/candidates"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← {t('back')}
        </Link>
      </div>
    )
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <Link
        to="/recruiter/candidates"
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('backToList')}
      </Link>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {/* Header */}
      <div className="mb-6 flex flex-col gap-4 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div className="flex items-center gap-3 sm:gap-4">
          <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 text-base font-bold text-white sm:h-16 sm:w-16 sm:text-lg">
            {initials(app.candidateName || app.candidateEmail)}
          </span>
          <div className="min-w-0 flex-1">
            <h1 className="truncate text-lg font-bold text-ink-900 dark:text-white sm:text-xl">
              {app.candidateName || t('candidate')}
            </h1>
            <p className="mt-0.5 flex flex-col gap-1 text-sm text-ink-500 dark:text-ink-400 sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-3 sm:gap-y-1">
              <span className="flex min-w-0 items-center gap-1">
                <Mail className="h-3.5 w-3.5 shrink-0" />
                <span className="truncate">{app.candidateEmail}</span>
              </span>
              {app.candidatePhone ? (
                <span className="flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5" />
                  {app.candidatePhone}
                </span>
              ) : null}
              <span className="flex min-w-0 items-center gap-1">
                <Briefcase className="h-3.5 w-3.5 shrink-0" />
                <span className="truncate">{app.jobTitle || t('position')}</span>
              </span>
            </p>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <span
                className={`whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium ${appStatusBadge(app.status)}`}
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
          <div className="flex items-center justify-between gap-2 self-stretch rounded-xl bg-ink-50 px-3 py-2 dark:bg-white/[0.03] sm:flex-col sm:items-center sm:justify-center sm:bg-transparent sm:px-0 sm:py-0 sm:dark:bg-transparent">
            <span className="text-xs text-ink-400 sm:hidden">{t('matchCVJD')}</span>
            {app.matchScore != null ? (
              <div className={`text-2xl font-bold sm:text-3xl ${scoreColor(app.matchScore)}`}>
                {formatScore(app.matchScore)}
              </div>
            ) : (
              <CvScoreBadge status={app.cvScoreStatus} retryAt={app.cvScoreRetryAt} />
            )}
            <div className="hidden text-xs text-ink-400 sm:block">{t('matchCVJD')}</div>
          </div>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Left */}
        <div className="space-y-6 lg:col-span-2">
          {/* Điểm CV kèm cách tính (ADR-070) — Recruiter sàng hồ sơ bằng con số này, nên phải thấy từng
              tiêu chí, trọng số và bằng chứng trong CV. */}
          <CvScoreBreakdown score={app.cvScore} />

          {/* Kết quả phỏng vấn theo vòng (ADR-069): ca đã gán · diễn biến · báo cáo AI · video · transcript,
              kèm nút tới đúng báo cáo. Thay cho hai khối cũ (danh sách đánh giá + danh sách phiên) vốn không
              có video/transcript, không nói được "AI đang chấm", và một khối không bấm vào đâu được. */}
          <InterviewResultsCard
            applicationId={app.id}
            evaluationHref={(evaluationId) => `/recruiter/evaluations?id=${evaluationId}`}
          />
        </div>

        {/* Right */}
        <div className="space-y-6">
          {/* Actions */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card sm:p-5">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">
              {t('actions')}
            </h2>
            <div className="space-y-2">
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
                  <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('viewCV')}
                  <ExternalLink className="ml-auto h-3.5 w-3.5 text-ink-400" />
                </button>
              )}
              {/* Cùng một thẻ với danh sách ứng viên của tin: server chọn vòng theo lịch đang giữ chỗ
                  (không đoán từ phiên đã xong — vòng trắc nghiệm không có phiên nào), nói rõ lý do khi
                  chưa cấp được, và có link vào phòng cho vòng làm từ nhà. */}
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

          {/* Info */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card sm:p-5">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">{t('info')}</h2>
            <dl className="space-y-3 text-sm">
              <div className="flex flex-col gap-1 sm:flex-row sm:justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('source')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.source === 'job_board' ? t('jobBoard') : t('invited')}
                </dd>
              </div>
              <div className="flex flex-col gap-1 sm:flex-row sm:justify-between">
                <dt className="text-ink-500 dark:text-ink-400">{t('practiceUsed')}</dt>
                <dd className="font-medium text-ink-900 dark:text-white">
                  {app.practiceSessionUsed ? t('used') : t('notUsed')}
                </dd>
              </div>
              <div className="flex flex-col gap-1 sm:flex-row sm:justify-between">
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
    </div>
  )
}
