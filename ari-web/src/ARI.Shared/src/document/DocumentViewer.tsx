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
import { useTranslation } from 'react-i18next'
import { renderAsync } from 'docx-preview'
import { Download, FileText, Loader2, X, AlertCircle } from 'lucide-react'
import { resolveAssetUrl, API_BASE_URL } from '@ari/shared/config/constants'
import { apiClient } from '@ari/shared/api/apiClient'

type DocKind = 'pdf' | 'docx' | 'image' | 'other'

interface OpenedDoc {
  url: string
  fileName: string
  /** Endpoint trả bản PDF do server dựng — xem `pdfUrl` của `DocumentPreview`. */
  pdfUrl?: string
}

interface DocumentViewerContextValue {
  /** Mở trình xem tài liệu inline. fileName để suy ra định dạng + hiển thị tiêu đề. */
  openDocument: (url: string, fileName?: string, pdfUrl?: string) => void
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

/**
 * `missing` = server trả 404/410: file KHÔNG có ở đó, không cách nào dựng ra được.
 * `failed`  = mạng lỗi, CORS chặn, hay 5xx: file có thể vẫn còn, chỉ là lượt tải của ta không tới.
 *
 * Tách hai thứ này ra vì chúng dẫn tới hai việc khác hẳn nhau — một cái phải đi sửa dữ liệu, một cái
 * còn cứu được bằng cách để trình duyệt tự tải (xem `PdfRender`). Gộp chung thành "không tải được"
 * thì người đọc thông báo không biết nên làm gì tiếp.
 */
type FetchError = 'missing' | 'failed'

type FetchState = {
  blob: Blob | null
  objectUrl: string
  loading: boolean
  error: FetchError | null
  /** Lý do kỹ thuật ("HTTP 404", "Failed to fetch"…) — in kèm thông báo để khỏi phải mở DevTools. */
  detail: string
}

/**
 * URL dùng RIÊNG cho lượt tải dựng bản xem trước.
 *
 * Trình quản lý tải ngoài (IDM, FDM…) cài extension vào trình duyệt và cướp mọi request có URL kết
 * thúc bằng đuôi file nằm trong danh sách của nó — kể cả request `fetch` mà ứng dụng phát ra chỉ để
 * dựng bản xem trước. Người dùng đang bấm "xem CV" thì nhận hộp thoại tải file.
 *
 * Đây cũng là lời giải cho chuyện "dev tải về, production thì không": trên dev, URL kết thúc bằng
 * `.pdf`; trên production là presigned URL của R2, kết thúc bằng `?X-Amz-...` nên luật theo đuôi file
 * không khớp. Thêm một tham số vào đây làm URL của dev mang đúng hình dạng đó.
 *
 * KHÔNG đụng vào URL đã có query: presigned URL ký theo đúng chuỗi truy vấn, thêm tham số là hỏng chữ
 * ký và ảnh/CV trên production sẽ 403.
 *
 * Đây là biện pháp GIẢM khả năng bị cướp, không phải bảo đảm — extension chạy dưới tầng ứng dụng, và
 * nếu nó bắt theo `Content-Type` thì vẫn cướp. Cách chắc chắn nằm ở cấu hình của chính phần mềm đó.
 */
function previewUrl(url: string): string {
  if (!url || url.includes('?')) return url
  return `${url}?inline=1`
}

/**
 * Tải file về **blob của chính trang này** rồi mới dựng, thay vì trỏ thẳng thẻ xem vào URL gốc.
 *
 * Lý do không phải là làm đẹp: URL gốc có thể ở origin khác (dev trỏ sang `localhost:5000`, production
 * là presigned URL của R2), mà cách trình duyệt xử lý một file tải từ origin khác thì KHÔNG giống
 * nhau — cùng một file PDF, chỗ thì hiện trong khung xem, chỗ thì rơi thẳng vào mục tải xuống, và
 * người dùng thấy hai môi trường "chạy khác nhau" dù chạy cùng một bản build. `blob:` là same-origin
 * với trang, nên đường dựng chỉ còn MỘT.
 *
 * Kèm theo, đây là chỗ duy nhất biết được việc tải hỏng: trước đây PDF hỏng chỉ ra một khung trắng
 * không chữ nào, vì `<iframe>` không có sự kiện lỗi dùng được.
 */
/**
 * Tải MỘT file, thử lần lượt các ứng viên cho tới khi có cái chạy.
 *
 * Dùng cho bản dựng PDF phía server: ứng viên đầu là endpoint chuyển đổi, ứng viên sau là chính file
 * gốc. Môi trường chưa cài LibreOffice thì endpoint trả 404 — đó là câu trả lời hợp lệ, không phải
 * sự cố — và ta lặng lẽ lùi về file gốc để bộ dựng trong trình duyệt lo nốt.
 *
 * URL trỏ vào API phải kèm `Authorization`, nên đi qua `apiClient`; file trong kho (presigned URL của
 * R2, thư mục tĩnh `/uploads`) thì không có và không được gửi token ra ngoài.
 */
async function fetchOne(url: string): Promise<{ blob: Blob } | { error: FetchError; detail: string }> {
  try {
    if (url.startsWith(API_BASE_URL)) {
      const { data } = await apiClient.get<Blob>(url.slice(API_BASE_URL.length), { responseType: 'blob' })
      return { blob: data }
    }
    const res = await fetch(previewUrl(url))
    if (!res.ok) {
      const kind: FetchError = res.status === 404 || res.status === 410 ? 'missing' : 'failed'
      return { error: kind, detail: `HTTP ${res.status}` }
    }
    return { blob: await res.blob() }
  } catch (e) {
    const status = (e as { response?: { status?: number } })?.response?.status
    const kind: FetchError = status === 404 || status === 410 ? 'missing' : 'failed'
    const detail = status ? `HTTP ${status}` : e instanceof Error ? e.message : String(e)
    return { error: kind, detail }
  }
}

/**
 * Tải file về **blob của chính trang này** rồi mới dựng, thay vì trỏ thẳng thẻ xem vào URL gốc.
 *
 * Lý do không phải là làm đẹp: URL gốc có thể ở origin khác (dev trỏ sang `localhost:5000`, production
 * là presigned URL của R2), mà cách trình duyệt xử lý một file tải từ origin khác thì KHÔNG giống
 * nhau — cùng một file PDF, chỗ thì hiện trong khung xem, chỗ thì rơi thẳng vào mục tải xuống, và
 * người dùng thấy hai môi trường "chạy khác nhau" dù chạy cùng một bản build. `blob:` là same-origin
 * với trang, nên đường dựng chỉ còn MỘT.
 *
 * Kèm theo, đây là chỗ duy nhất biết được việc tải hỏng: trước đây PDF hỏng chỉ ra một khung trắng
 * không chữ nào, vì `<iframe>` không có sự kiện lỗi dùng được.
 */
function useFileBlob(url: string, fallbackUrl = ''): FetchState {
  const [state, setState] = useState<FetchState>({
    blob: null,
    objectUrl: '',
    loading: true,
    error: null,
    detail: '',
  })

  useEffect(() => {
    let cancelled = false
    let created = ''
    const candidates = [url, fallbackUrl].filter(Boolean)
    if (candidates.length === 0) {
      setState({ blob: null, objectUrl: '', loading: false, error: null, detail: '' })
      return
    }
    setState({ blob: null, objectUrl: '', loading: true, error: null, detail: '' })
    ;(async () => {
      let last: { error: FetchError; detail: string } = { error: 'failed', detail: '' }
      for (const candidate of candidates) {
        const res = await fetchOne(candidate)
        if (cancelled) return
        if ('blob' in res) {
          created = URL.createObjectURL(res.blob)
          setState({ blob: res.blob, objectUrl: created, loading: false, error: null, detail: '' })
          return
        }
        last = res
      }
      // Báo lỗi của ứng viên CUỐI: ứng viên đầu hỏng là chuyện đã lường trước (chưa cài bộ chuyển
      // đổi), còn file gốc cũng hỏng mới là điều người dùng cần biết.
      setState({ blob: null, objectUrl: '', loading: false, error: last.error, detail: last.detail })
    })()
    return () => {
      cancelled = true
      if (created) URL.revokeObjectURL(created)
    }
  }, [url, fallbackUrl])

  return state
}

function ViewerStatus({
  loading,
  message,
  detail,
}: {
  loading: boolean
  message: string
  detail?: string
}) {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-3 p-10 text-center">
      {loading ? (
        <Loader2 className="h-8 w-8 animate-spin text-brand-600 dark:text-brand-400" />
      ) : (
        <AlertCircle className="h-10 w-10 text-amber-500" />
      )}
      <p className="max-w-sm text-sm text-ink-500 dark:text-ink-300">{message}</p>
      {detail && <p className="max-w-sm font-mono text-xs text-ink-400">{detail}</p>}
    </div>
  )
}

function PdfRender({
  blob,
  objectUrl,
  loading,
  error,
  detail,
  name,
  t,
}: {
  blob: Blob | null
  objectUrl: string
  loading: boolean
  error: FetchError | null
  detail: string
  name: string
  t: ReturnType<typeof useTranslation<'modules/shared/documentViewer'>>['t']
}) {
  // Hook phải nằm trên mọi nhánh return sớm.
  const frameUrl = usePdfObjectUrl(blob, objectUrl)

  if (loading) return <ViewerStatus loading message={t('documentViewer.loading')} />

  // 404: không có gì để dựng, nói thẳng file không còn ở đó thay vì đổ cho "tải lỗi".
  if (error === 'missing')
    return <ViewerStatus loading={false} message={t('documentViewer.fileMissing')} detail={detail} />

  // KHÔNG có đường lui kiểu "trỏ thẳng iframe vào URL gốc".
  //
  // Đường đó không cần CORS nên trông như một lưới đỡ miễn phí, nhưng nó chính là thứ khiến Chrome
  // TẢI FILE VỀ thay vì hiện ra: với tài liệu ở origin khác, trình duyệt tự quyết định dựng hay tải,
  // và quyết định đó nằm ngoài tầm với của ứng dụng. Thà báo lỗi rõ ràng còn hơn im lặng làm đúng cái
  // việc mà cả màn hình này sinh ra để tránh.
  if (error || !frameUrl)
    return <ViewerStatus loading={false} message={t('documentViewer.pdfError')} detail={detail} />

  return <iframe src={frameUrl} title={name} className="h-full w-full border-0" />
}

/**
 * Đỡ cho file .docx KHÔNG khai khổ giấy (`w:sectPr`).
 *
 * docx-preview đặt bề rộng trang theo đúng khai báo trong file; không có thì `<section>` không có bề
 * rộng nào và co lại vừa nội dung — ra một cột chữ hẹp giữa màn hình. Bộ dựng JD của hệ thống từng
 * quên khai (đã sửa), nhưng những file SINH RA TRƯỚC ĐÓ vẫn nằm nguyên trong kho và không tự tốt lên,
 * nên phải đỡ ở phía đọc chứ không chỉ ở phía ghi.
 *
 * Chỉ chạm vào trang nào thực sự thiếu bề rộng — file của Word luôn có `w:sectPr`, và ghi đè khổ giấy
 * của chúng là bóp méo đúng thứ người ta gửi tới.
 */
function applyFallbackPageSize(container: HTMLElement) {
  container.querySelectorAll<HTMLElement>('section.docx').forEach((page) => {
    if (page.style.width) return
    page.style.width = '21cm' // A4 dọc
    if (!page.style.padding) page.style.padding = '1.76cm' // 1000 twip, bằng lề của bản PDF
  })
}

/**
 * Tên để lưu khi người dùng bấm "Tải về".
 *
 * Nơi gọi hay đặt tên dễ đọc mà KHÔNG kèm đuôi ("Nguyễn Văn A - CV"), và trình duyệt lưu đúng chuỗi
 * đó — ra một file không đuôi, mở lên máy không biết dùng gì. Đuôi thật nằm ở URL (presigned URL của
 * R2 thì nằm trước dấu `?`), nên ghép lại từ đó.
 */
export function downloadName(fileName: string, url: string): string | undefined {
  const name = fileName.trim()
  if (!name) return undefined
  if (/\.[a-z0-9]{2,5}$/i.test(name)) return name

  const ext = url.split('?')[0].split('#')[0].match(/\.([a-z0-9]{2,5})$/i)?.[1]
  return ext ? `${name}.${ext.toLowerCase()}` : name
}

/**
 * Loại file suy từ CHÍNH BYTE đã tải về (`Content-Type` của response).
 *
 * Tên file có thể nói dối: màn hồ sơ ứng viên từng ghép cứng đuôi ".pdf" vào tên ứng viên, nên CV
 * .docx đi vào nhánh dựng PDF. Byte thì không nói dối, nên khi nó nói rõ ràng thì nó thắng.
 *
 * Trả `null` khi kiểu KHÔNG nói lên điều gì (`application/octet-stream`, chuỗi rỗng) — lúc đó phải
 * quay về suy theo tên, vì đoán bừa còn tệ hơn.
 */
function kindOfBlob(blob: Blob | null): DocKind | null {
  const type = blob?.type?.toLowerCase() ?? ''
  if (!type || type === 'application/octet-stream') return null
  if (type === 'application/pdf') return 'pdf'
  if (type.includes('wordprocessingml.document') || type === 'application/msword') return 'docx'
  if (type.startsWith('image/')) return 'image'
  return null
}

/**
 * URL cho `<iframe>`, LUÔN mang kiểu `application/pdf`.
 *
 * Đây là chốt chặn cuối để màn hình này KHÔNG BAO GIỜ tự tải file về. Một `<iframe>` trỏ vào blob mà
 * trình duyệt không dựng được (tài liệu Word, hay `application/octet-stream`) thì Chrome lặng lẽ tải
 * nó xuống — không cần ai bấm gì, chỉ cần trang được mở. Đúng thứ mà cả màn hình này sinh ra để tránh.
 *
 * Nếu đã quyết định dựng bằng nhánh PDF thì ép kiểu về PDF: byte sai định dạng chỉ làm trình xem PDF
 * báo lỗi NGAY TRONG KHUNG — một kết cục nhìn thấy được, thay vì một file rơi vào thư mục Downloads.
 */
function usePdfObjectUrl(blob: Blob | null, objectUrl: string): string {
  const [retyped, setRetyped] = useState('')

  useEffect(() => {
    if (!blob || blob.type === 'application/pdf') {
      setRetyped('')
      return
    }
    const url = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }))
    setRetyped(url)
    return () => URL.revokeObjectURL(url)
  }, [blob])

  return retyped || objectUrl
}

/** Loại file theo tên trước, URL sau — tên file là thứ đáng tin hơn presigned URL. */
function kindOf(name: string, url: string): DocKind {
  const byName = detectKind(name)
  return byName !== 'other' ? byName : detectKind(url)
}

function DocxRender({
  blob,
  loading,
  error,
  detail,
  t,
}: {
  blob: Blob | null
  loading: boolean
  error: FetchError | null
  detail: string
  t: ReturnType<typeof useTranslation<'modules/shared/documentViewer'>>['t']
}) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [rendering, setRendering] = useState(true)
  const [renderFailed, setRenderFailed] = useState(false)

  useEffect(() => {
    if (!blob) return
    let cancelled = false
    setRendering(true)
    setRenderFailed(false)
    ;(async () => {
      try {
        if (!containerRef.current) return
        containerRef.current.innerHTML = ''
        await renderAsync(blob, containerRef.current, undefined, {
          className: 'docx',
          inWrapper: true,
          ignoreWidth: false,
          ignoreHeight: false,
          breakPages: true,
          // Bật xử lý điểm dừng tab (`w:tabs`). Không có nó, mỗi ký tự tab bị dựng thành một khoảng
          // trắng cố định, nên mẫu CV nào dùng tab để chia cột sẽ có chữ trôi dạt sang phải.
          experimental: true,
          // Nhúng ảnh dạng base64 thay vì `blob:` URL. `blob:` bị thu hồi khi component unmount, mà
          // React StrictMode ở dev mount–unmount–mount: lượt dựng thứ hai ăn phải URL đã chết và ảnh
          // biến thành ô trống.
          useBase64URL: true,
        })
        if (!cancelled) {
          applyFallbackPageSize(containerRef.current)
          setRendering(false)
        }
      } catch {
        if (!cancelled) {
          setRenderFailed(true)
          setRendering(false)
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [blob])

  if (loading) return <ViewerStatus loading message={t('documentViewer.loading')} />
  // DOCX không có đường lui như PDF: trình duyệt không dựng được .docx, phải có bytes mới vẽ ra được.
  if (error === 'missing')
    return <ViewerStatus loading={false} message={t('documentViewer.fileMissing')} detail={detail} />
  if (error || renderFailed)
    return <ViewerStatus loading={false} message={t('documentViewer.docxError')} detail={detail} />

  return (
    <div className="relative h-full w-full overflow-auto bg-ink-100 dark:bg-ink-800">
      {rendering && (
        <div className="absolute inset-0 z-10 flex flex-col items-center justify-center gap-3 bg-ink-100/80 dark:bg-ink-800/80">
          <Loader2 className="h-8 w-8 animate-spin text-brand-600 dark:text-brand-400" />
          <p className="text-sm text-ink-500 dark:text-ink-300">{t('documentViewer.loading')}</p>
        </div>
      )}
      {/* docx-preview tự dựng `.docx-wrapper` (nền xám, căn giữa, trang trắng có đổ bóng) và đặt bề
          rộng trang theo `w:sectPr` của file. Lớp bọc ở đây chỉ cho nó đủ chỗ + cho phép cuộn ngang
          khi màn hẹp hơn khổ A4 — `w-fit` trước đây ghì nó lại khiến trang không bao giờ rộng ra. */}
      <div ref={containerRef} className="min-w-fit" />
    </div>
  )
}

/**
 * Khung xem tài liệu **không kèm hộp thoại** — dùng được cả trong lớp phủ lẫn nhúng thẳng vào trang.
 *
 * Tách ra vì màn chi tiết tin cần đọc CV **ngay tại chỗ** (không bắt mở lớp phủ rồi đóng lại cho mỗi
 * ứng viên). Chép phần dựng sang đó là có hai bộ hiển thị tài liệu, và lần sửa sau chỉ một bộ được
 * sửa — đúng kiểu trôi lệch mà bản xem trước JD vừa phải đi chữa.
 *
 * Tự lấp đầy khối cha, nên nơi dùng chỉ cần cho nó một chiều cao.
 */
export function DocumentPreview({
  url,
  fileName,
  source,
  pdfUrl,
}: {
  url: string
  fileName?: string
  /** Kết quả tải sẵn từ nơi gọi — để lớp phủ và nút "Tải về" dùng chung MỘT lượt tải. */
  source?: FetchState
  /**
   * Endpoint trả bản PDF do SERVER dựng, thử TRƯỚC `url`.
   *
   * `docx-preview` không bố trí được khung nổi / hộp văn bản / chia cột — thứ mà mẫu CV hiện đại dùng
   * khắp nơi — nên khối nền đè lên chữ. PDF thì trình duyệt tự dựng, trung thực tuyệt đối. Endpoint
   * hỏng hoặc môi trường chưa cài bộ chuyển đổi thì tự lùi về `url`, không ai phải biết.
   */
  pdfUrl?: string
}) {
  const { t } = useTranslation('modules/shared/documentViewer')
  const name = fileName || ''

  // Ghép origin NGAY TẠI ĐÂY chứ không phó thác cho nơi gọi. `openDocument` vốn đã ghép, nhưng khung
  // này còn được nhúng thẳng vào trang (màn hồ sơ ứng viên truyền `app.cvFileUrl` nguyên trạng) — và
  // bộ lưu trữ nội bộ trả đường dẫn TƯƠNG ĐỐI, nên ở dev nó bị phân giải theo origin của web thay vì
  // của API và khung xem rỗng. Hàm này không đụng vào URL tuyệt đối, nên ghép hai lần vẫn vô hại.
  const src = useMemo(() => resolveAssetUrl(url), [url])
  const nameKind = useMemo(() => kindOf(name, url), [name, url])

  // PDF và DOCX đều đi qua cùng một lượt tải: xem mục `useFileBlob` để biết vì sao không trỏ thẳng
  // thẻ xem vào URL gốc. Nơi gọi đã tải sẵn thì dùng lại, ảnh và file lạ thì không tải thừa.
  const wants = !source && (nameKind === 'pdf' || nameKind === 'docx')
  const own = useFileBlob(wants ? pdfUrl || src : '', wants && pdfUrl ? src : '')
  const { blob, objectUrl, loading, error, detail } = source ?? own

  // Byte thắng tên: `Content-Type` của lượt tải là thứ duy nhất không nói dối. Lúc còn đang tải thì
  // chưa có byte nào, nên tạm theo tên — nhánh nào cũng chỉ hiện vòng quay chờ.
  const kind = kindOfBlob(blob) ?? nameKind

  if (kind === 'pdf')
    return (
      <PdfRender
        blob={blob}
        objectUrl={objectUrl}
        loading={loading}
        error={error}
        detail={detail}
        name={name}
        t={t}
      />
    )

  if (kind === 'docx')
    return <DocxRender blob={blob} loading={loading} error={error} detail={detail} t={t} />

  if (kind === 'image')
    return (
      <div className="flex h-full w-full items-center justify-center overflow-auto bg-ink-100 dark:bg-ink-800 p-4">
        <img src={src} alt={name} className="max-h-full max-w-full object-contain" />
      </div>
    )

  return (
    <div className="flex h-full flex-col items-center justify-center gap-3 p-10 text-center">
      <FileText className="h-10 w-10 text-ink-400" />
      <p className="max-w-sm text-sm text-ink-600 dark:text-ink-300">
        {t('documentViewer.unsupportedFormat')}
      </p>
      <a
        href={src}
        download={downloadName(name, src)}
        target="_blank"
        rel="noreferrer"
        className="inline-flex items-center gap-1.5 rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
      >
        <Download className="h-4 w-4" /> {t('documentViewer.download')}
      </a>
    </div>
  )
}

function ViewerModal({ doc, onClose }: { doc: OpenedDoc; onClose: () => void }) {
  const { t } = useTranslation('modules/shared/documentViewer')

  // Tải MỘT lần ở đây rồi đưa xuống khung xem, để nút "Tải về" dùng lại đúng blob đó.
  //
  // Vì sao không để nguyên `<a href={url} download>`: thuộc tính `download` bị trình duyệt BỎ QUA khi
  // file ở origin khác — mà trên production mọi file đều là presigned URL của R2. Bấm "Tải về" một
  // file PDF khi đó chỉ mở thêm một tab hiện chính nó, còn DOCX thì mới tải. `blob:` là same-origin
  // với trang nên thuộc tính được tôn trọng ở mọi môi trường: xem là xem, tải là tải.
  const src = useMemo(() => resolveAssetUrl(doc.url), [doc.url])
  const kind = useMemo(() => kindOf(doc.fileName, doc.url), [doc.fileName, doc.url])
  const wants = kind === 'pdf' || kind === 'docx'
  const source = useFileBlob(wants ? doc.pdfUrl || src : '', wants && doc.pdfUrl ? src : '')

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
              {doc.fileName || t('documentViewer.defaultFileName')}
            </span>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <a
              // Khi đang xem BẢN PDF do server dựng, `source.objectUrl` là bản dựng chứ không phải
              // file gốc — tải nó về rồi đặt tên ".docx" là giao nhầm file. Lúc đó trỏ thẳng URL gốc.
              href={doc.pdfUrl ? src : source.objectUrl || src}
              download={downloadName(doc.fileName, src)}
              // Chỉ mở tab mới khi KHÔNG có blob (ảnh, định dạng lạ): lúc đó `download` bị bỏ qua ở
              // origin khác nên mở tab vẫn còn hơn không có gì. Có blob rồi thì mở tab chỉ tạo ra
              // một tab trắng bên cạnh lượt tải.
              {...(!doc.pdfUrl && source.objectUrl ? {} : { target: '_blank' as const, rel: 'noreferrer' })}
              className="inline-flex items-center gap-1.5 rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-1.5 text-xs font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
            >
              <Download className="h-3.5 w-3.5" /> {t('documentViewer.download')}
            </a>
            <button
              type="button"
              onClick={onClose}
              className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 hover:text-ink-700 dark:hover:bg-white/10 dark:hover:text-white"
              aria-label={t('documentViewer.close')}
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-hidden">
          <DocumentPreview url={doc.url} fileName={doc.fileName} source={source} />
        </div>
      </div>
    </div>,
    document.body
  )
}

/** Provider toàn cục: mount 1 lần, mọi nơi gọi useDocumentViewer().openDocument(url, name). */
export function DocumentViewerProvider({ children }: { children: ReactNode }) {
  const [doc, setDoc] = useState<OpenedDoc | null>(null)

  const openDocument = useCallback((url: string, fileName?: string, pdfUrl?: string) => {
    if (!url) return
    setDoc({ url: resolveAssetUrl(url), fileName: fileName ?? '', pdfUrl })
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
