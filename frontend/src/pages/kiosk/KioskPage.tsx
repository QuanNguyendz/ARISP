import { useState, useRef, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { ArrowRight, Lock, Clock, ShieldCheck, RefreshCw, Bot } from 'lucide-react'

export default function KioskPage() {
  const navigate = useNavigate()
  const [code, setCode] = useState(['', '', '', '', '', ''])
  const inputRefs = useRef<(HTMLInputElement | null)[]>([])

  // Focus first input on mount
  useEffect(() => {
    inputRefs.current[0]?.focus()
  }, [])

  const handleInput = (index: number, value: string) => {
    const char = value
      .toUpperCase()
      .replace(/[^A-Z0-9]/g, '')
      .slice(-1)
    const newCode = [...code]
    newCode[index] = char
    setCode(newCode)

    // Auto advance to next input
    if (char && index < 5) {
      inputRefs.current[index + 1]?.focus()
    }
  }

  const handleKeyDown = (index: number, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace') {
      if (!code[index] && index > 0) {
        // Move to previous input and clear it
        inputRefs.current[index - 1]?.focus()
        const newCode = [...code]
        newCode[index - 1] = ''
        setCode(newCode)
      } else {
        const newCode = [...code]
        newCode[index] = ''
        setCode(newCode)
      }
    } else if (e.key === 'ArrowLeft' && index > 0) {
      inputRefs.current[index - 1]?.focus()
    } else if (e.key === 'ArrowRight' && index < 5) {
      inputRefs.current[index + 1]?.focus()
    }
  }

  const handlePaste = (e: React.ClipboardEvent) => {
    e.preventDefault()
    const pastedData = e.clipboardData
      .getData('text')
      .toUpperCase()
      .replace(/[^A-Z0-9]/g, '')
      .slice(0, 6)
    const newCode = [...code]
    pastedData.split('').forEach((char, i) => {
      newCode[i] = char
    })
    setCode(newCode)
    const focusIndex = Math.min(pastedData.length, 5)
    inputRefs.current[focusIndex]?.focus()
  }

  const fullCode = code.join('')
  const isCodeComplete = fullCode.length === 6

  const handleStartInterview = () => {
    if (isCodeComplete) {
      navigate(`/interview/room/${fullCode}`)
    }
  }

  return (
    <div className="min-h-screen bg-ink-950 text-slate-100 antialiased flex flex-col relative overflow-hidden">
      {/* Background effects */}
      <div className="absolute inset-0 overflow-hidden">
        <div className="absolute -top-40 left-1/2 -translate-x-1/2 h-[40rem] w-[40rem] rounded-full bg-brand-600/20 blur-3xl"></div>
        <div className="absolute bottom-0 right-0 h-96 w-96 rounded-full bg-ai-600/15 blur-3xl"></div>
      </div>

      {/* Header */}
      <header className="relative flex items-center justify-between px-8 h-16">
        <div className="flex items-center gap-2.5">
          <svg
            className="h-8 w-8"
            viewBox="0 0 96 96"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            aria-label="ARISP"
          >
            <defs>
              <linearGradient
                id="lg-k"
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
            <rect x="4" y="4" width="88" height="88" rx="22" fill="url(#lg-k)" />
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
          <span className="font-display text-lg font-extrabold">
            ARISP <span className="text-slate-400 font-medium">Kiosk</span>
          </span>
        </div>
        <span className="flex items-center gap-2 rounded-full bg-white/5 px-3 py-1.5 text-xs font-medium text-slate-300 ring-1 ring-white/10">
          <Lock className="w-3.5 h-3.5" /> Chế độ khoá · Văn phòng
        </span>
      </header>

      {/* Center */}
      <main className="relative flex-1 flex items-center justify-center px-6">
        <div className="w-full max-w-lg text-center">
          {/* Icon */}
          <motion.div
            initial={{ scale: 0.9, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-gradient-to-br from-brand-600 to-ai-600 shadow-2xl"
          >
            <Bot className="w-8 h-8 text-white" />
          </motion.div>

          <motion.h1
            initial={{ y: 10, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.1 }}
            className="mt-6 font-display text-3xl font-extrabold leading-[1.25]"
          >
            Nhập mã phỏng vấn
          </motion.h1>
          <motion.p
            initial={{ y: 10, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.2 }}
            className="mt-2 text-slate-400"
          >
            Nhập mã 6 ký tự do nhân viên tuyển dụng cung cấp để bắt đầu phỏng vấn.
          </motion.p>

          {/* Code inputs */}
          <motion.div
            initial={{ y: 10, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.3 }}
            className="mt-8 flex justify-center gap-2.5 sm:gap-3"
          >
            {code.map((char, index) => (
              <input
                key={index}
                ref={(el) => {
                  inputRefs.current[index] = el
                }}
                type="text"
                inputMode="text"
                autoComplete="off"
                maxLength={1}
                value={char}
                onChange={(e) => handleInput(index, e.target.value)}
                onKeyDown={(e) => handleKeyDown(index, e)}
                onPaste={handlePaste}
                onFocus={(e) => e.target.select()}
                className="h-16 w-12 sm:w-14 rounded-2xl border border-white/10 bg-white/5 text-center font-mono text-3xl font-bold uppercase text-white caret-brand-400 outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/40 transition-all"
                autoFocus={index === 0}
              />
            ))}
          </motion.div>

          {/* Start button */}
          <motion.div
            initial={{ y: 10, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.4 }}
          >
            <button
              onClick={handleStartInterview}
              disabled={!isCodeComplete}
              className={`mt-8 w-full rounded-2xl px-6 py-4 text-base font-bold flex items-center justify-center gap-2 transition-all ${
                isCodeComplete
                  ? 'bg-gradient-to-r from-brand-600 to-ai-600 hover:opacity-95 text-white'
                  : 'bg-white/10 text-slate-500 cursor-not-allowed'
              }`}
            >
              Bắt đầu phỏng vấn <ArrowRight className="w-5 h-5" />
            </button>
          </motion.div>

          {/* Info badges */}
          <motion.div
            initial={{ y: 10, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.5 }}
            className="mt-6 flex items-center justify-center gap-6 text-xs text-slate-500"
          >
            <span className="flex items-center gap-1.5">
              <Clock className="w-3.5 h-3.5" /> Mã hiệu lực 2 giờ
            </span>
            <span className="flex items-center gap-1.5">
              <ShieldCheck className="w-3.5 h-3.5" /> Dùng một lần
            </span>
            <span className="flex items-center gap-1.5">
              <RefreshCw className="w-3.5 h-3.5" /> Tự khôi phục khi mất kết nối
            </span>
          </motion.div>

          <motion.p
            initial={{ y: 10, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.6 }}
            className="mt-8 text-sm text-slate-500"
          >
            Chưa có mã? Vui lòng liên hệ nhân viên tuyển dụng tại quầy lễ tân.
          </motion.p>
        </div>
      </main>

      {/* Footer - Demo section */}
      <motion.div
        initial={{ y: 20, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        transition={{ delay: 0.7 }}
        className="relative p-6 text-center"
      >
        <p className="text-xs text-slate-600 mb-3">Demo codes để test:</p>
        <div className="flex items-center justify-center gap-4">
          {['ABC123', 'XYZ789', 'INT001'].map((demoCode) => (
            <button
              key={demoCode}
              onClick={() => {
                const newCode = demoCode.split('').concat(Array(6 - demoCode.length).fill(''))
                setCode(newCode)
                inputRefs.current[Math.min(demoCode.length, 5)]?.focus()
              }}
              className="px-3 py-1.5 rounded-lg bg-white/5 text-xs text-slate-400 hover:bg-white/10 hover:text-slate-200 transition-colors font-mono"
            >
              {demoCode}
            </button>
          ))}
        </div>
      </motion.div>
    </div>
  )
}
