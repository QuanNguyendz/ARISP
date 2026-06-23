import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  ChevronRight,
  CalendarClock,
  Clock,
  CalendarPlus,
  Check,
  Sparkles,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Video,
  Download,
  FileText,
  Languages,
  ArrowRightCircle,
  MessageSquareText,
  User,
  Globe,
} from 'lucide-react'
import { applicationService } from '@services/application/applicationService'
import { resolveAssetUrl } from '@config/constants'
import { Skeleton } from '@components/ui/Skeleton'
import type {
  MyApplicationDetail,
  MyApplicationSession,
  MyEvalCriterion,
} from '../../types/application'

/** Nhãn tiếng Việt cho các tiêu chí chấm điểm phổ biến (key từ AI). */
const CRITERION_LABELS: Record<string, string> = {
  technical: 'Kiến thức kỹ thuật',
  technical_knowledge: 'Kiến thức kỹ thuật',
  communication: 'Giao tiếp',
  problem_solving: 'Giải quyết vấn đề',
  culture_fit: 'Văn hoá phù hợp',
  experience: 'Kinh nghiệm thực tế',
  practical_experience: 'Kinh nghiệm thực tế',
  language: 'Ngôn ngữ',
  attitude: 'Thái độ',
  teamwork: 'Làm việc nhóm',
}

function criterionLabel(name: string): string {
  const key = name.trim().toLowerCase().replace(/\s+/g, '_')
  if (CRITERION_LABELS[key]) return CRITERION_LABELS[key]
  // Humanize: "culture_fit" -> "Culture fit"
  const s = name.replace(/_/g, ' ').trim()
  return s.charAt(0).toUpperCase() + s.slice(1)
}

function roundTypeLabel(t?: string): string {
  const map: Record<string, string> = {
    screening: 'Screening',
    technical: 'Technical',
    online_test: 'Online Test',
    final: 'Final',
  }
  return map[(t || '').toLowerCase()] || t || ''
}

function scoreColor(score: number): string {
  if (score >= 80) return 'bg-emerald-500'
  if (score >= 60) return 'bg-amber-500'
  return 'bg-red-500'
}

function formatDate(iso?: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function formatDuration(seconds?: number | null): string {
  if (!seconds || seconds <= 0) return ''
  const m = Math.round(seconds / 60)
  return `${m} phút`
}

function langLevel(overall: number): string {
  // overall_score thang 0–10 → ước lượng CEFR thô để hiển thị.
  if (overall >= 9) return 'C1+'
  if (overall >= 8) return 'B2+'
  if (overall >= 6.5) return 'B2'
  if (overall >= 5) return 'B1'
  return 'A2'
}

/** Trạng thái hiển thị của một vòng trong danh sách bên trái. */
function roundBadge(s: MyApplicationSession) {
  if (s.sessionType === 'practice') {
    return {
      cls: 'bg-ai-50 text-ai-700',
      icon: Sparkles,
      label: 'Thử',
    }
  }
  const verdict = s.hrFinalVerdict || s.evaluation?.aiVerdict
  if (s.evaluation && verdict) {
    return verdict === 'pass'
      ? { cls: 'bg-emerald-50 text-emerald-700', icon: Check, label: 'Pass' }
      : { cls: 'bg-red-50 text-red-700', icon: XCircle, label: 'Not Pass' }
  }
  if (s.pendingHrReview) {
    return { cls: 'bg-amber-50 text-amber-700', icon: Clock, label: 'Chờ HR' }
  }
  if (s.status === 'in_progress' || s.status === 'active') {
    return { cls: 'bg-brand-50 text-brand-700', icon: Clock, label: 'Đang diễn ra' }
  }
  return { cls: 'bg-ink-100 text-ink-500', icon: Clock, label: 'Chưa có kết quả' }
}

function RoundButton({
  s,
  active,
  onClick,
}: {
  s: MyApplicationSession
  active: boolean
  onClick: () => void
}) {
  const badge = roundBadge(s)
  const BadgeIcon = badge.icon
  const score = s.evaluation?.overallScore
  return (
    <button
      onClick={onClick}
      className={`w-full rounded-2xl border bg-white p-4 text-left shadow-card transition ${
        active ? 'border-2 border-brand-300' : 'border border-ink-200 hover:border-brand-200'
      }`}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-semibold text-ink-900">
          Vòng {s.roundNumber} · {roundTypeLabel(s.roundType) || 'Phỏng vấn'}
        </span>
        <span
          className={`inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ${badge.cls}`}
        >
          <BadgeIcon className="h-3 w-3" /> {badge.label}
        </span>
      </div>
      <div className="mt-1 flex items-center justify-between text-xs text-ink-500">
        <span>{s.sessionType === 'practice' ? 'Phỏng vấn thử' : 'Phỏng vấn thật'}</span>
        <span className="font-semibold text-ink-700">
          {typeof score === 'number' ? `${Math.round(score)}/100` : '—'}
        </span>
      </div>
      {(s.endedAt || s.startedAt) && (
        <div className="mt-1 text-[11px] text-ink-400">
          {formatDate(s.endedAt || s.startedAt)}
          {formatDuration(s.durationSeconds) ? ` · ${formatDuration(s.durationSeconds)}` : ''}
        </div>
      )}
    </button>
  )
}

function CriterionBar({ c }: { c: MyEvalCriterion }) {
  const pct = Math.max(0, Math.min(100, Math.round(c.score)))
  return (
    <div>
      <div className="mb-1 flex items-center justify-between text-sm">
        <span className="font-medium text-ink-700">{criterionLabel(c.name)}</span>
        <span className="font-semibold text-ink-900">{pct}/100</span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-ink-100">
        <div className={`h-full rounded-full ${scoreColor(pct)}`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}

/** Báo cáo đánh giá của một vòng đã được HR chia sẻ. */
function ReportPanel({ s, jobTitle }: { s: MyApplicationSession; jobTitle: string }) {
  const [tab, setTab] = useState<'criteria' | 'questions'>('criteria')
  const ev = s.evaluation!
  const verdict = s.hrFinalVerdict || ev.aiVerdict
  const isPass = verdict === 'pass'
  const lang = ev.languageAssessment
  const score = typeof ev.overallScore === 'number' ? Math.round(ev.overallScore) : null

  return (
    <div className="space-y-6">
      {/* Report header */}
      <div className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
        <div
          className={`flex flex-wrap items-start justify-between gap-4 border-b border-ink-100 bg-gradient-to-r to-white p-6 ${
            isPass ? 'from-emerald-50' : 'from-red-50'
          }`}
        >
          <div>
            <div className="flex flex-wrap items-center gap-2 text-sm text-ink-500">
              <span className="inline-flex items-center gap-1 rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-semibold text-brand-700">
                Vòng {s.roundNumber} · {roundTypeLabel(s.roundType) || 'Phỏng vấn'}
              </span>
              {lang && (
                <span className="inline-flex items-center gap-1 rounded-full bg-ink-100 px-2.5 py-0.5 text-xs font-medium text-ink-600">
                  <Globe className="h-3 w-3" /> {lang.language?.toUpperCase() || 'NN'}
                </span>
              )}
            </div>
            <h1 className="mt-2 font-display text-xl font-extrabold">{jobTitle}</h1>
            <p className="text-sm text-ink-500">
              {s.sessionType === 'practice' ? 'Phỏng vấn thử' : 'Phỏng vấn thật'}
              {s.endedAt ? ` · ${formatDate(s.endedAt)}` : ''}
              {formatDuration(s.durationSeconds) ? ` · ${formatDuration(s.durationSeconds)}` : ''}
            </p>
          </div>
          <div className="text-center">
            <div
              className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-bold ring-1 ${
                isPass
                  ? 'bg-emerald-50 text-emerald-700 ring-emerald-200'
                  : 'bg-red-50 text-red-700 ring-red-200'
              }`}
            >
              {isPass ? <CheckCircle2 className="h-4 w-4" /> : <XCircle className="h-4 w-4" />}{' '}
              {isPass ? 'Pass' : 'Not Pass'}
            </div>
            {score !== null && (
              <div className="mt-2 font-display text-4xl font-extrabold text-ink-900">
                {score}
                <span className="text-lg text-ink-400">/100</span>
              </div>
            )}
          </div>
        </div>

        {/* Recording player (chỉ khi HR chia sẻ bản ghi) */}
        {s.recordingUrl && (
          <div className="p-6">
            <video
              src={resolveAssetUrl(s.recordingUrl)}
              controls
              className="aspect-video w-full rounded-xl bg-ink-900"
            />
            <div className="mt-3 flex flex-wrap gap-2">
              <a
                href={resolveAssetUrl(s.recordingUrl)}
                download
                className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50"
              >
                <Download className="h-4 w-4" /> Tải bản ghi
              </a>
            </div>
          </div>
        )}
      </div>

      {/* Overall reasoning */}
      {ev.reasoning && (
        <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
          <h2 className="mb-2 font-display text-lg font-bold">Nhận định tổng quan</h2>
          <p className="text-sm text-ink-600">{ev.reasoning}</p>
        </div>
      )}

      {/* Tabs */}
      {(ev.criterionScores.length > 0 || ev.questionAnalyses.length > 0) && (
        <div className="flex items-center gap-1 rounded-xl border border-ink-200 bg-white p-1 text-sm shadow-card">
          <button
            onClick={() => setTab('criteria')}
            className={`flex-1 rounded-lg px-3 py-2 ${
              tab === 'criteria'
                ? 'bg-brand-600 font-semibold text-white'
                : 'font-medium text-ink-600 hover:bg-ink-100'
            }`}
          >
            Theo tiêu chí
          </button>
          <button
            onClick={() => setTab('questions')}
            className={`flex-1 rounded-lg px-3 py-2 ${
              tab === 'questions'
                ? 'bg-brand-600 font-semibold text-white'
                : 'font-medium text-ink-600 hover:bg-ink-100'
            }`}
          >
            Theo câu hỏi
          </button>
        </div>
      )}

      {/* Per-criterion scores */}
      {tab === 'criteria' && ev.criterionScores.length > 0 && (
        <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
          <h2 className="mb-4 font-display text-lg font-bold">Điểm theo tiêu chí</h2>
          <div className="space-y-4">
            {ev.criterionScores.map((c, i) => (
              <CriterionBar key={i} c={c} />
            ))}
          </div>
        </div>
      )}

      {/* Per-question analysis */}
      {tab === 'questions' && (
        <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
          <h2 className="mb-4 font-display text-lg font-bold">Phân tích từng câu hỏi</h2>
          {ev.questionAnalyses.length === 0 ? (
            <p className="text-sm text-ink-500">Chưa có phân tích chi tiết cho từng câu hỏi.</p>
          ) : (
            <div className="space-y-3">
              {ev.questionAnalyses.map((q, i) => {
                const good = q.score >= 70
                const mid = q.score >= 50 && q.score < 70
                return (
                  <details
                    key={i}
                    className="group rounded-xl border border-ink-200 p-4"
                    open={i === 0}
                  >
                    <summary className="flex cursor-pointer items-center justify-between gap-3">
                      <span className="flex items-center gap-2 text-sm font-medium text-ink-800">
                        {good ? (
                          <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-500" />
                        ) : (
                          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" />
                        )}
                        {q.question}
                      </span>
                      <span
                        className={`shrink-0 text-xs font-semibold ${
                          good ? 'text-emerald-600' : mid ? 'text-amber-600' : 'text-red-600'
                        }`}
                      >
                        {good ? 'Tốt' : mid ? 'Khá' : 'Cần cải thiện'}
                      </span>
                    </summary>
                    {q.answer && (
                      <p className="mt-3 rounded-lg bg-ink-50 p-3 text-sm text-ink-600">
                        <span className="font-medium text-ink-700">Trả lời: </span>
                        {q.answer}
                      </p>
                    )}
                    {q.analysis && <p className="mt-2 text-sm text-ink-600">{q.analysis}</p>}
                    {q.feedback && (
                      <p className="mt-2 text-sm text-ink-500">
                        <span className="font-medium text-ink-600">Gợi ý: </span>
                        {q.feedback}
                      </p>
                    )}
                  </details>
                )
              })}
            </div>
          )}
        </div>
      )}

      {/* Language assessment */}
      {lang && (
        <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-6 shadow-card">
          <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
            <Languages className="h-4 w-4" /> Đánh giá ngôn ngữ ({lang.language?.toUpperCase() || 'NN'})
          </div>
          <div className="mt-4 grid gap-4 sm:grid-cols-3">
            <div className="rounded-xl bg-white/60 p-3 text-center ring-1 ring-ai-200">
              <div className="font-display text-2xl font-extrabold text-ai-700">
                {langLevel(lang.overallScore)}
              </div>
              <div className="text-xs text-ink-500">Trình độ tổng thể</div>
            </div>
            <div className="rounded-xl bg-white/60 p-3 text-center ring-1 ring-ai-200">
              <div className="font-display text-2xl font-extrabold text-ai-700">{lang.fluency}</div>
              <div className="text-xs text-ink-500">Độ trôi chảy /10</div>
            </div>
            <div className="rounded-xl bg-white/60 p-3 text-center ring-1 ring-ai-200">
              <div className="font-display text-2xl font-extrabold text-ai-700">{lang.grammar}</div>
              <div className="text-xs text-ink-500">Ngữ pháp /10</div>
            </div>
          </div>
        </div>
      )}

      {/* Next step + HR feedback */}
      {(ev.recommendedNextStep || s.hrFeedback) && (
        <div className="grid gap-4 sm:grid-cols-2">
          {ev.recommendedNextStep && (
            <div className="rounded-2xl border border-brand-200 bg-brand-50/60 p-5 shadow-card">
              <div className="flex items-center gap-2 text-sm font-semibold text-brand-700">
                <ArrowRightCircle className="h-4 w-4" /> Bước tiếp theo
              </div>
              <p className="mt-2 text-sm text-ink-600">{ev.recommendedNextStep}</p>
              <Link
                to="/candidate/applications"
                className="mt-3 block w-full rounded-xl bg-brand-600 px-3 py-2 text-center text-sm font-semibold text-white hover:bg-brand-700"
              >
                Đến đơn ứng tuyển
              </Link>
            </div>
          )}
          {s.hrFeedback && (
            <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
              <div className="flex items-center gap-2 text-sm font-semibold text-ink-700">
                <MessageSquareText className="h-4 w-4 text-brand-600" /> Nhận xét từ HR
              </div>
              <p className="mt-2 text-sm text-ink-600">{s.hrFeedback}</p>
              <div className="mt-3 flex items-center gap-2 text-xs text-ink-400">
                <User className="h-3.5 w-3.5" /> HR Leader
              </div>
            </div>
          )}
        </div>
      )}

      <p className="text-center text-xs text-ink-400">
        Báo cáo do AI tạo · HR đã xác nhận · Một số phần có thể được HR điều chỉnh hiển thị
      </p>
    </div>
  )
}

/** Trạng thái bên phải khi vòng được chọn chưa có báo cáo để hiển thị. */
function RoundPlaceholder({ s }: { s: MyApplicationSession }) {
  if (s.pendingHrReview) {
    return (
      <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-card">
        <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-amber-50 text-amber-600">
          <Clock className="h-6 w-6" />
        </div>
        <p className="mt-3 font-semibold text-ink-700">Kết quả đang chờ HR Leader xác nhận</p>
        <p className="mt-1 text-sm text-ink-500">
          AI đã hoàn tất chấm điểm vòng {s.roundNumber}. Bạn sẽ nhận thông báo ngay khi HR xác nhận
          và chia sẻ kết quả.
        </p>
      </div>
    )
  }
  return (
    <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-card">
      <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
        <Video className="h-6 w-6" />
      </div>
      <p className="mt-3 font-semibold text-ink-700">Chưa có báo cáo cho vòng này</p>
      <p className="mt-1 text-sm text-ink-500">
        Kết quả & bản ghi sẽ hiển thị tại đây sau khi vòng phỏng vấn hoàn tất và được HR chia sẻ.
      </p>
    </div>
  )
}

function DetailSkeleton() {
  return (
    <div className="mx-auto grid max-w-6xl gap-8 px-6 py-6 lg:grid-cols-[320px_1fr]">
      <div className="space-y-5">
        <Skeleton className="h-40 w-full rounded-2xl" />
        <Skeleton className="h-24 w-full rounded-2xl" />
        <Skeleton className="h-24 w-full rounded-2xl" />
      </div>
      <div className="space-y-6">
        <Skeleton className="h-64 w-full rounded-2xl" />
        <Skeleton className="h-12 w-full rounded-xl" />
        <Skeleton className="h-48 w-full rounded-2xl" />
      </div>
    </div>
  )
}

export default function ApplicationDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [detail, setDetail] = useState<MyApplicationDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    let active = true
    setLoading(true)
    setError('')
    applicationService
      .getMyApplicationDetail(id)
      .then((d) => {
        if (!active) return
        setDetail(d)
        // Mặc định chọn vòng mới nhất có báo cáo được chia sẻ, nếu không thì vòng mới nhất.
        const withEval = [...d.sessions].reverse().find((s) => s.evaluation)
        const fallback = d.sessions.length ? d.sessions[d.sessions.length - 1] : null
        setSelectedId((withEval || fallback)?.id ?? null)
      })
      .catch((err: any) =>
        active && setError(err?.response?.data?.message || err?.message || 'Không tải được hồ sơ.')
      )
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [id])

  const selected = useMemo(
    () => detail?.sessions.find((s) => s.id === selectedId) ?? null,
    [detail, selectedId]
  )

  const jobTitle = detail?.jobTitle || 'Vị trí tuyển dụng'

  return (
    <>
      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/jobs" className="hover:text-brand-600">
            Trang chủ
          </Link>
          <ChevronRight className="h-4 w-4" />
          <Link to="/candidate/applications" className="hover:text-brand-600">
            Hồ sơ ứng tuyển
          </Link>
          <ChevronRight className="h-4 w-4" />
          <span className="truncate font-medium text-ink-600">{loading ? '…' : jobTitle}</span>
        </div>
      </div>

      {loading ? (
        <DetailSkeleton />
      ) : error ? (
        <div className="mx-auto max-w-6xl px-6 py-6">
          <div className="flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <XCircle className="h-4 w-4" /> {error}
          </div>
        </div>
      ) : !detail ? null : (
        <main className="mx-auto grid max-w-6xl gap-8 px-6 py-6 lg:grid-cols-[320px_1fr]">
          {/* LEFT: schedule + rounds */}
          <div className="space-y-5">
            {/* Upcoming schedule */}
            {detail.upcomingInterview && (
              <div className="rounded-2xl border border-brand-200 bg-brand-50/60 p-5 shadow-card">
                <div className="flex items-center gap-2 text-sm font-semibold">
                  <CalendarClock className="h-4 w-4 text-brand-600" /> Lịch sắp tới
                </div>
                <div className="mt-3 flex items-start gap-3">
                  <div className="grid h-12 w-12 shrink-0 flex-col place-items-center rounded-xl bg-white text-brand-700 ring-1 ring-brand-200">
                    <span className="text-[10px] font-semibold leading-none">
                      Th{new Date(detail.upcomingInterview.startTime).getMonth() + 1}
                    </span>
                    <span className="font-display text-lg font-extrabold leading-none">
                      {new Date(detail.upcomingInterview.startTime).getDate()}
                    </span>
                  </div>
                  <div className="min-w-0">
                    <div className="text-sm font-semibold text-ink-800">
                      Vòng {detail.upcomingInterview.roundNumber} · Phỏng vấn
                    </div>
                    <div className="truncate text-xs text-ink-500">{jobTitle}</div>
                    <div className="mt-1 inline-flex items-center gap-1 text-xs text-ink-400">
                      <Clock className="h-3.5 w-3.5" />
                      {new Date(detail.upcomingInterview.startTime).toLocaleTimeString('vi-VN', {
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                      {detail.location ? ` · ${detail.location}` : ''}
                    </div>
                  </div>
                </div>
                <Link
                  to="/candidate/applications"
                  className="mt-3 flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-3 py-2 text-sm font-semibold text-white hover:bg-brand-700"
                >
                  <CalendarPlus className="h-4 w-4" /> Xem mã &amp; hướng dẫn
                </Link>
              </div>
            )}

            {/* Round list */}
            <div>
              <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-ink-400">
                Các vòng phỏng vấn
              </div>
              {detail.sessions.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-ink-300 bg-white p-6 text-center text-sm text-ink-500 shadow-card">
                  Chưa có vòng phỏng vấn nào cho hồ sơ này.
                </div>
              ) : (
                <div className="space-y-2">
                  {detail.sessions.map((s) => (
                    <RoundButton
                      key={s.id}
                      s={s}
                      active={s.id === selectedId}
                      onClick={() => setSelectedId(s.id)}
                    />
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* RIGHT: report */}
          <div>
            {!selected ? (
              <div className="rounded-2xl border border-ink-200 bg-white p-10 text-center shadow-card">
                <div className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-ink-100 text-ink-400">
                  <FileText className="h-6 w-6" />
                </div>
                <p className="mt-3 font-semibold text-ink-700">Chưa có vòng nào để hiển thị</p>
                <p className="mt-1 text-sm text-ink-500">
                  Khi bạn hoàn thành phỏng vấn, kết quả sẽ xuất hiện tại đây.
                </p>
              </div>
            ) : selected.evaluation ? (
              <ReportPanel s={selected} jobTitle={jobTitle} />
            ) : (
              <RoundPlaceholder s={selected} />
            )}
          </div>
        </main>
      )}
    </>
  )
}
