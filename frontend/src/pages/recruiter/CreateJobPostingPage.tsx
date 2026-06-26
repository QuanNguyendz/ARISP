import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import ReactQuill from 'react-quill'
import 'react-quill/dist/quill.snow.css'
import {
  ArrowLeft, Trash2, Loader2, PlusCircle, Check, UploadCloud, Sparkles, FileText, X, AlertCircle,
} from 'lucide-react'
import jobService from '@services/job/jobService'
import type { CreateJobPostingRequest, RoundConfig, JobPosting } from '@/types/job'

interface CreateJobPostingPageProps {
  mode: 'create' | 'edit'
}

const input =
  'w-full px-4 py-2.5 rounded-xl bg-white dark:bg-white/5 border border-ink-200 dark:border-white/10 text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:border-brand-400 dark:focus:border-brand-500/50 text-sm'
const label = 'block text-sm font-medium text-ink-700 dark:text-ink-200 mb-1.5'
const card = 'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card'

const CATEGORIES = [
  ['backend', 'Backend'], ['frontend', 'Frontend'], ['devops', 'DevOps / Infra'], ['qa', 'QA / Testing'],
  ['data', 'Data'], ['ai_ml', 'AI / ML'], ['mobile', 'Mobile'], ['pm', 'Project Manager'], ['designer', 'Designer'], ['other', 'Khác'],
]
const LEVELS = [
  ['intern', 'Intern'], ['fresher', 'Fresher'], ['junior', 'Junior'], ['middle', 'Middle'], ['senior', 'Senior'], ['lead', 'Lead'], ['manager', 'Manager'],
]
const EMPLOYMENT = [
  ['full_time', 'Toàn thời gian'], ['part_time', 'Bán thời gian'], ['contract', 'Hợp đồng'], ['internship', 'Thực tập'], ['freelance', 'Freelance'],
]

export default function CreateJobPostingPage({ mode }: CreateJobPostingPageProps) {
  const { id: jobId } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [submitting, setSubmitting] = useState(false)
  const [loading, setLoading] = useState(mode === 'edit')
  const [error, setError] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  // Fields
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
  const [languageRequirement, setLanguageRequirement] = useState('')
  const [skillInput, setSkillInput] = useState('')
  const [skills, setSkills] = useState<string[]>([])
  const [rounds, setRounds] = useState<RoundConfig[]>([
    { roundNumber: 1, roundType: 'screening', interviewLanguage: 'vi', interviewCodeTtlHours: 2, maxDurationMinutes: 30 },
  ])

  // JD file + analysis
  const [jdFileUrl, setJdFileUrl] = useState<string | undefined>()
  const [jdFileName, setJdFileName] = useState<string | undefined>()
  const [jdFileFormat, setJdFileFormat] = useState<string | undefined>()
  const [analyzing, setAnalyzing] = useState(false)
  const [analyzeMsg, setAnalyzeMsg] = useState<{ type: 'ok' | 'warn'; text: string } | null>(null)

  useEffect(() => {
    if (mode === 'edit' && jobId) {
      ;(async () => {
        try {
          setLoading(true)
          const job: JobPosting = await jobService.getJobPostingById(jobId)
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
          setLanguageRequirement(job.languageRequirement || '')
          setSkills(job.skills || [])
          setJdFileName(job.jdFileName)
          setJdFileFormat(job.jdFileFormat)
          // Không set jdFileUrl ở edit để tránh ghi đè file cũ trừ khi upload mới
          setRounds(job.roundConfigs?.length ? job.roundConfigs : rounds)
        } catch (err) {
          setError('Không thể tải dữ liệu tin tuyển dụng.')
        } finally {
          setLoading(false)
        }
      })()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode, jobId])

  const handleAnalyze = async (file: File) => {
    setAnalyzing(true)
    setAnalyzeMsg(null)
    setError(null)
    try {
      const r = await jobService.analyzeJd(file)
      setJdFileUrl(r.jdFileUrl)
      setJdFileName(r.jdFileName)
      setJdFileFormat(r.jdFileFormat)
      // Auto-fill (chỉ điền khi có giá trị; người dùng vẫn sửa lại được)
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
      setAnalyzeMsg(
        r.isValidJd
          ? { type: 'ok', text: 'Đã phân tích JD và điền tự động các trường. Bạn có thể chỉnh sửa lại.' }
          : { type: 'warn', text: 'File đã được lưu nhưng AI chưa nhận diện rõ là JD. Vui lòng kiểm tra và nhập thủ công.' },
      )
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Không thể phân tích file JD.')
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
    setRounds([...rounds, { roundNumber: rounds.length + 1, roundType: 'technical', interviewLanguage: 'vi', interviewCodeTtlHours: 2, maxDurationMinutes: 45 }])
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
      setError('Tiêu đề và Mô tả công việc là bắt buộc.')
      return
    }
    if (mode === 'create' && !jdFileUrl) {
      setError('Vui lòng upload và phân tích file JD (PDF/DOCX) trước khi tạo tin.')
      return
    }
    if (interviewMode !== 'remote' && !location.trim()) {
      setError('Địa điểm làm việc là bắt buộc khi chế độ phỏng vấn không phải Remote.')
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
        roundConfigs: rounds,
        languageRequirement: languageRequirement.trim() || undefined,
      }

      let saved: JobPosting
      if (mode === 'edit' && jobId) saved = await jobService.updateJob(jobId, payload)
      else saved = await jobService.createJobPosting(payload)
      navigate(`/recruiter/my-jobs/${saved.id}`)
    } catch (err: any) {
      setError(err?.response?.data?.message || err.message || 'Có lỗi xảy ra khi lưu tin tuyển dụng.')
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) {
    return (
      <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3">
        <Loader2 className="h-10 w-10 animate-spin text-brand-600 dark:text-brand-400" />
        <p className="text-sm text-ink-500 dark:text-ink-400">Đang tải dữ liệu tin tuyển dụng...</p>
      </div>
    )
  }

  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="mb-6">
        <button onClick={() => navigate(-1)} className="mb-3 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white">
          <ArrowLeft className="h-4 w-4" /> Quay lại
        </button>
        <h1 className="text-2xl font-bold text-ink-900 dark:text-white">
          {mode === 'edit' ? 'Chỉnh sửa tin tuyển dụng' : 'Tạo tin tuyển dụng mới'}
        </h1>
        <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
          Upload JD để AI tự điền các trường · tin sẽ được HR Leader duyệt trước khi đăng
        </p>
      </motion.div>

      {error && (
        <div className="mb-6 flex max-w-5xl items-start gap-2 rounded-xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-400">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {/* JD upload + analyze */}
      <div className="mb-6 max-w-5xl rounded-2xl border border-brand-200 dark:border-brand-500/30 bg-gradient-to-b from-brand-50/60 dark:from-brand-500/10 to-white dark:to-white/5 p-6 shadow-card">
        <div className="flex items-center gap-2 text-sm font-semibold text-brand-700 dark:text-brand-300">
          <Sparkles className="h-4 w-4" /> Tài liệu JD (PDF/DOCX){mode === 'create' ? ' — bắt buộc' : ''}
        </div>
        <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">
          Upload file JD → AI phân tích và tự động điền tiêu đề, kỹ năng, cấp bậc, mô tả... Bạn vẫn chỉnh sửa lại được bên dưới.
        </p>

        <input ref={fileRef} type="file" accept=".pdf,.docx" onChange={onPickFile} className="hidden" />

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => fileRef.current?.click()}
            disabled={analyzing}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-60"
          >
            {analyzing ? <Loader2 className="h-4 w-4 animate-spin" /> : <UploadCloud className="h-4 w-4" />}
            {analyzing ? 'Đang phân tích JD...' : jdFileName ? 'Tải JD khác' : 'Upload & phân tích JD'}
          </button>

          {jdFileName && (
            <span className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-700 dark:text-ink-200">
              <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" />
              <span className="max-w-[200px] truncate">{jdFileName}</span>
              {jdFileFormat && <span className="text-xs uppercase text-ink-400">{jdFileFormat}</span>}
              <button
                type="button"
                onClick={() => { setJdFileName(undefined); setJdFileFormat(undefined); setJdFileUrl(undefined); setAnalyzeMsg(null); if (fileRef.current) fileRef.current.value = '' }}
                className="text-ink-400 hover:text-red-500"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </span>
          )}
        </div>

        {analyzeMsg && (
          <div className={`mt-3 flex items-center gap-2 rounded-lg px-3 py-2 text-xs ${analyzeMsg.type === 'ok' ? 'bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-400' : 'bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-400'}`}>
            {analyzeMsg.type === 'ok' ? <Check className="h-3.5 w-3.5" /> : <AlertCircle className="h-3.5 w-3.5" />} {analyzeMsg.text}
          </div>
        )}
      </div>

      <form onSubmit={handleSubmit} className="grid max-w-5xl gap-6 lg:grid-cols-3">
        {/* Left */}
        <div className="space-y-6 lg:col-span-2">
          <div className={`${card} space-y-5`}>
            <h2 className="border-b border-ink-100 dark:border-white/10 pb-2 text-base font-semibold text-ink-900 dark:text-white">Thông tin chung</h2>

            <div>
              <label className={label}>Tiêu đề công việc *</label>
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="VD: Senior Backend Developer (.NET)" className={input} required />
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <div>
                <label className={label}>Phòng ban</label>
                <input value={department} onChange={(e) => setDepartment(e.target.value)} placeholder="Engineering" className={input} />
              </div>
              <div>
                <label className={label}>Lĩnh vực</label>
                <select value={jobCategory} onChange={(e) => setJobCategory(e.target.value)} className={input}>
                  <option value="">— Chọn —</option>
                  {CATEGORIES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </select>
              </div>
              <div>
                <label className={label}>Cấp bậc</label>
                <select value={experienceLevel} onChange={(e) => setExperienceLevel(e.target.value)} className={input}>
                  {LEVELS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </select>
              </div>
            </div>

            <div>
              <label className={label}>Mô tả công việc *</label>
              <div className="quill-editor-wrapper">
                <ReactQuill
                  theme="snow"
                  value={jobDescription}
                  onChange={setJobDescription}
                  placeholder="Mô tả nhiệm vụ, trách nhiệm, yêu cầu..."
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
              <label className={label}>Kỹ năng yêu cầu (Enter để thêm)</label>
              <input value={skillInput} onChange={(e) => setSkillInput(e.target.value)} onKeyDown={handleAddSkill} placeholder="VD: C#, .NET, PostgreSQL..." className={input} />
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
            <h2 className="border-b border-ink-100 dark:border-white/10 pb-2 text-base font-semibold text-ink-900 dark:text-white">Đãi ngộ & Địa điểm</h2>

            <div className="grid gap-4 sm:grid-cols-3">
              <div>
                <label className={label}>Hình thức</label>
                <select value={employmentType} onChange={(e) => setEmploymentType(e.target.value)} className={input}>
                  {EMPLOYMENT.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </select>
              </div>
              <div>
                <label className={label}>Nơi làm việc</label>
                <select value={workMode} onChange={(e) => setWorkMode(e.target.value)} className={input}>
                  <option value="onsite">Onsite</option>
                  <option value="hybrid">Hybrid</option>
                  <option value="remote">Remote</option>
                </select>
              </div>
              <div>
                <label className={label}>Chế độ phỏng vấn *</label>
                <select value={interviewMode} onChange={(e) => setInterviewMode(e.target.value as any)} className={input}>
                  <option value="remote">Remote</option>
                  <option value="onsite">On-site</option>
                  <option value="both">Hybrid</option>
                </select>
              </div>
            </div>

            {interviewMode !== 'remote' && (
              <div>
                <label className={label}>Địa điểm làm việc *</label>
                <input value={location} onChange={(e) => setLocation(e.target.value)} placeholder="VD: Tòa nhà FPT, Quận 9, TP.HCM" className={input} required />
              </div>
            )}

            <div>
              <label className={label}>Số lượng cần tuyển</label>
              <input
                type="number"
                min={1}
                value={vacancies}
                onChange={(e) => setVacancies(e.target.value === '' ? '' : Number(e.target.value))}
                placeholder="Để trống = không giới hạn"
                className={input}
              />
              <p className="mt-1 text-xs text-ink-400">Chỉ tiêu tuyển dụng — khi tuyển đủ có thể đóng tin.</p>
            </div>

            <div>
              <div className="mb-1.5 flex items-center justify-between">
                <label className={label + ' mb-0'}>Mức lương</label>
                <label className="flex cursor-pointer items-center gap-2 text-xs text-ink-500 dark:text-ink-400">
                  <input type="checkbox" checked={salaryIsNegotiable} onChange={(e) => setSalaryIsNegotiable(e.target.checked)} className="accent-brand-600" />
                  Thỏa thuận
                </label>
              </div>
              {!salaryIsNegotiable && (
                <div className="grid grid-cols-3 gap-2">
                  <input type="number" value={salaryMin} onChange={(e) => setSalaryMin(e.target.value === '' ? '' : Number(e.target.value))} placeholder="Tối thiểu" className={input} />
                  <input type="number" value={salaryMax} onChange={(e) => setSalaryMax(e.target.value === '' ? '' : Number(e.target.value))} placeholder="Tối đa" className={input} />
                  <select value={salaryCurrency} onChange={(e) => setSalaryCurrency(e.target.value)} className={input}>
                    <option value="VND">VND</option>
                    <option value="USD">USD</option>
                  </select>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Right */}
        <div className="space-y-6">
          <div className={`${card} space-y-4`}>
            <div className="flex items-center justify-between border-b border-ink-100 dark:border-white/10 pb-2">
              <h2 className="text-base font-semibold text-ink-900 dark:text-white">Vòng phỏng vấn AI</h2>
              <button type="button" onClick={addRound} className="flex items-center gap-1 text-xs font-medium text-brand-600 dark:text-brand-400 hover:underline">
                <PlusCircle className="h-3.5 w-3.5" /> Thêm vòng
              </button>
            </div>
            <div className="max-h-[320px] space-y-3 overflow-y-auto pr-1">
              <AnimatePresence initial={false}>
                {rounds.map((round, idx) => (
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
                    <div className="text-sm font-semibold text-ink-900 dark:text-white">Vòng {round.roundNumber}</div>
                    <div>
                      <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">Loại vòng</label>
                      <select value={round.roundType} onChange={(e) => changeRound(idx, 'roundType', e.target.value)} className={`${input} py-2`}>
                        <option value="screening">Screening / Sơ loại</option>
                        <option value="technical">Technical / Chuyên môn</option>
                      </select>
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <div>
                        <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">Ngôn ngữ</label>
                        <select value={round.interviewLanguage} onChange={(e) => changeRound(idx, 'interviewLanguage', e.target.value)} className={`${input} py-2`}>
                          <option value="vi">Tiếng Việt</option>
                          <option value="en">Tiếng Anh</option>
                        </select>
                      </div>
                      <div>
                        <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">Phút</label>
                        <input type="number" value={round.maxDurationMinutes} onChange={(e) => changeRound(idx, 'maxDurationMinutes', Number(e.target.value))} className={`${input} py-2`} />
                      </div>
                    </div>
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          </div>

          <div className={`${card} space-y-4`}>
            <h2 className="border-b border-ink-100 dark:border-white/10 pb-2 text-base font-semibold text-ink-900 dark:text-white">Hiển thị</h2>
            <div>
              <label className={label}>Yêu cầu ngôn ngữ</label>
              <input value={languageRequirement} onChange={(e) => setLanguageRequirement(e.target.value)} placeholder="VD: Tiếng Anh (IELTS 6.5)" className={input} />
            </div>
            <label className="flex cursor-pointer items-center gap-3 text-sm text-ink-700 dark:text-ink-200">
              <input type="checkbox" checked={isPublicListing} onChange={(e) => setIsPublicListing(e.target.checked)} className="accent-brand-600" />
              Công khai trên Job Board
            </label>
            <label className="flex cursor-pointer items-center gap-3 text-sm text-ink-700 dark:text-ink-200">
              <input type="checkbox" checked={isUrgent} onChange={(e) => setIsUrgent(e.target.checked)} className="accent-brand-600" />
              Tuyển gấp
            </label>

            <div className="flex gap-3 pt-2">
              <button type="button" onClick={() => navigate(-1)} disabled={submitting} className="flex-1 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10">
                Hủy
              </button>
              <button type="submit" disabled={submitting} className="flex flex-1 items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50">
                {submitting ? <><Loader2 className="h-4 w-4 animate-spin" /> Đang lưu...</> : <><Check className="h-4 w-4" /> {mode === 'edit' ? 'Lưu' : 'Tạo tin'}</>}
              </button>
            </div>
          </div>
        </div>
      </form>
    </div>
  )
}
