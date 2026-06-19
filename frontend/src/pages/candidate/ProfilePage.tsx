import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  User,
  Briefcase,
  Mail,
  Phone,
  MapPin,
  Calendar,
  FileText,
  Sparkles,
  GraduationCap,
  Link as LinkIcon,
  Link2,
  Shield,
  Plus,
  X,
  Trash2,
  Loader2,
  AlertCircle,
  Check,
  BadgeCheck,
  Globe,
  ChevronRight,
  Info,
  Save,
  UploadCloud,
  Star,
  Lightbulb,
  CheckCircle2,
  AlertTriangle,
  Download,
} from 'lucide-react'
import { profileService } from '@services/profile/profileService'
import { provinceService } from '@services/location/provinceService'
import type { Province, Ward } from '@services/location/provinceService'
import ChangePasswordModal from '@components/profile/ChangePasswordModal'
import SearchableSelect from '@components/ui/SearchableSelect'
import { resolveAssetUrl } from '@config/constants'
import type {
  CandidateProfile,
  ExperienceItem,
  EducationItem,
  CvReview,
} from '@services/profile/profileService'
import { useAuthStore } from '@store/auth/authStore'

const SECTIONS = [
  { id: 'personal', label: 'Thông tin cá nhân', icon: User },
  { id: 'cv', label: 'CV & tài liệu', icon: FileText },
  { id: 'skills', label: 'Kỹ năng', icon: Sparkles },
  { id: 'experience', label: 'Kinh nghiệm', icon: Briefcase },
  { id: 'education', label: 'Học vấn', icon: GraduationCap },
  { id: 'links', label: 'Liên kết', icon: LinkIcon },
  { id: 'account', label: 'Tài khoản & bảo mật', icon: Shield },
]

// Kỹ năng & công nghệ phổ biến hiện nay (gợi ý nhanh để ứng viên thêm bằng 1 cú nhấp)
const SUGGESTED_SKILLS = [
  // Ngôn ngữ
  'JavaScript',
  'TypeScript',
  'Python',
  'Java',
  'C#',
  'Go',
  'Rust',
  'Kotlin',
  'Swift',
  'PHP',
  'SQL',
  // Frontend
  'React',
  'Next.js',
  'Vue.js',
  'Angular',
  'TailwindCSS',
  'React Native',
  'Flutter',
  // Backend
  'Node.js',
  '.NET',
  'ASP.NET Core',
  'Spring Boot',
  'Django',
  'FastAPI',
  'Express',
  'GraphQL',
  // Data & AI
  'PostgreSQL',
  'MySQL',
  'MongoDB',
  'Redis',
  'Elasticsearch',
  'Machine Learning',
  'TensorFlow',
  'PyTorch',
  'LLM',
  'RAG',
  // DevOps & Cloud
  'Docker',
  'Kubernetes',
  'AWS',
  'Azure',
  'Google Cloud',
  'CI/CD',
  'Terraform',
  'Git',
  'Linux',
  'Microservices',
]

function initialsOf(name?: string, email?: string): string {
  const src = (name || email || 'U').trim()
  const parts = src.split(/\s+/).filter(Boolean)
  if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
  return src.slice(0, 2).toUpperCase()
}

const inputWrap =
  'flex items-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5 text-ink-800 outline-none placeholder:text-ink-400 focus-within:border-brand-500 focus-within:ring-2 focus-within:ring-brand-100'
const cardCls = 'rounded-2xl border border-ink-200 bg-white p-6 shadow-card scroll-mt-24'

export default function ProfilePage() {
  const { updateUser } = useAuthStore()
  const [profile, setProfile] = useState<CandidateProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [dirty, setDirty] = useState(false)
  const [savedAt, setSavedAt] = useState<number | null>(null)
  const [skillInput, setSkillInput] = useState('')
  const [cvUploading, setCvUploading] = useState(false)
  const [cvError, setCvError] = useState('')
  const [cvNotice, setCvNotice] = useState('')
  const [pwdModalOpen, setPwdModalOpen] = useState(false)
  const [provinces, setProvinces] = useState<Province[]>([])
  const [wards, setWards] = useState<Ward[]>([])
  const [wardsLoading, setWardsLoading] = useState(false)

  const todayStr = new Date().toISOString().slice(0, 10)

  useEffect(() => {
    profileService
      .getProfile()
      .then(setProfile)
      .catch((e: any) => setError(e?.message || 'Không tải được hồ sơ.'))
      .finally(() => setLoading(false))
  }, [])

  // Tải danh sách tỉnh/thành (Provinces Open API v2) 1 lần.
  useEffect(() => {
    provinceService.getProvinces().then(setProvinces).catch(() => setProvinces([]))
  }, [])

  // Khi đã có province trong hồ sơ → tải danh sách phường tương ứng.
  useEffect(() => {
    const code = profile?.provinceCode
    if (!code) {
      setWards([])
      return
    }
    setWardsLoading(true)
    provinceService
      .getWards(code)
      .then(setWards)
      .catch(() => setWards([]))
      .finally(() => setWardsLoading(false))
  }, [profile?.provinceCode])

  function onProvinceChange(code: number) {
    const p = provinces.find((x) => x.code === code)
    // Đổi tỉnh → reset phường đã chọn.
    patch({
      provinceCode: p ? p.code : null,
      provinceName: p ? p.name : null,
      wardCode: null,
      wardName: null,
    })
  }

  function onWardChange(code: number) {
    const w = wards.find((x) => x.code === code)
    patch({ wardCode: w ? w.code : null, wardName: w ? w.name : null })
  }

  const completeness = useMemo(() => {
    if (!profile) return 0
    const checks = [
      !!profile.fullName,
      !!profile.headline,
      !!profile.phone,
      !!profile.provinceCode,
      !!profile.about,
      profile.skills.length > 0,
      profile.experience.length > 0,
      !!(profile.linkedinUrl || profile.githubUrl || profile.portfolioUrl),
    ]
    return Math.round((checks.filter(Boolean).length / checks.length) * 100)
  }, [profile])

  function patch(p: Partial<CandidateProfile>) {
    setProfile((prev) => (prev ? { ...prev, ...p } : prev))
    setDirty(true)
    setSavedAt(null)
  }

  function addSkill(value?: string) {
    const v = (value ?? skillInput).trim()
    if (!v || !profile) return
    if (profile.skills.some((s) => s.toLowerCase() === v.toLowerCase())) {
      setSkillInput('')
      return
    }
    patch({ skills: [...profile.skills, v] })
    setSkillInput('')
  }

  function removeSkill(s: string) {
    if (!profile) return
    patch({ skills: profile.skills.filter((x) => x !== s) })
  }

  function updateExp(i: number, field: keyof ExperienceItem, value: string) {
    if (!profile) return
    const next = profile.experience.map((e, idx) => (idx === i ? { ...e, [field]: value } : e))
    patch({ experience: next })
  }
  function addExp() {
    if (!profile) return
    patch({
      experience: [
        ...profile.experience,
        { title: '', organization: '', period: '', description: '' },
      ],
    })
  }
  function removeExp(i: number) {
    if (!profile) return
    patch({ experience: profile.experience.filter((_, idx) => idx !== i) })
  }

  function updateEdu(i: number, field: keyof EducationItem, value: string) {
    if (!profile) return
    const next = profile.education.map((e, idx) => (idx === i ? { ...e, [field]: value } : e))
    patch({ education: next })
  }
  function addEdu() {
    if (!profile) return
    patch({ education: [...profile.education, { school: '', degree: '', period: '', note: '' }] })
  }
  function removeEdu(i: number) {
    if (!profile) return
    patch({ education: profile.education.filter((_, idx) => idx !== i) })
  }

  async function handleSave() {
    if (!profile) return
    setError('')

    // Validate nghiệp vụ
    if (!profile.fullName?.trim()) {
      setError('Vui lòng nhập họ và tên.')
      return
    }
    const phoneDigits = (profile.phone || '').replace(/\D/g, '')
    if (profile.phone?.trim() && (phoneDigits.length < 8 || phoneDigits.length > 15)) {
      setError('Số điện thoại không hợp lệ (chỉ chứa 8–15 chữ số).')
      return
    }
    if (profile.dateOfBirth && profile.dateOfBirth > todayStr) {
      setError('Ngày sinh không được ở tương lai.')
      return
    }

    // Bỏ các mục kinh nghiệm / học vấn trống (không lưu rác vào DB)
    const cleanExperience = profile.experience.filter(
      (e) => e.title?.trim() || e.organization?.trim() || e.description?.trim()
    )
    const cleanEducation = profile.education.filter((e) => e.school?.trim() || e.degree?.trim())

    setSaving(true)
    try {
      const updated = await profileService.updateProfile({
        fullName: profile.fullName.trim(),
        headline: profile.headline,
        phone: profile.phone,
        provinceCode: profile.provinceCode,
        provinceName: profile.provinceName,
        wardCode: profile.wardCode,
        wardName: profile.wardName,
        dateOfBirth: profile.dateOfBirth,
        about: profile.about,
        linkedinUrl: profile.linkedinUrl,
        githubUrl: profile.githubUrl,
        portfolioUrl: profile.portfolioUrl,
        skills: profile.skills,
        experience: cleanExperience,
        education: cleanEducation,
      })
      setProfile(updated)
      updateUser({ name: updated.fullName })
      setDirty(false)
      setSavedAt(Date.now())
    } catch (e: any) {
      setError(e?.response?.data?.message || e?.message || 'Lưu hồ sơ thất bại.')
    } finally {
      setSaving(false)
    }
  }

  async function handleCvUpload(file: File) {
    const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'))
    if (ext !== '.pdf' && ext !== '.docx') {
      setCvError('Chỉ chấp nhận file PDF hoặc DOCX.')
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      setCvError('Kích thước file tối đa 5MB.')
      return
    }
    setCvUploading(true)
    setCvError('')
    setCvNotice('')
    try {
      const res = await profileService.uploadCv(file)
      setProfile((prev) =>
        prev
          ? {
              ...prev,
              profileCvUrl: res.profileCvUrl,
              cvFileName: res.cvFileName ?? file.name,
              cvDownloadUrl: res.cvDownloadUrl,
              cvReview: res.review,
            }
          : prev
      )
      if (!res.aiAvailable) {
        setCvNotice(
          'Đã lưu CV. Phân tích AI tạm thời không khả dụng' +
            (res.aiMessage ? `: ${res.aiMessage}` : '.')
        )
      } else {
        setCvNotice('Đã tải lên và phân tích CV bằng AI.')
      }
    } catch (e: any) {
      setCvError(e?.response?.data?.message || e?.message || 'Tải lên CV thất bại.')
    } finally {
      setCvUploading(false)
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-32 text-ink-400">
        <Loader2 className="h-6 w-6 animate-spin" />
      </div>
    )
  }

  if (!profile) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-10">
        <div className="flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          <AlertCircle className="h-4 w-4" /> {error || 'Không tải được hồ sơ.'}
        </div>
      </div>
    )
  }

  const initials = initialsOf(profile.fullName, profile.email)

  return (
    <>
      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/" className="hover:text-brand-600">
            Trang chủ
          </Link>
          <ChevronRight className="h-4 w-4" />
          <span className="font-medium text-ink-600">Hồ sơ của tôi</span>
        </div>
      </div>

      <div className="mx-auto grid max-w-6xl gap-8 px-6 py-6 lg:grid-cols-[240px_1fr]">
        {/* Section nav */}
        <aside className="self-start lg:sticky lg:top-24">
          <nav className="rounded-2xl border border-ink-200 bg-white p-2 text-sm shadow-card">
            {SECTIONS.map((s) => {
              const Icon = s.icon
              return (
                <a
                  key={s.id}
                  href={`#${s.id}`}
                  className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-ink-600 hover:bg-ink-100"
                >
                  <Icon className="h-4 w-4 text-ink-400" /> {s.label}
                </a>
              )
            })}
          </nav>
          <div className="mt-4 rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <div className="flex items-center justify-between text-sm font-semibold">
              <span className="flex items-center gap-2">
                <BadgeCheck className="h-4 w-4 text-brand-600" /> Độ hoàn thiện
              </span>
              <span className="text-brand-600">{completeness}%</span>
            </div>
            <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-ink-100">
              <div
                className="h-full rounded-full bg-gradient-to-r from-brand-600 to-ai-600 transition-all"
                style={{ width: `${completeness}%` }}
              />
            </div>
            <p className="mt-2 text-xs text-ink-400">
              Thêm kỹ năng, kinh nghiệm và liên kết để đạt 100%.
            </p>
          </div>
        </aside>

        {/* Content */}
        <div className="space-y-6 pb-24">
          {error && (
            <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              <AlertCircle className="h-4 w-4" /> {error}
            </div>
          )}

          {/* Personal */}
          <section
            id="personal"
            className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card scroll-mt-24"
          >
            <div className="h-24 bg-gradient-to-r from-brand-600 via-ai-600 to-ai-500" />
            <div className="px-6 pb-2">
              <div className="-mt-10">
                <span className="grid h-20 w-20 place-items-center rounded-2xl bg-gradient-to-br from-brand-600 to-ai-600 text-2xl font-extrabold text-white shadow-card ring-4 ring-white dark:ring-ink-900">
                  {initials}
                </span>
              </div>
              <div className="mt-3">
                <h1 className="font-display text-2xl font-extrabold leading-tight">
                  {profile.fullName || 'Ứng viên'}
                </h1>
                <p className="text-sm text-ink-500">
                  {profile.headline || 'Chưa cập nhật chức danh'}
                </p>
              </div>
            </div>
            <div className="border-t border-ink-100 p-6">
              <div className="mb-4 flex items-center justify-between">
                <h2 className="font-display text-lg font-bold">Thông tin cá nhân</h2>
                <span className="text-xs text-ink-400">Hiển thị với HR khi bạn ứng tuyển</span>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Họ và tên">
                  <div className={inputWrap}>
                    <User className="h-4 w-4 text-ink-400" />
                    <input
                      className="w-full bg-transparent text-sm outline-none"
                      value={profile.fullName}
                      onChange={(e) => patch({ fullName: e.target.value })}
                    />
                  </div>
                </Field>
                <Field label="Chức danh hiện tại">
                  <div className={inputWrap}>
                    <Briefcase className="h-4 w-4 text-ink-400" />
                    <input
                      className="w-full bg-transparent text-sm outline-none"
                      placeholder="VD: Frontend Engineer"
                      value={profile.headline || ''}
                      onChange={(e) => patch({ headline: e.target.value })}
                    />
                  </div>
                </Field>
                <Field label="Email">
                  <div className="flex items-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5">
                    <Mail className="h-4 w-4 shrink-0 text-ink-400" />
                    <input
                      className="w-full min-w-0 bg-transparent text-sm text-ink-500 outline-none"
                      value={profile.email}
                      readOnly
                    />
                    {profile.emailVerified && (
                      <span className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                        <Check className="h-3 w-3 shrink-0" /> Đã xác minh
                      </span>
                    )}
                  </div>
                </Field>
                <Field label="Số điện thoại">
                  <div className={inputWrap}>
                    <Phone className="h-4 w-4 text-ink-400" />
                    <input
                      inputMode="tel"
                      className="w-full bg-transparent text-sm outline-none"
                      placeholder="VD: 0901 234 567"
                      value={profile.phone || ''}
                      onChange={(e) => patch({ phone: e.target.value.replace(/[^\d+\-() ]/g, '') })}
                    />
                  </div>
                </Field>
                <Field label="Tỉnh / Thành phố">
                  <SearchableSelect
                    icon={<MapPin className="h-4 w-4 shrink-0 text-ink-400" />}
                    options={provinces.map((p) => ({ value: p.code, label: p.name }))}
                    value={profile.provinceCode}
                    onChange={onProvinceChange}
                    placeholder="— Chọn tỉnh/thành —"
                  />
                </Field>
                <Field label="Phường / Xã">
                  <SearchableSelect
                    icon={<MapPin className="h-4 w-4 shrink-0 text-ink-400" />}
                    options={wards.map((w) => ({ value: w.code, label: w.name }))}
                    value={profile.wardCode}
                    onChange={onWardChange}
                    disabled={!profile.provinceCode || wardsLoading}
                    placeholder={
                      !profile.provinceCode
                        ? '— Chọn tỉnh trước —'
                        : wardsLoading
                          ? 'Đang tải...'
                          : '— Chọn phường/xã —'
                    }
                  />
                </Field>
                <Field label="Ngày sinh">
                  <div className={inputWrap}>
                    <Calendar className="h-4 w-4 text-ink-400" />
                    <input
                      type="date"
                      max={todayStr}
                      className="w-full bg-transparent text-sm outline-none"
                      value={profile.dateOfBirth || ''}
                      onChange={(e) => patch({ dateOfBirth: e.target.value })}
                    />
                  </div>
                </Field>
                <div className="sm:col-span-2">
                  <Field label="Giới thiệu bản thân">
                    <textarea
                      rows={3}
                      className="w-full rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5 text-sm text-ink-800 outline-none placeholder:text-ink-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100"
                      placeholder="Vài dòng về kinh nghiệm, thế mạnh và định hướng của bạn..."
                      value={profile.about || ''}
                      onChange={(e) => patch({ about: e.target.value })}
                    />
                  </Field>
                </div>
              </div>
            </div>
          </section>

          {/* CV */}
          <section id="cv" className={cardCls}>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="flex items-center gap-2 font-display text-lg font-bold">
                <FileText className="h-5 w-5 text-brand-600" /> CV & đánh giá AI
              </h2>
              <span className="text-xs text-ink-400">PDF / DOCX · tối đa 5MB</span>
            </div>

            {profile.profileCvUrl && (
              <div className="mb-3 flex items-center gap-3 rounded-xl border border-brand-200 bg-brand-50 p-3">
                {/* Vùng file — bấm để xem trong tab mới */}
                <a
                  href={resolveAssetUrl(profile.profileCvUrl)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex min-w-0 flex-1 items-center gap-3 rounded-lg hover:opacity-80"
                  title="Bấm để xem CV"
                >
                  <div className="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-red-50 text-red-600">
                    <FileText className="h-5 w-5" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-medium text-ink-800">
                      {profile.cvFileName || 'CV hồ sơ'}
                    </div>
                    <div className="text-xs text-ink-400">Bấm để xem</div>
                  </div>
                </a>
                {/* Icon tải về — bấm để download */}
                <a
                  href={resolveAssetUrl(profile.cvDownloadUrl || profile.profileCvUrl)}
                  download={profile.cvFileName || true}
                  className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-ink-400 hover:bg-white hover:text-brand-600"
                  title="Tải về"
                  aria-label="Tải CV về"
                >
                  <Download className="h-4 w-4" />
                </a>
              </div>
            )}

            {/* Upload */}
            <label
              className={`flex w-full cursor-pointer items-center justify-center gap-2 rounded-xl border border-dashed px-4 py-4 text-sm font-semibold ${
                cvUploading
                  ? 'border-ink-200 text-ink-400'
                  : 'border-ink-300 text-ink-600 hover:border-brand-400 hover:text-brand-700'
              }`}
            >
              {cvUploading ? (
                <>
                  <Loader2 className="h-5 w-5 animate-spin" /> AI đang phân tích CV...
                </>
              ) : (
                <>
                  <UploadCloud className="h-5 w-5" /> Tải lên CV để AI đánh giá (PDF, DOCX)
                </>
              )}
              <input
                type="file"
                accept=".pdf,.docx"
                className="hidden"
                disabled={cvUploading}
                onChange={(e) => {
                  const f = e.target.files?.[0]
                  if (f) handleCvUpload(f)
                  e.target.value = ''
                }}
              />
            </label>

            {cvError && (
              <div className="mt-3 flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                <AlertCircle className="h-4 w-4 shrink-0" /> {cvError}
              </div>
            )}
            {cvNotice && !cvError && (
              <div className="mt-3 flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700">
                <Check className="h-4 w-4 shrink-0" /> {cvNotice}
              </div>
            )}

            {/* AI Review */}
            {profile.cvReview && <CvReviewCard review={profile.cvReview} />}
          </section>

          {/* Skills */}
          <section id="skills" className={cardCls}>
            <h2 className="mb-4 flex items-center gap-2 font-display text-lg font-bold">
              <Sparkles className="h-5 w-5 text-brand-600" /> Kỹ năng & công nghệ
            </h2>
            <div className="flex flex-wrap gap-2">
              {profile.skills.map((s) => (
                <span
                  key={s}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-brand-50 px-3 py-1.5 text-sm font-medium text-brand-700"
                >
                  {s}
                  <button onClick={() => removeSkill(s)} aria-label={`Xoá ${s}`}>
                    <X className="h-3.5 w-3.5 cursor-pointer opacity-60 hover:opacity-100" />
                  </button>
                </span>
              ))}
              <div className="inline-flex items-center gap-1 rounded-lg border border-dashed border-ink-300 px-2 py-1">
                <input
                  className="w-28 bg-transparent px-1 text-sm outline-none placeholder:text-ink-400"
                  placeholder="Thêm kỹ năng"
                  value={skillInput}
                  onChange={(e) => setSkillInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault()
                      addSkill()
                    }
                  }}
                />
                <button
                  onClick={() => addSkill()}
                  className="text-ink-500 hover:text-brand-700"
                  aria-label="Thêm kỹ năng"
                >
                  <Plus className="h-4 w-4" />
                </button>
              </div>
            </div>

            {/* Gợi ý kỹ năng phổ biến — bấm để thêm nhanh */}
            {(() => {
              const available = SUGGESTED_SKILLS.filter(
                (s) => !profile.skills.some((x) => x.toLowerCase() === s.toLowerCase())
              )
              if (available.length === 0) return null
              return (
                <div className="mt-4 border-t border-ink-100 pt-4">
                  <p className="mb-2 text-xs font-medium text-ink-400">
                    Gợi ý phổ biến — bấm để thêm
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {available.map((s) => (
                      <button
                        key={s}
                        type="button"
                        onClick={() => addSkill(s)}
                        className="inline-flex items-center gap-1 rounded-lg border border-ink-200 bg-white px-2.5 py-1 text-xs font-medium text-ink-500 transition hover:border-brand-300 hover:bg-brand-50 hover:text-brand-700"
                      >
                        {s}
                        <Plus className="h-3 w-3 opacity-60" />
                      </button>
                    ))}
                  </div>
                </div>
              )
            })()}
          </section>

          {/* Experience */}
          <section id="experience" className={cardCls}>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="flex items-center gap-2 font-display text-lg font-bold">
                <Briefcase className="h-5 w-5 text-brand-600" /> Kinh nghiệm làm việc
              </h2>
              <button
                onClick={addExp}
                className="inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-semibold text-brand-600 hover:bg-brand-50"
              >
                <Plus className="h-4 w-4" /> Thêm
              </button>
            </div>
            {profile.experience.length === 0 ? (
              <p className="text-sm text-ink-400">Chưa có kinh nghiệm. Bấm "Thêm" để bổ sung.</p>
            ) : (
              <div className="space-y-4">
                {profile.experience.map((exp, i) => (
                  <div key={i} className="rounded-xl border border-ink-200 p-4">
                    <div className="grid gap-3 sm:grid-cols-2">
                      <input
                        className={`${inputWrap} text-sm`}
                        placeholder="Chức danh"
                        value={exp.title}
                        onChange={(e) => updateExp(i, 'title', e.target.value)}
                      />
                      <input
                        className={`${inputWrap} text-sm`}
                        placeholder="Công ty / tổ chức"
                        value={exp.organization}
                        onChange={(e) => updateExp(i, 'organization', e.target.value)}
                      />
                      <input
                        className={`${inputWrap} text-sm`}
                        placeholder="Thời gian (VD: 06/2022 – Hiện tại)"
                        value={exp.period}
                        onChange={(e) => updateExp(i, 'period', e.target.value)}
                      />
                      <button
                        onClick={() => removeExp(i)}
                        className="inline-flex items-center justify-center gap-1.5 rounded-xl border border-red-200 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 sm:w-auto"
                      >
                        <Trash2 className="h-4 w-4" /> Xoá
                      </button>
                    </div>
                    <textarea
                      rows={2}
                      className="mt-3 w-full rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-sm text-ink-800 outline-none placeholder:text-ink-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100"
                      placeholder="Mô tả công việc, thành tựu..."
                      value={exp.description || ''}
                      onChange={(e) => updateExp(i, 'description', e.target.value)}
                    />
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Education */}
          <section id="education" className={cardCls}>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="flex items-center gap-2 font-display text-lg font-bold">
                <GraduationCap className="h-5 w-5 text-brand-600" /> Học vấn
              </h2>
              <button
                onClick={addEdu}
                className="inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-semibold text-brand-600 hover:bg-brand-50"
              >
                <Plus className="h-4 w-4" /> Thêm
              </button>
            </div>
            {profile.education.length === 0 ? (
              <p className="text-sm text-ink-400">Chưa có học vấn. Bấm "Thêm" để bổ sung.</p>
            ) : (
              <div className="space-y-4">
                {profile.education.map((edu, i) => (
                  <div
                    key={i}
                    className="grid gap-3 rounded-xl border border-ink-200 p-4 sm:grid-cols-2"
                  >
                    <input
                      className={`${inputWrap} text-sm`}
                      placeholder="Trường"
                      value={edu.school}
                      onChange={(e) => updateEdu(i, 'school', e.target.value)}
                    />
                    <input
                      className={`${inputWrap} text-sm`}
                      placeholder="Bằng cấp / chuyên ngành"
                      value={edu.degree}
                      onChange={(e) => updateEdu(i, 'degree', e.target.value)}
                    />
                    <input
                      className={`${inputWrap} text-sm`}
                      placeholder="Thời gian (VD: 2016 – 2020)"
                      value={edu.period}
                      onChange={(e) => updateEdu(i, 'period', e.target.value)}
                    />
                    <div className="flex gap-2">
                      <input
                        className={`${inputWrap} flex-1 text-sm`}
                        placeholder="Ghi chú (GPA...)"
                        value={edu.note || ''}
                        onChange={(e) => updateEdu(i, 'note', e.target.value)}
                      />
                      <button
                        onClick={() => removeEdu(i)}
                        className="grid w-10 shrink-0 place-items-center rounded-xl border border-red-200 text-red-600 hover:bg-red-50"
                        aria-label="Xoá"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Links */}
          <section id="links" className={cardCls}>
            <h2 className="mb-4 flex items-center gap-2 font-display text-lg font-bold">
              <LinkIcon className="h-5 w-5 text-brand-600" /> Liên kết
            </h2>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="LinkedIn">
                <div className={inputWrap}>
                  <Link2 className="h-4 w-4 text-ink-400" />
                  <input
                    className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                    placeholder="linkedin.com/in/..."
                    value={profile.linkedinUrl || ''}
                    onChange={(e) => patch({ linkedinUrl: e.target.value })}
                  />
                </div>
              </Field>
              <Field label="GitHub">
                <div className={inputWrap}>
                  <LinkIcon className="h-4 w-4 text-ink-400" />
                  <input
                    className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                    placeholder="github.com/..."
                    value={profile.githubUrl || ''}
                    onChange={(e) => patch({ githubUrl: e.target.value })}
                  />
                </div>
              </Field>
              <div className="sm:col-span-2">
                <Field label="Portfolio / Website">
                  <div className={inputWrap}>
                    <Globe className="h-4 w-4 text-ink-400" />
                    <input
                      className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                      placeholder="your-site.dev"
                      value={profile.portfolioUrl || ''}
                      onChange={(e) => patch({ portfolioUrl: e.target.value })}
                    />
                  </div>
                </Field>
              </div>
            </div>
          </section>

          {/* Account */}
          <section id="account" className={cardCls}>
            <h2 className="mb-4 flex items-center gap-2 font-display text-lg font-bold">
              <Shield className="h-5 w-5 text-brand-600" /> Tài khoản & bảo mật
            </h2>
            <div className="divide-y divide-ink-100">
              <div className="flex items-center justify-between py-3">
                <div>
                  <div className="text-sm font-semibold text-ink-800">Mật khẩu</div>
                  <div className="text-xs text-ink-400">
                    {profile.hasPassword
                      ? 'Đổi mật khẩu đăng nhập'
                      : 'Tài khoản Google — chưa đặt mật khẩu'}
                  </div>
                </div>
                <button
                  onClick={() => setPwdModalOpen(true)}
                  className="rounded-xl border border-ink-200 px-4 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50"
                >
                  {profile.hasPassword ? 'Đổi mật khẩu' : 'Đặt mật khẩu'}
                </button>
              </div>
              {!profile.hasPassword && (
                <div className="flex items-center justify-between py-3">
                  <div className="flex items-center gap-3">
                    <span className="grid h-9 w-9 place-items-center rounded-lg bg-ink-100">
                      <Globe className="h-4 w-4 text-ink-600" />
                    </span>
                    <div>
                      <div className="text-sm font-semibold text-ink-800">Google</div>
                      <div className="text-xs text-ink-400">{profile.email}</div>
                    </div>
                  </div>
                  <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">
                    <Check className="h-3.5 w-3.5" /> Đã liên kết
                  </span>
                </div>
              )}
            </div>
          </section>
        </div>
      </div>

      {/* Sticky save bar */}
      {dirty && (
        <div className="sticky bottom-4 z-20 mx-auto flex max-w-6xl items-center justify-between gap-3 rounded-2xl border border-ink-200 bg-white/90 px-5 py-3 shadow-card backdrop-blur">
          <span className="flex items-center gap-2 text-sm text-ink-500">
            <Info className="h-4 w-4 text-amber-500" /> Thay đổi chưa được lưu
          </span>
          <button
            onClick={handleSave}
            disabled={saving}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2 text-sm font-bold text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
            Lưu thay đổi
          </button>
        </div>
      )}
      {savedAt && !dirty && (
        <div className="sticky bottom-4 z-20 mx-auto flex max-w-6xl items-center gap-2 rounded-2xl border border-emerald-200 bg-emerald-50 px-5 py-3 text-sm font-medium text-emerald-700 shadow-card">
          <Check className="h-4 w-4" /> Đã lưu hồ sơ
        </div>
      )}

      {pwdModalOpen && profile && (
        <ChangePasswordModal
          passwordSet={profile.hasPassword}
          onClose={() => setPwdModalOpen(false)}
          onSuccess={() => {
            setProfile((p) => (p ? { ...p, hasPassword: true } : p))
            setPwdModalOpen(false)
          }}
        />
      )}
    </>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="mb-1.5 block text-sm font-medium text-ink-600">{label}</label>
      {children}
    </div>
  )
}

function CvReviewCard({ review }: { review: CvReview }) {
  const score = review.overallScore
  const scoreColor =
    score >= 80 ? 'text-emerald-600' : score >= 60 ? 'text-amber-600' : 'text-red-600'
  const ring =
    score >= 80
      ? 'ring-emerald-200 bg-emerald-50'
      : score >= 60
        ? 'ring-amber-200 bg-amber-50'
        : 'ring-red-200 bg-red-50'

  return (
    <div className="mt-4 rounded-2xl border border-ai-200 bg-ai-50 p-5">
      <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
        <Sparkles className="h-4 w-4" /> Đánh giá CV bởi AI (Gemini)
      </div>
      <div className="mt-4 flex flex-wrap items-center gap-4">
        <div className={`grid h-20 w-20 shrink-0 place-items-center rounded-2xl ring-1 ${ring}`}>
          <div className="text-center">
            <div className={`font-display text-2xl font-extrabold ${scoreColor}`}>{score}</div>
            <div className="text-[10px] font-medium text-ink-400">/100</div>
          </div>
        </div>
        <div className="min-w-0 flex-1">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-white px-3 py-1 text-sm font-semibold text-ink-700 ring-1 ring-ink-200">
            <Star className="h-4 w-4 text-amber-500" /> {review.verdict}
          </span>
          <p className="mt-2 text-sm text-ink-600">{review.summary}</p>
        </div>
      </div>

      {review.strengths.length > 0 && (
        <div className="mt-4">
          <div className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-emerald-700">
            <CheckCircle2 className="h-4 w-4" /> Điểm mạnh
          </div>
          <ul className="space-y-1.5">
            {review.strengths.map((s, i) => (
              <li key={i} className="flex items-start gap-2 text-sm text-ink-600">
                <Check className="mt-0.5 h-4 w-4 shrink-0 text-emerald-500" /> {s}
              </li>
            ))}
          </ul>
        </div>
      )}

      {review.improvements.length > 0 && (
        <div className="mt-4">
          <div className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-brand-700">
            <Lightbulb className="h-4 w-4" /> Gợi ý cải thiện
          </div>
          <ul className="space-y-1.5">
            {review.improvements.map((s, i) => (
              <li key={i} className="flex items-start gap-2 text-sm text-ink-600">
                <Lightbulb className="mt-0.5 h-4 w-4 shrink-0 text-brand-500" /> {s}
              </li>
            ))}
          </ul>
        </div>
      )}

      {review.missingSections.length > 0 && (
        <div className="mt-4">
          <div className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-amber-700">
            <AlertTriangle className="h-4 w-4" /> Còn thiếu
          </div>
          <div className="flex flex-wrap gap-2">
            {review.missingSections.map((s, i) => (
              <span
                key={i}
                className="rounded-lg bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-700"
              >
                {s}
              </span>
            ))}
          </div>
        </div>
      )}

      {review.reviewedAt && (
        <p className="mt-4 text-xs text-ink-400">
          Phân tích lúc {new Date(review.reviewedAt).toLocaleString('vi-VN')}
        </p>
      )}
    </div>
  )
}
