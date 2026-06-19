#!/usr/bin/env node
// PostToolUse(Write|Edit): cảnh báo (không chặn) khi code đi ngược các ADR trong CLAUDE.md.
// Trả additionalContext để Claude tự sửa ở lượt sau.

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

const input = payload.tool_input || {};
const filePath = (input.file_path || "").replace(/\\/g, "/");
const content = input.content ?? input.new_string ?? "";
if (!content) process.exit(0);

const isTs = /\.(ts|tsx)$/.test(filePath);
const isComponent = /frontend\/src\/(pages|components)\//.test(filePath) && /\.tsx$/.test(filePath);

const warnings = [];

if (/@supabase\/supabase-js/.test(content)) {
  warnings.push("ADR-002: không dùng Supabase SDK — kết nối PostgreSQL trực tiếp qua EF Core.");
}
if (/\borganization_id\b/.test(content)) {
  warnings.push("ADR-012 (single-tenant): không dùng organization_id ở bất kỳ đâu.");
}
if (isComponent && /\b(fetch\(|axios\.(get|post|put|delete|patch))/.test(content)) {
  warnings.push("FE rule: không gọi API trực tiếp trong component — đi qua services/.");
}
if (isTs && /:\s*any\b|<any>|as any\b/.test(content)) {
  warnings.push("FE rule: tránh dùng `any` trong TypeScript.");
}
// Backend nghi vấn gọi thẳng OpenAI SDK thay vì IAIProvider.
if (/backend\//.test(filePath) && /\bnew OpenAIClient\b|OpenAI\.GPT/i.test(content)) {
  warnings.push("Rule #8: business logic không gọi trực tiếp OpenAI SDK — qua IAIProvider/IEmbeddingProvider.");
}

if (warnings.length === 0) process.exit(0);

process.stdout.write(
  JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PostToolUse",
      additionalContext:
        `[arch-guard] Lưu ý quy ước ARISP trong ${filePath}:\n- ` + warnings.join("\n- "),
    },
  })
);
process.exit(0);
