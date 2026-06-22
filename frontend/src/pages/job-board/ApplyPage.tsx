import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  ArrowLeft,
  FileText,
  UploadCloud,
  Loader2,
  CheckCircle2,
  Mail,
  Phone,
  User,
  MapPin,
  AlertCircle,
  Sparkles,
  X,
  Briefcase,
} from 'lucide-react'
import { useAuthStore } from '@store/auth'
import { jobService } from '@/services/job/jobService'
import { profileService } from '@/services/profile/profileService'
import type { CandidateProfile } from '@/services/profile/profileService'
import { provinceService } from '@/services/location/provinceService'
import type { Province } from '@/services/location/provinceService'
import { applicationService } from '@/services/application/applicationService'
import SearchableSelect from '@/components/ui/SearchableSelect'
import { resolveAssetUrl } from '@config/constants'
import type { JobPosting } from '@/types/job'

/** Nhãn bắt buộc — dấu * đỏ. */
function Req() {
  return <span className="text-red-500"> *</span>
}

const MAX_CV_MB = 10
const ACCEPTED = ['.pdf', '.docx']

export default function ApplyPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { isAuthenticated } = useAuthStore()

  const [job, setJob] = useState<JobPosting | null>(null)
  const [profile, setProfile] = useState<CandidateProfile | null>(null)
  const [provinces, setProvinces] = useState<Province[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  // Form state
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  // Nơi làm việc mong muốn = tỉnh/thành (combo box). Lưu cả code + tên hiển thị.
  const [locationCode, setLocationCode] = useState<number | null>(null)
  const [desiredLocation, setDesiredLocation] = useState('')
  const [coverLetter, setCoverLetter] = useState('')
  const [noticePeriod, setNoticePeriod] = useState('')
  // CV: 'profile' = dùng CV hồ sơ; 'upload' = nộp CV khác cho tin này.
  const [cvSource, setCvSource] = useState<'profile' | 'upload'>('profile')
  const [cvFile, setCvFile] = useState<File | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState('')
  // Validate theo từng trường: chỉ hiện lỗi khi trường đã blur (rời focus) hoặc sau khi bấm gửi.
  // Đang gõ thì xoá trạng thái touched của trường đó → không nhắc lỗi liên tục mỗi ký tự.
  const [touchedFields, setTouchedFields] = useState<Set<string>>(new Set())
  const [showCancel, setShowCancel] = useState(false)

  const markTouched = (key: string) =>
    setTouchedFields((prev) => (prev.has(key) ? prev : new Set(prev).add(key)))
  const clearTouched = (key: string) =>
    setTouchedFields((prev) => {
      if (!prev.has(key)) return prev
      const next = new Set(prev)
      next.delete(key)
      return next
    })

  // Chưa đăng nhập → về trang đăng nhập ứng viên.
  useEffect(() => {
    if (!isAuthenticated) navigate('/auth/candidate-login')
  }, [isAuthenticated, navigate])

  useEffect(() => {
    if (!id) return
    let active = true
    setLoading(true)
    Promise.all([
      jobService.getJobPostingById(id),
      profileService.getProfile().catch(() => null),
      provinceService.getProvinces().catch(() => [] as Province[]),
    ])
      .then(([j, p, provs]) => {
        if (!active) return
        setJob(j)
        setProfile(p)
        setProvinces(provs)
        if (p) {
          setFullName(p.fullName || '')
          setPhone(p.phone || '')
          // Nơi làm việc mong muốn = tỉnh/thành trong hồ sơ. Ưu tiên code+name; nếu hồ sơ cũ
          // chỉ còn chuỗi "Phường …, Tỉnh …" thì lấy phần tỉnh/thành (sau dấu phẩy cuối).
          const cityName =
            p.provinceName?.trim() || (p.location ? p.location.split(',').pop()!.trim() : '')
          const matched =
            (p.provinceCode != null && provs.find((x) => x.code === p.provinceCode)) ||
            provs.find((x) => x.name === cityName)
          if (matched) {
            setLocationCode(matched.code)
            setDesiredLocation(matched.name)
          } else if (cityName) {
            setDesiredLocation(cityName)
          }
        }
        // Không có CV hồ sơ → mặc định bắt buộc tải lên.
        if (!p?.profileCvUrl) setCvSource('upload')
      })
      .catch(() => active && setLoadError('Không tải được thông tin tin tuyển dụng.'))
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [id])

  const hasProfileCv = !!profile?.profileCvUrl

  const onPickFile = (f: File | null) => {
    setSubmitError('')
    if (!f) {
      setCvFile(null)
      return
    }
    const ext = '.' + (f.name.split('.').pop() || '').toLowerCase()
    if (!ACCEPTED.includes(ext)) {
      setSubmitError('Chỉ chấp nhận CV định dạng PDF hoặc DOCX.')
      return
    }
    if (f.size > MAX_CV_MB * 1024 * 1024) {
      setSubmitError(`Kích thước CV tối đa ${MAX_CV_MB}MB.`)
      return
    }
    setCvFile(f)
  }

  // Lỗi từng trường (chỉ hiện sau khi bấm gửi hoặc blur).
  const errors = useMemo(() => {
    const e: Record<string, string> = {}
    if (!fullName.trim()) e.fullName = 'Vui lòng nhập họ và tên.'
    if (!phone.trim()) e.phone = 'Vui lòng nhập số điện thoại.'
    else if (phone.length < 8 || phone.length > 15)
      e.phone = 'Số điện thoại không hợp lệ (chỉ chứa 8–15 chữ số).'
    if (!desiredLocation.trim()) e.desiredLocation = 'Vui lòng chọn nơi làm việc mong muốn.'
    if (!coverLetter.trim()) e.coverLetter = 'Vui lòng trả lời câu hỏi này.'
    if (!noticePeriod.trim()) e.noticePeriod = 'Vui lòng trả lời câu hỏi này.'
    if (cvSource === 'upload' && !cvFile) e.cv = 'Vui lòng đính kèm file CV (PDF/DOCX).'
    if (cvSource === 'profile' && !hasProfileCv)
      e.cv = 'Hồ sơ chưa có CV — hãy tải CV lên cho tin này.'
    return e
  }, [fullName, phone, desiredLocation, coverLetter, noticePeriod, cvSource, cvFile, hasProfileCv])

  const handleSubmit = async () => {
    // Đánh dấu tất cả trường là "đã chạm" để hiện lỗi (nếu có) khi bấm gửi.
    setTouchedFields(
      new Set(['fullName', 'phone', 'desiredLocation', 'coverLetter', 'noticePeriod', 'cv'])
    )
    setSubmitError('')
    if (Object.keys(errors).length > 0) return
    if (!id) return
    setSubmitting(true)
    try {
      await applicationService.applyToJob(id, {
        candidateName: fullName.trim(),
        candidatePhone: phone.trim(),
        desiredLocation: desiredLocation.trim(),
        coverLetter: coverLetter.trim(),
        noticePeriod: noticePeriod.trim(),
        cvFile: cvSource === 'upload' ? cvFile : null,
      })
      navigate('/candidate/applications')
    } catch (err: unknown) {
      const e = err as { response?: { status?: number; data?: { message?: string } } }
      if (e?.response?.status === 409) {
        navigate('/candidate/applications')
        return
      }
      if (e?.response?.status === 401 || e?.response?.status === 403) {
        navigate('/auth/candidate-login')
        return
      }
      setSubmitError(e?.response?.data?.message || 'Gửi hồ sơ thất bại. Vui lòng thử lại.')
    } finally {
      setSubmitting(false)
    }
  }

  const goBack = () => navigate(`/jobs/${id}`)

  // Giá trị nơi làm việc mong muốn lúc prefill (tỉnh/thành) — để so sánh "đã chỉnh sửa".
  const initialCity =
    profile?.provinceName?.trim() ||
    (profile?.location ? profile.location.split(',').pop()!.trim() : '')

  // Có dữ liệu đã nhập → hỏi xác nhận trước khi rời đi.
  const dirty =
    !!coverLetter.trim() ||
    !!noticePeriod.trim() ||
    !!cvFile ||
    desiredLocation !== initialCity ||
    locationCode !== (profile?.provinceCode ?? null) ||
    fullName !== (profile?.fullName || '') ||
    phone !== (profile?.phone || '')

  const requestCancel = () => {
    if (dirty) setShowCancel(true)
    else goBack()
  }

  const inputCls = (hasErr: boolean) =>
    `w-full rounded-xl border px-3 py-2.5 text-sm outline-none transition focus:ring-2 ${
      hasErr
        ? 'border-red-300 focus:border-red-400 focus:ring-red-100'
        : 'border-ink-200 focus:border-brand-500 focus:ring-brand-100'
    }`

  if (loading) {
    return (
      <div className="grid min-h-screen place-items-center bg-ink-50">
        <Loader2 className="h-10 w-10 animate-spin text-brand-600" />
      </div>
    )
  }

  if (loadError || !job) {
    return (
      <div className="grid min-h-screen place-items-center bg-ink-50 px-6 text-center">
        <div>
          <p className="text-ink-600">{loadError || 'Không tìm thấy tin tuyển dụng.'}</p>
          <Link
            to="/jobs"
            className="mt-4 inline-block rounded-xl bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
          >
            Về danh sách việc làm
          </Link>
        </div>
      </div>
    )
  }

  const showErr = (key: string) => touchedFields.has(key) && errors[key]

  return (
    <div className="min-h-screen bg-ink-50 text-ink-900 antialiased">
      {/* Top bar */}
      <header className="sticky top-0 z-30 border-b border-ink-200 bg-white/80 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-3xl items-center gap-3 px-4 sm:px-6">
          <button
            onClick={requestCancel}
            className="inline-flex items-center gap-1.5 rounded-lg px-2 py-1.5 text-sm font-medium text-ink-600 hover:bg-ink-100"
          >
            <ArrowLeft className="h-4 w-4" /> Quay lại
          </button>
          <span className="ml-auto font-display text-sm font-bold text-ink-700">Ứng tuyển</span>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
        {/* Job summary */}
        <div className="mb-6 rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
          <div className="text-xs font-semibold uppercase tracking-wide text-ink-400">
            Bạn đang ứng tuyển vị trí
          </div>
          <h1 className="mt-1 font-display text-xl font-extrabold">{job.title}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-500">
            <span className="inline-flex items-center gap-1.5">
              <Briefcase className="h-4 w-4" /> {job.department || 'Phòng ban'}
            </span>
            <span className="inline-flex items-center gap-1.5">
              <MapPin className="h-4 w-4" /> {job.location || 'Remote'}
            </span>
          </div>
        </div>

        <div className="space-y-6">
          {/* CV */}
          <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <h2 className="font-display text-base font-bold">
              CV ứng tuyển
              <Req />
            </h2>
            <p className="mt-0.5 text-sm text-ink-500">
              Mặc định dùng CV trong hồ sơ. Bạn có thể nộp CV khác cho riêng tin này.
            </p>

            <div className="mt-4 space-y-2">
              {/* Dùng CV hồ sơ */}
              <label
                className={`flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition ${
                  cvSource === 'profile'
                    ? 'border-brand-300 bg-brand-50/60'
                    : 'border-ink-200 hover:border-brand-200'
                } ${!hasProfileCv ? 'cursor-not-allowed opacity-60' : ''}`}
              >
                <input
                  type="radio"
                  name="cvSource"
                  className="text-brand-600"
                  checked={cvSource === 'profile'}
                  disabled={!hasProfileCv}
                  onChange={() => setCvSource('profile')}
                />
                <FileText className="h-5 w-5 shrink-0 text-brand-600" />
                <span className="min-w-0 flex-1">
                  {hasProfileCv ? (
                    <>
                      <span className="block truncate text-sm font-medium text-ink-800">
                        {profile?.cvFileName || 'CV hồ sơ'}
                      </span>
                      <span className="text-xs text-ink-400">Dùng CV trong hồ sơ</span>
                    </>
                  ) : (
                    <span className="text-sm text-ink-500">Hồ sơ chưa có CV</span>
                  )}
                </span>
                {hasProfileCv && profile?.profileCvUrl && (
                  <a
                    href={resolveAssetUrl(profile.profileCvUrl)}
                    target="_blank"
                    rel="noopener noreferrer"
                    onClick={(e) => e.stopPropagation()}
                    className="shrink-0 text-xs font-medium text-brand-600 hover:underline"
                  >
                    Xem
                  </a>
                )}
              </label>

              {/* Tải CV khác */}
              <label
                className={`flex cursor-pointer items-center gap-3 rounded-xl border p-3 transition ${
                  cvSource === 'upload'
                    ? 'border-brand-300 bg-brand-50/60'
                    : 'border-ink-200 hover:border-brand-200'
                }`}
              >
                <input
                  type="radio"
                  name="cvSource"
                  className="text-brand-600"
                  checked={cvSource === 'upload'}
                  onChange={() => setCvSource('upload')}
                />
                <UploadCloud className="h-5 w-5 shrink-0 text-ai-600" />
                <span className="min-w-0 flex-1">
                  <span className="block text-sm font-medium text-ink-800">
                    Tải CV khác cho tin này
                  </span>
                  <span className="text-xs text-ink-400">PDF hoặc DOCX, tối đa {MAX_CV_MB}MB</span>
                </span>
              </label>

              {cvSource === 'upload' && (
                <div className="rounded-xl border border-dashed border-ink-300 p-3">
                  <input
                    ref={fileRef}
                    type="file"
                    accept=".pdf,.docx"
                    className="hidden"
                    onChange={(e) => onPickFile(e.target.files?.[0] ?? null)}
                  />
                  {cvFile ? (
                    <div className="flex items-center gap-3">
                      <FileText className="h-5 w-5 shrink-0 text-ai-600" />
                      <span className="min-w-0 flex-1 truncate text-sm text-ink-700">
                        {cvFile.name}
                      </span>
                      <button
                        onClick={() => onPickFile(null)}
                        className="shrink-0 rounded-lg p-1.5 text-ink-400 hover:bg-ink-100 hover:text-red-600"
                        aria-label="Bỏ file"
                      >
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => fileRef.current?.click()}
                      className="flex w-full items-center justify-center gap-2 rounded-lg bg-ink-50 px-3 py-2 text-sm font-medium text-ink-600 hover:bg-ink-100"
                    >
                      <UploadCloud className="h-4 w-4" /> Chọn file CV
                    </button>
                  )}
                </div>
              )}
            </div>
            {showErr('cv') && <p className="mt-2 text-xs text-red-600">{errors.cv}</p>}
          </section>

          {/* Thông tin liên hệ */}
          <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <h2 className="font-display text-base font-bold">Thông tin liên hệ</h2>
            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-700">
                  Họ và tên
                  <Req />
                </label>
                <div className="relative">
                  <User className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-ink-400" />
                  <input
                    value={fullName}
                    onChange={(e) => {
                      setFullName(e.target.value)
                      clearTouched('fullName')
                    }}
                    onBlur={() => markTouched('fullName')}
                    placeholder="Nguyễn Văn A"
                    className={inputCls(!!showErr('fullName')) + ' pl-9'}
                  />
                </div>
                {showErr('fullName') && (
                  <p className="mt-1 text-xs text-red-600">{errors.fullName}</p>
                )}
              </div>
              <div>
                <label className="mb-1.5 block text-sm font-medium text-ink-700">
                  Số điện thoại
                  <Req />
                </label>
                <div className="relative">
                  <Phone className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-ink-400" />
                  <input
                    value={phone}
                    inputMode="numeric"
                    onChange={(e) => {
                      // Chỉ cho nhập chữ số. Validate ngay (live) → hiện lỗi tới khi hợp lệ.
                      setPhone(e.target.value.replace(/\D/g, ''))
                      markTouched('phone')
                    }}
                    onBlur={() => markTouched('phone')}
                    placeholder="09xx xxx xxx"
                    className={inputCls(!!showErr('phone')) + ' pl-9'}
                  />
                </div>
                {showErr('phone') && <p className="mt-1 text-xs text-red-600">{errors.phone}</p>}
              </div>
            </div>

            <div className="mt-4">
              <label className="mb-1.5 block text-sm font-medium text-ink-700">Email</label>
              <div className="relative">
                <Mail className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-ink-400" />
                <input
                  value={profile?.email || ''}
                  readOnly
                  className="w-full cursor-not-allowed rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5 pl-9 text-sm text-ink-500 outline-none"
                />
              </div>
            </div>

            <div className="mt-4">
              <label className="mb-1.5 block text-sm font-medium text-ink-700">
                Nơi làm việc mong muốn
                <Req />
              </label>
              <SearchableSelect
                icon={<MapPin className="h-4 w-4 shrink-0 text-ink-400" />}
                options={provinces.map((p) => ({ value: p.code, label: p.name }))}
                value={locationCode}
                onChange={(code) => {
                  const p = provinces.find((x) => x.code === code)
                  setLocationCode(p ? p.code : null)
                  setDesiredLocation(p ? p.name : '')
                  markTouched('desiredLocation')
                }}
                placeholder="— Chọn tỉnh/thành —"
              />
              {showErr('desiredLocation') && (
                <p className="mt-1 text-xs text-red-600">{errors.desiredLocation}</p>
              )}
            </div>
          </section>

          {/* Thư giới thiệu / câu trả lời */}
          <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <h2 className="font-display text-base font-bold">Thư giới thiệu</h2>
            <div className="mt-4">
              <label className="mb-1.5 block text-sm font-medium text-ink-700">
                1. Giới thiệu ngắn về kinh nghiệm, kỹ năng chính và vì sao bạn là ứng viên phù hợp?
                <Req />
              </label>
              <textarea
                value={coverLetter}
                onChange={(e) => {
                  setCoverLetter(e.target.value)
                  clearTouched('coverLetter')
                }}
                onBlur={() => markTouched('coverLetter')}
                rows={5}
                placeholder="Chia sẻ về kinh nghiệm, kỹ năng nổi bật và lý do bạn phù hợp với vị trí này…"
                className={inputCls(!!showErr('coverLetter'))}
              />
              {showErr('coverLetter') && (
                <p className="mt-1 text-xs text-red-600">{errors.coverLetter}</p>
              )}
            </div>
            <div className="mt-4">
              <label className="mb-1.5 block text-sm font-medium text-ink-700">
                2. Thời gian báo trước khi nghỉ việc (notice period) của bạn là bao lâu?
                <Req />
              </label>
              <input
                value={noticePeriod}
                onChange={(e) => {
                  setNoticePeriod(e.target.value)
                  clearTouched('noticePeriod')
                }}
                onBlur={() => markTouched('noticePeriod')}
                placeholder="VD: 30 ngày / 45 ngày / có thể bắt đầu ngay"
                className={inputCls(!!showErr('noticePeriod'))}
              />
              {showErr('noticePeriod') && (
                <p className="mt-1 text-xs text-red-600">{errors.noticePeriod}</p>
              )}
            </div>
          </section>

          {submitError && (
            <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              <AlertCircle className="h-4 w-4 shrink-0" /> {submitError}
            </div>
          )}

          {/* Actions */}
          <div className="flex flex-col-reverse gap-3 sm:flex-row sm:items-center sm:justify-between">
            <button
              onClick={requestCancel}
              className="inline-flex items-center justify-center gap-2 rounded-xl border border-ink-200 bg-white px-5 py-3 text-sm font-semibold text-ink-700 hover:bg-ink-50"
            >
              <ArrowLeft className="h-4 w-4" /> Quay lại tin tuyển dụng
            </button>
            <button
              onClick={handleSubmit}
              disabled={submitting}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-6 py-3 text-sm font-bold text-white hover:bg-brand-700 disabled:opacity-60"
            >
              {submitting ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" /> Đang gửi hồ sơ…
                </>
              ) : (
                <>
                  <CheckCircle2 className="h-4 w-4" /> Gửi hồ sơ ứng tuyển
                </>
              )}
            </button>
          </div>

          <p className="flex items-center justify-center gap-1.5 pb-6 text-center text-xs text-ink-400">
            <Sparkles className="h-3.5 w-3.5" /> Hồ sơ sẽ được gửi tới bộ phận nhân sự để xem xét.
          </p>
        </div>
      </main>

      {/* Popup xác nhận hủy */}
      {showCancel && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4">
          <div className="w-full max-w-sm rounded-2xl border border-ink-200 bg-white p-6 shadow-xl">
            <div className="flex items-start gap-3">
              <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-amber-50 text-amber-600">
                <AlertCircle className="h-5 w-5" />
              </span>
              <div>
                <h3 className="font-display text-base font-bold text-ink-900">Huỷ ứng tuyển?</h3>
                <p className="mt-1 text-sm text-ink-500">
                  Bạn có chắc chắn muốn quay lại? Thông tin đã nhập trong biểu mẫu sẽ không được
                  lưu.
                </p>
              </div>
            </div>
            <div className="mt-5 flex gap-3">
              <button
                onClick={() => setShowCancel(false)}
                className="flex-1 rounded-xl border border-ink-200 bg-white px-4 py-2.5 text-sm font-semibold text-ink-700 hover:bg-ink-50"
              >
                Tiếp tục điền
              </button>
              <button
                onClick={goBack}
                className="flex-1 rounded-xl bg-red-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-red-700"
              >
                Huỷ &amp; quay lại
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
