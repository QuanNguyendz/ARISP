#!/usr/bin/env node
// PostToolUse(Write|Edit): tự format file frontend bằng Prettier (đã cài trong frontend/).
// Không chặn, lỗi format được nuốt để không cản trở.

import { readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { resolve, dirname } from "node:path";
import { existsSync } from "node:fs";

function readStdin() {
  try {
    return readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

let payload = {};
try {
  payload = JSON.parse(readStdin() || "{}");
} catch {
  process.exit(0);
}

const input = payload.tool_input || {};
const filePath = input.file_path || payload?.tool_response?.filePath;
if (!filePath) process.exit(0);

const norm = filePath.replace(/\\/g, "/");
if (!/frontend\//.test(norm)) process.exit(0); // chỉ FE
if (!/\.(ts|tsx|js|jsx|json|css|scss|html|md)$/.test(norm)) process.exit(0);

// Tìm thư mục frontend để chạy prettier với cấu hình & binary cục bộ.
const idx = norm.toLowerCase().indexOf("frontend/");
const repoStyle = norm.slice(0, idx + "frontend".length);
const feDir = existsSync(repoStyle) ? repoStyle : dirname(filePath);

// Chạy prettier.cjs bằng chính node để tránh lỗi EINVAL khi spawn .cmd trên Windows.
const prettierCjs = resolve(feDir, "node_modules/prettier/bin/prettier.cjs");
if (!existsSync(prettierCjs)) process.exit(0);

try {
  execFileSync(process.execPath, [prettierCjs, "--write", filePath], {
    cwd: feDir,
    stdio: "ignore",
  });
} catch {
  // im lặng — format thất bại không nên cản công việc
}
process.exit(0);
