# ARISP RAG Interview Service

Microservice Python (FastAPI + LangChain + LangGraph) sở hữu **toàn bộ pipeline RAG + sinh câu hỏi/đánh giá phỏng vấn** (ADR-039 mở rộng). Backend .NET gọi qua HTTP nội bộ (`http://rag-service:8000`) — **không expose ra Nginx**.

## Lộ trình
- **Giai đoạn 1 (hiện tại):** Hybrid RAG = dense (pgvector cosine) + sparse (Postgres full-text) → hợp nhất Reciprocal Rank Fusion + weighting theo scope (ADR-025).
- **Giai đoạn 2:** CRAG — thêm node `grade_documents` + corrective vào LangGraph.
- **Giai đoạn 3:** Agentic — router/tool nodes.

## Kiến trúc
```
app/
  main.py            FastAPI app (+ /health), lifespan mở/đóng DB pool
  config.py          Settings (pydantic-settings) đọc env DATABASE_* / OPENAI_API_KEY
  core/
    db.py            asyncpg pool tới Supabase (kết nối trực tiếp, không Supabase SDK)
    embeddings.py    OpenAI text-embedding-3-small (1536) + mock mode
    llm.py           ChatOpenAI gpt-4o (stream) / gpt-4o-mini (JSON)
  rag/
    chunker.py       chia chunk (mirror PlaybookService.ChunkText của .NET)
    ingest.py        chunk -> embed -> ghi document_chunks (idempotent)
    retriever.py     HYBRID dense+sparse -> RRF -> scope weight
    graph.py         LangGraph StateGraph: retrieve -> generate (điểm mở rộng CRAG/Agentic)
  prompts/           prompt builders (language-aware, ADR-018)
  schemas/           pydantic models (wire camelCase, khớp DTO .NET)
  services/          logic interview (JSON endpoints + mock)
  api/               routers: ingest, retrieve, interview
```

## Endpoints
| Method | Path | Mô tả |
|---|---|---|
| GET  | `/health` | trạng thái + mock_mode + db |
| POST | `/ingest` | chunk+embed+lưu `document_chunks` |
| POST | `/retrieve` | hybrid retrieve, trả chunks xếp hạng |
| POST | `/next-question` | **SSE stream** token câu hỏi (LangGraph) |
| POST | `/analyze-answer` | JSON `AnswerAnalysis` |
| POST | `/evaluate` | JSON `EvaluationReport` |
| POST | `/detect-language` | JSON `{language}` |
| POST | `/assess-language` | JSON `LanguageAssessment` |
| POST | `/complete-json` | JSON generic (fallback của .NET) |

SSE `/next-question` phát các dòng `data: {"token": "..."}` rồi `data: [DONE]`.

## Bảng dữ liệu
Bảng `document_chunks` do **EF Core (.NET) sở hữu schema**; service này chỉ đọc/ghi.
Sparse retrieval cần GIN index FTS — tạo bằng EF migration `AddDocumentChunksFtsIndex`
(`to_tsvector('simple', chunk_text)`).

## Chạy local (không Docker)
```bash
cd rag-service
python -m venv .venv && . .venv/Scripts/activate   # Windows: .venv\Scripts\activate
pip install -e ".[dev]"
cp .env.example .env          # điền DATABASE_* (+ OPENAI_API_KEY nếu muốn dùng model thật)
uvicorn app.main:app --reload --port 8000
# Swagger: http://localhost:8000/docs
```
Không set `OPENAI_API_KEY` → **mock mode**: vector & câu trả lời giả định, vẫn ghi/đọc DB thật để test luồng end-to-end.

## Test
```bash
pip install -e ".[dev]"
pytest            # unit tests: chunker, retriever (RRF/scope weight), schemas, embeddings mock
```

## Docker
Build & chạy cùng stack qua `docker/docker-compose.yml` (service `rag-service`, container `arisp-rag`, port 8000, mạng `arisp-network`).
