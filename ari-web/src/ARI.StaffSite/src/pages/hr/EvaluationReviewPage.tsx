import { useMemo, useState, useEffect } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  Code2,
  Layers,
  Calendar,
  Clock,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Languages,
  ArrowLeft,
  Search,
  Eye,
  Award,
  XCircle,
  AlertTriangle,
  X,
} from 'lucide-react'
import { evaluationService } from '@/fservices/evaluation/evaluationService'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import type { EvaluationReport } from '@ari/shared/types/evaluation'
import { EvaluationListSkeleton, HrStatsSkeleton } from './_skeletons'
import { PageHeader, StatsGrid, Pagination } from '@ari/shared/ui'
import { useQuery, keepPreviousData } from '@tanstack/react-query'

/**
 * Backend từng trả verdict ở nhiều dạng (`pass`, `not_pass`, `verdict.pass`...). Kiểm `not`
 * TRƯỚC vì chuỗi "not_pass" cũng chứa "pass" — thứ tự ngược lại sẽ đọc "Không đạt" thành "Đạt".
 */
function isPassVerdict(verdict?: string | null): boolean {
  if (!verdict) return false
  const v = verdict.toLowerCase().trim()
  if (v.includes('not')) return false
  return v.includes('pass')
}

function formatDate(dateString?: string) {
  if (!dateString) return '—'
  try {
    return new Date(dateString).toLocaleDateString('vi-VN')
  } catch {
    return dateString
  }
}

function formatTime(dateString?: string) {
  if (!dateString) return ''
  try {
    const d = new Date(dateString)
    return d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  } catch {
    return ''
  }
}

function getInitials(name?: string) {
  if (!name) return 'NA'
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

function getScoreColor(score: number) {
  if (score >= 80) return 'bg-emerald-500'
  if (score >= 60) return 'bg-brand-500'
  return 'bg-amber-500'
}

function getScoreTextColor(score: number) {
  if (score >= 80) return 'text-emerald-700 dark:text-emerald-400'
  if (score >= 60) return 'text-brand-700 dark:text-brand-400'
  return 'text-amber-700 dark:text-amber-400'
}

function getScoreBgColor(score: number) {
  if (score >= 80) return 'bg-emerald-50 dark:bg-emerald-500/20'
  if (score >= 60) return 'bg-brand-50 dark:bg-brand-500/20'
  return 'bg-amber-50 dark:bg-amber-500/20'
}

export default function EvaluationReviewPage() {
  const { t } = useTranslation('modules/hr/evaluations')
  const [searchParams, setSearchParams] = useSearchParams()
  const targetId = searchParams.get('id')

  const page = Number(searchParams.get('page')) || 1
  const searchQuery = searchParams.get('search') || ''
  const statusFilter = (searchParams.get('status') as 'all' | 'pending' | 'pass' | 'not_pass') || 'all'
  const dateFilter = searchParams.get('date') || ''

  const [selectedEvaluation, setSelectedEvaluation] = useState<EvaluationReport | null>(null)
  const [loadingDetail, setLoadingDetail] = useState<boolean>(Boolean(targetId))
  const [isOverrideMode, setIsOverrideMode] = useState(false)
  const [overrideReason, setOverrideReason] = useState('')
  /** Verdict nhân sự chọn khi ghi đè. null = chưa mở khối ghi đè (lúc mở sẽ đặt mặc định). */
  const [overrideVerdict, setOverrideVerdict] = useState<'pass' | 'not_pass' | null>(null)
  const [submittingAction, setSubmittingAction] = useState<'confirm' | 'override' | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>({})

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

  const {
    data: evaluationsResponse,
    isLoading: loading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['evaluations-all'],
    queryFn: () => evaluationService.getEvaluations({ page: 1, pageSize: 100 }),
    refetchOnWindowFocus: false,
    placeholderData: keepPreviousData,
  })

  const evaluations = evaluationsResponse?.items || []

  const displayError = error ? t('loadingError') : null

  const filteredEvaluations = useMemo(() => {
    return evaluations.filter((e) => {
      const q = searchQuery.toLowerCase().trim()
      const matchesSearch = !q || (e.candidateName || '').toLowerCase().includes(q) || (e.jobTitle || '').toLowerCase().includes(q)
      if (!matchesSearch) return false

      if (dateFilter) {
        const eDate = e.createdAt ? new Date(e.createdAt).toISOString().split('T')[0] : ''
        if (eDate !== dateFilter) return false
      }

      if (statusFilter === 'pending') return e.status === 'pending'
      const verdict = e.finalVerdict ?? e.aiVerdict
      const isPass = isPassVerdict(verdict)
      if (statusFilter === 'pass') return isPass && e.status !== 'pending'
      if (statusFilter === 'not_pass') return !isPass && e.status !== 'pending'
      return true
    })
  }, [evaluations, searchQuery, statusFilter, dateFilter])

  interface EvaluationSessionGroup {
    groupId: string
    jobTitle: string
    roundNumber: number
    sessionDate: string
    sessionTime: string
    items: EvaluationReport[]
    passCount: number
    notPassCount: number
    pendingCount: number
  }

  const GROUPS_PER_PAGE = 5

  const groupedSessions = useMemo(() => {
    const groupsMap = new Map<string, EvaluationSessionGroup>()

    filteredEvaluations.forEach((evalItem) => {
      const dateStr = formatDate(evalItem.createdAt)
      const timeStr = formatTime(evalItem.createdAt)
      const key = evalItem.sessionId || `${dateStr}_${evalItem.jobTitle}_${evalItem.roundNumber}`

      if (!groupsMap.has(key)) {
        groupsMap.set(key, {
          groupId: key,
          jobTitle: evalItem.jobTitle || 'Phiên phỏng vấn',
          roundNumber: evalItem.roundNumber,
          sessionDate: dateStr,
          sessionTime: timeStr,
          items: [],
          passCount: 0,
          notPassCount: 0,
          pendingCount: 0,
        })
      }

      const grp = groupsMap.get(key)!
      grp.items.push(evalItem)

      if (evalItem.status === 'pending') {
        grp.pendingCount += 1
      } else {
        const verdict = evalItem.finalVerdict ?? evalItem.aiVerdict
        const isPass = isPassVerdict(verdict)
        if (isPass) grp.passCount += 1
        else grp.notPassCount += 1
      }
    })

    return Array.from(groupsMap.values())
  }, [filteredEvaluations])


  const totalGroupPages = Math.max(1, Math.ceil(groupedSessions.length / GROUPS_PER_PAGE))

  const paginatedGroupedSessions = useMemo(() => {
    const start = (page - 1) * GROUPS_PER_PAGE
    return groupedSessions.slice(start, start + GROUPS_PER_PAGE)
  }, [groupedSessions, page])

  function toggleGroup(groupId: string) {
    setExpandedGroups((prev) => ({
      ...prev,
      [groupId]: !prev[groupId],
    }))
  }

  useEffect(() => {
    if (targetId) {
      handleOpenDetail(targetId)
    } else {
      setSelectedEvaluation(null)
      setLoadingDetail(false)
    }
  }, [targetId])

  async function handleOpenDetail(evaluationId: string) {
    try {
      setLoadingDetail(true)
      setActionError(null)
      setIsOverrideMode(false)
      setOverrideReason('')
      const detail = await evaluationService.getEvaluationById(evaluationId)
      setSelectedEvaluation(detail)
    } catch (fetchError) {
      console.error(fetchError)
      setSelectedEvaluation(null)
    } finally {
      setLoadingDetail(false)
    }
  }

  function closeDetail() {
    setSelectedEvaluation(null)
    setLoadingDetail(false)
    setActionError(null)
    setIsOverrideMode(false)
    setOverrideReason('')
    setSubmittingAction(null)
    if (targetId) {
      setSearchParams({})
    }
  }

  async function refreshListAndSelection(evaluationId: string) {
    await refetch()
    const detailResponse = await evaluationService.getEvaluationById(evaluationId)
    setSelectedEvaluation(detailResponse)
  }

  async function handleConfirm() {
    if (!selectedEvaluation) return
    try {
      setSubmittingAction('confirm')
      setActionError(null)
      await evaluationService.confirmEvaluation(selectedEvaluation)
      await refreshListAndSelection(selectedEvaluation.id)
    } catch (submitError) {
      console.error(submitError)
      setActionError(t('confirmError'))
    } finally {
      setSubmittingAction(null)
    }
  }

  async function handleOverride() {
    if (!selectedEvaluation) return
    const trimmedReason = overrideReason.trim()
    if (!trimmedReason) {
      setActionError(t('overrideReasonRequired'))
      return
    }
    try {
      setSubmittingAction('override')
      setActionError(null)
      // Gửi ĐÚNG verdict nhân sự chọn. Trước đây service tự lật ngược verdict của AI nên hai
      // nút chọn trên UI chỉ là trang trí — bấm gì cũng ra cùng một kết quả.
      const verdict = overrideVerdict ?? (aiPassed ? 'not_pass' : 'pass')
      await evaluationService.overrideEvaluation(selectedEvaluation, trimmedReason, verdict)
      await refreshListAndSelection(selectedEvaluation.id)
    } catch (submitError) {
      console.error(submitError)
      setActionError(t('overrideError'))
    } finally {
      setSubmittingAction(null)
    }
  }

  const counts = useMemo(() => {
    const total = evaluations.length
    const completed = evaluations.filter((item) => item.status === 'completed').length
    const pending = evaluations.filter((item) => item.status === 'pending').length
    const highScore = evaluations.filter((item) => (item.overallScore ?? 0) >= 80).length
    const pass = evaluations.filter((item) => {
      const v = item.finalVerdict ?? item.aiVerdict
      return item.status !== 'pending' && isPassVerdict(v)
    }).length
    const notPass = evaluations.filter((item) => {
      const v = item.finalVerdict ?? item.aiVerdict
      return item.status !== 'pending' && !isPassVerdict(v)
    }).length

    return { total, completed, pending, highScore, pass, notPass }
  }, [evaluations])

  // Màu theo cùng quy ước với các màn staff khác (xem CandidatesPage): tổng = xanh dương,
  // chờ = hổ phách, đang chạy = tím, đạt = xanh lá.
  const stats = useMemo(
    () => [
      { label: t('stats.total'), value: counts.total, color: 'text-blue-600 dark:text-blue-400' },
      {
        label: t('stats.completed'),
        value: counts.completed,
        color: 'text-violet-600 dark:text-violet-400',
      },
      {
        label: t('stats.pending'),
        value: counts.pending,
        color: 'text-amber-600 dark:text-amber-400',
      },
      {
        label: t('stats.highScore'),
        value: counts.highScore,
        color: 'text-emerald-600 dark:text-emerald-400',
      },
    ],
    [counts, t]
  )

  const criterionEntries = useMemo(
    () => Object.entries(selectedEvaluation?.criterionScores ?? {}),
    [selectedEvaluation]
  )

  const aiPassed = isPassVerdict(selectedEvaluation?.aiVerdict)

  /** Nhãn verdict qua i18n — trước đây hàm này ghi cứng "Đạt"/"Không đạt" giữa một màn đã i18n. */
  const verdictLabel = (verdict?: string | null) =>
    verdict ? (isPassVerdict(verdict) ? t('verdictPass') : t('verdictNotPass')) : '—'

  /** Mở khối ghi đè thì đặt sẵn verdict ở phía NGƯỢC với AI — đó là lý do người ta bấm Ghi đè. */
  const toggleOverrideMode = () => {
    setIsOverrideMode((open) => {
      if (!open) setOverrideVerdict(aiPassed ? 'not_pass' : 'pass')
      return !open
    })
  }

  if (loadingDetail) {
    return (
      <div className="grid min-h-[60vh] place-items-center bg-ink-50 dark:bg-ink-950">
        <div className="flex flex-col items-center gap-3">
          <div className="h-10 w-10 animate-spin rounded-full border-2 border-brand-300 border-t-brand-600 dark:border-brand-500/30 dark:border-t-brand-400" />
          <p className="text-sm font-medium text-ink-500 dark:text-ink-400">Đang tải báo cáo đánh giá chi tiết...</p>
        </div>
      </div>
    )
  }

  // List view
  if (!selectedEvaluation) {
    return (
      <main className="min-h-screen bg-ink-50 dark:bg-ink-950 p-4 sm:p-6 lg:p-8">
        {/* Dùng PageHeader + StatsGrid CHUNG thay vì tự dựng lại. Bản tự dựng trước đây là lý do
            màn này lệch hẳn phần còn lại: tiêu đề `font-display font-extrabold` (các màn khác
            `font-semibold`), thẻ số liệu đảo ngược (số to trên, nhãn nhỏ dưới, toàn màu đen)
            trong khi chuẩn là nhãn trên - số dưới có màu theo nhóm; và quan trọng nhất là MẤT
            hiệu ứng trôi lên `initial={{opacity:0,y:20}}` vốn nằm sẵn trong 2 component đó. */}
        <PageHeader title={t('title')} description={t('subtitle')} />

        {/* Skeleton lúc tải giống các màn khác — trước đây thẻ số liệu hiện ngay số 0 rồi mới
            nhảy sang số thật, đọc thoáng qua tưởng là "không có đánh giá nào". */}
        {loading ? <HrStatsSkeleton /> : <StatsGrid stats={stats} />}

        {/* Thanh tìm kiếm & lọc — nối tiếp nhịp trôi lên sau 4 thẻ số liệu (mỗi thẻ 0.05s). */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="mb-6 flex flex-col lg:flex-row items-stretch lg:items-center justify-between gap-3 bg-white dark:bg-white/5 p-3 rounded-2xl border border-ink-200 dark:border-white/10 shadow-sm"
        >
          <div className="flex flex-col sm:flex-row items-center gap-2 flex-1">
            <div className="relative w-full sm:flex-1">
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-ink-400" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => updateParam('search', e.target.value)}
                placeholder="Tìm theo tên ứng viên hoặc vị trí..."
                className="w-full pl-9 pr-4 py-2 text-sm bg-ink-50 dark:bg-white/5 border border-ink-200 dark:border-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-brand-500 text-ink-900 dark:text-white"
              />
            </div>

            {/* Date filter */}
            <div className="relative flex items-center w-full sm:w-auto">
              <Calendar className="w-4 h-4 absolute left-3 text-ink-400 pointer-events-none" />
              <input
                type="date"
                value={dateFilter}
                onChange={(e) => updateParam('date', e.target.value)}
                className="w-full sm:w-auto pl-9 pr-8 py-2 text-xs bg-ink-50 dark:bg-white/5 border border-ink-200 dark:border-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-brand-500 text-ink-900 dark:text-white"
              />
              {dateFilter && (
                <button
                  onClick={() => updateParam('date', '')}
                  className="absolute right-2 text-ink-400 hover:text-ink-600 p-0.5 rounded-full"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              )}
            </div>
          </div>

          <div className="flex items-center gap-1.5 overflow-x-auto pb-1 lg:pb-0">
            <button
              onClick={() => updateParam('status', 'all')}
              className={`px-3 py-1.5 rounded-xl text-xs font-medium transition-colors shrink-0 ${
                statusFilter === 'all'
                  ? 'bg-brand-600 text-white font-semibold shadow-sm'
                  : 'bg-ink-50 dark:bg-white/5 text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10'
              }`}
            >
              Tất cả ({counts.total})
            </button>
            <button
              onClick={() => updateParam('status', 'pending')}
              className={`px-3 py-1.5 rounded-xl text-xs font-medium transition-colors shrink-0 flex items-center gap-1 ${
                statusFilter === 'pending'
                  ? 'bg-amber-500 text-white font-semibold shadow-sm'
                  : 'bg-ink-50 dark:bg-white/5 text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10'
              }`}
            >
              <Clock className="w-3.5 h-3.5" />
              Chờ duyệt ({counts.pending})
            </button>
            <button
              onClick={() => updateParam('status', 'pass')}
              className={`px-3 py-1.5 rounded-xl text-xs font-medium transition-colors shrink-0 flex items-center gap-1 ${
                statusFilter === 'pass'
                  ? 'bg-emerald-600 text-white font-semibold shadow-sm'
                  : 'bg-ink-50 dark:bg-white/5 text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10'
              }`}
            >
              <CheckCircle2 className="w-3.5 h-3.5" />
              Đạt ({counts.pass})
            </button>
            <button
              onClick={() => updateParam('status', 'not_pass')}
              className={`px-3 py-1.5 rounded-xl text-xs font-medium transition-colors shrink-0 flex items-center gap-1 ${
                statusFilter === 'not_pass'
                  ? 'bg-red-600 text-white font-semibold shadow-sm'
                  : 'bg-ink-50 dark:bg-white/5 text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10'
              }`}
            >
              <XCircle className="w-3.5 h-3.5" />
              Không đạt ({counts.notPass})
            </button>
          </div>
        </motion.div>

        {loading ? (
          <EvaluationListSkeleton rows={4} />
        ) : displayError ? (
          <div className="rounded-2xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-400">
            {displayError}
          </div>
        ) : groupedSessions.length === 0 ? (
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-12 text-center text-ink-500 dark:text-ink-400 space-y-2">
            <Award className="w-10 h-10 mx-auto text-ink-300" />
            <p className="font-semibold text-ink-700 dark:text-ink-300">Không tìm thấy ca thi hoặc báo cáo đánh giá phù hợp</p>
            <p className="text-xs text-ink-400">Thử điều chỉnh từ khóa tìm kiếm hoặc bộ lọc ngày / trạng thái</p>
          </div>
        ) : (
          <div className="space-y-4">
            {paginatedGroupedSessions.map((group, i) => {
              const isExpanded = Boolean(expandedGroups[group.groupId])
              return (
                // Nối tiếp nhịp trôi lên: header → 4 thẻ số liệu (0.05s/thẻ) → thanh lọc (0.2)
                // → danh sách từ 0.25. Trước đây màn này chỉ animate phần mở rộng bên trong
                // nên chuyển sang là cả trang hiện ra khô cứng, lệch hẳn các màn còn lại.
                <motion.div
                  key={group.groupId}
                  initial={{ opacity: 0, y: 16 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.25 + Math.min(i * 0.04, 0.3) }}
                  className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 overflow-hidden shadow-card"
                >
                  {/* Session Group Header */}
                  <div
                    onClick={() => toggleGroup(group.groupId)}
                    className="flex flex-wrap items-center justify-between p-4 bg-ink-50/70 dark:bg-white/5 border-b border-ink-200/60 dark:border-white/10 cursor-pointer hover:bg-ink-100/50 dark:hover:bg-white/10 transition-colors gap-3"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400">
                        <Clock className="w-5 h-5" />
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <h3 className="font-display text-base font-bold text-ink-900 dark:text-white truncate">
                            Ca thi: {group.sessionTime ? `${group.sessionTime} (${group.sessionDate})` : group.sessionDate}
                          </h3>
                          <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-brand-500 text-white">
                            {group.jobTitle} · Vòng {group.roundNumber}
                          </span>
                        </div>
                        <p className="text-xs text-ink-500 dark:text-ink-400 mt-0.5">
                          Tổng số thí sinh: <b className="text-ink-700 dark:text-ink-300">{group.items.length}</b>
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-3 ml-auto">
                      <div className="hidden sm:flex items-center gap-2 text-xs">
                        {group.passCount > 0 && (
                          <span className="px-2.5 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 font-semibold border border-emerald-200 dark:border-emerald-500/30">
                            {group.passCount} Đạt
                          </span>
                        )}
                        {group.notPassCount > 0 && (
                          <span className="px-2.5 py-0.5 rounded-full bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400 font-semibold border border-red-200 dark:border-red-500/30">
                            {group.notPassCount} Không đạt
                          </span>
                        )}
                        {group.pendingCount > 0 && (
                          <span className="px-2.5 py-0.5 rounded-full bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400 font-semibold border border-amber-200 dark:border-amber-500/30">
                            {group.pendingCount} Chờ duyệt
                          </span>
                        )}
                      </div>

                      <button className="flex items-center gap-1 text-xs font-medium text-ink-600 dark:text-ink-400 p-1.5 rounded-lg hover:bg-ink-200/60 dark:hover:bg-white/10 transition-colors">
                        <span className="hidden sm:inline">{isExpanded ? 'Thu gọn' : 'Mở rộng'}</span>
                        <ChevronDown className={`w-4 h-4 transition-transform duration-200 ${isExpanded ? 'rotate-180' : ''}`} />
                      </button>
                    </div>
                  </div>

                  {/* Session Group Candidates List */}
                  {isExpanded && (
                    <div className="p-3 space-y-2.5 divide-y divide-ink-100 dark:divide-white/5">
                      {group.items.map((evaluation) => {
                        const overallScore = evaluation.overallScore ?? 0
                        const isPending = evaluation.status === 'pending'
                        const verdict = evaluation.finalVerdict ?? evaluation.aiVerdict
                        const isPass = isPassVerdict(verdict)

                        return (
                          <div
                            key={evaluation.id}
                            className="pt-2.5 first:pt-0 flex flex-wrap items-center justify-between gap-3 p-3 rounded-xl hover:bg-ink-50 dark:hover:bg-white/5 transition-colors cursor-pointer group"
                            onClick={() => handleOpenDetail(evaluation.id)}
                          >
                            <div className="flex items-center gap-3 min-w-0">
                              <div className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-gradient-to-br from-brand-500 to-ai-600 text-white font-display text-xs font-extrabold shadow-sm">
                                {getInitials(evaluation.candidateName)}
                              </div>
                              <div className="min-w-0">
                                <h4 className="font-display text-sm font-bold text-ink-900 dark:text-white group-hover:text-brand-600 transition-colors truncate">
                                  {evaluation.candidateName ?? t('candidate')}
                                </h4>
                                <p className="text-xs text-ink-500 dark:text-ink-400 truncate">
                                  {evaluation.candidateEmail || evaluation.jobTitle}
                                </p>
                              </div>
                            </div>

                            <div className="flex items-center gap-3 ml-auto sm:ml-0">
                              {isPending ? (
                                <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 dark:bg-amber-500/20 px-2.5 py-1 text-xs font-semibold text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-500/30">
                                  <Clock className="w-3 h-3" /> Chờ HR duyệt
                                </span>
                              ) : (
                                <div className="flex items-center gap-2.5">
                                  <span
                                    className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold border ${
                                      isPass
                                        ? 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 border-emerald-200 dark:border-emerald-500/30'
                                        : 'bg-red-50 dark:bg-red-500/20 text-red-700 dark:text-red-400 border-red-200 dark:border-red-500/30'
                                    }`}
                                  >
                                    {isPass ? <CheckCircle2 className="w-3 h-3" /> : <XCircle className="w-3 h-3" />}
                                    {isPass ? 'Đạt' : 'Không đạt'}
                                  </span>

                                  <div className={`px-2.5 py-0.5 rounded-lg font-display text-xs font-extrabold flex items-center gap-0.5 ${getScoreBgColor(overallScore)} ${getScoreTextColor(overallScore)}`}>
                                    <span>{overallScore}</span>
                                    <span className="text-[9px] opacity-70">/100</span>
                                  </div>
                                </div>
                              )}

                              <button
                                onClick={(e) => { e.stopPropagation(); handleOpenDetail(evaluation.id) }}
                                className="flex items-center gap-1 px-2.5 py-1 rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-xs font-medium text-ink-700 dark:text-ink-200 group-hover:bg-brand-600 group-hover:text-white group-hover:border-brand-600 transition-all"
                              >
                                <Eye className="w-3.5 h-3.5" /> Xem chi tiết
                              </button>
                            </div>
                          </div>
                        )
                      })}
                    </div>
                  )}
                </motion.div>
              )
            })}
          </div>
        )}

        {!loading && !displayError && groupedSessions.length > 0 && (
          <Pagination
            page={page}
            totalPages={totalGroupPages}
            total={groupedSessions.length}
            label="ca thi"
            onPageChange={handlePageChange}
          />
        )}
      </main>
    )
  }

  // Detail view
  return (
    <>
      <header className="sticky top-0 z-20 flex items-center gap-2 border-b border-ink-200 dark:border-white/10 bg-white/80 dark:bg-white/5 backdrop-blur px-4 sm:px-6 h-14 sm:h-16">
        <button
          onClick={closeDetail}
          className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div className="flex min-w-0 flex-1 items-center gap-2 text-sm text-ink-400">
          <Link to="#" onClick={closeDetail} className="shrink-0 hover:text-brand-600 dark:text-brand-400">
            {t('evaluations')}
          </Link>
          <ChevronRight className="h-4 w-4 shrink-0" />
          <span className="truncate text-ink-600 dark:text-ink-300 font-medium">
            {selectedEvaluation.candidateName} · {selectedEvaluation.jobTitle}
          </span>
        </div>
        {/* Đã gỡ nút chuông ở đây: không có onClick, chấm đỏ vẽ cứng (luôn hiện dù không có
            thông báo nào), và trùng với chuông THẬT trên thanh header của layout ngay phía trên. */}
      </header>

      <main className="p-4 sm:p-6 grid gap-4 sm:gap-6 lg:grid-cols-[1fr_360px]">
        {/* LEFT: report */}
        <div className="space-y-6">
          {/* Candidate header */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="grid h-14 w-14 place-items-center rounded-2xl bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 font-display text-lg font-extrabold">
                  {getInitials(selectedEvaluation.candidateName)}
                </div>
                <div>
                  <h1 className="font-display text-xl font-extrabold leading-snug text-ink-900 dark:text-white">
                    {selectedEvaluation.candidateName}
                  </h1>
                  <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
                    <span className="inline-flex items-center gap-1.5">
                      <Code2 className="w-4 h-4" /> {selectedEvaluation.jobTitle}
                    </span>
                    <span className="inline-flex items-center gap-1.5">
                      <Layers className="w-4 h-4" /> {t('round')} {selectedEvaluation.roundNumber}
                    </span>
                    <span className="inline-flex items-center gap-1.5">
                      <Calendar className="w-4 h-4" /> {formatDate(selectedEvaluation.createdAt)}
                    </span>
                  </div>
                </div>
              </div>
              {selectedEvaluation.status === 'pending' ? (
                <span className="inline-flex items-center gap-2 rounded-full bg-amber-50 dark:bg-amber-500/20 px-3 py-1.5 text-sm font-semibold text-amber-700 dark:text-amber-400 ring-1 ring-amber-200 dark:ring-amber-500/30">
                  <Clock className="w-4 h-4" /> {t('status.pendingHrReview')}
                </span>
              ) : (
                <span
                  className={`inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-sm font-semibold ${
                    (selectedEvaluation.overallScore ?? 0) >= 80
                      ? 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                      : (selectedEvaluation.overallScore ?? 0) >= 60
                        ? 'bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                        : 'bg-amber-50 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'
                  }`}
                >
                  {verdictLabel(selectedEvaluation.finalVerdict ?? selectedEvaluation.aiVerdict)}
                </span>
              )}
            </div>
          </div>

          {/* Scores */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
            <h2 className="font-display text-lg font-bold mb-4 text-ink-900 dark:text-white">
              {t('criterionScores')}
            </h2>
            <div className="space-y-4">
              {criterionEntries.length > 0 ? (
                criterionEntries.map(([criterion, score]) => (
                  <div key={criterion}>
                    <div className="mb-1 flex justify-between text-sm">
                      <span className="text-ink-600 dark:text-ink-400">{criterion}</span>
                      <span className="font-semibold text-ink-900 dark:text-white">{score}</span>
                    </div>
                    <div className="h-2 rounded-full bg-ink-100 dark:bg-white/10">
                      <div
                        className={`h-full rounded-full ${getScoreColor(score)}`}
                        style={{ width: `${score}%` }}
                      />
                    </div>
                  </div>
                ))
              ) : (
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('noCriterionData')}</p>
              )}
            </div>

            {/* Đánh giá ngôn ngữ: API trả 6 chỉ số nhưng màn này trước đây chỉ vẽ 3
                (CEFR / trôi chảy / từ vựng), bỏ mất ngữ pháp, khả năng nghe hiểu và điểm chung. */}
            {selectedEvaluation.languageAssessment && (
              <div className="mt-6 rounded-xl border border-ai-200 dark:border-ai-500/30 bg-ai-50/50 dark:bg-ai-500/10 p-4">
                <div className="flex items-center gap-2 text-sm font-semibold text-ai-700 dark:text-ai-400">
                  <Languages className="w-4 h-4" />
                  {t('languageAssessment')} ({selectedEvaluation.languageAssessment.language})
                </div>
                <div className="mt-3 grid grid-cols-3 gap-2 text-center text-xs sm:gap-3 sm:text-sm">
                  {[
                    { label: t('cefr'), value: selectedEvaluation.languageAssessment.cefrLevel },
                    { label: t('languageOverall'), value: selectedEvaluation.languageAssessment.overallScore },
                    { label: t('fluency'), value: selectedEvaluation.languageAssessment.fluency },
                    { label: t('grammar'), value: selectedEvaluation.languageAssessment.grammar },
                    { label: t('vocabulary'), value: selectedEvaluation.languageAssessment.vocabulary },
                    { label: t('comprehension'), value: selectedEvaluation.languageAssessment.comprehension },
                  ].map((metric) => (
                    <div
                      key={metric.label}
                      className="min-w-0 rounded-lg bg-white dark:bg-white/10 p-2"
                    >
                      <div className="truncate font-display text-base font-extrabold text-ink-900 dark:text-white sm:text-lg">
                        {metric.value ?? '—'}
                      </div>
                      <div className="text-xs text-ink-400">{metric.label}</div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* Nhận định tổng của AI — `reasoning` vốn có trong API nhưng chưa bao giờ được hiện,
              trong khi đây là phần giải thích VÌ SAO ra verdict đó. */}
          {selectedEvaluation.reasoning && (
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
              <h2 className="font-display text-lg font-bold mb-3 text-ink-900 dark:text-white">
                {t('aiReasoning')}
              </h2>
              <p className="whitespace-pre-wrap text-sm leading-6 text-ink-600 dark:text-ink-300">
                {selectedEvaluation.reasoning}
              </p>
            </div>
          )}

          {/* Tín hiệu gian lận (ADR-054) — hạ tầng đã ghi nhận thật từ Kiosk nhưng màn duyệt
              chưa hề hiển thị, nên nhân sự duyệt mà không biết buổi thi có bất thường hay không. */}
          {(selectedEvaluation.cheatScore != null ||
            (selectedEvaluation.cheatSignals && selectedEvaluation.cheatSignals.length > 0)) && (
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
              <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                <h2 className="font-display text-lg font-bold text-ink-900 dark:text-white">
                  {t('integritySignals')}
                </h2>
                {selectedEvaluation.cheatScore != null && (
                  <span
                    className={`rounded-full px-3 py-1 text-xs font-semibold ${
                      selectedEvaluation.cheatScore >= 60
                        ? 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400'
                        : selectedEvaluation.cheatScore >= 30
                          ? 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'
                          : 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                    }`}
                  >
                    {t('cheatScore')}: {selectedEvaluation.cheatScore}/100
                  </span>
                )}
              </div>
              {selectedEvaluation.cheatSignals && selectedEvaluation.cheatSignals.length > 0 ? (
                <ul className="space-y-2">
                  {selectedEvaluation.cheatSignals.map((signal, index) => (
                    <li
                      key={`${signal.type}-${index}`}
                      className="flex items-start gap-2 rounded-lg bg-ink-50 dark:bg-white/5 p-3 text-sm"
                    >
                      <AlertTriangle
                        className={`mt-0.5 h-4 w-4 shrink-0 ${
                          signal.severity === 'high'
                            ? 'text-red-500'
                            : signal.severity === 'medium'
                              ? 'text-amber-500'
                              : 'text-ink-400'
                        }`}
                      />
                      <span className="text-ink-600 dark:text-ink-300">{signal.description}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('noCheatSignals')}</p>
              )}
            </div>
          )}

          {/* Phân tích từng câu. Trước đây chỉ hiện `analysis` — bỏ mất chính CÂU TRẢ LỜI của
              ứng viên và `feedback`, tức nhân sự đọc nhận xét mà không biết ứng viên đã nói gì.
              Điểm để `null` (không phải 0) khi AI không chấm được — xem ADR-053. */}
          {selectedEvaluation.questionAnalyses &&
            selectedEvaluation.questionAnalyses.length > 0 && (
              <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
                <h2 className="font-display text-lg font-bold mb-4 text-ink-900 dark:text-white">
                  {t('questionAnalysis')}
                </h2>
                <div className="space-y-3">
                  {selectedEvaluation.questionAnalyses.map((item, index) => {
                    const hasScore = typeof item.score === 'number'
                    return (
                      <details
                        key={`${item.question}-${index}`}
                        className="group rounded-xl border border-ink-200 dark:border-white/10"
                        open={index === 0}
                      >
                        <summary className="flex cursor-pointer items-start justify-between gap-3 px-4 py-3">
                          <span className="flex min-w-0 items-start gap-2 text-sm font-medium text-ink-800 dark:text-ink-200">
                            <span className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-full bg-ink-100 dark:bg-white/10 text-xs">
                              {index + 1}
                            </span>
                            <span className="min-w-0">{item.question}</span>
                          </span>
                          <span className="flex shrink-0 items-center gap-2">
                            {hasScore ? (
                              <span
                                className={`rounded-full px-2 py-0.5 text-xs font-semibold ${getScoreBgColor(item.score)} ${getScoreTextColor(item.score)}`}
                              >
                                {item.score}/10
                              </span>
                            ) : (
                              <span className="rounded-full bg-ink-100 dark:bg-white/10 px-2 py-0.5 text-xs font-semibold text-ink-500 dark:text-ink-400">
                                {t('notScored')}
                              </span>
                            )}
                            <ChevronDown className="w-4 h-4 text-ink-400 group-open:rotate-180 transition" />
                          </span>
                        </summary>
                        <div className="space-y-3 border-t border-ink-100 dark:border-white/10 px-4 py-3 text-sm">
                          <div>
                            <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-ink-400">
                              {t('candidateAnswer')}
                            </div>
                            {item.answer?.trim() ? (
                              <p className="whitespace-pre-wrap rounded-lg bg-ink-50 dark:bg-white/5 p-3 text-ink-700 dark:text-ink-300">
                                {item.answer}
                              </p>
                            ) : (
                              <p className="italic text-ink-400">{t('noAnswer')}</p>
                            )}
                          </div>
                          {item.analysis && (
                            <div>
                              <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-ink-400">
                                {t('aiComment')}
                              </div>
                              <p className="text-ink-600 dark:text-ink-300">{item.analysis}</p>
                            </div>
                          )}
                          {item.feedback && (
                            <div>
                              <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-ink-400">
                                {t('improvementNote')}
                              </div>
                              <p className="text-ink-600 dark:text-ink-300">{item.feedback}</p>
                            </div>
                          )}
                        </div>
                      </details>
                    )
                  })}
                </div>
              </div>
            )}

          {/* Bản ghi hình buổi phỏng vấn thật (ADR-052) — tự xoá khi hết hạn lưu.
              Trước đây khối này là placeholder chết: nút Play và link "xem transcript" đều
              không gắn onClick, nên nhân sự bấm mãi không ra gì. */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
            <h2 className="font-display text-lg font-bold mb-4 text-ink-900 dark:text-white">
              {t('recordingTranscript')}
            </h2>
            {selectedEvaluation.recordingUrl ? (
              <>
                <video
                  src={resolveAssetUrl(selectedEvaluation.recordingUrl)}
                  controls
                  className="aspect-video w-full rounded-xl bg-ink-900"
                />
                {selectedEvaluation.recordingExpiresAt && (
                  <p className="mt-2 text-xs text-ink-500 dark:text-ink-400">
                    {t('recordingExpiresAt', {
                      date: new Date(selectedEvaluation.recordingExpiresAt).toLocaleString(),
                    })}
                  </p>
                )}
              </>
            ) : (
              <div className="aspect-video rounded-xl bg-ink-100 dark:bg-white/5 grid place-items-center px-6 text-center text-sm text-ink-500 dark:text-ink-400">
                {selectedEvaluation.recordingDeletedAt
                  ? t('recordingDeleted', {
                      date: new Date(selectedEvaluation.recordingDeletedAt).toLocaleDateString(),
                    })
                  : t('recordingNone')}
              </div>
            )}
          </div>
        </div>

        {/* RIGHT: verdict & decision */}
        <aside className="space-y-5 xl:sticky xl:top-24 self-start">
          {/* Verdict AI. Trước đây badge LUÔN xanh lá kèm dấu tick bất kể AI chấm gì — hồ sơ
              "Không đạt" vẫn hiện dấu tick xanh; và dòng đề xuất luôn ghi cứng "mời vòng N+1"
              dù API đã trả `recommendedNextStep`. */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card text-center">
            <div className="flex items-center justify-center gap-2 text-sm font-semibold text-ai-700 dark:text-ai-400">
              {t('aiVerdict')}
            </div>
            <div
              className={`mt-3 inline-flex items-center gap-2 rounded-full px-4 py-1.5 text-base font-bold ring-1 ${
                aiPassed
                  ? 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 ring-emerald-200 dark:ring-emerald-500/30'
                  : 'bg-red-50 dark:bg-red-500/20 text-red-700 dark:text-red-400 ring-red-200 dark:ring-red-500/30'
              }`}
            >
              {aiPassed ? <CheckCircle2 className="w-5 h-5" /> : <XCircle className="w-5 h-5" />}
              {verdictLabel(selectedEvaluation.aiVerdict)}
            </div>
            <div className="mt-4 font-display text-4xl sm:text-5xl font-extrabold leading-none text-ink-900 dark:text-white">
              {/* Chưa chấm thì hiện "—", không phải 0 — 0 điểm là một kết quả khác hẳn (ADR-053). */}
              {selectedEvaluation.overallScore ?? '—'}
              <span className="text-lg text-ink-400">/100</span>
            </div>
            {selectedEvaluation.recommendedNextStep && (
              <p className="mt-3 text-sm text-ink-500 dark:text-ink-400">
                {t('recommendation')}:{' '}
                <b className="text-ink-700 dark:text-ink-200">
                  {selectedEvaluation.recommendedNextStep}
                </b>
              </p>
            )}
          </div>

          {/* Quyết định của nhân sự */}
          {!selectedEvaluation.hrReview ? (
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 sm:p-6 shadow-card">
              <h3 className="font-display font-bold text-ink-900 dark:text-white">
                {t('hrDecision')}
              </h3>
              <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">{t('hrDecisionHint')}</p>

              <div className="mt-4 grid grid-cols-2 gap-2">
                <button
                  onClick={handleConfirm}
                  disabled={submittingAction !== null}
                  className={`rounded-xl border-2 px-3 py-2.5 text-sm font-semibold transition ${submittingAction === 'confirm' ? 'border-brand-500 bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400' : 'border-emerald-500 bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400 hover:bg-emerald-100'} disabled:opacity-50`}
                >
                  {submittingAction === 'confirm' ? t('confirming') : t('confirmAi')}
                </button>
                <button
                  onClick={toggleOverrideMode}
                  disabled={submittingAction !== null}
                  className={`rounded-xl border-2 px-3 py-2.5 text-sm font-semibold transition ${isOverrideMode ? 'border-amber-500 bg-amber-50 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400' : 'border-ink-200 dark:border-white/10 text-ink-600 dark:text-ink-300 hover:border-ink-300'} disabled:opacity-50`}
                >
                  {t('override')}
                </button>
              </div>

              <AnimatePresence>
                {isOverrideMode && (
                  <motion.div
                    initial={{ opacity: 0, height: 0 }}
                    animate={{ opacity: 1, height: 'auto' }}
                    exit={{ opacity: 0, height: 0 }}
                    className="mt-4 space-y-3 overflow-hidden"
                  >
                    <div>
                      <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                        {t('overrideVerdict')}
                      </label>
                      {/* Hai nút này trước đây KHÔNG có onClick và "Đạt" luôn tô như đang chọn,
                          trong khi service tự lật ngược verdict của AI — nhân sự bấm "Đạt" nhưng
                          thứ gửi đi lại là kết quả ngược với AI. Nay là lựa chọn thật, mặc định
                          đặt sẵn ở phía ngược với AI (vì đó mới là lý do người ta bấm Ghi đè). */}
                      <div className="grid grid-cols-2 gap-2">
                        <button
                          type="button"
                          onClick={() => setOverrideVerdict('pass')}
                          className={`rounded-lg border px-3 py-2 text-sm font-medium transition ${
                            overrideVerdict === 'pass'
                              ? 'border-emerald-300 bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                              : 'border-ink-200 dark:border-white/10 text-ink-600 dark:text-ink-300 hover:border-emerald-300'
                          }`}
                        >
                          {t('verdictPass')}
                        </button>
                        <button
                          type="button"
                          onClick={() => setOverrideVerdict('not_pass')}
                          className={`rounded-lg border px-3 py-2 text-sm font-medium transition ${
                            overrideVerdict === 'not_pass'
                              ? 'border-red-300 bg-red-50 dark:bg-red-500/20 text-red-700 dark:text-red-400'
                              : 'border-ink-200 dark:border-white/10 text-ink-600 dark:text-ink-300 hover:border-red-300'
                          }`}
                        >
                          {t('verdictNotPass')}
                        </button>
                      </div>
                    </div>
                    <div>
                      <label className="mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300">
                        {t('overrideReason')} <span className="text-red-500">*</span>
                      </label>
                      <textarea
                        rows={3}
                        value={overrideReason}
                        onChange={(e) => setOverrideReason(e.target.value)}
                        placeholder={t('overrideReasonPlaceholder')}
                        className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-100 dark:focus:ring-brand-500/30"
                      />
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>

              {actionError && (
                <div className="mt-4 rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-400">
                  {actionError}
                </div>
              )}

              {isOverrideMode && (
                <button
                  onClick={handleOverride}
                  disabled={submittingAction !== null || !overrideReason.trim()}
                  className="mt-4 w-full rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-brand-700 disabled:opacity-50"
                >
                  {submittingAction === 'override' ? t('confirming') : t('saveAndSendResult')}
                </button>
              )}
            </div>
          ) : (
            <div className="rounded-2xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50 dark:bg-emerald-500/10 p-4 sm:p-6 shadow-card">
              <h3 className="font-display font-bold text-emerald-800 dark:text-emerald-400">
                {t('confirmed')}
              </h3>
              <p className="mt-1 text-sm text-emerald-700 dark:text-emerald-400">
                {t('verdict')}: <b>{verdictLabel(selectedEvaluation.hrReview.finalVerdict)}</b>
              </p>
              {selectedEvaluation.hrReview.isOverride &&
                selectedEvaluation.hrReview.overrideReason && (
                  <div className="mt-3 p-3 rounded-lg bg-white dark:bg-white/10 text-sm text-ink-600 dark:text-ink-300">
                    <b>{t('overrideReason')}:</b> {selectedEvaluation.hrReview.overrideReason}
                  </div>
                )}
            </div>
          )}

          {/* Điểm khớp CV-JD (ADR-030). Trước đây thẻ này vẽ CỨNG số 87 và thanh w-[87%] cho
              MỌI hồ sơ — một con số bịa đặt trình bày như dữ liệu thật. Nay lấy từ bản phân tích
              Gemini gắn với hồ sơ; hồ sơ chưa có phân tích thì ẩn thẻ thay vì bịa số. */}
          {selectedEvaluation.cvMatchScore != null && (
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
              <div className="flex items-center justify-between text-sm">
                <span className="flex items-center gap-1.5 font-semibold text-ai-700 dark:text-ai-400">
                  {t('cvJdMatch')}
                </span>
                <span className="font-display text-xl font-extrabold text-ai-700 dark:text-ai-400">
                  {selectedEvaluation.cvMatchScore}
                </span>
              </div>
              <div className="mt-2 h-2 rounded-full bg-ai-100 dark:bg-white/10">
                <div
                  className="h-full rounded-full bg-gradient-to-r from-brand-600 to-ai-600"
                  style={{ width: `${Math.min(100, Math.max(0, selectedEvaluation.cvMatchScore))}%` }}
                />
              </div>
              {selectedEvaluation.cvMatchSummary && (
                <p className="mt-3 text-xs leading-5 text-ink-500 dark:text-ink-400">
                  {selectedEvaluation.cvMatchSummary}
                </p>
              )}
            </div>
          )}
        </aside>
      </main>
    </>
  )
}
