#!/usr/bin/env node
// PreToolUse(Bash): chặn lệnh git nguy hiểm trên nhánh được bảo vệ.
// - force-push vào main/develop
// - bỏ qua hook khi commit/push (--no-verify)

import { readFileSync } from "node:fs";

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

const cmd = (payload.tool_input?.command || "").toString();

// Loại bỏ nội dung trong chuỗi trích dẫn (vd commit message) để không bắt nhầm
// flag xuất hiện trong text. Chỉ kiểm tra phần "lệnh" thực sự.
const scan = cmd.replace(/'[^']*'/g, "''").replace(/"[^"]*"/g, '""');

function deny(reason) {
  process.stdout.write(
    JSON.stringify({
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: "deny",
        permissionDecisionReason: reason,
      },
    })
  );
  process.exit(0);
}

const isPush = /\bgit\s+push\b/.test(scan);
const isForce = /(--force\b|--force-with-lease\b|\s-f\b)/.test(scan);
const hitsProtected = /\b(main|develop)\b/.test(scan);

if (isPush && isForce && hitsProtected) {
  deny("Từ chối force-push vào main/develop. Dùng nhánh feature/* hoặc fix/* rồi mở PR (CLAUDE.md - Git).");
}
if (/--no-verify\b/.test(scan)) {
  deny("Từ chối --no-verify: không bỏ qua git hooks. Hãy sửa nguyên nhân hook fail.");
}

process.exit(0);
