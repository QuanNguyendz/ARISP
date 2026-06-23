import { useState, useEffect } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import {
  MapPin,
  DollarSign,
  Clock,
  Loader2,
  Languages,
  Briefcase,
  Sparkles,
  Users,
  HeartPulse,
  Plane,
  GraduationCap,
  Home,
  Code2,
  Server,
  BrainCircuit,
  CheckCircle2,
  Bookmark,
  ChevronRight,
  FileText,
} from 'lucide-react'
import jobService from '@/services/job/jobService'
import { savedJobService } from '@/services/job/savedJobService'
import { profileService } from '@/services/profile/profileService'
import type { CvMatchResult } from '@/services/profile/profileService'
import { applicationService } from '@/services/application/applicationService'
import { resolveAssetUrl } from '@config/constants'
import type { JobPosting } from '@/types/job'
import { useAuthStore } from '@store/auth/authStore'
import CandidateHeader from '@components/layout/CandidateHeader'

function formatSalary(job: JobPosting): string {
  if (job.salaryIsNegotiable) return 'Thỏa thuận'
  if (job.salaryMin && job.salaryMax) {
    return `$${job.salaryMin.toLocaleString()} - $${job.salaryMax.toLocaleString()}`
  }
  if (job.salaryMin) return `Từ $${job.salaryMin.toLocaleString()}`
  if (job.salaryMax) return `Đến $${job.salaryMax.toLocaleString()}`
  return 'Thỏa thuận'
}

function formatWorkMode(mode?: string): string {
  if (!mode) return 'Full-time'
  const mappings: Record<string, string> = {
    fulltime: 'Full-time',
    parttime: 'Part-time',
    contract: 'Hợp đồng',
    internship: 'Thực tập',
  }
  return mappings[mode.toLowerCase()] || mode
}

function formatPostedDate(dateStr?: string): string {
  if (!dateStr) return 'Mới đăng'
  try {
    const diff = Math.abs(new Date().getTime() - new Date(dateStr).getTime())
    const days = Math.ceil(diff / (1000 * 60 * 60 * 24))
    if (days <= 1) return 'Hôm nay'
    if (days <= 2) return 'Hôm qua'
    return `${days} ngày trước`
  } catch {
    return 'Gần đây'
  }
}

function getJobIcon(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai') || dept.includes('ml')) {
    return <BrainCircuit className="w-8 h-8" />
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return <Server className="w-8 h-8" />
  }
  return <Code2 className="w-8 h-8" />
}

function getIconBg(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai')) {
    return 'bg-ai-50 text-ai-600'
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return 'bg-emerald-50 text-emerald-600'
  }
  return 'bg-brand-50 text-brand-600'
}

/**
 * Tách phần `summary` của Gemini (định dạng "🌟 Điểm sáng: …\n⚠️ Điểm cần lưu ý: …")
 * thành 2 khối riêng để hiển thị dễ nhìn. Phần không khớp marker được gom vào `rest`.
 */
function parseMatchSummary(summary?: string): {
  strengths?: string
  gaps?: string
  rest?: string
} {
  if (!summary) return {}
  const result: { strengths?: string; gaps?: string; rest: string[] } = { rest: [] }
  // Tách theo emoji marker dù có xuống dòng hay không.
  const parts = summary
    .replace(/⚠️/g, '\n⚠️')
    .replace(/🌟/g, '\n🌟')
    .split('\n')
    .map((s) => s.trim())
    .filter(Boolean)
  for (const p of parts) {
    if (p.startsWith('🌟')) {
      result.strengths = p.replace(/^🌟\s*(Điểm sáng\s*:?\s*)?/u, '').trim()
    } else if (p.startsWith('⚠️')) {
      result.gaps = p.replace(/^⚠️\s*(Điểm cần lưu ý\s*:?\s*)?/u, '').trim()
    } else {
      result.rest.push(p)
    }
  }
  return {
    strengths: result.strengths,
    gaps: result.gaps,
    rest: result.rest.length ? result.rest.join(' ') : undefined,
  }
}

export default function JobDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { isAuthenticated } = useAuthStore()
  const [job, setJob] = useState<JobPosting | null>(null)
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  // Phân tích độ phù hợp CV–JD theo CV trong hồ sơ ứng viên đang đăng nhập.
  const [match, setMatch] = useState<CvMatchResult | null>(null)
  const [matchLoading, setMatchLoading] = useState(false)
  const [matchAuthError, setMatchAuthError] = useState(false)

  // Lưu / bỏ lưu việc làm (bookmark)
  const [isSaved, setIsSaved] = useState(false)
  const [savePending, setSavePending] = useState(false)

  // Ứng tuyển — đã nộp hồ sơ cho tin này chưa (để đổi nút sang "Xem hồ sơ").
  const [appliedId, setAppliedId] = useState<string | null>(null)

  useEffect(() => {
    async function loadJobDetail() {
      if (!id) return
      try {
        setLoading(true)
        const data = await jobService.getJobPostingById(id)
        setJob(data)
      } catch (err) {
        console.error(err)
        setError('Không tìm thấy tin tuyển dụng này hoặc tin tuyển dụng đã ngừng nhận hồ sơ.')
      } finally {
        setLoading(false)
      }
    }

    loadJobDetail()
  }, [id])

  useEffect(() => {
    if (!id || !isAuthenticated) return
    let cancelled = false
    let attempts = 0
    const MAX_ATTEMPTS = 40 // ~100s ở mức 2.5s/lần — đủ cho Gemini (~25s)
    const POLL_MS = 2500
    setMatchLoading(true)
    setMatchAuthError(false)

    const poll = async () => {
      try {
        const data = await profileService.getCvMatch(id)
        if (cancelled) return
        setMatch(data)
        // Backend phân tích ở nền — poll tiếp tới khi xong (kết quả được cache).
        if (data.status === 'processing' && attempts < MAX_ATTEMPTS) {
          attempts += 1
          window.setTimeout(poll, POLL_MS)
          return
        }
        setMatchLoading(false)
      } catch (err: unknown) {
        if (cancelled) return
        const status = (err as { response?: { status?: number } })?.response?.status
        if (status === 401 || status === 403) {
          setMatchAuthError(true)
          setMatchLoading(false)
        } else if (attempts < MAX_ATTEMPTS) {
          // Lỗi mạng tạm thời → thử lại (kết quả vẫn đang được tính & cache ở backend).
          attempts += 1
          window.setTimeout(poll, POLL_MS)
        } else {
          console.error('CV match failed:', err)
          setMatchLoading(false)
        }
      }
    }

    poll()
    return () => {
      cancelled = true
    }
  }, [id, isAuthenticated])

  // Trạng thái đã lưu của tin này (chỉ với ứng viên đã đăng nhập).
  useEffect(() => {
    if (!id || !isAuthenticated) {
      setIsSaved(false)
      return
    }
    let cancelled = false
    savedJobService
      .getSavedJobIds()
      .then((ids) => {
        if (!cancelled) setIsSaved(ids.includes(id))
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
  }, [id, isAuthenticated])

  // Đã ứng tuyển tin này chưa? (để nút đổi sang "Xem hồ sơ ứng tuyển")
  useEffect(() => {
    if (!id || !isAuthenticated) {
      setAppliedId(null)
      return
    }
    let cancelled = false
    applicationService
      .getMyApplications()
      .then((apps) => {
        if (cancelled) return
        const found = apps.find((a) => a.jobPostingId === id && a.status !== 'withdrawn')
        if (found) setAppliedId(found.id)
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
  }, [id, isAuthenticated])

  // Mở màn ứng tuyển. Chưa đăng nhập → trang đăng nhập; đã ứng tuyển → xem hồ sơ.
  const handleApply = () => {
    if (!id) return
    if (!isAuthenticated) {
      navigate('/auth/candidate-login')
      return
    }
    if (appliedId) {
      navigate('/candidate/applications')
      return
    }
    navigate(`/jobs/${id}/apply`)
  }

  // Lưu / bỏ lưu — cập nhật lạc quan, rollback nếu lỗi. Chưa đăng nhập → tới trang đăng nhập.
  const handleToggleSave = async () => {
    if (!id) return
    if (!isAuthenticated) {
      navigate('/auth/candidate-login')
      return
    }
    const wasSaved = isSaved
    setIsSaved(!wasSaved)
    setSavePending(true)
    try {
      if (wasSaved) await savedJobService.unsaveJob(id)
      else await savedJobService.saveJob(id)
    } catch {
      setIsSaved(wasSaved)
    } finally {
      setSavePending(false)
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-ink-50">
        <CandidateHeader />
        <div className="flex min-h-[60vh] flex-col items-center justify-center gap-3">
          <Loader2 className="w-10 h-10 text-brand-600 animate-spin" />
          <p className="text-sm text-ink-500">Đang tải thông tin...</p>
        </div>
      </div>
    )
  }

  if (error || !job) {
    return (
      <div className="min-h-screen bg-ink-50">
        <CandidateHeader />
        <div className="py-20 text-center">
          <h2 className="mb-2 text-2xl font-display font-bold text-ink-900">Đã xảy ra lỗi</h2>
          <p className="mb-6 text-ink-500">{error || 'Không tìm thấy dữ liệu.'}</p>
          <button
            type="button"
            onClick={() => navigate('/jobs')}
            className="inline-flex items-center gap-2 rounded-xl border border-ink-200 bg-white px-6 py-3 font-medium text-ink-700 hover:bg-ink-50"
          >
            <ChevronRight className="w-4 h-4 rotate-180" />
            Xem việc làm khác
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-ink-50 text-ink-900 antialiased">
      <CandidateHeader />

      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/jobs" className="hover:text-brand-600">
            Việc làm
          </Link>
          <ChevronRight className="w-4 h-4" />
          <span className="text-ink-600 font-medium">{job.title}</span>
        </div>
      </div>

      <main className="mx-auto max-w-6xl px-6 py-6 grid gap-8 lg:grid-cols-[1fr_360px]">
        {/* Left: content */}
        <div className="space-y-6">
          {/* Header card */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
            <div className="flex items-start gap-4">
              <div
                className={`grid h-16 w-16 shrink-0 place-items-center rounded-2xl ${getIconBg(job.department)}`}
              >
                {getJobIcon(job.department)}
              </div>
              <div className="min-w-0 flex-1">
                <h1 className="font-display text-2xl font-extrabold leading-snug">{job.title}</h1>
                <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-500">
                  <span className="inline-flex items-center gap-1.5">
                    <Users className="w-4 h-4" /> {job.department || 'Phòng ban'}
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <MapPin className="w-4 h-4" /> {job.location || 'Remote'}
                  </span>
                  <span className="inline-flex items-center gap-1.5">
                    <Clock className="w-4 h-4" /> Đăng {formatPostedDate(job.createdAt)}
                  </span>
                </div>
                <div className="mt-3 flex flex-wrap gap-2 text-xs">
                  <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2.5 py-1 text-ink-600">
                    <Briefcase className="w-3.5 h-3.5" />{' '}
                    {formatWorkMode(job.workMode || job.employmentType)}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-lg bg-emerald-50 px-2.5 py-1 text-emerald-700">
                    <DollarSign className="w-3.5 h-3.5" /> {formatSalary(job)}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2.5 py-1 text-ink-600 capitalize">
                    <Sparkles className="w-3.5 h-3.5" /> {job.experienceLevel || 'Không yêu cầu'}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded-lg bg-brand-50 px-2.5 py-1 text-brand-700">
                    <Languages className="w-3.5 h-3.5" /> {job.languageRequirement || 'Tiếng Việt'}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* JD sections */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card space-y-6">
            <section>
              <h2 className="font-display text-lg font-bold mb-3">Mô tả công việc</h2>
              <p className="text-ink-600 leading-relaxed whitespace-pre-line">
                {job.jobDescription}
              </p>
            </section>

            {job.skills && job.skills.length > 0 && (
              <section>
                <h2 className="font-display text-lg font-bold mb-3">Yêu cầu</h2>
                <ul className="space-y-2 text-ink-600">
                  {job.skills.map((skill, i) => (
                    <li key={i} className="flex gap-2.5">
                      <CheckCircle2 className="w-5 h-5 text-emerald-500 shrink-0" /> {skill}
                    </li>
                  ))}
                </ul>
              </section>
            )}

            <section>
              <h2 className="font-display text-lg font-bold mb-3">Quyền lợi</h2>
              <div className="grid sm:grid-cols-2 gap-3 text-ink-600">
                <div className="flex gap-2.5 rounded-xl bg-ink-50 p-3">
                  <HeartPulse className="w-5 h-5 text-brand-600 shrink-0" /> Bảo hiểm sức khoẻ cao
                  cấp
                </div>
                <div className="flex gap-2.5 rounded-xl bg-ink-50 p-3">
                  <Plane className="w-5 h-5 text-brand-600 shrink-0" /> Du lịch & team building
                </div>
                <div className="flex gap-2.5 rounded-xl bg-ink-50 p-3">
                  <GraduationCap className="w-5 h-5 text-brand-600 shrink-0" /> Ngân sách học tập
                </div>
                <div className="flex gap-2.5 rounded-xl bg-ink-50 p-3">
                  <Home className="w-5 h-5 text-brand-600 shrink-0" /> Hybrid working
                </div>
              </div>
            </section>
          </div>
        </div>

        {/* Right: sticky apply + match */}
        <aside className="space-y-5 lg:sticky lg:top-24 self-start">
          {/* Apply card */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
            {appliedId ? (
              <button
                onClick={() => navigate('/candidate/applications')}
                className="flex w-full items-center justify-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-bold text-emerald-700 hover:bg-emerald-100"
              >
                <CheckCircle2 className="h-4 w-4" /> Đã ứng tuyển · Xem hồ sơ
              </button>
            ) : (
              <button
                onClick={handleApply}
                className="w-full rounded-xl bg-brand-600 px-4 py-3 text-sm font-bold text-white hover:bg-brand-700"
              >
                Ứng tuyển ngay
              </button>
            )}
            <button
              onClick={handleToggleSave}
              disabled={savePending}
              className={`mt-2 w-full rounded-xl border px-4 py-3 text-sm font-semibold flex items-center justify-center gap-2 disabled:opacity-60 ${
                isSaved
                  ? 'border-ai-200 bg-ai-50 text-ai-700 hover:bg-ai-100'
                  : 'border-ink-200 text-ink-700 hover:bg-ink-50'
              }`}
            >
              <Bookmark className={`w-4 h-4 ${isSaved ? 'fill-current' : ''}`} />
              {isSaved ? 'Đã lưu' : 'Lưu việc làm'}
            </button>
          </div>

          {/* CV-JD Match (signature AI) — dùng CV thật trong hồ sơ ứng viên */}
          <div className="rounded-2xl border border-ai-200 bg-gradient-to-b from-ai-50/70 to-white p-6 shadow-card">
            <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
              <Sparkles className="w-4 h-4" /> Độ phù hợp CV–JD
            </div>

            {!isAuthenticated || matchAuthError ? (
              <div className="mt-3">
                <p className="text-sm text-ink-500">
                  Đăng nhập bằng tài khoản ứng viên và tải CV lên hồ sơ để xem độ phù hợp với tin
                  này.
                </p>
                <Link
                  to="/auth/candidate-login"
                  className="mt-3 block w-full rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-center text-sm font-semibold text-white hover:opacity-90"
                >
                  Đăng nhập
                </Link>
              </div>
            ) : matchLoading ? (
              <div className="mt-4 flex items-start gap-2 text-sm text-ink-500">
                <Loader2 className="w-4 h-4 mt-0.5 shrink-0 animate-spin text-ai-600" />
                <span>Đang phân tích CV của bạn… việc này có thể mất ~20–30 giây.</span>
              </div>
            ) : match && !match.hasCv ? (
              <div className="mt-3">
                <p className="text-sm text-ink-500">
                  Hồ sơ của bạn chưa có CV. Tải CV lên để AI phân tích độ phù hợp với tin này.
                </p>
                <Link
                  to="/candidate/profile?focus=cv"
                  className="mt-3 flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-sm font-semibold text-white hover:opacity-90"
                >
                  <FileText className="w-4 h-4" /> Tải CV lên
                </Link>
              </div>
            ) : match && match.hasCv ? (
              <div>
                {/* CV đã có sẵn — hiện rõ tên file, bấm để xem */}
                <a
                  href={resolveAssetUrl(match.cvUrl)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-3 flex items-center gap-2 rounded-xl border border-ai-200 bg-white px-3 py-2 text-sm text-ink-700 hover:border-ai-300"
                >
                  <FileText className="w-4 h-4 shrink-0 text-ai-600" />
                  <span className="flex-1 truncate">{match.cvFileName}</span>
                  <span className="shrink-0 text-xs font-medium text-ai-700">Xem</span>
                </a>

                {match.analysis ? (
                  <>
                    <div className="mt-4 flex items-end gap-2">
                      <div className="font-display text-5xl font-extrabold text-ai-700 leading-none">
                        {match.analysis.matchScore}
                      </div>
                      <div className="pb-1 text-sm text-ink-500">/ 100</div>
                    </div>
                    <div className="mt-3 h-2.5 w-full overflow-hidden rounded-full bg-ai-100">
                      <div
                        className="h-full rounded-full bg-gradient-to-r from-brand-600 to-ai-600"
                        style={{ width: `${match.analysis.matchScore}%` }}
                      />
                    </div>
                    <div className="mt-2 flex items-center gap-1 text-xs text-ink-400">
                      <Sparkles className="h-3 w-3" /> Phân tích bởi AI (
                      {match.analysis.reviewedBy ?? 'Gemini'})
                    </div>
                    {/* Kỹ năng khớp / còn thiếu — dạng chip cho dễ quét */}
                    {match.analysis.skillsMatched.length > 0 && (
                      <div className="mt-4">
                        <div className="mb-1.5 text-xs font-semibold text-ink-500">
                          Kỹ năng khớp
                        </div>
                        <div className="flex flex-wrap gap-1.5">
                          {match.analysis.skillsMatched.map((s, i) => (
                            <span
                              key={i}
                              className="inline-flex items-center gap-1 rounded-lg bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700"
                            >
                              <CheckCircle2 className="h-3.5 w-3.5 shrink-0" />
                              {s}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}
                    {match.analysis.skillsGaps.length > 0 && (
                      <div className="mt-3">
                        <div className="mb-1.5 text-xs font-semibold text-ink-500">Còn thiếu</div>
                        <div className="flex flex-wrap gap-1.5">
                          {match.analysis.skillsGaps.map((s, i) => (
                            <span
                              key={i}
                              className="inline-flex items-center gap-1 rounded-lg bg-amber-50 px-2 py-1 text-xs font-medium text-amber-700"
                            >
                              <span className="shrink-0">⚠️</span>
                              {s}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* Tóm tắt: tách Điểm sáng / Điểm cần lưu ý thành 2 khối */}
                    {(() => {
                      const sum = parseMatchSummary(match.analysis.summary)
                      return (
                        <div className="mt-4 space-y-2.5">
                          {sum.strengths && (
                            <div className="rounded-xl bg-emerald-50 p-3">
                              <div className="flex items-center gap-1.5 text-xs font-semibold text-emerald-700">
                                <CheckCircle2 className="h-4 w-4" /> Điểm sáng
                              </div>
                              <p className="mt-1 text-sm text-ink-600">{sum.strengths}</p>
                            </div>
                          )}
                          {sum.gaps && (
                            <div className="rounded-xl bg-amber-50 p-3">
                              <div className="flex items-center gap-1.5 text-xs font-semibold text-amber-700">
                                <span>⚠️</span> Điểm cần lưu ý
                              </div>
                              <p className="mt-1 text-sm text-ink-600">{sum.gaps}</p>
                            </div>
                          )}
                          {sum.rest && <p className="text-sm text-ink-500">{sum.rest}</p>}
                        </div>
                      )
                    })()}

                    <p className="mt-3 text-xs text-ink-400">
                      Phân tích bởi Gemini 2.5 Flash · chỉ mang tính tham khảo
                    </p>
                    <Link
                      to="/candidate/profile?focus=cv"
                      className="mt-3 block w-full rounded-xl border border-ai-300 bg-white px-3 py-2 text-center text-sm font-semibold text-ai-700 hover:bg-ai-50"
                    >
                      Cập nhật CV khác
                    </Link>
                  </>
                ) : (
                  <p className="mt-4 text-sm text-ink-500">
                    {match.message || 'Chưa thể phân tích CV lúc này. Vui lòng thử lại sau.'}
                  </p>
                )}
              </div>
            ) : (
              <p className="mt-4 text-sm text-ink-500">
                Không tải được phân tích CV. Vui lòng thử lại sau.
              </p>
            )}
          </div>

          {/* Company */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
            <div className="text-sm font-semibold mb-3">Về phòng ban</div>
            <div className="flex items-center gap-3">
              <div className="grid h-11 w-11 place-items-center rounded-xl bg-brand-50 text-brand-600">
                <Users className="w-5 h-5" />
              </div>
              <div>
                <div className="font-semibold text-sm">{job.department || 'Engineering'}</div>
                <div className="text-xs text-ink-400">Đội ngũ ~40 kỹ sư · Hybrid</div>
              </div>
            </div>
          </div>
        </aside>
      </main>
    </div>
  )
}
