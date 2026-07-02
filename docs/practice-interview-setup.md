# Phỏng vấn thử (Practice) — Hướng dẫn cắm API key để test

Luồng phỏng vấn thử đã được nối **end-to-end**. Code không chứa secret — bạn chỉ cần điền **4 API key** rồi chạy là test được.

## Pipeline thực tế (ADR-043/044)

```
Mic (FE) ──> Deepgram live STT ──> transcript
                                      │ (SubmitAnswerText qua SignalR)
                                      ▼
                       Backend InterviewService ──> LLM/RAG (OpenAI GPT-4o)
                                      │ ReceiveQuestion (SignalR /hubs/session)
                                      ▼
   FE nhận câu hỏi ──> BE TTS ElevenLabs (PCM 24k base64) ──> LiveAvatar repeatAudio()
                                      ▼
                 HeyGen LiveAvatar (LITE) lip-sync audio → video stream (attach <video>)
```

- **STT/Avatar chạy client-side** qua SDK chính thức; **BE giữ API key thật và chỉ mint token ngắn hạn** (`GET /api/interview/session/{id}/media-config`).
- Mọi nhánh media **fallback mềm**: thiếu HeyGen → browser TTS + bot tĩnh; thiếu Deepgram → nút "Gửi trả lời" (nhập tay). Để test **đúng trải nghiệm thật** cần đủ 4 key.

## 4 API key cần điền

> **Bảo mật (rule #2):** KHÔNG commit key vào `appsettings.json`. Dùng `dotnet user-secrets` (dev) hoặc biến môi trường. Các khóa dưới đây trong `appsettings.json` chỉ là placeholder rỗng.

| # | Dịch vụ | Vai trò | Khóa cấu hình | Lấy ở đâu |
|---|---|---|---|---|
| 1 | **OpenAI** | LLM "bộ não" sinh câu hỏi + chấm | `AI:OpenAI:ApiKey` (Provider=`openai`) | platform.openai.com/api-keys |
| 2 | **Deepgram** | STT live (giọng → text) | `Media:Deepgram:ApiKey` | console.deepgram.com → API Keys |
| 3 | **ElevenLabs** | Giọng TTS (Flash v2.5) | `Media:ElevenLabs:ApiKey` + `Media:ElevenLabs:DefaultVoiceId` | elevenlabs.io/app/settings/api-keys |
| 4 | **HeyGen LiveAvatar** | Avatar (LITE, lip-sync audio ElevenLabs) | `Media:HeyGen:ApiKey` + `Media:HeyGen:DefaultAvatarId` (UUID) | api.liveavatar.com / app.liveavatar.com |

### Cách điền bằng user-secrets (khuyến nghị cho dev)

```bash
cd backend/ARISP.API
dotnet user-secrets set "AI:OpenAI:ApiKey"            "sk-..."
dotnet user-secrets set "Media:Deepgram:ApiKey"       "..."
dotnet user-secrets set "Media:ElevenLabs:ApiKey"     "..."
dotnet user-secrets set "Media:ElevenLabs:DefaultVoiceId" "<elevenlabs_voice_id>"
dotnet user-secrets set "Media:HeyGen:ApiKey"         "..."
dotnet user-secrets set "Media:HeyGen:DefaultAvatarId" "<heygen_avatar_id>"
# (tuỳ chọn) route giọng ElevenLabs qua avatar HeyGen:
dotnet user-secrets set "Media:HeyGen:DefaultVoiceId" "<voice_id_trong_heygen>"
```

### Lấy key ở đâu (link trực tiếp)

**OpenAI** — LLM (đã cấu hình)
- Dashboard: https://platform.openai.com/api-keys

**Deepgram** — STT (tài khoản mới có credit miễn phí ~$200)
- Đăng ký/đăng nhập: https://console.deepgram.com/signup
- Tạo key: Console → **API Keys** (sidebar) → **Create a New API Key** → copy
```bash
dotnet user-secrets set "Media:Deepgram:ApiKey" "<deepgram_key>"
```

**ElevenLabs** — TTS (giọng nói)
- API key: https://elevenlabs.io/app/settings/api-keys → **Create / copy**
- Voice ID: https://elevenlabs.io/app/voice-library → chọn giọng → **⋯ → Copy Voice ID**
```bash
dotnet user-secrets set "Media:ElevenLabs:ApiKey"         "<elevenlabs_key>"
dotnet user-secrets set "Media:ElevenLabs:DefaultVoiceId" "<voice_id>"
```

**HeyGen LiveAvatar** — Avatar (đã migrate từ Streaming API cũ đã sunset → **LiveAvatar**, chế độ LITE)
- API key: https://app.liveavatar.com (hoặc dashboard HeyGen có LiveAvatar) — key dùng cho header `X-API-KEY` của `https://api.liveavatar.com`.
- Avatar ID: **UUID của LiveAvatar** (vd `dd73ea75-1218-4ef3-92ce-606d5f7fbc0a` — avatar demo public, chạy được ở sandbox). **KHÁC** avatar id Streaming cũ (chuỗi hex liền như `c1926d82...` sẽ KHÔNG dùng được).
```bash
dotnet user-secrets set "Media:HeyGen:ApiKey"          "<liveavatar_api_key>"
dotnet user-secrets set "Media:HeyGen:DefaultAvatarId" "dd73ea75-1218-4ef3-92ce-606d5f7fbc0a"   # avatar UUID
# IsSandbox mặc định true (test miễn phí); set false khi chạy thật:
# dotnet user-secrets set "Media:HeyGen:IsSandbox" "false"
```

> **Lưu ý LiveAvatar (ADR-044):**
> - Chế độ **LITE**: avatar chỉ **lip-sync audio ElevenLabs** do BE cấp (giữ não RAG/GPT-4o), không dùng agent built-in. Vì vậy **bắt buộc có ElevenLabs key + voice** để avatar nói.
> - `DefaultAvatarId` phải là **UUID LiveAvatar**. Avatar id Streaming cũ không dùng được nữa.
> - `Media:HeyGen:IsSandbox=true` (mặc định) để test không tốn phí. Đổi `false` khi production.
> - Thiếu LiveAvatar key/avatar (hoặc API lỗi) → media-config trả `heyGen=null`, FE **fallback browser TTS + bot tĩnh**; STT/LLM vẫn chạy. Không làm sập phòng phỏng vấn.

> **LLM qua RAG service (tuỳ chọn):** mặc định `AI:Provider=openai` chạy in-process, **không cần** chạy Python. Nếu muốn dùng Hybrid RAG (ADR-039): đặt `AI:Provider=rag`, chạy `rag-service` và set `OPENAI_API_KEY` trong `rag-service/.env`.

## Điều kiện nghiệp vụ để vào được phòng thử

Practice mở **1 lượt / vòng** sau khi ứng viên **đã pass CV + đặt lịch buổi thật của vòng** (ADR-020/027). Để test nhanh, đảm bảo có 1 `Application` ở trạng thái phù hợp rồi vào route:

```
/interview/practice/:applicationId    (đăng nhập Candidate)
```

## Chạy

```bash
# Backend
cd backend/ARISP.API && dotnet run
# Frontend
cd frontend && npm install && npm run dev
```

Vào phòng → qua cổng kiểm tra mic/cam (ADR-040) → AI hỏi (avatar nói) → bạn trả lời bằng giọng (Deepgram nhận) → dừng ~1s để AI nhận, hoặc bấm **"Gửi trả lời"** → AI hỏi tiếp → bấm **Kết thúc** sinh Evaluation Report.

## Endpoint/Hub liên quan
- `POST /api/interview/session/start` (CandidateOnly) — tạo phiên practice.
- `GET /api/interview/session/{id}/media-config` (CandidateOnly) — token Deepgram + HeyGen.
- SignalR `/hubs/session`: `JoinSession`, `StartInterview`, `SubmitAnswerText`; nhận `ReceiveQuestion` / `ReceiveSessionStatus`.
- `POST /api/interview/session/{id}/end` — kết thúc + tự sinh Evaluation.
