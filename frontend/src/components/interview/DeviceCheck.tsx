import { useCallback, useEffect, useRef, useState } from 'react'
import {
  Camera,
  Mic,
  Loader2,
  AlertTriangle,
  RefreshCw,
  Check,
  ShieldCheck,
  EyeOff,
} from 'lucide-react'

interface DeviceCheckProps {
  title?: string
  subtitle?: string
  startLabel?: string
  /** Nhận MediaStream đang chạy (đã được kiểm tra) — caller sở hữu & chịu trách nhiệm stop. */
  onReady: (stream: MediaStream) => void
  onCancel?: () => void
}

type Phase = 'idle' | 'requesting' | 'ready' | 'error'
type ErrKind = 'denied' | 'notfound' | 'lost' | 'other'

// Ngưỡng độ sáng trung bình (0..255) coi là camera bị che / phòng quá tối.
const DARK_LUMA_THRESHOLD = 14
// Số lần lấy mẫu tối liên tiếp trước khi kết luận (chống nhiễu/nháy).
const DARK_SAMPLES_TO_BLOCK = 2

/**
 * Cổng kiểm tra thiết bị trước phỏng vấn: ứng viên CHỈ được vào phỏng vấn (thử & thật)
 * khi camera và microphone thực sự hoạt động. Theo dõi LIÊN TỤC:
 *  - track bị dừng/thu hồi quyền/rút thiết bị (event `ended`/`mute`) → khóa lại ngay.
 *  - camera bị che hoặc phòng quá tối (phân tích độ sáng khung hình) → khóa lại + cảnh báo.
 * Khi bắt đầu, bàn giao luôn stream đang chạy cho caller (tránh prompt quyền lần hai).
 */
export default function DeviceCheck({
  title = 'Kiểm tra thiết bị',
  subtitle = 'Cần bật camera và micro để vào phỏng vấn. Quyền truy cập chỉ dùng trong phiên này.',
  startLabel = 'Bắt đầu',
  onReady,
  onCancel,
}: DeviceCheckProps) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const audioCtxRef = useRef<AudioContext | null>(null)
  const rafRef = useRef<number | null>(null)
  const darkTimerRef = useRef<number | null>(null)
  const darkStreakRef = useRef(0)
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const handedOff = useRef(false)

  const [phase, setPhase] = useState<Phase>('idle')
  const [errKind, setErrKind] = useState<ErrKind | null>(null)
  const [camOk, setCamOk] = useState(false)
  const [micOk, setMicOk] = useState(false)
  const [camDark, setCamDark] = useState(false)
  const [level, setLevel] = useState(0) // 0..1

  const stopMonitors = useCallback(() => {
    if (rafRef.current) cancelAnimationFrame(rafRef.current)
    rafRef.current = null
    if (darkTimerRef.current) clearInterval(darkTimerRef.current)
    darkTimerRef.current = null
    audioCtxRef.current?.close().catch(() => {})
    audioCtxRef.current = null
  }, [])

  const stopAll = useCallback(() => {
    stopMonitors()
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
  }, [stopMonitors])

  // Mất track (thu hồi quyền / rút thiết bị / OS mute) → khóa lại ngay.
  const handleTrackLost = useCallback(() => {
    if (handedOff.current) return
    stopMonitors()
    setCamOk(false)
    setMicOk(false)
    setCamDark(false)
    setErrKind('lost')
    setPhase('error')
  }, [stopMonitors])

  const start = useCallback(async () => {
    setPhase('requesting')
    setErrKind(null)
    setCamDark(false)
    darkStreakRef.current = 0
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true })
      streamRef.current = stream
      const vt = stream.getVideoTracks()[0]
      const at = stream.getAudioTracks()[0]
      setCamOk(!!vt && vt.readyState === 'live')
      setMicOk(!!at && at.readyState === 'live')

      // Theo dõi liên tục: nếu một track bị dừng/mute (thu hồi quyền, rút thiết bị) → khóa lại.
      ;[vt, at].forEach((t) => {
        if (!t) return
        t.addEventListener('ended', handleTrackLost)
        t.addEventListener('mute', handleTrackLost)
      })

      if (videoRef.current) {
        videoRef.current.srcObject = stream
        videoRef.current.play().catch(() => {})
      }

      // Đo mức âm micro (Web Audio) để ứng viên thấy mic thực sự nhận tiếng.
      const AC =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext
      const ctx = new AC()
      audioCtxRef.current = ctx
      await ctx.resume().catch(() => {})
      const srcNode = ctx.createMediaStreamSource(stream)
      const analyser = ctx.createAnalyser()
      analyser.fftSize = 512
      srcNode.connect(analyser)
      const buf = new Uint8Array(analyser.fftSize)
      const tick = () => {
        analyser.getByteTimeDomainData(buf)
        let sum = 0
        for (let i = 0; i < buf.length; i++) {
          const v = (buf[i] - 128) / 128
          sum += v * v
        }
        const rms = Math.sqrt(sum / buf.length)
        setLevel(Math.min(1, rms * 3))
        rafRef.current = requestAnimationFrame(tick)
      }
      tick()

      // Giám sát độ sáng khung hình → phát hiện camera bị che / quá tối.
      const canvas = canvasRef.current ?? document.createElement('canvas')
      canvas.width = 32
      canvas.height = 24
      canvasRef.current = canvas
      const cctx = canvas.getContext('2d', { willReadFrequently: true })
      darkTimerRef.current = window.setInterval(() => {
        const video = videoRef.current
        if (!cctx || !video || video.readyState < 2 || !video.videoWidth) return
        cctx.drawImage(video, 0, 0, canvas.width, canvas.height)
        let lumaSum = 0
        try {
          const { data } = cctx.getImageData(0, 0, canvas.width, canvas.height)
          for (let i = 0; i < data.length; i += 4) {
            lumaSum += 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2]
          }
        } catch {
          return // tránh lỗi tainted canvas (không xảy ra với camera nội bộ)
        }
        const avg = lumaSum / (canvas.width * canvas.height)
        if (avg < DARK_LUMA_THRESHOLD) {
          darkStreakRef.current += 1
          if (darkStreakRef.current >= DARK_SAMPLES_TO_BLOCK) setCamDark(true)
        } else {
          darkStreakRef.current = 0
          setCamDark(false)
        }
      }, 700)

      setPhase('ready')
    } catch (e: unknown) {
      stopAll()
      const name = (e as { name?: string })?.name
      setErrKind(
        name === 'NotAllowedError' || name === 'SecurityError'
          ? 'denied'
          : name === 'NotFoundError' ||
              name === 'OverconstrainedError' ||
              name === 'DevicesNotFoundError'
            ? 'notfound'
            : 'other'
      )
      setPhase('error')
    }
  }, [handleTrackLost, stopAll])

  // Dọn dẹp khi unmount nếu chưa bàn giao stream cho caller.
  useEffect(() => {
    return () => {
      if (!handedOff.current) stopAll()
    }
  }, [stopAll])

  const ready = phase === 'ready' && camOk && micOk && !camDark

  const proceed = () => {
    if (!ready || !streamRef.current) return
    handedOff.current = true
    stopMonitors() // ngừng đo nhưng GIỮ stream sống để bàn giao
    onReady(streamRef.current)
  }

  const SEGMENTS = 14
  const litSegments = Math.round(level * SEGMENTS)

  return (
    <div className="mx-auto w-full max-w-3xl rounded-3xl border border-white/10 bg-ink-900/60 p-6 shadow-2xl sm:p-8">
      <div className="mb-6 text-center">
        <h1 className="font-display text-2xl font-extrabold text-white">{title}</h1>
        <p className="mx-auto mt-2 max-w-md text-sm text-slate-400">{subtitle}</p>
      </div>

      <div className="grid gap-6 md:grid-cols-[1fr_240px]">
        {/* Camera preview */}
        <div className="relative aspect-video overflow-hidden rounded-2xl bg-ink-950 ring-1 ring-white/10">
          <video
            ref={videoRef}
            muted
            playsInline
            className={`h-full w-full -scale-x-100 object-cover ${camOk ? '' : 'opacity-0'}`}
          />
          {!camOk && (
            <div className="absolute inset-0 grid place-items-center text-slate-500">
              {phase === 'requesting' ? (
                <Loader2 className="h-8 w-8 animate-spin" />
              ) : (
                <Camera className="h-10 w-10" />
              )}
            </div>
          )}
          {camOk && camDark && (
            <div className="absolute inset-0 grid place-items-center bg-black/70 text-center">
              <div className="px-6">
                <EyeOff className="mx-auto h-9 w-9 text-amber-400" />
                <p className="mt-2 text-sm font-semibold text-amber-200">Không thấy hình ảnh</p>
                <p className="mt-1 text-xs text-slate-300">
                  Camera đang bị che hoặc phòng quá tối. Hãy bỏ vật che ống kính / bật thêm đèn.
                </p>
              </div>
            </div>
          )}
          {camOk && !camDark && (
            <span className="absolute left-3 top-3 flex items-center gap-1.5 rounded-full bg-black/50 px-2.5 py-1 text-xs font-medium text-emerald-300 backdrop-blur">
              <span className="h-2 w-2 rounded-full bg-emerald-400" /> Camera
            </span>
          )}
        </div>

        {/* Status + mic meter */}
        <div className="flex flex-col justify-between gap-4">
          <div className="space-y-3">
            <StatusRow
              Icon={Camera}
              label="Camera"
              ok={camOk && !camDark}
              pending={phase === 'requesting'}
              warn={camOk && camDark ? 'Bị che / tối' : null}
            />
            <StatusRow Icon={Mic} label="Micro" ok={micOk} pending={phase === 'requesting'} />

            {/* Mic level */}
            <div className="rounded-xl border border-white/10 bg-ink-950/50 p-3">
              <div className="mb-2 text-xs text-slate-400">Mức âm micro</div>
              <div className="flex items-end gap-1" aria-hidden>
                {Array.from({ length: SEGMENTS }).map((_, i) => (
                  <span
                    key={i}
                    className={`w-full rounded-sm transition-colors ${
                      i < litSegments
                        ? i > SEGMENTS * 0.8
                          ? 'bg-red-400'
                          : i > SEGMENTS * 0.55
                            ? 'bg-amber-400'
                            : 'bg-emerald-400'
                        : 'bg-white/10'
                    }`}
                    style={{ height: `${10 + (i / SEGMENTS) * 22}px` }}
                  />
                ))}
              </div>
              <div className="mt-2 text-[11px] text-slate-500">
                {micOk ? 'Hãy thử nói — vạch sẽ nhảy theo giọng của bạn.' : 'Chưa nhận được micro.'}
              </div>
            </div>
          </div>

          <p className="flex items-start gap-1.5 text-[11px] text-slate-500">
            <ShieldCheck className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" />
            Không có camera/micro hoạt động sẽ không thể vào phỏng vấn.
          </p>
        </div>
      </div>

      {/* Cảnh báo camera bị che (kèm dưới khu vực preview để luôn thấy) */}
      {phase === 'ready' && camDark && (
        <div className="mt-5 flex items-start gap-3 rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
          <EyeOff className="mt-0.5 h-5 w-5 shrink-0 text-amber-400" />
          <div>
            <div className="font-semibold">Camera đang bị che hoặc quá tối</div>
            <div className="mt-1 text-amber-100/80">
              Vì lý do minh bạch của buổi phỏng vấn, bạn cần để camera nhìn rõ khuôn mặt. Hãy bỏ vật
              che ống kính hoặc bật thêm ánh sáng — nút vào phỏng vấn sẽ mở lại ngay khi thấy hình.
            </div>
          </div>
        </div>
      )}

      {/* Error */}
      {phase === 'error' && (
        <div className="mt-5 flex items-start gap-3 rounded-xl border border-red-500/30 bg-red-500/10 p-4 text-sm text-red-200">
          <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-red-400" />
          <div>
            <div className="font-semibold text-red-100">
              {errKind === 'denied'
                ? 'Bạn đã chặn quyền camera/micro'
                : errKind === 'notfound'
                  ? 'Không tìm thấy camera hoặc micro'
                  : errKind === 'lost'
                    ? 'Mất quyền truy cập camera/micro'
                    : 'Không truy cập được thiết bị'}
            </div>
            <div className="mt-1 text-red-200/80">
              {errKind === 'denied'
                ? 'Mở biểu tượng quyền trên thanh địa chỉ trình duyệt → cho phép Camera & Micro → nhấn Thử lại.'
                : errKind === 'notfound'
                  ? 'Hãy kết nối camera/micro và đảm bảo không ứng dụng khác đang chiếm dụng, rồi Thử lại.'
                  : errKind === 'lost'
                    ? 'Quyền camera/micro vừa bị tắt hoặc thiết bị bị ngắt. Hãy bật lại quyền rồi nhấn Thử lại để vào phỏng vấn.'
                    : 'Vui lòng kiểm tra thiết bị rồi thử lại.'}
            </div>
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="mt-6 flex items-center justify-center gap-3">
        {onCancel && (
          <button
            onClick={() => {
              stopAll()
              onCancel()
            }}
            className="rounded-xl px-5 py-3 text-sm font-medium text-slate-300 hover:bg-white/5"
          >
            Quay lại
          </button>
        )}

        {phase === 'idle' && (
          <button
            onClick={start}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-6 py-3 text-sm font-semibold text-white hover:opacity-90"
          >
            <Camera className="h-4 w-4" /> Cho phép camera & micro
          </button>
        )}

        {(phase === 'requesting' || phase === 'ready') && (
          <button
            onClick={proceed}
            disabled={!ready}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-6 py-3 text-sm font-semibold text-white hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {ready ? <Check className="h-4 w-4" /> : <Loader2 className="h-4 w-4 animate-spin" />}
            {ready ? startLabel : camDark ? 'Camera bị che…' : 'Đang kiểm tra…'}
          </button>
        )}

        {phase === 'error' && (
          <button
            onClick={start}
            className="inline-flex items-center gap-2 rounded-xl bg-white/10 px-6 py-3 text-sm font-semibold text-white hover:bg-white/20"
          >
            <RefreshCw className="h-4 w-4" /> Thử lại
          </button>
        )}
      </div>
    </div>
  )
}

function StatusRow({
  Icon,
  label,
  ok,
  pending,
  warn,
}: {
  Icon: typeof Camera
  label: string
  ok: boolean
  pending: boolean
  warn?: string | null
}) {
  return (
    <div className="flex items-center justify-between rounded-xl border border-white/10 bg-ink-950/50 px-3 py-2.5">
      <span className="flex items-center gap-2 text-sm text-slate-200">
        <Icon className="h-4 w-4 text-slate-400" /> {label}
      </span>
      {warn ? (
        <span className="flex items-center gap-1 text-xs font-semibold text-amber-400">
          <AlertTriangle className="h-3.5 w-3.5" /> {warn}
        </span>
      ) : ok ? (
        <span className="flex items-center gap-1 text-xs font-semibold text-emerald-400">
          <Check className="h-3.5 w-3.5" /> Sẵn sàng
        </span>
      ) : pending ? (
        <Loader2 className="h-4 w-4 animate-spin text-slate-400" />
      ) : (
        <span className="text-xs font-medium text-slate-500">Chưa sẵn sàng</span>
      )}
    </div>
  )
}
