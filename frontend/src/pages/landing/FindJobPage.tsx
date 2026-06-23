import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Search,
  MapPin,
  Briefcase,
  DollarSign,
  Bookmark,
  TrendingUp,
  Loader2,
  Sparkles,
  X,
  ChevronDown,
  Check,
  ChevronLeft,
  ChevronRight,
  SlidersHorizontal,
  Code2,
  Server,
  BrainCircuit,
} from 'lucide-react'
import { useAuthStore } from '@store/auth'
import CandidateHeader from '@components/layout/CandidateHeader'
import jobService from '@/services/job/jobService'
import type { JobFacets } from '@/services/job/jobService'
import { savedJobService } from '@/services/job/savedJobService'
import { provinceService } from '@/services/location/provinceService'
import type { City } from '@/services/location/provinceService'
import { profileService } from '@/services/profile/profileService'
import SearchableSelect from '@/components/ui/SearchableSelect'
import type { JobPosting } from '@/types/job'

// ============== CONSTANTS ==============
const POPULAR_KEYWORDS = ['Frontend', 'Backend', 'C# / .NET', 'AI Engineer']

// Số tin tuyển dụng hiển thị mỗi trang.
const JOBS_PER_PAGE = 8

const EMPTY_FACETS: JobFacets = {
  categories: [],
  employmentTypes: [],
  experienceLevels: [],
  workModes: [],
  locations: [],
  skills: [],
  languages: [],
  totalJobs: 0,
}

// ============== TYPE DEFINITIONS ==============
// Tất cả bộ lọc đều dùng GIÁ TRỊ THÔ (raw value) khớp với dữ liệu trong DB.
interface FiltersState {
  categories: string[]
  employmentTypes: string[]
  experienceLevels: string[]
  workModes: string[]
  locations: string[]
  skills: string[]
  languages: string[]
}

const EMPTY_FILTERS: FiltersState = {
  categories: [],
  employmentTypes: [],
  experienceLevels: [],
  workModes: [],
  locations: [],
  skills: [],
  languages: [],
}

// ============== HELPER FUNCTIONS ==============
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

function formatPostedDate(dateStr: string): string {
  try {
    const diffTime = Math.abs(new Date().getTime() - new Date(dateStr).getTime())
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24))
    if (diffDays <= 1) return 'Hôm nay'
    if (diffDays <= 2) return 'Hôm qua'
    return `${diffDays} ngày trước`
  } catch {
    return 'Gần đây'
  }
}

function getJobIcon(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai') || dept.includes('ml')) {
    return <BrainCircuit className="w-6 h-6" />
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return <Server className="w-6 h-6" />
  }
  return <Code2 className="w-6 h-6" />
}

function getIconColor(department?: string) {
  const dept = department?.toLowerCase() || ''
  if (dept.includes('data') || dept.includes('ai')) {
    return 'bg-ai-50 text-ai-600'
  }
  if (dept.includes('backend') || dept.includes('server')) {
    return 'bg-emerald-50 text-emerald-600'
  }
  return 'bg-brand-50 text-brand-600'
}


// ============== FILTER SIDEBAR COMPONENT ==============
type FilterKey = keyof FiltersState

interface FilterSidebarProps {
  filters: FiltersState
  setFilters: React.Dispatch<React.SetStateAction<FiltersState>>
  onClearAll: () => void
  facets: JobFacets
  /** Địa điểm đã lọc về chỉ thành phố trực thuộc TW (lấy từ Province Open API) + có trong DB. */
  locationFacets: JobFacets['locations']
}

// Số mục tối đa hiển thị trước khi gập bớt một nhóm bộ lọc dài.
const FILTER_ITEM_LIMIT = 6

/** Một nhóm bộ lọc có thể thu gọn/mở rộng (accordion) để sidebar không bị quá dài. */
function CollapsibleSection({
  title,
  selectedCount,
  defaultOpen = true,
  description,
  children,
}: {
  title: string
  selectedCount: number
  defaultOpen?: boolean
  description?: string
  children: React.ReactNode
}) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="py-3 first:pt-0 last:pb-0">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between gap-2 text-left"
      >
        <span className="flex items-center gap-2 font-semibold text-ink-700">
          {title}
          {selectedCount > 0 && (
            <span className="grid h-5 min-w-[20px] place-items-center rounded-full bg-brand-100 px-1.5 text-[11px] font-bold text-brand-700">
              {selectedCount}
            </span>
          )}
        </span>
        <ChevronDown
          className={`h-4 w-4 shrink-0 text-ink-400 transition ${open ? 'rotate-180' : ''}`}
        />
      </button>
      {open && (
        <div className="mt-2.5">
          {description && <p className="mb-2 text-xs text-ink-400">{description}</p>}
          {children}
        </div>
      )}
    </div>
  )
}

/** Danh sách rút gọn: chỉ hiện tối đa `limit` mục, kèm nút "Xem thêm / Thu gọn". */
function TruncatedList<T>({
  items,
  render,
  limit = FILTER_ITEM_LIMIT,
  wrapperClassName,
}: {
  items: T[]
  render: (item: T) => React.ReactNode
  limit?: number
  wrapperClassName?: string
}) {
  const [expanded, setExpanded] = useState(false)
  const shown = expanded ? items : items.slice(0, limit)
  return (
    <>
      <div className={wrapperClassName}>{shown.map(render)}</div>
      {items.length > limit && (
        <button
          type="button"
          onClick={() => setExpanded((e) => !e)}
          className="mt-1.5 text-xs font-medium text-brand-600 hover:underline"
        >
          {expanded ? 'Thu gọn' : `Xem thêm ${items.length - limit}`}
        </button>
      )}
    </>
  )
}

/** Nhóm bộ lọc dạng checkbox (thu gọn được), mỗi dòng hiển thị "Nhãn (số lượng)". */
function FacetCheckboxGroup({
  title,
  items,
  selected,
  onToggle,
  description,
  defaultOpen = true,
}: {
  title: string
  items: JobFacets['categories']
  selected: string[]
  onToggle: (value: string) => void
  description?: string
  defaultOpen?: boolean
}) {
  if (items.length === 0) return null
  return (
    <CollapsibleSection
      title={title}
      selectedCount={selected.length}
      description={description}
      defaultOpen={defaultOpen || selected.length > 0}
    >
      <TruncatedList
        items={items}
        render={(item) => (
          <label
            key={item.value}
            className="flex items-center gap-2.5 py-1 text-ink-600 cursor-pointer"
          >
            <input
              type="checkbox"
              checked={selected.includes(item.value)}
              onChange={() => onToggle(item.value)}
              className="rounded border-ink-300 text-brand-600"
            />
            <span className="flex-1">{item.label}</span>
            <span className="text-xs text-ink-400">({item.count})</span>
          </label>
        )}
      />
    </CollapsibleSection>
  )
}

function FilterSidebar({
  filters,
  setFilters,
  onClearAll,
  facets,
  locationFacets,
}: FilterSidebarProps) {
  const hasActiveFilters = Object.values(filters).some((arr) => arr.length > 0)

  const toggleStringFilter = (key: FilterKey, value: string) => {
    setFilters((prev) => ({
      ...prev,
      [key]: prev[key].includes(value)
        ? prev[key].filter((v) => v !== value)
        : [...prev[key], value],
    }))
  }

  // Chip cho từ "đang chọn" — tra nhãn hiển thị từ facet.
  const labelOf = (items: JobFacets['categories'], value: string) =>
    items.find((i) => i.value === value)?.label ?? value

  const activeChips: { key: FilterKey; value: string; label: string }[] = [
    ...filters.categories.map((v) => ({
      key: 'categories' as const,
      value: v,
      label: labelOf(facets.categories, v),
    })),
    ...filters.experienceLevels.map((v) => ({
      key: 'experienceLevels' as const,
      value: v,
      label: labelOf(facets.experienceLevels, v),
    })),
    ...filters.locations.map((v) => ({
      key: 'locations' as const,
      value: v,
      label: labelOf(locationFacets, v),
    })),
    ...filters.skills.map((v) => ({ key: 'skills' as const, value: v, label: v })),
  ]

  const noFacets =
    facets.categories.length === 0 &&
    facets.employmentTypes.length === 0 &&
    facets.experienceLevels.length === 0 &&
    facets.workModes.length === 0 &&
    locationFacets.length === 0 &&
    facets.skills.length === 0 &&
    facets.languages.length === 0

  return (
    <aside className="space-y-6">
      <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
        <div className="flex items-center justify-between">
          <span className="font-semibold flex items-center gap-2">
            <SlidersHorizontal className="w-4 h-4 text-brand-600" />
            Bộ lọc
          </span>
          {hasActiveFilters && (
            <button
              onClick={onClearAll}
              className="text-xs font-medium text-brand-600 hover:underline"
            >
              Xoá tất cả
            </button>
          )}
        </div>

        {/* Active filters */}
        {activeChips.length > 0 && (
          <div className="mt-4 flex flex-wrap gap-1.5">
            {activeChips.map(({ key, value, label }) => (
              <span
                key={`${key}-${value}`}
                className="inline-flex items-center gap-1 rounded-full bg-brand-50 px-2.5 py-1 text-xs font-medium text-brand-700 cursor-pointer hover:bg-brand-100"
                onClick={() => toggleStringFilter(key, value)}
              >
                {label} <X className="w-3 h-3" />
              </span>
            ))}
          </div>
        )}

        {noFacets ? (
          <p className="mt-5 text-sm text-ink-400">Chưa có dữ liệu để lọc.</p>
        ) : (
          <div className="mt-5 divide-y divide-ink-100 text-sm">
            {/* Lĩnh vực */}
            <FacetCheckboxGroup
              title="Lĩnh vực"
              items={facets.categories}
              selected={filters.categories}
              onToggle={(v) => toggleStringFilter('categories', v)}
            />

            {/* Hình thức */}
            <FacetCheckboxGroup
              title="Hình thức"
              items={facets.employmentTypes}
              selected={filters.employmentTypes}
              onToggle={(v) => toggleStringFilter('employmentTypes', v)}
            />

            {/* Cấp bậc */}
            <FacetCheckboxGroup
              title="Cấp bậc"
              items={facets.experienceLevels}
              selected={filters.experienceLevels}
              onToggle={(v) => toggleStringFilter('experienceLevels', v)}
            />

            {/* Nơi làm việc */}
            {facets.workModes.length > 0 && (
              <CollapsibleSection title="Nơi làm việc" selectedCount={filters.workModes.length}>
                <div className="grid grid-cols-3 gap-1.5">
                  {facets.workModes.map((mode) => (
                    <button
                      key={mode.value}
                      onClick={() => toggleStringFilter('workModes', mode.value)}
                      className={`rounded-lg border px-2 py-1.5 text-xs font-medium transition-colors ${
                        filters.workModes.includes(mode.value)
                          ? 'border-brand-300 bg-brand-50 text-brand-700'
                          : 'border-ink-200 text-ink-600 hover:border-brand-300'
                      }`}
                    >
                      {mode.label} ({mode.count})
                    </button>
                  ))}
                </div>
              </CollapsibleSection>
            )}

            {/* Địa điểm (tỉnh/thành lấy từ Province Open API, chỉ nơi có job) */}
            <FacetCheckboxGroup
              title="Địa điểm"
              items={locationFacets}
              selected={filters.locations}
              onToggle={(v) => toggleStringFilter('locations', v)}
            />

            {/* Kỹ năng / Công nghệ — mặc định thu gọn vì có thể rất nhiều */}
            {facets.skills.length > 0 && (
              <CollapsibleSection
                title="Kỹ năng / Công nghệ"
                selectedCount={filters.skills.length}
                defaultOpen={filters.skills.length > 0}
              >
                <TruncatedList
                  items={facets.skills}
                  wrapperClassName="flex flex-wrap gap-1.5"
                  render={(skill) => (
                    <button
                      key={skill.value}
                      onClick={() => toggleStringFilter('skills', skill.value)}
                      className={`rounded-lg px-2.5 py-1 text-xs font-medium transition-colors ${
                        filters.skills.includes(skill.value)
                          ? 'bg-brand-600 text-white'
                          : 'border border-ink-200 text-ink-600 hover:border-brand-300'
                      }`}
                    >
                      {skill.label} ({skill.count})
                    </button>
                  )}
                />
              </CollapsibleSection>
            )}

            {/* Ngôn ngữ phỏng vấn — mặc định thu gọn */}
            <FacetCheckboxGroup
              title="Ngôn ngữ phỏng vấn"
              description="AI phỏng vấn theo ngôn ngữ của JD"
              items={facets.languages}
              selected={filters.languages}
              onToggle={(v) => toggleStringFilter('languages', v)}
              defaultOpen={false}
            />
          </div>
        )}
      </div>
    </aside>
  )
}

// ============== CV TIP BANNER (đầu danh sách job) ==============
function CvTipBanner({
  isAuthenticated,
  onAction,
}: {
  isAuthenticated: boolean
  onAction: () => void
}) {
  return (
    <div className="mb-5 flex flex-col gap-3 rounded-2xl border border-ai-200 bg-gradient-to-r from-ai-50/80 to-brand-50/70 p-4 shadow-card sm:flex-row sm:items-center sm:justify-between">
      <div className="flex items-start gap-3">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-white text-ai-600 shadow-sm">
          <Sparkles className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <div className="font-display font-bold text-ink-900">Mẹo — Tải CV để xem độ phù hợp</div>
          <p className="text-sm text-ink-500">
            {isAuthenticated
              ? 'Tải CV lên hồ sơ để AI chấm điểm phù hợp CV–JD và gợi ý việc khớp nhất với bạn.'
              : 'Đăng nhập và tải CV lên để AI chấm điểm phù hợp CV–JD và gợi ý việc khớp nhất với bạn.'}
          </p>
        </div>
      </div>
      <button
        onClick={onAction}
        className="shrink-0 whitespace-nowrap rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2 text-sm font-semibold text-white hover:opacity-90"
      >
        {isAuthenticated ? 'Tải CV lên' : 'Đăng nhập'}
      </button>
    </div>
  )
}

// ============== JOB CARD COMPONENT ==============
interface JobCardProps {
  job: JobPosting
  isSaved?: boolean
  onToggleSave?: (jobId: string) => void
}

function JobCard({ job, isSaved = false, onToggleSave }: JobCardProps) {
  const navigate = useNavigate()

  return (
    <article
      className="group rounded-2xl border border-ink-200 bg-white p-5 shadow-card hover:shadow-card-hover hover:border-brand-200 transition cursor-pointer"
      onClick={() => navigate(`/jobs/${job.id}`)}
    >
      <div className="flex gap-4">
        <div
          className={`grid h-12 w-12 shrink-0 place-items-center rounded-xl ${getIconColor(job.department)}`}
        >
          {getJobIcon(job.department)}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h3 className="font-semibold text-ink-900 group-hover:text-brand-700">{job.title}</h3>
              <div className="mt-0.5 flex items-center gap-1.5 text-sm text-ink-500">
                <MapPin className="w-3.5 h-3.5" />
                {job.location || 'Remote'} · {job.department || 'Engineering'}
              </div>
            </div>
            <button
              onClick={(e) => {
                e.stopPropagation()
                onToggleSave?.(job.id)
              }}
              title={isSaved ? 'Bỏ lưu việc làm' : 'Lưu việc làm'}
              className={`shrink-0 p-2 rounded-lg transition-colors ${isSaved ? 'text-ai-600' : 'text-ink-300 hover:text-ai-600'}`}
            >
              <Bookmark className={`w-5 h-5 ${isSaved ? 'fill-current' : ''}`} />
            </button>
          </div>
          <div className="mt-3 flex flex-wrap gap-2 text-xs">
            <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
              <Briefcase className="w-3.5 h-3.5" />
              {formatWorkMode(job.workMode || job.employmentType)}
            </span>
            <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
              <DollarSign className="w-3.5 h-3.5" />
              {formatSalary(job)}
            </span>
            {job.experienceLevel && (
              <span className="inline-flex items-center gap-1 rounded-lg bg-ink-100 px-2 py-1 text-ink-600">
                <TrendingUp className="w-3.5 h-3.5" />
                {job.experienceLevel}
              </span>
            )}
          </div>
          <div className="mt-3 flex items-center justify-between">
            <span className="text-xs text-ink-400">Đăng {formatPostedDate(job.createdAt)}</span>
            <button
              onClick={(e) => {
                e.stopPropagation()
                navigate(`/jobs/${job.id}`)
              }}
              className="rounded-xl border border-brand-200 bg-brand-50 px-4 py-1.5 text-sm font-semibold text-brand-700 hover:bg-brand-100"
            >
              Ứng tuyển
            </button>
          </div>
        </div>
      </div>
    </article>
  )
}

// ============== PAGINATION COMPONENT ==============
/** Tạo dãy số trang có dấu "…" khi quá nhiều trang (luôn giữ trang đầu/cuối + lân cận trang hiện tại). */
function buildPageList(current: number, total: number): (number | 'ellipsis')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
  const pages: (number | 'ellipsis')[] = [1]
  const start = Math.max(2, current - 1)
  const end = Math.min(total - 1, current + 1)
  if (start > 2) pages.push('ellipsis')
  for (let i = start; i <= end; i++) pages.push(i)
  if (end < total - 1) pages.push('ellipsis')
  pages.push(total)
  return pages
}

function Pagination({
  page,
  totalPages,
  onChange,
}: {
  page: number
  totalPages: number
  onChange: (page: number) => void
}) {
  if (totalPages <= 1) return null
  const pages = buildPageList(page, totalPages)

  return (
    <nav className="mt-8 flex items-center justify-center gap-1.5" aria-label="Phân trang">
      <button
        onClick={() => onChange(page - 1)}
        disabled={page === 1}
        aria-label="Trang trước"
        className="grid h-9 w-9 place-items-center rounded-lg border border-ink-200 bg-white text-ink-600 transition hover:border-brand-300 hover:text-brand-700 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-ink-200 disabled:hover:text-ink-600"
      >
        <ChevronLeft className="h-4 w-4" />
      </button>

      {pages.map((p, i) =>
        p === 'ellipsis' ? (
          <span key={`e-${i}`} className="px-1.5 text-sm text-ink-400">
            …
          </span>
        ) : (
          <button
            key={p}
            onClick={() => onChange(p)}
            aria-current={p === page ? 'page' : undefined}
            className={`grid h-9 min-w-[36px] place-items-center rounded-lg px-2 text-sm font-semibold transition ${
              p === page
                ? 'bg-brand-600 text-white'
                : 'border border-ink-200 bg-white text-ink-600 hover:border-brand-300 hover:text-brand-700'
            }`}
          >
            {p}
          </button>
        )
      )}

      <button
        onClick={() => onChange(page + 1)}
        disabled={page === totalPages}
        aria-label="Trang sau"
        className="grid h-9 w-9 place-items-center rounded-lg border border-ink-200 bg-white text-ink-600 transition hover:border-brand-300 hover:text-brand-700 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-ink-200 disabled:hover:text-ink-600"
      >
        <ChevronRight className="h-4 w-4" />
      </button>
    </nav>
  )
}

// ============== MAIN PAGE COMPONENT ==============
export default function FindJob() {
  const navigate = useNavigate()
  const { isAuthenticated } = useAuthStore()
  const [searchQuery, setSearchQuery] = useState('')
  const [jobs, setJobs] = useState<JobPosting[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<FiltersState>(EMPTY_FILTERS)
  const [facets, setFacets] = useState<JobFacets>(EMPTY_FACETS)
  const [cities, setCities] = useState<City[]>([])
  const [profileSkills, setProfileSkills] = useState<string[]>([])
  const [profileHasCv, setProfileHasCv] = useState(false)
  const [savedIds, setSavedIds] = useState<Set<string>>(new Set())
  const locationNames = new Set(cities.map((c) => c.name))
  const [sortBy, setSortBy] = useState<'newest' | 'match' | 'salary'>('newest')
  const [page, setPage] = useState(1)
  const listTopRef = useRef<HTMLDivElement>(null)

  // Load jobs + facets từ API
  useEffect(() => {
    async function loadJobs() {
      try {
        setLoading(true)
        const [data, facetData] = await Promise.all([
          jobService.getPublicJobPostings(),
          jobService.getJobFacets(),
        ])
        setJobs(data)
        setFacets(facetData)
      } catch (err) {
        console.error('Failed to load jobs:', err)
        setError('Không thể kết nối máy chủ để tải danh sách công việc.')
      } finally {
        setLoading(false)
      }
    }
    loadJobs()
  }, [])

  // Danh sách thành phố trực thuộc TW (Province Open API) — dùng để giới hạn bộ lọc Địa điểm.
  useEffect(() => {
    provinceService
      .getLocations()
      .then(setCities)
      .catch((err) => console.error('Failed to load locations:', err))
  }, [])

  // Kỹ năng từ CV/hồ sơ ứng viên — dùng để gợi ý cá nhân hoá. Lỗi (chưa login/không phải
  // ứng viên) → bỏ qua, dùng gợi ý phổ biến.
  useEffect(() => {
    if (!isAuthenticated) {
      setProfileSkills([])
      setProfileHasCv(false)
      setSavedIds(new Set())
      return
    }
    profileService
      .getProfile()
      .then((p) => {
        setProfileSkills(p.skills ?? [])
        setProfileHasCv(!!p.profileCvUrl)
      })
      .catch(() => {
        setProfileSkills([])
        setProfileHasCv(false)
      })
    savedJobService
      .getSavedJobIds()
      .then((ids) => setSavedIds(new Set(ids)))
      .catch(() => setSavedIds(new Set()))
  }, [isAuthenticated])

  // Lưu / bỏ lưu việc làm. Cập nhật lạc quan (optimistic), rollback nếu API lỗi.
  // Chưa đăng nhập → điều hướng tới trang đăng nhập ứng viên.
  const handleToggleSave = (jobId: string) => {
    if (!isAuthenticated) {
      navigate('/auth/candidate-login')
      return
    }
    const wasSaved = savedIds.has(jobId)
    setSavedIds((prev) => {
      const next = new Set(prev)
      if (wasSaved) next.delete(jobId)
      else next.add(jobId)
      return next
    })
    const action = wasSaved ? savedJobService.unsaveJob(jobId) : savedJobService.saveJob(jobId)
    action.catch(() => {
      setSavedIds((prev) => {
        const next = new Set(prev)
        if (wasSaved) next.add(jobId)
        else next.delete(jobId)
        return next
      })
    })
  }

  // Gợi ý cho ứng viên: ưu tiên kỹ năng trong CV, nếu không có thì lấy kỹ năng phổ biến
  // (theo số job thực tế), cuối cùng fallback từ khoá mặc định.
  const suggestions =
    profileSkills.length > 0
      ? profileSkills.slice(0, 6)
      : facets.skills.length > 0
        ? facets.skills.slice(0, 6).map((s) => s.label)
        : POPULAR_KEYWORDS
  const suggestionsArePersonalized = profileSkills.length > 0

  // Bộ lọc Địa điểm ở sidebar = facet location ∩ tỉnh/thành từ Province API (chỉ nơi đang có job).
  const locationFacets = facets.locations.filter((l) => locationNames.has(l.value))

  // Filter jobs theo giá trị thô khớp DB
  const filteredJobs = jobs.filter((job) => {
    const q = searchQuery.trim().toLowerCase()
    const matchesSearch =
      q === '' ||
      job.title.toLowerCase().includes(q) ||
      (job.department || '').toLowerCase().includes(q) ||
      (job.skills || []).some((s) => s.toLowerCase().includes(q))

    const matchesCategory =
      filters.categories.length === 0 ||
      filters.categories.includes((job.jobCategory || '').toLowerCase())

    const matchesEmployment =
      filters.employmentTypes.length === 0 ||
      filters.employmentTypes.includes((job.employmentType || '').toLowerCase())

    const matchesExperience =
      filters.experienceLevels.length === 0 ||
      filters.experienceLevels.includes((job.experienceLevel || '').toLowerCase())

    const matchesWorkMode =
      filters.workModes.length === 0 ||
      filters.workModes.includes((job.workMode || '').toLowerCase())

    const matchesLocation =
      filters.locations.length === 0 || filters.locations.includes(job.location || '')

    const matchesSkills =
      filters.skills.length === 0 ||
      filters.skills.some((skill) => (job.skills || []).includes(skill))

    const matchesLanguage =
      filters.languages.length === 0 ||
      filters.languages.includes((job.detectedLanguage || '').toLowerCase())

    return (
      matchesSearch &&
      matchesCategory &&
      matchesEmployment &&
      matchesExperience &&
      matchesWorkMode &&
      matchesLocation &&
      matchesSkills &&
      matchesLanguage
    )
  })

  // Sort jobs
  const sortedJobs = [...filteredJobs].sort((a, b) => {
    switch (sortBy) {
      case 'newest':
        return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      case 'salary':
        return (b.salaryMax || 0) - (a.salaryMax || 0)
      default:
        return 0
    }
  })

  // Phân trang client-side (toàn bộ job đã tải sẵn, lọc/sắp xếp trong bộ nhớ).
  const totalPages = Math.max(1, Math.ceil(sortedJobs.length / JOBS_PER_PAGE))
  const currentPage = Math.min(page, totalPages)
  const pagedJobs = sortedJobs.slice((currentPage - 1) * JOBS_PER_PAGE, currentPage * JOBS_PER_PAGE)

  // Đổi bộ lọc / tìm kiếm / sắp xếp → quay về trang 1.
  useEffect(() => {
    setPage(1)
  }, [filters, searchQuery, sortBy])

  const goToPage = (next: number) => {
    const clamped = Math.min(Math.max(1, next), totalPages)
    setPage(clamped)
    // Cuộn lên đầu danh sách để người dùng thấy trang mới từ đầu.
    listTopRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  const clearFilters = () => {
    setFilters(EMPTY_FILTERS)
    setSearchQuery('')
  }

  return (
    <div className="min-h-screen bg-ink-50">
      <CandidateHeader />

      {/* Hero + search */}
      <section className="relative border-b border-ink-200 bg-white">
        <div className="absolute inset-0 bg-gradient-to-br from-brand-50 via-white to-ai-50" />
        <div className="relative mx-auto max-w-6xl px-6 py-14">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-ai-50 px-3 py-1 text-xs font-semibold text-ai-700 ring-1 ring-ai-200">
            <Sparkles className="w-3.5 h-3.5" />
            Phỏng vấn AI tự động · Đánh giá khách quan
          </span>
          <h1 className="mt-4 font-display text-4xl sm:text-5xl font-extrabold leading-[1.4] max-w-2xl text-ink-900">
            Tìm công việc IT phù hợp với{' '}
            <span className="inline-block pb-1 bg-gradient-to-r from-brand-600 to-ai-600 bg-clip-text text-transparent">
              bạn nhất
            </span>
          </h1>
          <p className="mt-3 max-w-xl text-ink-500">
            Ứng tuyển trực tiếp, AI phân tích độ phù hợp CV–JD và phỏng vấn bạn qua nhiều vòng —
            minh bạch, không thiên vị.
          </p>

          {/* Search bar */}
          <div className="mt-7 rounded-2xl border border-ink-200 bg-white p-2 shadow-card flex flex-col gap-2 sm:flex-row">
            <div className="flex flex-1 items-center gap-2 rounded-xl px-3 py-2.5 bg-ink-50">
              <Search className="w-5 h-5 text-ink-400" />
              <input
                className="w-full bg-transparent text-sm outline-none placeholder:text-ink-400"
                placeholder="Chức danh, kỹ năng (React, .NET...)"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <div className="hidden sm:block w-px bg-ink-200 my-1.5" />
            <div className="flex-1">
              <SearchableSelect
                options={cities.map((c) => ({ value: c.code, label: c.name }))}
                value={cities.find((c) => c.name === filters.locations[0])?.code ?? null}
                onChange={(code) => {
                  const city = cities.find((c) => c.code === code)
                  setFilters((prev) => ({
                    ...prev,
                    locations: city ? [city.name] : [],
                  }))
                }}
                placeholder="Địa điểm"
                emptyText="Không tìm thấy tỉnh/thành"
                icon={<MapPin className="w-5 h-5 shrink-0 text-ink-400" />}
                onClear={() => setFilters((prev) => ({ ...prev, locations: [] }))}
              />
            </div>
            <button className="rounded-xl bg-brand-600 px-6 py-2.5 text-sm font-semibold text-white hover:bg-brand-700">
              Tìm kiếm
            </button>
          </div>

          {/* Gợi ý cho ứng viên (cá nhân hoá theo CV nếu có, hoặc phổ biến) */}
          <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-ink-500">
            <span className="inline-flex items-center gap-1.5">
              {suggestionsArePersonalized && <Sparkles className="w-3.5 h-3.5 text-ai-600" />}
              Gợi ý cho bạn:
            </span>
            {suggestions.map((keyword) => (
              <button
                key={keyword}
                onClick={() => setSearchQuery(keyword)}
                className="rounded-full bg-white px-3 py-1 ring-1 ring-ink-200 hover:ring-brand-300 hover:text-brand-700"
              >
                {keyword}
              </button>
            ))}
          </div>
        </div>
      </section>

      {/* Body: filters + list */}
      <main className="mx-auto max-w-6xl px-6 py-10 grid gap-8 lg:grid-cols-[260px_1fr]">
        {/* Filters */}
        <FilterSidebar
          filters={filters}
          setFilters={setFilters}
          onClearAll={clearFilters}
          facets={facets}
          locationFacets={locationFacets}
        />

        {/* Job List */}
        <section>
          {/* Banner gợi ý tải CV — chỉ hiện khi ứng viên chưa có CV */}
          {!profileHasCv && (
            <CvTipBanner
              isAuthenticated={isAuthenticated}
              onAction={() =>
                navigate(isAuthenticated ? '/candidate/profile?focus=cv' : '/auth/candidate-login')
              }
            />
          )}

          {/* Section Header */}
          <div ref={listTopRef} className="flex items-center justify-between scroll-mt-24">
            <h2 className="font-display text-lg font-bold text-ink-900">
              {loading ? 'Đang tải...' : `${sortedJobs.length} việc làm phù hợp`}
            </h2>
            <select
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as typeof sortBy)}
              className="rounded-xl border border-ink-200 px-3 py-2 text-sm outline-none focus:border-brand-500 cursor-pointer"
            >
              <option value="newest">Mới nhất</option>
              <option value="match">Phù hợp nhất</option>
              <option value="salary">Lương cao</option>
            </select>
          </div>

          {error && (
            <div className="mt-4 p-4 rounded-xl bg-red-50 border border-red-200 text-red-700 text-sm">
              {error}
            </div>
          )}

          {/* Job List */}
          <div className="mt-5 space-y-4">
            {loading ? (
              <div className="flex flex-col items-center justify-center py-20 gap-3">
                <Loader2 className="w-10 h-10 text-brand-600 animate-spin" />
                <p className="text-sm text-ink-500">Đang tải tin tuyển dụng mới nhất...</p>
              </div>
            ) : sortedJobs.length === 0 ? (
              <div className="rounded-2xl border border-ink-200 bg-white p-12 text-center">
                <p className="text-ink-500">
                  Không tìm thấy tin tuyển dụng nào phù hợp với bộ lọc tìm kiếm.
                </p>
                <button
                  onClick={clearFilters}
                  className="mt-4 px-4 py-2 rounded-lg bg-brand-600 text-sm font-semibold text-white hover:bg-brand-700"
                >
                  Xóa bộ lọc
                </button>
              </div>
            ) : (
              pagedJobs.map((job) => (
                <JobCard
                  key={job.id}
                  job={job}
                  isSaved={savedIds.has(job.id)}
                  onToggleSave={handleToggleSave}
                />
              ))
            )}
          </div>

          {/* Phân trang */}
          {!loading && sortedJobs.length > 0 && (
            <Pagination page={currentPage} totalPages={totalPages} onChange={goToPage} />
          )}
        </section>
      </main>

      {/* Footer */}
      <footer className="border-t border-ink-200 bg-white">
        <div className="mx-auto max-w-6xl px-6 py-8 text-sm text-ink-400 flex items-center justify-between">
          <span>© 2026 ARISP — Nền tảng tuyển dụng & phỏng vấn AI</span>
          <span className="flex items-center gap-1.5">
            <Check className="w-4 h-4" />
            Đánh giá minh bạch
          </span>
        </div>
      </footer>
    </div>
  )
}
