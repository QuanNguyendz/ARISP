import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { createPortal } from 'react-dom'
import { renderAsync } from 'docx-preview'
import { Download, FileText, Loader2, X, AlertCircle } from 'lucide-react'

type DocKind = 'pdf' | 'docx' | 'image' | 'other'

interface OpenedDoc {
  url: string
  fileName: string
}

interface DocumentViewerContextValue {
  /** Mở trình xem tài liệu inline. fileName để suy ra định dạng + hiển thị tiêu đề. */
  openDocument: (url: string, fileName?: string) => void
}

const DocumentViewerContext = createContext<DocumentViewerContextValue | null>(null)

/** Suy ra loại file từ tên file hoặc URL (bỏ querystring của presigned URL). */
function detectKind(nameOrUrl: string): DocKind {
  const clean = nameOrUrl.split('?')[0].split('#')[0].toLowerCase()
  if (clean.endsWith('.pdf')) return 'pdf'
  if (clean.endsWith('.docx') || clean.endsWith('.doc')) return 'docx'
  if (/\.(png|jpe?g|gif|webp|bmp|svg)$/.test(clean)) return 'image'
  return 'other'
}

function fileLabel(fileName: string): string {
  return fileName || 'Tài liệu'
}

function DocxRender({ url }: { url: string }) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    const container = containerRef.current
    setLoading(true)
    setError(null)
    if (container) container.innerHTML = ''
    ;(async () => {
      try {
        const res = await fetch(url)
        if (!res.ok) throw new Error(`HTTP ${res.status}`)
        const blob = await res.blob()
        if (cancelled || !containerRef.current) return
        await renderAsync(blob, containerRef.current, undefined, {
          className: 'docx',
          inWrapper: true,
          ignoreWidth: false,
          ignoreHeight: false,
          breakPages: true,
        })
        if (!cancelled) setLoading(false)
      } catch (e) {
        if (!cancelled) {
          setError(
            'Không thể hiển thị file DOCX trực tiếp (có thể do CORS hoặc file lỗi). Hãy tải về để xem.'
          )
          setLoading(false)
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [url])

  return (
    <div className="relative h-full w-full overflow-auto bg-ink-100 dark:bg-ink-800">
      {loading && (
        <div className="absolute inset-0 z-10 flex flex-col items-center justify-center gap-3 bg-ink-100/80 dark:bg-ink-800/80">
          <Loader2 className="h-8 w-8 animate-spin text-brand-600 dark:text-brand-400" />
          <p className="text-sm text-ink-500 dark:text-ink-300">Đang dựng nội dung tài liệu...</p>
        </div>
      )}
      {error && (
        <div className="flex flex-col items-center justify-center gap-3 p-10 text-center">
          <AlertCircle className="h-10 w-10 text-amber-500" />
          <p className="max-w-sm text-sm text-ink-600 dark:text-ink-300">{error}</p>
        </div>
      )}
      {/* docx-preview chèn nội dung vào đây */}
      <div ref={containerRef} className="mx-auto my-4 w-fit" />
    </div>
  )
}

function ViewerModal({ doc, onClose }: { doc: OpenedDoc; onClose: () => void }) {
  // Ưu tiên đuôi từ fileName; nếu fileName không có đuôi nhận biết được thì suy từ URL.
  const kind = useMemo(() => {
    const byName = detectKind(doc.fileName)
    return byName !== 'other' ? byName : detectKind(doc.url)
  }, [doc])

  // ESC để đóng
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [onClose])

  return createPortal(
    <div className="fixed inset-0 z-[100] flex flex-col bg-ink-950/70 backdrop-blur-sm p-3 sm:p-6">
      <div className="mx-auto flex h-full w-full max-w-5xl flex-col overflow-hidden rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-ink-900 shadow-card-hover">
        {/* Header */}
        <div className="flex items-center justify-between gap-3 border-b border-ink-100 dark:border-white/10 px-4 py-3">
          <div className="flex min-w-0 items-center gap-2">
            <FileText className="h-5 w-5 shrink-0 text-brand-600 dark:text-brand-400" />
            <span className="truncate text-sm font-medium text-ink-900 dark:text-white">
              {fileLabel(doc.fileName)}
            </span>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <a
              href={doc.url}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1.5 rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-1.5 text-xs font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
            >
              <Download className="h-3.5 w-3.5" /> Tải về
            </a>
            <button
              type="button"
              onClick={onClose}
              className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-ink-700 dark:hover:bg-white/10 dark:hover:text-white"
              aria-label="Đóng"
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-hidden">
          {kind === 'pdf' && (
            <iframe src={doc.url} title={doc.fileName} className="h-full w-full border-0" />
          )}
          {kind === 'docx' && <DocxRender url={doc.url} />}
          {kind === 'image' && (
            <div className="flex h-full w-full items-center justify-center overflow-auto bg-ink-100 dark:bg-ink-800 p-4">
              <img src={doc.url} alt={doc.fileName} className="max-h-full max-w-full object-contain" />
            </div>
          )}
          {kind === 'other' && (
            <div className="flex h-full flex-col items-center justify-center gap-3 p-10 text-center">
              <FileText className="h-10 w-10 text-ink-400" />
              <p className="max-w-sm text-sm text-ink-600 dark:text-ink-300">
                Không hỗ trợ xem trực tiếp định dạng này. Hãy tải về để mở.
              </p>
              <a
                href={doc.url}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-1.5 rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
              >
                <Download className="h-4 w-4" /> Tải về
              </a>
            </div>
          )}
        </div>
      </div>
    </div>,
    document.body
  )
}

/** Provider toàn cục: mount 1 lần, mọi nơi gọi useDocumentViewer().openDocument(url, name). */
export function DocumentViewerProvider({ children }: { children: ReactNode }) {
  const [doc, setDoc] = useState<OpenedDoc | null>(null)

  const openDocument = useCallback((url: string, fileName?: string) => {
    if (!url) return
    setDoc({ url, fileName: fileName ?? '' })
  }, [])

  const value = useMemo(() => ({ openDocument }), [openDocument])

  return (
    <DocumentViewerContext.Provider value={value}>
      {children}
      {doc && <ViewerModal doc={doc} onClose={() => setDoc(null)} />}
    </DocumentViewerContext.Provider>
  )
}

export function useDocumentViewer(): DocumentViewerContextValue {
  const ctx = useContext(DocumentViewerContext)
  if (!ctx) throw new Error('useDocumentViewer must be used within DocumentViewerProvider')
  return ctx
}
