import { useCallback, useEffect, useMemo, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  ArrowLeft,
  CalendarPlus,
  Clock,
  Loader2,
  Plus,
  Minus,
  Trash2,
  Users,
  AlertCircle,
} from 'lucide-react'
import { ErrorAlert } from '@ari/shared/ui'
import jobService from '@ari/shared/fservices/job'
import { scheduleService } from '@ari/shared/fservices/schedule'
import type { JobPosting, AvailabilitySlot } from '@ari/shared/types/job'

function errMsg(e: unknown, fallback: string): string {
  const x = e as { response?: { data?: { message?: string } } }
  return x?.response?.data?.message || fallback
}

function fmtRange(s: AvailabilitySlot): string {
  const start = new Date(s.startTime)
  const end = new Date(s.endTime)
  const d = start.toLocaleDateString(undefined, {
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
  })
  const t = (x: Date) => x.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
  return `${d} · ${t(start)} – ${t(end)}`
}

export default function JobScheduleConfigPage() {
  const { t } = useTranslation('modules/recruiter/scheduleConfig')
  const { id: jobId } = useParams<{ id: string }>()
  const [job, setJob] = useState<JobPosting | null>(null)
  const [round, setRound] = useState(1)
  const [slots, setSlots] = useState<AvailabilitySlot[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const [date, setDate] = useState('')
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [capacity, setCapacity] = useState(1)

  const rounds = useMemo(() => job?.roundConfigs ?? [], [job])

  const loadSlots = useCallback(
    async (r: number) => {
      if (!jobId) return
      try {
        setLoading(true)
        const data = await scheduleService.getSlots(jobId, r)
        setSlots(data)
      } catch (e) {
        setError(errMsg(e, t('slotsLoadError')))
      } finally {
        setLoading(false)
      }
    },
    [jobId, t]
  )

  useEffect(() => {
    if (!jobId) return
    ;(async () => {
      try {
        const j = await jobService.getJobPostingById(jobId)
        setJob(j)
      } catch (e) {
        setError(errMsg(e, t('loadError')))
      }
    })()
  }, [jobId, t])

  useEffect(() => {
    void loadSlots(round)
  }, [round, loadSlots])

  const addSlot = async () => {
    if (!jobId) return
    if (!date || !start || !end) {
      setError(t('validation.fillAllFields'))
      return
    }
    const startIso = new Date(`${date}T${start}`).toISOString()
    const endIso = new Date(`${date}T${end}`).toISOString()
    if (new Date(endIso) <= new Date(startIso)) {
      setError(t('validation.endAfterStart'))
      return
    }
    setBusy(true)
    setError('')
    try {
      await scheduleService.createSlot({
        jobPostingId: jobId,
        roundNumber: round,
        startTime: startIso,
        endTime: endIso,
        capacity,
      })
      setDate('')
      setStart('')
      setEnd('')
      setCapacity(1)
      await loadSlots(round)
    } catch (e) {
      setError(errMsg(e, t('actions.createError')))
    } finally {
      setBusy(false)
    }
  }

  const changeCapacity = async (slot: AvailabilitySlot, delta: number) => {
    const next = slot.capacity + delta
    if (next < 1 || next < slot.bookedCount) return
    try {
      const updated = await scheduleService.updateSlotCapacity(slot.id, next)
      setSlots((prev) => prev.map((s) => (s.id === slot.id ? updated : s)))
    } catch (e) {
      setError(errMsg(e, t('actions.capacityError')))
    }
  }

  const removeSlot = async (slot: AvailabilitySlot) => {
    try {
      await scheduleService.deleteSlot(slot.id)
      setSlots((prev) => prev.filter((s) => s.id !== slot.id))
    } catch (e) {
      setError(errMsg(e, t('actions.deleteError')))
    }
  }

  const roundLabel = (num: number, type?: string) =>
    t('roundLabel', { number: num, type: type === 'technical' ? t('technical') : t('screening') })

  const isHr = typeof window !== 'undefined' && window.location.pathname.startsWith('/hr')
  const backUrl = jobId
    ? isHr
      ? `/hr/jobs/${jobId}`
      : `/recruiter/my-jobs/${jobId}`
    : isHr
    ? '/hr/jobs'
    : '/recruiter/my-jobs'

  return (
    <div className="p-6 lg:p-8">
      <Link
        to={backUrl}
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('back')}
      </Link>

      <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="mb-6">
        <h1 className="flex items-center gap-2 text-2xl font-bold text-ink-900 dark:text-white">
          <CalendarPlus className="h-6 w-6 text-brand-600 dark:text-brand-400" /> {t('title')}
        </h1>
        <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
          {t('description', { jobTitle: job?.title || '' })}
        </p>
      </motion.div>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {/* Round selector */}
      {rounds.length > 1 && (
        <div className="mb-5 flex flex-wrap gap-2">
          {rounds.map((r) => (
            <button
              key={r.roundNumber}
              type="button"
              onClick={() => setRound(r.roundNumber)}
              className={`rounded-xl px-3.5 py-2 text-sm font-medium transition-colors ${
                round === r.roundNumber
                  ? 'bg-brand-600 text-white'
                  : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10'
              }`}
            >
              {roundLabel(r.roundNumber, r.roundType)}
            </button>
          ))}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Slot form */}
        <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
          <h2 className="mb-4 text-base font-semibold text-ink-900 dark:text-white flex items-center justify-between">
            <span>{t('addSlot')}</span>
            <span className="px-2.5 py-0.5 rounded-full text-xs font-bold bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400">
              Vòng {round}
            </span>
          </h2>
          <div className="space-y-3">
            <div>
              <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                {t('slotForm.date')}
              </label>
              <input
                type="date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
                className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white"
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                  {t('slotForm.startTime')}
                </label>
                <input
                  type="time"
                  value={start}
                  onChange={(e) => setStart(e.target.value)}
                  className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                  {t('slotForm.endTime')}
                </label>
                <input
                  type="time"
                  value={end}
                  onChange={(e) => setEnd(e.target.value)}
                  className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white"
                />
              </div>
            </div>
            <div>
              <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                {t('slotForm.capacity')}
              </label>
              <input
                type="number"
                min={1}
                value={capacity}
                onChange={(e) => setCapacity(Math.max(1, Number(e.target.value) || 1))}
                className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white"
              />
            </div>
            <button
              type="button"
              onClick={addSlot}
              disabled={busy}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
            >
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}{' '}
              {t('slotForm.add')}
            </button>
          </div>
        </div>

        {/* Slot list */}
        <div className="lg:col-span-2 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
          <div className="border-b border-ink-100 dark:border-white/10 px-5 py-4">
            <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
              <Clock className="h-5 w-5 text-brand-600 dark:text-brand-400" />{' '}
              {t('slotList.title', { round, count: slots.length })}
            </h2>
          </div>
          {loading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-brand-600 dark:text-brand-400" />
            </div>
          ) : slots.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-12 text-center">
              <AlertCircle className="h-8 w-8 text-ink-300" />
              <p className="text-sm text-ink-500 dark:text-ink-400">{t('slotList.empty')}</p>
            </div>
          ) : (
            <div className="divide-y divide-ink-100 dark:divide-white/10">
              {slots.map((s) => (
                <div key={s.id} className="flex items-center justify-between gap-3 px-5 py-3.5">
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-ink-900 dark:text-white">
                      {fmtRange(s)}
                    </p>
                    <p className="flex items-center gap-1 text-xs text-ink-500 dark:text-ink-400">
                      <Users className="h-3 w-3" />{' '}
                      {t('slotList.booked', { booked: s.bookedCount, capacity: s.capacity })}
                      {s.bookedCount >= s.capacity && (
                        <span className="ml-1 rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-500/20 dark:text-amber-400">
                          {t('slotList.full')}
                        </span>
                      )}
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-1">
                    <button
                      type="button"
                      onClick={() => changeCapacity(s, -1)}
                      disabled={s.capacity <= 1 || s.capacity <= s.bookedCount}
                      className="grid h-7 w-7 place-items-center rounded-lg border border-ink-200 dark:border-white/10 text-ink-500 hover:bg-ink-50 dark:hover:bg-white/10 disabled:opacity-40"
                      title={t('slotList.decreaseCapacity')}
                    >
                      <Minus className="h-3.5 w-3.5" />
                    </button>
                    <span className="w-6 text-center text-sm text-ink-700 dark:text-ink-200">
                      {s.capacity}
                    </span>
                    <button
                      type="button"
                      onClick={() => changeCapacity(s, 1)}
                      className="grid h-7 w-7 place-items-center rounded-lg border border-ink-200 dark:border-white/10 text-ink-500 hover:bg-ink-50 dark:hover:bg-white/10"
                      title={t('slotList.increaseCapacity')}
                    >
                      <Plus className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={() => removeSlot(s)}
                      disabled={s.bookedCount > 0}
                      className="ml-1 grid h-7 w-7 place-items-center rounded-lg text-ink-400 hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-500/10 disabled:opacity-40"
                      title={s.bookedCount > 0 ? t('slotList.cannotDelete') : t('slotList.delete')}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
