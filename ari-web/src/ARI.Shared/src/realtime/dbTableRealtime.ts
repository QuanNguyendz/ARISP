import { useEffect, useRef } from 'react'

/**
 * Sự kiện DOM "một bảng vừa đổi" (ADR-057) — lối vào realtime cho những màn còn giữ state cục bộ
 * (không qua react-query), nên `invalidateQueries` không chạm tới được.
 *
 * `useAppNotifications` phát sự kiện này cho MỌI thay đổi server gửi xuống, trước khi rẽ nhánh theo
 * bảng. Nhờ vậy màn mới chỉ cần gọi {@link useDbTableChanged} với tên bảng của mình — không phải
 * thêm nhánh vào `handleDbChange`. Trước đây thiếu đúng lối vào này: server định tuyến
 * `recruitment_requests`, `jd_templates`, `departments`… từ lâu, nhưng các màn đó không có cách nào
 * nghe, nên người dùng phải F5 mới thấy phiếu vừa được duyệt.
 */
export const DB_TABLE_CHANGED_EVENT = 'db-table:changed'

/** Bảng giả của tín hiệu `resync`: listener vừa nối lại DB, mọi màn phải coi như bảng của mình đã đổi. */
export const RESYNC_TABLE = '*'

export interface DbTableChangedDetail {
  /** Tên bảng, ví dụ `recruitment_requests`; {@link RESYNC_TABLE} = không rõ bảng nào. */
  table: string
  /** I = thêm, U = sửa, D = xoá, S = đổi hàng loạt, `resync`. */
  op?: string
  /** Khoá của dòng vừa đổi (không có với `S` và `resync`). */
  id?: string | null
}

export function publishDbTableChanged(
  detail: DbTableChangedDetail,
  target: EventTarget | null = globalThis.window ?? null
): void {
  target?.dispatchEvent(new CustomEvent<DbTableChangedDetail>(DB_TABLE_CHANGED_EVENT, { detail }))
}

/**
 * Nghe thay đổi của các bảng cho trước. `resync` luôn lọt qua — bỏ sót một lần nạp lại tệ hơn nạp
 * thừa một lần.
 *
 * Gom các thay đổi đến sát nhau thành MỘT lần gọi `handler`: một thao tác nghiệp vụ thường ghi vài
 * dòng liền nhau (duyệt phiếu = sửa phiếu + tạo thông báo), không gom thì mỗi dòng là một lượt tải
 * lại nối đuôi. Trả về hàm huỷ — gọi trong cleanup của `useEffect`.
 */
export function onDbTableChanged(
  tables: readonly string[],
  handler: (changes: DbTableChangedDetail[]) => void,
  options: { delayMs?: number; target?: EventTarget | null } = {}
): () => void {
  const target = options.target === undefined ? globalThis.window ?? null : options.target
  if (!target) return () => {}

  const delayMs = options.delayMs ?? 250
  const wanted = new Set(tables)
  let pending: DbTableChangedDetail[] = []
  let timer: ReturnType<typeof setTimeout> | null = null

  const flush = () => {
    timer = null
    const changes = pending
    pending = []
    if (changes.length > 0) handler(changes)
  }

  const listener = (event: Event) => {
    const detail = (event as CustomEvent<DbTableChangedDetail>).detail
    // Sự kiện không mang chi tiết là sự kiện không rõ phạm vi — xử như resync.
    const change = detail ?? { table: RESYNC_TABLE }
    if (change.table !== RESYNC_TABLE && !wanted.has(change.table)) return

    pending.push(change)
    if (timer) clearTimeout(timer)
    timer = setTimeout(flush, delayMs)
  }

  target.addEventListener(DB_TABLE_CHANGED_EVENT, listener)
  return () => {
    target.removeEventListener(DB_TABLE_CHANGED_EVENT, listener)
    if (timer) clearTimeout(timer)
    timer = null
    pending = []
  }
}

/**
 * Bản hook của {@link onDbTableChanged}. `handler` luôn là bản mới nhất của lần render gần nhất,
 * nên nó đọc được state hiện tại (có thay đổi chưa lưu không, có đang lưu không) mà không phải
 * đăng ký lại mỗi lần render.
 *
 * Màn có FORM phải tự kiểm "còn thay đổi chưa lưu" trước khi nạp lại — nạp đè lên chữ người dùng
 * đang gõ là mất dữ liệu, tệ hơn nhiều so với hiện số liệu cũ.
 */
export function useDbTableChanged(
  tables: readonly string[],
  handler: (changes: DbTableChangedDetail[]) => void
): void {
  const handlerRef = useRef(handler)
  handlerRef.current = handler

  // Mảng bảng thường là literal viết ngay trong lời gọi — so theo nội dung để không đăng ký lại mỗi render.
  const key = tables.join('|')

  useEffect(() => {
    return onDbTableChanged(key ? key.split('|') : [], (changes) => handlerRef.current(changes))
  }, [key])
}

/** Có thay đổi nào của đúng dòng này (hoặc một `resync`) trong đợt không. */
export function touchesRow(
  changes: readonly DbTableChangedDetail[],
  table: string,
  id: string | null | undefined
): boolean {
  return changes.some(
    (c) => c.table === RESYNC_TABLE || (c.table === table && (!id || !c.id || c.id === id))
  )
}
