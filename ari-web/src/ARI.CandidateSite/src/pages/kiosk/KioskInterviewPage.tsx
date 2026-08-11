import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import {
  AlertTriangle,
  Bot,
  CheckCircle2,
  Clock,
  Keyboard,
  Loader2,
  Mic,
  MicOff,
  Phone,
  Send,
  ShieldCheck,
  Video,
} from 'lucide-react'
import DeviceCheck from '@ari/shared/media/DeviceCheck'
import { useInterviewSession } from '@ari/shared/media/usePracticeSession'
import { exitFullscreen, useKioskLockdown } from '@ari/shared/media/useKioskLockdown'
import {
  clearKioskSession,
  loadKioskSession,
  markKioskSessionStarted,
  type KioskSession,
} from './kioskSession'

type Phase = 'intro' | 'live' | 'ended'

function mmss(total: number): string {
  const s = Math.max(0, Math.floor(total))
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}

const AUTO_RESET_SECONDS = 30

export default function KioskInterviewPage() {
  const { t } = useTranslation('modules/kiosk')
  const navigate = useNavigate()
  const [session] = useState<KioskSession | null>(() => loadKioskSession())
  const [phase, setPhase] = useState<Phase>('intro')
  const [recordingState, setRecordingState] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')
  const [resetIn, setResetIn] = useState(AUTO_RESET_SECONDS)

  const streamRef = useRef<MediaStream | null>(null)
  const selfVideoRef = useRef<HTMLVideoElement>(null)
  const transcriptRef = useRef<HTMLDivElement>(null)

  const interview = useInterviewSession({
    existingSessionId: session?.sessionId ?? null,
    sessionType: 'real',
    recordVideo: true,
  })

  const lockdown = useKioskLockdown(session?.sessionId ?? null, phase === 'live')

  const stopStream = useCallback(() => {
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
  }, [])

  useEffect(() => () => stopStream(), [stopStream])

  useEffect(() => {
    if (!session) navigate('/kiosk', { replace: true })
  }, [session, navigate])

  useEffect(() => {
    const el = transcriptRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [interview.messages, interview.answerText, interview.interim])

  useEffect(() => {
    if (phase === 'live' && selfVideoRef.current && streamRef.current) {
      selfVideoRef.current.srcObject = streamRef.current
      selfVideoRef.current.play().catch(() => {})
    }
  }, [phase])

  const finishSession = useCallback(async () => {
    setPhase('ended')
    setRecordingState('saving')
    const outcome = await interview.finalizeRecording()
    stopStream()
    void exitFullscreen()
    setRecordingState(outcome === 'error' ? 'error' : outcome === 'saved' ? 'saved' : 'idle')
    clearKioskSession()
  }, [interview, stopStream])

  useEffect(() => {
    if (interview.status === 'ended' && phase === 'live') void finishSession()
  }, [interview.status, phase, finishSession])

  useEffect(() => {
    if (phase !== 'ended') return
    const id = window.setInterval(() => {
      setResetIn((s) => {
        if (s <= 1) {
          window.clearInterval(id)
          navigate('/kiosk', { replace: true })
          return 0
        }
        return s - 1
      })
    }, 1000)
    return () => window.clearInterval(id)
  }, [phase, navigate])

  const handleReady = (stream: MediaStream) => {
    streamRef.current = stream
    setPhase('live')
    markKioskSessionStarted()
    void interview.start(stream)
  }

  const endSession = async () => {
    await interview.end()
    await finishSession()
  }

  if (!session) return null

  const header = (
    <header className="flex h-14 shrink-0 items-center justify-between border-b border-white/5 px-6">
      <div className="flex items-center gap-3 text-sm">
        <span className="font-display text-lg font-extrabold text-white">
          ARISP <span className="font-medium text-slate-400">Kiosk</span>
        </span>
        <span className="hidden rounded-full bg-white/5 px-3 py-1 text-xs text-slate-300 ring-1 ring-white/10 sm:inline">
          {session.candidateName || t('kioskInterview.roleUser')} · {session.jobTitle || ''} · Vòng {session.roundNumber}
        </span>
      </div>
      <div className="flex items-center gap-3">
        {phase === 'live' && (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-red-500/15 px-3 py-1 text-xs font-semibold text-red-300 ring-1 ring-red-500/30">
            <span className="h-2 w-2 animate-pulse rounded-full bg-red-500" /> {t('kioskInterview.status.recording')}
          </span>
        )}
        {interview.remainingSeconds !== null && phase === 'live' && (
          <span
            className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold ring-1 ${
              interview.remainingSeconds <= 300
                ? 'bg-amber-500/15 text-amber-300 ring-amber-500/30'
                : 'bg-white/5 text-slate-300 ring-white/10'
            }`}
          >
            <Clock className="h-3.5 w-3.5" /> {mmss(interview.remainingSeconds)}
          </span>
        )}
      </div>
    </header>
  )

  if (phase === 'intro') {
    return (
      <div className="flex min-h-screen flex-col bg-ink-950 text-slate-100">
        {header}
        <div className="mx-auto flex w-full max-w-4xl flex-1 flex-col px-4 py-8">
          <div className="mb-6 rounded-2xl border border-white/10 bg-gradient-to-br from-brand-600/15 to-ai-600/10 p-5">
            <h1 className="font-display text-2xl font-bold text-white">
              {t('kioskInterview.greeting', { name: session.candidateName || '' })}
            </h1>
            <p className="mt-1 text-slate-300">
              {t('kioskInterview.interviewInfo', { round: session.roundNumber, jobTitle: session.jobTitle ? ` — ${session.jobTitle}` : '' })}
            </p>
            <ul className="mt-4 grid gap-2 text-sm text-slate-300 sm:grid-cols-2">
              <li className="flex items-start gap-2">
                <Video className="mt-0.5 h-4 w-4 shrink-0 text-red-300" /> {t('kioskInterview.intro.recordingNotice')}
              </li>
              <li className="flex items-start gap-2">
                <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-emerald-300" /> {t('kioskInterview.intro.recordingPrivacy')}
              </li>
              <li className="flex items-start gap-2">
                <Mic className="mt-0.5 h-4 w-4 shrink-0 text-brand-300" /> {t('kioskInterview.intro.voiceAnswer')}
              </li>
              <li className="flex items-start gap-2">
                <Clock className="mt-0.5 h-4 w-4 shrink-0 text-amber-300" /> {t('kioskInterview.intro.timeLimit')}
              </li>
            </ul>
          </div>

          <div className="flex flex-1 items-center">
            <DeviceCheck
              title={t('kioskInterview.deviceCheck.title')}
              startLabel={t('kioskInterview.deviceCheck.startLabel')}
              onReady={handleReady}
              onCancel={() => navigate('/kiosk')}
            />
          </div>
        </div>
      </div>
    )
  }

  if (phase === 'ended') {
    return (
      <div className="grid min-h-screen place-items-center bg-ink-950 px-4">
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          className="max-w-md text-center"
        >
          <div className="mx-auto mb-6 grid h-24 w-24 place-items-center rounded-full bg-emerald-500/20">
            <CheckCircle2 className="h-12 w-12 text-emerald-400" />
          </div>
          <h1 className="mb-3 font-display text-3xl font-bold text-white">
            {t('kioskInterview.thanks', { name: session.candidateName || '' })}
          </h1>
          <p className="mb-6 text-slate-400">{t('kioskInterview.completion')}</p>

          <div className="mb-8 flex items-center justify-center gap-2 text-sm">
            {recordingState === 'saving' && (
              <span className="inline-flex items-center gap-2 text-slate-300">
                <Loader2 className="h-4 w-4 animate-spin" /> {t('kioskInterview.recordingSaving')}
              </span>
            )}
            {recordingState === 'saved' && (
              <span className="inline-flex items-center gap-2 text-emerald-300">
                <ShieldCheck className="h-4 w-4" /> {t('kioskInterview.recordingSaved')}
              </span>
            )}
            {recordingState === 'error' && (
              <span className="inline-flex items-center gap-2 text-amber-300">
                <AlertTriangle className="h-4 w-4" /> {t('kioskInterview.recordingFailed')}
              </span>
            )}
          </div>

          {lockdown.exitCount > 0 && (
            <div className="mb-6 rounded-xl border border-amber-500/30 bg-amber-500/10 p-3 text-sm text-amber-200">
              {t('kioskInterview.exitRecorded', { count: lockdown.exitCount })}
            </div>
          )}

          <p className="text-xs text-slate-500">
            {t('kioskInterview.autoReset', { seconds: resetIn })}
          </p>
          <button
            onClick={() => navigate('/kiosk', { replace: true })}
            disabled={recordingState === 'saving'}
            className="mt-3 rounded-xl bg-white/10 px-6 py-3 font-semibold text-white hover:bg-white/20 disabled:opacity-50"
          >
            {t('kioskInterview.returnToCode')}
          </button>
        </motion.div>
      </div>
    )
  }

  const starting = interview.status === 'starting'
  return (
    <div className="flex h-screen flex-col overflow-hidden bg-ink-950 text-slate-100">
      {!lockdown.isFullscreen && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-ink-950/95 px-6 backdrop-blur">
          <div className="max-w-md text-center">
            <div className="mx-auto mb-5 grid h-16 w-16 place-items-center rounded-full bg-amber-500/15 text-amber-300 ring-1 ring-amber-500/30">
              <AlertTriangle className="h-8 w-8" />
            </div>
            <h2 className="font-display text-2xl font-bold text-white">
              {t('kioskInterview.exitWarning.title')}
            </h2>
            <p className="mt-2 text-slate-400">
              {t('kioskInterview.exitWarning.message')}
            </p>
            <p className="mt-3 text-sm font-semibold text-amber-300">
              {t('kioskInterview.exitWarning.exitCount', { count: lockdown.exitCount })}
            </p>
            <button
              onClick={() => void lockdown.requestFullscreen()}
              className="mt-6 w-full rounded-2xl bg-gradient-to-r from-brand-600 to-ai-600 px-6 py-4 text-base font-bold text-white hover:opacity-95"
            >
              {t('kioskInterview.fullscreenReturn')}
            </button>
          </div>
        </div>
      )}

      {header}

      <main className="grid min-h-0 flex-1 gap-4 p-4 lg:grid-cols-[1fr_380px]">
        <section className="relative min-h-0 overflow-hidden rounded-2xl border border-white/10 bg-black/40">
          <video
            ref={interview.videoRef}
            autoPlay
            playsInline
            className="h-full w-full object-cover"
          />
          {!interview.avatarReady && (
            <div className="absolute inset-0 grid place-items-center">
              <div className="text-center">
                <div
                  className={`mx-auto grid h-24 w-24 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 ${
                    interview.aiSpeaking ? 'animate-pulse' : ''
                  }`}
                >
                  <Bot className="h-12 w-12 text-white" />
                </div>
                <p className="mt-4 text-sm text-slate-400">
                  {starting ? t('kioskInterview.status.connecting') : t('kioskInterview.status.aiInterviewerIdle')}
                </p>
              </div>
            </div>
          )}

          <video
            ref={selfVideoRef}
            autoPlay
            playsInline
            muted
            className="absolute bottom-4 right-4 h-28 w-44 rounded-xl border border-white/10 object-cover shadow-2xl"
          />

          {interview.timeUp && (
            <div className="absolute inset-x-0 top-4 mx-auto w-fit rounded-full bg-amber-500/20 px-4 py-1.5 text-sm text-amber-200 ring-1 ring-amber-500/40">
              {t('kioskInterview.timeUp')}
            </div>
          )}
        </section>

        <section className="flex min-h-0 flex-col gap-3">
          <div
            ref={transcriptRef}
            className="min-h-0 flex-1 space-y-3 overflow-y-auto rounded-2xl border border-white/10 bg-white/5 p-3"
          >
            {interview.messages.length === 0 && (
              <p className="text-sm text-slate-500">{t('kioskInterview.emptyTranscript')}</p>
            )}
            {interview.messages.map((m, i) => (
              <div
                key={i}
                className={`rounded-xl p-3 text-sm ${
                  m.role === 'ai' ? 'bg-brand-600/15 text-slate-100' : 'ml-6 bg-white/5 text-slate-200'
                }`}
              >
                <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
                  {m.role === 'ai' ? t('kioskInterview.roleAi') : t('kioskInterview.roleUser')}
                </div>
                {m.text}
              </div>
            ))}
            {interview.interim && (
              <div className="ml-6 rounded-xl border border-dashed border-white/10 p-3 text-sm italic text-slate-400">
                {interview.interim}
              </div>
            )}
          </div>

          <div className="rounded-2xl border border-white/10 bg-white/5 p-3">
            <div className="mb-2 flex items-center justify-between text-xs text-slate-400">
              <span className="inline-flex items-center gap-1.5">
                {interview.micEnabled ? (
                  <>
                    <Mic className="h-3.5 w-3.5 text-emerald-400" />
                    {interview.listening ? t('kioskInterview.micStatus.listening') : t('kioskInterview.micStatus.micOn')}
                  </>
                ) : (
                  <>
                    <Keyboard className="h-3.5 w-3.5" /> {t('kioskInterview.micStatus.typing')}
                  </>
                )}
              </span>
              <span>{t('kioskInterview.charCount', { count: interview.answerText.length })}</span>
            </div>
            <textarea
              value={interview.answerText}
              onChange={(e) => interview.setAnswerText(e.target.value)}
              placeholder={t('kioskInterview.answerPlaceholder')}
              className="h-24 w-full resize-none rounded-xl border border-white/10 bg-ink-950/60 p-3 text-sm text-slate-100 outline-none focus:border-brand-500"
            />
            <div className="mt-2 flex items-center gap-2">
              <button
                onClick={interview.toggleMic}
                className="rounded-xl border border-white/10 bg-white/5 p-2.5 text-slate-200 hover:bg-white/10"
                title={t('kioskInterview.toggleMic')}
              >
                {interview.micEnabled ? <Mic className="h-5 w-5" /> : <MicOff className="h-5 w-5 text-red-400" />}
              </button>
              <button
                onClick={interview.submitAnswer}
                disabled={!interview.answerText.trim() || interview.aiSpeaking}
                className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-bold text-white hover:opacity-90 disabled:opacity-40"
              >
                <Send className="h-4 w-4" /> {t('kioskInterview.submitAnswer')}
              </button>
              <button
                onClick={endSession}
                className="rounded-xl bg-red-600/90 p-2.5 text-white hover:bg-red-600"
                title={t('kioskInterview.endInterview')}
              >
                <Phone className="h-5 w-5 rotate-[135deg]" />
              </button>
            </div>
          </div>
        </section>
      </main>

      {interview.error && (
        <div className="border-t border-red-500/20 bg-red-500/10 px-6 py-2 text-sm text-red-200">
          {interview.error}
        </div>
      )}
    </div>
  )
}
