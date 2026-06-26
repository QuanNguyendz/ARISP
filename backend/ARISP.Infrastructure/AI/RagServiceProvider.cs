using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;

namespace ARISP.Infrastructure.AI
{
    /// <summary>
    /// Client gọi RAG microservice (Python/FastAPI) — ADR-039 (mở rộng: Python sở hữu cả
    /// retrieval lẫn sinh câu hỏi/đánh giá). Giữ nguyên abstraction <see cref="IAIProvider"/> +
    /// <see cref="IEmbeddingProvider"/> để business logic không đổi (ADR-004, rule #8); thêm
    /// <see cref="IRagIngestionService"/> cho luồng ingest.
    ///
    /// HttpClient là typed client, BaseAddress = RAG_SERVICE_URL (cấu hình ở Program.cs).
    /// Wire JSON dùng camelCase, khớp pydantic alias phía service Python.
    /// </summary>
    public class RagServiceProvider : IAIProvider, IEmbeddingProvider, IRagIngestionService
    {
        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        public RagServiceProvider(HttpClient http)
        {
            _http = http;
        }

        // ---------------- IRagIngestionService ----------------

        public async Task<int> IngestAsync(string sourceType, Guid sourceId, string text,
            string? scope = null, string? documentType = null, bool replaceExisting = true,
            CancellationToken ct = default)
        {
            var payload = new
            {
                sourceType,
                sourceId = sourceId.ToString(),
                text,
                scope,
                documentType,
                replaceExisting,
            };
            using var resp = await _http.PostAsJsonAsync("/ingest", payload, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts, ct);
            return doc.TryGetProperty("chunksWritten", out var cw) ? cw.GetInt32() : 0;
        }

        // ---------------- IEmbeddingProvider ----------------

        public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            using var resp = await _http.PostAsJsonAsync("/embed", new { text }, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts, ct);
            var arr = doc.GetProperty("embedding");
            var vector = new float[arr.GetArrayLength()];
            int i = 0;
            foreach (var v in arr.EnumerateArray()) vector[i++] = (float)v.GetDouble();
            return vector;
        }

        /// <summary>
        /// Vector-only retrieval không còn dùng ở .NET: truy hồi (hybrid) nằm trong service Python,
        /// được gọi nội bộ khi sinh câu hỏi (/next-question) hoặc qua /retrieve bằng câu truy vấn text.
        /// Giữ để thỏa interface (ADR-004); không có caller trong codebase.
        /// </summary>
        public Task<IEnumerable<DocumentChunk>> RetrieveAsync(Guid? sourceId, float[] queryVector, int topK = 5, CancellationToken ct = default)
            => throw new NotSupportedException(
                "Vector retrieval do RAG service (Python) sở hữu — dùng IRagIngestionService + /next-question, " +
                "hoặc gọi /retrieve bằng câu truy vấn dạng text.");

        // ---------------- IAIProvider ----------------

        public async IAsyncEnumerable<string> StreamQuestionAsync(QuestionContext ctx,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/next-question")
            {
                Content = JsonContent.Create(ctx, options: JsonOpts),
            };
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                string? token = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("token", out var t))
                        token = t.GetString();
                }
                catch (JsonException)
                {
                    // Bỏ qua dòng SSE không phải JSON hợp lệ.
                }
                if (!string.IsNullOrEmpty(token))
                    yield return token!;
            }
        }

        public async Task<AnswerAnalysis> AnalyzeAnswerAsync(AnswerContext ctx, CancellationToken ct)
        {
            using var resp = await _http.PostAsJsonAsync("/analyze-answer", ctx, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<AnswerAnalysis>(JsonOpts, ct)
                   ?? new AnswerAnalysis();
        }

        public async Task<EvaluationReport> GenerateEvaluationAsync(SessionContext ctx, CancellationToken ct)
        {
            using var resp = await _http.PostAsJsonAsync("/evaluate", ctx, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<EvaluationReport>(JsonOpts, ct)
                   ?? new EvaluationReport();
        }

        public async Task<string> DetectLanguageRequirementAsync(string jdText, CancellationToken ct)
        {
            using var resp = await _http.PostAsJsonAsync("/detect-language", new { jdText }, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts, ct);
            return doc.TryGetProperty("language", out var lang) ? lang.GetString() ?? "vi" : "vi";
        }

        public async Task<LanguageAssessment> AssessLanguageProficiencyAsync(SessionContext ctx, CancellationToken ct)
        {
            using var resp = await _http.PostAsJsonAsync("/assess-language", ctx, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<LanguageAssessment>(JsonOpts, ct)
                   ?? new LanguageAssessment();
        }

        public async Task<string> CompleteJsonAsync(string systemInstruction, string userContent, CancellationToken ct = default)
        {
            using var resp = await _http.PostAsJsonAsync("/complete-json",
                new { systemInstruction, userContent }, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            // Service trả về JSON object thô — trả nguyên chuỗi (giống OpenAIProvider.CompleteJsonAsync).
            return await resp.Content.ReadAsStringAsync(ct);
        }
    }
}
