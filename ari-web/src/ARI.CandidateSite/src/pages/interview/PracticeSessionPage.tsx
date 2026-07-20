import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import {
  Mic,
  MicOff,
  Phone,
  Bot,
  ShieldCheck,
  Sparkles,
  FileText,
  GraduationCap,
  Info,
  Send,
  Loader2,
  AlertTriangle,
} from 'lucide-react'
import DeviceCheck from '@ari/shared/media/DeviceCheck'
import { usePracticeSession } from '@ari/shared/media/usePracticeSession'

type Phase = 'intro' | 'live' | 'ended'

function Logo() {
  return (
    <svg className="h-7 w-7" viewBox="0 0 96 96" fill="none" aria-label="ARISP">
      <defs>
        <linearGradient id="lg-prac" x1="12" y1="10" x2="84" y2="86" gradientUnits="userSpaceOnUse">
          <stop stopColor="#6366f1" />
          <stop offset="1" stopColor="#a855f7" />
        </linearGradient>
      </defs>
      <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-prac)" />
      <path
        d="M30 70 L48 26 L66 70"
        stroke="white"
        strokeWidth="8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M38 56 H58" stroke="white" strokeWidth="8" strokeLinecap="round" />
      <path
        d="M70 20 C71.4 27 72.5 28.1 79.5 29.5 C72.5 30.9 71.4 32 70 39 C68.6 32 67.5 30.9 60.5 29.5 C67.5 28.1 68.6 27 70 20 Z"
        fill="white"
        fillOpacity="0.95"
      />
    </svg>
  )
}

export default function PracticeSessionPage() {
  const { applicationId } = useParams<{ applicationId: string }>()
  const navigate = useNavigate()
  const { t } = useTranslation('modules/interview/practice')

  const [phase, setPhase] = useState<Phase>('intro')
  const [isMuted, setIsMuted] = useState(false)
  const streamRef = useRef<MediaStream | null>(null)
  const selfVideoRef = useRef<HTMLVideoElement>(null)
  const transcriptRef = useRef<HTMLDivElement>(null)

  const practice = usePracticeSession(applicationId ?? '', 1)

  // Tin nhắn/diễn giải mới → tự cuộn transcript xuống cuối (chỉ cuộn panel, không cuộn page).
  useEffect(() => {
    const el = transcriptRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [practice.messages, practice.draft, practice.interim])

  const stopStream = useCallback(() => {
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
  }, [])

  useEffect(() => () => stopStream(), [stopStream])

  // Buổi kết thúc do server báo (hết câu hỏi) → chuyển màn kết thúc.
  useEffect(() => {
    if (practice.status === 'ended' && phase === 'live') {
      stopStream()
      setPhase('ended')
    }
  }, [practice.status, phase, stopStream])

  // Gắn stream (đã qua kiểm tra thiết bị) vào ô tự xem khi vào phòng.
  useEffect(() => {
    if (phase === 'live' && selfVideoRef.current && streamRef.current) {
      selfVideoRef.current.srcObject = streamRef.current
      selfVideoRef.current.play().catch(() => {})
    }
  }, [phase])

  const handleReady = (stream: MediaStream) => {
    streamRef.current = stream
    setIsMuted(false)
    setPhase('live')
    void practice.start(stream)
  }

  const toggleMute = () => {
    const track = streamRef.current?.getAudioTracks()[0]
    if (!track) return
    track.enabled = !track.enabled
    setIsMuted(!track.enabled)
  }

  const endSession = async () => {
    await practice.end()
    stopStream()
    setPhase('ended')
  }

  if (!applicationId) {
    return (
      <div className="grid min-h-screen place-items-center bg-ink-950 text-slate-300">
        {t('practice.invalidApplication')}
      </div>
    )
  }

  // ===== INTRO + DEVICE CHECK =====
  if (phase === 'intro') {
    return (
      <div className="min-h-screen bg-ink-950 text-slate-100">
        <div className="mx-auto flex min-h-screen max-w-4xl flex-col px-4 py-8">
          <div className="mb-8 flex items-center gap-2.5">
            <Logo />
            <span className="font-display text-lg font-extrabold text-white">
              ARISP{' '}
              <span className="font-medium text-slate-400">
                {t('practice.introTitle').replace('ARISP ', '')}
              </span>
            </span>
          </div>

          <div className="mb-6 rounded-2xl border border-white/10 bg-gradient-to-br from-brand-600/15 to-ai-600/10 p-5">
            <div className="mb-2 inline-flex items-center gap-1.5 rounded-full bg-white/10 px-3 py-1 text-xs font-medium text-ai-300">
              <Sparkles className="h-3.5 w-3.5" /> {t('practice.badge')}
            </div>
            <h2 className="font-display text-xl font-bold text-white">
              {t('practice.beforeStart')}
            </h2>
            <ul className="mt-3 grid gap-2 text-sm text-slate-300 sm:grid-cols-2">
              <li className="flex items-start gap-2">
                <FileText className="mt-0.5 h-4 w-4 shrink-0 text-brand-300" />{' '}
                {t('practice.points.jdBased')}
              </li>
              <li className="flex items-start gap-2">
                <GraduationCap className="mt-0.5 h-4 w-4 shrink-0 text-brand-300" />{' '}
                {t('practice.points.sameAsReal')}
              </li>
              <li className="flex items-start gap-2">
                <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-emerald-300" />{' '}
                {t('practice.points.noRecording')}
              </li>
              <li className="flex items-start gap-2">
                <Mic className="mt-0.5 h-4 w-4 shrink-0 text-brand-300" />{' '}
                {t('practice.points.voiceAnswer')}
              </li>
            </ul>
          </div>

          <div className="flex flex-1 items-center">
            <DeviceCheck
              title={t('practice.deviceCheck.title')}
              startLabel={t('practice.deviceCheck.startLabel')}
              onReady={handleReady}
              onCancel={() => navigate(-1)}
            />
          </div>
        </div>
      </div>
    )
  }

  // ===== ENDED =====
  if (phase === 'ended') {
    return (
      <div className="grid min-h-screen place-items-center bg-ink-950 px-4">
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          className="max-w-md text-center"
        >
          <div className="mx-auto mb-6 grid h-24 w-24 place-items-center rounded-full bg-emerald-500/20">
            <ShieldCheck className="h-12 w-12 text-emerald-400" />
          </div>
          <h1 className="mb-3 font-display text-3xl font-bold text-white">
            {t('practice.ended.title')}
          </h1>
          <p className="mb-8 text-slate-400">{t('practice.ended.message')}</p>
          <div className="flex items-center justify-center gap-3">
            <button
              onClick={() => navigate(`/candidate/applications/${applicationId}`)}
              className="rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-6 py-3 font-semibold text-white hover:opacity-90"
            >
              {t('practice.ended.viewResult')}
            </button>
            <button
              onClick={() => navigate('/candidate/applications')}
              className="rounded-xl bg-white/10 px-6 py-3 font-semibold text-white hover:bg-white/20"
            >
              {t('practice.ended.backToApplication')}
            </button>
          </div>
        </motion.div>
      </div>
    )
  }

  // ===== LIVE ROOM =====
  const starting = practice.status === 'starting'
  return (
    // h-screen + overflow-hidden: page KHÔNG giãn theo transcript — chỉ panel Transcript cuộn nội bộ.
    <div className="flex h-screen flex-col overflow-hidden bg-ink-950 text-slate-100">
      {/* Top bar */}
      <header className="flex h-14 items-center justify-between border-b border-white/5 px-6">
        <div className="flex items-center gap-3">
          <Logo />
          <div className="text-sm">
            <span className="font-semibold text-white">{t('practice.header.title')}</span>
            <span className="text-slate-400"> · {t('practice.header.subtitle')}</span>
          </div>
        </div>
        <span className="flex items-center gap-2 rounded-full bg-emerald-500/10 px-3 py-1 text-sm font-medium text-emerald-300">
          <ShieldCheck className="h-4 w-4" /> {t('practice.noVideo')}
        </span>
      </header>

      {/* Main — min-h-0 để grid con không đẩy chiều cao vượt viewport */}
      <main className="grid min-h-0 flex-1 grid-rows-[minmax(45vh,1fr)_minmax(0,1fr)] lg:grid-cols-[1fr_380px] lg:grid-rows-none">
        {/* Avatar — video phủ kín toàn khung (object-cover), không còn ô nhỏ giữa màn */}
        <section className="relative min-h-0 overflow-hidden bg-black">
          <video
            ref={practice.videoRef}
            autoPlay
            playsInline
            className={`absolute inset-0 h-full w-full object-cover transition-opacity duration-500 ${
              practice.avatarReady ? 'opacity-100' : 'opacity-0'
            }`}
          />

          {/* Chưa có avatar (đang kết nối / thiếu cấu hình) → nền gradient + bot tĩnh */}
          {!practice.avatarReady && (
            <div className="absolute inset-0 grid place-items-center bg-gradient-to-b from-brand-600/15 via-ink-950 to-ai-600/15">
              <div className="relative">
                <div
                  className={`absolute -inset-8 rounded-full bg-gradient-to-r from-brand-500 to-ai-500 blur-2xl transition-opacity ${
                    practice.aiSpeaking ? 'opacity-60' : 'opacity-25'
                  }`}
                />
                <div className="relative grid h-44 w-44 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 shadow-2xl">
                  <Bot className="h-20 w-20 text-white" />
                </div>
              </div>
            </div>
          )}

          {/* Trạng thái nổi trên video */}
          <div className="absolute inset-x-0 bottom-4 z-10 flex justify-center px-4">
            <div className="flex items-center gap-1.5 rounded-full bg-black/55 px-4 py-2 text-sm text-slate-200 backdrop-blur">
              {starting ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />{' '}
                  {t('practice.statusMessages.connecting')}
                </>
              ) : practice.status === 'error' ? (
                <>
                  <AlertTriangle className="h-4 w-4 text-red-400" />
                  <span className="text-red-300">{practice.error}</span>
                </>
              ) : practice.aiSpeaking ? (
                <>
                  <Sparkles className="h-4 w-4 text-ai-300" />{' '}
                  {t('practice.statusMessages.aiSpeaking')}
                </>
              ) : practice.listening ? (
                <>
                  <Mic className="h-4 w-4 text-emerald-300" />{' '}
                  {t('practice.statusMessages.listening')}
                </>
              ) : (
                <>
                  <Info className="h-4 w-4" /> {t('practice.statusMessages.ready')}
                </>
              )}
            </div>
          </div>

          {/* Câu hỏi hiện tại nổi phía trên (đọc được ngay cả khi không nhìn transcript) */}
          {practice.messages.length > 0 && (
            <div className="absolute inset-x-0 top-4 z-10 flex justify-center px-4">
              <p className="max-w-2xl rounded-2xl bg-black/55 px-5 py-3 text-center text-sm leading-relaxed text-white backdrop-blur">
                {[...practice.messages].reverse().find((m) => m.role === 'ai')?.text}
              </p>
            </div>
          )}

          {/* Self view */}
          <div className="absolute bottom-4 right-4 z-10 aspect-video w-40 overflow-hidden rounded-xl bg-black shadow-lg ring-1 ring-white/15 sm:w-52">
            <video
              ref={selfVideoRef}
              muted
              playsInline
              className="h-full w-full -scale-x-100 object-cover"
            />
            {isMuted && (
              <span className="absolute bottom-1.5 left-1.5 grid h-6 w-6 place-items-center rounded-full bg-red-500/80">
                <MicOff className="h-3.5 w-3.5 text-white" />
              </span>
            )}
          </div>
        </section>

        {/* Transcript — min-h-0 để vùng messages cuộn nội bộ thay vì giãn page */}
        <aside className="flex min-h-0 flex-col border-l border-white/5 bg-ink-900/60">
          <div className="flex items-center justify-between border-b border-white/5 px-5 py-4">
            <span className="font-display font-bold text-white">
              {t('practice.transcript.title')}
            </span>
            <span className="text-xs text-slate-400">{t('practice.transcript.jdCv')}</span>
          </div>
          <div ref={transcriptRef} className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5">
            {practice.messages.length === 0 && !practice.interim && (
              <p className="pt-6 text-center text-sm text-slate-500">
                {t('practice.transcript.waiting')}
              </p>
            )}
            {practice.messages.map((m, i) => (
              <div
                key={i}
                className={`rounded-xl px-3.5 py-2.5 text-sm ${
                  m.role === 'ai'
                    ? 'bg-brand-600/15 text-slate-100'
                    : 'ml-6 bg-white/5 text-slate-300'
                }`}
              >
                <span className="mb-0.5 block text-[11px] font-medium uppercase tracking-wide text-slate-500">
                  {m.role === 'ai'
                    ? t('practice.transcript.aiLabel')
                    : t('practice.transcript.candidateLabel')}
                </span>
                {m.text}
              </div>
            ))}
            {(practice.draft || practice.interim) && (
              <div className="ml-6 rounded-xl border border-dashed border-brand-400/40 bg-white/5 px-3.5 py-2.5 text-sm text-slate-300">
                <span className="mb-0.5 block text-[11px] font-medium uppercase tracking-wide text-brand-300">
                  {t('practice.transcript.drafting')}
                </span>
                {practice.draft}
                {practice.interim && (
                  <span className="italic text-slate-400">
                    {practice.draft ? ' ' : ''}
                    {practice.interim}…
                  </span>
                )}
              </div>
            )}
          </div>
        </aside>
      </main>

      {/* Controls */}
      <footer className="border-t border-white/5 px-6 py-4">
        <div className="mx-auto flex max-w-3xl items-center justify-center gap-3">
          <button
            onClick={toggleMute}
            className="grid h-12 w-12 place-items-center rounded-full bg-white/10 transition hover:bg-white/20"
            aria-label={isMuted ? t('practice.controls.unmute') : t('practice.controls.mute')}
          >
            {isMuted ? (
              <MicOff className="h-5 w-5 text-red-400" />
            ) : (
              <Mic className="h-5 w-5 text-white" />
            )}
          </button>
          <button
            onClick={practice.submitAnswer}
            disabled={starting || !(practice.draft || practice.interim)}
            className={`flex h-12 items-center gap-2 rounded-full px-5 font-semibold text-white transition disabled:opacity-40 ${
              practice.draft || practice.interim
                ? 'bg-gradient-to-r from-brand-600 to-ai-600 hover:opacity-90'
                : 'bg-white/10 hover:bg-white/20'
            }`}
          >
            <Send className="h-4 w-4" /> {t('practice.controls.submitAnswer')}
          </button>
          <button
            onClick={endSession}
            className="flex h-12 items-center gap-2 rounded-full bg-red-600 px-6 font-semibold text-white transition hover:bg-red-700"
          >
            <Phone className="h-5 w-5" /> {t('practice.controls.end')}
          </button>
        </div>
        <p className="mt-3 text-center text-xs text-slate-500">
          {practice.sttEnabled ? t('practice.footer.sttEnabled') : t('practice.footer.sttDisabled')}
        </p>
      </footer>
    </div>
  )
}
