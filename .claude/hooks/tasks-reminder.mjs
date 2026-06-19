#!/usr/bin/env node
// Stop: nhắc cập nhật .ai/tasks.md (CLAUDE.md rule #19 — BẮT BUỘC sau mọi task).
// Heuristic: nếu có thay đổi ở code (backend/ hoặc frontend/) mà .ai/tasks.md chưa đổi -> nhắc.

import { execFileSync } from "node:child_process";

function git(args) {
  try {
    return execFileSync("git", args, { encoding: "utf8" });
  } catch {
    return "";
  }
}

const status = git(["status", "--porcelain"]);
if (!status.trim()) process.exit(0); // working tree sạch -> không nhắc

const files = status
  .split("\n")
  .map((l) => l.slice(3).trim())
  .filter(Boolean);

const touchedCode = files.some((f) => /^(backend|frontend)\//.test(f.replace(/\\/g, "/")));
const touchedTasks = files.some((f) => /\.ai\/tasks\.md$/.test(f.replace(/\\/g, "/")));

if (touchedCode && !touchedTasks) {
  process.stdout.write(
    JSON.stringify({
      systemMessage:
        "⚠️ Có thay đổi code nhưng .ai/tasks.md chưa được cập nhật. " +
        "CLAUDE.md rule #19 yêu cầu đánh dấu [x] + ngày hoàn thành và commit tasks.md CÙNG commit code.",
    })
  );
}
process.exit(0);
