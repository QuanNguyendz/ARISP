using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Common;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ARISP.Infrastructure.AI
{
    public class GeminiProvider : IGeminiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiProvider> _logger;
        private readonly string _apiKey;

        public GeminiProvider(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["GEMINI_API_KEY"] ?? string.Empty;
        }

        public async Task<Result<CvJdAnalysisResultDto>> AnalyzeCvJdMatchAsync(
            string jdText, 
            byte[]? cvFileBytes, 
            string? cvMimeType, 
            string? fallbackCvText,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return Result<CvJdAnalysisResultDto>.Failure("GEMINI_API_KEY is not configured.");
            }

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var systemInstruction = @"You are an expert Headhunter and Tech Lead.
Your task is to analyze a candidate's CV against a Job Description (JD).
CRITICAL INSTRUCTION: You must STRICTLY verify if the document is actually a Resume/CV. If irrelevant, you MUST set 'is_valid_cv' to false, 'match_score' to 0.

If it IS a valid CV, employ Chain-of-Thought reasoning:
1. Identify Seniority Required in JD (Fresher, Junior, Mid, Senior).
2. Calculate candidate's Professional Experience. CRITICAL RULE: Academic projects and short internships DO NOT count towards professional experience for Senior roles.
3. PENALTY RULE: If JD requires Senior (e.g., 4+ years) and CV is Fresher/Intern (< 1 year), 'match_score' MUST NOT exceed 30%, regardless of keyword matches.
4. Depth Check: Evaluate if they have hands-on production depth (e.g. building RAG, Vector DBs, System Optimization) or just surface-level API usage.

CRITICAL LANGUAGE RULE: EVERY text value in the JSON (summary, skills_matched, skills_gaps, red_flags, experience_relevance, analysis_reasoning, seniority_alignment, tech_depth_analysis) MUST be written in VIETNAMESE (tiếng Việt) — regardless of the language of the CV or JD. Only keep proper nouns / technical terms as-is (e.g. C#, .NET, PostgreSQL, React, RAG, Vector DB). Do NOT write these fields in English.

You MUST return ONLY a valid JSON object matching this schema, without markdown formatting.
{
  ""is_valid_cv"": boolean,
  ""analysis_reasoning"": string (Tiếng Việt — lập luận từng bước),
  ""seniority_alignment"": string (Tiếng Việt — phân tích khoảng cách cấp bậc giữa JD và CV),
  ""tech_depth_analysis"": string (Tiếng Việt — đánh giá chiều sâu thực chiến vs kiến thức bề mặt),
  ""match_score"": int (0-100),
  ""summary"": string (Tiếng Việt. Định dạng ĐÚNG 2 đoạn, mỗi đoạn nằm trên một dòng riêng, ngăn cách bằng ký tự xuống dòng '\n'. Đoạn 1 bắt đầu bằng '🌟 Điểm sáng: ' nêu ưu điểm. Đoạn 2 bắt đầu bằng '⚠️ Điểm cần lưu ý: ' nêu vì sao chưa đạt yêu cầu của JD. Mỗi đoạn 2-4 câu, súc tích.),
  ""skills_matched"": string[] (Tiếng Việt — mỗi phần tử là MỘT kỹ năng khớp, kèm mức độ ngắn gọn trong ngoặc, ví dụ ""C# (cơ bản, qua thực tập)"". Giữ tên công nghệ nguyên gốc.),
  ""skills_gaps"": string[] (Tiếng Việt — mỗi phần tử là MỘT kỹ năng/kinh nghiệm còn thiếu, ngắn gọn. Giữ tên công nghệ nguyên gốc.),
  ""red_flags"": string[] (Tiếng Việt — khoảng trống sự nghiệp hoặc điểm đáng ngờ. Để mảng rỗng nếu không có.),
  ""experience_relevance"": string (Tiếng Việt — mức độ phù hợp lĩnh vực với JD),
  ""overall_recommendation"": string (CHỈ chọn đúng một trong các giá trị tiếng Anh sau: 'Strong Hire', 'Hire', 'Proceed with caution', 'Reject')
}";

            var parts = new List<object>
            {
                new { text = $"--- JOB DESCRIPTION ---\n{jdText}\n\n--- CANDIDATE CV ---" }
            };

            // Use Multimodal (PDF) if available, otherwise use fallback text
            if (cvFileBytes != null && cvFileBytes.Length > 0 && cvMimeType == "application/pdf")
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = "application/pdf",
                        data = Convert.ToBase64String(cvFileBytes)
                    }
                });
                
                if (!string.IsNullOrEmpty(fallbackCvText))
                {
                    parts.Add(new { text = "\n(Fallback Extracted Text in case PDF parsing fails):\n" + fallbackCvText });
                }
            }
            else if (!string.IsNullOrEmpty(fallbackCvText))
            {
                parts.Add(new { text = fallbackCvText });
            }
            else
            {
                return Result<CvJdAnalysisResultDto>.Failure("Either PDF file bytes or fallback text must be provided.");
            }

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemInstruction } }
                },
                contents = new[]
                {
                    new { parts = parts }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0.0
                }
            };

            var sw = Stopwatch.StartNew();
            
            string responseJson = string.Empty;
            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, ct);
                response.EnsureSuccessStatusCode();
                responseJson = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API HTTP Request failed.");
                return Result<CvJdAnalysisResultDto>.Failure($"Gemini API tạm thời không khả dụng: {ex.Message}");
            }
            sw.Stop();
            
            _logger.LogInformation($"Gemini API call completed in {sw.ElapsedMilliseconds}ms");

            try
            {
                // Parse the Gemini Response structure
                using var document = JsonDocument.Parse(responseJson);
                var root = document.RootElement;
                
                var candidates = root.GetProperty("candidates");
                var firstCandidate = candidates[0];
                var content = firstCandidate.GetProperty("content");
                var responseParts = content.GetProperty("parts");
                var firstPart = responseParts[0];
                var rawJsonString = firstPart.GetProperty("text").GetString();

                if (string.IsNullOrEmpty(rawJsonString))
                {
                    return Result<CvJdAnalysisResultDto>.Failure("Gemini returned empty text.");
                }

                // Extract Usage Metadata
                int promptTokens = 0, completionTokens = 0;
                if (root.TryGetProperty("usageMetadata", out var usageProp))
                {
                    if (usageProp.TryGetProperty("promptTokenCount", out var pCount)) promptTokens = pCount.GetInt32();
                    if (usageProp.TryGetProperty("candidatesTokenCount", out var cCount)) completionTokens = cCount.GetInt32();
                }

                // Remove markdown code blocks if Gemini ignores responseMimeType
                if (rawJsonString.StartsWith("```json"))
                {
                    rawJsonString = rawJsonString.Substring(7);
                    if (rawJsonString.EndsWith("```"))
                    {
                        rawJsonString = rawJsonString.Substring(0, rawJsonString.Length - 3);
                    }
                }
                rawJsonString = rawJsonString.Trim();

                var result = JsonSerializer.Deserialize<CvJdAnalysisResultDto>(rawJsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null)
                {
                    return Result<CvJdAnalysisResultDto>.Failure("Failed to deserialize Gemini JSON output.");
                }

                // Gắn Telemetry vào DTO để trả về cho ApplicationService
                result.RawResponse = responseJson;
                result.ProcessingTimeMs = (int)sw.ElapsedMilliseconds;
                result.PromptTokens = promptTokens;
                result.CompletionTokens = completionTokens;

                return Result<CvJdAnalysisResultDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini response. Raw Response: {RawResponse}", responseJson);
                return Result<CvJdAnalysisResultDto>.Failure($"Failed to parse Gemini response: {ex.Message}");
            }
        }

        public async Task<Result<CvReviewResultDto>> ReviewCvAsync(
            byte[]? cvFileBytes,
            string? cvMimeType,
            string? fallbackCvText,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
                return Result<CvReviewResultDto>.Failure("GEMINI_API_KEY is not configured.");

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var systemInstruction = @"You are an expert technical recruiter reviewing a candidate's CV/Resume (no specific job description).
CRITICAL: First verify the document is actually a CV/Resume. If it is not, set 'is_valid_cv' to false and 'overall_score' to 0.

If it IS a valid CV, evaluate its overall quality as a professional IT resume: clarity, completeness, impact of achievements (quantified results), technical depth, and presentation.

You MUST return ONLY a valid JSON object matching this schema, in Vietnamese, without markdown formatting:
{
  ""is_valid_cv"": boolean,
  ""overall_score"": int (0-100, overall CV quality),
  ""verdict"": string (one of: ""Đạt chuẩn"", ""Khá - cần chỉnh sửa nhẹ"", ""Cần cải thiện"", ""Chưa đạt""),
  ""summary"": string (2-3 câu nhận xét tổng quan về CV),
  ""strengths"": string[] (3-5 điểm mạnh nổi bật của CV),
  ""improvements"": string[] (3-6 gợi ý cụ thể để cải thiện CV),
  ""missing_sections"": string[] (các mục quan trọng còn thiếu, ví dụ: 'Kết quả định lượng', 'Liên kết GitHub'. Để rỗng nếu đầy đủ)
}";

            var parts = new List<object>
            {
                new { text = "--- CANDIDATE CV ---" }
            };

            if (cvFileBytes != null && cvFileBytes.Length > 0 && cvMimeType == "application/pdf")
            {
                parts.Add(new { inline_data = new { mime_type = "application/pdf", data = Convert.ToBase64String(cvFileBytes) } });
                if (!string.IsNullOrEmpty(fallbackCvText))
                    parts.Add(new { text = "\n(Fallback Extracted Text):\n" + fallbackCvText });
            }
            else if (!string.IsNullOrEmpty(fallbackCvText))
            {
                parts.Add(new { text = fallbackCvText });
            }
            else
            {
                return Result<CvReviewResultDto>.Failure("Either PDF file bytes or fallback text must be provided.");
            }

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { parts = parts } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.2 }
            };

            string responseJson = string.Empty;
            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, ct);
                response.EnsureSuccessStatusCode();
                responseJson = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini CV review HTTP request failed.");
                return Result<CvReviewResultDto>.Failure($"Gemini API tạm thời không khả dụng: {ex.Message}");
            }

            try
            {
                using var document = JsonDocument.Parse(responseJson);
                var root = document.RootElement;
                var rawJsonString = root.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                if (string.IsNullOrEmpty(rawJsonString))
                    return Result<CvReviewResultDto>.Failure("Gemini returned empty text.");

                int promptTokens = 0, completionTokens = 0;
                if (root.TryGetProperty("usageMetadata", out var usageProp))
                {
                    if (usageProp.TryGetProperty("promptTokenCount", out var pCount)) promptTokens = pCount.GetInt32();
                    if (usageProp.TryGetProperty("candidatesTokenCount", out var cCount)) completionTokens = cCount.GetInt32();
                }

                if (rawJsonString.StartsWith("```json"))
                {
                    rawJsonString = rawJsonString.Substring(7);
                    if (rawJsonString.EndsWith("```")) rawJsonString = rawJsonString.Substring(0, rawJsonString.Length - 3);
                }
                rawJsonString = rawJsonString.Trim();

                var result = JsonSerializer.Deserialize<CvReviewResultDto>(rawJsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result == null)
                    return Result<CvReviewResultDto>.Failure("Failed to deserialize Gemini CV review output.");

                result.PromptTokens = promptTokens;
                result.CompletionTokens = completionTokens;
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
            if (string.IsNullOrEmpty(_apiKey))
                return Result<JdExtractionResultDto>.Failure("GEMINI_API_KEY is not configured.");

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var systemInstruction = @"You are an expert IT recruiter assistant. You read a Job Description (JD) document and extract structured fields to pre-fill a job posting form.
CRITICAL: First verify the document is actually a Job Description. If it is not, set 'is_valid_jd' to false and leave the other fields empty/null.

Map values to these EXACT enums (lowercase, English) when applicable, otherwise null:
- job_category: backend | frontend | devops | qa | data | ai_ml | mobile | pm | designer | other
- experience_level: intern | fresher | junior | middle | senior | lead | manager
- employment_type: full_time | part_time | contract | internship | freelance
- work_mode: onsite | hybrid | remote

Rules:
- 'title' is the job position name (e.g. ""Backend Developer (.NET)"").
- 'job_description' MUST be a clean, well-structured plain-text version of the JD body (responsibilities + requirements). Keep the original language of the JD.
- 'skills' is an array of concrete technical skills/tools mentioned (keep proper names: C#, .NET, React, PostgreSQL...). Max 15.
- 'language_requirement' ONLY if the JD explicitly requires a foreign language proficiency (e.g. ""English (TOEIC > 700)""). If the JD is Vietnamese with no foreign-language requirement, set null.
- 'salary_min'/'salary_max' as numbers ONLY if explicitly stated; otherwise null. Do not invent.
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
  ""salary_min"": number|null,
  ""salary_max"": number|null
}";

            var parts = new List<object>
            {
                new { text = "--- JOB DESCRIPTION DOCUMENT ---" }
            };

            if (jdFileBytes != null && jdFileBytes.Length > 0 && jdMimeType == "application/pdf")
            {
                parts.Add(new { inline_data = new { mime_type = "application/pdf", data = Convert.ToBase64String(jdFileBytes) } });
                if (!string.IsNullOrEmpty(fallbackJdText))
                    parts.Add(new { text = "\n(Fallback Extracted Text):\n" + fallbackJdText });
            }
            else if (!string.IsNullOrEmpty(fallbackJdText))
            {
                parts.Add(new { text = fallbackJdText });
            }
            else
            {
                return Result<JdExtractionResultDto>.Failure("Either PDF file bytes or fallback text must be provided.");
            }

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { parts = parts } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.1 }
            };

            string responseJson = string.Empty;
            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, ct);
                response.EnsureSuccessStatusCode();
                responseJson = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini JD extraction HTTP request failed.");
                return Result<JdExtractionResultDto>.Failure($"Gemini API tạm thời không khả dụng: {ex.Message}");
            }

            try
            {
                using var document = JsonDocument.Parse(responseJson);
                var root = document.RootElement;
                var rawJsonString = root.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                if (string.IsNullOrEmpty(rawJsonString))
                    return Result<JdExtractionResultDto>.Failure("Gemini returned empty text.");

                int promptTokens = 0, completionTokens = 0;
                if (root.TryGetProperty("usageMetadata", out var usageProp))
                {
                    if (usageProp.TryGetProperty("promptTokenCount", out var pCount)) promptTokens = pCount.GetInt32();
                    if (usageProp.TryGetProperty("candidatesTokenCount", out var cCount)) completionTokens = cCount.GetInt32();
                }

                if (rawJsonString.StartsWith("```json"))
                {
                    rawJsonString = rawJsonString.Substring(7);
                    if (rawJsonString.EndsWith("```")) rawJsonString = rawJsonString.Substring(0, rawJsonString.Length - 3);
                }
                rawJsonString = rawJsonString.Trim();

                var result = JsonSerializer.Deserialize<JdExtractionResultDto>(rawJsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
    }
}
