import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
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
} from 'lucide-react'
import DeviceCheck from '@components/interview/DeviceCheck'

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
      <path d="M30 70 L48 26 L66 70" stroke="white" strokeWidth="8" strokeLinecap="round" strokeLinejoin="round" />
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

  const [phase, setPhase] = useState<Phase>('intro')
  const [isMuted, setIsMuted] = useState(false)
  const streamRef = useRef<MediaStream | null>(null)
  const selfVideoRef = useRef<HTMLVideoElement>(null)

  const stopStream = useCallback(() => {
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
  }, [])

  useEffect(() => () => stopStream(), [stopStream])

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
  }

  const toggleMute = () => {
    const track = streamRef.current?.getAudioTracks()[0]
    if (!track) return
    track.enabled = !track.enabled
    setIsMuted(!track.enabled)
  }

  const endSession = () => {
    stopStream()
    setPhase('ended')
  }

  if (!applicationId) {
    return (
      <div className="grid min-h-screen place-items-center bg-ink-950 text-slate-300">
        Hồ sơ ứng tuyển không hợp lệ.
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
              ARISP <span className="font-medium text-slate-400">Phỏng vấn thử</span>
            </span>
          </div>

          <div className="mb-6 rounded-2xl border border-white/10 bg-gradient-to-br from-brand-600/15 to-ai-600/10 p-5">
            <div className="mb-2 inline-flex items-center gap-1.5 rounded-full bg-white/10 px-3 py-1 text-xs font-medium text-ai-300">
              <Sparkles className="h-3.5 w-3.5" /> Luyện tập • Không ảnh hưởng kết quả tuyển dụng
            </div>
            <h2 className="font-display text-xl font-bold text-white">Trước khi bắt đầu</h2>
            <ul className="mt-3 grid gap-2 text-sm text-slate-300 sm:grid-cols-2">
              <li className="flex items-start gap-2">
                <FileText className="mt-0.5 h-4 w-4 shrink-0 text-brand-300" /> AI hỏi dựa trên JD của vị trí và CV của bạn.
              </li>
              <li className="flex items-start gap-2">
                <GraduationCap className="mt-0.5 h-4 w-4 shrink-0 text-brand-300" /> Chỉ làm được 1 lần để làm quen hệ thống.
              </li>
              <li className="flex items-start gap-2">
                <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-emerald-300" /> Không ghi hình — chỉ lưu transcript &amp; nhận xét tham khảo.
              </li>
              <li className="flex items-start gap-2">
                <Mic className="mt-0.5 h-4 w-4 shrink-0 text-brand-300" /> Trả lời bằng giọng nói như phỏng vấn thật.
              </li>
            </ul>
          </div>

          <div className="flex flex-1 items-center">
            <DeviceCheck
              title="Kiểm tra camera & micro"
              startLabel="Vào phỏng vấn thử"
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
          <h1 className="mb-3 font-display text-3xl font-bold text-white">Hoàn thành buổi thử!</h1>
          <p className="mb-8 text-slate-400">
            Đây là buổi luyện tập nên không ảnh hưởng kết quả tuyển dụng. Bản nhận xét tham khảo sẽ hiển thị trong mục Kết quả.
          </p>
          <div className="flex items-center justify-center gap-3">
            <button
              onClick={() => navigate('/candidate/results')}
              className="rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-6 py-3 font-semibold text-white hover:opacity-90"
            >
              Xem kết quả
            </button>
            <button
              onClick={() => navigate('/candidate/applications')}
              className="rounded-xl bg-white/10 px-6 py-3 font-semibold text-white hover:bg-white/20"
            >
              Về hồ sơ ứng tuyển
            </button>
          </div>
        </motion.div>
      </div>
    )
  }

  // ===== LIVE ROOM =====
  return (
    <div className="flex min-h-screen flex-col bg-ink-950 text-slate-100">
      {/* Top bar */}
      <header className="flex h-14 items-center justify-between border-b border-white/5 px-6">
        <div className="flex items-center gap-3">
          <Logo />
          <div className="text-sm">
            <span className="font-semibold text-white">Phỏng vấn thử</span>
            <span className="text-slate-400"> · Luyện tập · Dựa trên JD + CV</span>
          </div>
        </div>
        <span className="flex items-center gap-2 rounded-full bg-emerald-500/10 px-3 py-1 text-sm font-medium text-emerald-300">
          <ShieldCheck className="h-4 w-4" /> Không ghi hình
        </span>
      </header>

      {/* Main */}
      <main className="grid flex-1 lg:grid-cols-[1fr_380px]">
        {/* Avatar */}
        <section className="relative flex flex-col items-center justify-center p-8">
          <div className="absolute inset-0 bg-gradient-to-b from-brand-600/10 via-transparent to-ai-600/10" />
          <div className="relative">
            <div className="absolute -inset-6 rounded-full bg-gradient-to-r from-brand-500 to-ai-500 opacity-30 blur-xl" />
            <div className="relative grid h-44 w-44 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 shadow-2xl">
              <Bot className="h-20 w-20 text-white" />
            </div>
          </div>
          <div className="relative mt-8 max-w-xl text-center">
            <p className="font-display text-xl font-bold leading-relaxed text-white">
              Sẵn sàng cho buổi phỏng vấn thử.
            </p>
            <p className="mt-2 flex items-center justify-center gap-1.5 text-sm text-slate-400">
              <Info className="h-4 w-4" /> AI phỏng vấn sẽ kết nối qua pipeline phỏng vấn (đang phát triển).
            </p>
          </div>

          {/* Self view */}
          <div className="absolute bottom-4 right-4 aspect-video w-44 overflow-hidden rounded-xl bg-black ring-1 ring-white/10">
            <video ref={selfVideoRef} muted playsInline className="h-full w-full -scale-x-100 object-cover" />
            {isMuted && (
              <span className="absolute bottom-1.5 left-1.5 grid h-6 w-6 place-items-center rounded-full bg-red-500/80">
                <MicOff className="h-3.5 w-3.5 text-white" />
              </span>
            )}
          </div>
        </section>

        {/* Transcript */}
        <aside className="flex flex-col border-l border-white/5 bg-ink-900/60">
          <div className="flex items-center justify-between border-b border-white/5 px-5 py-4">
            <span className="font-display font-bold text-white">Transcript</span>
            <span className="text-xs text-slate-400">JD + CV</span>
          </div>
          <div className="flex flex-1 items-center justify-center p-6 text-center text-sm text-slate-500">
            Cuộc trò chuyện sẽ hiển thị tại đây khi buổi phỏng vấn bắt đầu.
          </div>
        </aside>
      </main>

      {/* Controls */}
      <footer className="border-t border-white/5 px-6 py-4">
        <div className="mx-auto flex max-w-3xl items-center justify-center gap-3">
          <button
            onClick={toggleMute}
            className="grid h-12 w-12 place-items-center rounded-full bg-white/10 transition hover:bg-white/20"
            aria-label={isMuted ? 'Bật micro' : 'Tắt micro'}
          >
            {isMuted ? <MicOff className="h-5 w-5 text-red-400" /> : <Mic className="h-5 w-5 text-white" />}
          </button>
          <button
            onClick={endSession}
            className="flex h-12 items-center gap-2 rounded-full bg-red-600 px-6 font-semibold text-white transition hover:bg-red-700"
          >
            <Phone className="h-5 w-5" /> Kết thúc
          </button>
        </div>
        <p className="mt-3 text-center text-xs text-slate-500">
          Buổi thử chỉ để luyện tập — không ảnh hưởng đến kết quả tuyển dụng.
        </p>
      </footer>
    </div>
  )
}
