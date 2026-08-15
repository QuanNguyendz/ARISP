#!/usr/bin/env node
/**
 * Chốt chặn i18n ở mức NAMESPACE cho cả hai site.
 *
 * Vì sao cần: ADR-058 gộp hai trang Phỏng vấn thành một component dùng chung nhưng đánh rơi toàn bộ
 * tầng i18n — 87 chuỗi viết cứng — và **không có gì báo động**, vì i18n hỏng thì im lặng: màn vẫn
 * chạy, chỉ là chọn EN vẫn ra tiếng Việt. Dấu vết duy nhất để lần ra là hai namespace bỗng không còn
 * ai dùng. Script này biến đúng dấu vết đó thành lỗi CI.
 *
 * Chỉ kiểm ở mức namespace (không kiểm từng khoá): khoá được dựng động ở nhiều nơi hợp lệ
 * (`t(state.labelKey)`, `t(`integrations.descriptions.${k}`)`) nên kiểm khoá sẽ đẻ ra báo động giả —
 * mà một cổng CI hay báo sai còn tệ hơn không có cổng nào.
 *
 * Chạy: node scripts/check-i18n-namespaces.mjs
 */
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '..')

const SITES = [
  { name: 'ARI.CandidateSite', dir: join(ROOT, 'src/ARI.CandidateSite/src') },
  { name: 'ARI.StaffSite', dir: join(ROOT, 'src/ARI.StaffSite/src') },
]
const SHARED_RESOURCES = join(ROOT, 'src/ARI.Shared/src/i18n/sharedResources.ts')

/** Mọi file .ts/.tsx của site, trừ chính thư mục i18n (nơi khai báo namespace). */
function sourceFiles(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry)
    if (statSync(p).isDirectory()) {
      if (entry !== 'i18n' && entry !== 'node_modules') sourceFiles(p, out)
    } else if (/\.tsx?$/.test(p)) {
      out.push(p)
    }
  }
  return out
}

/** Namespace khai trong bảng resources: các dòng dạng `'modules/x/y': fooVi,`. */
function registeredNamespaces(indexSource) {
  return new Set([...indexSource.matchAll(/^\s*'?([\w/.-]+)'?:\s*\w+Vi,\s*$/gm)].map((m) => m[1]))
}

const shared = new Set(
  [...readFileSync(SHARED_RESOURCES, 'utf8').matchAll(/^\s*'?([\w/.-]+)'?:\s*\w+Vi,\s*$/gm)].map(
    (m) => m[1]
  )
)

let failures = 0

for (const site of SITES) {
  const index = readFileSync(join(site.dir, 'i18n/index.ts'), 'utf8')
  const registered = registeredNamespaces(index)
  const files = sourceFiles(site.dir)

  // "Đang dùng" = tên namespace xuất hiện nguyên văn ở đâu đó ngoài thư mục i18n. Cố ý rộng rãi:
  // namespace có thể đi qua hằng số (`useTranslation(INTERVIEWS_NS)`) chứ không phải lúc nào cũng là
  // chuỗi ngay tại chỗ gọi. Mục tiêu là bắt namespace CHẾT HẲN, không phải bắt lỗi phong cách.
  const corpus = files.map((f) => readFileSync(f, 'utf8')).join('\n')
  const orphans = [...registered].filter((ns) => !corpus.includes(`'${ns}'`)).sort()

  // Chiều ngược lại thì kiểm chính xác: gọi useTranslation('x') với x chưa khai ở đâu cả = màn hình
  // sẽ hiện nguyên chuỗi khoá cho người dùng.
  const unregistered = new Map()
  for (const file of files) {
    const src = readFileSync(file, 'utf8')
    for (const m of src.matchAll(/useTranslation\(\s*'([^']+)'/g)) {
      const ns = m[1]
      if (!registered.has(ns) && !shared.has(ns)) {
        unregistered.set(ns, relative(ROOT, file))
      }
    }
  }

  if (orphans.length === 0 && unregistered.size === 0) {
    console.log(`✓ ${site.name}: ${registered.size} namespace, không có namespace mồ côi.`)
    continue
  }

  failures++
  console.error(`✗ ${site.name}:`)
  for (const ns of orphans) {
    console.error(`   - '${ns}' đã đăng ký nhưng KHÔNG màn nào dùng.`)
    console.error(`     Hoặc xoá file locale + dòng đăng ký, hoặc nối lại vào màn đang thiếu i18n.`)
  }
  for (const [ns, file] of unregistered) {
    console.error(`   - '${ns}' được dùng ở ${file} nhưng CHƯA đăng ký → màn sẽ hiện nguyên khoá.`)
  }
}

if (failures > 0) {
  console.error('\nKiểm tra i18n thất bại.')
  process.exit(1)
}
console.log('Kiểm tra i18n: đạt.')
