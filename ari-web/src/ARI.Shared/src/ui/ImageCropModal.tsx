import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, Check, Loader2, Minus, Plus, RotateCcw, X } from 'lucide-react'

type Point = { x: number; y: number }
/** Vị trí khung nhìn trên ảnh: `zoom` = bội số so với mức "phủ kín khung", `x`/`y` = độ lệch tâm (px màn hình). */
type View = { zoom: number; x: number; y: number }

const MIN_ZOOM = 1
const MAX_ZOOM = 4
/** Ảnh kết quả phải lọt giới hạn của endpoint tải lên (2MB) — hạ chất lượng JPEG dần nếu vượt. */
const MAX_OUTPUT_BYTES = 2 * 1024 * 1024

export interface ImageCropModalProps {
  /** Ảnh gốc người dùng vừa chọn. Chưa gửi lên server — cắt xong mới gửi. */
  file: File
  /** Tỷ lệ khung cắt (rộng / cao). 1 = vuông, dùng cho ảnh đại diện. */
  aspect?: number
  /** Cạnh ngang tối đa của ảnh kết quả (px). Không phóng to quá ảnh gốc. */
  outputSize?: number
  /** Bo tròn vùng xem trước (ảnh đại diện) thay vì khung chữ nhật. */
  circular?: boolean
  /** Đang tải lên ở phía cha — khoá nút và hiện spinner. */
  busy?: boolean
  /** Lỗi từ phía cha (vd upload thất bại) — hiện ngay trong modal để người dùng thử lại. */
  errorMessage?: string
  title?: string
  onCancel: () => void
  /** Nhận file JPEG đã cắt. Trả Promise để modal giữ trạng thái "đang xử lý" tới khi cha xong. */
  onCropped: (file: File) => void | Promise<void>
}

function clampZoom(z: number): number {
  return Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, z))
}

function canvasToBlob(canvas: HTMLCanvasElement, quality: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', quality))
}

/**
 * Modal cắt ảnh theo tỷ lệ cố định trước khi tải lên: kéo để chọn vùng, cuộn/chụm/thanh trượt để
 * phóng to. Ảnh luôn phủ kín khung (không bao giờ lộ nền), kết quả là JPEG vuông đã resize nên
 * không phụ thuộc kích thước ảnh gốc.
 */
export default function ImageCropModal({
  file,
  aspect = 1,
  outputSize = 512,
  circular = true,
  busy = false,
  errorMessage,
  title,
  onCancel,
  onCropped,
}: ImageCropModalProps) {
  const { t } = useTranslation('modules/shared/imageCrop')

  const [image, setImage] = useState<HTMLImageElement | null>(null)
  const [loadFailed, setLoadFailed] = useState(false)
  const [view, setView] = useState<View>({ zoom: 1, x: 0, y: 0 })
  const [frameSize, setFrameSize] = useState({ w: 0, h: 0 })
  const [dragging, setDragging] = useState(false)
  const [processing, setProcessing] = useState(false)

  const frameRef = useRef<HTMLDivElement | null>(null)
  const pointers = useRef(new Map<number, Point>())
  const dragStart = useRef<{ pointer: Point; view: View } | null>(null)
  const pinchStart = useRef<{ dist: number; mid: Point; view: View } | null>(null)

  const locked = busy || processing

  // Ảnh gốc → HTMLImageElement (dùng chung cho cả xem trước lẫn vẽ canvas nên khung nhìn khớp
  // tuyệt đối với ảnh xuất ra).
  useEffect(() => {
    const url = URL.createObjectURL(file)
    const img = new Image()
    img.onload = () => {
      setImage(img)
      setView({ zoom: 1, x: 0, y: 0 })
    }
    img.onerror = () => setLoadFailed(true)
    img.src = url
    return () => URL.revokeObjectURL(url)
  }, [file])

  // Khung cắt co giãn theo bề rộng modal → đo lại mỗi khi đổi kích thước cửa sổ.
  useEffect(() => {
    const el = frameRef.current
    if (!el) return
    const measure = () => {
      const w = el.clientWidth
      setFrameSize({ w, h: Math.round(w / aspect) })
    }
    measure()
    const ro = new ResizeObserver(measure)
    ro.observe(el)
    return () => ro.disconnect()
  }, [aspect])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !locked) onCancel()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [locked, onCancel])

  // Tỷ lệ "phủ kín khung": cạnh ngắn của ảnh vừa đúng cạnh tương ứng của khung.
  const geo = useMemo(() => {
    if (!image || !frameSize.w) return null
    const natW = image.naturalWidth
    const natH = image.naturalHeight
    if (!natW || !natH) return null
    return { natW, natH, base: Math.max(frameSize.w / natW, frameSize.h / natH) }
  }, [image, frameSize])

  /** Giữ ảnh luôn phủ kín khung: độ lệch không được vượt quá phần ảnh thừa ra ngoài. */
  const clampView = useCallback(
    (v: View): View => {
      if (!geo) return v
      const w = geo.natW * geo.base * v.zoom
      const h = geo.natH * geo.base * v.zoom
      const maxX = Math.max(0, (w - frameSize.w) / 2)
      const maxY = Math.max(0, (h - frameSize.h) / 2)
      return {
        zoom: v.zoom,
        x: Math.min(maxX, Math.max(-maxX, v.x)),
        y: Math.min(maxY, Math.max(-maxY, v.y)),
      }
    },
    [geo, frameSize]
  )

  /** Phóng to/thu nhỏ quanh tâm khung — giữ nguyên điểm ảnh đang nằm giữa khung. */
  const zoomTo = useCallback(
    (next: number) =>
      setView((v) => {
        const zoom = clampZoom(next)
        return clampView({ zoom, x: (v.x * zoom) / v.zoom, y: (v.y * zoom) / v.zoom })
      }),
    [clampView]
  )

  // Cuộn chuột để phóng to: phải addEventListener thủ công vì React gắn wheel dạng passive
  // (preventDefault trong onWheel của React không chặn được cuộn trang).
  useEffect(() => {
    const el = frameRef.current
    if (!el || !geo || locked) return
    const onWheel = (e: WheelEvent) => {
      e.preventDefault()
      setView((v) => {
        const zoom = clampZoom(v.zoom * Math.exp(-e.deltaY / 400))
        return clampView({ zoom, x: (v.x * zoom) / v.zoom, y: (v.y * zoom) / v.zoom })
      })
    }
    el.addEventListener('wheel', onWheel, { passive: false })
    return () => el.removeEventListener('wheel', onWheel)
  }, [geo, locked, clampView])

  function pointerList(): Point[] {
    return Array.from(pointers.current.values())
  }

  function onPointerDown(e: React.PointerEvent<HTMLDivElement>) {
    if (!geo || locked) return
    e.currentTarget.setPointerCapture(e.pointerId)
    pointers.current.set(e.pointerId, { x: e.clientX, y: e.clientY })
    const pts = pointerList()
    if (pts.length === 1) {
      dragStart.current = { pointer: pts[0], view }
      setDragging(true)
    } else if (pts.length === 2) {
      pinchStart.current = {
        dist: Math.hypot(pts[0].x - pts[1].x, pts[0].y - pts[1].y) || 1,
        mid: { x: (pts[0].x + pts[1].x) / 2, y: (pts[0].y + pts[1].y) / 2 },
        view,
      }
    }
  }

  function onPointerMove(e: React.PointerEvent<HTMLDivElement>) {
    if (!geo || locked || !pointers.current.has(e.pointerId)) return
    pointers.current.set(e.pointerId, { x: e.clientX, y: e.clientY })
    const pts = pointerList()

    if (pts.length >= 2 && pinchStart.current) {
      const start = pinchStart.current
      const dist = Math.hypot(pts[0].x - pts[1].x, pts[0].y - pts[1].y) || 1
      const mid = { x: (pts[0].x + pts[1].x) / 2, y: (pts[0].y + pts[1].y) / 2 }
      const zoom = clampZoom((start.view.zoom * dist) / start.dist)
      const ratio = zoom / start.view.zoom
      setView(
        clampView({
          zoom,
          x: start.view.x * ratio + (mid.x - start.mid.x),
          y: start.view.y * ratio + (mid.y - start.mid.y),
        })
      )
      return
    }

    const start = dragStart.current
    if (!start) return
    setView(
      clampView({
        zoom: start.view.zoom,
        x: start.view.x + (e.clientX - start.pointer.x),
        y: start.view.y + (e.clientY - start.pointer.y),
      })
    )
  }

  function endPointer(e: React.PointerEvent<HTMLDivElement>) {
    pointers.current.delete(e.pointerId)
    const pts = pointerList()
    if (pts.length < 2) pinchStart.current = null
    if (pts.length === 1) {
      // Nhả 1 ngón sau khi chụm: lấy ngón còn lại làm mốc kéo mới, tránh ảnh nhảy.
      dragStart.current = { pointer: pts[0], view }
    } else if (pts.length === 0) {
      dragStart.current = null
      setDragging(false)
    }
  }

  async function renderCrop(): Promise<Blob | null> {
    if (!image || !geo) return null
    const scale = geo.base * view.zoom

    // Khung nhìn (px màn hình) → hình chữ nhật tương ứng trên ảnh gốc.
    const sw = Math.min(geo.natW, frameSize.w / scale)
    const sh = Math.min(geo.natH, frameSize.h / scale)
    const sx = Math.max(0, Math.min(geo.natW - sw, geo.natW / 2 - view.x / scale - sw / 2))
    const sy = Math.max(0, Math.min(geo.natH - sh, geo.natH / 2 - view.y / scale - sh / 2))

    // Không phóng to quá ảnh gốc — thà nhỏ mà nét còn hơn to mà nhoè.
    const targetW = Math.max(1, Math.round(Math.min(outputSize, sw)))
    const targetH = Math.max(1, Math.round(targetW / aspect))

    const canvas = document.createElement('canvas')
    canvas.width = targetW
    canvas.height = targetH
    const ctx = canvas.getContext('2d')
    if (!ctx) return null
    ctx.imageSmoothingEnabled = true
    ctx.imageSmoothingQuality = 'high'
    // JPEG không có kênh trong suốt: nền trắng thay vì đen cho ảnh PNG/WEBP trong suốt.
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, targetW, targetH)

    // Thu nhỏ nhiều lần (ảnh điện thoại 12MP → 512px) bằng drawImage một bước sẽ rỗ;
    // createImageBitmap có bộ lọc chất lượng cao riêng. Không hỗ trợ thì quay về drawImage.
    let drawn = false
    if (typeof createImageBitmap === 'function' && sw / targetW > 2) {
      try {
        const bmp = await createImageBitmap(
          image,
          Math.round(sx),
          Math.round(sy),
          Math.round(sw),
          Math.round(sh),
          { resizeWidth: targetW, resizeHeight: targetH, resizeQuality: 'high' }
        )
        ctx.drawImage(bmp, 0, 0)
        bmp.close()
        drawn = true
      } catch {
        drawn = false
      }
    }
    if (!drawn) ctx.drawImage(image, sx, sy, sw, sh, 0, 0, targetW, targetH)

    let last: Blob | null = null
    for (const quality of [0.92, 0.82, 0.7]) {
      last = await canvasToBlob(canvas, quality)
      if (last && last.size <= MAX_OUTPUT_BYTES) return last
    }
    return last
  }

  async function handleSave() {
    if (!image || !geo || locked) return
    setProcessing(true)
    try {
      const blob = await renderCrop()
      if (!blob) {
        setLoadFailed(true)
        return
      }
      const base = file.name.replace(/\.[^./\\]+$/, '') || 'avatar'
      await onCropped(new File([blob], `${base}.jpg`, { type: 'image/jpeg' }))
    } finally {
      setProcessing(false)
    }
  }

  const dispW = geo ? geo.natW * geo.base * view.zoom : 0
  const dispH = geo ? geo.natH * geo.base * view.zoom : 0

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-ink-900/50 p-4 backdrop-blur-sm"
      onClick={() => !locked && onCancel()}
      role="dialog"
      aria-modal="true"
    >
      <div
        className="w-full max-w-md rounded-2xl border border-ink-200 bg-white p-5 shadow-card-hover dark:border-ink-700 dark:bg-ink-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-1 flex items-center justify-between">
          <h3 className="font-display text-lg font-bold text-ink-900 dark:text-white">
            {title || t('title')}
          </h3>
          <button
            type="button"
            onClick={onCancel}
            disabled={locked}
            className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-ink-700 disabled:opacity-40"
            aria-label={t('cancel')}
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        <p className="text-xs text-ink-500">{t('hint')}</p>

        {/* Khung cắt */}
        <div
          ref={frameRef}
          className="relative mt-4 w-full select-none overflow-hidden rounded-2xl bg-ink-900"
          style={{
            aspectRatio: String(aspect),
            touchAction: 'none',
            cursor: geo ? (dragging ? 'grabbing' : 'grab') : 'default',
          }}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={endPointer}
          onPointerCancel={endPointer}
        >
          {image && geo ? (
            <img
              src={image.src}
              alt=""
              draggable={false}
              className="pointer-events-none absolute left-1/2 top-1/2 max-w-none"
              style={{
                width: dispW,
                height: dispH,
                transform: `translate(-50%, -50%) translate(${view.x}px, ${view.y}px)`,
              }}
            />
          ) : (
            <div className="absolute inset-0 grid place-items-center text-white/70">
              {loadFailed ? (
                <span className="flex items-center gap-2 text-xs">
                  <AlertTriangle className="h-4 w-4" /> {t('loadFailed')}
                </span>
              ) : (
                <Loader2 className="h-6 w-6 animate-spin" />
              )}
            </div>
          )}

          {/* Lớp phủ chỉ vùng sẽ được giữ lại — phần ngoài bị làm tối. */}
          <div
            className={`pointer-events-none absolute inset-0 shadow-[0_0_0_9999px_rgba(15,23,42,0.55)] ring-2 ring-inset ring-white/70 ${
              circular ? 'rounded-full' : 'rounded-xl'
            }`}
          />
        </div>

        {/* Phóng to / thu nhỏ */}
        <div className="mt-4 flex items-center gap-3">
          <button
            type="button"
            onClick={() => zoomTo(view.zoom - 0.25)}
            disabled={!geo || locked || view.zoom <= MIN_ZOOM}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-lg border border-ink-200 text-ink-600 hover:bg-ink-50 disabled:opacity-40 dark:border-ink-700"
            aria-label={t('zoomOut')}
          >
            <Minus className="h-4 w-4" />
          </button>
          <input
            type="range"
            min={MIN_ZOOM}
            max={MAX_ZOOM}
            step={0.01}
            value={view.zoom}
            disabled={!geo || locked}
            onChange={(e) => zoomTo(Number(e.target.value))}
            aria-label={t('zoom')}
            className="h-1.5 w-full cursor-pointer appearance-none rounded-full bg-ink-200 accent-brand-600 disabled:opacity-40"
          />
          <button
            type="button"
            onClick={() => zoomTo(view.zoom + 0.25)}
            disabled={!geo || locked || view.zoom >= MAX_ZOOM}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-lg border border-ink-200 text-ink-600 hover:bg-ink-50 disabled:opacity-40 dark:border-ink-700"
            aria-label={t('zoomIn')}
          >
            <Plus className="h-4 w-4" />
          </button>
          <button
            type="button"
            onClick={() => setView({ zoom: 1, x: 0, y: 0 })}
            disabled={!geo || locked}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-lg border border-ink-200 text-ink-600 hover:bg-ink-50 disabled:opacity-40 dark:border-ink-700"
            title={t('reset')}
            aria-label={t('reset')}
          >
            <RotateCcw className="h-4 w-4" />
          </button>
        </div>

        {errorMessage && (
          <div className="mt-3 flex items-start gap-2 rounded-xl bg-red-50 px-3 py-2 text-xs text-red-700">
            <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {errorMessage}
          </div>
        )}

        <div className="mt-5 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            disabled={locked}
            className="rounded-xl border border-ink-200 px-4 py-2 text-sm font-semibold text-ink-600 hover:bg-ink-50 disabled:opacity-50 dark:border-ink-700 dark:text-ink-300"
          >
            {t('cancel')}
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={!image || !geo || locked}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2 text-sm font-bold text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {locked ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
            {t('apply')}
          </button>
        </div>
      </div>
    </div>
  )
}
