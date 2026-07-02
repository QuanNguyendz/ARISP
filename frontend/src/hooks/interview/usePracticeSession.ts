import { useCallback, useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { createClient, LiveTranscriptionEvents } from '@deepgram/sdk'
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

/**
 * Điều phối toàn bộ luồng phỏng vấn thử:
 * startSession(practice) → media-config (token Deepgram/HeyGen) → HeyGen avatar
 * → SignalR (JoinSession/StartInterview) → nhận ReceiveQuestion → avatar.speak
 * → Deepgram live STT bắt câu trả lời → SubmitAnswerText → lặp → EndSession.
 *
 * Mọi nhánh media đều fallback mềm: thiếu HeyGen → browser TTS + bot tĩnh;
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
  const dgConnRef = useRef<any>(null)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const sessionIdRef = useRef<string | null>(null)
  const currentQuestionRef = useRef<{ id: string; askedAt: number } | null>(null)
  const finalBufRef = useRef<string>('')

  const speak = useCallback(async (text: string) => {
    const session = avatarRef.current
    const sessionId = sessionIdRef.current
    // Avatar LiveAvatar (LITE): TTS ElevenLabs (BE) → repeatAudio (avatar lip-sync). aiSpeaking
    // do sự kiện AVATAR_SPEAK_STARTED/ENDED điều khiển.
    if (session && sessionId) {
      try {
        const audio = await interviewService.getTtsAudio(sessionId, text)
        if (audio) {
          session.repeatAudio(audio)
          return
        }
      } catch {
        /* rơi xuống fallback browser TTS */
      }
    }
    // Fallback: chưa có avatar/ElevenLabs → đọc bằng giọng trình duyệt.
    try {
      const u = new SpeechSynthesisUtterance(text)
      setAiSpeaking(true)
      u.onend = () => setAiSpeaking(false)
      window.speechSynthesis.speak(u)
    } catch {
      /* không hỗ trợ TTS — bỏ qua, vẫn hiển thị text */
    }
  }, [])

  const submitAnswer = useCallback(() => {
    const q = currentQuestionRef.current
    const sessionId = sessionIdRef.current
    const answer = (finalBufRef.current + ' ' + interim).trim()
    if (!q || !sessionId || !answer) return
    finalBufRef.current = ''
    setInterim('')
    setMessages((m) => [...m, { role: 'candidate', text: answer }])
    const responseMs = Date.now() - q.askedAt
    connRef.current?.invoke('SubmitAnswerText', sessionId, q.id, answer, responseMs).catch(() => {})
    currentQuestionRef.current = null
  }, [interim])

  const initAvatar = useCallback(
    async (
      cfg: NonNullable<Awaited<ReturnType<typeof interviewService.getMediaConfig>>['heyGen']>
    ) => {
      if (!cfg?.token) return // chưa cấu hình avatar → fallback browser TTS
      // LiveAvatar LITE: BE đã mint session token; ta KHÔNG dùng voiceChat của SDK (STT tự làm bằng Deepgram).
      const session = new LiveAvatarSession(cfg.token, {
        voiceChat: false,
        apiUrl: cfg.serverUrl,
      } as any)
      avatarRef.current = session
      session.on(SessionEvent.SESSION_STREAM_READY, () => {
        if (videoRef.current) {
          try {
            session.attach(videoRef.current) // gắn cả video + audio track vào <video>
          } catch {
            /* ignore */
          }
          setAvatarReady(true)
        }
      })
      session.on(AgentEventsEnum.AVATAR_SPEAK_STARTED, () => setAiSpeaking(true))
      session.on(AgentEventsEnum.AVATAR_SPEAK_ENDED, () => setAiSpeaking(false))
      await session.start()
    },
    []
  )

  const initDeepgram = useCallback(
    (cfg: { token: string; model: string }, micStream: MediaStream, language: string) => {
      const dg = createClient({ accessToken: cfg.token } as any)
      const live = dg.listen.live({
        model: cfg.model || 'nova-2',
        language: language || 'vi',
        interim_results: true,
        smart_format: true,
        vad_events: true,
        endpointing: 300,
        utterance_end_ms: 1000,
      } as any)
      dgConnRef.current = live
      let dgOpen = false

      live.on(LiveTranscriptionEvents.Open, () => {
        dgOpen = true
        console.info('[deepgram] OPEN — model:', cfg.model, '| lang:', language)
        try {
          const mime = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
            ? 'audio/webm;codecs=opus'
            : 'audio/webm'
          const rec = new MediaRecorder(micStream, { mimeType: mime })
          recorderRef.current = rec
          let sent = 0
          rec.ondataavailable = (ev: BlobEvent) => {
            if (ev.data.size > 0 && dgOpen) {
              try {
                ;(live as any).send(ev.data)
                if (sent++ === 0) console.info('[deepgram] gửi audio chunk đầu tiên, mime:', mime)
              } catch (e) {
                console.warn('[deepgram] send lỗi', e)
              }
            }
          }
          rec.start(250)
          setListening(true)
          setSttEnabled(true)
          console.info('[deepgram] MediaRecorder started')
        } catch (e) {
          console.warn('[deepgram] không khởi tạo được MediaRecorder', e)
          setSttEnabled(false)
        }
      })

      live.on(LiveTranscriptionEvents.Transcript, (data: any) => {
        const text: string = data?.channel?.alternatives?.[0]?.transcript ?? ''
        if (text)
          console.debug(
            '[deepgram] transcript',
            data?.is_final ? '(final)' : '(interim)',
            ':',
            text
          )
        if (!text) return
        if (data.is_final) {
          finalBufRef.current = `${finalBufRef.current} ${text}`.trim()
          setInterim('')
        } else {
          setInterim(text)
        }
      })

      live.on(LiveTranscriptionEvents.UtteranceEnd, () => {
        console.debug('[deepgram] UtteranceEnd → submit. buffer:', finalBufRef.current)
        submitAnswer()
      })
      live.on(LiveTranscriptionEvents.Error, (e: any) => console.error('[deepgram] ERROR', e))
      live.on(LiveTranscriptionEvents.Close, (e: any) => {
        dgOpen = false
        console.warn('[deepgram] CLOSE', e?.code ?? '', e?.reason ?? '')
      })
      live.on(LiveTranscriptionEvents.Metadata, (m: any) => console.debug('[deepgram] metadata', m))
    },
    [submitAnswer]
  )

  const connectHub = useCallback(async () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${ASSET_BASE_URL}/hubs/session`, {
        accessTokenFactory: () => useAuthStore.getState().tokens?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .build()
    connRef.current = conn

    conn.on('ReceiveQuestion', async (p: any) => {
      currentQuestionRef.current = { id: p.questionId, askedAt: Date.now() }
      finalBufRef.current = ''
      setInterim('')
      setMessages((m) => [...m, { role: 'ai', text: p.questionText, seq: p.sequenceNumber }])
      await speak(p.questionText)
    })
    conn.on('ReceiveSessionStatus', (p: any) => {
      const st = typeof p === 'string' ? p : p?.status
      if (st === 'completed') setStatus('ended')
    })
    // ReceiveAnswerAnalysis / ReceiveCheatAlert: chưa hiển thị ở practice

    await conn.start()
  }, [speak])

  const start = useCallback(
    async (micStream: MediaStream) => {
      try {
        setStatus('starting')
        setError(null)

        const session = await interviewService.startSession({
          applicationId,
          roundNumber,
          sessionType: 'practice',
        })
        const sessionId = session.sessionId
        sessionIdRef.current = sessionId

        const media = await interviewService.getMediaConfig(sessionId)

        if (media.heyGen?.token) {
          try {
            await initAvatar(media.heyGen)
          } catch {
            /* avatar lỗi → fallback browser TTS */
          }
        }

        await connectHub()

        if (media.deepgram?.token) {
          try {
            initDeepgram(media.deepgram, micStream, media.language)
          } catch {
            setSttEnabled(false)
          }
        }

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
    try {
      recorderRef.current?.stop()
    } catch {
      /* noop */
    }
    try {
      dgConnRef.current?.requestClose?.()
    } catch {
      /* noop */
    }
    try {
      avatarRef.current?.stop?.()
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
