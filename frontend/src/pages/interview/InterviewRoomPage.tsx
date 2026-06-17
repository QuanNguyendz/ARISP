import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import {
  Mic,
  MicOff,
  Captions,
  Phone,
  Settings,
  Wifi,
  Bot,
  MessageSquare,
  ChevronRight,
} from 'lucide-react'

// Mock data for demo
const mockSession = {
  title: 'Senior Frontend Engineer',
  round: 'Round 1',
  roundType: 'Screening',
}

const mockConversation = [
  {
    type: 'ai',
    text: 'Chào bạn, chúng ta bắt đầu vòng screening nhé. Hãy giới thiệu ngắn gọn về bản thân.',
  },
  {
    type: 'candidate',
    text: 'Mình là Hoàng, 5 năm kinh nghiệm Frontend với React và TypeScript. Mình đã làm việc ở nhiều dự án từ startup đến enterprise.',
  },
]

const mockQuestions = [
  'Xin chào! Rất vui được trò chuyện với bạn. Bạn có thể giới thiệu về bản thân được không?',
  'Bạn có thể cho tôi biết về kinh nghiệm làm việc với React của bạn không?',
  'Bạn hãy mô tả một lần tối ưu hiệu năng cho ứng dụng React lớn — bạn đã đo lường và cải thiện thế nào?',
]

function SpeakingIndicator() {
  return (
    <div className="relative flex items-end gap-1 h-4">
      {[...Array(5)].map((_, i) => (
        <motion.span
          key={i}
          className="inline-block w-1 bg-ai-400 rounded-full"
          animate={{ height: ['25%', '100%', '25%', '75%', '25%'] }}
          transition={{ duration: 1, repeat: Infinity, ease: 'easeInOut', delay: i * 0.15 }}
        />
      ))}
    </div>
  )
}

export default function InterviewRoomPage() {
  const { sessionId } = useParams<{ sessionId: string }>()
  const navigate = useNavigate()
  const [isMuted, setIsMuted] = useState(false)
  const [isCaptionsOn, setIsCaptionsOn] = useState(true)
  const [isRecording] = useState(true)
  const [recordingTime, setRecordingTime] = useState('12:48')
  const [currentQuestion, setCurrentQuestion] = useState(2)
  const [conversation, setConversation] = useState(mockConversation)
  const [isAiSpeaking, setIsAiSpeaking] = useState(false)
  const [isInterviewEnded, setIsInterviewEnded] = useState(false)

  // Simulate recording timer
  useEffect(() => {
    if (!isRecording) return
    const interval = setInterval(() => {
      setRecordingTime((prev) => {
        const [min, sec] = prev.split(':').map(Number)
        const newSec = sec + 1
        if (newSec >= 60) {
          return `${min + 1}:${String(newSec - 60).padStart(2, '0')}`
        }
        return `${min}:${String(newSec).padStart(2, '0')}`
      })
    }, 1000)
    return () => clearInterval(interval)
  }, [isRecording])

  // Simulate AI speaking
  useEffect(() => {
    const timeout = setTimeout(() => setIsAiSpeaking(true), 2000)
    return () => clearTimeout(timeout)
  }, [])

  const handleEndInterview = () => {
    setIsInterviewEnded(true)
  }

  const handleNextQuestion = () => {
    if (currentQuestion < mockQuestions.length) {
      setIsAiSpeaking(true)
      setTimeout(() => {
        setConversation((prev) => [...prev, { type: 'ai', text: mockQuestions[currentQuestion] }])
        setIsAiSpeaking(false)
        setCurrentQuestion((prev) => prev + 1)
      }, 2000)
    }
  }

  if (isInterviewEnded) {
    return (
      <div className="flex-1 bg-ink-950 flex items-center justify-center min-h-0">
        <motion.div
          initial={{ opacity: 0, scale: 0.9 }}
          animate={{ opacity: 1, scale: 1 }}
          className="text-center max-w-md"
        >
          <div className="w-24 h-24 rounded-full bg-emerald-500/20 flex items-center justify-center mx-auto mb-6">
            <MessageSquare className="w-12 h-12 text-emerald-400" />
          </div>
          <h1 className="text-3xl font-display font-bold text-white mb-4">Hoàn thành phỏng vấn!</h1>
          <p className="text-slate-400 mb-8">
            Cảm ơn bạn đã tham gia phỏng vấn. Kết quả sẽ được gửi qua email trong vòng 24 giờ.
          </p>
          <button
            onClick={() => navigate('/candidate/results')}
            className="px-8 py-4 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-semibold hover:opacity-90 transition-opacity"
          >
            Xem kết quả
          </button>
        </motion.div>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen flex-col bg-ink-950 text-slate-100">
      {/* Top status bar */}
      <header className="flex items-center justify-between px-6 h-14 border-b border-white/5">
        <div className="flex items-center gap-3">
          <svg
            className="h-7 w-7"
            viewBox="0 0 96 96"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            aria-label="ARISP"
          >
            <defs>
              <linearGradient
                id="lg-ir"
                x1="12"
                y1="10"
                x2="84"
                y2="86"
                gradientUnits="userSpaceOnUse"
              >
                <stop stopColor="#6366f1" />
                <stop offset="1" stopColor="#a855f7" />
              </linearGradient>
            </defs>
            <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-ir)" />
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
          <div className="text-sm">
            <span className="font-semibold text-white">{mockSession.title}</span>
            <span className="text-slate-400">
              {' '}
              · {mockSession.round} · {mockSession.roundType}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-4">
          <span className="flex items-center gap-2 rounded-full bg-red-500/10 px-3 py-1 text-sm font-medium text-red-400">
            <span className="h-2 w-2 rounded-full bg-red-500 animate-pulse" />
            REC {recordingTime}
          </span>
          <span className="flex items-center gap-2 text-sm text-emerald-400">
            <Wifi className="w-4 h-4" /> Ổn định
          </span>
        </div>
      </header>

      {/* Main content */}
      <main className="flex-1 grid lg:grid-cols-[1fr_380px]">
        {/* Avatar / question */}
        <section className="relative flex flex-col items-center justify-center p-8">
          <div className="absolute inset-0 bg-gradient-to-b from-brand-600/10 via-transparent to-ai-600/10" />

          {/* Avatar */}
          <motion.div
            className="relative"
            animate={{ scale: isAiSpeaking ? [1, 1.02, 1] : 1 }}
            transition={{ duration: 2, repeat: isAiSpeaking ? Infinity : 0 }}
          >
            <motion.div
              className="absolute -inset-6 rounded-full bg-gradient-to-r from-brand-500 to-ai-500 blur-xl opacity-50"
              animate={{ opacity: isAiSpeaking ? [0.5, 0.7, 0.5] : 0.3 }}
              transition={{ duration: 2, repeat: Infinity }}
            />
            <div className="relative grid h-44 w-44 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600 shadow-2xl">
              <Bot className="w-20 h-20 text-white" />
            </div>
          </motion.div>

          {/* Speaking indicator */}
          <motion.div
            className="relative mt-6 flex items-center gap-2 rounded-full bg-white/5 px-4 py-2 ring-1 ring-white/10"
            animate={{ opacity: isAiSpeaking ? 1 : 0.5 }}
          >
            <SpeakingIndicator />
            <span className="text-sm font-medium text-ai-400">AI đang nói…</span>
          </motion.div>

          {/* Current question */}
          <div className="relative mt-8 max-w-xl text-center">
            <div className="text-xs uppercase tracking-widest text-slate-500 mb-2">
              Câu hỏi {currentQuestion + 1} / {mockQuestions.length}
            </div>
            <p className="font-display text-2xl font-bold leading-relaxed text-white">
              {mockQuestions[currentQuestion - 1] || mockQuestions[0]}
            </p>
          </div>
        </section>

        {/* Transcript panel */}
        <aside className="border-l border-white/5 bg-ink-900/60 flex flex-col">
          <div className="px-5 py-4 border-b border-white/5 flex items-center justify-between">
            <span className="font-display font-bold text-white">Transcript</span>
            <span className="text-xs text-slate-400">Streaming</span>
          </div>
          <div className="flex-1 overflow-y-auto p-5 space-y-4 text-sm">
            {conversation.map((msg, index) => (
              <motion.div
                key={index}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className={`flex gap-3 ${msg.type === 'candidate' ? 'flex-row-reverse' : ''}`}
              >
                {msg.type === 'ai' ? (
                  <div className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-gradient-to-br from-brand-600 to-ai-600">
                    <Bot className="w-4 h-4 text-white" />
                  </div>
                ) : (
                  <div className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-slate-700 text-xs font-bold text-white">
                    LH
                  </div>
                )}
                <div
                  className={`rounded-2xl px-4 py-2.5 max-w-[80%] ${
                    msg.type === 'ai'
                      ? 'rounded-tl-sm bg-white/5 text-slate-200'
                      : 'rounded-tr-sm bg-brand-600/20 text-slate-100'
                  }`}
                >
                  <p>{msg.text}</p>
                  {index === conversation.length - 1 && msg.type === 'ai' && (
                    <span className="inline-block w-1.5 h-4 align-middle bg-ai-400 ml-0.5 animate-pulse" />
                  )}
                </div>
              </motion.div>
            ))}
          </div>
        </aside>
      </main>

      {/* Controls */}
      <footer className="border-t border-white/5 px-6 py-4">
        <div className="mx-auto flex max-w-3xl items-center justify-center gap-3">
          <button
            onClick={() => setIsMuted(!isMuted)}
            className="grid h-12 w-12 place-items-center rounded-full bg-white/10 hover:bg-white/20 transition"
          >
            {isMuted ? (
              <MicOff className="w-5 h-5 text-red-400" />
            ) : (
              <Mic className="w-5 h-5 text-white" />
            )}
          </button>
          <button
            onClick={() => setIsCaptionsOn(!isCaptionsOn)}
            className={`grid h-12 w-12 place-items-center rounded-full transition ${
              isCaptionsOn
                ? 'bg-brand-500/30 text-brand-400'
                : 'bg-white/10 text-white hover:bg-white/20'
            }`}
          >
            <Captions className="w-5 h-5" />
          </button>
          <button
            onClick={handleEndInterview}
            className="flex items-center gap-2 rounded-full bg-red-600 hover:bg-red-700 px-6 h-12 font-semibold transition text-white"
          >
            <Phone className="w-5 h-5" /> Kết thúc phỏng vấn
          </button>
          <button className="grid h-12 w-12 place-items-center rounded-full bg-white/10 hover:bg-white/20 transition">
            <Settings className="w-5 h-5 text-white" />
          </button>
        </div>
        <p className="mt-3 text-center text-xs text-slate-500">
          Mất kết nối sẽ tự động khôi phục phiên khi bạn nhập lại Interview Code
        </p>
      </footer>
    </div>
  )
}
