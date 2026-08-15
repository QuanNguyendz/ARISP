import { useEffect, useMemo, useState, Fragment } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Search, Mail, Eye, ChevronRight, ChevronDown, Calendar, Briefcase, X, Phone, FileText, User, UserCheck, Lock, GraduationCap, Award, Globe, Link2 } from 'lucide-react'
import {
  PageHeader,
  StatsGrid,
  EmptyState,
  ErrorAlert,
  NoticeAlert,
  Pagination,
} from '@ari/shared/ui'
import { HrStatsSkeleton, CandidatesTableSkeleton } from './_skeletons'
import { applicationService } from '@ari/shared/fservices/application'
import type { HrApplicationItem } from '@ari/shared/types/application'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'

type Group = 'pending' | 'interviewing' | 'passed' | 'rejected' | 'other'

interface StatusMeta {
  label: string
  group: Group
  badge: string
}

function statusMeta(status: string, t: (key: string) => string): StatusMeta {
  const s = (status || '').toLowerCase()
  const map: Record<string, StatusMeta> = {
    invited: {
      label: t('status.invited'),
      group: 'pending',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    },
    cv_submitted: {
      label: t('status.cvSubmitted'),
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    pending: {
      label: t('status.pending'),
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    pending_review: {
      label: t('status.pending'),
      group: 'pending',
      badge: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
    },
    screening: {
      label: t('status.screening'),
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    interview: {
      label: t('status.screening'),
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    interview_code_generated: {
      label: t('status.codeGenerated'),
      group: 'interviewing',
      badge: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
    },
    interview_code_used: {
      label: t('status.screening'),
      group: 'interviewing',
      badge: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
    },
    practice: {
      label: t('status.practice'),
      group: 'interviewing',
      badge: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
    },
    pass: {
      label: t('status.pass'),
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    approved: {
      label: t('status.pass'),
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    completed: {
      label: t('status.completed'),
      group: 'passed',
      badge: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
    },
    not_pass: {
      label: t('status.notPass'),
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    rejected: {
      label: t('status.notPass'),
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    failed: {
      label: t('status.notPass'),
      group: 'rejected',
      badge: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
    },
    withdrawn: {
      label: t('status.withdrawn'),
      group: 'rejected',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400',
    },
  }
  return (
    map[s] ?? {
      label: status || '—',
      group: 'other',
      badge: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300',
    }
  )
}

function initials(name: string): string {
  return (name || '?')
    .trim()
    .split(/\s+/)
    .map((n) => n[0])
    .slice(-2)
    .join('')
    .toUpperCase()
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime())
    ? '—'
    : d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function scoreColor(score: number): string {
  if (score >= 80) return 'text-emerald-600 dark:text-emerald-400'
  if (score >= 60) return 'text-amber-600 dark:text-amber-400'
  return 'text-red-600 dark:text-red-400'
}

export default function CandidatesPage() {
  const { t } = useTranslation('modules/hr/candidates')
  const navigate = useNavigate()
  const { openDocument } = useDocumentViewer()
  const [apps, setApps] = useState<HrApplicationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [selectedProfileApp, setSelectedProfileApp] = useState<HrApplicationItem | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()

  const page = Number(searchParams.get('page')) || 1
  const search = searchParams.get('search') || ''
  const filter = (searchParams.get('filter') as Group | 'all') || 'all'
  const dateStart = searchParams.get('dateStart') || ''
  const dateEnd = searchParams.get('dateEnd') || ''

  const updateParam = (key: string, value: string) => {
    setSearchParams((prev) => {
      const p = new URLSearchParams(prev)
      if (!value || value === 'all') {
        p.delete(key)
      } else {
        p.set(key, value)
      }
      p.delete('page')
      return p
    }, { replace: true })
  }

  const handlePageChange = (newPage: number) => {
    setSearchParams((prev) => {
      const p = new URLSearchParams(prev)
      if (newPage > 1) p.set('page', String(newPage))
      else p.delete('page')
      return p
    }, { replace: true })
  }

  const filters: { key: 'all' | Group; label: string }[] = [
    { key: 'all', label: t('filters.all') },
    { key: 'pending', label: t('filters.pending') },
    { key: 'interviewing', label: t('filters.interviewing') },
    { key: 'passed', label: t('filters.passed') },
    { key: 'rejected', label: t('filters.rejected') },
  ]

  useEffect(() => {
    let active = true
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        const data = await applicationService.getApplications()
        if (active) setApps(data)
      } catch (err) {
        if (active) setError(err instanceof Error ? err.message : t('loadingError'))
      } finally {
        if (active) setLoading(false)
      }
    })()
    return () => {
      active = false
    }
  }, [])

  const stats = useMemo(() => {
    const allEmails = new Set(apps.map((a) => a.candidateEmail).filter(Boolean))
    const byGroup = (g: Group) => {
      const uniqueEmails = new Set(
        apps.filter((a) => statusMeta(a.status, t).group === g).map((a) => a.candidateEmail).filter(Boolean)
      )
      return uniqueEmails.size
    }
    return [
      { label: t('stats.total'), value: allEmails.size, color: 'text-blue-600 dark:text-blue-400' },
      {
        label: t('filters.pending'),
        value: byGroup('pending'),
        color: 'text-amber-600 dark:text-amber-400',
      },
      {
        label: t('filters.interviewing'),
        value: byGroup('interviewing'),
        color: 'text-violet-600 dark:text-violet-400',
      },
      {
        label: t('filters.passed'),
        value: byGroup('passed'),
        color: 'text-emerald-600 dark:text-emerald-400',
      },
    ]
  }, [apps, t])

  const [expandedEmails, setExpandedEmails] = useState<Set<string>>(new Set())

  const toggleExpand = (email: string) => {
    setExpandedEmails((prev) => {
      const next = new Set(prev)
      if (next.has(email)) next.delete(email)
      else next.add(email)
      return next
    })
  }

  const groupedCandidates = useMemo(() => {
    const q = search.trim().toLowerCase()
    
    const groups = new Map<string, { email: string; name: string; phone: string; cvFileUrl: string; applications: HrApplicationItem[]; latestDate: string }>()
    
    apps.forEach(app => {
      const email = app.candidateEmail || 'unknown'
      if (!groups.has(email)) {
        groups.set(email, {
          email,
          name: app.candidateName || t('table.anonymous'),
          phone: app.candidatePhone || '',
          cvFileUrl: app.cvFileUrl || '',
          applications: [],
          latestDate: app.createdAt
        })
      }
      const group = groups.get(email)!
      if (!group.phone && app.candidatePhone) group.phone = app.candidatePhone
      if (!group.cvFileUrl && app.cvFileUrl) group.cvFileUrl = app.cvFileUrl
      group.applications.push(app)
      if (new Date(app.createdAt) > new Date(group.latestDate)) {
        group.latestDate = app.createdAt
      }
    })
    
    let arr = Array.from(groups.values())
    
    arr = arr.filter(group => {
      const matchingApps = group.applications.filter((a) => {
        const appDate = a.createdAt ? a.createdAt.slice(0, 10) : ''
        const matchesStartDate = !dateStart || appDate >= dateStart
        const matchesEndDate = !dateEnd || appDate <= dateEnd
        const matchesFilter = filter === 'all' || statusMeta(a.status, t).group === filter
        return matchesStartDate && matchesEndDate && matchesFilter
      })

      const matchesSearch =
        !q ||
        group.name.toLowerCase().includes(q) ||
        group.email.toLowerCase().includes(q) ||
        group.applications.some((a) => a.jobTitle?.toLowerCase().includes(q))

      return matchingApps.length > 0 && matchesSearch
    })
    
    return arr.sort((a, b) => new Date(b.latestDate).getTime() - new Date(a.latestDate).getTime())
  }, [apps, search, filter, dateStart, dateEnd, t])

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(groupedCandidates.length / PAGE_SIZE))
  const pagedGroups = useMemo(
    () => groupedCandidates.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [groupedCandidates, page]
  )


  return (
    <div className="min-h-screen bg-ink-50 dark:bg-ink-950 p-4 sm:p-6 lg:p-8">
      <PageHeader title={t('title')} description={t('subtitle')} />

      {notice && <NoticeAlert message={notice} onDismiss={() => setNotice('')} />}
      {!loading && error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {loading && <HrStatsSkeleton />}
      {!loading && !error && <StatsGrid stats={stats} />}

      {!loading && !error && apps.length > 0 && (
        <div className="p-4 rounded-2xl bg-white dark:bg-white/5 border border-ink-200 dark:border-white/10 mb-6">
          <div className="flex flex-col lg:flex-row gap-4 lg:items-center">
            <div className="flex-1 relative">
              <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-ink-400" />
              <input
                type="text"
                value={search}
                onChange={(e) => updateParam('search', e.target.value)}
                placeholder={t('searchPlaceholder')}
                className="w-full pl-12 pr-4 py-2.5 rounded-xl bg-ink-50 dark:bg-white/5 border border-ink-200 dark:border-white/10 text-ink-900 dark:text-white placeholder:text-ink-400 focus:outline-none focus:border-brand-400 transition-colors text-sm"
              />
            </div>

            {/* Lọc theo ngày nộp */}
            <div className="flex items-center gap-2 bg-ink-50 dark:bg-white/5 border border-ink-200 dark:border-white/10 px-3 py-2 rounded-xl text-sm text-ink-600 dark:text-ink-300">
              <Calendar className="w-4 h-4 text-brand-500 shrink-0" />
              <span className="text-xs font-medium text-ink-400 shrink-0">Từ:</span>
              <input
                type="date"
                value={dateStart}
                onChange={(e) => updateParam('dateStart', e.target.value)}
                className="bg-transparent text-ink-900 dark:text-white focus:outline-none text-xs"
              />
              <span className="text-xs font-medium text-ink-400 shrink-0">Đến:</span>
              <input
                type="date"
                value={dateEnd}
                onChange={(e) => updateParam('dateEnd', e.target.value)}
                className="bg-transparent text-ink-900 dark:text-white focus:outline-none text-xs"
              />
              {(dateStart || dateEnd) && (
                <button
                  onClick={() => { updateParam('dateStart', ''); updateParam('dateEnd', ''); }}
                  className="p-1 hover:bg-ink-200 dark:hover:bg-white/10 rounded-md transition-colors"
                  title="Xóa lọc ngày"
                >
                  <X className="w-3.5 h-3.5 text-ink-400" />
                </button>
              )}
            </div>

            <div className="flex flex-wrap gap-2">
              {filters.map((f) => {
                const activeTab = filter === f.key
                return (
                  <button
                    key={f.key}
                    onClick={() => updateParam('filter', f.key)}
                    className={`px-3.5 py-2 rounded-xl text-sm font-medium transition-all ${activeTab ? 'bg-gradient-to-r from-brand-600 to-ai-600 text-white' : 'border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-100 dark:hover:bg-white/10'}`}
                  >
                    {f.label}
                  </button>
                )
              })}
            </div>
          </div>
        </div>
      )}

      {loading && <CandidatesTableSkeleton rows={6} />}

      {!loading && !error && groupedCandidates.length === 0 && (
        <EmptyState
          icon={<Search className="w-8 h-8 text-ink-400" />}
          title={apps.length === 0 ? t('noCandidates') : t('noResults')}
          description={apps.length === 0 ? t('noCandidatesHint') : t('noResultsHint')}
        />
      )}

      {!loading && !error && groupedCandidates.length > 0 && (
        <div className="p-2 sm:p-4 rounded-2xl bg-white dark:bg-white/5 border border-ink-200 dark:border-white/10">
          <div className="overflow-x-auto">
            <div className="min-w-[640px]">
            <table className="w-full">
              <thead>
                <tr className="border-b border-ink-200 dark:border-white/10">
                  <th className="w-12 text-center py-3 px-2 text-sm font-medium text-ink-600 dark:text-ink-400"></th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-ink-600 dark:text-ink-400">
                    {t('table.candidate')}
                  </th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-ink-600 dark:text-ink-400">
                    Số công việc đã nộp
                  </th>
                  <th className="text-left py-3 px-4 text-sm font-medium text-ink-600 dark:text-ink-400">
                    Lần nộp gần nhất
                  </th>
                  <th className="text-right py-3 px-4 text-sm font-medium text-ink-600 dark:text-ink-400">
                    {t('table.actions')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {pagedGroups.map((group, index) => {
                  const isExpanded = expandedEmails.has(group.email)

                  const matchingApps = group.applications.filter((app) => {
                    const appDate = app.createdAt ? app.createdAt.slice(0, 10) : ''
                    const matchesStartDate = !dateStart || appDate >= dateStart
                    const matchesEndDate = !dateEnd || appDate <= dateEnd
                    const matchesFilter = filter === 'all' || statusMeta(app.status, t).group === filter
                    return matchesStartDate && matchesEndDate && matchesFilter
                  })

                  const hasFilterActive = filter !== 'all' || Boolean(dateStart) || Boolean(dateEnd)

                  return (
                    <Fragment key={group.email}>
                      <motion.tr
                        initial={{ opacity: 0, y: 10 }}
                        animate={{ opacity: 1, y: 0 }}
                        transition={{ delay: Math.min(index * 0.03, 0.3) }}
                        onClick={() => toggleExpand(group.email)}
                        className="border-b border-ink-100 dark:border-white/5 hover:bg-ink-50 dark:hover:bg-white/[0.02] cursor-pointer transition-colors"
                      >
                        <td className="py-4 px-2 text-center text-ink-400">
                          {isExpanded ? <ChevronDown className="w-5 h-5 mx-auto text-brand-500" /> : <ChevronRight className="w-5 h-5 mx-auto" />}
                        </td>
                        <td className="py-4 px-4">
                          <div className="flex items-center gap-3">
                            <div className="w-10 h-10 rounded-full bg-gradient-to-br from-brand-500 to-ai-500 flex items-center justify-center text-sm font-medium text-white shrink-0">
                              {initials(group.name)}
                            </div>
                            <div className="min-w-0">
                              <p className="font-medium text-ink-900 dark:text-white truncate">
                                {group.name}
                              </p>
                              <div className="flex flex-wrap items-center gap-x-2 text-xs text-ink-600 dark:text-ink-400 truncate">
                                <span>{group.email}</span>
                                {group.phone && <span className="text-brand-600 dark:text-brand-400 font-medium">• SĐT: {group.phone}</span>}
                              </div>
                            </div>
                          </div>
                        </td>
                        <td className="py-4 px-4">
                          <span className="inline-flex items-center justify-center bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 px-2.5 py-1 rounded-lg text-sm font-semibold">
                            {hasFilterActive
                              ? `${matchingApps.length} / ${group.applications.length} vị trí`
                              : `${group.applications.length} vị trí`}
                          </span>
                        </td>
                        <td className="py-4 px-4 text-ink-600 dark:text-ink-400">
                          {formatDate(group.latestDate)}
                        </td>
                        <td className="py-4 px-4">
                          <div className="flex items-center justify-end gap-2">
                            <button
                              onClick={(e) => {
                                e.stopPropagation()
                                toggleExpand(group.email)
                              }}
                              className="px-3 py-1.5 rounded-lg border border-ink-200 dark:border-white/10 hover:bg-ink-100 dark:hover:bg-white/10 text-sm font-medium transition-colors"
                            >
                              {isExpanded ? 'Đóng' : 'Mở rộng'}
                            </button>
                          </div>
                        </td>
                      </motion.tr>

                      <AnimatePresence>
                        {isExpanded && (
                          <motion.tr
                            key={`${group.email}-detail`}
                            initial={{ opacity: 0, height: 0 }}
                            animate={{ opacity: 1, height: 'auto' }}
                            exit={{ opacity: 0, height: 0 }}
                            className="bg-ink-50/60 dark:bg-white/[0.02]"
                          >
                            <td colSpan={5} className="py-4 px-4 sm:px-6 border-b border-ink-200/60 dark:border-white/10">
                              <div className="space-y-4">
                                {/* Thẻ thông tin cá nhân ứng viên */}
                                <div className="rounded-xl border border-ink-200/80 dark:border-white/10 bg-white dark:bg-ink-900 p-4 shadow-sm">
                                  <div className="text-xs font-semibold text-ink-500 dark:text-ink-400 mb-3 flex items-center gap-2">
                                    <User className="w-4 h-4 text-brand-500" />
                                    <span>THÔNG TIN CÁ NHÂN ỨNG VIÊN</span>
                                  </div>
                                  <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3 text-xs">
                                    <div className="p-2.5 rounded-lg bg-ink-50/70 dark:bg-white/5 border border-ink-100 dark:border-white/5">
                                      <span className="text-ink-400 block text-[11px] font-medium mb-0.5">Họ & Tên:</span>
                                      <span className="font-semibold text-ink-900 dark:text-white text-sm">{group.name}</span>
                                    </div>
                                    <div className="p-2.5 rounded-lg bg-ink-50/70 dark:bg-white/5 border border-ink-100 dark:border-white/5">
                                      <span className="text-ink-400 block text-[11px] font-medium mb-0.5">Email liên hệ:</span>
                                      <a href={`mailto:${group.email}`} className="font-medium text-brand-600 dark:text-brand-400 hover:underline flex items-center gap-1.5 truncate">
                                        <Mail className="w-3.5 h-3.5 shrink-0" />
                                        <span className="truncate">{group.email}</span>
                                      </a>
                                    </div>
                                    <div className="p-2.5 rounded-lg bg-ink-50/70 dark:bg-white/5 border border-ink-100 dark:border-white/5">
                                      <span className="text-ink-400 block text-[11px] font-medium mb-0.5">Số điện thoại:</span>
                                      {group.phone ? (
                                        <a href={`tel:${group.phone}`} className="font-medium text-ink-900 dark:text-white hover:underline flex items-center gap-1.5">
                                          <Phone className="w-3.5 h-3.5 text-emerald-500 shrink-0" />
                                          <span>{group.phone}</span>
                                        </a>
                                      ) : (
                                        <span className="text-ink-400">Chưa cập nhật</span>
                                      )}
                                    </div>
                                    <div className="p-2.5 rounded-lg bg-ink-50/70 dark:bg-white/5 border border-ink-100 dark:border-white/5">
                                      <span className="text-ink-400 block text-[11px] font-medium mb-1">Hồ sơ & Profile:</span>
                                      <div className="flex items-center gap-2">
                                        {group.cvFileUrl ? (
                                          <button
                                            type="button"
                                            onClick={() =>
                                              openDocument(
                                                resolveAssetUrl(group.cvFileUrl),
                                                `${group.name} - CV`
                                              )
                                            }
                                            className="inline-flex items-center gap-1 font-medium text-brand-600 dark:text-brand-400 hover:underline text-left cursor-pointer"
                                          >
                                            <FileText className="w-3.5 h-3.5 shrink-0 text-brand-500" />
                                            <span>Xem CV</span>
                                          </button>
                                        ) : (
                                          <span className="text-ink-400">Chưa có CV</span>
                                        )}
                                        <span className="text-ink-300 dark:text-white/20">|</span>
                                        <button
                                          type="button"
                                          onClick={() => setSelectedProfileApp(group.applications[0])}
                                          className="inline-flex items-center gap-1 font-medium text-purple-600 dark:text-purple-400 hover:underline text-left cursor-pointer"
                                        >
                                          <UserCheck className="w-3.5 h-3.5 shrink-0 text-purple-500" />
                                          <span>Profile Online</span>
                                        </button>
                                      </div>
                                    </div>
                                  </div>
                                </div>

                                {/* Bảng lịch sử ứng tuyển chi tiết */}
                                <div className="rounded-xl border border-ink-200/80 dark:border-white/10 bg-white dark:bg-ink-900 p-4 shadow-sm">
                                  <div className="text-xs font-semibold text-ink-500 dark:text-ink-400 mb-3 flex items-center justify-between">
                                    <div className="flex items-center gap-2">
                                      <Briefcase className="w-4 h-4 text-brand-500" />
                                      <span>LỊCH SỬ ỨNG TUYỂN CHI TIẾT ({matchingApps.length} VỊ TRÍ)</span>
                                    </div>
                                    {hasFilterActive && (
                                      <span className="text-xs text-brand-600 dark:text-brand-400 font-normal">
                                        (Hiển thị {matchingApps.length} trên tổng {group.applications.length} vị trí theo bộ lọc)
                                      </span>
                                    )}
                                  </div>
                                {matchingApps.length === 0 ? (
                                  <p className="text-sm text-ink-500 py-3 text-center">Không có ứng tuyển nào khớp với bộ lọc hiện tại.</p>
                                ) : (
                                  <div className="overflow-x-auto">
                                    <table className="w-full text-sm">
                                      <thead>
                                        <tr className="border-b border-ink-200 dark:border-white/10 text-xs font-semibold text-ink-500 dark:text-ink-400 bg-ink-50/70 dark:bg-white/5">
                                          <th className="text-left py-2.5 px-3">Vị trí tuyển dụng</th>
                                          <th className="text-left py-2.5 px-3">Thời gian nộp</th>
                                          <th className="text-left py-2.5 px-3">Trạng thái hồ sơ</th>
                                          <th className="text-left py-2.5 px-3">Đánh giá CV</th>
                                          <th className="text-right py-2.5 px-3">Thao tác</th>
                                        </tr>
                                      </thead>
                                      <tbody>
                                        {matchingApps.map((app) => {
                                          const meta = statusMeta(app.status, t)
                                          return (
                                            <tr
                                              key={app.id}
                                              onClick={() => navigate(`/hr/candidates/${app.id}`)}
                                              className="border-b border-ink-100 dark:border-white/5 hover:bg-ink-50 dark:hover:bg-white/5 cursor-pointer transition-colors"
                                            >
                                              <td className="py-2.5 px-3 font-medium text-ink-900 dark:text-white">
                                                {app.jobTitle || '—'}
                                              </td>
                                              <td className="py-2.5 px-3 text-ink-600 dark:text-ink-400 text-xs">
                                                {formatDate(app.createdAt)}
                                              </td>
                                              <td className="py-2.5 px-3">
                                                <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${meta.badge}`}>
                                                  {meta.label}
                                                </span>
                                              </td>
                                              <td className="py-2.5 px-3 text-sm">
                                                {typeof app.matchScore === 'number' ? (
                                                  <span className={`font-semibold text-xs ${scoreColor(app.matchScore)}`}>
                                                    Điểm CV: {app.matchScore}
                                                  </span>
                                                ) : (
                                                  <span className="text-ink-400 text-xs">—</span>
                                                )}
                                              </td>
                                              <td className="py-2.5 px-3 text-right" onClick={(e) => e.stopPropagation()}>
                                                <div className="flex items-center justify-end gap-1.5">
                                                  {app.cvFileUrl && (
                                                    <button
                                                      type="button"
                                                      onClick={(e) => {
                                                        e.stopPropagation()
                                                        openDocument(
                                                          resolveAssetUrl(app.cvFileUrl!),
                                                          `${app.candidateName || group.name} - CV (${app.jobTitle || 'Job'})`
                                                        )
                                                      }}
                                                      title="Xem File CV ứng tuyển cho công việc này"
                                                      className="p-1.5 rounded-lg border border-brand-200 dark:border-brand-500/30 bg-brand-50/50 dark:bg-brand-500/10 hover:bg-brand-100 dark:hover:bg-brand-500/20 text-brand-600 dark:text-brand-400 transition-colors flex items-center gap-1 text-xs font-medium cursor-pointer"
                                                    >
                                                      <FileText className="w-3.5 h-3.5" />
                                                      <span>Xem CV</span>
                                                    </button>
                                                  )}
                                                  <button
                                                    onClick={() => navigate(`/hr/candidates/${app.id}`)}
                                                    title={t('viewDetails')}
                                                    className="p-1.5 rounded-lg border border-ink-200 dark:border-white/10 hover:bg-ink-100 dark:hover:bg-white/10 text-ink-600 dark:text-ink-300 transition-colors"
                                                  >
                                                    <Eye className="w-3.5 h-3.5" />
                                                  </button>
                                                </div>
                                              </td>
                                            </tr>
                                          )
                                        })}
                                      </tbody>
                                    </table>
                                  </div>
                                )}
                              </div>
                            </div>
                          </td>
                          </motion.tr>
                        )}
                      </AnimatePresence>
                    </Fragment>
                  )
                })}
              </tbody>
            </table>
            </div>
          </div>
        </div>
      )}

      {!loading && !error && groupedCandidates.length > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          total={groupedCandidates.length}
          label={t('paginationLabel')}
          onPageChange={handlePageChange}
        />
      )}

      {selectedProfileApp && (
        <CandidateOnlineProfileModal
          app={selectedProfileApp}
          onClose={() => setSelectedProfileApp(null)}
        />
      )}
    </div>
  )
}

export function CandidateOnlineProfileModal({
  app,
  onClose,
}: {
  app: HrApplicationItem
  onClose: () => void
}) {
  const isSharing = app.allowHrViewProfile !== false

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200" onClick={onClose}>
      <div
        className="bg-white dark:bg-ink-900 border border-ink-200 dark:border-white/10 rounded-2xl shadow-2xl w-full max-w-3xl overflow-hidden flex flex-col max-h-[90vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Modal Header */}
        <div className="px-6 py-4 border-b border-ink-100 dark:border-white/10 flex items-center justify-between bg-ink-50/50 dark:bg-white/5">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-gradient-to-tr from-brand-600 to-purple-600 text-white font-bold flex items-center justify-center text-sm shadow-md">
              {app.candidateName?.slice(0, 2).toUpperCase() || 'CV'}
            </div>
            <div>
              <h3 className="font-bold text-lg text-ink-900 dark:text-white flex items-center gap-2">
                <span>{app.candidateName}</span>
                {isSharing ? (
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-300">
                    <UserCheck className="w-3.5 h-3.5" /> Cho phép xem đầy đủ
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-300">
                    <Lock className="w-3.5 h-3.5" /> Chế độ Riêng tư
                  </span>
                )}
              </h3>
              <p className="text-xs text-ink-500 dark:text-ink-400">
                {app.candidateHeadline || 'Chưa cập nhật chức danh'}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-ink-400 hover:text-ink-600 dark:hover:text-white hover:bg-ink-100 dark:hover:bg-white/10 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 overflow-y-auto space-y-6 flex-1 text-sm">
          {/* Thông tin cá nhân cơ bản */}
          <div className="rounded-xl border border-ink-200/80 dark:border-white/10 p-4 bg-ink-50/40 dark:bg-white/5 space-y-3">
            <h4 className="text-xs font-bold text-ink-500 dark:text-ink-400 uppercase tracking-wider flex items-center gap-2">
              <User className="w-4 h-4 text-brand-500" /> Thông tin cá nhân cơ bản
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 text-xs">
              <div>
                <span className="text-ink-400 block mb-0.5 font-medium">Email:</span>
                <span className="font-semibold text-ink-900 dark:text-white">{app.candidateEmail}</span>
              </div>
              <div>
                <span className="text-ink-400 block mb-0.5 font-medium">Số điện thoại:</span>
                <span className="font-semibold text-ink-900 dark:text-white">{app.candidatePhone || 'Chưa có'}</span>
              </div>
              <div>
                <span className="text-ink-400 block mb-0.5 font-medium">Nơi làm việc mong muốn:</span>
                <span className="font-semibold text-ink-900 dark:text-white">{app.candidateLocation || 'Chưa cập nhật'}</span>
              </div>
              <div>
                <span className="text-ink-400 block mb-0.5 font-medium">Ngày sinh:</span>
                <span className="font-semibold text-ink-900 dark:text-white">{app.candidateDateOfBirth || 'Chưa cập nhật'}</span>
              </div>
            </div>

            {app.candidateAbout && (
              <div className="pt-2 border-t border-ink-200/60 dark:border-white/5">
                <span className="text-ink-400 block text-xs font-medium mb-1">Giới thiệu bản thân:</span>
                <p className="text-xs text-ink-700 dark:text-ink-300 leading-relaxed bg-white dark:bg-ink-800 p-3 rounded-lg border border-ink-100 dark:border-white/5 whitespace-pre-line">
                  {app.candidateAbout}
                </p>
              </div>
            )}
          </div>

          {/* Nếu ứng viên TẮT chia sẻ Profile Mở rộng */}
          {!isSharing && (
            <div className="p-4 rounded-xl border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/10 text-amber-800 dark:text-amber-300 flex items-start gap-3 text-xs leading-relaxed">
              <Lock className="w-5 h-5 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
              <div>
                <strong className="font-semibold block mb-1">Ứng viên đã bật Chế độ Riêng tư cho Profile Online</strong>
                Ứng viên đang tắt tùy chọn chia sẻ thông tin Profile mở rộng (Kỹ năng, Kinh nghiệm, Học vấn). 
                Bạn vẫn có thể xem Thông tin cá nhân cơ bản phía trên và file CV đính kèm cho từng đơn ứng tuyển.
              </div>
            </div>
          )}

          {/* Nếu ứng viên BẬT chia sẻ Profile Mở rộng */}
          {isSharing && (
            <>
              {/* Kỹ năng & Công nghệ */}
              <div className="space-y-2">
                <h4 className="text-xs font-bold text-ink-500 dark:text-ink-400 uppercase tracking-wider flex items-center gap-2">
                  <Award className="w-4 h-4 text-purple-500" /> Kỹ năng & Công nghệ
                </h4>
                {app.candidateSkills && app.candidateSkills.length > 0 ? (
                  <div className="flex flex-wrap gap-1.5">
                    {app.candidateSkills.map((skill, idx) => (
                      <span
                        key={idx}
                        className="px-2.5 py-1 rounded-lg text-xs font-medium bg-purple-50 dark:bg-purple-500/15 text-purple-700 dark:text-purple-300 border border-purple-200/60 dark:border-purple-500/20"
                      >
                        {skill}
                      </span>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-ink-400 italic">Ứng viên chưa bổ sung thông tin kỹ năng.</p>
                )}
              </div>

              {/* Kinh nghiệm làm việc */}
              <div className="space-y-2">
                <h4 className="text-xs font-bold text-ink-500 dark:text-ink-400 uppercase tracking-wider flex items-center gap-2">
                  <Briefcase className="w-4 h-4 text-brand-500" /> Kinh nghiệm làm việc
                </h4>
                {app.candidateExperience && app.candidateExperience.length > 0 ? (
                  <div className="space-y-2.5">
                    {app.candidateExperience.map((exp, idx) => (
                      <div
                        key={idx}
                        className="p-3 rounded-xl border border-ink-200/70 dark:border-white/10 bg-white dark:bg-ink-800 space-y-1"
                      >
                        <div className="flex items-center justify-between">
                          <span className="font-bold text-ink-900 dark:text-white text-xs">{exp.title}</span>
                          <span className="text-[11px] font-medium text-brand-600 dark:text-brand-400 bg-brand-50 dark:bg-brand-500/10 px-2 py-0.5 rounded-full">
                            {exp.period}
                          </span>
                        </div>
                        <div className="text-xs font-medium text-ink-600 dark:text-ink-300">{exp.organization}</div>
                        {exp.description && (
                          <p className="text-xs text-ink-500 dark:text-ink-400 whitespace-pre-line pt-1">
                            {exp.description}
                          </p>
                        )}
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-ink-400 italic">Ứng viên chưa bổ sung lịch sử làm việc.</p>
                )}
              </div>

              {/* Học vấn */}
              <div className="space-y-2">
                <h4 className="text-xs font-bold text-ink-500 dark:text-ink-400 uppercase tracking-wider flex items-center gap-2">
                  <GraduationCap className="w-4 h-4 text-emerald-500" /> Học vấn & Bằng cấp
                </h4>
                {app.candidateEducation && app.candidateEducation.length > 0 ? (
                  <div className="space-y-2.5">
                    {app.candidateEducation.map((edu, idx) => (
                      <div
                        key={idx}
                        className="p-3 rounded-xl border border-ink-200/70 dark:border-white/10 bg-white dark:bg-ink-800 space-y-1"
                      >
                        <div className="flex items-center justify-between">
                          <span className="font-bold text-ink-900 dark:text-white text-xs">{edu.school}</span>
                          <span className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-500/10 px-2 py-0.5 rounded-full">
                            {edu.period}
                          </span>
                        </div>
                        <div className="text-xs font-medium text-ink-600 dark:text-ink-300">{edu.degree}</div>
                        {edu.note && (
                          <p className="text-xs text-ink-500 dark:text-ink-400 whitespace-pre-line pt-1">
                            {edu.note}
                          </p>
                        )}
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-ink-400 italic">Ứng viên chưa bổ sung thông tin học vấn.</p>
                )}
              </div>

              {/* Các liên kết */}
              {(app.candidateLinkedinUrl || app.candidateGithubUrl || app.candidatePortfolioUrl) && (
                <div className="space-y-2">
                  <h4 className="text-xs font-bold text-ink-500 dark:text-ink-400 uppercase tracking-wider">
                    Liên kết cá nhân / Mạng xã hội
                  </h4>
                  <div className="flex flex-wrap gap-2">
                    {app.candidateLinkedinUrl && (
                      <a
                        href={app.candidateLinkedinUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-sky-200 bg-sky-50 text-sky-700 dark:bg-sky-500/10 dark:border-sky-500/20 dark:text-sky-300 text-xs font-medium hover:underline"
                      >
                        <Link2 className="w-3.5 h-3.5 text-sky-600" /> LinkedIn
                      </a>
                    )}
                    {app.candidateGithubUrl && (
                      <a
                        href={app.candidateGithubUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-ink-300 bg-ink-100 text-ink-800 dark:bg-white/10 dark:border-white/20 dark:text-white text-xs font-medium hover:underline"
                      >
                        <Link2 className="w-3.5 h-3.5 text-ink-900 dark:text-white" /> GitHub
                      </a>
                    )}
                    {app.candidatePortfolioUrl && (
                      <a
                        href={app.candidatePortfolioUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-indigo-200 bg-indigo-50 text-indigo-700 dark:bg-indigo-500/10 dark:border-indigo-500/20 dark:text-indigo-300 text-xs font-medium hover:underline"
                      >
                        <Globe className="w-3.5 h-3.5 text-indigo-600" /> Portfolio Website
                      </a>
                    )}
                  </div>
                </div>
              )}
            </>
          )}
        </div>

        {/* Modal Footer */}
        <div className="px-6 py-3 border-t border-ink-100 dark:border-white/10 bg-ink-50/50 dark:bg-white/5 flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-xl bg-ink-200 dark:bg-white/10 text-ink-800 dark:text-white hover:bg-ink-300 dark:hover:bg-white/20 text-xs font-semibold transition-colors cursor-pointer"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  )
}
