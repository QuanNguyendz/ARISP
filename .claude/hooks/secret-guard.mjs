#!/usr/bin/env node
// PreToolUse(Write|Edit): chặn việc ghi secret hardcode vào source.
// Thực thi CLAUDE.md rule #2 "Không hardcode secrets".
// Đọc hook input (JSON) từ stdin, trả permissionDecision "deny" nếu phát hiện.

import { readFileSync } from "node:fs";

function readStdin() {
  try {
    return readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

const raw = readStdin();
let payload = {};
try {
  payload = JSON.parse(raw || "{}");
} catch {
  process.exit(0); // không parse được -> không chặn
}

const input = payload.tool_input || {};
const filePath = (input.file_path || "").replace(/\\/g, "/");

// Bỏ qua các file vốn để chứa giá trị thật / mẫu.
if (
  /(^|\/)\.env(\.|$)/.test(filePath) ||
  /\.example$/.test(filePath) ||
  /(^|\/)\.claude\//.test(filePath)
) {
  process.exit(0);
}

// Nội dung sẽ được ghi: Write -> content, Edit -> new_string.
const content = input.content ?? input.new_string ?? "";

const patterns = [
  [/ghp_[A-Za-z0-9]{36}/, "GitHub Personal Access Token"],
  [/github_pat_[A-Za-z0-9_]{60,}/, "GitHub fine-grained PAT"],
  [/sbp_[A-Za-z0-9]{40}/, "Supabase access token"],
  [/sk-[A-Za-z0-9]{20,}/, "OpenAI API key"],
  [/AIza[0-9A-Za-z_\-]{35}/, "Google API key (Gemini)"],
  [/xkeysib-[A-Za-z0-9]{40,}/, "SendGrid/Brevo API key"],
  [/AKIA[0-9A-Z]{16}/, "AWS Access Key ID"],
  [/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/, "Private key block"],
  // Connection string Postgres có mật khẩu thật (không phải biến môi trường).
  [/(?:Password|Pwd)\s*=\s*(?!\$\{|<|YOUR_|xxx)[^;\s"']{6,}/i, "DB password trong connection string"],
];

const hits = [];
for (const [re, label] of patterns) {
  if (re.test(content)) hits.push(label);
}

if (hits.length === 0) process.exit(0);

const reason =
  `Phát hiện secret hardcode (${hits.join(", ")}) trong ${filePath || "file"}. ` +
  `Vi phạm CLAUDE.md rule #2. Hãy chuyển sang environment variable / appsettings + user-secrets.`;

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
