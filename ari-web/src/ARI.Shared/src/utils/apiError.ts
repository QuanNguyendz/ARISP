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
 * 1. **Mã nghiệp vụ** (`CODE_KEYS`) → dịch từ namespace `errors`. Câu dịch nói đúng chuyện đã xảy ra,
 *    ở cả hai ngôn ngữ — nên thắng mọi thứ khác.
 * 2. **Câu chữ server gửi, khi nó CÙNG NGÔN NGỮ với giao diện** (xem {@link serverMessageInUiLanguage}).
 *    Backend viết `message` bằng tiếng Việt, nói rõ VÌ SAO bị chặn và phải làm gì ("Ngân hàng đề mới có
 *    10 câu, chưa đủ 20…"). Giao diện đang tiếng Việt thì câu đó tốt hơn hẳn mọi câu viết sẵn.
 * 3. **Mã nhóm** (`GENERIC_CODE_KEYS`: `not_found`, `forbidden`, `conflict`…) → câu chung đã dịch.
 * 4. `fallbackKey` của màn gọi — câu viết sẵn của màn ("Không thể cập nhật trạng thái tin.").
 * 5. Câu chữ server gửi ở ngôn ngữ khác, nếu trông như câu cho người đọc (xem {@link looksHuman}).
 * 6. Câu chung theo **mã HTTP**, đã dịch. Không bao giờ để lọt "HTTP 500" ra màn hình.
 *
 * ### Vì sao bước 2 đứng trước câu viết sẵn của màn
 *
 * Bản đầu đặt câu viết sẵn của màn (nay là bước 4) TRƯỚC câu server, để giao diện tiếng Anh không lẫn
 * tiếng Việt. Nhưng gần như màn nào cũng truyền `fallbackKey`, nên câu server không bao giờ tới được màn
 * hình — kể cả khi giao diện đang tiếng Việt, nơi chẳng có gì lệch ngôn ngữ. Người dùng bấm "Gửi HM ký
 * duyệt", server trả đúng lý do, còn màn hình chỉ hiện "Không thể cập nhật trạng thái tin." Nay câu server
 * chỉ thắng khi nó cùng ngôn ngữ giao diện: tiếng Việt thấy lý do cụ thể, tiếng Anh giữ nguyên như cũ.
 * Mã NHÓM cũng xếp sau bước 2 vì cùng một lý do: "Xung đột dữ liệu" không nói được điều gì mà câu server
 * kèm theo ("Tin này chưa có Hiring Manager phụ trách. HR Admin cần gán…") không nói rõ hơn.
 */
import i18n, { defaultLanguage } from '@ari/shared/i18n/core'

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
 * Mã lỗi NGHIỆP VỤ của backend → khoá i18n trong namespace `errors`.
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

  // Cổng rời bản nháp của tin (UpdateJobStatusCommand)
  online_test_bank_insufficient: 'job.onlineTestBankInsufficient',
  cv_rubric_required: 'job.cvRubricRequired',
  // Cổng lên job board: vòng phỏng vấn chưa có bộ tiêu chí chấm (ADR-073)
  interview_rubric_required: 'job.interviewRubricRequired',

  // Đổi thời lượng bài thi khi còn ca thi chưa đóng (OnlineTestWindow — ADR-072)
  online_test_duration_locked: 'job.onlineTestDurationLocked',
}

/**
 * Mã NHÓM — chỉ nói loại lỗi, không nói chuyện gì đã xảy ra. Tầm thông tin ngang câu chung theo mã HTTP,
 * nên xếp SAU câu server cùng ngôn ngữ giao diện (xem thứ tự ở đầu file).
 */
const GENERIC_CODE_KEYS: Record<string, string> = {
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

/** Ngôn ngữ backend viết `message` — mọi câu `Result.Failure` phía .NET đều bằng tiếng Việt. */
const SERVER_LANGUAGE = 'vi'

/** Chữ cái chỉ tiếng Việt mới có. Câu tiếng Anh (kể cả câu còn sót ở backend) không bao giờ chứa. */
const VIETNAMESE_LETTER =
  /[àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ]/i

function uiLanguage(): string {
  return (i18n.language || defaultLanguage).slice(0, 2).toLowerCase()
}

/**
 * Câu server gửi, NẾU nó đọc được và cùng ngôn ngữ với giao diện — không thì `undefined`.
 *
 * - Chỉ lấy từ thân response thật (`response.data.message`): `Error.message` của axios ("Network Error",
 *   "timeout of 30000ms exceeded") cũng trông như câu nhưng không phải lời server.
 * - Bỏ qua 401: đó là chuyện phiên đăng nhập, câu server ở đó viết cho lập trình viên ("…gửi Bearer
 *   token"); "Phiên đăng nhập đã hết hạn" mới là điều người dùng cần biết.
 * - Câu phải thật sự là tiếng Việt (có chữ chỉ tiếng Việt mới có): câu tiếng Anh còn sót ở backend
 *   không được chen vào giao diện tiếng Việt, nó rơi xuống câu viết sẵn của màn như trước.
 */
function serverMessageInUiLanguage(error: unknown): string | undefined {
  if (uiLanguage() !== SERVER_LANGUAGE) return undefined
  if (apiErrorStatus(error) === 401) return undefined

  const message = body(error)?.message
  if (!looksHuman(message)) return undefined
  return VIETNAMESE_LETTER.test(message.normalize('NFC')) ? message.trim() : undefined
}

/**
 * Câu hiển thị cho người dùng — thứ tự quyết định ở đầu file.
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

  const inUiLanguage = serverMessageInUiLanguage(error)
  if (inUiLanguage) return inUiLanguage

  if (code && GENERIC_CODE_KEYS[code]) return translate(GENERIC_CODE_KEYS[code])

  if (fallbackKey) {
    const translated = t(fallbackKey)
    // i18next trả về chính khoá khi không tìm thấy — đừng in khoá ra màn hình.
    if (translated && translated !== fallbackKey) return translated
  }

  // Câu server ở ngôn ngữ khác, hoặc lời của Error do authService dựng. Hai thứ KHÔNG phải lời server bị
  // loại: chữ của tầng axios khi không có response ("Network Error", "timeout of 30000ms exceeded" — tiếng
  // Anh kỹ thuật, dưới kia đã có câu "Lỗi mạng" đã dịch), và 401 (xem serverMessageInUiLanguage).
  const status = apiErrorStatus(error)
  if (status !== 401) {
    const isAxiosTransport = (error as { isAxiosError?: unknown } | null)?.isAxiosError === true
    const serverMessage =
      body(error)?.message ?? (isAxiosTransport ? undefined : (error as { message?: unknown } | null)?.message)
    if (looksHuman(serverMessage)) return serverMessage
  }

  if (status && STATUS_KEYS[status]) return translate(STATUS_KEYS[status])
  if (status === undefined) return translate('server.network') // không có response = hỏng đường truyền

  return translate('server.internal')
}
