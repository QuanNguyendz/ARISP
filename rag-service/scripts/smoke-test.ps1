# Smoke test RAG service qua HTTP (cần service đang chạy ở http://localhost:8000 + DB đã cấu hình).
# Chạy: powershell -File scripts\smoke-test.ps1
$ErrorActionPreference = "Stop"
$base = "http://localhost:8000"
$sid  = [guid]::NewGuid().ToString()   # giả lập 1 application_id

Write-Host "== /health ==" -ForegroundColor Cyan
Invoke-RestMethod "$base/health" | ConvertTo-Json -Depth 5

Write-Host "`n== /ingest (CV mẫu) ==" -ForegroundColor Cyan
$ingest = @{
  sourceType = "cv"
  sourceId   = $sid
  text       = @"
Nguyen Van A - Backend Engineer.

Kinh nghiem 5 nam voi C# ASP.NET Core va PostgreSQL.

Da trien khai he thong RAG, toi uu truy van SQL cham, dung Redis cache va Docker.
"@
  documentType = $null
} | ConvertTo-Json
Invoke-RestMethod "$base/ingest" -Method Post -ContentType "application/json" -Body $ingest | ConvertTo-Json

Write-Host "`n== /retrieve (hybrid: dense + sparse) ==" -ForegroundColor Cyan
$retrieve = @{
  query       = "toi uu PostgreSQL va cache Redis"
  sourceId    = $sid
  sourceTypes = @("cv")
  topK        = 3
} | ConvertTo-Json
$res = Invoke-RestMethod "$base/retrieve" -Method Post -ContentType "application/json" -Body $retrieve
$res.chunks | Format-Table chunkIndex, score, @{n="text";e={$_.chunkText.Substring(0,[Math]::Min(60,$_.chunkText.Length))}}

Write-Host "`nOK." -ForegroundColor Green
