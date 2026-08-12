import { useState, useRef, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Search, Briefcase, User } from 'lucide-react'
import jobService from '@ari/shared/fservices/job'
import { applicationService } from '@ari/shared/fservices/application'

interface GlobalSearchProps {
  /** Xác định nguồn dữ liệu + đường dẫn chi tiết: HR xem toàn bộ, Recruiter chỉ tin/ứng viên của mình. */
  scope: 'hr' | 'recruiter'
  placeholder: string
}

const MAX_PER_GROUP = 5

/**
 * Ô tìm kiếm tổng hợp trên header staff: gõ → dropdown 2 nhóm "Tin tuyển dụng" + "Ứng viên",
 * bấm để mở thẳng chi tiết. Dữ liệu nạp 1 lần khi focus (client-side filter, cache 60s).
 */
export default function GlobalSearch({ scope, placeholder }: GlobalSearchProps) {
  const { t } = useTranslation('modules/shared/nav')
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [focused, setFocused] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)

  // Đóng dropdown khi bấm ra ngoài.
  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [])

  // Chỉ nạp khi đã focus ô tìm (tránh gọi API thừa lúc mới vào trang).
  const { data: jobs = [], isLoading: jobsLoading } = useQuery({
    queryKey: ['global-search-jobs', scope],
    queryFn: () =>
      scope === 'hr' ? jobService.getAdminJobPostings() : jobService.getMyJobPostings(),
    enabled: focused,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  })
  const { data: apps = [], isLoading: appsLoading } = useQuery({
    queryKey: ['global-search-apps', scope],
    queryFn: () => applicationService.getApplications(scope === 'recruiter'),
    enabled: focused,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  })

  const q = query.trim().toLowerCase()

  const jobMatches = useMemo(() => {
    if (!q) return []
    return jobs
      .filter(
        (j) =>
          (j.title || '').toLowerCase().includes(q) ||
          (j.department || '').toLowerCase().includes(q),
      )
      .slice(0, MAX_PER_GROUP)
  }, [jobs, q])

  // Gộp ứng viên theo email để không lặp (1 người có thể nhiều hồ sơ), lấy hồ sơ xuất hiện trước.
  const candidateMatches = useMemo(() => {
    if (!q) return []
    const seen = new Set<string>()
    const out: typeof apps = []
    for (const a of apps) {
      const hit =
        (a.candidateName || '').toLowerCase().includes(q) ||
        (a.candidateEmail || '').toLowerCase().includes(q) ||
        (a.jobTitle || '').toLowerCase().includes(q)
      if (!hit) continue
      const key = (a.candidateEmail || a.id).toLowerCase()
      if (seen.has(key)) continue
      seen.add(key)
      out.push(a)
      if (out.length >= MAX_PER_GROUP) break
    }
    return out
  }, [apps, q])

  const loading = jobsLoading || appsLoading
  const hasResults = jobMatches.length > 0 || candidateMatches.length > 0

  const goJob = (id: string) => {
    setOpen(false)
    setQuery('')
    navigate(scope === 'hr' ? `/hr/jobs/${id}` : `/recruiter/my-jobs/${id}`)
  }
  const goCandidate = (id: string) => {
    setOpen(false)
    setQuery('')
    navigate(scope === 'hr' ? `/hr/candidates/${id}` : `/recruiter/candidates/${id}`)
  }

  // Enter: mở kết quả đầu tiên (ưu tiên tin tuyển dụng, sau đó ứng viên).
  const onEnter = () => {
    if (jobMatches[0]) goJob(jobMatches[0].id)
    else if (candidateMatches[0]) goCandidate(candidateMatches[0].id)
  }

  return (
    <div ref={rootRef} className="relative hidden max-w-md flex-1 sm:block">
      <div className="flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 px-3 py-2 focus-within:border-brand-400">
        <Search className="h-4 w-4 text-ink-400" />
        <input
          className="w-full bg-transparent text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400"
          placeholder={placeholder}
          value={query}
          onFocus={() => {
            setFocused(true)
            setOpen(true)
          }}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              onEnter()
            } else if (e.key === 'Escape') {
              setOpen(false)
            }
          }}
        />
        <kbd className="hidden rounded border border-ink-200 dark:border-white/10 bg-white dark:bg-white/10 px-1.5 text-[10px] font-semibold text-ink-400 sm:inline">
          ⏎
        </kbd>
      </div>

      {open && q.length > 0 && (
        <div className="absolute left-0 right-0 top-full z-50 mt-2 max-h-[70vh] overflow-y-auto rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 shadow-xl">
          {loading && !hasResults ? (
            <div className="px-4 py-6 text-center text-sm text-ink-400">{t('search.loading')}</div>
          ) : !hasResults ? (
            <div className="px-4 py-6 text-center text-sm text-ink-400">{t('search.noResults')}</div>
          ) : (
            <>
              {jobMatches.length > 0 && (
                <div className="py-1.5">
                  <div className="px-4 py-1 text-[11px] font-semibold uppercase tracking-wide text-ink-400">
                    {t('search.jobs')}
                  </div>
                  {jobMatches.map((j) => (
                    <button
                      key={j.id}
                      type="button"
                      onClick={() => goJob(j.id)}
                      className="flex w-full items-center gap-3 px-4 py-2 text-left hover:bg-ink-50 dark:hover:bg-white/5"
                    >
                      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-brand-50 text-brand-600 dark:bg-brand-500/20 dark:text-brand-400">
                        <Briefcase className="h-4 w-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-ink-900 dark:text-white">
                          {j.title}
                        </span>
                        {j.department && (
                          <span className="block truncate text-xs text-ink-400">{j.department}</span>
                        )}
                      </span>
                    </button>
                  ))}
                </div>
              )}
              {candidateMatches.length > 0 && (
                <div className="border-t border-ink-100 py-1.5 dark:border-white/10">
                  <div className="px-4 py-1 text-[11px] font-semibold uppercase tracking-wide text-ink-400">
                    {t('search.candidates')}
                  </div>
                  {candidateMatches.map((a) => (
                    <button
                      key={a.id}
                      type="button"
                      onClick={() => goCandidate(a.id)}
                      className="flex w-full items-center gap-3 px-4 py-2 text-left hover:bg-ink-50 dark:hover:bg-white/5"
                    >
                      <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-emerald-50 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
                        <User className="h-4 w-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-ink-900 dark:text-white">
                          {a.candidateName || a.candidateEmail}
                        </span>
                        <span className="block truncate text-xs text-ink-400">
                          {a.jobTitle || a.candidateEmail}
                        </span>
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  )
}
