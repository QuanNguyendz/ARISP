/**
 * Đọc lỗi từ API và trả về MỘT câu người dùng đọc được, đúng ngôn ngữ giao diện đang chọn.
 *
 * ## Vì sao cần một chỗ dùng chung
 *
 * Trước đây mỗi màn tự viết `e?.response?.data?.message || t('errors.xxx')` (hơn 80 chỗ). Hệ quả:
 *
 * - Thân lỗi 500 của server serialize ra `Message` hoa đầu, không khớp `message` → mọi màn rơi về
 *   chuỗi dự phòng của axios/fetch và người dùng nhìn thấy đúng hai chữ **"HTTP 500"**.
 * - Câu chữ lỗi được viết cứng tiếng Việt ở backend, nên giao diện đang để tiếng Anh vẫn hiện tiếng
 *   Việt — và ngược lại với vài message còn sót tiếng Anh (`"Account not provisioned."`).
 * - Vài chỗ in thẳng mã kỹ thuật (`external_authentication_failed`) ra màn hình.
 *
 * ## Thứ tự quyết định
 *
 * 1. **Mã lỗi** (`code`) có trong bảng `CODE_KEYS` → dịch từ namespace `errors`. Đây là đường DUY
 *    NHẤT đổi được ngôn ngữ, nên mã luôn thắng câu chữ server gửi kèm.
 * 2. `fallbackKey` của màn gọi — câu hợp cảnh nhất khi mã chưa được phân loại.
 * 3. **Câu chữ server gửi** nếu nó trông như câu cho người đọc (xem {@link looksHuman}). Backend
 *    ARISP viết phần lớn message bằng tiếng Việt đầy đủ, nên bỏ đi là mất thông tin cụ thể —
 *    nhưng chỉ nhận khi nó thật sự là câu, không phải mã hay tên biến.
 * 4. Câu chung theo **mã HTTP**, đã dịch. Không bao giờ để lọt "HTTP 500" ra màn hình.
 */

/**
 * Hàm dịch, khai theo HÌNH DẠNG chứ không lấy `TFunction` của i18next.
 *
 * Nhiều component trong dự án nhận `t` qua props với kiểu tự khai gọn hơn (`(key: string) => string`),
 * và `TFunction` có chồng nhiều overload nên không nhận được các hàm đó. Khai tối thiểu thì cả `t`
 * gốc của `useTranslation` lẫn `t` truyền qua props đều dùng được — đúng yêu cầu ở đây: đưa vào một
 * khoá, nhận lại một câu.
 */
export type TranslateFn = (key: string, options?: Record<string, unknown>) => string

/** Thân lỗi chuẩn mà API ARISP trả về. */
interface ApiErrorBody {
  message?: unknown
  code?: unknown
  statusCode?: unknown
}

/**
 * Mã lỗi backend → khoá i18n trong namespace `errors`.
 *
 * Chỉ khai mã đã có câu dịch ở CẢ HAI ngôn ngữ. Mã thiếu ở đây không phải lỗi: nó rơi xuống bước
 * sau và vẫn ra một câu đọc được — bảng này lớn dần theo việc backend gán mã cho từng lỗi.
 */
const CODE_KEYS: Record<string, string> = {
  // Xác thực (AuthErrorCodes phía .NET)
  invalid_credentials: 'auth.invalidCredentials',
  account_disabled: 'auth.accountLocked',
  passwordless_google: 'auth.oauthFailed',
  sso_only: 'auth.oauthFailed',
  email_not_verified: 'auth.emailNotVerified',
  pending_approval: 'auth.accountPending',
  account_not_provisioned: 'auth.accountNotFound',
  domain_not_allowed: 'auth.domainNotAllowed',
  external_authentication_failed: 'auth.oauthFailed',
  no_email_from_provider: 'auth.oauthFailed',

  // Chung (CommonErrorCodes phía .NET)
  not_found: 'server.notFound',
  forbidden: 'server.forbidden',
  unauthorized: 'server.unauthorized',
  conflict: 'server.conflict',
  validation_error: 'server.badRequest',

  // Lưới cuối của ErrorHandlingMiddleware
  internal_error: 'server.internal',
}

/** Câu chung theo mã HTTP khi không còn gì cụ thể hơn để nói. */
const STATUS_KEYS: Record<number, string> = {
  400: 'server.badRequest',
  401: 'auth.sessionExpired',
  403: 'server.forbidden',
  404: 'server.notFound',
  408: 'server.timeout',
  409: 'server.conflict',
}

/**
 * Chuỗi này có phải câu viết cho người đọc không.
 *
 * Loại ra đúng ba thứ đã từng lọt ra màn hình: mã kỹ thuật (`domain_not_allowed`), chuỗi dự phòng
 * của tầng fetch (`HTTP 500`, `Request failed with status code 500`), và mẩu quá ngắn để hiểu.
 */
function looksHuman(value: unknown): value is string {
  if (typeof value !== 'string') return false
  const text = value.trim()
  if (text.length < 12) return false
  if (/^HTTP\s+\d+$/i.test(text)) return false
  if (/^Request failed with status code/i.test(text)) return false
  if (/^[a-z0-9]+(_[a-z0-9]+)+$/.test(text)) return false // snake_case thuần = mã, không phải câu
  return text.includes(' ')
}

function body(error: unknown): ApiErrorBody | undefined {
  if (typeof error !== 'object' || error === null) return undefined
  const response = (error as { response?: { data?: unknown } }).response
  const data = response?.data
  return typeof data === 'object' && data !== null ? (data as ApiErrorBody) : undefined
}

/** Mã lỗi backend, đọc từ thân response hoặc từ chính Error do `authService` dựng. */
export function apiErrorCode(error: unknown): string | undefined {
  const fromBody = body(error)?.code
  if (typeof fromBody === 'string' && fromBody.length > 0) return fromBody

  const direct = (error as { code?: unknown } | null)?.code
  return typeof direct === 'string' && direct.length > 0 ? direct : undefined
}

/** Mã HTTP của response lỗi, nếu có. */
export function apiErrorStatus(error: unknown): number | undefined {
  if (typeof error !== 'object' || error === null) return undefined
  const status = (error as { response?: { status?: unknown } }).response?.status
  if (typeof status === 'number') return status

  const inBody = body(error)?.statusCode
  return typeof inBody === 'number' ? inBody : undefined
}

/**
 * Câu hiển thị cho người dùng.
 *
 * @param t   hàm dịch của màn gọi (`useTranslation('modules/...')`). Câu chung luôn được tra trong
 *            namespace `errors` qua tuỳ chọn `ns`, nên màn gọi không phải nạp thêm namespace nào.
 * @param fallbackKey khoá i18n hợp cảnh của màn gọi, ví dụ `t('errors.saveFailed')` của chính màn đó.
 */
export function resolveApiError(
  error: unknown,
  t: TranslateFn,
  fallbackKey?: string
): string {
  const translate = (key: string) => t(key, { ns: 'errors' })

  const code = apiErrorCode(error)
  if (code && CODE_KEYS[code]) return translate(CODE_KEYS[code])

  if (fallbackKey) {
    const translated = t(fallbackKey)
    // i18next trả về chính khoá khi không tìm thấy — đừng in khoá ra màn hình.
    if (translated && translated !== fallbackKey) return translated
  }

  const serverMessage = body(error)?.message ?? (error as { message?: unknown } | null)?.message
  if (looksHuman(serverMessage)) return serverMessage

  const status = apiErrorStatus(error)
  if (status && STATUS_KEYS[status]) return translate(STATUS_KEYS[status])
  if (status === undefined) return translate('server.network') // không có response = hỏng đường truyền

  return translate('server.internal')
}
