import { useCallback, useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { LiveAvatarSession, SessionEvent, AgentEventsEnum } from '@heygen/liveavatar-web-sdk'
import { interviewService } from '@ari/shared/fservices/interview'
import { useAuthStore } from '@ari/shared/store/auth'
import { ASSET_BASE_URL } from '@ari/shared/config/constants'

export interface TranscriptItem {
  role: 'ai' | 'candidate'
  text: string
  seq?: number
}

export type PracticeStatus = 'idle' | 'starting' | 'live' | 'ended' | 'error'

/** Payload SignalR từ SessionHub (ISessionClient phía BE). */
interface QuestionPayload {
  questionId: string
  questionText: string
  sequenceNumber?: number
}
interface QuestionAudioPayload {
  questionId?: string
  audio?: string // PCM 16-bit mono 24kHz, base64
}
type SessionStatusPayload = string | { status?: string }

const DEEPGRAM_WS_URL = 'wss://api.deepgram.com/v1/listen'
/** Số lần tự nối lại STT khi WebSocket rớt giữa phiên (mint token mới mỗi lần). */
const MAX_STT_RECONNECT = 3
/** Số lần tự dựng lại LiveAvatar khi session rớt giữa buổi (LITE hay idle-timeout). */
const MAX_AVATAR_RECONNECT = 2
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
 * Watchdog speaking: HeyGen đôi khi KHÔNG bắn AVATAR_SPEAK_ENDED → cờ aiSpeaking kẹt true
 * và nuốt toàn bộ transcript ứng viên; đặt timer theo độ dài audio để tự nhả cờ.
 *
 * Gửi trả lời THỦ CÔNG: transcript tích lũy hiển thị live (draft), ứng viên bấm
 * "Gửi trả lời" mới submit — không auto-submit khi im lặng (UtteranceEnd chỉ flush interim).
 *
 * Mọi nhánh media đều fallback mềm: thiếu avatar → phát PCM qua WebAudio / browser TTS;
 * thiếu Deepgram → nhập tay + nút "Gửi trả lời".
 */
export function usePracticeSession(applicationId: string, roundNumber = 1) {
  const [status, setStatus] = useState<PracticeStatus>('idle')
  const [error, setError] = useState<string | null>(null)
  const [messages, setMessages] = useState<TranscriptItem[]>([])
  const [interim, setInterim] = useState('')
  const [draft, setDraft] = useState('') // transcript đã chốt (is_final) chờ ứng viên bấm "Gửi trả lời"
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
  const speakWatchdogRef = useRef<number | null>(null)
  const closingRef = useRef(false) // đã nhận lời cảm ơn kết thúc từ AI (ReceiveClosing)
  const pendingEndRef = useRef(false) // server báo completed nhưng chờ AI nói xong lời cảm ơn
  const closingAudioTimerRef = useRef<number | null>(null)

  const updateInterim = useCallback((v: string) => {
    interimRef.current = v
    setInterim(v)
  }, [])

  /** Đồng bộ draft hiển thị với buffer transcript đã chốt. */
  const syncDraft = useCallback(() => {
    setDraft(finalBufRef.current)
  }, [])

  const setSpeaking = useCallback((v: boolean) => {
    if (aiSpeakingRef.current !== v)
      console.info('[speak]', v ? 'AI bắt đầu nói' : 'AI nói xong — đang nghe ứng viên')
    aiSpeakingRef.current = v
    setAiSpeaking(v)
  }, [])

  /** AI nói xong: xả buffer (loại echo lọt vào lúc AI nói) + tính giờ trả lời từ thời điểm này. */
  const handleSpeakEnded = useCallback(() => {
    if (speakWatchdogRef.current) {
      window.clearTimeout(speakWatchdogRef.current)
      speakWatchdogRef.current = null
    }
    setSpeaking(false)
    finalBufRef.current = ''
    updateInterim('')
    setDraft('')
    if (currentQuestionRef.current) currentQuestionRef.current.askedAt = Date.now()
    // Lời cảm ơn kết thúc vừa phát xong + server đã báo completed → giờ mới chuyển màn kết thúc.
    if (pendingEndRef.current) {
      pendingEndRef.current = false
      endedRef.current = true
      setStatus('ended')
    }
  }, [setSpeaking, updateInterim])

  /**
   * Chốt chặn cờ aiSpeaking: nếu sự kiện "nói xong" (HeyGen AVATAR_SPEAK_ENDED / onended)
   * không bắn, cờ kẹt true sẽ nuốt mọi transcript của ứng viên → tự nhả sau thời lượng dự kiến.
   * speakUntilRef ghi "hạn sử dụng" của lượt nói hiện tại — mọi đường set cờ đều phải qua đây
   * (KHÔNG bao giờ setSpeaking(true) mà không kèm watchdog, tránh kẹt vĩnh viễn).
   */
  const speakUntilRef = useRef(0)
  const armSpeakWatchdog = useCallback(
    (expectedMs: number) => {
      const ms = Math.min(expectedMs, 90_000) // trần cứng — không lượt nói nào quá 90s
      speakUntilRef.current = Date.now() + ms
      if (speakWatchdogRef.current) window.clearTimeout(speakWatchdogRef.current)
      speakWatchdogRef.current = window.setTimeout(() => {
        if (aiSpeakingRef.current) {
          console.warn('[speak-watchdog] không nhận được sự kiện nói xong — tự nhả cờ aiSpeaking')
          handleSpeakEnded()
        }
      }, ms)
    },
    [handleSpeakEnded]
  )

  const submitAnswer = useCallback(() => {
    const q = currentQuestionRef.current
    const sessionId = sessionIdRef.current
    const answer = `${finalBufRef.current} ${interimRef.current}`.trim()
    if (!q || !sessionId || !answer) return
    finalBufRef.current = ''
    updateInterim('')
    setDraft('')
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
      armSpeakWatchdog(buffer.duration * 1000 + 3000)
      src.start()
      audioSrcRef.current = src
    },
    [armSpeakWatchdog, handleSpeakEnded, setSpeaking]
  )

  const speakBrowserTts = useCallback(
    (text: string) => {
      try {
        const u = new SpeechSynthesisUtterance(text)
        u.lang = languageRef.current
        setSpeaking(true)
        // Không biết trước thời lượng → ước lượng theo độ dài text (~90ms/ký tự + đệm).
        armSpeakWatchdog(text.length * 90 + 4000)
        u.onend = handleSpeakEnded
        window.speechSynthesis.speak(u)
      } catch {
        /* không hỗ trợ TTS — bỏ qua, vẫn hiển thị text */
      }
    },
    [armSpeakWatchdog, handleSpeakEnded, setSpeaking]
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
          // HeyGen có khi không bắn AVATAR_SPEAK_STARTED/ENDED → tự set cờ + watchdog
          // theo thời lượng PCM 16-bit mono 24kHz (base64 → bytes ≈ len * 3/4)
          // + đệm rộng 6s vì avatar bắt đầu phát TRỄ (network/xử lý phía HeyGen).
          const pcmBytes = Math.floor(base64.length * 0.75)
          const durationMs = (pcmBytes / 2 / 24000) * 1000
          setSpeaking(true)
          armSpeakWatchdog(durationMs + 6000)
          return
        } catch {
          /* rơi xuống WebAudio */
        }
      }
      void playPcmViaWebAudio(base64)
    },
    [armSpeakWatchdog, playPcmViaWebAudio, setSpeaking]
  )

  const avatarRetryRef = useRef(0)

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
          avatarRetryRef.current = 0 // stream sống lại → reset quota reconnect
        }
      })
      session.on(SessionEvent.SESSION_DISCONNECTED, () => {
        avatarReadyRef.current = false
        setAvatarReady(false) // trong lúc chờ reconnect, câu hỏi tự rơi xuống WebAudio
        // Avatar chết GIỮA câu nói (gói free LiveAvatar cắt phiên sau 2 phút!) →
        // SPEAK_ENDED không bao giờ bắn → nhả cờ ngay để STT tiếp tục nhận giọng ứng viên.
        if (aiSpeakingRef.current) {
          console.warn('[avatar] disconnected giữa lượt nói — nhả cờ aiSpeaking')
          handleSpeakEnded()
        }
        if (endedRef.current) return
        // Session LITE rớt giữa buổi (idle-timeout/mạng) → token cũ đã vô hiệu:
        // mint token MỚI qua media-config rồi dựng lại session, tối đa MAX_AVATAR_RECONNECT lần.
        if (avatarRetryRef.current >= MAX_AVATAR_RECONNECT) {
          console.warn('[avatar] hết quota reconnect — dùng WebAudio đến hết buổi')
          return
        }
        avatarRetryRef.current += 1
        console.warn(
          `[avatar] DISCONNECTED — thử dựng lại (${avatarRetryRef.current}/${MAX_AVATAR_RECONNECT})`
        )
        window.setTimeout(async () => {
          if (endedRef.current) return
          try {
            const media = await interviewService.getMediaConfig(sessionIdRef.current!)
            if (media.heyGen?.token) await initAvatar(media.heyGen)
          } catch (e) {
            console.warn('[avatar] reconnect thất bại', e)
          }
        }, 1000 * avatarRetryRef.current)
      })
      // SPEAK_STARTED có thể bắn TRỄ (sau khi watchdog đã nhả cờ) — chỉ chấp nhận khi vẫn
      // trong cửa sổ lượt nói dự kiến (+10s grace); quá hạn = sự kiện lạc → bỏ, nếu không cờ
      // sẽ kẹt true (SPEAK_ENDED không đáng tin) và nuốt toàn bộ transcript của ứng viên.
      session.on(AgentEventsEnum.AVATAR_SPEAK_STARTED, () => {
        if (Date.now() > speakUntilRef.current + 10_000) {
          console.warn('[avatar] SPEAK_STARTED lạc ngoài cửa sổ lượt nói — bỏ qua')
          return
        }
        setSpeaking(true)
        // Gia hạn watchdog theo thời gian dự kiến còn lại (avatar phát trễ nên cần thêm).
        armSpeakWatchdog(Math.max(speakUntilRef.current - Date.now(), 3000) + 3000)
      })
      // SPEAK_ENDED lạc (đến khi ứng viên đang trả lời) sẽ xóa oan buffer → chỉ xử lý khi đang nói.
      session.on(AgentEventsEnum.AVATAR_SPEAK_ENDED, () => {
        if (aiSpeakingRef.current) handleSpeakEnded()
      })
      await session.start()
      // LITE session có thể bị idle-timeout khi AI im lâu (ứng viên suy nghĩ) → giữ sống định kỳ.
      if (keepAliveTimerRef.current) window.clearInterval(keepAliveTimerRef.current)
      keepAliveTimerRef.current = window.setInterval(() => {
        avatarRef.current?.keepAlive().catch(() => {})
      }, 60_000)
    },
    [armSpeakWatchdog, handleSpeakEnded, setSpeaking]
  )

  /**
   * STT Deepgram qua WebSocket trực tiếp, auth bằng subprotocol ['bearer', <access token>].
   * MediaRecorder (webm/opus, 250ms/chunk) đẩy liên tục — kể cả lúc AI nói (giữ stream liền mạch),
   * transcript trong lúc AI nói bị bỏ ở phía nhận.
   */
  const initDeepgram = useCallback(
    (cfg: { token: string; model: string }, micStream: MediaStream, language: string) => {
      console.info(
        '[deepgram] INIT — lang:',
        language,
        '| audio tracks:',
        micStream.getAudioTracks().length
      )
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
          // MediaRecorder mimeType audio/* KHÔNG chấp nhận stream có video track
          // (DeviceCheck trả stream cam+mic) → Chrome ném NotSupportedError ngay tại
          // start(). Bắt buộc tách stream audio-only từ audio track trước khi ghi.
          const audioTracks = micStream.getAudioTracks().filter((t) => t.readyState === 'live')
          if (audioTracks.length === 0) throw new Error('Mic stream không còn audio track sống')
          const audioOnly = new MediaStream(audioTracks)

          const mime = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
            ? 'audio/webm;codecs=opus'
            : 'audio/webm'
          const rec = new MediaRecorder(audioOnly, { mimeType: mime })
          recorderRef.current = rec
          rec.ondataavailable = (ev: BlobEvent) => {
            if (ev.data.size > 0 && ws.readyState === WebSocket.OPEN) ws.send(ev.data)
          }
          rec.start(250)
          setListening(true)
          setSttEnabled(true)
          console.info('[deepgram] MediaRecorder RECORDING —', mime)
        } catch (e) {
          // Lỗi khởi tạo recorder là lỗi cục bộ (stream/codec) — reconnect Deepgram cũng
          // sẽ thất bại y hệt → đóng socket & dừng hẳn, tránh vòng lặp OPEN/CLOSE 1011.
          console.error('[deepgram] không khởi tạo được MediaRecorder — dừng STT', e)
          setSttEnabled(false)
          dgRetryRef.current = MAX_STT_RECONNECT
          try {
            ws.close()
          } catch {
            /* noop */
          }
        }
      }

      ws.onmessage = (ev: MessageEvent) => {
        interface DeepgramMessage {
          type?: string
          is_final?: boolean
          channel?: { alternatives?: { transcript?: string }[] }
        }
        let data: DeepgramMessage
        try {
          data = JSON.parse(ev.data as string) as DeepgramMessage
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
            syncDraft()
          } else {
            updateInterim(text)
          }
        }
        // UtteranceEnd: KHÔNG auto-submit — ứng viên chủ động bấm "Gửi trả lời" (draft giữ nguyên).
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
    [syncDraft, updateInterim]
  )

  const connectHub = useCallback(async () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${ASSET_BASE_URL}/hubs/session`, {
        accessTokenFactory: () => useAuthStore.getState().tokens?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .build()
    connRef.current = conn

    conn.on('ReceiveQuestion', (p: QuestionPayload) => {
      currentQuestionRef.current = { id: p.questionId, askedAt: Date.now() }
      finalBufRef.current = ''
      updateInterim('')
      setDraft('')
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

    conn.on('ReceiveQuestionAudio', (p: QuestionAudioPayload) => {
      if (!p?.audio || !p?.questionId) return
      if (currentQuestionRef.current && currentQuestionRef.current.id !== p.questionId) return
      if (questionAudioTimerRef.current) window.clearTimeout(questionAudioTimerRef.current)
      playQuestionAudio(p.questionId, p.audio)
    })

    // AI kết thúc buổi phỏng vấn: lời cảm ơn (text) → audio → server báo completed.
    conn.on('ReceiveClosing', (p: { text?: string }) => {
      if (!p?.text) return
      closingRef.current = true
      currentQuestionRef.current = null // không còn câu hỏi chờ trả lời
      finalBufRef.current = ''
      updateInterim('')
      setDraft('')
      setMessages((m) => [...m, { role: 'ai', text: p.text! }])
      // Audio cảm ơn không tới trong 6s → browser TTS để vẫn có lời chào.
      closingAudioTimerRef.current = window.setTimeout(() => {
        if (!endedRef.current && !aiSpeakingRef.current) speakBrowserTts(p.text!)
      }, QUESTION_AUDIO_TIMEOUT_MS)
    })

    conn.on('ReceiveClosingAudio', (p: { audio?: string }) => {
      if (!p?.audio) return
      if (closingAudioTimerRef.current) window.clearTimeout(closingAudioTimerRef.current)
      playQuestionAudio('__closing__', p.audio)
    })

    conn.on('ReceiveSessionStatus', (p: SessionStatusPayload) => {
      const st = typeof p === 'string' ? p : p?.status
      if (st !== 'completed') return
      // Đang phát lời cảm ơn → chờ nói xong (handleSpeakEnded) mới chuyển màn; kèm chốt chặn 20s.
      if (closingRef.current && aiSpeakingRef.current) {
        pendingEndRef.current = true
        window.setTimeout(() => {
          if (pendingEndRef.current) {
            pendingEndRef.current = false
            endedRef.current = true
            setStatus('ended')
          }
        }, 20_000)
        return
      }
      setStatus('ended')
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
        console.info(
          '[media-config] deepgram:',
          !!media.deepgram?.token,
          '| heyGen:',
          !!media.heyGen?.token,
          '| lang:',
          media.language
        )

        // STT không phụ thuộc avatar/hub → mở ngay; avatar + hub khởi tạo song song
        // (trước đây tuần tự: avatar chậm làm token Deepgram hết hạn trước khi STT kịp nối).
        if (media.deepgram?.token) {
          try {
            initDeepgram(media.deepgram, micStream, languageRef.current)
          } catch (e) {
            console.error('[deepgram] init lỗi — tắt STT', e)
            setSttEnabled(false)
          }
        } else {
          console.warn('[deepgram] media-config không có token — STT tắt (nhập tay)')
          setSttEnabled(false)
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
    if (speakWatchdogRef.current) window.clearTimeout(speakWatchdogRef.current)
    if (closingAudioTimerRef.current) window.clearTimeout(closingAudioTimerRef.current)
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
    draft,
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
