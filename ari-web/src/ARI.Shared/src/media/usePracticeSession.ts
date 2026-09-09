import { useCallback, useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { interviewService } from '@ari/shared/fservices/interview'
import { useAuthStore } from '@ari/shared/store/auth'
import { getInterviewSessionToken } from '@ari/shared/api/apiClient'
import { ASSET_BASE_URL } from '@ari/shared/config/constants'
import { getStoredLanguage } from '@ari/shared/i18n'

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
/** Quá hạn chờ ReceiveQuestionAudio (BE TTS đẩy qua SignalR) → tự fetch /tts rồi browser TTS. */
const QUESTION_AUDIO_TIMEOUT_MS = 6000

/**
 * Điều phối toàn bộ luồng phỏng vấn thử:
 * startSession → media-config (token Deepgram) → khởi tạo song song SignalR + Deepgram STT
 * → StartInterview → nhận ReceiveQuestion (text) + ReceiveQuestionAudio (PCM 24k từ ElevenLabs,
 * BE đẩy sẵn — không round-trip /tts) → phát qua WebAudio → Deepgram bắt câu trả lời
 * → SubmitAnswerText → lặp.
 *
 * STT dùng WebSocket Deepgram trực tiếp với subprotocol ['bearer', token] — SDK v3 KHÔNG
 * hỗ trợ access token ngắn hạn (chỉ nhận API key) nên trước đây STT chết ngay khi khởi tạo.
 * Chống echo: bỏ mọi transcript khi AI đang nói (aiSpeakingRef) + xóa buffer khi AI nói xong.
 * Watchdog speaking: sự kiện "nói xong" không phải lúc nào cũng bắn (browser TTS nuốt `onend`)
 * → cờ aiSpeaking kẹt true và nuốt toàn bộ transcript ứng viên; đặt timer theo độ dài audio để tự nhả.
 *
 * Gửi trả lời THỦ CÔNG (ADR-050 nhập kép): transcript Deepgram append vào answerText hiển thị
 * trong <textarea> ứng viên SỬA/GÕ TAY được (mic tắt = gõ tự do); bấm "Gửi trả lời" mới submit —
 * không auto-submit khi im lặng (UtteranceEnd chỉ flush interim).
 *
 * Trần thời lượng (ADR-050): media-config trả maxDurationSeconds → đếm ngược; hết giờ khoá mic +
 * NotifyTimeout → server cho AI nói câu kết rồi đóng phiên.
 *
 * AUDIO-ONLY cho CẢ buổi thử lẫn buổi thật (ADR-050 mở rộng ở ADR-067 — avatar đã gỡ khỏi dự án):
 * audio ElevenLabs phát qua WebAudio (playPcmViaWebAudio). Thiếu Deepgram → nhập tay + "Gửi trả lời".
 */
export interface InterviewSessionOptions {
  /** Bắt buộc khi tự tạo phiên (phỏng vấn thử). Kiosk truyền `existingSessionId` thay cho cặp này. */
  applicationId?: string
  roundNumber?: number
  sessionType?: 'practice' | 'real'
  /** Kiosk: phiên đã được tạo sẵn khi xác thực Interview Code — hook chỉ việc tham gia (ADR-052). */
  existingSessionId?: string | null
  /** Kiosk: quay video buổi thật (cam + mic) để tải lên storage sau khi kết thúc. */
  recordVideo?: boolean
}

/**
 * Phỏng vấn THỬ (audio-only, tự tạo phiên) — giữ nguyên chữ ký cũ cho các màn hiện có.
 * Bản đầy đủ (dùng cho cả Kiosk phỏng vấn thật) là {@link useInterviewSession}.
 */
export function usePracticeSession(applicationId: string, roundNumber = 1) {
  return useInterviewSession({ applicationId, roundNumber, sessionType: 'practice' })
}

export function useInterviewSession(options: InterviewSessionOptions) {
  const {
    applicationId = '',
    roundNumber = 1,
    sessionType = 'practice',
    existingSessionId = null,
    recordVideo = false,
  } = options
  const [status, setStatus] = useState<PracticeStatus>('idle')
  const [error, setError] = useState<string | null>(null)
  const [messages, setMessages] = useState<TranscriptItem[]>([])
  const [interim, setInterim] = useState('')
  // Nhập kép (ADR-050): MỘT nguồn duy nhất cho câu trả lời — Deepgram final append vào đây,
  // ứng viên sửa/gõ tay cũng ghi vào đây; nút "Gửi trả lời" đọc từ đây.
  const [answerText, setAnswerState] = useState('')
  const [aiSpeaking, setAiSpeaking] = useState(false)
  const [listening, setListening] = useState(false)
  const [sttEnabled, setSttEnabled] = useState(false)
  const [micEnabled, setMicEnabled] = useState(true) // tắt mic = chuyển sang gõ phím tự do (ADR-050)
  const [remainingSeconds, setRemainingSeconds] = useState<number | null>(null) // đếm ngược trần thời lượng
  const [timeUp, setTimeUp] = useState(false) // hết giờ — đang chờ AI nói câu kết thúc
  /**
   * Phòng chờ buổi THẬT (ADR-067): `waiting` = chưa ai vào phòng · `hm_joined` = Hiring Manager đã
   * có mặt · `admitted` = đã được cho vào, AI đang hỏi. Buổi thử và tin chưa gán HM luôn `admitted`.
   */
  const [admission, setAdmission] = useState<'waiting' | 'hm_joined' | 'admitted'>('admitted')
  // Id phiên đã tạo — page dùng để mở trang xem lại transcript sau khi kết thúc (ADR-051).
  const [sessionId, setSessionId] = useState<string | null>(null)

  const connRef = useRef<signalR.HubConnection | null>(null)
  const dgSocketRef = useRef<WebSocket | null>(null)
  const dgRetryRef = useRef(0)
  const recorderRef = useRef<MediaRecorder | null>(null)
  // Ghi hình buổi THẬT (ADR-052) — recorder riêng trên stream cam+mic, tách khỏi recorder
  // audio-only đang đẩy cho Deepgram.
  const videoRecorderRef = useRef<MediaRecorder | null>(null)
  const videoChunksRef = useRef<Blob[]>([])
  const micStreamRef = useRef<MediaStream | null>(null)
  const languageRef = useRef('vi')
  const sessionIdRef = useRef<string | null>(null)
  const currentQuestionRef = useRef<{ id: string; askedAt: number } | null>(null)
  const answerTextRef = useRef('') // mirror của answerText để đọc trong callback WS/SignalR (tránh stale closure)
  const interimRef = useRef('')
  const aiSpeakingRef = useRef(false)
  const endedRef = useRef(false)
  const audioCtxRef = useRef<AudioContext | null>(null)
  const audioSrcRef = useRef<AudioBufferSourceNode | null>(null)
  const questionAudioTimerRef = useRef<number | null>(null)
  const audioPlayedForRef = useRef<string | null>(null)
  const speakWatchdogRef = useRef<number | null>(null)
  const closingRef = useRef(false) // đã nhận lời cảm ơn kết thúc từ AI (ReceiveClosing)
  const pendingEndRef = useRef(false) // server báo completed nhưng chờ AI nói xong lời cảm ơn
  const closingAudioTimerRef = useRef<number | null>(null)
  const micEnabledRef = useRef(true)
  const clockTimerRef = useRef<number | null>(null) // interval đếm ngược trần thời lượng
  const startClockRef = useRef(0) // mốc bắt đầu (ms local — dùng giờ máy để tránh lệch clock server)
  const capSecondsRef = useRef(0) // trần thời lượng (giây); 0 = không giới hạn
  const timeUpRef = useRef(false)

  const updateInterim = useCallback((v: string) => {
    interimRef.current = v
    setInterim(v)
  }, [])

  /** Cập nhật câu trả lời (ref + state) — nguồn chung cho Deepgram append lẫn sửa/gõ tay. */
  const setAnswer = useCallback((v: string) => {
    answerTextRef.current = v
    setAnswerState(v)
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
    updateInterim('')
    setAnswer('')
    if (currentQuestionRef.current) currentQuestionRef.current.askedAt = Date.now()
    // Lời cảm ơn kết thúc vừa phát xong + server đã báo completed → giờ mới chuyển màn kết thúc.
    if (pendingEndRef.current) {
      pendingEndRef.current = false
      endedRef.current = true
      setStatus('ended')
    }
  }, [setSpeaking, updateInterim, setAnswer])

  /**
   * Chốt chặn cờ aiSpeaking: nếu sự kiện "nói xong" (`onended` của WebAudio / browser TTS)
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
    // Ưu tiên answerText (đã gồm phần sửa tay); cộng interim đang treo để không rớt câu cuối.
    const answer = `${answerTextRef.current} ${interimRef.current}`.trim()
    if (!q || !sessionId || !answer) return
    updateInterim('')
    setAnswer('')
    setMessages((m) => [...m, { role: 'candidate', text: answer }])
    const responseMs = Date.now() - q.askedAt
    connRef.current?.invoke('SubmitAnswerText', sessionId, q.id, answer, responseMs).catch(() => {})
    currentQuestionRef.current = null
  }, [updateInterim, setAnswer])

  /** Nhập kép: bật/tắt mic. Tắt = ngừng gửi audio + ngừng append transcript → gõ phím tự do (ADR-050). */
  const toggleMic = useCallback(() => {
    const next = !micEnabledRef.current
    micEnabledRef.current = next
    setMicEnabled(next)
    // Tắt track để đỡ băng thông + tránh echo; bật lại khi mở mic.
    micStreamRef.current?.getAudioTracks().forEach((t) => (t.enabled = next))
  }, [])

  /** Dừng HẲN thu mic (stop recorder + đóng WS Deepgram) — dùng khi hết giờ. Không teardown cả phiên. */
  const stopMic = useCallback(() => {
    // Chặn ws.onclose tự nối lại STT (nó bỏ cuộc khi dgRetry đã chạm trần).
    dgRetryRef.current = MAX_STT_RECONNECT
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
    setListening(false)
    setSttEnabled(false)
  }, [])

  /** Phát PCM 16-bit mono 24kHz (base64 từ ElevenLabs) qua WebAudio. */
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

  /** Phát audio câu hỏi qua WebAudio. */
  const playQuestionAudio = useCallback(
    (questionId: string, base64: string) => {
      if (audioPlayedForRef.current === questionId) return // đã phát (chống phát đôi khi audio đến trễ)
      audioPlayedForRef.current = questionId
      void playPcmViaWebAudio(base64)
    },
    [playPcmViaWebAudio]
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
            // micEnabledRef=false (ứng viên chuyển sang gõ phím) → ngừng gửi audio tới Deepgram.
            if (ev.data.size > 0 && ws.readyState === WebSocket.OPEN && micEnabledRef.current)
              ws.send(ev.data)
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
          if (aiSpeakingRef.current) return // echo giọng AI lọt vào mic → bỏ
          if (!micEnabledRef.current) return // đang gõ phím (mic tắt) → không append transcript
          if (data.is_final) {
            // Append vào cuối answerText (giữ nguyên phần ứng viên đã sửa tay trước đó).
            setAnswer(`${answerTextRef.current} ${text}`.trim())
            updateInterim('')
          } else {
            updateInterim(text)
          }
        }
        // UtteranceEnd: KHÔNG auto-submit — ứng viên chủ động bấm "Gửi trả lời".
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
    [updateInterim, setAnswer]
  )

  const connectHub = useCallback(async () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${ASSET_BASE_URL}/hubs/session`, {
        // Kiosk (ADR-052) dùng token phạm vi phiên; ứng viên đăng nhập dùng token tài khoản.
        accessTokenFactory: () =>
          getInterviewSessionToken() ?? useAuthStore.getState().tokens?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .build()
    connRef.current = conn

    conn.on('ReceiveQuestion', (p: QuestionPayload) => {
      currentQuestionRef.current = { id: p.questionId, askedAt: Date.now() }
      updateInterim('')
      setAnswer('')
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
      updateInterim('')
      setAnswer('')
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

      // PHÒNG CHỜ buổi thật (ADR-067). Hai mốc, hai nghĩa khác nhau với người đang ngồi ở Kiosk:
      // "hm_joined" = người phỏng vấn đã có mặt (sắp tới lượt), "admitted" = mời vào, AI bắt đầu hỏi.
      if (st === 'hm_joined') {
        setAdmission('hm_joined')
        return
      }
      if (st === 'admitted') {
        setAdmission('admitted')
        // Server vừa mở cổng — giờ lệnh này mới sinh được câu hỏi. Trước khi được cho vào thì nó bị
        // từ chối im lặng (phiên chưa `active`), nên phải gọi LẠI ở đúng thời điểm này.
        void connRef.current?.invoke('StartInterview', sessionIdRef.current).catch(() => {})
        return
      }

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
  }, [playQuestionAudio, speakBrowserTts, updateInterim, setAnswer])

  /**
   * Hết giờ (đếm ngược chạm 0, ADR-050): khoá mic + báo server. Server (PracticeTimeoutCloseAsync)
   * cho AI nói 1 câu kết thúc rồi đóng phiên — closing chảy về qua ReceiveClosing/SessionStatus.
   */
  const onTimeUp = useCallback(() => {
    if (timeUpRef.current) return
    timeUpRef.current = true
    setTimeUp(true)
    if (clockTimerRef.current) {
      window.clearInterval(clockTimerRef.current)
      clockTimerRef.current = null
    }
    setRemainingSeconds(0)
    stopMic()
    const sessionId = sessionIdRef.current
    if (sessionId) connRef.current?.invoke('NotifyTimeout', sessionId).catch(() => {})
  }, [stopMic])

  const start = useCallback(
    async (micStream: MediaStream) => {
      try {
        setStatus('starting')
        setError(null)
        endedRef.current = false
        micStreamRef.current = micStream

        // Kiosk: phiên đã tạo lúc xác thực Interview Code → chỉ tham gia, KHÔNG tạo phiên mới
        // (tránh đốt thêm 1 phiên thật). Phỏng vấn thử vẫn tự tạo phiên như cũ.
        const sessionId =
          existingSessionId ??
          (
            await interviewService.startSession({
              applicationId,
              roundNumber,
              sessionType,
              // Nhận xét AI viết theo ngôn ngữ ứng viên đang dùng → màn xem lại không trộn Việt–Anh.
              uiLanguage: getStoredLanguage(),
            })
          ).sessionId
        sessionIdRef.current = sessionId
        setSessionId(sessionId)

        // Quay video buổi thật (ADR-052) — best-effort, lỗi codec không được chặn phỏng vấn.
        if (recordVideo) {
          try {
            const mime = MediaRecorder.isTypeSupported('video/webm;codecs=vp9,opus')
              ? 'video/webm;codecs=vp9,opus'
              : MediaRecorder.isTypeSupported('video/webm;codecs=vp8,opus')
                ? 'video/webm;codecs=vp8,opus'
                : 'video/webm'
            videoChunksRef.current = []
            const vrec = new MediaRecorder(micStream, { mimeType: mime, videoBitsPerSecond: 900_000 })
            vrec.ondataavailable = (ev: BlobEvent) => {
              if (ev.data.size > 0) videoChunksRef.current.push(ev.data)
            }
            vrec.start(5000) // cắt chunk 5s để không giữ 1 buffer khổng lồ trong RAM
            videoRecorderRef.current = vrec
          } catch (e) {
            console.error('[recording] không quay được video buổi phỏng vấn', e)
          }
        }

        const media = await interviewService.getMediaConfig(sessionId)
        languageRef.current = media.language || 'vi'
        console.info('[media-config] deepgram:', !!media.deepgram?.token, '| lang:', media.language)

        // Phòng chờ (ADR-067): phiên chưa được cho vào thì màn hình phải nói rõ đang chờ ai, thay vì
        // hiện phòng phỏng vấn im lặng không có câu hỏi nào.
        setAdmission(
          media.status === 'waiting'
            ? media.hiringManagerPresent
              ? 'hm_joined'
              : 'waiting'
            : 'admitted'
        )

        // STT không phụ thuộc hub → mở ngay, hub nối song song.
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

        await connectHub()

        setStatus('live')

        // Đồng hồ đếm ngược trần thời lượng (ADR-050). Đếm theo giờ MÁY (tránh lệch clock server);
        // server enforce trần độc lập nên lệch nhỏ không ảnh hưởng tính đúng đắn.
        const cap = media.maxDurationSeconds ?? 0
        if (cap > 0) {
          capSecondsRef.current = cap
          startClockRef.current = Date.now()
          timeUpRef.current = false
          setTimeUp(false)
          setRemainingSeconds(cap)
          if (clockTimerRef.current) window.clearInterval(clockTimerRef.current)
          clockTimerRef.current = window.setInterval(() => {
            const left = Math.max(0, Math.ceil(cap - (Date.now() - startClockRef.current) / 1000))
            setRemainingSeconds(left)
            if (left <= 0) onTimeUp()
          }, 1000)
        }

        await connRef.current?.invoke('JoinSession', sessionId)
        await connRef.current?.invoke('StartInterview', sessionId)
      } catch (e: any) {
        setError(e?.response?.data?.message ?? e?.message ?? 'Không thể bắt đầu phỏng vấn thử.')
        setStatus('error')
      }
    },
    [applicationId, roundNumber, connectHub, initDeepgram, onTimeUp]
  )

  const cleanup = useCallback(() => {
    endedRef.current = true
    if (questionAudioTimerRef.current) window.clearTimeout(questionAudioTimerRef.current)
    if (clockTimerRef.current) window.clearInterval(clockTimerRef.current)
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
      connRef.current?.stop()
    } catch {
      /* noop */
    }
    try {
      window.speechSynthesis?.cancel()
    } catch {
      /* noop */
    }
    // Recorder video dừng ở đây để flush chunk cuối; chunks GIỮ LẠI cho finalizeRecording() upload.
    try {
      if (videoRecorderRef.current?.state === 'recording') videoRecorderRef.current.stop()
    } catch {
      /* noop */
    }
  }, [])

  /**
   * Chốt file ghi hình rồi tải lên storage (ADR-052). Gọi sau khi phiên đã kết thúc.
   * Trả về trạng thái để màn Kiosk hiển thị: không quay → 'skipped'.
   */
  const finalizeRecording = useCallback(async (): Promise<'saved' | 'skipped' | 'error'> => {
    const rec = videoRecorderRef.current
    const sessionId = sessionIdRef.current
    if (!rec || !sessionId) return 'skipped'
    videoRecorderRef.current = null

    // Chờ recorder flush nốt chunk cuối trước khi ghép Blob.
    await new Promise<void>((resolve) => {
      if (rec.state === 'inactive') return resolve()
      rec.onstop = () => resolve()
      try {
        rec.stop()
      } catch {
        resolve()
      }
      window.setTimeout(resolve, 5000) // recorder treo → không chặn màn kết thúc
    })

    const chunks = videoChunksRef.current
    videoChunksRef.current = []
    if (chunks.length === 0) return 'skipped'

    try {
      const blob = new Blob(chunks, { type: rec.mimeType || 'video/webm' })
      await interviewService.uploadRecording(sessionId, blob, `interview-${sessionId}.webm`)
      return 'saved'
    } catch (e) {
      console.error('[recording] tải video lên thất bại', e)
      return 'error'
    }
  }, [])

  const end = useCallback(async () => {
    const sessionId = sessionIdRef.current
    // Dừng recorder video TRƯỚC cleanup (cleanup đóng stream/track) nhưng giữ chunks để upload.
    try {
      if (videoRecorderRef.current?.state === 'recording') videoRecorderRef.current.requestData()
    } catch {
      /* noop */
    }
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
    sessionId, // dùng cho lối vào trang xem lại transcript sau khi kết thúc
    messages,
    interim,
    answerText,
    setAnswerText: setAnswer, // page bind <textarea> onChange để sửa/gõ tay
    aiSpeaking,
    listening,
    sttEnabled,
    micEnabled,
    toggleMic,
    remainingSeconds, // number | null (null = không giới hạn)
    timeUp,
    admission, // 'waiting' | 'hm_joined' | 'admitted' — phòng chờ buổi thật (ADR-067)
    start,
    end,
    submitAnswer,
    finalizeRecording, // Kiosk: chốt + tải video buổi thật lên storage sau khi kết thúc
  }
}
