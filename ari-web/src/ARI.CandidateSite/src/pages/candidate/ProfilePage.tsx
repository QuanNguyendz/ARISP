import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
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
  Lightbulb,
  CheckCircle2,
  AlertTriangle,
  Download,
} from 'lucide-react'
import { profileService } from '@ari/shared/fservices/profile/profileService'
import { provinceService } from '@/fservices/location/provinceService'
import type { Province } from '@/fservices/location/provinceService'
import ChangePasswordModal from '@components/profile/ChangePasswordModal'
import SearchableSelect from '@ari/shared/ui/SearchableSelect'
import { Skeleton } from '@ari/shared/ui/Skeleton'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import type {
  CandidateProfile,
  ExperienceItem,
  EducationItem,
  CvReview,
} from '@ari/shared/fservices/profile/profileService'
import { useAuthStore } from '@ari/shared/store/auth'

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
  const { t } = useTranslation('modules/candidate')
  const { updateUser } = useAuthStore()
  const { openDocument } = useDocumentViewer()

  const SECTIONS = [
    { id: 'personal', label: t('profile.personalInfo'), icon: User },
    { id: 'cv', label: t('profile.cvAndAi'), icon: FileText },
    { id: 'skills', label: t('profile.skillsAndTech'), icon: Sparkles },
    { id: 'experience', label: t('profile.experience'), icon: Briefcase },
    { id: 'education', label: t('profile.education'), icon: GraduationCap },
    { id: 'links', label: t('profile.links'), icon: LinkIcon },
    { id: 'account', label: t('profile.accountSecurity'), icon: Shield },
  ]
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

  // Chỉ dẫn tới khu vực tải CV khi vào từ banner "Tải CV lên" (?focus=cv).
  const [searchParams, setSearchParams] = useSearchParams()
  const cvSectionRef = useRef<HTMLElement | null>(null)
  const [cvGuideMounted, setCvGuideMounted] = useState(false) // còn trong DOM (giữ trong lúc fade-out)
  const [cvGuide, setCvGuide] = useState(false) // đang hiển thị ở opacity đầy đủ
  const cvGuideTriggered = useRef(false)

  const todayStr = new Date().toISOString().slice(0, 10)

  useEffect(() => {
    profileService
      .getProfile()
      .then(setProfile)
      .catch((e: any) => setError(e?.message || t('profile.saveFailed')))
      .finally(() => setLoading(false))
  }, [t])

  // Sau khi hồ sơ đã render, nếu được dẫn từ banner (?focus=cv) → cuộn tới khối CV,
  // bật hiệu ứng chỉ dẫn (glow + nhãn) rồi gỡ tham số khỏi URL để không lặp lại khi refresh.
  // Dùng ref one-shot để StrictMode (double-invoke effect ở dev) không huỷ scroll/glow:
  // không clear timeout trong cleanup, và chỉ chạy đúng một lần.
  useEffect(() => {
    if (loading || !profile) return
    if (cvGuideTriggered.current) return
    if (searchParams.get('focus') !== 'cv') return
    cvGuideTriggered.current = true

    // Gỡ tham số khỏi URL để refresh không lặp lại chỉ dẫn.
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev)
        next.delete('focus')
        return next
      },
      { replace: true }
    )

    // Đợi layout/route ổn định rồi cuộn + bật chỉ dẫn. Không clear ở cleanup để
    // lần "unmount giả" của StrictMode không huỷ hiệu ứng.
    // Vòng đời: mount → (rAF) fade-in → giữ ~4s → fade-out (700ms) → gỡ khỏi DOM.
    window.setTimeout(() => {
      cvSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
      setCvGuideMounted(true)
      window.requestAnimationFrame(() => setCvGuide(true))
      window.setTimeout(() => setCvGuide(false), 4200) // bắt đầu fade-out
      window.setTimeout(() => setCvGuideMounted(false), 4200 + 800) // gỡ sau khi fade xong
    }, 250)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loading, profile, searchParams])

  // Tải danh sách tỉnh/thành (Provinces Open API v2) 1 lần.
  useEffect(() => {
    provinceService
      .getProvinces()
      .then(setProvinces)
      .catch(() => setProvinces([]))
  }, [])

  function onProvinceChange(code: number) {
    const p = provinces.find((x) => x.code === code)
    // "Nơi làm việc mong muốn" = tỉnh/thành. Xoá luôn phường/xã (không còn dùng).
    patch({
      provinceCode: p ? p.code : null,
      provinceName: p ? p.name : null,
      wardCode: null,
      wardName: null,
    })
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
      setError(t('profile.nameRequired'))
      return
    }
    const phoneDigits = (profile.phone || '').replace(/\D/g, '')
    if (profile.phone?.trim() && (phoneDigits.length < 8 || phoneDigits.length > 15)) {
      setError(t('profile.phoneInvalid'))
      return
    }
    if (profile.dateOfBirth && profile.dateOfBirth > todayStr) {
      setError(t('profile.dobFutureError'))
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
        // Không còn dùng phường/xã — "Nơi làm việc mong muốn" chỉ là tỉnh/thành.
        wardCode: null,
        wardName: null,
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
      setError(e?.response?.data?.message || e?.message || t('profile.saveFailed'))
    } finally {
      setSaving(false)
    }
  }

  async function handleCvUpload(file: File) {
    const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'))
    if (ext !== '.pdf' && ext !== '.docx') {
      setCvError(t('profile.cvFileTypeError'))
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      setCvError(t('profile.cvFileSizeError'))
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
        setCvNotice(t('profile.cvAiTempUnavailable') + (res.aiMessage ? `: ${res.aiMessage}` : '.'))
      } else {
        setCvNotice(t('profile.cvUploadedAndAnalyzed'))
      }
    } catch (e: any) {
      setCvError(e?.response?.data?.message || e?.message || t('profile.cvUploadFailed'))
    } finally {
      setCvUploading(false)
    }
  }

  if (loading) {
    return <ProfileSkeleton />
  }

  if (!profile) {
    return (
      <div className="mx-auto max-w-3xl px-4 sm:px-6 py-8 sm:py-10">
        <div className="flex items-center gap-2 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          <AlertCircle className="h-4 w-4" /> {error || t('profile.saveFailed')}
        </div>
      </div>
    )
  }

  const initials = initialsOf(profile.fullName, profile.email)

  return (
    <>
      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-4 sm:px-6 pt-6">
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="/" className="hover:text-brand-600">
            {t('profile.home')}
          </Link>
          <ChevronRight className="h-4 w-4" />
          <span className="font-medium text-ink-600">{t('profile.myProfile')}</span>
        </div>
      </div>

      <div className="mx-auto grid max-w-6xl gap-6 px-4 sm:px-6 py-6 lg:grid-cols-[240px_1fr] lg:gap-8">
        {/* Section nav — horizontal scroll trên mobile, sticky aside từ lg */}
        <aside className="lg:self-start lg:sticky lg:top-24">
          <nav
            aria-label="Profile sections"
            className="-mx-4 -mr-2 flex gap-2 overflow-x-auto px-4 py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden sm:mx-0 sm:mr-0 sm:flex-wrap sm:px-0 lg:flex-col lg:gap-0 lg:overflow-visible lg:rounded-2xl lg:border lg:border-ink-200 lg:bg-white lg:p-2 lg:text-sm lg:shadow-card"
          >
            {SECTIONS.map((s) => {
              const Icon = s.icon
              return (
                <a
                  key={s.id}
                  href={`#${s.id}`}
                  className="inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full border border-ink-200 bg-white px-3 py-1.5 text-sm text-ink-600 hover:bg-ink-50 sm:gap-2 sm:px-3.5 lg:whitespace-normal lg:rounded-xl lg:border-0 lg:px-3 lg:py-2.5 lg:text-sm lg:hover:bg-ink-100"
                >
                  <Icon className="h-4 w-4 text-ink-400" /> {s.label}
                </a>
              )
            })}
          </nav>
          <div className="mt-4 rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <div className="flex items-center justify-between text-sm font-semibold">
              <span className="flex items-center gap-2">
                <BadgeCheck className="h-4 w-4 text-brand-600" /> {t('profile.completeness')}
              </span>
              <span className="text-brand-600">{completeness}%</span>
            </div>
            <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-ink-100">
              <div
                className="h-full rounded-full bg-gradient-to-r from-brand-600 to-ai-600 transition-all"
                style={{ width: `${completeness}%` }}
              />
            </div>
            <p className="mt-2 text-xs text-ink-400">{t('profile.completeHint')}</p>
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
            <div className="px-4 sm:px-6 pb-2">
              <div className="-mt-10">
                <span className="grid h-16 w-16 place-items-center rounded-2xl bg-gradient-to-br from-brand-600 to-ai-600 text-xl sm:text-2xl font-extrabold text-white shadow-card ring-4 ring-white dark:ring-ink-900 sm:h-20 sm:w-20">
                  {initials}
                </span>
              </div>
              <div className="mt-3">
                <h1 className="font-display text-xl sm:text-2xl font-extrabold leading-tight">
                  {profile.fullName || t('profile.candidate')}
                </h1>
                <p className="text-sm text-ink-500">
                  {profile.headline || t('profile.notUpdatedYet')}
                </p>
              </div>
            </div>
            <div className="border-t border-ink-100 p-6">
              <div className="mb-4 flex items-center justify-between">
                <h2 className="font-display text-lg font-bold">{t('profile.personalInfo')}</h2>
                <span className="text-xs text-ink-400">{t('profile.showingToHr')}</span>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label={t('profile.fullName')}>
                  <div className={inputWrap}>
                    <User className="h-4 w-4 text-ink-400" />
                    <input
                      className="w-full bg-transparent text-sm outline-none"
                      value={profile.fullName}
                      onChange={(e) => patch({ fullName: e.target.value })}
                    />
                  </div>
                </Field>
                <Field label={t('profile.headline')}>
                  <div className={inputWrap}>
                    <Briefcase className="h-4 w-4 text-ink-400" />
                    <input
                      className="w-full bg-transparent text-sm outline-none"
                      placeholder={t('profile.headlinePlaceholder')}
                      value={profile.headline || ''}
                      onChange={(e) => patch({ headline: e.target.value })}
                    />
                  </div>
                </Field>
                <Field label={t('profile.email')}>
                  <div className="flex items-center gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5">
                    <Mail className="h-4 w-4 shrink-0 text-ink-400" />
                    <input
                      className="w-full min-w-0 bg-transparent text-sm text-ink-500 outline-none"
                      value={profile.email}
                      readOnly
                    />
                    {profile.emailVerified && (
                      <span className="inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                        <Check className="h-3 w-3 shrink-0" /> {t('profile.verified')}
                      </span>
                    )}
                  </div>
                </Field>
                <Field label={t('profile.phone')}>
                  <div className={inputWrap}>
                    <Phone className="h-4 w-4 text-ink-400" />
                    <input
                      inputMode="tel"
                      className="w-full bg-transparent text-sm outline-none"
                      placeholder={t('profile.headlinePlaceholder')}
                      value={profile.phone || ''}
                      onChange={(e) => patch({ phone: e.target.value.replace(/[^\d+\-() ]/g, '') })}
                    />
                  </div>
                </Field>
                <Field label={t('profile.desiredLocation')}>
                  <SearchableSelect
                    icon={<MapPin className="h-4 w-4 shrink-0 text-ink-400" />}
                    options={provinces.map((p) => ({ value: p.code, label: p.name }))}
                    value={profile.provinceCode}
                    onChange={onProvinceChange}
                    placeholder={t('profile.selectProvince')}
                  />
                </Field>
                <Field label={t('profile.dateOfBirth')}>
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
                  <Field label={t('profile.about')}>
                    <textarea
                      rows={3}
                      className="w-full rounded-xl border border-ink-200 bg-ink-50 px-3 py-2.5 text-sm text-ink-800 outline-none placeholder:text-ink-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100"
                      placeholder={t('profile.aboutPlaceholder')}
                      value={profile.about || ''}
                      onChange={(e) => patch({ about: e.target.value })}
                    />
                  </Field>
                </div>
              </div>
            </div>
          </section>

          {/* CV section */}
          <section id="cv" ref={cvSectionRef} className={`${cardCls} relative`}>
            {cvGuideMounted && (
              <>
                {/* Lớp phủ viền glow — fade in/out mượt (chỉ là box-shadow ở viền, không che nội dung) */}
                <div
                  className={`pointer-events-none absolute inset-0 z-10 rounded-2xl ring-2 ring-ai-400 transition-opacity duration-700 ${
                    cvGuide ? 'animate-guide-glow opacity-100' : 'opacity-0'
                  }`}
                />
                {/* Nhãn chỉ dẫn */}
                <div
                  className={`pointer-events-none absolute -top-3 left-6 z-20 inline-flex items-center gap-1.5 rounded-full bg-ai-600 px-3 py-1 text-xs font-semibold text-white shadow-lg transition-all duration-700 ${
                    cvGuide ? 'translate-y-0 opacity-100' : '-translate-y-1 opacity-0'
                  }`}
                >
                  <Sparkles className="h-3.5 w-3.5" /> {t('profile.uploadCvGuide')}
                </div>
              </>
            )}
            <div className="mb-4 flex items-center justify-between">
              <h2 className="flex items-center gap-2 font-display text-lg font-bold">
                <FileText className="h-5 w-5 text-brand-600" /> {t('profile.cvAndAi')}
              </h2>
              <span className="text-xs text-ink-400">{t('profile.pdfDocxMax5mb')}</span>
            </div>

            {profile.profileCvUrl && (
              <div className="mb-3 flex items-center gap-3 rounded-xl border border-brand-200 bg-brand-50 p-3">
                {/* Vùng file — bấm để xem inline trong app */}
                <button
                  type="button"
                  onClick={() =>
                    openDocument(
                      resolveAssetUrl(profile.profileCvUrl),
                      profile.cvFileName || t('profile.cv')
                    )
                  }
                  className="flex min-w-0 flex-1 items-center gap-3 rounded-lg text-left hover:opacity-80"
                  title={t('profile.clickToView')}
                >
                  <div className="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-red-50 text-red-600">
                    <FileText className="h-5 w-5" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-medium text-ink-800">
                      {profile.cvFileName || t('profile.cv')}
                    </div>
                    <div className="text-xs text-ink-400">{t('profile.clickToView')}</div>
                  </div>
                </button>
                {/* Icon tải về — bấm để download */}
                <a
                  href={resolveAssetUrl(profile.cvDownloadUrl || profile.profileCvUrl)}
                  download={profile.cvFileName || true}
                  className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-ink-400 hover:bg-white hover:text-brand-600"
                  title={t('profile.download')}
                  aria-label={t('profile.download')}
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
                  <Loader2 className="h-5 w-5 animate-spin" /> {t('profile.aiAnalyzing')}
                </>
              ) : (
                <>
                  <UploadCloud className="h-5 w-5" /> {t('profile.uploadToAiEvaluate')}
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
              <Sparkles className="h-5 w-5 text-brand-600" /> {t('profile.skillsAndTech')}
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
                  className="w-20 sm:w-28 bg-transparent px-1 text-sm outline-none placeholder:text-ink-400"
                  placeholder={t('profile.skillPlaceholder')}
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
                  aria-label={t('profile.addSkill')}
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
                    {t('profile.suggestionsPopular')}
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
                <Briefcase className="h-5 w-5 text-brand-600" /> {t('profile.experience')}
              </h2>
              <button
                onClick={addExp}
                className="inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-semibold text-brand-600 hover:bg-brand-50"
              >
                <Plus className="h-4 w-4" /> {t('profile.add')}
              </button>
            </div>
            {profile.experience.length === 0 ? (
              <p className="text-sm text-ink-400">{t('profile.noExperience')}</p>
            ) : (
              <div className="space-y-4">
                {profile.experience.map((exp, i) => (
                  <div key={i} className="rounded-xl border border-ink-200 p-4">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <input
                        className={`${inputWrap} text-sm`}
                        placeholder={t('profile.jobTitle')}
                        value={exp.title}
                        onChange={(e) => updateExp(i, 'title', e.target.value)}
                      />
                      <input
                        className={`${inputWrap} text-sm`}
                        placeholder={t('profile.companyOrg')}
                        value={exp.organization}
                        onChange={(e) => updateExp(i, 'organization', e.target.value)}
                      />
                      <input
                        className={`${inputWrap} text-sm`}
                        placeholder={t('profile.timePeriodPlaceholder')}
                        value={exp.period}
                        onChange={(e) => updateExp(i, 'period', e.target.value)}
                      />
                      <button
                        onClick={() => removeExp(i)}
                        className="inline-flex items-center justify-center gap-1.5 rounded-xl border border-red-200 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 sm:w-auto"
                      >
                        <Trash2 className="h-4 w-4" /> {t('profile.delete')}
                      </button>
                    </div>
                    <textarea
                      rows={2}
                      className="mt-3 w-full rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-sm text-ink-800 outline-none placeholder:text-ink-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-100"
                      placeholder={t('profile.jobDescription')}
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
                <GraduationCap className="h-5 w-5 text-brand-600" /> {t('profile.education')}
              </h2>
              <button
                onClick={addEdu}
                className="inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-semibold text-brand-600 hover:bg-brand-50"
              >
                <Plus className="h-4 w-4" /> {t('profile.add')}
              </button>
            </div>
            {profile.education.length === 0 ? (
              <p className="text-sm text-ink-400">{t('profile.noEducation')}</p>
            ) : (
              <div className="space-y-4">
                {profile.education.map((edu, i) => (
                  <div
                    key={i}
                    className="grid grid-cols-1 gap-3 rounded-xl border border-ink-200 p-4 sm:grid-cols-2"
                  >
                    <input
                      className={`${inputWrap} text-sm`}
                      placeholder={t('profile.school')}
                      value={edu.school}
                      onChange={(e) => updateEdu(i, 'school', e.target.value)}
                    />
                    <input
                      className={`${inputWrap} text-sm`}
                      placeholder={t('profile.degreeMajor')}
                      value={edu.degree}
                      onChange={(e) => updateEdu(i, 'degree', e.target.value)}
                    />
                    <input
                      className={`${inputWrap} text-sm`}
                      placeholder={t('profile.periodPlaceholder')}
                      value={edu.period}
                      onChange={(e) => updateEdu(i, 'period', e.target.value)}
                    />
                    <div className="flex min-w-0 gap-2">
                      <input
                        className={`${inputWrap} min-w-0 flex-1 text-sm`}
                        placeholder={t('profile.notes')}
                        value={edu.note || ''}
                        onChange={(e) => updateEdu(i, 'note', e.target.value)}
                      />
                      <button
                        onClick={() => removeEdu(i)}
                        className="grid w-10 shrink-0 place-items-center rounded-xl border border-red-200 text-red-600 hover:bg-red-50"
                        aria-label={t('profile.delete')}
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
              <LinkIcon className="h-5 w-5 text-brand-600" /> {t('profile.links')}
            </h2>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label={t('profile.linkedin')}>
                <div className={inputWrap}>
                  <Link2 className="h-4 w-4 text-ink-400" />
                  <input
                    className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                    placeholder={t('profile.linkedinPlaceholder')}
                    value={profile.linkedinUrl || ''}
                    onChange={(e) => patch({ linkedinUrl: e.target.value })}
                  />
                </div>
              </Field>
              <Field label={t('profile.github')}>
                <div className={inputWrap}>
                  <LinkIcon className="h-4 w-4 text-ink-400" />
                  <input
                    className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                    placeholder={t('profile.githubPlaceholder')}
                    value={profile.githubUrl || ''}
                    onChange={(e) => patch({ githubUrl: e.target.value })}
                  />
                </div>
              </Field>
              <div className="sm:col-span-2">
                <Field label={t('profile.portfolio')}>
                  <div className={inputWrap}>
                    <Globe className="h-4 w-4 text-ink-400" />
                    <input
                      className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                      placeholder={t('profile.portfolioPlaceholder')}
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
              <Shield className="h-5 w-5 text-brand-600" /> {t('profile.accountSecurity')}
            </h2>
            <div className="divide-y divide-ink-100">
              <div className="flex items-center justify-between py-3">
                <div>
                  <div className="text-sm font-semibold text-ink-800">{t('profile.password')}</div>
                  <div className="text-xs text-ink-400">
                    {profile.hasPassword
                      ? t('profile.changeLoginPassword')
                      : t('profile.noPasswordSet')}
                  </div>
                </div>
                <button
                  onClick={() => setPwdModalOpen(true)}
                  className="rounded-xl border border-ink-200 px-4 py-2 text-sm font-semibold text-ink-700 hover:bg-ink-50"
                >
                  {profile.hasPassword ? t('profile.changePassword') : t('profile.setPassword')}
                </button>
              </div>
              {!profile.hasPassword && (
                <div className="flex items-center justify-between py-3">
                  <div className="flex items-center gap-3">
                    <span className="grid h-9 w-9 place-items-center rounded-lg bg-ink-100">
                      <Globe className="h-4 w-4 text-ink-600" />
                    </span>
                    <div>
                      <div className="text-sm font-semibold text-ink-800">
                        {t('profile.googleLinked')}
                      </div>
                      <div className="text-xs text-ink-400">{profile.email}</div>
                    </div>
                  </div>
                  <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">
                    <Check className="h-3.5 w-3.5" /> {t('profile.linked')}
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
            <Info className="h-4 w-4 text-amber-500" /> {t('profile.unsavedChanges')}
          </span>
          <button
            onClick={handleSave}
            disabled={saving}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2 text-sm font-bold text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
            {t('profile.saveChanges')}
          </button>
        </div>
      )}
      {savedAt && !dirty && (
        <div className="sticky bottom-4 z-20 mx-auto flex max-w-6xl items-center gap-2 rounded-2xl border border-emerald-200 bg-emerald-50 px-5 py-3 text-sm font-medium text-emerald-700 shadow-card">
          <Check className="h-4 w-4" /> {t('profile.saved')}
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

/** Khung skeleton (shimmer) mô phỏng bố cục trang hồ sơ khi đang tải. */
function ProfileSkeleton() {
  return (
    <>
      {/* Breadcrumb */}
      <div className="mx-auto max-w-6xl px-4 sm:px-6 pt-6">
        <Skeleton className="h-4 w-48" />
      </div>

      <div className="mx-auto grid max-w-6xl gap-6 px-4 sm:px-6 py-6 lg:grid-cols-[240px_1fr] lg:gap-8">
        {/* Section nav + completeness */}
        <aside className="space-y-4 self-start">
          <div className="-mx-4 -mr-2 flex gap-2 overflow-x-auto px-4 py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden sm:mx-0 sm:mr-0 sm:flex-wrap sm:px-0 lg:flex-col lg:gap-0 lg:overflow-visible lg:rounded-2xl lg:border lg:border-ink-200 lg:bg-white lg:p-2 lg:shadow-card">
            {Array.from({ length: 7 }).map((_, i) => (
              <div
                key={i}
                className="inline-flex shrink-0 items-center gap-2 whitespace-nowrap rounded-full border border-ink-200 bg-white px-3.5 py-1.5 lg:w-full lg:whitespace-normal lg:rounded-xl lg:border-0 lg:px-3 lg:py-2.5"
              >
                <Skeleton className="h-4 w-4 rounded" />
                <Skeleton className="h-4 w-24" />
              </div>
            ))}
          </div>
          <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <div className="flex items-center justify-between">
              <Skeleton className="h-4 w-28" />
              <Skeleton className="h-4 w-10" />
            </div>
            <Skeleton className="mt-3 h-2 w-full rounded-full" />
            <Skeleton className="mt-3 h-3 w-40" />
          </div>
        </aside>

        {/* Content */}
        <div className="space-y-6">
          {/* Personal card with banner */}
          <div className="overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-card">
            <Skeleton className="h-24 rounded-none" />
            <div className="px-4 sm:px-6 pb-2">
              <Skeleton className="-mt-10 h-16 w-16 rounded-2xl ring-4 ring-white dark:ring-ink-900 sm:h-20 sm:w-20" />
              <div className="mt-3 space-y-2">
                <Skeleton className="h-6 w-48" />
                <Skeleton className="h-4 w-36" />
              </div>
            </div>
            <div className="border-t border-ink-100 p-6">
              <Skeleton className="mb-4 h-5 w-40" />
              <div className="grid gap-4 sm:grid-cols-2">
                {Array.from({ length: 6 }).map((_, i) => (
                  <div key={i} className="space-y-1.5">
                    <Skeleton className="h-3.5 w-24" />
                    <Skeleton className="h-10 w-full rounded-xl" />
                  </div>
                ))}
                <div className="space-y-1.5 sm:col-span-2">
                  <Skeleton className="h-3.5 w-32" />
                  <Skeleton className="h-20 w-full rounded-xl" />
                </div>
              </div>
            </div>
          </div>

          {/* Các section card còn lại */}
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
              <Skeleton className="mb-4 h-5 w-44" />
              <div className="flex flex-wrap gap-2">
                {Array.from({ length: 6 }).map((_, j) => (
                  <Skeleton key={j} className="h-8 w-24 rounded-lg" />
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
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
  const { t } = useTranslation('candidate')
  return (
    <div className="mt-4 rounded-2xl border border-ai-200 bg-ai-50 p-5">
      <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
        <Sparkles className="h-4 w-4" /> Phân tích định hướng CV bởi AI (
        {review.reviewedBy ?? 'Gemini'})
      </div>

      <p className="mt-3 text-sm text-ink-600 leading-relaxed bg-white/50 rounded-xl p-3 border border-ai-100/50">
        {review.summary}
      </p>

      {review.suggestedPositions && review.suggestedPositions.length > 0 && (
        <div className="mt-4 border-t border-ai-200/50 pt-4">
          <div className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-brand-700">
            <Briefcase className="h-4 w-4 text-brand-600" /> Vị trí công việc phù hợp gợi ý
          </div>
          <div className="flex flex-wrap gap-2">
            {review.suggestedPositions.map((pos, i) => (
              <span
                key={i}
                className="inline-flex items-center rounded-xl bg-white px-3 py-1.5 text-xs font-semibold text-brand-700 ring-1 ring-brand-100 shadow-sm"
              >
                {pos}
              </span>
            ))}
          </div>
        </div>
      )}

      {review.strengths.length > 0 && (
        <div className="mt-4 border-t border-ai-200/50 pt-4">
          <div className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-emerald-700">
            <CheckCircle2 className="h-4 w-4" /> {t('profile.strengths')}
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
        <div className="mt-4 border-t border-ai-200/50 pt-4">
          <div className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-brand-700">
            <Lightbulb className="h-4 w-4" /> {t('profile.suggestions')}
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
        <div className="mt-4 border-t border-ai-200/50 pt-4">
          <div className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-amber-700">
            <AlertTriangle className="h-4 w-4" /> {t('profile.missing')}
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
          {t('profile.analyzedAt')} {new Date(review.reviewedAt).toLocaleString('vi-VN')}
        </p>
      )}
    </div>
  )
}
