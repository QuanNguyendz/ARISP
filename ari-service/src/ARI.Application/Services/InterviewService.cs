using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Common.Security;
using ARI.Application.DTOs;
using ARI.Application.Evaluations;
using ARI.Application.Interfaces;
using ARI.Application.Options;
using ARI.Domain.Entities;
using ARI.Domain.Constants;

namespace ARI.Application.Services
{
    public class InterviewService : IInterviewService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIProvider _aiProvider;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IAvatarService _avatarService;
        private readonly INotificationService _notificationService;
        private readonly IDeepgramTokenService _deepgramTokenService;
        private readonly IRagIngestionService _ragIngestion;
        private readonly ITTSService _ttsService;
        private readonly InterviewOptions _interviewOptions;

        public InterviewService(
            IUnitOfWork unitOfWork,
            IAIProvider aiProvider,
            IEmbeddingProvider embeddingProvider,
            IAvatarService avatarService,
            INotificationService notificationService,
            IDeepgramTokenService deepgramTokenService,
            IRagIngestionService ragIngestion,
            ITTSService ttsService,
            InterviewOptions? interviewOptions = null)
        {
            _unitOfWork = unitOfWork;
            _aiProvider = aiProvider;
            _embeddingProvider = embeddingProvider;
            _avatarService = avatarService;
            _notificationService = notificationService;
            _deepgramTokenService = deepgramTokenService;
            _ragIngestion = ragIngestion;
            _ttsService = ttsService;
            _interviewOptions = interviewOptions ?? new InterviewOptions();
        }

        /// <summary>
        /// TTS cho 1 câu hỏi (base64 PCM 24k) để FE đẩy vào LiveAvatar repeatAudio (ADR-044).
        /// Xác thực ứng viên sở hữu phiên. Rỗng nếu chưa cấu hình ElevenLabs → FE fallback browser TTS.
        /// </summary>
        public async Task<Result<string>> GetSpeechAudioAsync(
            Guid sessionId, string text, Guid? candidateAccountId, string? candidateEmail, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Result.Success(string.Empty);

            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<string>("Không tìm thấy phiên phỏng vấn.");
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            if (application == null)
                return Result.Failure<string>("Không tìm thấy hồ sơ ứng tuyển.");
            var owns = (candidateAccountId.HasValue && application.CandidateAccountId == candidateAccountId.Value)
                       || (!string.IsNullOrEmpty(candidateEmail)
                           && string.Equals(application.CandidateEmail, candidateEmail, StringComparison.OrdinalIgnoreCase));
            if (!owns)
                return Result.Failure<string>("Bạn không có quyền truy cập phiên phỏng vấn này.");

            try
            {
                var audio = await _ttsService.TextToSpeechBase64PcmAsync(text, string.Empty, ct);
                return Result.Success(audio);
            }
            catch
            {
                return Result.Success(string.Empty); // FE fallback browser TTS
            }
        }

        /// <summary>
        /// Đảm bảo CV (theo application) + JD (theo job) đã được ingest vào document_chunks để
        /// RAG (hybrid retrieve hoặc in-process) có ngữ cảnh khi sinh câu hỏi. Chỉ ingest khi THIẾU
        /// (idempotent, tránh re-embed mỗi lần vào phòng). An toàn nếu RAG service tạm lỗi (nuốt lỗi).
        /// </summary>
        private async Task EnsureSourcesIngestedAsync(ARI.Domain.Entities.Application application, JobPosting? jobPosting, CancellationToken ct)
        {
            try
            {
                var cvExists = (await _unitOfWork.Repository<DocumentChunk>()
                    .QueryAsync(q => q.Where(c => c.SourceType == "cv" && c.SourceId == application.Id).Select(c => c.Id).Take(1), ct)).Any();
                if (!cvExists && !string.IsNullOrWhiteSpace(application.CvText))
                    await _ragIngestion.IngestAsync("cv", application.Id, application.CvText!, ct: ct);

                if (jobPosting != null && !string.IsNullOrWhiteSpace(jobPosting.JobDescription))
                {
                    var jdExists = (await _unitOfWork.Repository<DocumentChunk>()
                        .QueryAsync(q => q.Where(c => c.SourceType == "jd" && c.SourceId == jobPosting.Id).Select(c => c.Id).Take(1), ct)).Any();
                    if (!jdExists)
                        await _ragIngestion.IngestAsync("jd", jobPosting.Id, jobPosting.JobDescription, ct: ct);
                }
            }
            catch
            {
                // RAG service/embedding tạm lỗi → vẫn vào phỏng vấn (openai path dùng full-text JD/CV trực tiếp).
            }
        }

        /// <summary>
        /// Cấu hình media cho FE vào phòng phỏng vấn: mint token Deepgram (STT) + HeyGen (avatar),
        /// kèm ngôn ngữ + voice. Xác thực ứng viên sở hữu phiên. Token nào chưa cấu hình key → null
        /// (FE tự fallback).
        /// </summary>
        public async Task<Result<PracticeMediaConfigResponse>> GetMediaConfigAsync(
            Guid sessionId, Guid? candidateAccountId, string? candidateEmail, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<PracticeMediaConfigResponse>("Không tìm thấy phiên phỏng vấn.");

            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            if (application == null)
                return Result.Failure<PracticeMediaConfigResponse>("Không tìm thấy hồ sơ ứng tuyển.");

            var owns = (candidateAccountId.HasValue && application.CandidateAccountId == candidateAccountId.Value)
                       || (!string.IsNullOrEmpty(candidateEmail)
                           && string.Equals(application.CandidateEmail, candidateEmail, StringComparison.OrdinalIgnoreCase));
            if (!owns)
                return Result.Failure<PracticeMediaConfigResponse>("Bạn không có quyền truy cập phiên phỏng vấn này.");

            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);

            // Token media là BEST-EFFORT: provider lỗi (hết key/sunset/transient) → null để FE
            // fallback mềm (browser TTS / nhập tay), KHÔNG được làm sập cả phòng phỏng vấn.
            DeepgramToken? deepgram = null;
            try { deepgram = await _deepgramTokenService.CreateTemporaryTokenAsync(ct); }
            catch { /* STT tuỳ chọn */ }

            // ADR-048: Practice audio-only — KHÔNG mint avatar (giữ đủ STT/RAG/LLM/ElevenLabs).
            // Tránh cạnh tranh concurrency LiveAvatar với buổi thật + đốt credit không dự đoán.
            // Real luôn có avatar. Cờ PracticeUseAvatar cho phép bật lại khi cần.
            AvatarStreamingToken? avatar = null;
            var useAvatar = session.SessionType != "practice" || _interviewOptions.PracticeUseAvatar;
            if (useAvatar)
            {
                try { avatar = await _avatarService.CreateStreamingTokenAsync(null, jobPosting?.PersonaVoiceId, ct); }
                catch { /* avatar tuỳ chọn — FE fallback WebAudio (ElevenLabs) + bot tĩnh */ }
            }

            // Trần thời lượng để FE vẽ đếm ngược khớp giờ server (ADR-048). Practice = config;
            // real = 0 (không chặn — tới Phase 7). StartedAtUtc để FE tính remaining chính xác.
            var maxDurationSeconds = session.SessionType == "practice" && _interviewOptions.PracticeMaxDurationMinutes > 0
                ? _interviewOptions.PracticeMaxDurationMinutes * 60
                : 0;

            return Result.Success(new PracticeMediaConfigResponse
            {
                SessionId = session.Id,
                Language = session.InterviewLanguage,
                SessionType = session.SessionType,
                MaxDurationSeconds = maxDurationSeconds,
                StartedAtUtc = session.StartedAt,
                Deepgram = deepgram == null ? null : new DeepgramConfigDto
                {
                    Token = deepgram.AccessToken,
                    ExpiresInSeconds = deepgram.ExpiresInSeconds,
                    Model = deepgram.Model
                },
                HeyGen = avatar == null ? null : new HeyGenConfigDto
                {
                    Token = avatar.Token,
                    ServerUrl = avatar.ServerUrl,
                    AvatarId = avatar.AvatarId,
                    VoiceId = avatar.VoiceId
                }
            });
        }

        /// <summary>
        /// Danh sách phiên phỏng vấn cho HR: join Application + JobPosting + Evaluation mới nhất.
        /// </summary>
        public async Task<List<HrInterviewSessionItem>> GetSessionsForHrAsync(CancellationToken ct = default)
        {
            var sessions = (await _unitOfWork.Repository<InterviewSession>().GetAllAsync(ct)).ToList();
            if (sessions.Count == 0) return new List<HrInterviewSessionItem>();

            var appIds = sessions.Select(s => s.ApplicationId).Distinct().ToList();
            var apps = (await _unitOfWork.Repository<ARI.Domain.Entities.Application>()
                .FindAsync(a => appIds.Contains(a.Id), ct)).ToList();
            var appById = apps.ToDictionary(a => a.Id);

            var jobIds = apps.Select(a => a.JobPostingId).Distinct().ToList();
            var jobTitleById = (await _unitOfWork.Repository<JobPosting>()
                .FindAsync(j => jobIds.Contains(j.Id), ct))
                .ToDictionary(j => j.Id, j => j.Title);

            // Đánh giá mới nhất theo từng phiên — chỉ lấy cột nhẹ (bỏ JSON criterion/question/...).
            var evaluations = await _unitOfWork.Repository<Evaluation>()
                .QueryAsync(q => q
                    .Where(e => appIds.Contains(e.ApplicationId))
                    .Select(e => new { e.Id, e.ApplicationId, e.RoundNumber, e.AiVerdict, e.CreatedAt }), ct);
            var evalByAppRound = evaluations
                .GroupBy(e => (e.ApplicationId, e.RoundNumber))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());

            return sessions
                .OrderByDescending(s => s.CreatedAt)
                .Select(s =>
                {
                    appById.TryGetValue(s.ApplicationId, out var app);
                    evalByAppRound.TryGetValue((s.ApplicationId, s.RoundNumber), out var eval);
                    return new HrInterviewSessionItem
                    {
                        Id = s.Id,
                        ApplicationId = s.ApplicationId,
                        CandidateName = app?.CandidateName ?? "—",
                        JobTitle = app != null && jobTitleById.TryGetValue(app.JobPostingId, out var t) ? t : null,
                        RoundNumber = s.RoundNumber,
                        RoundType = s.RoundType,
                        SessionType = s.SessionType,
                        Status = s.Status,
                        InterviewLanguage = s.InterviewLanguage,
                        DurationSeconds = s.DurationSeconds,
                        HasRecording = !string.IsNullOrEmpty(s.RecordingUrl),
                        StartedAt = s.StartedAt,
                        EndedAt = s.EndedAt,
                        CreatedAt = s.CreatedAt,
                        EvaluationId = eval?.Id,
                        Verdict = eval?.AiVerdict,
                    };
                })
                .ToList();
        }

        public async Task<Result<StartSessionResponse>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(request.ApplicationId, ct);
            if (application == null)
                return Result.Failure<StartSessionResponse>("Application not found.");

            // Giới hạn phỏng vấn thử theo VÒNG (ADR-038, mặc định 1 lượt/vòng).
            // Interview:PracticeAttemptsPerRound <= 0 = không giới hạn (chỉ dùng dev/test).
            if (request.SessionType == "practice")
            {
                var maxAttempts = _interviewOptions.PracticeAttemptsPerRound;
                if (maxAttempts > 0)
                {
                    var existingPractice = await _unitOfWork.Repository<InterviewSession>().FindAsync(
                        s => s.ApplicationId == application.Id
                             && s.SessionType == "practice"
                             && s.RoundNumber == request.RoundNumber, ct);
                    if (existingPractice.Count() >= maxAttempts)
                        return Result.Failure<StartSessionResponse>("Bạn đã dùng lượt phỏng vấn thử cho vòng này.");
                }

                // Giữ cờ tổng để tương thích ngược (không còn dùng làm điều kiện chặn).
                application.PracticeSessionUsed = true;
                _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);
            }

            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
            if (jobPosting == null)
                return Result.Failure<StartSessionResponse>("Job posting not found.");

            var roundConfigs = await _unitOfWork.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == jobPosting.Id && r.RoundNumber == request.RoundNumber, ct);
            var roundConfig = roundConfigs.FirstOrDefault() ?? new InterviewRoundConfig
            {
                RoundType = request.RoundNumber == 1 ? "screening" : "technical",
                InterviewCodeTtlHours = 2,
                MaxDurationMinutes = 45
            };

            var session = new InterviewSession
            {
                ApplicationId = application.Id,
                RoundNumber = request.RoundNumber,
                RoundType = roundConfig.RoundType,
                SessionType = request.SessionType,
                InterviewLanguage = jobPosting.DetectedLanguage ?? "vi",
                Status = "active",
                StartedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<InterviewSession>().AddAsync(session, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Đảm bảo CV + JD đã ingest vào RAG (practice = JD+CV; playbook chỉ cho real) trước câu hỏi đầu.
            await EnsureSourcesIngestedAsync(application, jobPosting, ct);

            // Seed Must-Ask questions from Playbook into tracking if it's a real session
            if (request.SessionType == "real")
            {
                var playbooks = await _unitOfWork.Repository<PlaybookDocument>()
                    .FindAsync(p => p.Scope == "job_posting" && p.ScopeRefId == jobPosting.Id && p.DocumentType == "must_ask", ct);
                
                foreach (var playbook in playbooks)
                {
                    if (string.IsNullOrEmpty(playbook.ParsedText)) continue;
                    
                    var mustAskLines = playbook.ParsedText.Split(new[] { "\n", ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var q in mustAskLines)
                    {
                        var track = new MustAskTracking
                        {
                            SessionId = session.Id,
                            PlaybookDocumentId = playbook.Id,
                            QuestionText = q.Trim()
                        };
                        await _unitOfWork.Repository<MustAskTracking>().AddAsync(track, ct);
                    }
                }
                await _unitOfWork.SaveChangesAsync(ct);
            }

            // HeyGen avatar integration (Hybrid Idle Strategy)
            string? heyGenSdp = null;
            string? heyGenSessionId = null;

            if (!string.IsNullOrEmpty(jobPosting.PersonaVoiceId) && !string.IsNullOrEmpty(jobPosting.PersonaStyle))
            {
                try
                {
                    var sdpMessage = await _avatarService.StartSessionAsync(jobPosting.PersonaVoiceId, jobPosting.PersonaStyle, ct);
                    heyGenSdp = sdpMessage.Sdp;
                    heyGenSessionId = "heygen_" + Guid.NewGuid().ToString("N");
                }
                catch
                {
                    // Fail silently, fall back to simple non-avatar
                }
            }

            var response = new StartSessionResponse
            {
                SessionId = session.Id,
                Status = session.Status,
                Language = session.InterviewLanguage,
                HeyGenSdpOffer = heyGenSdp,
                HeyGenSessionId = heyGenSessionId
            };

            return Result.Success(response);
        }

        public async Task<Result<string>> GenerateAndSendNextQuestionAsync(Guid sessionId, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<string>("Session not found.");
            if (session.Status != "active")
                return Result.Failure<string>("Session is not active.");

            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application!.JobPostingId, ct);
            var questions = await _unitOfWork.Repository<Question>().FindAsync(q => q.SessionId == sessionId, ct);
            var sequenceNumber = questions.Count() + 1;

            // Cap an toàn: quá số câu tối đa HOẶC vượt trần thời lượng (ADR-048, practice 20')
            // → KHÔNG cắt phụt; buộc AI sinh lời cảm ơn kết thúc (ForceClosing) rồi mới đóng phiên
            // — ứng viên luôn nhận được lời chào tạm biệt. Đây là lớp enforce server-side độc lập
            // với đồng hồ FE (ứng viên nói quá giờ → câu kế tiếp thành lời chào kết thúc).
            var elapsedMinutes = session.StartedAt.HasValue
                ? (DateTimeOffset.UtcNow - session.StartedAt.Value).TotalMinutes
                : 0;
            var maxMinutes = session.SessionType == "practice" ? _interviewOptions.PracticeMaxDurationMinutes : 0;
            var timeExceeded = maxMinutes > 0 && elapsedMinutes >= maxMinutes;
            var forceClosing = sequenceNumber > 12 || timeExceeded;

            // 1. Gather Weighted RAG Context.
            // CHỈ project ChunkText — KHÔNG load full entity (cột `embedding` kiểu pgvector không
            // materialize được qua Npgsql/EF khi chưa bật UseVector → InvalidCastException).
            var ragContext = new List<string>();
            var cvChunks = await _unitOfWork.Repository<DocumentChunk>()
                .QueryAsync(q => q.Where(c => c.SourceType == "cv" && c.SourceId == application.Id).Select(c => c.ChunkText), ct);
            ragContext.AddRange(cvChunks.Select(t => $"[CV Chunk] {t}"));

            var jdChunks = await _unitOfWork.Repository<DocumentChunk>()
                .QueryAsync(q => q.Where(c => c.SourceType == "jd" && c.SourceId == jobPosting!.Id).Select(c => c.ChunkText), ct);
            ragContext.AddRange(jdChunks.Select(t => $"[JD Chunk] {t}"));

            if (session.SessionType == "real")
            {
                var playbookChunks = await _unitOfWork.Repository<DocumentChunk>()
                    .QueryAsync(q => q.Where(c => c.SourceType == "playbook").Select(c => c.ChunkText), ct);
                ragContext.AddRange(playbookChunks.Select(t => $"[Org Playbook] {t}"));
            }

            // 2. Select Next Question Strategy
            string? mustAskQuestionText = null;
            MustAskTracking? mustAskRecord = null;

            if (session.SessionType == "real")
            {
                var unaskedMustAsks = await _unitOfWork.Repository<MustAskTracking>()
                    .FindAsync(m => m.SessionId == sessionId && m.AskedAt == null, ct);
                mustAskRecord = unaskedMustAsks.FirstOrDefault();
                if (mustAskRecord != null)
                {
                    mustAskQuestionText = mustAskRecord.QuestionText;
                }
            }

            // 3. Assemble Prompt & Generate via AI
            // Load toàn bộ answer của phiên trong 1 query (tránh N+1 — DB remote, mỗi round-trip đắt).
            var sessionAnswers = await _unitOfWork.Repository<Answer>().FindAsync(a => a.SessionId == sessionId, ct);
            var answerByQuestionId = sessionAnswers
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());
            var chatHistory = questions
                .OrderBy(x => x.SequenceNumber)
                .Select(q => new QuestionAnswerDto
                {
                    SequenceNumber = q.SequenceNumber,
                    QuestionText = q.QuestionText,
                    AnswerText = answerByQuestionId.TryGetValue(q.Id, out var a) ? a.Transcript : "[No response]"
                })
                .ToList();

            var aiContext = new QuestionContext
            {
                SessionId = sessionId,
                JobPostingId = jobPosting!.Id,
                ApplicationId = application.Id,
                JobDescription = jobPosting.JobDescription,
                CandidateCv = application.CvText ?? "",
                SessionType = session.SessionType,
                ChatHistory = chatHistory,
                PlaybookStyleGuides = ragContext.Where(r => r.StartsWith("[Org")).ToList(),
                Language = session.InterviewLanguage,
                ForceClosing = forceClosing
            };

            if (mustAskQuestionText != null)
            {
                aiContext.MustAskQuestions = new List<string> { mustAskQuestionText };
            }

            // Call AI provider to generate the question text
            string generatedQuestion = "";
            await foreach (var token in _aiProvider.StreamQuestionAsync(aiContext, ct))
            {
                generatedQuestion += token;
            }

            // AI chủ động kết thúc (đủ độ bao phủ, marker [END_INTERVIEW]) hoặc bị buộc (cap câu hỏi):
            // gửi lời cảm ơn (text + TTS) cho ứng viên TRƯỚC, rồi mới đóng phiên + sinh evaluation.
            const string endMarker = "[END_INTERVIEW]";
            if (forceClosing || generatedQuestion.Contains(endMarker, StringComparison.OrdinalIgnoreCase))
            {
                var aiText = generatedQuestion
                    .Replace(endMarker, "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
                var farewell = await CloseWithFarewellAsync(sessionId, session.InterviewLanguage, aiText, ct);
                return Result.Success(farewell);
            }

            // 4. Save generated question
            var question = new Question
            {
                SessionId = sessionId,
                SequenceNumber = sequenceNumber,
                QuestionText = generatedQuestion,
                QuestionType = mustAskQuestionText != null ? "playbook_must_ask" : "ai_generated",
                DifficultyLevel = questions.LastOrDefault()?.DifficultyLevel ?? 3,
                Source = mustAskQuestionText != null ? "playbook_must_ask" : "ai_generated"
            };

            await _unitOfWork.Repository<Question>().AddAsync(question, ct);

            if (mustAskRecord != null)
            {
                mustAskRecord.AskedAt = DateTimeOffset.UtcNow;
                mustAskRecord.QuestionId = question.Id;
                _unitOfWork.Repository<MustAskTracking>().Update(mustAskRecord);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            // 5. Notify SignalR Clients — đẩy text ngay để FE hiển thị không chờ TTS.
            await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveQuestion", new
            {
                questionId = question.Id,
                sequenceNumber = question.SequenceNumber,
                questionText = question.QuestionText,
                questionType = question.QuestionType,
                difficultyLevel = question.DifficultyLevel
            }, ct);

            // 6. TTS server-side rồi đẩy audio qua SignalR (ReceiveQuestionAudio) — bỏ round-trip
            // FE→BE /tts (kèm 2 query xác thực) khỏi critical path. Lỗi TTS → FE tự fallback.
            try
            {
                var audio = await _ttsService.TextToSpeechBase64PcmAsync(question.QuestionText, string.Empty, ct);
                if (!string.IsNullOrEmpty(audio))
                {
                    await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveQuestionAudio", new
                    {
                        questionId = question.Id,
                        audio
                    }, ct);
                }
            }
            catch
            {
                // TTS best-effort — FE fallback browser TTS khi không nhận được audio.
            }

            return Result.Success(question.QuestionText);
        }

        public async Task<Result<Answer>> SubmitAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default)
        {
            var saved = await SaveAnswerAsync(sessionId, questionId, transcript, responseTimeMs, ct);
            if (saved.IsFailure)
                return saved;

            await AnalyzeAnswerAndAdaptAsync(sessionId, questionId, transcript, ct);
            return saved;
        }

        /// <summary>
        /// Lưu answer NHANH (không gọi LLM) — dùng ở SessionHub để câu hỏi kế tiếp được sinh ngay,
        /// phân tích adaptive chạy sau (AnalyzeAnswerAndAdaptAsync) ngoài critical path latency.
        /// </summary>
        public async Task<Result<Answer>> SaveAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<Answer>("Session not found.");
            if (session.Status != "active")
                return Result.Failure<Answer>("Session is not active.");

            var answer = new Answer
            {
                QuestionId = questionId,
                SessionId = sessionId,
                Transcript = transcript,
                ResponseTimeMs = responseTimeMs
            };

            await _unitOfWork.Repository<Answer>().AddAsync(answer, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success(answer);
        }

        /// <summary>
        /// Phân tích answer để điều chỉnh độ khó (adaptive difficulty) + đẩy ReceiveAnswerAnalysis.
        /// Best-effort: lỗi LLM không ảnh hưởng luồng phỏng vấn.
        /// </summary>
        public async Task AnalyzeAnswerAndAdaptAsync(Guid sessionId, Guid questionId, string transcript, CancellationToken ct = default)
        {
            try
            {
                var question = await _unitOfWork.Repository<Question>().GetByIdAsync(questionId, ct);
                if (question == null) return;

                var analysis = await _aiProvider.AnalyzeAnswerAsync(new AnswerContext
                {
                    QuestionText = question.QuestionText,
                    AnswerTranscript = transcript
                }, ct);

                // Update question difficulty level adaptively
                question.DifficultyLevel = analysis.DifficultyLevel;
                _unitOfWork.Repository<Question>().Update(question);
                await _unitOfWork.SaveChangesAsync(ct);

                await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveAnswerAnalysis", new
                {
                    feedback = analysis.Feedback
                }, ct);
            }
            catch
            {
                // Fallback if AI analysis fails
            }
        }

        public async Task<Result<bool>> EndSessionAsync(Guid sessionId, string status = "completed", CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<bool>("Session not found.");

            // Idempotent: phiên đã "completed" là trạng thái cuối — không đóng/sinh evaluation lần hai
            // (chống race khi enforce server + NotifyTimeout FE cùng bắn — ADR-048).
            if (session.Status == "completed")
                return Result.Success(true);

            session.Status = status;
            session.EndedAt = DateTimeOffset.UtcNow;
            if (session.StartedAt.HasValue)
            {
                session.DurationSeconds = (int)(session.EndedAt.Value - session.StartedAt.Value).TotalSeconds;
            }

            _unitOfWork.Repository<InterviewSession>().Update(session);
            await _unitOfWork.SaveChangesAsync(ct);

            // If session is completed, automatically trigger AI Evaluation report generation
            if (status == "completed")
            {
                await GenerateEvaluationReportAsync(session.Id, ct);
            }

            await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveSessionStatus", new { status }, ct);

            return Result.Success(true);
        }

        /// <summary>
        /// Gửi lời chào kết thúc (text + TTS best-effort) rồi đóng phiên "completed" (ADR-048).
        /// Dùng chung cho: cap câu hỏi / trần thời lượng (GenerateAndSendNextQuestionAsync) và
        /// hết giờ phía FE (PracticeTimeoutCloseAsync). IDEMPOTENT — phiên đã "completed" thì no-op
        /// (chống race khi cả enforce server lẫn trigger FE cùng bắn).
        /// Trả về câu chào đã dùng.
        /// </summary>
        private async Task<string> CloseWithFarewellAsync(Guid sessionId, string? language, string? aiText, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null || session.Status == "completed")
                return string.Empty; // idempotent: đã đóng rồi thì không gửi closing/evaluation lần hai

            var farewell = (aiText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(farewell))
            {
                farewell = (language ?? "vi").StartsWith("vi", StringComparison.OrdinalIgnoreCase)
                    ? "Cảm ơn bạn đã dành thời gian tham gia buổi phỏng vấn hôm nay. Kết quả sẽ được gửi tới bạn trong thời gian sớm nhất. Chúc bạn một ngày tốt lành!"
                    : "Thank you for taking the time to join this interview. Your results will be shared with you soon. Have a great day!";
            }

            await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveClosing", new { text = farewell }, ct);

            try
            {
                var closingAudio = await _ttsService.TextToSpeechBase64PcmAsync(farewell, string.Empty, ct);
                if (!string.IsNullOrEmpty(closingAudio))
                {
                    await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveClosingAudio", new { audio = closingAudio }, ct);
                }
            }
            catch
            {
                // TTS best-effort — FE tự fallback browser TTS.
            }

            await EndSessionAsync(sessionId, "completed", ct);
            return farewell;
        }

        /// <summary>
        /// FE báo hết giờ (đồng hồ đếm ngược chạm 0) → AI nói 1 câu kết thúc rồi đóng phiên (ADR-048).
        /// Guard server-side: chỉ chấp nhận khi ĐÃ chạm ~95% trần thời lượng — FE không thể kết thúc
        /// sớm để né phần còn lại. Chỉ áp dụng phiên "practice".
        /// </summary>
        public async Task<Result<bool>> PracticeTimeoutCloseAsync(Guid sessionId, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<bool>("Session not found.");
            if (session.Status == "completed")
                return Result.Success(true); // đã đóng — idempotent

            if (session.SessionType != "practice")
                return Result.Failure<bool>("Timeout close chỉ áp dụng cho phỏng vấn thử.");

            var maxMinutes = _interviewOptions.PracticeMaxDurationMinutes;
            if (maxMinutes > 0 && session.StartedAt.HasValue)
            {
                var elapsedMinutes = (DateTimeOffset.UtcNow - session.StartedAt.Value).TotalMinutes;
                if (elapsedMinutes < maxMinutes * 0.95)
                    return Result.Failure<bool>("Chưa hết thời gian phỏng vấn thử.");
            }

            await CloseWithFarewellAsync(sessionId, session.InterviewLanguage, null, ct);
            return Result.Success(true);
        }

        private async Task GenerateEvaluationReportAsync(Guid sessionId, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(session!.ApplicationId, ct);
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application!.JobPostingId, ct);
            var questions = await _unitOfWork.Repository<Question>().FindAsync(q => q.SessionId == sessionId, ct);

            var chatHistory = new List<QuestionAnswerDto>();
            foreach (var q in questions.OrderBy(x => x.SequenceNumber))
            {
                var answers = await _unitOfWork.Repository<Answer>().FindAsync(a => a.QuestionId == q.Id, ct);
                chatHistory.Add(new QuestionAnswerDto
                {
                    SequenceNumber = q.SequenceNumber,
                    QuestionText = q.QuestionText,
                    AnswerText = answers.FirstOrDefault()?.Transcript ?? ""
                });
            }

            var evalCtx = new SessionContext
            {
                SessionId = sessionId,
                JobDescription = jobPosting!.JobDescription,
                CandidateCv = application.CvText ?? "",
                SessionType = session.SessionType,
                ChatHistory = chatHistory,
                ScoringRubric = jobPosting.ScoringRubric ?? "{}",
                Language = session.InterviewLanguage ?? jobPosting.DetectedLanguage
            };

            // Call AI provider to generate Verdict, Score, Reasoning, etc.
            var evalReport = await _aiProvider.GenerateEvaluationAsync(evalCtx, ct);
            
            // Collect cheat detection signals
            var signals = await _unitOfWork.Repository<CheatDetectionSignal>()
                .FindAsync(s => s.SessionId == sessionId, ct);
            decimal cheatScore = signals.Any() ? 10 : 0; // simple heuristic calculation for prototype

            // Language Assessment (English/other languages check)
            LanguageAssessment? langAssess = null;
            if (!string.IsNullOrEmpty(jobPosting.DetectedLanguage))
            {
                langAssess = await _aiProvider.AssessLanguageProficiencyAsync(evalCtx, ct);
            }

            var evaluation = new Evaluation
            {
                SessionId = sessionId,
                ApplicationId = application.Id,
                RoundNumber = session.RoundNumber,
                SessionType = session.SessionType,
                AiVerdict = evalReport.Verdict,
                OverallScore = evalReport.Score,
                CriterionScores = evalReport.CriterionScoresJson,
                Reasoning = evalReport.Reasoning,
                RecommendedNextStep = evalReport.RecommendedNextStep,
                QuestionAnalyses = evalReport.QuestionAnalysesJson,
                CheatScore = cheatScore,
                CheatSignals = "[]",
                LanguageAssessment = langAssess != null
                    ? System.Text.Json.JsonSerializer.Serialize(new
                    {
                        language = evalCtx.Language,
                        fluency = langAssess.Fluency,
                        grammar = langAssess.Grammar,
                        vocabulary = langAssess.Vocabulary,
                        comprehension = langAssess.Comprehension,
                        overall_score = langAssess.OverallScore,
                        language_adherence = langAssess.LanguageAdherence
                    })
                    : null
            };

            await _unitOfWork.Repository<Evaluation>().AddAsync(evaluation, ct);
            
            // Set candidate status to screening / interview finished
            application.Status = evalReport.Verdict == "pass" ? "screening" : "not_pass";
            _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);

            await _unitOfWork.SaveChangesAsync(ct);
            
            // Notify HR Admin that there is a new evaluation to review
            await _notificationService.PublishGroupEventAsync("hr_admin", "ReceiveSystemEvent", new { 
                Type = "AiEvaluationComplete", 
                EvaluationId = evaluation.Id,
                ApplicationId = application.Id
            }, ct);
        }

        public async Task<Result<bool>> SubmitHrReviewAsync(Guid hrUserId, ConfirmReviewRequest request, string? frontendBaseUrl = null, CancellationToken ct = default)
        {
            var evaluation = await _unitOfWork.Repository<Evaluation>().GetByIdAsync(request.EvaluationId, ct);
            if (evaluation == null)
                return Result.Failure<bool>("Evaluation report not found.");

            // Validate User & Role for Override actions
            var hrUser = await _unitOfWork.Repository<User>().GetByIdAsync(hrUserId, ct);
            if (hrUser == null)
                return Result.Failure<bool>("HR User not found.");

            bool isOverride = evaluation.AiVerdict != request.FinalVerdict;
            if (isOverride)
            {
                bool isAuthorized = string.Equals(hrUser.Role, AppRoles.HrAdmin, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(hrUser.Role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(hrUser.Role, "hr_admin", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(hrUser.Role, "super_admin", StringComparison.OrdinalIgnoreCase);

                if (!isAuthorized)
                    return Result.Failure<bool>("Only HR Admin or Super Admin can override AI verdict.");

                if (string.IsNullOrEmpty(request.OverrideReason))
                    return Result.Failure<bool>("Override reason is mandatory when changing the AI verdict.");
            }

            var review = new HrReview
            {
                EvaluationId = evaluation.Id,
                ReviewedByUserId = hrUserId,
                FinalVerdict = request.FinalVerdict,
                IsOverride = isOverride,
                OverrideReason = request.OverrideReason,
                ShareRecording = request.ShareRecording,
                ShareTranscript = request.ShareTranscript,
                ShareEvaluation = request.ShareEvaluation,
                ShareFeedback = request.ShareFeedback,
                CandidateFeedback = request.CandidateFeedback
            };

            await _unitOfWork.Repository<HrReview>().AddAsync(review, ct);

            // Update Application status based on final verdict
            var application = await _unitOfWork.Repository<ARI.Domain.Entities.Application>().GetByIdAsync(evaluation.ApplicationId, ct);
            if (application != null)
            {
                application.Status = request.FinalVerdict == "pass" ? "pass" : "not_pass";
                _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);

                var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
                var jobTitle = jobPosting?.Title ?? "vị trí ứng tuyển";

                bool hasProgressed = false;
                // Auto-Progression Logic to Round N+1 (ADR-017 / ADR-014)
                if (request.FinalVerdict == "pass" && evaluation.SessionType == "real")
                {
                    hasProgressed = await TriggerAutoProgressionAsync(application, evaluation.RoundNumber, frontendBaseUrl, ct);
                }

                // Auto-send email to Candidate if not progressed
                if (!hasProgressed)
                {
                    string emailBody;
                    string subject;
                    if (request.FinalVerdict == "pass")
                    {
                        subject = "ARISP - Chúc mừng bạn đã vượt qua vòng phỏng vấn!";
                        emailBody = $$"""
                            <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);">
                                <div style="background: linear-gradient(135deg, #059669, #10b981); padding: 24px; text-align: center; color: white;">
                                    <h2 style="margin: 0; font-size: 20px; font-weight: 600; letter-spacing: 0.5px;">THƯ CHÚC MỪNG VƯỢT QUA VÒNG PHỎNG VẤN</h2>
                                </div>
                                <div style="padding: 32px 24px; background-color: #ffffff; color: #334155; line-height: 1.6;">
                                    <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                                    <p>Chúng tôi vô cùng vui mừng thông báo rằng Anh/Chị đã chính thức vượt qua các vòng đánh giá năng lực của vị trí tuyển dụng <strong>{{jobTitle}}</strong> tại ARISP.</p>
                                    <p>Đội ngũ tuyển dụng đánh giá rất cao năng lực chuyên môn, phong cách làm việc cũng như sự phù hợp của Anh/Chị với định hướng phát triển của chúng tôi.</p>
                                    <p>Đại diện bộ phận Nhân sự (HR) sẽ liên hệ trực tiếp với Anh/Chị trong vòng 1-2 ngày làm việc tới để trao đổi chi tiết về kế hoạch công việc, mức đãi ngộ và gửi Thư mời nhận việc chính thức (Offer Letter).</p>
                                    <p>Cảm ơn Anh/Chị đã luôn dành sự quan tâm và nỗ lực trong suốt hành trình tuyển dụng cùng ARISP.</p>
                                    <div style="text-align: center; margin: 30px 0;">
                                        <a href="http://localhost:3000/candidate/applications/{{application.Id}}" style="background-color: #059669; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block; box-shadow: 0 4px 6px rgba(5,150,105,0.2);">Xem kết quả chi tiết</a>
                                    </div>
                                    <p style="margin-bottom: 0;">Trân trọng,<br><strong>Trưởng Ban Tuyển Dụng ARISP</strong></p>
                                </div>
                                <div style="background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;">
                                    <p style="margin: 0;">Đây là thư điện tử tự động từ hệ thống ARISP. Vui lòng không trả lời trực tiếp thư này.</p>
                                </div>
                            </div>
                            """;
                    }
                    else
                    {
                        subject = "ARISP - Thư cảm ơn tham gia phỏng vấn";
                        emailBody = $$"""
                            <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);">
                                <div style="background: linear-gradient(135deg, #4b5563, #6b7280); padding: 24px; text-align: center; color: white;">
                                    <h2 style="margin: 0; font-size: 20px; font-weight: 600; letter-spacing: 0.5px;">THƯ CẢM ƠN THAM GIA PHỎNG VẤN</h2>
                                </div>
                                <div style="padding: 32px 24px; background-color: #ffffff; color: #334155; line-height: 1.6;">
                                    <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                                    <p>Đội ngũ tuyển dụng ARISP chân thành cảm ơn Anh/Chị đã dành thời gian và tâm huyết tham gia quy trình ứng tuyển vào vị trí <strong>{{jobTitle}}</strong>.</p>
                                    <p>Sau khi cân nhắc kỹ lưỡng dựa trên kết quả phỏng vấn và so sánh với định hướng hiện tại của vị trí, chúng tôi rất tiếc phải thông báo rằng chưa thể đồng hành cùng Anh/Chị trong dự án lần này.</p>
                                    <p>Hồ sơ năng lực của Anh/Chị sẽ được lưu trữ bảo mật trong Cơ sở dữ liệu ứng viên tiềm năng của ARISP. Chúng tôi sẽ chủ động liên hệ ngay khi có những cơ hội nghề nghiệp mới phù hợp hơn với thế mạnh của Anh/Chị.</p>
                                    <div style="text-align: center; margin: 30px 0;">
                                        <a href="http://localhost:3000/candidate/applications/{{application.Id}}" style="background-color: #4b5563; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block; box-shadow: 0 4px 6px rgba(75,85,99,0.2);">Xem thông tin hồ sơ</a>
                                    </div>
                                    <p>Chúc Anh/Chị luôn dồi dào sức khỏe, may mắn và gặt hái được nhiều thành công rực rỡ trên con đường sự nghiệp sắp tới.</p>
                                    <p style="margin-bottom: 0;">Trân trọng,<br><strong>Ban Tuyển Dụng ARISP</strong></p>
                                </div>
                                <div style="background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;">
                                    <p style="margin: 0;">Đây là thư điện tử tự động từ hệ thống ARISP. Vui lòng không trả lời trực tiếp thư này.</p>
                                </div>
                            </div>
                            """;
                    }

                    await _notificationService.SendEmailAsync(application.CandidateEmail, subject, emailBody, ct);
                }

                // Notify candidate in real-time
                if (application.CandidateAccountId.HasValue)
                {
                    await _notificationService.PublishUserEventAsync(application.CandidateAccountId.Value, "ReceiveApplicationStatusUpdate", new { 
                        Id = application.Id, 
                        JobPostingId = application.JobPostingId, 
                        Status = application.Status 
                    }, ct);

                    // Add Notification record
                    var notifRepo = _unitOfWork.Repository<ARI.Domain.Entities.Notification>();
                    var dedupKey = $"hr_review:{evaluation.Id}";
                    var existingNotifs = await notifRepo.FindAsync(n => n.CandidateAccountId == application.CandidateAccountId.Value && n.DedupKey == dedupKey, ct);
                    if (existingNotifs.FirstOrDefault() == null)
                    {
                        var verdict = isOverride ? request.FinalVerdict : evaluation.AiVerdict;
                        var isPass = verdict == "pass";
                        var newNotif = new ARI.Domain.Entities.Notification
                        {
                            CandidateAccountId = application.CandidateAccountId.Value,
                            DedupKey = dedupKey,
                            Type = "result",
                            Title = "Kết quả phỏng vấn",
                            Body = isPass ? "Chúc mừng bạn đã vượt qua vòng phỏng vấn!" : "Rất tiếc, bạn chưa phù hợp với vị trí này.",
                            Link = $"/candidate/applications/{application.Id}",
                            IsRead = false
                        };
                        await notifRepo.AddAsync(newNotif, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }

                    // Trigger bell update
                    await _notificationService.PublishUserEventAsync(application.CandidateAccountId.Value, "ReceiveUserNotification", new { Type = "InterviewResult" }, ct);
                }
            }

            // Save Audit Log (mandatory for confirm/override actions)
            var auditLog = new AuditLog
            {
                ActorUserId = hrUserId,
                Action = isOverride ? "hr_override" : "hr_confirm",
                EntityType = "evaluation",
                EntityId = evaluation.Id,
                Metadata = $"{{\"evaluation_id\":\"{evaluation.Id}\",\"final_verdict\":\"{request.FinalVerdict}\",\"is_override\":{isOverride.ToString().ToLower()}}}"
            };
            await _unitOfWork.Repository<AuditLog>().AddAsync(auditLog, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success(true);
        }

        private async Task<bool> TriggerAutoProgressionAsync(ARI.Domain.Entities.Application application, int currentRoundNumber, string? frontendBaseUrl = null, CancellationToken ct = default)
        {
            var nextRoundNumber = currentRoundNumber + 1;

            // Check if there is configured round configs for N+1
            var nextRoundConfigs = await _unitOfWork.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == application.JobPostingId && r.RoundNumber == nextRoundNumber, ct);

            if (nextRoundConfigs.Any())
            {
                var nextRound = nextRoundConfigs.First();
                // Status = interview (đang trong giai đoạn phỏng vấn vòng kế); chi tiết vòng suy ra từ record.
                application.Status = "interview";
                _unitOfWork.Repository<ARI.Domain.Entities.Application>().Update(application);

                // Tạo lời mời CHỌN LỊCH cho vòng kế (mỗi vòng cần duyệt → chỉ tạo sau khi confirm Pass).
                var job = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
                var ttlHours = job?.InviteTokenTtlHours is { } h && h > 0 ? h : 48;
                var baseUrl = (string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:3000" : frontendBaseUrl).TrimEnd('/');
                var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

                var oldInvites = await _unitOfWork.Repository<InterviewInvite>()
                    .FindAsync(i => i.ApplicationId == application.Id && i.RoundNumber == nextRoundNumber && i.ScheduledAt == null, ct);
                foreach (var old in oldInvites)
                    _unitOfWork.Repository<InterviewInvite>().Delete(old);

                await _unitOfWork.Repository<InterviewInvite>().AddAsync(new InterviewInvite
                {
                    ApplicationId = application.Id,
                    RoundNumber = nextRoundNumber,
                    TokenHash = TokenHashing.Sha256Hex(rawToken),
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(ttlHours),
                }, ct);

                var scheduleLink = $"{baseUrl}/portal/schedule/{application.Id}?token={rawToken}&round={nextRoundNumber}";

                var emailBody = $$"""
                    <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);">
                        <div style="background: linear-gradient(135deg, #1e3a8a, #2563eb); padding: 24px; text-align: center; color: white;">
                            <h2 style="margin: 0; font-size: 20px; font-weight: 600; letter-spacing: 0.5px;">HỆ THỐNG TUYỂN DỤNG THÔNG MINH ARISP</h2>
                        </div>
                        <div style="padding: 32px 24px; background-color: #ffffff; color: #334155; line-height: 1.6;">
                            <p style="margin-top: 0; font-size: 16px;">Kính gửi Anh/Chị <strong>{{application.CandidateName}}</strong>,</p>
                            <p>Chúc mừng Anh/Chị đã hoàn thành xuất sắc vòng phỏng vấn số <strong>{{currentRoundNumber}}</strong>.</p>
                            <p>Đội ngũ tuyển dụng ARISP trân trọng kính mời Anh/Chị tiếp tục tham gia vào <strong>Vòng phỏng vấn số {{nextRoundNumber}}</strong>.</p>
                            <div style="margin: 30px 0; text-align: center;">
                                <a href="{{scheduleLink}}" style="background-color: #2563eb; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block; box-shadow: 0 4px 6px rgba(37,99,235,0.2);">Đặt lịch phỏng vấn ngay</a>
                            </div>
                            <p>Vui lòng mở liên kết trên (trên thiết bị cá nhân) để chọn khung giờ phỏng vấn vòng {{nextRoundNumber}}. Buổi phỏng vấn thật diễn ra tại văn phòng — bạn nhập mã phỏng vấn do nhân sự cấp tại chỗ.</p>
                            <p>Nếu gặp bất kỳ khó khăn hoặc cần hỗ trợ kỹ thuật, xin vui lòng phản hồi trực tiếp email này hoặc liên hệ bộ phận hỗ trợ tuyển dụng.</p>
                            <p style="margin-bottom: 0;">Trân trọng,<br><strong>Ban Tuyển Dụng ARISP</strong></p>
                        </div>
                        <div style="background-color: #f8fafc; padding: 16px 24px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;">
                            <p style="margin: 0;">Đây là thư điện tử tự động từ hệ thống ARISP. Vui lòng không trả lời trực tiếp thư này.</p>
                        </div>
                    </div>
                    """;

                // Send email invite
                await _notificationService.SendEmailAsync(
                    application.CandidateEmail,
                    $"ARISP - Mời bạn tham gia vòng phỏng vấn số {nextRoundNumber}",
                    emailBody,
                    ct
                );
                return true;
            }
            return false;
        }
    }
}
