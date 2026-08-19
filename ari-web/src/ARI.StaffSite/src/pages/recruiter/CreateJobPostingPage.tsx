import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate, useLocation, Link } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import ReactQuill from 'react-quill'
import 'react-quill/dist/quill.snow.css'
import {
  ArrowLeft, Trash2, Loader2, PlusCircle, Check, UploadCloud, Sparkles, FileText, X, AlertCircle,
  AlertTriangle, Eye,
} from 'lucide-react'
import { useAuthStore } from '@ari/shared/store/auth'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import jobService from '@ari/shared/fservices/job'
import { ErrorAlert, Select } from '@ari/shared/ui'
import type { CreateJobPostingRequest, RoundConfig, JobPosting } from '@ari/shared/types/job'

interface CreateJobPostingPageProps {
  mode: 'create' | 'edit'
}

// Trần thời lượng mỗi vòng phỏng vấn (sơ loại / chuyên môn) — tối đa 20 phút.
const MAX_ROUND_MINUTES = 20

const input =
  'w-full px-4 py-2.5 rounded-xl bg-white dark:bg-white/5 border border-ink-200 dark:border-white/10 text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:border-brand-400 dark:focus:border-brand-500/50 text-sm'
const label = 'block text-sm font-medium text-ink-700 dark:text-ink-200 mb-1.5'
const card = 'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card'

// Dấu * bắt buộc — luôn hiển thị màu đỏ.
const RequiredStar = () => <span className="text-red-500 ml-0.5">*</span>

export default function CreateJobPostingPage({ mode }: CreateJobPostingPageProps) {
  const { t } = useTranslation('modules/recruiter/createJob')
  const { id: jobId } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const routerLocation = useLocation()
  const user = useAuthStore((state) => state.user)
  const { openDocument } = useDocumentViewer()
  const [submitting, setSubmitting] = useState(false)
  const [loading, setLoading] = useState(mode === 'edit')
  const [error, setError] = useState<string | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const [title, setTitle] = useState('')
  const [department, setDepartment] = useState('')
  const [jobDescription, setJobDescription] = useState('')
  const [jobCategory, setJobCategory] = useState('')
  const [experienceLevel, setExperienceLevel] = useState('middle')
  const [employmentType, setEmploymentType] = useState('full_time')
  const [workMode, setWorkMode] = useState('onsite')
  const [interviewMode, setInterviewMode] = useState<'remote' | 'onsite' | 'both'>('onsite')
  const [location, setLocation] = useState('')
  const [salaryIsNegotiable, setSalaryIsNegotiable] = useState(true)
  const [salaryMin, setSalaryMin] = useState<number | ''>('')
  const [salaryMax, setSalaryMax] = useState<number | ''>('')
  const [salaryCurrency, setSalaryCurrency] = useState('VND')
  const [vacancies, setVacancies] = useState<number | ''>('')
  const [isUrgent, setIsUrgent] = useState(false)
  const [isPublicListing, setIsPublicListing] = useState(true)
  const [jobStatus, setJobStatus] = useState<string>('draft')
  const [languageRequirement, setLanguageRequirement] = useState('')
  const [skillInput, setSkillInput] = useState('')
  const [skills, setSkills] = useState<string[]>([])
  const [applicationDeadline, setApplicationDeadline] = useState('')
  // Mặc định theo đúng phễu tuyển dụng thật: lọc bằng trắc nghiệm trước (rẻ, tự chấm), rồi sơ loại,
  // rồi chuyên sâu kỹ thuật. Vòng trắc nghiệm dùng thời lượng riêng (30') vì không bị trần 20' của
  // buổi phỏng vấn AI — xem ADR-049/050.
  const [interviewPassScore, setInterviewPassScore] = useState(70)
  const [rounds, setRounds] = useState<RoundConfig[]>([
    { roundNumber: 1, roundType: 'online_test', interviewLanguage: 'vi', interviewCodeTtlHours: 2, maxDurationMinutes: 30 },
    { roundNumber: 2, roundType: 'screening', interviewLanguage: 'vi', interviewCodeTtlHours: 2, maxDurationMinutes: MAX_ROUND_MINUTES },
    { roundNumber: 3, roundType: 'technical', interviewLanguage: 'vi', interviewCodeTtlHours: 2, maxDurationMinutes: MAX_ROUND_MINUTES },
  ])

  // `jdFileUrl` là storageKey gửi lên khi lưu tin; `jdFileViewUrl` là URL mở được trên trình duyệt.
  // Trước đây dùng chung một biến nên khung xem nhận đúng storageKey → PDF trắng, DOCX báo lỗi.
  const [jdFileUrl, setJdFileUrl] = useState<string | undefined>()
  const [jdFileViewUrl, setJdFileViewUrl] = useState<string | undefined>()
  const [jdFileName, setJdFileName] = useState<string | undefined>()
  const [jdFileFormat, setJdFileFormat] = useState<string | undefined>()
  const [analyzing, setAnalyzing] = useState(false)
  const [analyzeMsg, setAnalyzeMsg] = useState<{ type: 'ok' | 'warn'; text: string } | null>(null)

  useEffect(() => {
    if (mode === 'edit' && jobId) {
      (async () => {
        try {
          setLoading(true)
          const job: JobPosting = await jobService.getJobPostingById(jobId)
          if (user?.role === 'recruiter' && job.createdByUserId && job.createdByUserId !== user.id) {
            setLoadError('Bạn không có quyền truy cập tin tuyển dụng này hoặc tin không tồn tại.')
            return
          }
          // Recruiter chỉ được sửa tin ở trạng thái nháp hoặc bị từ chối — đã gửi duyệt/đã duyệt thì khoá.
          if (user?.role === 'recruiter' && job.status !== 'draft' && job.status !== 'rejected') {
            setLoadError('Tin đã gửi HR duyệt hoặc đã được duyệt nên không thể chỉnh sửa. Bạn chỉ sửa được tin ở trạng thái nháp hoặc khi bị từ chối.')
            return
          }
          setTitle(job.title || '')
          setDepartment(job.department || '')
          setJobDescription(job.jobDescription || '')
          setJobCategory(job.jobCategory || '')
          setExperienceLevel(job.experienceLevel || 'middle')
          setEmploymentType(job.employmentType || 'full_time')
          setWorkMode(job.workMode || 'onsite')
          setInterviewMode(job.interviewMode || 'onsite')
          setLocation(job.location || '')
          setSalaryIsNegotiable(job.salaryIsNegotiable ?? true)
          setSalaryMin(job.salaryMin ?? '')
          setSalaryMax(job.salaryMax ?? '')
          setSalaryCurrency(job.salaryCurrency || 'VND')
          setVacancies(job.vacancies ?? '')
          setIsUrgent(job.isUrgent || false)
          setIsPublicListing(job.isPublicListing ?? true)
          setJobStatus(job.status || 'draft')
          setLanguageRequirement(job.languageRequirement || '')
          setSkills(job.skills || [])
          setJdFileName(job.jdFileName)
          setJdFileFormat(job.jdFileFormat)
          // Ở chế độ sửa tin, GetJobById ĐÃ presign sẵn `jdFileUrl` → dùng làm URL xem. Không đưa vào
          // `jdFileUrl` (storageKey) vì giá trị đó được gửi ngược lên khi Lưu.
          setJdFileViewUrl(job.jdFileUrl)
          setApplicationDeadline(job.applicationDeadline ? job.applicationDeadline.split('T')[0] : '')
          setRounds(job.roundConfigs?.length ? job.roundConfigs : rounds)
          setInterviewPassScore(job.interviewPassScore ?? 70)
        } catch (err) {
          setLoadError('Bạn không có quyền truy cập tin tuyển dụng này hoặc tin không tồn tại.')
        } finally {
          setLoading(false)
        }
      })()
    }
  }, [mode, jobId, t])

  const handleAnalyze = async (file: File) => {
    setAnalyzing(true)
    setAnalyzeMsg(null)
    setError(null)
    try {
      const r = await jobService.analyzeJd(file)
      setJdFileUrl(r.jdFileUrl)
      // URL xem tách riêng khỏi storageKey: cái gửi lên khi tạo tin phải là KHOÁ, cái mở file phải là URL.
      setJdFileViewUrl(r.jdFileViewUrl || undefined)
      setJdFileName(r.jdFileName)
      setJdFileFormat(r.jdFileFormat)
      if (r.title) setTitle(r.title)
      if (r.department) setDepartment(r.department)
      if (r.jobDescription) setJobDescription(r.jobDescription)
      if (r.jobCategory) setJobCategory(r.jobCategory)
      if (r.experienceLevel) setExperienceLevel(r.experienceLevel)
      if (r.employmentType) setEmploymentType(r.employmentType)
      if (r.workMode) setWorkMode(r.workMode)
      if (r.location) setLocation(r.location)
      if (r.skills?.length) setSkills(r.skills)
      if (r.languageRequirement) setLanguageRequirement(r.languageRequirement)
      if (r.salaryMin != null || r.salaryMax != null) {
        setSalaryIsNegotiable(false)
        if (r.salaryMin != null) setSalaryMin(r.salaryMin)
        if (r.salaryMax != null) setSalaryMax(r.salaryMax)
      }
      // Ba nguyên nhân khác nhau, ba câu khác nhau: AI chết giữa chừng / PDF toàn ảnh không có chữ /
      // đọc được nhưng nội dung không phải JD. Gộp chung là đổ oan cho file của người dùng.
      setAnalyzeMsg(
        r.analysisFailed
          ? { type: 'warn', text: t(r.scannedPdf ? 'jdUpload.failedScanned' : 'jdUpload.failed') }
          : r.isValidJd
            ? {
                type: 'ok',
                text: r.scannedPdf
                  ? `${t('jdUpload.success')} ${t('jdUpload.scannedHint')}`
                  : t('jdUpload.success'),
              }
            : { type: 'warn', text: t('jdUpload.warning') },
      )
    } catch (err: any) {
      setError(err?.response?.data?.message || t('validation.analyzeError'))
    } finally {
      setAnalyzing(false)
    }
  }

  const onPickFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) handleAnalyze(file)
  }

  const handleAddSkill = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && skillInput.trim()) {
      e.preventDefault()
      if (!skills.includes(skillInput.trim())) setSkills([...skills, skillInput.trim()])
      setSkillInput('')
    }
  }

  const addRound = () =>
    setRounds([...rounds, { roundNumber: rounds.length + 1, roundType: 'technical', interviewLanguage: 'vi', interviewCodeTtlHours: 2, maxDurationMinutes: MAX_ROUND_MINUTES }])
  const removeRound = (n: number) => {
    if (rounds.length <= 1) return
    setRounds(rounds.filter((r) => r.roundNumber !== n).map((r, i) => ({ ...r, roundNumber: i + 1 })))
  }
  const changeRound = (i: number, field: keyof RoundConfig, value: any) => {
    const u = [...rounds]
    u[i] = { ...u[i], [field]: value }
    setRounds(u)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    const cleanDesc = jobDescription.replace(/<[^>]*>/g, '').trim()
    if (!title.trim() || !cleanDesc) {
      setError(t('validation.requiredFields'))
      return
    }
    if (mode === 'create' && !jdFileUrl) {
      setError(t('validation.jdRequired'))
      return
    }
    if (interviewMode !== 'remote' && !location.trim()) {
      setError(t('validation.locationRequired'))
      return
    }

    try {
      setSubmitting(true)
      setError(null)
      const payload: CreateJobPostingRequest = {
        title: title.trim(),
        department: department.trim() || undefined,
        jobDescription: jobDescription.trim(),
        jdFileUrl,
        jdFileName,
        jdFileFormat,
        interviewMode,
        location: interviewMode !== 'remote' ? location.trim() : undefined,
        workMode,
        employmentType,
        jobCategory: jobCategory || undefined,
        salaryIsNegotiable,
        salaryMin: salaryIsNegotiable || salaryMin === '' ? undefined : Number(salaryMin),
        salaryMax: salaryIsNegotiable || salaryMax === '' ? undefined : Number(salaryMax),
        salaryCurrency: salaryIsNegotiable ? undefined : salaryCurrency,
        experienceLevel,
        isUrgent,
        isPublicListing,
        vacancies: vacancies === '' ? undefined : Number(vacancies),
        skills,
        interviewPassScore,
        roundConfigs: rounds.map((r) =>
          r.roundType === 'online_test'
            ? { ...r, maxDurationMinutes: Math.max(r.maxDurationMinutes || 30, 1) }
            : { ...r, maxDurationMinutes: Math.min(Math.max(r.maxDurationMinutes || MAX_ROUND_MINUTES, 1), MAX_ROUND_MINUTES) },
        ),
        languageRequirement: languageRequirement.trim() || undefined,
        applicationDeadline: applicationDeadline ? new Date(applicationDeadline).toISOString() : undefined,
      }

      let saved: JobPosting
      if (mode === 'edit' && jobId) saved = await jobService.updateJob(jobId, payload)
      else saved = await jobService.createJobPosting(payload)
      
      if (routerLocation.pathname.startsWith('/hr')) {
        navigate(`/hr/jobs/${saved.id}`)
      } else {
        navigate(`/recruiter/my-jobs/${saved.id}`)
      }
    } catch (err: any) {
      setError(err?.response?.data?.message || err.message || t('validation.saveError'))
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) {
    return (
      <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3">
        <Loader2 className="h-10 w-10 animate-spin text-brand-600 dark:text-brand-400" />
        <p className="text-sm text-ink-500 dark:text-ink-400">{t('loading')}</p>
      </div>
    )
  }

  if (mode === 'edit' && loadError) {
    return (
      <div className="p-6 lg:p-8">
        <ErrorAlert message={loadError || 'Bạn không có quyền truy cập tin tuyển dụng này hoặc tin không tồn tại.'} />
        <Link
          to="/recruiter/my-jobs"
          className="text-sm text-brand-600 dark:text-brand-400 hover:underline"
        >
          ← Quay lại danh sách
        </Link>
      </div>
    )
  }

  const isHr = routerLocation.pathname.startsWith('/hr')
  const canTogglePublic = mode === 'create' || isHr || jobStatus === 'draft'

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="mb-6">
        <button onClick={() => navigate(-1)} className="mb-3 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white">
          <ArrowLeft className="h-4 w-4" /> {t('back')}
        </button>
        <h1 className="text-2xl font-bold text-ink-900 dark:text-white">
          {mode === 'edit' ? t('editTitle') : t('title')}
        </h1>
        <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">{t('description')}</p>
      </motion.div>

      {error && (
        <div className="mb-6 flex items-start gap-2 rounded-xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-400">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {/* JD upload */}
      <div className="mb-6 rounded-2xl border border-brand-200 dark:border-brand-500/30 bg-gradient-to-b from-brand-50/60 dark:from-brand-500/10 to-white dark:to-white/5 p-6 shadow-card">
        <div className="flex items-center gap-2 text-sm font-semibold text-brand-700 dark:text-brand-300">
          <Sparkles className="h-4 w-4" /> {mode === 'create' ? t('jdUpload.titleWithCreate') : t('jdUpload.title')}
        </div>
        <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">{t('jdUpload.description')}</p>

        <input ref={fileRef} type="file" accept=".pdf,.docx" onChange={onPickFile} className="hidden" />

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => fileRef.current?.click()}
            disabled={analyzing}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-60"
          >
            {analyzing ? <Loader2 className="h-4 w-4 animate-spin" /> : <UploadCloud className="h-4 w-4" />}
            {analyzing ? t('jdUpload.analyzing') : jdFileName ? t('jdUpload.changeJd') : t('jdUpload.upload')}
          </button>

          {jdFileName && (
            <span className="inline-flex items-center gap-1 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 pr-2 text-sm text-ink-700 dark:text-ink-200">
              {/* Chip này MỞ ĐƯỢC file JD, nhưng trước đây nó là <span> trơn nên không ai đoán ra.
                  Nay là <button> thật: con trỏ tay, icon mắt, gạch chân khi rê chuột, kèm tooltip. */}
              <button
                type="button"
                onClick={() => jdFileViewUrl && openDocument(jdFileViewUrl, jdFileName)}
                disabled={!jdFileViewUrl}
                title={jdFileViewUrl ? t('jdUpload.clickToView') : undefined}
                className="group inline-flex items-center gap-2 rounded-l-xl py-2 pl-3 pr-1 transition enabled:hover:bg-brand-50 disabled:cursor-default dark:enabled:hover:bg-brand-500/10"
              >
                <FileText className="h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                <span className="max-w-[200px] truncate group-enabled:group-hover:text-brand-700 group-enabled:group-hover:underline dark:group-enabled:group-hover:text-brand-300">
                  {jdFileName}
                </span>
                {jdFileFormat && <span className="text-xs uppercase text-ink-400">{jdFileFormat}</span>}
                {jdFileViewUrl && (
                  <Eye className="h-3.5 w-3.5 shrink-0 text-ink-400 group-hover:text-brand-600 dark:group-hover:text-brand-400" />
                )}
              </button>
              <button
                type="button"
                title={t('jdUpload.removeFile')}
                onClick={() => { setJdFileName(undefined); setJdFileFormat(undefined); setJdFileUrl(undefined); setJdFileViewUrl(undefined); setAnalyzeMsg(null); if (fileRef.current) fileRef.current.value = '' }}
                className="rounded-lg p-1 text-ink-400 hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-500/10"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </span>
          )}
          {jdFileName && jdFileViewUrl && (
            <span className="text-xs text-ink-400 dark:text-ink-500">{t('jdUpload.clickToViewHint')}</span>
          )}
        </div>

        {analyzeMsg && (
          <div className={`mt-3 flex items-center gap-2 rounded-lg px-3 py-2 text-xs ${analyzeMsg.type === 'ok' ? 'bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-400' : 'bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-400'}`}>
            {analyzeMsg.type === 'ok' ? <Check className="h-3.5 w-3.5" /> : <AlertCircle className="h-3.5 w-3.5" />} {analyzeMsg.text}
          </div>
        )}
      </div>

      <form
        onSubmit={handleSubmit}
        className="grid gap-6 grid-cols-1 lg:grid-cols-[minmax(0,1fr)_minmax(320px,360px)]"
      >
        {/* Left */}
        <div className="space-y-6">
          <div className={`${card} space-y-5`}>
            <h2 className="border-b border-ink-100 dark:border-white/10 pb-2 text-base font-semibold text-ink-900 dark:text-white">{t('form.generalInfo')}</h2>

            <div>
              <label className={label}>{t('form.jobTitle')}<RequiredStar /></label>
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('form.jobTitlePlaceholder')} className={input} required />
            </div>

            <div className="grid gap-4 grid-cols-1 sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <label className={label}>{t('form.department')}</label>
                <input value={department} onChange={(e) => setDepartment(e.target.value)} placeholder={t('form.departmentPlaceholder')} className={input} />
              </div>
              <div>
                <label className={label}>{t('form.category')}</label>
                <Select
                  value={jobCategory}
                  onChange={setJobCategory}
                  placeholder={`— ${t('options.selectCategory')} —`}
                  className="w-full"
                  buttonClassName="px-4 py-2.5 text-sm"
                  options={[
                    { value: 'backend', label: 'Backend' },
                    { value: 'frontend', label: 'Frontend' },
                    { value: 'devops', label: 'DevOps / Infra' },
                    { value: 'qa', label: 'QA / Testing' },
                    { value: 'data', label: 'Data' },
                    { value: 'ai_ml', label: 'AI / ML' },
                    { value: 'mobile', label: 'Mobile' },
                    { value: 'pm', label: 'Project Manager' },
                    { value: 'designer', label: 'Designer' },
                    { value: 'other', label: 'Khác' },
                  ]}
                />
              </div>
              <div>
                <label className={label}>{t('form.level')}</label>
                <Select
                  value={experienceLevel}
                  onChange={setExperienceLevel}
                  className="w-full"
                  buttonClassName="px-4 py-2.5 text-sm"
                  options={[
                    { value: 'intern', label: 'Intern' },
                    { value: 'fresher', label: 'Fresher' },
                    { value: 'junior', label: 'Junior' },
                    { value: 'middle', label: 'Middle' },
                    { value: 'senior', label: 'Senior' },
                    { value: 'lead', label: 'Lead' },
                    { value: 'manager', label: 'Manager' },
                  ]}
                />
              </div>
            </div>

            <div>
              <label className={label}>{t('form.jobDescription')}<RequiredStar /></label>
              <div className="quill-editor-wrapper">
                <ReactQuill
                  theme="snow"
                  value={jobDescription}
                  onChange={setJobDescription}
                  placeholder={t('form.jobDescriptionPlaceholder')}
                  modules={{
                    toolbar: [
                      [{ header: [1, 2, 3, false] }],
                      ['bold', 'italic', 'underline', 'strike'],
                      [{ list: 'ordered' }, { list: 'bullet' }],
                      ['clean'],
                    ],
                  }}
                />
              </div>
            </div>

            <div>
              <label className={label}>{t('form.skills')}</label>
              <input value={skillInput} onChange={(e) => setSkillInput(e.target.value)} onKeyDown={handleAddSkill} placeholder={t('form.skillsPlaceholder')} className={input} />
              {skills.length > 0 && (
                <div className="mt-3 flex flex-wrap gap-2">
                  {skills.map((tag) => (
                    <span key={tag} className="flex items-center gap-1 rounded-full bg-brand-100 dark:bg-brand-500/20 px-3 py-1 text-xs font-medium text-brand-700 dark:text-brand-300">
                      {tag}
                      <button type="button" onClick={() => setSkills(skills.filter((s) => s !== tag))} className="ml-0.5 hover:text-red-500">×</button>
                    </span>
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className={`${card} space-y-5`}>
            <h2 className="border-b border-ink-100 dark:border-white/10 pb-2 text-base font-semibold text-ink-900 dark:text-white">{t('form.compensationLocation')}</h2>

            <div className="grid gap-4 grid-cols-1 sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <label className={label}>{t('form.employmentType')}</label>
                <Select
                  value={employmentType}
                  onChange={setEmploymentType}
                  className="w-full"
                  buttonClassName="px-4 py-2.5 text-sm"
                  options={[
                    { value: 'full_time', label: 'Toàn thời gian' },
                    { value: 'part_time', label: 'Bán thời gian' },
                    { value: 'contract', label: 'Hợp đồng' },
                    { value: 'internship', label: 'Thực tập' },
                    { value: 'freelance', label: 'Freelance' },
                  ]}
                />
              </div>
              <div>
                <label className={label}>{t('form.workLocation')}</label>
                <Select
                  value={workMode}
                  onChange={setWorkMode}
                  className="w-full"
                  buttonClassName="px-4 py-2.5 text-sm"
                  options={[
                    { value: 'onsite', label: t('options.onsite') },
                    { value: 'hybrid', label: t('options.hybrid') },
                    { value: 'remote', label: t('options.remoteWork') },
                  ]}
                />
              </div>
              <div>
                <label className={label}>{t('form.interviewMode')}<RequiredStar /></label>
                <Select
                  value={interviewMode}
                  onChange={(v) => setInterviewMode(v as typeof interviewMode)}
                  className="w-full"
                  buttonClassName="px-4 py-2.5 text-sm"
                  options={[
                    { value: 'remote', label: t('options.remote') },
                    { value: 'onsite', label: t('options.onsiteInterview') },
                    { value: 'both', label: t('options.hybridInterview') },
                  ]}
                />
              </div>
            </div>

            {interviewMode !== 'remote' && (
              <div>
                <label className={label}>{t('form.workAddress')}<RequiredStar /></label>
                <input value={location} onChange={(e) => setLocation(e.target.value)} placeholder={t('form.workLocationPlaceholder')} className={input} required />
              </div>
            )}

            <div>
              <label className={label}>{t('form.vacancies')}</label>
              <input
                type="number"
                min={1}
                value={vacancies}
                onChange={(e) => setVacancies(e.target.value === '' ? '' : Number(e.target.value))}
                placeholder={t('form.vacanciesPlaceholder')}
                className={input}
              />
              <p className="mt-1 text-xs text-ink-400">{t('form.vacanciesHint')}</p>
            </div>

            <div>
              <div className="mb-1.5 flex items-center justify-between">
                <label className={label + ' mb-0'}>{t('form.salary')}</label>
                <label className="flex cursor-pointer items-center gap-2 text-xs text-ink-500 dark:text-ink-400">
                  <input type="checkbox" checked={salaryIsNegotiable} onChange={(e) => setSalaryIsNegotiable(e.target.checked)} className="accent-brand-600" />
                  {t('form.salaryNegotiable')}
                </label>
              </div>
              {!salaryIsNegotiable && (
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-3">
                  <input type="number" value={salaryMin} onChange={(e) => setSalaryMin(e.target.value === '' ? '' : Number(e.target.value))} placeholder={t('form.salaryMin')} className={`${input} min-w-0`} />
                  <input type="number" value={salaryMax} onChange={(e) => setSalaryMax(e.target.value === '' ? '' : Number(e.target.value))} placeholder={t('form.salaryMax')} className={`${input} min-w-0`} />
                  <Select
                    value={salaryCurrency}
                    onChange={setSalaryCurrency}
                    className="min-w-0 sm:col-span-2 md:col-span-1"
                    buttonClassName="px-4 py-2.5 text-sm"
                    options={[
                      { value: 'VND', label: 'VND' },
                      { value: 'USD', label: 'USD' },
                    ]}
                  />
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Right */}
        <div className="space-y-6">
          <div className={`${card} space-y-4`}>
            <div className="flex items-center justify-between border-b border-ink-100 dark:border-white/10 pb-2">
              <h2 className="text-base font-semibold text-ink-900 dark:text-white">{t('form.aiInterviewRounds')}</h2>
              <button type="button" onClick={addRound} className="flex items-center gap-1 text-xs font-medium text-brand-600 dark:text-brand-400 hover:underline">
                <PlusCircle className="h-3.5 w-3.5" /> {t('form.addRound')}
              </button>
            </div>
            {/* Cao hơn hẳn mức 320px cũ: 3 vòng mặc định trước đây không nằm gọn trong khung nên
                người dùng phải cuộn mới thấy được toàn cảnh cấu hình vòng. */}
            <div className="max-h-[620px] space-y-3 overflow-y-auto pr-1">
              <AnimatePresence initial={false}>
                {rounds.map((round, idx) => {
                  // Vòng trắc nghiệm cấu hình thời gian riêng ở mục "Bài thi trắc nghiệm" → không áp trần 20 phút.
                  const capped = round.roundType !== 'online_test'
                  return (
                  <motion.div
                    key={round.roundNumber}
                    initial={{ opacity: 0, scale: 0.96 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.96 }}
                    className="group relative space-y-3 rounded-xl border border-ink-100 dark:border-white/10 bg-ink-50 dark:bg-white/5 p-3"
                  >
                    {rounds.length > 1 && (
                      <button type="button" onClick={() => removeRound(round.roundNumber)} className="absolute right-2 top-2 text-ink-300 hover:text-red-500">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    )}
                    <div className="text-sm font-semibold text-ink-900 dark:text-white">{t('form.round', { number: round.roundNumber })}</div>
                    <div>
                      <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">{t('form.roundType')}</label>
                      <Select
                        value={round.roundType}
                        onChange={(v) => changeRound(idx, 'roundType', v)}
                        className="w-full"
                        buttonClassName="px-4 py-2 text-sm"
                        options={[
                          { value: 'screening', label: t('form.screening') },
                          { value: 'technical', label: t('form.technical') },
                          { value: 'online_test', label: t('form.onlineTest') },
                        ]}
                      />
                      {round.roundType === 'online_test' && (
                        <p className="mt-1 text-xs text-brand-600 dark:text-brand-400">{t('form.onlineTestHint')}</p>
                      )}
                    </div>
                    {/* Ô "Phút" chỉ chứa 2–3 chữ số nên cho nó cột hẹp cố định; phần còn lại dành hết
                        cho ô Ngôn ngữ. Chia đôi 50/50 như trước làm nút Select hẹp tới mức "Tiếng Việt"
                        bị cắt thành "Tiếng ..." (Select dùng `truncate`), phải bấm mở mới đọc được. */}
                    <div className="grid grid-cols-[minmax(0,1fr)_92px] gap-2">
                      <div className="min-w-0">
                        <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">{t('form.language')}</label>
                        <Select
                          value={round.interviewLanguage ?? 'vi'}
                          onChange={(v) => changeRound(idx, 'interviewLanguage', v)}
                          className="w-full"
                          buttonClassName="px-3 py-2 text-sm"
                          options={[
                            { value: 'vi', label: t('form.vietnamese') },
                            { value: 'en', label: t('form.english') },
                          ]}
                        />
                      </div>
                      <div className="min-w-0">
                        <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">{t('form.minutes')}</label>
                        <input
                          type="number"
                          min={1}
                          max={capped ? MAX_ROUND_MINUTES : undefined}
                          inputMode="numeric"
                          value={round.maxDurationMinutes === 0 ? '' : round.maxDurationMinutes}
                          onChange={(e) =>
                            changeRound(
                              idx,
                              'maxDurationMinutes',
                              e.target.value === ''
                                ? 0
                                : capped
                                  ? Math.min(Number(e.target.value), MAX_ROUND_MINUTES)
                                  : Number(e.target.value),
                            )
                          }
                          onBlur={(e) => {
                            const v = Number(e.target.value)
                            if (e.target.value === '' || v < 1) changeRound(idx, 'maxDurationMinutes', capped ? MAX_ROUND_MINUTES : 30)
                            else if (capped && v > MAX_ROUND_MINUTES) changeRound(idx, 'maxDurationMinutes', MAX_ROUND_MINUTES)
                          }}
                          className={`${input} py-2`}
                        />
                        {capped && <p className="mt-1 text-xs text-ink-400 dark:text-ink-500">{t('form.minutesMax')}</p>}
                      </div>
                    </div>
                    {/* Ngôn ngữ vòng trắc nghiệm quyết định ngôn ngữ của ĐỀ THI, mà đề nằm ở ngân hàng
                        câu hỏi tạo sau — đổi ở đây mà quên cập nhật đề là ứng viên nhận bài sai ngôn ngữ. */}
                    {round.roundType === 'online_test' && (
                      <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-2.5 py-2 dark:border-amber-500/30 dark:bg-amber-500/10">
                        <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0 text-amber-600 dark:text-amber-400" />
                        <p className="text-xs leading-relaxed text-amber-800 dark:text-amber-200">
                          {t('form.onlineTestLanguageNotice', {
                            language:
                              (round.interviewLanguage ?? 'vi') === 'en'
                                ? t('form.english')
                                : t('form.vietnamese'),
                          })}
                        </p>
                      </div>
                    )}
                  </motion.div>
                  )
                })}
              </AnimatePresence>
            </div>

            {/* Điểm sàn phỏng vấn (ADR-060): ngưỡng AI kết luận đạt/không đạt khi tin đã khai bộ
                tiêu chí chấm. Đặt ở đây vì nó áp cho MỌI vòng phỏng vấn của tin, không phải từng vòng. */}
            <div className="border-t border-ink-100 pt-3 dark:border-white/10">
              <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                {t('form.interviewPassScore')}
              </label>
              <input
                type="number"
                min={0}
                max={100}
                inputMode="numeric"
                value={interviewPassScore}
                onChange={(e) =>
                  setInterviewPassScore(Math.min(100, Math.max(0, Number(e.target.value) || 0)))
                }
                className={`${input} py-2`}
              />
              <p className="mt-1 text-xs text-ink-400 dark:text-ink-500">
                {t('form.interviewPassScoreHint')}
              </p>
            </div>
          </div>

          <div className={`${card} space-y-4`}>
            <h2 className="border-b border-ink-100 dark:border-white/10 pb-2 text-base font-semibold text-ink-900 dark:text-white">{t('form.visibility')}</h2>
            <div>
              <label className={label}>{t('form.languageRequirement')}</label>
              <input value={languageRequirement} onChange={(e) => setLanguageRequirement(e.target.value)} placeholder={t('form.languageRequirementPlaceholder')} className={input} />
            </div>
            <div>
              <label className={label}>{t('form.applicationDeadline')}</label>
              <input
                type="date"
                value={applicationDeadline}
                onChange={(e) => setApplicationDeadline(e.target.value)}
                className={input}
              />
            </div>
            <label className="flex items-center gap-2 cursor-pointer text-sm font-medium text-ink-700 dark:text-ink-300">
              <input type="checkbox" checked={isUrgent} onChange={(e) => setIsUrgent(e.target.checked)} className="accent-brand-600" />
              {t('form.urgent')}
            </label>
            <label className={`flex items-center gap-2 text-sm font-medium text-ink-700 dark:text-ink-300 ${!canTogglePublic ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'}`} title={!canTogglePublic ? 'Chỉ HR Admin mới có quyền bật/tắt tin Public khi tin đã được gửi duyệt.' : ''}>
              <input type="checkbox" checked={isPublicListing} onChange={(e) => setIsPublicListing(e.target.checked)} disabled={!canTogglePublic} className="accent-brand-600" />
              {t('form.publicOnJobBoard')}
            </label>

            <div className="flex gap-3 pt-2">
              <button type="button" onClick={() => navigate(-1)} disabled={submitting} className="flex-1 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10">
                {t('form.cancel')}
              </button>
              <button type="submit" disabled={submitting} className="flex flex-1 items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50">
                {submitting ? <><Loader2 className="h-4 w-4 animate-spin" /> {t('back')}...</> : <><Check className="h-4 w-4" /> {mode === 'edit' ? t('form.save') : t('form.createJob')}</>}
              </button>
            </div>
          </div>
        </div>
      </form>
    </div>
  )
}
