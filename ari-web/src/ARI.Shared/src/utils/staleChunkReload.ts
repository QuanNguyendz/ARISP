/**
 * Tự phục hồi khi tab đang mở gặp chunk đã bị xoá sau deploy.
 *
 * Vite băm hash vào tên file chunk, mỗi lần deploy sinh tên mới và image mới
 * KHÔNG còn file cũ. Tab người dùng mở từ trước deploy vẫn giữ `index-*.js` cũ,
 * nên khi bấm sang một route lazy-load, `import()` xin đúng tên chunk cũ đã biến
 * mất → route không bao giờ mở được, app đứng im cho tới khi người dùng tự F5.
 *
 * Ở đây bắt lỗi đó và reload đúng MỘT lần để lấy `index.html` mới (kèm bảng tên
 * chunk mới). Cooldown qua sessionStorage để nếu asset mất thật (lỗi build /
 * deploy hỏng) thì không rơi vào vòng lặp reload vô tận.
 */

const RELOAD_MARK_KEY = 'arisp:stale-chunk-reload-at';
const RELOAD_COOLDOWN_MS = 15_000;

/**
 * Thông điệp khác nhau giữa các trình duyệt cho cùng một tình huống:
 * - Chrome/Edge: "Failed to fetch dynamically imported module: <url>"
 * - Firefox: "error loading dynamically imported module"
 * - Safari: "Importing a module script failed."
 * - Khi nginx trả index.html (text/html) thay cho .js: lỗi strict MIME type.
 */
function looksLikeStaleChunk(reason: unknown): boolean {
  const message = reason instanceof Error ? reason.message : String(reason ?? '');
  return (
    message.includes('Failed to fetch dynamically imported module') ||
    message.includes('error loading dynamically imported module') ||
    message.includes('Importing a module script failed') ||
    (message.includes('MIME type') && message.includes('module'))
  );
}

function reloadOnce(): boolean {
  let lastReloadAt = 0;
  try {
    lastReloadAt = Number(window.sessionStorage.getItem(RELOAD_MARK_KEY)) || 0;
  } catch {
    // sessionStorage bị chặn (private mode / cookie policy) — coi như chưa reload
    // lần nào. Mất cooldown thì vẫn hơn là không tự phục hồi được.
  }

  if (Date.now() - lastReloadAt < RELOAD_COOLDOWN_MS) return false;

  try {
    window.sessionStorage.setItem(RELOAD_MARK_KEY, String(Date.now()));
  } catch {
    /* bỏ qua */
  }

  window.location.reload();
  return true;
}

export function installStaleChunkReload(): void {
  if (typeof window === 'undefined') return;

  // Vite bắn sự kiện này khi `__vitePreload` không tải được chunk. preventDefault
  // để Vite không ném tiếp lỗi ra console — ta đã xử lý bằng reload.
  window.addEventListener('vite:preloadError', ((event: Event) => {
    if (reloadOnce()) event.preventDefault();
  }) as EventListener);

  // Lưới an toàn: React.lazy nuốt lỗi vào promise, và không phải đường dẫn nào
  // cũng đi qua __vitePreload.
  window.addEventListener('unhandledrejection', (event) => {
    if (looksLikeStaleChunk(event.reason) && reloadOnce()) event.preventDefault();
  });
}
