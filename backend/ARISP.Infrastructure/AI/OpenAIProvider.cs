using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;
using ARISP.Infrastructure.Data;

namespace ARISP.Infrastructure.AI
{
    public class OpenAIProvider : IAIProvider, IEmbeddingProvider
    {
        private readonly ARISPDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly bool _useLocalMock;

        public OpenAIProvider(ARISPDbContext context, IConfiguration configuration, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
            _apiKey = configuration["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            _useLocalMock = configuration["AI:Provider"] == "local" || string.IsNullOrEmpty(_apiKey);
        }

        // --- IEmbeddingProvider ---

        public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            if (_useLocalMock)
            {
                // Return a mock vector of size 1536
                var mockVector = new float[1536];
                var rand = new Random(text.GetHashCode());
                for (int i = 0; i < mockVector.Length; i++) mockVector[i] = (float)rand.NextDouble();
                return mockVector;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            var payload = new { input = text, model = "text-embedding-3-small" };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseString);
            var embeddingArray = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");

            var vector = new float[embeddingArray.GetArrayLength()];
            int idx = 0;
            foreach (var val in embeddingArray.EnumerateArray())
            {
                vector[idx++] = (float)val.GetDouble();
            }

            return vector;
        }

        public async Task<IEnumerable<DocumentChunk>> RetrieveAsync(Guid? sourceId, float[] queryVector, int topK = 5, CancellationToken ct = default)
        {
            // Convert vector to pgvector string representation: [0.1, 0.2, 0.3...]
            var vectorString = $"[{string.Join(",", queryVector)}]";

            // Cosine distance <=> is Cosine Distance. We want the nearest.
            if (sourceId.HasValue)
            {
                return await _context.DocumentChunks
                    .FromSqlRaw("SELECT * FROM document_chunks WHERE source_id = {0} ORDER BY embedding <=> {1}::vector LIMIT {2}",
                        sourceId.Value, vectorString, topK)
                    .ToListAsync(ct);
            }
            else
            {
                return await _context.DocumentChunks
                    .FromSqlRaw("SELECT * FROM document_chunks ORDER BY embedding <=> {0}::vector LIMIT {1}",
                        vectorString, topK)
                    .ToListAsync(ct);
            }
        }

        // --- IAIProvider ---

        public async Task<string> DetectLanguageRequirementAsync(string jdText, CancellationToken ct)
        {
            if (_useLocalMock)
            {
                if (jdText.Contains("English", StringComparison.OrdinalIgnoreCase) || jdText.Contains("tiếng Anh", StringComparison.OrdinalIgnoreCase))
                    return "en";
                if (jdText.Contains("Japanese", StringComparison.OrdinalIgnoreCase) || jdText.Contains("tiếng Nhật", StringComparison.OrdinalIgnoreCase))
                    return "ja";
                return "vi";
            }

            var prompt = $"Analyze the following job description and detect if there is a primary foreign language requirement (like English, Japanese, Korean) for interviews. Reply only with the language code (e.g. 'en', 'ja', 'ko', 'vi'):\n\n{jdText}";
            return await CallOpenAIChatAsync(prompt, ct);
        }

        public async IAsyncEnumerable<string> StreamQuestionAsync(QuestionContext ctx, CancellationToken ct)
        {
            if (_useLocalMock)
            {
                // Generate a mock question based on history and type
                var mockQuestions = new[]
                {
                    "Bạn có thể giới thiệu bản thân và kinh nghiệm phát triển Backend nổi bật nhất của mình không?",
                    "Trong CV bạn có đề cập làm việc với PostgreSQL, bạn đã tối ưu hóa các câu lệnh query chậm như thế nào?",
                    "Bạn xử lý thế nào khi có bất đồng ý kiến về thiết kế hệ thống với Technical Lead?",
                    "Mô hình Clean Architecture mang lại những lợi ích và khó khăn gì trong các dự án thực tế của bạn?",
                    "Hãy chia sẻ một lỗi nghiêm trọng trên production bạn từng gặp và cách bạn debug xử lý nó."
                };

                var sequenceIndex = (ctx.ChatHistory.Count) % mockQuestions.Length;
                var baseQuestion = mockQuestions[sequenceIndex];

                if (ctx.MustAskQuestions.Count > 0)
                {
                    baseQuestion = $"[Must Ask] {ctx.MustAskQuestions[0]}";
                }

                // Simulate token streaming
                var words = baseQuestion.Split(' ');
                foreach (var word in words)
                {
                    yield return word + " ";
                    await Task.Delay(50, ct);
                }
                yield break;
            }

            // Real Streaming using OpenAI Server-Sent Events (SSE)
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var systemPrompt = "You are a professional HR and Technical AI Interviewer. Generate a suitable interview question based on the Job Description, Candidate CV, and current Chat History. Adjust difficulty adaptively. Be polite and concise.";
            if (ctx.PlaybookStyleGuides.Count > 0)
            {
                systemPrompt += "\nAdhere to company interview playbook style:\n" + string.Join("\n", ctx.PlaybookStyleGuides);
            }

            var userContent = $"Job Description: {ctx.JobDescription}\nCandidate CV: {ctx.CandidateCv}\n";
            if (ctx.MustAskQuestions.Count > 0)
            {
                userContent += $"MUST-ASK Question to include: {ctx.MustAskQuestions[0]}\n";
            }

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            };

            foreach (var history in ctx.ChatHistory)
            {
                messages.Add(new { role = "assistant", content = history.QuestionText });
                messages.Add(new { role = "user", content = history.AnswerText });
            }

            var payload = new
            {
                model = "gpt-4o",
                messages = messages,
                stream = true
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
                
                var data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                using var doc = JsonDocument.Parse(data);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var contentElement))
                    {
                        yield return contentElement.GetString() ?? "";
                    }
                }
            }
        }

        public async Task<AnswerAnalysis> AnalyzeAnswerAsync(AnswerContext ctx, CancellationToken ct)
        {
            if (_useLocalMock)
            {
                return new AnswerAnalysis
                {
                    DifficultyLevel = 3,
                    Feedback = "Câu trả lời của ứng viên có cấu trúc tốt, thể hiện kiến thức cơ bản tốt."
                };
            }

            var prompt = $"Analyze the candidate's answer for accuracy and communication quality.\nQuestion: {ctx.QuestionText}\nAnswer: {ctx.AnswerTranscript}\n\nReturn JSON only in this format: {{\"DifficultyLevel\": 3, \"Feedback\": \"your feedback string\"}}";
            
            var jsonResponse = await CallOpenAIChatAsync(prompt, ct, jsonMode: true);
            using var doc = JsonDocument.Parse(jsonResponse);
            return new AnswerAnalysis
            {
                DifficultyLevel = doc.RootElement.GetProperty("DifficultyLevel").GetInt32(),
                Feedback = doc.RootElement.GetProperty("Feedback").GetString() ?? ""
            };
        }

        public async Task<EvaluationReport> GenerateEvaluationAsync(SessionContext ctx, CancellationToken ct)
        {
            if (_useLocalMock)
            {
                return new EvaluationReport
                {
                    Verdict = "pass",
                    Score = 85.5m,
                    Reasoning = "Ứng viên thể hiện năng lực kỹ thuật xuất sắc, có khả năng giải quyết vấn đề tốt qua các câu trả lời thực tế.",
                    RecommendedNextStep = "Mời ứng viên tham gia vòng phỏng vấn chuyên sâu Technical Deep-dive.",
                    CriterionScoresJson = "{\"technical\":88,\"communication\":82,\"culture_fit\":85}",
                    QuestionAnalysesJson = "[]"
                };
            }

            var prompt = $"You are an HR director. Evaluate the candidate's interview session based on Job Description, CV, and QA History.\nJob: {ctx.JobDescription}\nCV: {ctx.CandidateCv}\n\n" +
                         $"QA History: {JsonSerializer.Serialize(ctx.ChatHistory)}\n\n" +
                         $"Evaluate according to scoring rubrics. Return JSON format only: {{\"Verdict\": \"pass\"|\"not_pass\", \"Score\": 85.0, \"Reasoning\": \"overall text\", \"RecommendedNextStep\": \"text\", \"CriterionScores\": {{\"technical\": 80}}, \"QuestionAnalyses\": []}}";

            var jsonResponse = await CallOpenAIChatAsync(prompt, ct, jsonMode: true);
            using var doc = JsonDocument.Parse(jsonResponse);
            
            return new EvaluationReport
            {
                Verdict = doc.RootElement.GetProperty("Verdict").GetString() ?? "not_pass",
                Score = doc.RootElement.GetProperty("Score").GetDecimal(),
                Reasoning = doc.RootElement.GetProperty("Reasoning").GetString() ?? "",
                RecommendedNextStep = doc.RootElement.GetProperty("RecommendedNextStep").GetString() ?? "",
                CriterionScoresJson = doc.RootElement.GetProperty("CriterionScores").ToString(),
                QuestionAnalysesJson = doc.RootElement.GetProperty("QuestionAnalyses").ToString()
            };
        }

        public async Task<LanguageAssessment> AssessLanguageProficiencyAsync(SessionContext ctx, CancellationToken ct)
        {
            if (_useLocalMock)
            {
                return new LanguageAssessment
                {
                    Fluency = 8.0m,
                    Grammar = 7.5m,
                    Vocabulary = 8.0m,
                    Comprehension = 8.5m,
                    OverallScore = 8.0m
                };
            }

            var prompt = $"Assess candidate language proficiency based on the conversation history:\n{JsonSerializer.Serialize(ctx.ChatHistory)}\n\n" +
                         $"Return JSON format only: {{\"Fluency\": 8.0, \"Grammar\": 7.5, \"Vocabulary\": 8.0, \"Comprehension\": 8.5, \"OverallScore\": 8.0}}";

            var jsonResponse = await CallOpenAIChatAsync(prompt, ct, jsonMode: true);
            using var doc = JsonDocument.Parse(jsonResponse);

            return new LanguageAssessment
            {
                Fluency = doc.RootElement.GetProperty("Fluency").GetDecimal(),
                Grammar = doc.RootElement.GetProperty("Grammar").GetDecimal(),
                Vocabulary = doc.RootElement.GetProperty("Vocabulary").GetDecimal(),
                Comprehension = doc.RootElement.GetProperty("Comprehension").GetDecimal(),
                OverallScore = doc.RootElement.GetProperty("OverallScore").GetDecimal()
            };
        }

        // Fallback structured-JSON completion (dùng khi Gemini lỗi). GPT-4o-mini rẻ + nhanh,
        // response_format json_object đảm bảo trả JSON hợp lệ đúng schema trong systemInstruction.
        public async Task<string> CompleteJsonAsync(string systemInstruction, string userContent, CancellationToken ct = default)
        {
            if (_useLocalMock)
                throw new InvalidOperationException("OpenAI fallback chưa được cấu hình (thiếu OPENAI_API_KEY).");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = systemInstruction },
                    new { role = "user", content = userContent },
                },
                temperature = 0.2,
                response_format = new { type = "json_object" },
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        private async Task<string> CallOpenAIChatAsync(string prompt, CancellationToken ct, bool jsonMode = false)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var payload = new
            {
                model = "gpt-4o",
                messages = new[] { new { role = "user", content = prompt } },
                response_format = jsonMode ? new { type = "json_object" } : null
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
    }
}
