import { useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import type { JdTemplate } from '@ari/shared/fservices/jdTemplate'
import type { JdDocument } from '@ari/shared/fservices/jdDocument'
import {
  EMPLOYMENT_TYPES,
  WORK_MODES,
  EXPERIENCE_LEVELS,
  jobOptionLabel,
} from '@ari/shared/utils/jobOptions'

/**
 * Xem trước bản mô tả công việc dưới dạng **tờ giấy A4**, mỗi lần một trang, có số trang và nút
 * chuyển trang (ADR-064).
 *
 * Vì sao phải phân trang ở client: file thật do server dựng và PdfSharpCore tự ngắt trang, nhưng
 * người dùng cần thấy **ngay lúc gõ** là nội dung có tràn sang trang hai không. Bản xem trước đo
 * chiều cao thật của từng khối rồi xếp vào các trang cao đúng bằng vùng nội dung A4.
 *
 * **Đây là xấp xỉ, không phải bản in chính xác từng pixel** — phông của trình duyệt khác phông
 * server dùng, nên vị trí ngắt trang có thể lệch một hai dòng. Nó đủ để trả lời "bố cục có đúng
 * không, JD dài mấy trang"; muốn chắc chắn thì mở file PDF thật.
 *
 * Bố cục bám khuôn JD doanh nghiệp: logo + tên công ty cùng hàng → tiêu đề canh giữa có đường kẻ →
 * bảng thông tin → **tiêu đề mục là thanh màu đặc chữ trắng** → gạch đầu dòng.
 */

/** A4 ở 96dpi. Đo ở kích thước thật rồi mới thu nhỏ bằng CSS transform — thu trước thì số đo sai. */
const PAGE_W = 794
const PAGE_H = 1123
const PAGE_PAD = 56

const CONTENT_W = PAGE_W - PAGE_PAD * 2
const CONTENT_H = PAGE_H - PAGE_PAD * 2

interface Props {
  template: JdTemplate
  /** Nội dung thật. Bỏ trống thì dựng nội dung mẫu từ gợi ý của từng mục (dùng ở màn cấu hình mẫu). */
  doc?: JdDocument | null
  /** Bề ngang khung chứa, px. Tờ giấy tự thu nhỏ cho vừa. */
  containerWidth?: number
}

export default function JdPaperPreview({ template, doc, containerWidth }: Props) {
  const { t } = useTranslation('modules/staff/jdComposer')
  const measureRef = useRef<HTMLDivElement>(null)
  const [pages, setPages] = useState<number[][]>([])
  const [page, setPage] = useState(0)

  const blocks = useMemo(() => buildBlocks(template, doc, t), [template, doc, t])

  // Đo chiều cao thật của từng khối rồi xếp vào trang. Chạy trong useLayoutEffect để người dùng
  // không kịp thấy một khung trắng nhấp nháy trước khi phân trang xong.
  useLayoutEffect(() => {
    const container = measureRef.current
    if (!container) return

    const heights = Array.from(container.children).map((el) => (el as HTMLElement).offsetHeight)

    const result: number[][] = []
    let current: number[] = []
    let used = 0

    heights.forEach((h, i) => {
      // Khối cao hơn cả trang thì vẫn phải nằm ở đâu đó — cho nó một trang riêng thay vì lặp vô hạn.
      if (current.length > 0 && used + h > CONTENT_H) {
        result.push(current)
        current = []
        used = 0
      }
      current.push(i)
      used += h
    })
    if (current.length > 0) result.push(current)

    setPages(result.length > 0 ? result : [[]])
    setPage((p) => Math.min(p, Math.max(result.length - 1, 0)))
  }, [blocks])

  // Chưa đo được bề ngang thì CHƯA vẽ tờ giấy. Nếu để mặc định scale = 1, tờ A4 rộng 794px nằm
  // trong một cột hẹp có `overflow: hidden` → chỉ thấy dải bên trái, còn tiêu đề canh giữa rơi ra
  // ngoài vùng nhìn thấy và trông như bị lệch phải.
  const measured = containerWidth != null && containerWidth > 0
  const scale = measured ? Math.min(1, containerWidth / PAGE_W) : 1

  // `pages` giữ CHỈ SỐ vào `blocks`. Sửa mẫu (xoá chân trang, tắt một mục) làm `blocks` ngắn đi, mà
  // lần render ngay sau đó vẫn dùng `pages` cũ — `useLayoutEffect` chạy SAU render. Lọc bỏ chỉ số
  // đã lạc thay vì để `blocks[i].key` ném lỗi; một khung hình sau là effect xếp lại trang, và vì
  // effect chạy trước khi trình duyệt vẽ nên người dùng không thấy nhấp nháy.
  const visible = (pages[page] ?? []).map((i) => blocks[i]).filter(Boolean)

  return (
    <div>
      {/*
        Khung đo: cùng bề ngang vùng nội dung, ẩn khỏi mắt nhưng VẪN có layout để đo được.

        `visibility: hidden` chứ không phải `display: none` — `display:none` cho `offsetHeight = 0`.

        `position: fixed` chứ không phải `absolute`: bản đầu dùng `absolute`, mà lớp phủ xem trước
        lại là `position: fixed` (tức một ancestor CÓ vị trí), nên khung đo cao cả nghìn pixel này
        neo vào lớp phủ và kéo dài vùng cuộn của nó — cuộn xuống là thấy một mảng trắng dài đúng
        bằng chiều cao toàn bộ nội dung. `fixed` đưa nó ra khỏi luồng cuộn hẳn.
      */}
      <div
        aria-hidden
        ref={measureRef}
        style={{
          width: CONTENT_W,
          position: 'fixed',
          top: 0,
          left: -99999,
          visibility: 'hidden',
          pointerEvents: 'none',
        }}
      >
        {blocks.map((b) => (
          <div key={b.key}>{b.node}</div>
        ))}
      </div>

      {/* Tờ giấy */}
      <div style={{ height: PAGE_H * scale, overflow: 'hidden', visibility: measured ? 'visible' : 'hidden' }}>
        <div
          className="bg-white shadow-lg ring-1 ring-ink-200 dark:ring-white/10"
          style={{
            width: PAGE_W,
            height: PAGE_H,
            padding: PAGE_PAD,
            transform: `scale(${scale})`,
            transformOrigin: 'top left',
            fontFamily: `${template.fontFamily}, system-ui, sans-serif`,
            color: '#111827',
          }}
        >
          {visible.map((b) => (
            <div key={b.key}>{b.node}</div>
          ))}
        </div>
      </div>

      {/* Số trang + chuyển trang.
          Đặt trên MẶT NỀN RIÊNG (viên thuốc trắng có viền) chứ không để trong suốt: component này
          xuất hiện ở hai chỗ có nền khác hẳn nhau — thẻ trắng ở màn Mẫu JD, và lớp phủ tối mờ ở
          trình soạn. Chữ xám trên nền tối mờ gần như không đọc được. Có mặt nền riêng thì nó đọc
          được ở cả hai chỗ mà không cần biết đang nằm trên nền gì. */}
      <div className="mt-3 flex justify-center">
        <div className="inline-flex items-center gap-1 rounded-full border border-ink-200 bg-white px-1.5 py-1 shadow-card dark:border-white/10 dark:bg-ink-900">
          <button
            type="button"
            onClick={() => setPage((p) => Math.max(0, p - 1))}
            disabled={page === 0}
            className="grid h-7 w-7 place-items-center rounded-full text-ink-600 hover:bg-ink-100 disabled:opacity-30 disabled:hover:bg-transparent dark:text-ink-300 dark:hover:bg-white/10"
            aria-label={t('preview.prevPage')}
          >
            <ChevronLeft className="h-4 w-4" />
          </button>

          <span className="px-1.5 text-xs font-medium text-ink-700 dark:text-ink-200">
            {t('preview.pageOf', { page: page + 1, total: Math.max(pages.length, 1) })}
          </span>

          <button
            type="button"
            onClick={() => setPage((p) => Math.min(pages.length - 1, p + 1))}
            disabled={page >= pages.length - 1}
            className="grid h-7 w-7 place-items-center rounded-full text-ink-600 hover:bg-ink-100 disabled:opacity-30 disabled:hover:bg-transparent dark:text-ink-300 dark:hover:bg-white/10"
            aria-label={t('preview.nextPage')}
          >
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  )
}

// ===================== dựng các khối =====================

interface Block {
  key: string
  node: ReactNode
}

function buildBlocks(
  template: JdTemplate,
  doc: JdDocument | null | undefined,
  t: (key: string, opts?: Record<string, unknown>) => string
): Block[] {
  const accent = template.accentColor || '#4F46E5'
  const blocks: Block[] = []

  // ---- Đầu trang: logo + tên công ty CÙNG HÀNG ----
  blocks.push({
    key: 'header',
    node: (
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 18 }}>
        {template.logoUrl && (
          <img src={template.logoUrl} alt="" style={{ height: 30, objectFit: 'contain' }} />
        )}
        <div>
          <div style={{ fontSize: 13, fontWeight: 700, color: accent }}>
            {template.companyName || t('preview.companyPlaceholder')}
          </div>
          {[template.companyAddress, template.companyWebsite, template.companyEmail]
            .filter(Boolean)
            .map((line) => (
              <div key={line as string} style={{ fontSize: 9, color: '#6B7280' }}>
                {line}
              </div>
            ))}
        </div>
      </div>
    ),
  })

  // ---- Tiêu đề văn bản, canh giữa + đường kẻ ----
  blocks.push({
    key: 'doc-title',
    node: (
      <div style={{ marginBottom: 14 }}>
        <div style={{ textAlign: 'center', fontSize: 14, fontWeight: 700, letterSpacing: 0.3 }}>
          {template.documentTitle || 'THÔNG TIN TUYỂN DỤNG'}
        </div>
        <div style={{ height: 2, backgroundColor: accent, marginTop: 8 }} />
      </div>
    ),
  })

  // ---- Bảng thông tin ----
  for (const fact of buildFacts(doc, t)) {
    blocks.push({
      key: `fact-${fact.label}`,
      node: (
        <div style={{ display: 'flex', fontSize: 11, lineHeight: '18px' }}>
          <span style={{ width: 110, flexShrink: 0 }}>{fact.label}</span>
          <span style={{ fontWeight: 700 }}>: {fact.value}</span>
        </div>
      ),
    })
  }

  // ---- Từng mục: thanh màu đặc + gạch đầu dòng ----
  const sections = (template.sections ?? []).filter((s) => s.enabled)
  for (const section of sections) {
    const raw = doc ? (doc.sections[section.key] ?? '') : (section.hint ?? '')
    const lines = raw
      .split('\n')
      .map((l) => l.trim().replace(/^[-•*]\s*/, ''))
      .filter(Boolean)

    // Mục bật mà chưa viết gì thì KHÔNG in tiêu đề trống — giống hệt renderer phía server.
    if (lines.length === 0) continue

    // Thanh tiêu đề gộp cùng dòng nội dung đầu tiên thành MỘT khối, để phân trang không bao giờ
    // đẩy tiêu đề mục đứng trơ trọi cuối trang.
    blocks.push({
      key: `sec-${section.key}`,
      node: (
        <div style={{ marginTop: 16 }}>
          <div
            style={{
              backgroundColor: accent,
              color: '#fff',
              fontSize: 10.5,
              fontWeight: 700,
              letterSpacing: 0.4,
              padding: '4px 8px',
              textTransform: 'uppercase',
            }}
          >
            {section.title}
          </div>
          <div style={{ fontSize: 11, lineHeight: '18px', paddingLeft: 16, marginTop: 6 }}>
            • {lines[0]}
          </div>
        </div>
      ),
    })

    lines.slice(1).forEach((line, i) => {
      blocks.push({
        key: `sec-${section.key}-${i}`,
        node: (
          <div style={{ fontSize: 11, lineHeight: '18px', paddingLeft: 16 }}>• {line}</div>
        ),
      })
    })
  }

  if (template.footerNote) {
    blocks.push({
      key: 'footer',
      node: (
        <div style={{ marginTop: 20 }}>
          <div style={{ height: 1, backgroundColor: '#E5E7EB', marginBottom: 6 }} />
          <div style={{ fontSize: 9, color: '#9CA3AF' }}>{template.footerNote}</div>
        </div>
      ),
    })
  }

  return blocks
}

/** Cùng thứ tự và cùng quy tắc bỏ trường trống với `JdLayout` phía server. */
function buildFacts(
  doc: JdDocument | null | undefined,
  t: (key: string, opts?: Record<string, unknown>) => string
): { label: string; value: string }[] {
  const out: { label: string; value: string }[] = []
  const add = (label: string, value?: string | number | null) => {
    if (value === null || value === undefined || `${value}`.trim() === '') return
    out.push({ label, value: `${value}`.trim() })
  }

  if (!doc) {
    add(t('preview.factPosition'), t('preview.samplePosition'))
    add(t('preview.factVacancies'), 15)
    add(t('preview.factTime'), jobOptionLabel(EMPLOYMENT_TYPES, 'full_time'))
    add(t('preview.factLocation'), 'Hà Nội')
    return out
  }

  add(t('preview.factPosition'), doc.title)
  if (doc.vacancies && doc.vacancies > 0) add(t('preview.factVacancies'), doc.vacancies)
  // In NHÃN chứ không in khoá: bản xem trước phải giống hệt file server dựng ra, mà server đã ánh
  // xạ qua `JdLayout.DisplayLabel`. Truyền thẳng `doc.employmentType` thì người dùng thấy `full_time`
  // ở đây nhưng "Toàn thời gian" trên biểu mẫu — hai màn nói hai thứ khác nhau về cùng một ô.
  // Bảng nhãn ở `@ari/shared/utils/jobOptions` khớp từng chữ với bảng bên server.
  add(t('preview.factTime'), jobOptionLabel(EMPLOYMENT_TYPES, doc.employmentType))
  add(t('preview.factLocation'), doc.location)
  add(t('preview.factDepartment'), doc.department)
  add(t('preview.factLevel'), jobOptionLabel(EXPERIENCE_LEVELS, doc.experienceLevel))
  add(t('preview.factWorkMode'), jobOptionLabel(WORK_MODES, doc.workMode))
  add(t('preview.factSalary'), formatSalary(doc.salaryMin, doc.salaryMax, doc.salaryCurrency))
  return out
}

/** Dấu chấm phân nhóm như bản server — `NumberFormatInfo` tường minh, không phụ thuộc locale máy. */
function formatSalary(
  min: number | null | undefined,
  max: number | null | undefined,
  currency: string | null | undefined
): string {
  if (min == null && max == null) return ''
  const unit = currency || 'VND'
  const n = (v: number) => v.toLocaleString('vi-VN')
  if (min != null && max != null) return `${n(min)} – ${n(max)} ${unit}`
  return `${n((min ?? max) as number)} ${unit}`
}
