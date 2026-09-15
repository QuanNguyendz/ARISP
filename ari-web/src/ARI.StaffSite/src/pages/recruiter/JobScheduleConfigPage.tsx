import { useEffect, useMemo, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { roundTypeKey, isOnlineTestRound } from '@ari/shared/utils/roundTypes'
import {
  ArrowLeft,
  CalendarPlus,
  Clock,
  Loader2,
  Plus,
  Trash2,
  Users,
  AlertCircle,
  CalendarCheck,
  UserCheck,
  Pencil,
  Check,
  XCircle,
  Mail,
} from 'lucide-react'
import { ErrorAlert, TimeInput } from '@ari/shared/ui'
import { formatTime24 } from '@ari/shared/utils/time24'
import jobService from '@ari/shared/fservices/job'
import { scheduleService, type HmAvailabilityWindow } from '@ari/shared/fservices/schedule'
import { interviewKeys } from '@/components/interviews/interviewQueryKeys'
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
  return `${d} · ${formatTime24(start)} – ${formatTime24(end)}`
}

/**
 * Ca co nam TRON trong mot khung gio ranh cua Hiring Manager khong.
 *
 * Cung vi tu voi server (`HmAvailabilitySupport.IsCovered`): doi nam tron chu khong chi giao nhau —
 * ca 14:00–15:00 chong len khung ranh 14:00–14:15 nghia la HM phai roi phong giua buoi.
 */
/**
 * Hai khoang thoi gian co CHONG nhau khong. Bien trung nhau (14:00–15:00 roi 15:00–16:00) KHONG
 * tinh la chong — do la cach xep ca lien tiep binh thuong. Cung vi tu voi server.
 */
function overlaps(
  aStart: string | Date,
  aEnd: string | Date,
  bStart: string | Date,
  bEnd: string | Date
) {
  return (
    new Date(aStart).getTime() < new Date(bEnd).getTime() &&
    new Date(bStart).getTime() < new Date(aEnd).getTime()
  )
}

function isCoveredByHm(
  slotStart: string | Date,
  slotEnd: string | Date,
  windows: HmAvailabilityWindow[]
) {
  const s = new Date(slotStart).getTime()
  const e = new Date(slotEnd).getTime()
  return windows.some(
    (w) => new Date(w.startTime).getTime() <= s && new Date(w.endTime).getTime() >= e
  )
}

export default function JobScheduleConfigPage() {
  const { t } = useTranslation('modules/recruiter/scheduleConfig')
  const { id: jobId } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const [job, setJob] = useState<JobPosting | null>(null)
  const [round, setRound] = useState(1)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const [date, setDate] = useState('')
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  /**
  * Giới hạn số ứng viên mỗi ca — CHỈ vòng trắc nghiệm mới có ô này.
  *
  * Vòng hội thoại luôn bằng 1 (Hiring Manager ngồi cùng AI suốt buổi, ADR-067) nên không phải là
  * lựa chọn của người dùng. Vòng trắc nghiệm là bài thi trực tuyến: mặc định KHÔNG giới hạn —
  * chuỗi rỗng — và đặt được trần nếu muốn mở đợt thi nhỏ.
  */
  const [testLimit, setTestLimit] = useState('')

  const rounds = useMemo(() => job?.roundConfigs ?? [], [job])

  /** Vòng đang chọn có phải vòng trắc nghiệm không — quyết định luật sức chứa của cả màn này. */
  const isTestRound = useMemo(
    () => isOnlineTestRound(rounds.find((r) => r.roundNumber === round)?.roundType),
    [rounds, round]
  )

  /**
   * Ca THI trắc nghiệm đứng ngoài luật chồng giờ — cùng vị từ với server
   * (`SchedulingSupport.ValidateSlotTimeAsync`). Luật đó sinh ra vì Hiring Manager ngồi cùng mọi buổi
   * phỏng vấn; bài thi trực tuyến không ai ngồi cùng, nên một đợt thi cả ngày không được phép chặn ca
   * phỏng vấn vòng sau, và hai đợt thi gối nhau cũng không phải lỗi.
   */
  const testRoundNumbers = useMemo(
    () =>
      new Set(rounds.filter((r) => isOnlineTestRound(r.roundType)).map((r) => r.roundNumber)),
    [rounds]
  )

  // Qua react-query để realtime (ADR-057) làm mới được trang này khi người khác sửa khung giờ.
  const {
    data: slots = [],
    isLoading: loading,
    error: slotsError,
  } = useQuery({
    queryKey: ['schedule-slots', jobId, round],
    queryFn: () => scheduleService.getSlots(jobId!, round),
    enabled: !!jobId,
    staleTime: 30_000,
  })

  /**
   * TOÀN BỘ ca của tin (mọi vòng) — chỉ dùng để phát hiện ca trùng giờ.
   *
   * Vì sao không dùng lại `slots` đang hiển thị: danh sách đó lọc theo vòng đang xem, nên một ca
   * vòng 1 chồng lên ca vòng 2 sẽ KHÔNG bị phát hiện — mà một dấu hiệu "không sao" trong khi thực
   * tế có xung đột còn tệ hơn là không có dấu hiệu nào. Cùng phạm vi với server (cả tin).
   */
  const { data: allSlots = [] } = useQuery({
    queryKey: ['schedule-slots', jobId, 'all'],
    queryFn: () => scheduleService.getSlots(jobId!),
    enabled: !!jobId,
    staleTime: 30_000,
  })

  /** Ca tham gia luật chồng giờ: mọi ca trừ ca thi trắc nghiệm. */
  const clashCandidates = useMemo(
    () => allSlots.filter((x) => x.roundNumber == null || !testRoundNumbers.has(x.roundNumber)),
    [allSlots, testRoundNumbers]
  )

  /**
   * Lịch rảnh của Hiring Manager cho ĐÚNG vòng đang chọn (ADR-067).
   *
   * Vì sao màn này cần nó: ca Recruiter tạo ra chỉ dùng được nếu nằm trọn trong một khung như thế —
   * server chặn ở bước GÁN ca. Không hiện ở đây thì Recruiter tạo cả loạt ca theo phỏng đoán rồi
   * mới biết chúng vô dụng, lúc đang có ứng viên chờ xếp lịch.
   */
  const { data: hmWindows = [] } = useQuery({
    queryKey: ['hm-availability', jobId, round],
    queryFn: () => scheduleService.getHmAvailability(jobId!, round),
    enabled: !!jobId,
    staleTime: 30_000,
  })

  /**
   * Mọi thay đổi khung giờ đều phải chạm tới màn Phỏng vấn.
   *
   * Trước đây trang này chỉ splice kết quả vào state cục bộ và không gọi `useQueryClient` lần nào,
   * nên tăng sức chứa xong thì màn Phỏng vấn (và modal "Dời lịch" trong đó) vẫn hiện số cũ cho tới
   * khi tải lại toàn trang — đúng lỗi người dùng báo. Realtime cũng đã nối, nhưng invalidate ngay
   * tại đây khiến việc sửa trong CÙNG một tab hiện tức thì, không phụ thuộc đường realtime.
   */
  const syncScheduleCaches = () => {
    queryClient.invalidateQueries({ queryKey: ['schedule-slots', jobId] })
    if (jobId) queryClient.invalidateQueries({ queryKey: interviewKeys.slots(jobId) })
    queryClient.invalidateQueries({ queryKey: interviewKeys.jobs })
  }

  useEffect(() => {
    if (slotsError) setError(errMsg(slotsError, t('slotsLoadError')))
  }, [slotsError, t])

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

  /**
   * Ca đang gõ dở có nằm ngoài mọi khung giờ rảnh của HM không.
   *
   * Chỉ tính khi đã đủ ba ô VÀ tin thật sự có lịch của HM — chưa khai khung nào thì cảnh báo riêng
   * ở panel bên dưới đã nói rồi, nhắc thêm lần nữa chỉ là tiếng ồn.
   */
  const draftOutsideHm = useMemo(() => {
    // Vòng trắc nghiệm không có luật khớp giờ HM nên không có gì để cảnh báo.
    if (isTestRound) return false
    if (!date || !start || !end || hmWindows.length === 0) return false
    const startIso = new Date(`${date}T${start}`)
    const endIso = new Date(`${date}T${end}`)
    if (Number.isNaN(startIso.getTime()) || Number.isNaN(endIso.getTime())) return false
    if (endIso <= startIso) return false
    return !isCoveredByHm(startIso.toISOString(), endIso.toISOString(), hmWindows)
  }, [date, start, end, hmWindows, isTestRound])

  /**
   * Ca đang gõ dở có chồng giờ với ca nào đã tạo không — server chặn, nhưng nói trước thì người
   * dùng sửa ngay trong lúc gõ thay vì bấm rồi nhận lỗi.
   */
  const draftOverlapsSlot = useMemo(() => {
    if (isTestRound) return false
    if (!date || !start || !end) return false
    const s = new Date(`${date}T${start}`)
    const e = new Date(`${date}T${end}`)
    if (Number.isNaN(s.getTime()) || Number.isNaN(e.getTime()) || e <= s) return false
    return clashCandidates.some((x) => overlaps(s, e, x.startTime, x.endTime))
  }, [date, start, end, clashCandidates, isTestRound])

  /** Ca đang sửa giờ tại chỗ. `null` = không sửa ca nào. */
  const [editing, setEditing] = useState<{
    id: string
    date: string
    start: string
    end: string
  } | null>(null)

  const openEdit = (slot: AvailabilitySlot) => {
    const from = new Date(slot.startTime)
    const to = new Date(slot.endTime)
    const pad = (n: number) => String(n).padStart(2, '0')
    setError('')
    setEditing({
      id: slot.id,
      date: `${from.getFullYear()}-${pad(from.getMonth() + 1)}-${pad(from.getDate())}`,
      start: `${pad(from.getHours())}:${pad(from.getMinutes())}`,
      end: `${pad(to.getHours())}:${pad(to.getMinutes())}`,
    })
  }

  const saveEdit = async () => {
    if (!editing) return
    if (!editing.date || !editing.start || !editing.end) {
      setError(t('validation.fillAllFields'))
      return
    }
    const startIso = new Date(`${editing.date}T${editing.start}`).toISOString()
    const endIso = new Date(`${editing.date}T${editing.end}`).toISOString()
    if (new Date(endIso) <= new Date(startIso)) {
      setError(t('validation.endAfterStart'))
      return
    }
    setBusy(true)
    setError('')
    try {
      await scheduleService.updateSlotTime(editing.id, startIso, endIso)
      setEditing(null)
      syncScheduleCaches()
    } catch (e) {
      setError(errMsg(e, t('actions.updateTimeError')))
    } finally {
      setBusy(false)
    }
  }

  /** Điền sẵn ô nhập theo một khung giờ rảnh — tạo ca trùng khít khung đó bằng một cú bấm. */
  const applyHmWindow = (w: HmAvailabilityWindow) => {
    const from = new Date(w.startTime)
    const to = new Date(w.endTime)
    const pad = (n: number) => String(n).padStart(2, '0')
    setDate(`${from.getFullYear()}-${pad(from.getMonth() + 1)}-${pad(from.getDate())}`)
    setStart(`${pad(from.getHours())}:${pad(from.getMinutes())}`)
    // Khung rảnh có thể dài vài tiếng còn một ca thì ngắn: điền giờ kết thúc của khung để Recruiter
    // thấy biên trên, họ tự rút ngắn lại. Điền sẵn một độ dài tự nghĩ ra thì lại là đoán hộ.
    setEnd(`${pad(to.getHours())}:${pad(to.getMinutes())}`)
    setError('')
  }

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
        // Bỏ trống = để server quyết theo loại vòng. Không tự gửi 1 từ đây: luật sức chứa thuộc
        // về server, gửi kèm một con số đoán trước là dựng bản thứ hai của cùng một luật.
        capacity: isTestRound && testLimit.trim() !== '' ? Number(testLimit) : null,
      })
      setDate('')
      setStart('')
      setEnd('')
      syncScheduleCaches()
    } catch (e) {
      setError(errMsg(e, t('actions.createError')))
    } finally {
      setBusy(false)
    }
  }

  const changeCapacity = async (slot: AvailabilitySlot, delta: number) => {
    const next = (slot.capacity ?? 1) + delta
    if (next < 1 || next < slot.bookedCount) return
    try {
      await scheduleService.updateSlotCapacity(slot.id, next)
      syncScheduleCaches()
    } catch (e) {
      setError(errMsg(e, t('actions.capacityError')))
    }
  }

  const removeSlot = async (slot: AvailabilitySlot) => {
    try {
      await scheduleService.deleteSlot(slot.id)
      syncScheduleCaches()
    } catch (e) {
      setError(errMsg(e, t('actions.deleteError')))
    }
  }

  // Phân loại loại vòng dùng hàm DÙNG CHUNG. Bản trước ở đây là một phép chọn HAI nhánh
  // (`type === 'technical' ? ... : ...`) cho một tập BA giá trị, nên vòng trắc nghiệm hiện ra
  // thành "Sơ loại" — tab ghi `Vòng 1 · Sơ loại` trong khi tin khai `Vòng 1: Trắc nghiệm`.
  const roundLabel = (num: number, type?: string | null) =>
    t('roundLabel', { number: num, type: t(`roundTypes.${roundTypeKey(type)}`) })

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
        {/* Cột trái: ô nhập + lịch rảnh của HM. Gom trong MỘT thẻ lưới vì để rời hai thẻ thì thuật
            toán đặt tự động đẩy danh sách ca xuống hàng 2 và bỏ trống ô hàng 1 bên phải. */}
        <div className="space-y-6">
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
                {/* Ô giờ 24 tiếng tự dựng — ô `type="time"` gốc hiện SA/CH theo cài đặt vùng của
                    Windows, và người dùng đã xếp nhầm 23:30 → 00:01 vì đọc "11:30 CH → 12:01 SA". */}
                <div>
                  <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                    {t('slotForm.startTime')}
                  </label>
                  <TimeInput value={start} onChange={setStart} ariaLabel={t('slotForm.startTime')} />
                </div>
                <div>
                  <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                    {t('slotForm.endTime')}
                  </label>
                  <TimeInput value={end} onChange={setEnd} ariaLabel={t('slotForm.endTime')} />
                </div>
              </div>
              {/* Sức chứa TUỲ LOẠI VÒNG. Vòng hội thoại: luôn 1 vì Hiring Manager ngồi cùng AI
                suốt buổi (ADR-067) — không phải lựa chọn, nên chỉ nói một câu chứ không dựng ô nhập
                rồi để server từ chối. Vòng trắc nghiệm: bài thi trực tuyến, mặc định không giới hạn
                và đặt trần được. */}
              {isTestRound ? (
                <div>
                  <label className="mb-1 block text-xs font-medium text-ink-600 dark:text-ink-300">
                    {t('slotForm.testLimit')}
                  </label>
                  <input
                    type="number"
                    min={1}
                    value={testLimit}
                    onChange={(e) => setTestLimit(e.target.value)}
                    placeholder={t('slotForm.testLimitPlaceholder')}
                    className="w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white"
                  />
                  <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">
                    {t('slotForm.testLimitHint')}
                  </p>
                </div>
              ) : (
                <p className="rounded-xl border border-ink-100 bg-ink-50 px-3 py-2 text-xs text-ink-600 dark:border-white/5 dark:bg-white/5 dark:text-ink-300">
                  {t('slotForm.oneCandidatePerSlot')}
                </p>
              )}

              {/* Cảnh báo chứ KHÔNG chặn: ca vẫn được phép tạo trước khi Hiring Manager gửi lịch (màn
                tạo tin dựng ca từ lúc chưa có ai duyệt hồ sơ). Server mới là nơi chặn, và nó chặn ở
                bước GÁN ca — nên ở đây chỉ cần nói trước để khỏi tạo ra một loạt ca vô dụng. */}
              {draftOverlapsSlot && (
                <p className="flex items-start gap-1.5 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
                  <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <span>{t('validation.overlapsExisting')}</span>
                </p>
              )}

              {draftOutsideHm && (
                <p className="flex items-start gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/25 dark:bg-amber-500/10 dark:text-amber-300">
                  <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <span>{t('hmAvailability.outsideWarning')}</span>
                </p>
              )}
              <button
                type="button"
                onClick={addSlot}
                disabled={busy || draftOverlapsSlot}
                className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
              >
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}{' '}
                {t('slotForm.add')}
              </button>
            </div>
          </div>

          {/* Lịch rảnh của Hiring Manager — đặt NGAY DƯỚI ô nhập, vì đây là thứ Recruiter phải nhìn
            trong lúc gõ giờ, không phải một thông tin tra cứu ở đâu đó.

            ẨN HẴN ở vòng trắc nghiệm: không ai ngồi cùng ứng viên nên server cũng KHÔNG áp luật khớp
            giờ ở đó. Giữ panel lại thì nó báo "Hiring Manager chưa gửi khung giờ" cho một vòng không
            cần khung nào — Recruiter đi hỏi một thứ không tồn tại. */}
          {!isTestRound && (
          <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5">
            <h2 className="mb-1 flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
              <UserCheck className="h-5 w-5 text-brand-600 dark:text-brand-400" />
              {t('hmAvailability.title')}
            </h2>
            <p className="mb-3 text-xs text-ink-500 dark:text-ink-400">
              {t('hmAvailability.hint')}
            </p>

            {hmWindows.length === 0 ? (
              <p className="flex items-start gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/25 dark:bg-amber-500/10 dark:text-amber-300">
                <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <span>{t('hmAvailability.empty', { round })}</span>
              </p>
            ) : (
              <ul className="space-y-2">
                {hmWindows.map((w) => {
                  const from = new Date(w.startTime)
                  const to = new Date(w.endTime)
                  return (
                    <li
                      key={w.id}
                      className="rounded-xl border border-ink-100 px-3 py-2.5 dark:border-white/10"
                    >
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        {from.toLocaleDateString(undefined, {
                          weekday: 'short',
                          day: '2-digit',
                          month: '2-digit',
                        })}{' '}
                        ·{' '}
                        {formatTime24(from)} – {formatTime24(to)}
                      </p>
                      {(w.hiringManagerName || w.note) && (
                        <p className="mt-0.5 truncate text-xs text-ink-500 dark:text-ink-400">
                          {[w.hiringManagerName, w.note].filter(Boolean).join(' · ')}
                        </p>
                      )}
                      {/* Điền sẵn ô nhập theo đúng khung này — bước hay làm nhất là "tạo ca trùng
                        khít khung HM rảnh", nên cho làm bằng một cú bấm. */}
                      <button
                        type="button"
                        onClick={() => applyHmWindow(w)}
                        className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-brand-200 px-2.5 py-1 text-xs font-medium text-brand-700 hover:bg-brand-50 dark:border-brand-500/30 dark:text-brand-400 dark:hover:bg-brand-500/10"
                      >
                        <CalendarCheck className="h-3.5 w-3.5" /> {t('hmAvailability.use')}
                      </button>
                    </li>
                  )
                })}
              </ul>
            )}
          </div>
          )}
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
              {slots.map((s) => {
                const held = s.bookings ?? []
                const isEditing = editing?.id === s.id

                return (
                  <div key={s.id} className="px-5 py-3.5">
                    <div className="flex items-center justify-between gap-3">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-ink-900 dark:text-white">
                          {fmtRange(s)}
                        </p>
                        <p className="flex flex-wrap items-center gap-1 text-xs text-ink-500 dark:text-ink-400">
                          <Users className="h-3 w-3" />{' '}
                          {s.capacity == null
                            ? t('slotList.bookedUnlimited', { booked: s.bookedCount })
                            : t('slotList.booked', { booked: s.bookedCount, capacity: s.capacity })}
                          {s.capacity != null && s.bookedCount >= s.capacity && (
                            <span className="ml-1 rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-500/20 dark:text-amber-400">
                              {t('slotList.full')}
                            </span>
                          )}
                          {/* Ca nằm ngoài lịch rảnh của HM: gán ứng viên vào sẽ bị server từ chối.
                          Đánh dấu ngay tại dòng, thay vì để Recruiter phát hiện lúc đang xếp lịch
                          cho một người cụ thể. */}
                          {/* Ca trùng giờ ca khác — dữ liệu cũ tạo trước khi có luật chặn. Đánh
                          dấu để người vận hành sửa, vì cả hai ca đều không dùng được như ý. */}
                          {!isTestRound &&
                            clashCandidates.some(
                              (x) =>
                                x.id !== s.id &&
                                overlaps(s.startTime, s.endTime, x.startTime, x.endTime)
                            ) && (
                            <span className="ml-1 inline-flex items-center gap-1 rounded bg-red-100 px-1.5 py-0.5 text-[10px] font-semibold text-red-700 dark:bg-red-500/20 dark:text-red-400">
                              <AlertCircle className="h-3 w-3" /> {t('slotList.overlapping')}
                            </span>
                          )}
                          {!isTestRound &&
                            hmWindows.length > 0 &&
                            !isCoveredByHm(s.startTime, s.endTime, hmWindows) && (
                              <span className="ml-1 inline-flex items-center gap-1 rounded bg-red-100 px-1.5 py-0.5 text-[10px] font-semibold text-red-700 dark:bg-red-500/20 dark:text-red-400">
                                <AlertCircle className="h-3 w-3" /> {t('slotList.outsideHm')}
                              </span>
                            )}
                        </p>
                      </div>
                      <div className="flex shrink-0 items-center gap-1">
                        {/* Ca dữ liệu cũ còn sức chứa > 1 ở vòng HỘI THOẠI: cho HẠ về 1, không cho
                        tăng (ADR-067). Vòng trắc nghiệm thì trần là hợp lệ, không phải thứ để dọn. */}
                        {!isTestRound && s.capacity != null && s.capacity > 1 && (
                          <button
                            type="button"
                            onClick={() => changeCapacity(s, 1 - (s.capacity ?? 1))}
                            disabled={s.bookedCount > 1}
                            className="rounded-lg border border-amber-200 px-2 py-1 text-[11px] font-medium text-amber-700 hover:bg-amber-50 disabled:opacity-40 dark:border-amber-500/30 dark:text-amber-400 dark:hover:bg-amber-500/10"
                            title={t('slotList.normalizeCapacity')}
                          >
                            {t('slotList.normalizeCapacity')}
                          </button>
                        )}
                        {/* Sua gio chi khi CHUA ai giu cho: doi gio mot ca da hen la doi lich hen cua
                        nguoi khac ma khong bao ho — duong dung cho viec do la "doi lich". */}
                        <button
                          type="button"
                          onClick={() => (isEditing ? setEditing(null) : openEdit(s))}
                          disabled={held.length > 0}
                          className="grid h-7 w-7 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-ink-700 disabled:opacity-40 dark:hover:bg-white/10 dark:hover:text-white"
                          title={
                            held.length > 0 ? t('slotList.cannotEdit') : t('slotList.editTime')
                          }
                        >
                          <Pencil className="h-3.5 w-3.5" />
                        </button>
                        {/* Ca còn dòng booking ĐÃ ĐÓNG (báo bận / huỷ) cũng KHÔNG xoá được: dòng đó
                        là bằng chứng "ứng viên được mời vào đúng giờ đó rồi báo bận". Trước đây nút này chỉ
                        nhìn `bookedCount` (đếm dòng đang giữ chỗ) nên ca hiện "Đã đặt 0/1" — trông như
                        trống — bấm vào thì khoá ngoại ở DB chặn và người dùng nhận lỗi 500. */}
                        <button
                          type="button"
                          onClick={() => removeSlot(s)}
                          disabled={s.bookedCount > 0 || (s.closedBookingCount ?? 0) > 0}
                          className="ml-1 grid h-7 w-7 place-items-center rounded-lg text-ink-400 hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-500/10 disabled:opacity-40"
                          title={
                            s.bookedCount > 0
                              ? t('slotList.cannotDelete')
                              : (s.closedBookingCount ?? 0) > 0
                                ? t('slotList.cannotDeleteHistory')
                                : t('slotList.delete')
                          }
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>

                    {/* Ai dang giu cho. Truoc day dong nay chi ghi "Da dat 0/1" — mot con so khong noi
                      duoc ca do dang hen voi ai, va nguoi van hanh phai sang man khac moi biet. */}
                    {held.length > 0 && (
                      <ul className="mt-2 space-y-1.5">
                        {held.map((b) => (
                          <li
                            key={b.bookingId}
                            className="flex flex-wrap items-center gap-2 rounded-xl border border-ink-100 bg-ink-50/60 px-3 py-2 dark:border-white/10 dark:bg-white/5"
                          >
                            <span className="text-sm font-medium text-ink-900 dark:text-white">
                              {b.candidateName || t('slotList.unknownCandidate')}
                            </span>
                            {b.candidateEmail && (
                              <span className="inline-flex items-center gap-1 text-xs text-ink-500 dark:text-ink-400">
                                <Mail className="h-3 w-3" /> {b.candidateEmail}
                              </span>
                            )}
                            <span
                              className={`ml-auto rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                                b.confirmationStatus === 'confirmed'
                                  ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                                  : b.confirmationStatus === 'declined'
                                    ? 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
                                    : 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400'
                              }`}
                            >
                              {t(`slotList.confirmation.${b.confirmationStatus}`, {
                                defaultValue: b.confirmationStatus,
                              })}
                            </span>
                          </li>
                        ))}
                      </ul>
                    )}

                    {/* Lịch sử của ca: người TỪNG được gán rồi thôi (từ chối, bị loại…). Không chiếm
                      chỗ, nhưng là lý do ca không xoá được — nên ghi rõ AI và VÌ SAO, thay cho con số
                      trần "N lượt đã đóng" không ai hiểu là lượt gì. */}
                    {(s.closedBookings?.length ?? 0) > 0 && (
                      <div className="mt-2">
                        <p className="text-[11px] font-semibold uppercase tracking-wide text-ink-400 dark:text-ink-500">
                          {t('slotList.history.title')}
                        </p>
                        <ul className="mt-1 space-y-1">
                          {s.closedBookings!.map((c) => (
                            <li
                              key={c.bookingId}
                              className="flex flex-wrap items-center gap-2 rounded-lg border border-dashed border-ink-200 px-3 py-1.5 text-xs text-ink-500 dark:border-white/10 dark:text-ink-400"
                            >
                              <span className="font-medium text-ink-700 dark:text-ink-200">
                                {c.candidateName || t('slotList.unknownCandidate')}
                              </span>
                              <span
                                className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                                  c.state === 'declined_by_candidate'
                                    ? 'bg-orange-100 text-orange-700 dark:bg-orange-500/20 dark:text-orange-400'
                                    : 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300'
                                }`}
                              >
                                {t(`slotList.history.state.${c.state}`, { defaultValue: c.state })}
                              </span>
                              {c.reason && (
                                <span className="min-w-0 truncate italic" title={c.reason}>
                                  “{c.reason}”
                                </span>
                              )}
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                    {/* Sua gio ngay tai dong — khong day sang mot hop thoai, vi thu can nhin trong
                      luc sua (cac ca khac cua vong nay) dang nam ngay ben canh. */}
                    {isEditing && editing && (
                      <div className="mt-2 flex flex-wrap items-center gap-2 rounded-xl border border-brand-200 bg-brand-50/60 px-3 py-2.5 dark:border-brand-500/30 dark:bg-brand-500/10">
                        <input
                          type="date"
                          value={editing.date}
                          onChange={(e) => setEditing({ ...editing, date: e.target.value })}
                          className="rounded-lg border border-ink-200 bg-white px-2.5 py-1.5 text-sm text-ink-900 dark:border-white/10 dark:bg-white/5 dark:text-white"
                        />
                        <TimeInput
                          value={editing.start}
                          onChange={(v) => setEditing((cur) => (cur ? { ...cur, start: v } : cur))}
                          ariaLabel={t('slotForm.startTime')}
                          className="w-[6.25rem]"
                          inputClassName="rounded-lg py-1.5 pl-2.5 text-sm"
                        />
                        <span className="text-sm text-ink-400">–</span>
                        <TimeInput
                          value={editing.end}
                          onChange={(v) => setEditing((cur) => (cur ? { ...cur, end: v } : cur))}
                          ariaLabel={t('slotForm.endTime')}
                          className="w-[6.25rem]"
                          inputClassName="rounded-lg py-1.5 pl-2.5 text-sm"
                        />
                        <button
                          type="button"
                          onClick={saveEdit}
                          disabled={busy}
                          className="ml-auto inline-flex items-center gap-1.5 rounded-lg bg-brand-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
                        >
                          <Check className="h-3.5 w-3.5" /> {t('slotList.saveTime')}
                        </button>
                        <button
                          type="button"
                          onClick={() => setEditing(null)}
                          className="inline-flex items-center gap-1.5 rounded-lg border border-ink-200 px-3 py-1.5 text-xs font-medium text-ink-600 hover:bg-ink-50 dark:border-white/10 dark:text-ink-300 dark:hover:bg-white/5"
                        >
                          <XCircle className="h-3.5 w-3.5" /> {t('slotList.cancelEdit')}
                        </button>
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
