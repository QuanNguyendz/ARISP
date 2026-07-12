import { useCallback, useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { LiveAvatarSession, SessionEvent, AgentEventsEnum } from '@heygen/liveavatar-web-sdk'
import { interviewService } from '@services/interview'
import { useAuthStore } from '@store/auth'
import { ASSET_BASE_URL } from '@config/constants'

export interface TranscriptItem {
  role: 'ai' | 'candidate'
  text: string
  seq?: number
}

export type PracticeStatus = 'idle' | 'starting' | 'live' | 'ended' | 'error'

const DEEPGRAM_WS_URL = 'wss://api.deepgram.com/v1/listen'
/** Số lần tự nối lại STT khi WebSocket rớt giữa phiên (mint token mới mỗi lần). */
const MAX_STT_RECONNECT = 3
/** Quá hạn chờ ReceiveQuestionAudio (BE TTS đẩy qua SignalR) → tự fetch /tts rồi browser TTS. */
const QUESTION_AUDIO_TIMEOUT_MS = 6000

/**
 * Điều phối toàn bộ luồng phỏng vấn thử:
 * startSession(practice) → media-config (token Deepgram/LiveAvatar) → khởi tạo song song
 * LiveAvatar + SignalR + Deepgram STT → StartInterview → nhận ReceiveQuestion (text)
 * + ReceiveQuestionAudio (PCM 24k từ ElevenLabs, BE đẩy sẵn — không round-trip /tts)
 * → avatar.repeatAudio lip-sync → Deepgram bắt câu trả lời → SubmitAnswerText → lặp.
 *
 * STT dùng WebSocket Deepgram trực tiếp với subprotocol ['bearer', token] — SDK v3 KHÔNG
 * hỗ trợ access token ngắn hạn (chỉ nhận API key) nên trước đây STT chết ngay khi khởi tạo.
 * Chống echo: bỏ mọi transcript khi AI đang nói (aiSpeakingRef) + xóa buffer khi AI nói xong.
 *
 * Mọi nhánh media đều fallback mềm: thiếu avatar → phát PCM qua WebAudio / browser TTS;
 * thiếu Deepgram → nhập tay + nút "Gửi trả lời".
 */
export function usePracticeSession(applicationId: string, roundNumber = 1) {
  const [status, setStatus] = useState<PracticeStatus>('idle')
  const [error, setError] = useState<string | null>(null)
  const [messages, setMessages] = useState<TranscriptItem[]>([])
  const [interim, setInterim] = useState('')
  const [aiSpeaking, setAiSpeaking] = useState(false)
  const [listening, setListening] = useState(false)
  const [avatarReady, setAvatarReady] = useState(false)
  const [sttEnabled, setSttEnabled] = useState(false)

  const videoRef = useRef<HTMLVideoElement | null>(null)
  const connRef = useRef<signalR.HubConnection | null>(null)
  const avatarRef = useRef<LiveAvatarSession | null>(null)
  const avatarReadyRef = useRef(false)
  const dgSocketRef = useRef<WebSocket | null>(null)
  const dgRetryRef = useRef(0)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const micStreamRef = useRef<MediaStream | null>(null)
  const languageRef = useRef('vi')
  const sessionIdRef = useRef<string | null>(null)
  const currentQuestionRef = useRef<{ id: string; askedAt: number } | null>(null)
  const finalBufRef = useRef('')
  const interimRef = useRef('')
  const aiSpeakingRef = useRef(false)
  const endedRef = useRef(false)
  const audioCtxRef = useRef<AudioContext | null>(null)
  const audioSrcRef = useRef<AudioBufferSourceNode | null>(null)
  const questionAudioTimerRef = useRef<number | null>(null)
  const audioPlayedForRef = useRef<string | null>(null)
  const keepAliveTimerRef = useRef<number | null>(null)

  const updateInterim = useCallback((v: string) => {
    interimRef.current = v
    setInterim(v)
  }, [])

  const setSpeaking = useCallback((v: boolean) => {
    aiSpeakingRef.current = v
    setAiSpeaking(v)
  }, [])

  /** AI nói xong: xả buffer (loại echo lọt vào lúc AI nói) + tính giờ trả lời từ thời điểm này. */
  const handleSpeakEnded = useCallback(() => {
    setSpeaking(false)
    finalBufRef.current = ''
    updateInterim('')
    if (currentQuestionRef.current) currentQuestionRef.current.askedAt = Date.now()
  }, [setSpeaking, updateInterim])

  const submitAnswer = useCallback(() => {
    const q = currentQuestionRef.current
    const sessionId = sessionIdRef.current
    const answer = `${finalBufRef.current} ${interimRef.current}`.trim()
    if (!q || !sessionId || !answer) return
    finalBufRef.current = ''
    updateInterim('')
    setMessages((m) => [...m, { role: 'candidate', text: answer }])
    const responseMs = Date.now() - q.askedAt
    connRef.current?.invoke('SubmitAnswerText', sessionId, q.id, answer, responseMs).catch(() => {})
    currentQuestionRef.current = null
  }, [updateInterim])

  /** Fallback không avatar: phát PCM 16-bit mono 24kHz (base64 từ ElevenLabs) qua WebAudio. */
  const playPcmViaWebAudio = useCallback(
    async (base64: string) => {
      const AC =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext
      const ctx = audioCtxRef.current ?? new AC()
      audioCtxRef.current = ctx
      await ctx.resume().catch(() => {})

      const raw = atob(base64)
      const bytes = new Uint8Array(raw.length)
      for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i)
      const samples = new Int16Array(bytes.buffer, 0, Math.floor(bytes.length / 2))
      const buffer = ctx.createBuffer(1, samples.length, 24000)
      const channel = buffer.getChannelData(0)
      for (let i = 0; i < samples.length; i++) channel[i] = samples[i] / 32768

      try {
        audioSrcRef.current?.stop()
      } catch {
        /* noop */
      }
      const src = ctx.createBufferSource()
      src.buffer = buffer
      src.connect(ctx.destination)
      src.onended = handleSpeakEnded
      setSpeaking(true)
      src.start()
      audioSrcRef.current = src
    },
    [handleSpeakEnded, setSpeaking]
  )

  const speakBrowserTts = useCallback(
    (text: string) => {
      try {
        const u = new SpeechSynthesisUtterance(text)
        u.lang = languageRef.current
        setSpeaking(true)
        u.onend = handleSpeakEnded
        window.speechSynthesis.speak(u)
      } catch {
        /* không hỗ trợ TTS — bỏ qua, vẫn hiển thị text */
      }
    },
    [handleSpeakEnded, setSpeaking]
  )

  /** Phát audio câu hỏi: avatar lip-sync (repeatAudio) nếu sẵn sàng, không thì WebAudio. */
  const playQuestionAudio = useCallback(
    (questionId: string, base64: string) => {
      if (audioPlayedForRef.current === questionId) return // đã phát (chống phát đôi khi audio đến trễ)
      audioPlayedForRef.current = questionId
      const session = avatarRef.current
      if (session && avatarReadyRef.current) {
        try {
          session.repeatAudio(base64)
          return
        } catch {
          /* rơi xuống WebAudio */
        }
      }
      void playPcmViaWebAudio(base64)
    },
    [playPcmViaWebAudio]
  )

  const initAvatar = useCallback(
    async (
      cfg: NonNullable<Awaited<ReturnType<typeof interviewService.getMediaConfig>>['heyGen']>
    ) => {
      if (!cfg?.token) return // chưa cấu hình avatar → fallback WebAudio/browser TTS
      // LiveAvatar LITE: BE đã mint session token; KHÔNG dùng voiceChat của SDK (STT tự làm bằng Deepgram).
      const session = new LiveAvatarSession(cfg.token, {
        voiceChat: false,
        apiUrl: cfg.serverUrl,
      })
      avatarRef.current = session
      session.on(SessionEvent.SESSION_STREAM_READY, () => {
        if (videoRef.current) {
          try {
            session.attach(videoRef.current) // gắn cả video + audio track vào <video>
          } catch {
            /* ignore */
          }
          avatarReadyRef.current = true
          setAvatarReady(true)
        }
      })
      session.on(SessionEvent.SESSION_DISCONNECTED, () => {
        avatarReadyRef.current = false
        setAvatarReady(false) // các câu hỏi sau tự rơi xuống WebAudio
      })
      session.on(AgentEventsEnum.AVATAR_SPEAK_STARTED, () => setSpeaking(true))
      session.on(AgentEventsEnum.AVATAR_SPEAK_ENDED, handleSpeakEnded)
      await session.start()
      // LITE session có thể bị idle-timeout khi AI im lâu (ứng viên suy nghĩ) → giữ sống định kỳ.
      keepAliveTimerRef.current = window.setInterval(() => {
        avatarRef.current?.keepAlive().catch(() => {})
      }, 60_000)
    },
    [handleSpeakEnded, setSpeaking]
  )

  /**
   * STT Deepgram qua WebSocket trực tiếp, auth bằng subprotocol ['bearer', <access token>].
   * MediaRecorder (webm/opus, 250ms/chunk) đẩy liên tục — kể cả lúc AI nói (giữ stream liền mạch),
   * transcript trong lúc AI nói bị bỏ ở phía nhận.
   */
  const initDeepgram = useCallback(
    (cfg: { token: string; model: string }, micStream: MediaStream, language: string) => {
      const params = new URLSearchParams({
        model: cfg.model || 'nova-2',
        language: language || 'vi',
        interim_results: 'true',
        smart_format: 'true',
        vad_events: 'true',
        endpointing: '300',
        utterance_end_ms: '1000',
      })
      const ws = new WebSocket(`${DEEPGRAM_WS_URL}?${params.toString()}`, ['bearer', cfg.token])
      dgSocketRef.current = ws

      ws.onopen = () => {
        dgRetryRef.current = 0
        console.info('[deepgram] OPEN — model:', cfg.model, '| lang:', language)
        try {
          recorderRef.current?.stop() // recorder cũ (nếu reconnect) trỏ socket chết
        } catch {
          /* noop */
        }
        try {
          const mime = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
            ? 'audio/webm;codecs=opus'
            : 'audio/webm'
          const rec = new MediaRecorder(micStream, { mimeType: mime })
          recorderRef.current = rec
          rec.ondataavailable = (ev: BlobEvent) => {
            if (ev.data.size > 0 && ws.readyState === WebSocket.OPEN) ws.send(ev.data)
          }
          rec.start(250)
          setListening(true)
          setSttEnabled(true)
        } catch (e) {
          console.warn('[deepgram] không khởi tạo được MediaRecorder', e)
          setSttEnabled(false)
        }
      }

      ws.onmessage = (ev: MessageEvent) => {
        let data: any
        try {
          data = JSON.parse(ev.data as string)
        } catch {
          return
        }
        if (data.type === 'Results') {
          const text: string = data.channel?.alternatives?.[0]?.transcript ?? ''
          if (!text) return
          if (aiSpeakingRef.current) return // echo giọng avatar lọt vào mic → bỏ
          if (data.is_final) {
            finalBufRef.current = `${finalBufRef.current} ${text}`.trim()
            updateInterim('')
          } else {
            updateInterim(text)
          }
        } else if (data.type === 'UtteranceEnd') {
          if (!aiSpeakingRef.current) submitAnswer()
        }
      }

      ws.onerror = (e) => console.error('[deepgram] ERROR', e)

      ws.onclose = (e) => {
        setListening(false)
        if (endedRef.current) return
        console.warn('[deepgram] CLOSE', e.code, e.reason)
        // Rớt giữa phiên (token 1 lần chỉ dùng lúc handshake nên đây là sự cố mạng/servers)
        // → mint token mới + nối lại, tối đa MAX_STT_RECONNECT lần.
        if (dgRetryRef.current >= MAX_STT_RECONNECT) {
          setSttEnabled(false)
          return
        }
        dgRetryRef.current += 1
        window.setTimeout(async () => {
          if (endedRef.current) return
          try {
            const media = await interviewService.getMediaConfig(sessionIdRef.current!)
            if (media.deepgram?.token) {
              initDeepgram(media.deepgram, micStream, languageRef.current)
            } else {
              setSttEnabled(false)
            }
          } catch {
            setSttEnabled(false)
          }
        }, 500 * dgRetryRef.current)
      }
    },
    [submitAnswer, updateInterim]
  )

  const connectHub = useCallback(async () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${ASSET_BASE_URL}/hubs/session`, {
        accessTokenFactory: () => useAuthStore.getState().tokens?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .build()
    connRef.current = conn

    conn.on('ReceiveQuestion', (p: any) => {
      currentQuestionRef.current = { id: p.questionId, askedAt: Date.now() }
      finalBufRef.current = ''
      updateInterim('')
      setMessages((m) => [...m, { role: 'ai', text: p.questionText, seq: p.sequenceNumber }])
      // Audio do BE TTS đẩy qua ReceiveQuestionAudio (thường <2s). Quá hạn → tự fetch /tts,
      // vẫn không có → browser TTS (giữ đúng nhịp phỏng vấn dù thiếu ElevenLabs).
      if (questionAudioTimerRef.current) window.clearTimeout(questionAudioTimerRef.current)
      questionAudioTimerRef.current = window.setTimeout(async () => {
        if (audioPlayedForRef.current === p.questionId) return
        try {
          const audio = await interviewService.getTtsAudio(sessionIdRef.current!, p.questionText)
          if (audio) {
            playQuestionAudio(p.questionId, audio)
            return
          }
        } catch {
          /* rơi xuống browser TTS */
        }
        audioPlayedForRef.current = p.questionId
        speakBrowserTts(p.questionText)
      }, QUESTION_AUDIO_TIMEOUT_MS)
    })

    conn.on('ReceiveQuestionAudio', (p: any) => {
      if (!p?.audio || !p?.questionId) return
      if (currentQuestionRef.current && currentQuestionRef.current.id !== p.questionId) return
      if (questionAudioTimerRef.current) window.clearTimeout(questionAudioTimerRef.current)
      playQuestionAudio(p.questionId, p.audio)
    })

    conn.on('ReceiveSessionStatus', (p: any) => {
      const st = typeof p === 'string' ? p : p?.status
      if (st === 'completed') setStatus('ended')
    })
    // ReceiveAnswerAnalysis / ReceiveCheatAlert: chưa hiển thị ở practice

    await conn.start()
  }, [playQuestionAudio, speakBrowserTts, updateInterim])

  const start = useCallback(
    async (micStream: MediaStream) => {
      try {
        setStatus('starting')
        setError(null)
        endedRef.current = false
        micStreamRef.current = micStream

        const session = await interviewService.startSession({
          applicationId,
          roundNumber,
          sessionType: 'practice',
        })
        const sessionId = session.sessionId
        sessionIdRef.current = sessionId

        const media = await interviewService.getMediaConfig(sessionId)
        languageRef.current = media.language || 'vi'

        // STT không phụ thuộc avatar/hub → mở ngay; avatar + hub khởi tạo song song
        // (trước đây tuần tự: avatar chậm làm token Deepgram hết hạn trước khi STT kịp nối).
        if (media.deepgram?.token) {
          try {
            initDeepgram(media.deepgram, micStream, languageRef.current)
          } catch {
            setSttEnabled(false)
          }
        }

        const boot: Promise<unknown>[] = [connectHub()]
        if (media.heyGen?.token) {
          boot.push(
            initAvatar(media.heyGen).catch(() => {
              /* avatar lỗi → fallback WebAudio/browser TTS */
            })
          )
        }
        await Promise.all(boot)

        setStatus('live')

        await connRef.current?.invoke('JoinSession', sessionId)
        await connRef.current?.invoke('StartInterview', sessionId)
      } catch (e: any) {
        setError(e?.response?.data?.message ?? e?.message ?? 'Không thể bắt đầu phỏng vấn thử.')
        setStatus('error')
      }
    },
    [applicationId, roundNumber, initAvatar, connectHub, initDeepgram]
  )

  const cleanup = useCallback(() => {
    endedRef.current = true
    if (questionAudioTimerRef.current) window.clearTimeout(questionAudioTimerRef.current)
    if (keepAliveTimerRef.current) window.clearInterval(keepAliveTimerRef.current)
    try {
      recorderRef.current?.stop()
    } catch {
      /* noop */
    }
    try {
      dgSocketRef.current?.close()
    } catch {
      /* noop */
    }
    try {
      audioSrcRef.current?.stop()
    } catch {
      /* noop */
    }
    try {
      audioCtxRef.current?.close()
    } catch {
      /* noop */
    }
    audioCtxRef.current = null
    try {
      avatarRef.current?.stop()
    } catch {
      /* noop */
    }
    try {
      connRef.current?.stop()
    } catch {
      /* noop */
    }
    try {
      window.speechSynthesis?.cancel()
    } catch {
      /* noop */
    }
  }, [])

  const end = useCallback(async () => {
    const sessionId = sessionIdRef.current
    cleanup()
    if (sessionId) {
      try {
        await interviewService.endSession(sessionId)
      } catch {
        /* noop */
      }
    }
    setStatus('ended')
  }, [cleanup])

  useEffect(() => () => cleanup(), [cleanup])

  return {
    status,
    error,
    messages,
    interim,
    aiSpeaking,
    listening,
    avatarReady,
    sttEnabled,
    videoRef,
    start,
    end,
    submitAnswer,
  }
}
