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

            var systemInstruction = @"You are an expert Technical Recruiter and HR Professional.
Your task is to analyze a candidate's CV against a Job Description (JD).
You MUST return ONLY a valid JSON object matching this schema, without any markdown formatting like ```json.
{
  ""match_score"": int (0-100),
  ""summary"": string (Brief explanation of the score),
  ""skills_matched"": string[],
  ""skills_gaps"": string[],
  ""red_flags"": string[] (Any career gaps, job hopping, or suspicious claims),
  ""experience_relevance"": string (How relevant their past roles are),
  ""overall_recommendation"": string (e.g., 'Strong Hire', 'Proceed with caution', 'Reject')
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
                    responseMimeType = "application/json"
                }
            };

            var sw = Stopwatch.StartNew();
            
            var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
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
    }
}
