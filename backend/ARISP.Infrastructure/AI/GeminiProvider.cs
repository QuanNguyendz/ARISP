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

You MUST return ONLY a valid JSON object matching this schema, without markdown formatting.
{
  ""is_valid_cv"": boolean,
  ""analysis_reasoning"": string (Your step-by-step reasoning),
  ""seniority_alignment"": string (Directly analyze the gap between JD seniority and CV seniority),
  ""tech_depth_analysis"": string (Evaluate production depth vs surface-level knowledge),
  ""match_score"": int (0-100),
  ""summary"": string (Write a detailed summary. Format exactly as 2 paragraphs. Paragraph 1 starting with '🌟 Điểm sáng (Strengths):' highlighting good points. Paragraph 2 starting with '⚠️ Điểm thiếu sót nghiêm trọng (Critical Gaps):' highlighting why they fall short of the JD requirements.),
  ""skills_matched"": string[] (List matched skills with years of exp),
  ""skills_gaps"": string[] (Crucial skills missing),
  ""red_flags"": string[] (Career gaps or suspicious claims. Empty if none),
  ""experience_relevance"": string (How their domain fits the JD),
  ""overall_recommendation"": string ('Strong Hire', 'Hire', 'Proceed with caution', 'Reject')
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
    }
}
