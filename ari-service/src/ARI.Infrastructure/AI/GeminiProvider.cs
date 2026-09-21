using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.DTOs;
using ARI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.AI
{
    public class GeminiProvider : IGeminiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiProvider> _logger;
        private readonly IAIProvider _aiProvider; // fallback khi Gemini lỗi/quá tải (OpenAI GPT-4o-mini)
        private readonly string _apiKey;

        // Không nhúng API key vào URL — sẽ bị HttpClient logging ghi ra log dạng plaintext.
        // Key được truyền qua header x-goog-api-key trong PostToGeminiAsync.
        private const string GeminiEndpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        private const string PdfMime = "application/pdf";

        private static readonly JsonSerializerOptions ReadOpts = new() { PropertyNameCaseInsensitive = true };

        public GeminiProvider(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiProvider> logger, IAIProvider aiProvider)
        {
            _httpClient = httpClient;
            _logger = logger;
            _aiProvider = aiProvider;
            _apiKey = configuration["GEMINI_API_KEY"] ?? string.Empty;
        }

        /// <summary>
        /// Lấy JSON kết quả: thử Gemini trước; nếu lỗi (vd 503 quá tải, hết retry) thì fallback sang
        /// OpenAI GPT-4o-mini với cùng system instruction + nội dung + CHÍNH CÁC FILE gốc (ADR-070 —
        /// trước đây fallback chỉ nhận text trích ra, CV scan tới tay model gần như rỗng), rồi bọc lại
        /// theo envelope giống Gemini (candidates[0].content.parts[0].text) để khối parse dùng chung.
        /// Nếu cả hai cùng lỗi → ném exception cho caller trả Result.Failure.
        /// </summary>
        private async Task<(string Json, string Provider)> GetAnalysisJsonAsync(
            object geminiRequestBody, string systemInstruction, string userContent,
            IReadOnlyList<AiAttachment>? attachments, CancellationToken ct)
        {
            try
            {
                var json = await PostToGeminiAsync(geminiRequestBody, ct);
                return (json, "Gemini");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Gemini lỗi — chuyển fallback OpenAI GPT-4o-mini.");
                var innerJson = await _aiProvider.CompleteJsonAsync(systemInstruction, userContent, attachments, ct);
                var envelope = new
                {
                    candidates = new[]
                    {
                        new { content = new { parts = new[] { new { text = innerJson } } } },
                    },
                };
                return (JsonSerializer.Serialize(envelope), "GPT-4o-mini");
            }
        }

        /// <summary>
        /// Gọi Gemini generateContent với API key ở header (không lộ trong URL/log) và
        /// retry exponential backoff cho lỗi tạm thời 503 (overload) / 429 (rate limit).
        /// Ném exception nếu thất bại sau khi hết số lần thử — caller bắt và trả Result.Failure.
        /// </summary>
        private async Task<string> PostToGeminiAsync(object requestBody, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("GEMINI_API_KEY is not configured.");

            const int maxAttempts = 3;
            for (int attempt = 1; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, GeminiEndpoint)
                {
                    Content = JsonContent.Create(requestBody),
                };
                request.Headers.Add("x-goog-api-key", _apiKey);

                var response = await _httpClient.SendAsync(request, ct);
                try
                {
                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadAsStringAsync(ct);

                    // Đọc body để lấy thông điệp lỗi thật của Gemini (vd "model is overloaded")
                    // — EnsureSuccessStatusCode không đọc body nên trước đây ta không thấy lý do.
                    var status = (int)response.StatusCode;
                    var errorBody = await response.Content.ReadAsStringAsync(ct);

                    var transient = response.StatusCode == HttpStatusCode.ServiceUnavailable
                        || response.StatusCode == HttpStatusCode.TooManyRequests;
                    if (transient && attempt < maxAttempts)
                    {
                        var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)); // 0.5s → 1s
                        _logger.LogWarning(
                            "Gemini trả {Status}, thử lại lần {Next}/{Max} sau {Delay}ms. Chi tiết: {Detail}",
                            status, attempt + 1, maxAttempts, delay.TotalMilliseconds, errorBody);
                        await Task.Delay(delay, ct);
                        continue;
                    }

                    // Hết retry (hoặc lỗi không phải transient) → ném exception kèm lý do từ Gemini.
                    throw new HttpRequestException(
                        $"Gemini trả HTTP {status}. Chi tiết: {errorBody}");
                }
                finally
                {
                    response.Dispose();
                }
            }
        }

        /// <summary>
        /// Bóc JSON do model sinh ra khỏi envelope <c>candidates[0].content.parts[0].text</c>, gỡ rào
        /// markdown nếu model lờ <c>responseMimeType</c>, kèm số token. Trả null nếu rỗng.
        /// </summary>
        private static (string? Json, int PromptTokens, int CompletionTokens) Unwrap(string envelopeJson)
        {
            using var document = JsonDocument.Parse(envelopeJson);
            var root = document.RootElement;
            var raw = root.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0]
                .GetProperty("text").GetString();

            int promptTokens = 0, completionTokens = 0;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var p)) promptTokens = p.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var c)) completionTokens = c.GetInt32();
            }

            if (string.IsNullOrEmpty(raw)) return (null, promptTokens, completionTokens);

            raw = raw.Trim();
            if (raw.StartsWith("```"))
            {
                var firstNewLine = raw.IndexOf('\n');
                raw = firstNewLine >= 0 ? raw[(firstNewLine + 1)..] : raw.TrimStart('`');
                if (raw.EndsWith("```")) raw = raw[..^3];
            }
            return (raw.Trim(), promptTokens, completionTokens);
        }

        /// <summary>Phần <c>parts</c> của một file gốc: PDF gửi nguyên file, loại khác không gửi được.</summary>
        private static object InlinePdf(AiAttachment file) => new
        {
            inline_data = new { mime_type = PdfMime, data = Convert.ToBase64String(file.Bytes) },
        };

        private static bool IsPdf(AiAttachment? file)
            => file is { Bytes.Length: > 0 } && string.Equals(file.MimeType, PdfMime, StringComparison.OrdinalIgnoreCase);

        // ================================================================
        // Chấm CV theo bộ tiêu chí (ADR-060 / ADR-070)
        // ================================================================

        private const string CvScoringInstruction = @"You are an expert technical recruiter scoring a candidate's CV against a Job Description using the company's SCORING RUBRIC.
CRITICAL: First verify the document is actually a CV/Resume. If it is not, set ""is_valid_cv"" to false, return an empty ""criteria"" array, and leave the other fields empty.

If it IS a CV:
1. Identify the seniority the JD requires (Fresher, Junior, Mid, Senior).
2. Work out the candidate's PROFESSIONAL experience. Academic projects and short internships DO NOT count as professional experience for Senior roles.
3. Evaluate EVERY scoring criterion (section ""TIÊU CHÍ CHẤM ĐIỂM"") exactly once. Base everything ONLY on evidence written in the CV — no evidence means a low band; never assume. You never give numbers: the company's own formula turns your qualitative answers into points.
   a. ""band"": choose ""excellent"" (clearly exceeds what the role needs), ""good"" (clearly meets it), ""fair"" (partly meets it) or ""poor"" (does not meet it). When the criterion lists band descriptions (lines ""excellent:"", ""good:"", ""fair:"", ""poor:""), pick the band whose description the evidence matches; otherwise judge against the criterion's scoring guide.
      - For criteria about experience or seniority: if the JD requires Senior (e.g. 4+ years) and the candidate has under 1 year of professional experience, the band MUST be ""poor"" and ""position"" MUST be 0.
      - Check depth: hands-on production work (building systems, optimisation, ownership) ranks higher than surface-level API usage or keyword lists.
   b. If the criterion has a checklist (""ý kiểm"", items written as [key] text), answer EVERY item in ""checks"" as { ""key"", ""met"", ""evidence"" }:
      - ""met"": true ONLY when the CV explicitly proves the item, and then quote that proof verbatim in ""evidence"" (at most 200 characters). Otherwise ""met"": false and ""evidence"" is an empty string.
      - Judge each item on its own; do not change the band because of the checklist. The system computes the exact position inside the band from your answers, so set ""position"" to null for these criteria.
   c. If the criterion has NO checklist, give ""position"" = where the evidence sits inside the chosen band, a number from 0 to 1: 0 when the evidence barely meets the band description, 0.5 when it clearly meets it with several pieces of evidence, 1 when it is close to the next band's description (1 in ""excellent"" only when it clearly exceeds every aspect). Use ""checks"": [].
   - ""met"": null for scoring criteria.
   - ""evidence"": quote the CV verbatim for the criterion as a whole (short, at most 300 characters; join several quotes with "" … ""). Use an empty string when the CV has nothing relevant.
   - ""reasoning"": 1-2 sentences explaining why the evidence falls in that band, referring to the band descriptions / scoring guide.
4. Answer EVERY required condition (section ""ĐIỀU KIỆN BẮT BUỘC"", if present) exactly once, as { ""key"", ""met"", ""evidence"", ""reasoning"" } with ""band"": null, ""position"": null, ""checks"": []:
   - ""met"": true ONLY when the CV explicitly proves the condition, and then quote that proof verbatim in ""evidence"". A condition the CV does not mention is NOT met (""met"": false, ""evidence"": """") — never assume.
   - A required condition must never influence the band of any scoring criterion.
5. Do NOT produce an overall score or recommendation. The system computes them with the company's formula.
6. Anything listed under ""KHÔNG ĐƯỢC DÙNG ĐỂ CHẤM ĐIỂM"" must never influence any answer.

LANGUAGE RULE: every text value (analysis_reasoning, seniority_alignment, tech_depth_analysis, reasoning, summary, skills_matched, skills_gaps, red_flags, experience_relevance) MUST be written in VIETNAMESE. Keep proper nouns / technical terms as-is (C#, .NET, PostgreSQL, React...). The ""evidence"" quotes stay in the CV's original language.

Return ONLY a valid JSON object, without markdown formatting:
{
  ""is_valid_cv"": boolean,
  ""analysis_reasoning"": string (lập luận từng bước),
  ""seniority_alignment"": string (khoảng cách cấp bậc giữa JD và CV),
  ""tech_depth_analysis"": string (chiều sâu thực chiến so với kiến thức bề mặt),
  ""criteria"": [ { ""key"": string (exactly one of the rubric keys), ""band"": ""excellent"" | ""good"" | ""fair"" | ""poor"" | null (null for required conditions), ""position"": number 0-1 or null (null for criteria with a checklist and for required conditions), ""met"": boolean or null (required conditions only), ""checks"": [ { ""key"": string (exactly one of this criterion's checklist keys), ""met"": boolean, ""evidence"": string } ], ""evidence"": string, ""reasoning"": string } ],
  ""summary"": string (ĐÚNG 2 đoạn, ngăn cách bằng '\n'. Đoạn 1 bắt đầu bằng '🌟 Điểm sáng: '. Đoạn 2 bắt đầu bằng '⚠️ Điểm cần lưu ý: '. Mỗi đoạn 2-4 câu.),
  ""skills_matched"": string[] (mỗi phần tử một kỹ năng khớp, kèm mức độ ngắn trong ngoặc),
  ""skills_gaps"": string[] (mỗi phần tử một kỹ năng/kinh nghiệm còn thiếu),
  ""red_flags"": string[] (khoảng trống sự nghiệp hoặc điểm đáng ngờ; mảng rỗng nếu không có),
  ""experience_relevance"": string (mức độ phù hợp lĩnh vực với JD)
}";

        public async Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(CvScoringAiRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.RubricInstruction) || request.CriterionKeys.Count == 0)
                return Result<CvJdAnalysisResultDto>.Failure("Không có bộ tiêu chí — không chấm CV.");

            var hasCvPdf = IsPdf(request.CvPdf);
            if (!hasCvPdf && string.IsNullOrWhiteSpace(request.CvText))
                return Result<CvJdAnalysisResultDto>.Failure("Either PDF file bytes or fallback text must be provided.");

            var systemInstruction = CvScoringInstruction
                + "\n\n--- SCORING RUBRIC ---\n" + request.RubricInstruction
                + "\n\nREQUIRED criterion keys (answer each exactly once, no other keys): "
                + string.Join(", ", request.CriterionKeys);

            var hasJdPdf = IsPdf(request.JdPdf);
            var jdHeader = $"--- JOB DESCRIPTION ---\n{request.JdText}"
                           + (hasJdPdf ? "\n(File JD gốc đính kèm ngay sau đây — ưu tiên nội dung trong file.)" : string.Empty);

            var parts = new List<object> { new { text = jdHeader } };
            if (hasJdPdf) parts.Add(InlinePdf(request.JdPdf!));
            parts.Add(new { text = "\n--- CANDIDATE CV ---" });
            if (hasCvPdf)
            {
                parts.Add(InlinePdf(request.CvPdf!));
                if (!string.IsNullOrWhiteSpace(request.CvText))
                    parts.Add(new { text = "\n(Fallback Extracted Text in case PDF parsing fails):\n" + request.CvText });
            }
            else
            {
                parts.Add(new { text = request.CvText! });
            }

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { parts } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.0 },
            };

            var attachments = new List<AiAttachment>();
            if (hasJdPdf) attachments.Add(request.JdPdf!);
            if (hasCvPdf) attachments.Add(request.CvPdf!);

            var fallbackUser = $"{jdHeader}\n\n--- CANDIDATE CV ---\n"
                               + (hasCvPdf ? "(CV gốc đính kèm dạng PDF.)\n" : string.Empty)
                               + request.CvText;

            var sw = Stopwatch.StartNew();
            string responseJson;
            string provider;
            try
            {
                (responseJson, provider) = await GetAnalysisJsonAsync(requestBody, systemInstruction, fallbackUser, attachments, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Gemini + fallback OpenAI đều lỗi (chấm CV).");
                return Result<CvJdAnalysisResultDto>.Failure($"Dịch vụ AI tạm thời không khả dụng: {ex.Message}");
            }
            sw.Stop();
            _logger.LogInformation("Chấm CV bằng {Provider} xong trong {Ms}ms", provider, sw.ElapsedMilliseconds);

            try
            {
                var (json, promptTokens, completionTokens) = Unwrap(responseJson);
                if (string.IsNullOrEmpty(json))
                    return Result<CvJdAnalysisResultDto>.Failure("AI trả về nội dung rỗng.");

                var result = JsonSerializer.Deserialize<CvJdAnalysisResultDto>(json, ReadOpts);
                if (result == null)
                    return Result<CvJdAnalysisResultDto>.Failure("Không đọc được kết quả chấm CV của AI.");

                result.Criteria ??= new List<CvCriterionAiResult>();
                result.RawResponse = responseJson;
                result.ProcessingTimeMs = (int)sw.ElapsedMilliseconds;
                result.PromptTokens = promptTokens;
                result.CompletionTokens = completionTokens;
                result.Provider = provider;
                return Result<CvJdAnalysisResultDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không parse được kết quả chấm CV. Raw: {RawResponse}", responseJson);
                return Result<CvJdAnalysisResultDto>.Failure($"Không đọc được kết quả chấm CV của AI: {ex.Message}");
            }
        }

        // ================================================================
        // Gợi ý bộ tiêu chí chấm CV (ADR-070)
        // ================================================================

        private const string RubricSuggestionInstruction = @"You help a Hiring Manager draft a CV SCORING RUBRIC for one job opening.
Rules:
- Return 4 to 6 criteria that can be judged FROM A CV ALONE: professional experience, required technical skills, domain knowledge, education / certificates, measurable achievements, CV clarity. Do NOT include criteria that need an interview (communication, attitude, culture fit).
- NEVER use protected or discriminatory attributes (age, gender, marital status, religion, ethnicity, hometown, appearance, health) as criteria.
- Weights are integers that sum to exactly 100 and reflect importance for THIS role.
- Every text value is Vietnamese; keep technology names as-is.
- ""name"": short criterion name. ""description"": what a strong candidate shows in the CV (1-2 sentences).
- ""excellent"" (clearly exceeds the role's needs), ""good"" (clearly meets them), ""fair"" (partly meets them), ""poor"" (does not meet them): concrete, observable descriptions (years, named technologies, measurable results) so two reviewers would pick the same band. Never write point ranges — the company sets them.
- ""checks"": 3 to 5 short yes/no items, each verifiable from the CV text alone and each a DISTINCT sign of strength for this criterion (e.g. ""Có ≥ 3 năm làm C#/.NET production"", ""Có số liệu kết quả đo được (%, số người dùng)""). They decide the exact score inside a band, so do not just restate the band descriptions, and never use protected attributes.
Return ONLY a valid JSON object, without markdown:
{ ""criteria"": [ { ""name"": string, ""weight"": integer, ""description"": string, ""excellent"": string, ""good"": string, ""fair"": string, ""poor"": string, ""checks"": string[] } ] }";

        private sealed class RubricSuggestionEnvelope
        {
            public List<CvRubricSuggestionItem>? Criteria { get; set; }
        }

        private const string InterviewRubricSuggestionInstruction = @"You help a Hiring Manager draft an INTERVIEW SCORING RUBRIC for one job opening. An AI interviewer holds a spoken interview with the candidate; this rubric is used to score the candidate's ANSWERS.
Rules:
- Return 4 to 6 criteria that can be judged FROM INTERVIEW ANSWERS for THIS role: depth of role-specific technical knowledge, problem solving and reasoning, practical experience shown through concrete examples, clarity of explanation, attitude and collaboration. Do NOT include criteria that can only be judged from documents (degrees, certificates, years written on the CV).
- NEVER use protected or discriminatory attributes (age, gender, marital status, religion, ethnicity, hometown, appearance, health) as criteria.
- Weights are integers that sum to exactly 100 and reflect importance for THIS role.
- Every text value is Vietnamese; keep technology names as-is.
- ""name"": short criterion name. ""description"": what a strong answer demonstrates (1-2 sentences).
- ""excellent"" (90-100), ""good"" (70-89), ""fair"" (40-69), ""poor"" (0-39): concrete, observable descriptions of answers at that level (correctness, depth, concrete examples, trade-offs considered) so two interviewers would pick the same band.
Return ONLY a valid JSON object, without markdown:
{ ""criteria"": [ { ""name"": string, ""weight"": integer, ""description"": string, ""excellent"": string, ""good"": string, ""fair"": string, ""poor"": string } ] }";

        public Task<Result<List<CvRubricSuggestionItem>>> SuggestCvRubricAsync(CvRubricSuggestionInput input, CancellationToken ct = default)
            => SuggestRubricAsync(RubricSuggestionInstruction, input, keepChecks: true, ct);

        public Task<Result<List<CvRubricSuggestionItem>>> SuggestInterviewRubricAsync(CvRubricSuggestionInput input, CancellationToken ct = default)
            => SuggestRubricAsync(InterviewRubricSuggestionInstruction, input, keepChecks: false, ct);

        private async Task<Result<List<CvRubricSuggestionItem>>> SuggestRubricAsync(
            string instruction, CvRubricSuggestionInput input, bool keepChecks, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Title))
                return Result<List<CvRubricSuggestionItem>>.Failure("Cần tên vị trí để gợi ý bộ tiêu chí.");

            var skills = input.Skills is { Count: > 0 } ? string.Join(", ", input.Skills) : "(không nêu)";
            var userContent =
                $"Vị trí: {input.Title}\n"
                + $"Cấp bậc: {input.ExperienceLevel ?? "(không nêu)"}\n"
                + $"Kỹ năng nêu trong tin: {skills}\n\n"
                + $"--- MÔ TẢ CÔNG VIỆC ---\n{input.Description ?? "(không có)"}\n\n"
                + $"--- YÊU CẦU ỨNG VIÊN ---\n{input.Requirements ?? "(không có)"}";

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = instruction } } },
                contents = new[] { new { parts = new[] { new { text = userContent } } } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.3 },
            };

            string responseJson;
            try
            {
                (responseJson, _) = await GetAnalysisJsonAsync(requestBody, instruction, userContent, null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Gemini + fallback OpenAI đều lỗi (gợi ý bộ tiêu chí).");
                return Result<List<CvRubricSuggestionItem>>.Failure($"Dịch vụ AI tạm thời không khả dụng: {ex.Message}");
            }

            try
            {
                var (json, _, _) = Unwrap(responseJson);
                var parsed = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<RubricSuggestionEnvelope>(json, ReadOpts);
                var items = parsed?.Criteria?.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList() ?? new();
                if (!keepChecks) foreach (var item in items) item.Checks = null;
                return items.Count == 0
                    ? Result<List<CvRubricSuggestionItem>>.Failure("AI chưa gợi ý được tiêu chí nào — hãy thử lại hoặc tự nhập.")
                    : Result<List<CvRubricSuggestionItem>>.Success(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không parse được gợi ý bộ tiêu chí. Raw: {RawResponse}", responseJson);
                return Result<List<CvRubricSuggestionItem>>.Failure("Không đọc được gợi ý của AI — hãy thử lại.");
            }
        }

        // ================================================================
        // Các tác vụ khác (không chấm điểm)
        // ================================================================

        public async Task<Result<CvReviewResultDto>> ReviewCvAsync(
            byte[]? cvFileBytes,
            string? cvMimeType,
            string? fallbackCvText,
            CancellationToken ct = default)
        {
            var systemInstruction = @"You are an expert technical recruiter reviewing a candidate's CV/Resume (no specific job description).
CRITICAL: First verify the document is actually a CV/Resume. If it is not, set 'is_valid_cv' to false.

If it IS a valid CV, analyze its contents to suggest suitable job positions, and provide constructive feedback on their strengths and specific areas to improve. Do not score the CV, do not rate the CV as good/bad/average, and do not make value judgments on the overall quality. Focus entirely on helpful guidance for the candidate.

You MUST return ONLY a valid JSON object matching this schema, in Vietnamese, without markdown formatting:
{
  ""is_valid_cv"": boolean,
  ""summary"": string (2-3 câu tóm tắt định hướng, nền tảng kỹ thuật và lĩnh vực hoạt động của ứng viên từ thông tin trong CV, không nhận xét tốt/tệ),
  ""suggested_positions"": string[] (3-5 vị trí công việc cụ thể phù hợp nhất với CV, ví dụ: 'Backend Developer (C#/.NET)', 'Frontend React Developer'),
  ""strengths"": string[] (3-5 điểm tốt, thế mạnh chuyên môn nổi bật của ứng viên trong CV),
  ""improvements"": string[] (3-6 gợi ý cải thiện cụ thể cho các điểm chưa tốt, chỉ rõ hành động/kỹ năng cần bổ sung để người dùng có định hướng rõ ràng),
  ""missing_sections"": string[] (các mục quan trọng còn thiếu trong CV như 'GitHub link', 'Mô tả dự án'. Để rỗng nếu đầy đủ)
}";

            var built = BuildDocumentParts("--- CANDIDATE CV ---", cvFileBytes, cvMimeType, fallbackCvText, "cv.pdf");
            if (built == null)
                return Result<CvReviewResultDto>.Failure("Either PDF file bytes or fallback text must be provided.");

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { parts = built.Value.Parts } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.2 },
            };

            string responseJson;
            string reviewProvider;
            try
            {
                (responseJson, reviewProvider) = await GetAnalysisJsonAsync(
                    requestBody, systemInstruction, $"--- CANDIDATE CV ---\n{fallbackCvText}", built.Value.Attachments, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Gemini + fallback OpenAI đều lỗi (CV review).");
                return Result<CvReviewResultDto>.Failure($"Dịch vụ AI tạm thời không khả dụng: {ex.Message}");
            }

            try
            {
                var (json, promptTokens, completionTokens) = Unwrap(responseJson);
                if (string.IsNullOrEmpty(json))
                    return Result<CvReviewResultDto>.Failure("Gemini returned empty text.");

                var result = JsonSerializer.Deserialize<CvReviewResultDto>(json, ReadOpts);
                if (result == null)
                    return Result<CvReviewResultDto>.Failure("Failed to deserialize Gemini CV review output.");

                result.PromptTokens = promptTokens;
                result.CompletionTokens = completionTokens;
                result.Provider = reviewProvider;
                return Result<CvReviewResultDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini CV review. Raw: {RawResponse}", responseJson);
                return Result<CvReviewResultDto>.Failure($"Failed to parse Gemini response: {ex.Message}");
            }
        }

        public async Task<Result<JdExtractionResultDto>> ExtractJobFromJdAsync(
            byte[]? jdFileBytes,
            string? jdMimeType,
            string? fallbackJdText,
            CancellationToken ct = default)
        {
            var systemInstruction = @"You are an expert IT recruiter assistant. You read a Job Description (JD) document and extract structured fields to pre-fill a job posting form.
CRITICAL: First verify the document is actually a Job Description. If it is not, set 'is_valid_jd' to false and leave the other fields empty/null.

Map values to these EXACT enums (lowercase, English) when applicable, otherwise null:
- job_category: backend | frontend | devops | qa | data | ai_ml | mobile | pm | designer | other
- experience_level: intern | fresher | junior | middle | senior | lead | manager
- employment_type: full_time | part_time | contract | internship | freelance
- work_mode: onsite | hybrid | remote

Rules:
- 'title' is the job position name (e.g. ""Backend Developer (.NET)"").
- 'job_description' MUST be a clean, well-structured HTML version of the JD body, structured clearly into logical sections depending on the JD (e.g. <strong>Mô tả công việc</strong>, <strong>Yêu cầu công việc</strong>, <strong>Quyền lợi</strong>). You MUST format all section headers inside <strong> tags (e.g. <strong>Mô tả công việc</strong>) and format lists using standard HTML tags: <ul> and <li> (e.g. <ul><li>Thiết kế phát triển...</li></ul>). Use <p> and <br /> tags for clean spacing and paragraphs. Do NOT use markdown symbols like **, *, or - for formatting. Keep the original language of the JD.
  CRITICAL CONTACT RULE: You MUST EXCLUDE any company contact information (such as candidate application submission emails, HR contact names, phone numbers, mail subject formats, or call-to-actions like ""Liên hệ nộp hồ sơ qua email...""). If this contact section is at the end of the JD, stop extracting before it.
- 'skills' is an array of concrete technical skills/tools mentioned (keep proper names: C#, .NET, React, PostgreSQL...). Max 15.
- 'language_requirement' ONLY if the JD explicitly requires a foreign language proficiency (e.g. ""English (TOEIC > 700)""). If the JD is Vietnamese with no foreign-language requirement, set null.
- 'interview_language' is the language the AI interview rounds should be held in: ""en"" if the JD is written in English OR explicitly requires working/communicating in English, otherwise ""vi"". Only ""vi"" or ""en"".
- 'salary_min'/'salary_max': Extract the salary range.
  CRITICAL SALARY RULE: If the JD mentions an active starting/training salary/allowance (e.g. ""Trợ cấp đào tạo 6,000,000 – 8,000,000 VNĐ/tháng"") AND a prospective/potential future salary after contract/training (e.g. ""cơ hội ký hợp đồng chính thức với mức thu nhập trung bình từ 12.000.000 VNĐ - 15.000.000 VNĐ/tháng""), you MUST extract the active starting/training salary (e.g., min: 6000000, max: 8000000). Do NOT extract the potential/future contract salary.
  If the original JD states the salary in USD or other currencies, you MUST automatically convert it to VND (Vietnamese Dong) using the current approximate rate (e.g. 1 USD = 25,000 VND). Round the final converted value to the nearest million VND (e.g. 37,500,000 VND should be rounded to 38,000,000 VND, 15,300,000 VND should be rounded to 15,000,000 VND) and output it as a plain number (e.g. 38000000). If the original JD is in VND, keep it in VND but still round it to the nearest million VND. If no salary is explicitly stated, set them to null. Do not invent.
- Only fill a field if you are confident it is in the JD; otherwise use null (or empty array for skills).

You MUST return ONLY a valid JSON object matching this schema, without markdown formatting:
{
  ""is_valid_jd"": boolean,
  ""title"": string|null,
  ""department"": string|null,
  ""job_description"": string|null,
  ""job_category"": string|null,
  ""experience_level"": string|null,
  ""employment_type"": string|null,
  ""work_mode"": string|null,
  ""location"": string|null,
  ""skills"": string[],
  ""language_requirement"": string|null,
  ""interview_language"": ""vi""|""en"",
  ""salary_min"": number|null,
  ""salary_max"": number|null
}";

            var built = BuildDocumentParts("--- JOB DESCRIPTION DOCUMENT ---", jdFileBytes, jdMimeType, fallbackJdText, "jd.pdf");
            if (built == null)
                return Result<JdExtractionResultDto>.Failure("Either PDF file bytes or fallback text must be provided.");

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { parts = built.Value.Parts } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.1 },
            };

            string responseJson;
            try
            {
                (responseJson, _) = await GetAnalysisJsonAsync(
                    requestBody, systemInstruction, $"--- JOB DESCRIPTION ---\n{fallbackJdText}", built.Value.Attachments, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Gemini + fallback OpenAI đều lỗi (JD extraction).");
                return Result<JdExtractionResultDto>.Failure($"Dịch vụ AI tạm thời không khả dụng: {ex.Message}");
            }

            try
            {
                var (json, promptTokens, completionTokens) = Unwrap(responseJson);
                if (string.IsNullOrEmpty(json))
                    return Result<JdExtractionResultDto>.Failure("Gemini returned empty text.");

                var result = JsonSerializer.Deserialize<JdExtractionResultDto>(json, ReadOpts);
                if (result == null)
                    return Result<JdExtractionResultDto>.Failure("Failed to deserialize Gemini JD extraction output.");

                result.PromptTokens = promptTokens;
                result.CompletionTokens = completionTokens;
                return Result<JdExtractionResultDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini JD extraction. Raw: {RawResponse}", responseJson);
                return Result<JdExtractionResultDto>.Failure($"Failed to parse Gemini response: {ex.Message}");
            }
        }

        public async Task<Result<CvContactVerificationResultDto>> VerifyCvContactInfoAsync(
            byte[]? cvFileBytes,
            string? cvMimeType,
            string? fallbackCvText,
            string formName,
            string formPhone,
            string formEmail,
            CancellationToken ct = default)
        {
            var systemInstruction = $@"You are an AI assistant verifying CV contact details against a job application form.
You are given a candidate's CV and the contact details they entered in the form:
- Full Name: {formName}
- Phone: {formPhone}
- Email: {formEmail}

Your task is to extract the name, phone number, and email address from the CV, and check if they match the form values.
Rules for matching:
- Name: Check if the name in the CV is substantially the same as the name in the form (ignore accent variations, casing, or middle names slightly formatted differently, e.g., 'Nguyen Van A' and 'Nguyễn Văn A' match).
- Phone: Check if the phone number in the CV matches the form (ignore formatting like spaces, hyphens, country code like +84 vs 0, e.g., '+84912345678' and '0912345678' match). If the CV has no phone number, flag it.
- Email: Check if the email address in the CV matches the form (case-insensitive). If the CV has no email address, flag it.

If any of these fields mismatch or are missing in the CV, set 'is_match' to false and provide clear, polite, and detailed warning details in Vietnamese in 'mismatch_details' explaining exactly which fields are mismatched. For each mismatched field, specify BOTH the value entered in the form and the value extracted from the CV.
For example:
- 'Họ tên đã nhập ({formName}) khác với họ tên trong CV (Trần Văn B).'
- 'Số điện thoại đã nhập ({formPhone}) khác với số điện thoại trong CV (0987654321).'
- 'Không tìm thấy thông tin email hoặc số điện thoại trong file CV.'
If everything matches, set 'is_match' to true and 'mismatch_details' to null.

You MUST return ONLY a valid JSON object matching this schema, without markdown formatting:
{{
  ""is_match"": boolean,
  ""mismatch_details"": string|null
}}";

            var built = BuildDocumentParts("--- CANDIDATE CV ---", cvFileBytes, cvMimeType, fallbackCvText, "cv.pdf");
            if (built == null)
                return Result<CvContactVerificationResultDto>.Failure("Either PDF file bytes or fallback text must be provided.");

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { parts = built.Value.Parts } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.1 },
            };

            string responseJson;
            try
            {
                (responseJson, _) = await GetAnalysisJsonAsync(
                    requestBody, systemInstruction, $"--- CANDIDATE CV ---\n{fallbackCvText}", built.Value.Attachments, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Gemini + fallback OpenAI đều lỗi (CV contact verification).");
                return Result<CvContactVerificationResultDto>.Failure($"Dịch vụ AI tạm thời không khả dụng: {ex.Message}");
            }

            try
            {
                var (json, _, _) = Unwrap(responseJson);
                if (string.IsNullOrEmpty(json))
                    return Result<CvContactVerificationResultDto>.Failure("Gemini returned empty text.");

                var result = JsonSerializer.Deserialize<CvContactVerificationResultDto>(json, ReadOpts);
                if (result == null)
                    return Result<CvContactVerificationResultDto>.Failure("Failed to deserialize Gemini CV contact verification output.");

                return Result<CvContactVerificationResultDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini CV contact verification. Raw: {RawResponse}", responseJson);
                return Result<CvContactVerificationResultDto>.Failure($"Failed to parse Gemini response: {ex.Message}");
            }
        }

        /// <summary>
        /// Phần nội dung cho một tài liệu đơn (CV hoặc JD): PDF gửi nguyên file (kèm text dự phòng),
        /// loại khác gửi text. Trả kèm danh sách file để đường dự phòng cũng đọc được file gốc.
        /// Null khi không có gì để gửi.
        /// </summary>
        private static (List<object> Parts, List<AiAttachment> Attachments)? BuildDocumentParts(
            string header, byte[]? fileBytes, string? mimeType, string? fallbackText, string fileName)
        {
            var parts = new List<object> { new { text = header } };
            var attachments = new List<AiAttachment>();

            if (fileBytes is { Length: > 0 } && mimeType == PdfMime)
            {
                var file = new AiAttachment(fileName, PdfMime, fileBytes);
                parts.Add(InlinePdf(file));
                attachments.Add(file);
                if (!string.IsNullOrEmpty(fallbackText))
                    parts.Add(new { text = "\n(Fallback Extracted Text):\n" + fallbackText });
            }
            else if (!string.IsNullOrEmpty(fallbackText))
            {
                parts.Add(new { text = fallbackText });
            }
            else
            {
                return null;
            }

            return (parts, attachments);
        }
    }
}
