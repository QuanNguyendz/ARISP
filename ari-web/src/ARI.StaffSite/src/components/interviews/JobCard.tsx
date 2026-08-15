import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { Briefcase, Calendar, CheckCircle2, ChevronDown, ChevronRight, Eye, Plus, Users } from 'lucide-react'
import { interviewService, type InterviewJobSummary } from '@ari/shared/fservices/interview'
import { SlotCard } from './SlotCard'
import { interviewKeys } from './interviewQueryKeys'
import { fmtDate, fmtTime, isSameDay } from './format'
import { INTERVIEWS_NS, type WorkspaceConfig } from './workspaceConfig'

export function JobCard({
  job,
  dateFilter,
  workspace,
}: {
  job: InterviewJobSummary
  dateFilter: string
  workspace: WorkspaceConfig
}) {
  const { t } = useTranslation(INTERVIEWS_NS)
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)

  // Nạp khi thẻ mở HOẶC khi đang lọc theo ngày (bộ lọc cần biết ca của mọi tin đang hiển thị).
  // Trước đây là useState + guard `slots.length === 0`, nên thu gọn rồi mở lại KHÔNG nạp lại và
  // sức chứa sửa ở nơi khác không bao giờ về tới đây.
  const { data: slots = [], isLoading } = useQuery({
    queryKey: interviewKeys.slots(job.jobId),
    queryFn: () => interviewService.getSlotsForJob(job.jobId),
    enabled: (open || !!dateFilter) && job.totalSlots > 0,
    staleTime: 30_000,
  })

  const filteredSlots = useMemo(
    () => (dateFilter ? slots.filter((s) => isSameDay(s.startTime, dateFilter)) : slots),
    [slots, dateFilter]
  )

  const slotsByRound = useMemo(() => {
    const map = new Map<number, typeof filteredSlots>()
    filteredSlots.forEach((s) => {
      const arr = map.get(s.roundNumber) ?? []
      arr.push(s)
      map.set(s.roundNumber, arr)
    })
    return map
  }, [filteredSlots])

  const isActive = job.jobStatus === 'active' || job.jobStatus === 'published'

  return (
    <motion.div
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card overflow-hidden"
    >
      <div
        onClick={() => setOpen((v) => !v)}
        role="button"
        tabIndex={0}
        className="w-full flex items-center justify-between p-5 lg:p-6 text-left hover:bg-ink-50/50 dark:hover:bg-white/5 transition-colors cursor-pointer"
      >
        <div className="flex items-center gap-4 min-w-0">
          <div className="w-12 h-12 shrink-0 rounded-xl bg-gradient-to-br from-brand-600 to-ai-600 flex items-center justify-center shadow-md">
            <Briefcase className="w-5 h-5 text-white" />
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap mb-1">
              <h3 className="text-base font-bold text-ink-900 dark:text-white truncate">{job.jobTitle}</h3>
              <span
                className={`px-2.5 py-0.5 rounded-full text-[11px] font-semibold ${
                  isActive
                    ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                    : 'bg-ink-100 text-ink-500 dark:bg-white/10 dark:text-ink-400'
                }`}
              >
                {isActive ? t('job.recruiting') : t('job.closed')}
              </span>
              {job.maxRound > 0 && (
                <span className="px-2 py-0.5 rounded-md text-[11px] font-medium bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400">
                  {t('job.round', { number: job.maxRound })}
                </span>
              )}
            </div>
            <div className="flex items-center gap-4 flex-wrap">
              <span className="text-xs text-ink-500 dark:text-ink-400 flex items-center gap-1 font-medium">
                <Calendar className="w-3.5 h-3.5" /> {t('job.slots', { count: job.totalSlots })}
              </span>
              <span
                className="text-xs text-ink-500 dark:text-ink-400 flex items-center gap-1 font-medium"
                title={t('job.bookedTitle')}
              >
                <Users className="w-3.5 h-3.5" /> {t('job.booked', { count: job.totalBooked })}
              </span>
              <span className="text-xs text-emerald-600 dark:text-emerald-400 flex items-center gap-1 font-semibold">
                <CheckCircle2 className="w-3.5 h-3.5" /> {t('job.confirmed', { count: job.totalConfirmed })}
              </span>
              {job.nextSlotTime && (
                <span className="text-xs text-brand-600 dark:text-brand-400 font-semibold">
                  {t('job.nextSlot', {
                    when: `${fmtDate(job.nextSlotTime)} ${fmtTime(job.nextSlotTime)}`,
                  })}
                </span>
              )}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0 ml-4">
          <button
            onClick={(e) => {
              e.stopPropagation()
              navigate(workspace.jobHref(job.jobId))
            }}
            className="hidden sm:flex items-center gap-1 px-3 py-1.5 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-xs font-medium text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors"
          >
            <Eye className="w-3.5 h-3.5" /> {t('job.manage')}
          </button>
          {open ? <ChevronDown className="w-5 h-5 text-ink-400" /> : <ChevronRight className="w-5 h-5 text-ink-400" />}
        </div>
      </div>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
          >
            <div className="border-t border-ink-100 dark:border-white/10 p-4 lg:p-5 bg-ink-50/50 dark:bg-white/[0.02]">
              {isLoading ? (
                <p className="text-sm text-ink-400 text-center py-6">{t('job.loadingSlots')}</p>
              ) : job.totalSlots === 0 ? (
                <div className="text-center py-8 bg-white dark:bg-white/5 rounded-2xl border border-dashed border-ink-200 dark:border-white/10 p-6">
                  <Calendar className="w-8 h-8 text-ink-300 dark:text-white/20 mx-auto mb-2" />
                  <p className="text-sm font-semibold text-ink-700 dark:text-ink-200">
                    {t('job.noSlotsTitle')}
                  </p>
                  <p className="text-xs text-ink-400 mt-1 mb-4">{t('job.noSlotsHint')}</p>
                  <button
                    onClick={() => navigate(workspace.jobHref(job.jobId))}
                    className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-brand-600 hover:bg-brand-700 text-white text-xs font-semibold transition-colors shadow-sm"
                  >
                    <Plus className="w-4 h-4" /> {t('job.createSlot')}
                  </button>
                </div>
              ) : filteredSlots.length === 0 ? (
                <div className="text-center py-6 text-xs text-ink-400">
                  {t('job.noSlotsOnDate')}{' '}
                  <span className="font-semibold text-ink-700 dark:text-white">{dateFilter}</span>
                </div>
              ) : (
                <div className="space-y-6">
                  {[...slotsByRound.entries()]
                    .sort(([a], [b]) => a - b)
                    .map(([round, rSlots]) => (
                      <div key={round}>
                        <p className="text-xs font-bold text-ink-500 dark:text-ink-400 uppercase tracking-wider mb-3">
                          {t('job.roundGroup', { round, count: rSlots.length })}
                        </p>
                        <div className="space-y-3">
                          {rSlots.map((slot) => (
                            <SlotCard key={slot.slotId} slot={slot} jobId={job.jobId} workspace={workspace} />
                          ))}
                        </div>
                      </div>
                    ))}
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  )
}
