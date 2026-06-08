using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARISP.Application.Common;
using ARISP.Application.DTOs;
using ARISP.Application.Interfaces;
using ARISP.Domain.Entities;
using ARISP.Domain.Constants;

namespace ARISP.Application.Services
{
    public class InterviewService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAIProvider _aiProvider;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IAvatarService _avatarService;
        private readonly INotificationService _notificationService;

        public InterviewService(
            IUnitOfWork unitOfWork,
            IAIProvider aiProvider,
            IEmbeddingProvider embeddingProvider,
            IAvatarService avatarService,
            INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _aiProvider = aiProvider;
            _embeddingProvider = embeddingProvider;
            _avatarService = avatarService;
            _notificationService = notificationService;
        }

        public async Task<Result<StartSessionResponse>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default)
        {
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(request.ApplicationId, ct);
            if (application == null)
                return Result.Failure<StartSessionResponse>("Application not found.");

            // Check practice session limit (1 session per application)
            if (request.SessionType == "practice")
            {
                if (application.PracticeSessionUsed)
                    return Result.Failure<StartSessionResponse>("Practice interview already used for this application.");
                
                application.PracticeSessionUsed = true;
                _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(application);
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

            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(session.ApplicationId, ct);
            var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application!.JobPostingId, ct);
            var questions = await _unitOfWork.Repository<Question>().FindAsync(q => q.SessionId == sessionId, ct);
            var sequenceNumber = questions.Count() + 1;

            // Maximum question threshold check for safety
            if (sequenceNumber > 12)
            {
                await EndSessionAsync(sessionId, "completed", ct);
                return Result.Success("Interview completed.");
            }

            // 1. Gather Weighted RAG Context
            var ragContext = new List<string>();
            var cvChunks = await _unitOfWork.Repository<DocumentChunk>()
                .FindAsync(c => c.SourceType == "cv" && c.SourceId == application.Id, ct);
            ragContext.AddRange(cvChunks.Select(c => $"[CV Chunk] {c.ChunkText}"));

            var jdChunks = await _unitOfWork.Repository<DocumentChunk>()
                .FindAsync(c => c.SourceType == "jd" && c.SourceId == jobPosting!.Id, ct);
            ragContext.AddRange(jdChunks.Select(c => $"[JD Chunk] {c.ChunkText}"));

            if (session.SessionType == "real")
            {
                var playbookChunks = await _unitOfWork.Repository<DocumentChunk>()
                    .FindAsync(c => c.SourceType == "playbook", ct);
                ragContext.AddRange(playbookChunks.Select(c => $"[Org Playbook] {c.ChunkText}"));
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
            var chatHistory = new List<QuestionAnswerDto>();
            foreach (var q in questions.OrderBy(x => x.SequenceNumber))
            {
                var answers = await _unitOfWork.Repository<Answer>().FindAsync(a => a.QuestionId == q.Id, ct);
                chatHistory.Add(new QuestionAnswerDto
                {
                    SequenceNumber = q.SequenceNumber,
                    QuestionText = q.QuestionText,
                    AnswerText = answers.FirstOrDefault()?.Transcript ?? "[No response]"
                });
            }

            var aiContext = new QuestionContext
            {
                SessionId = sessionId,
                JobPostingId = jobPosting!.Id,
                ApplicationId = application.Id,
                JobDescription = jobPosting.JobDescription,
                CandidateCv = application.CvText ?? "",
                SessionType = session.SessionType,
                ChatHistory = chatHistory,
                PlaybookStyleGuides = ragContext.Where(r => r.StartsWith("[Org")).ToList()
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

            // 5. Notify SignalR Clients
            await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveQuestion", new
            {
                sequenceNumber = question.SequenceNumber,
                questionText = question.QuestionText,
                questionType = question.QuestionType,
                difficultyLevel = question.DifficultyLevel
            }, ct);

            return Result.Success(question.QuestionText);
        }

        public async Task<Result<Answer>> SubmitAnswerAsync(Guid sessionId, Guid questionId, string transcript, int? responseTimeMs, CancellationToken ct = default)
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

            // Analyze answer for adaptive difficulty adjustment
            try
            {
                var question = await _unitOfWork.Repository<Question>().GetByIdAsync(questionId, ct);
                if (question != null)
                {
                    var analysis = await _aiProvider.AnalyzeAnswerAsync(new AnswerContext
                    {
                        QuestionText = question.QuestionText,
                        AnswerTranscript = transcript
                    }, ct);

                    // Update question difficulty level adaptively
                    question.DifficultyLevel = analysis.DifficultyLevel;
                    _unitOfWork.Repository<Question>().Update(question);

                    await _notificationService.PublishInterviewSessionEventAsync(sessionId, "ReceiveAnswerAnalysis", new
                    {
                        feedback = analysis.Feedback
                    }, ct);
                }
            }
            catch
            {
                // Fallback if AI analysis fails
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success(answer);
        }

        public async Task<Result<bool>> EndSessionAsync(Guid sessionId, string status = "completed", CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            if (session == null)
                return Result.Failure<bool>("Session not found.");

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

        private async Task GenerateEvaluationReportAsync(Guid sessionId, CancellationToken ct = default)
        {
            var session = await _unitOfWork.Repository<InterviewSession>().GetByIdAsync(sessionId, ct);
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(session!.ApplicationId, ct);
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
                ScoringRubric = jobPosting.ScoringRubric ?? "{}"
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
                    ? $"{{\"fluency\":{langAssess.Fluency},\"grammar\":{langAssess.Grammar},\"vocabulary\":{langAssess.Vocabulary},\"comprehension\":{langAssess.Comprehension},\"overall_score\":{langAssess.OverallScore}}}"
                    : null
            };

            await _unitOfWork.Repository<Evaluation>().AddAsync(evaluation, ct);
            
            // Set candidate status to screening / interview finished
            application.Status = evalReport.Verdict == "pass" ? "screening" : "not_pass";
            _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(application);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<Result<bool>> SubmitHrReviewAsync(Guid hrUserId, ConfirmReviewRequest request, CancellationToken ct = default)
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
            var application = await _unitOfWork.Repository<ARISP.Domain.Entities.Application>().GetByIdAsync(evaluation.ApplicationId, ct);
            if (application != null)
            {
                application.Status = request.FinalVerdict == "pass" ? "pass" : "not_pass";
                _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(application);

                var jobPosting = await _unitOfWork.Repository<JobPosting>().GetByIdAsync(application.JobPostingId, ct);
                var jobTitle = jobPosting?.Title ?? "vị trí ứng tuyển";

                bool hasProgressed = false;
                // Auto-Progression Logic to Round N+1 (ADR-017 / ADR-014)
                if (request.FinalVerdict == "pass" && evaluation.SessionType == "real")
                {
                    hasProgressed = await TriggerAutoProgressionAsync(application, evaluation.RoundNumber, ct);
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

        private async Task<bool> TriggerAutoProgressionAsync(ARISP.Domain.Entities.Application application, int currentRoundNumber, CancellationToken ct = default)
        {
            var nextRoundNumber = currentRoundNumber + 1;
            
            // Check if there is configured round configs for N+1
            var nextRoundConfigs = await _unitOfWork.Repository<InterviewRoundConfig>()
                .FindAsync(r => r.JobPostingId == application.JobPostingId && r.RoundNumber == nextRoundNumber, ct);
            
            if (nextRoundConfigs.Any())
            {
                var nextRound = nextRoundConfigs.First();
                // Set status back to the next round type (e.g. "technical") or default "interview"
                application.Status = !string.IsNullOrEmpty(nextRound.RoundType) ? nextRound.RoundType.ToLower() : "interview";
                _unitOfWork.Repository<ARISP.Domain.Entities.Application>().Update(application);

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
                                <a href="https://arisp.portal/candidate/login" style="background-color: #2563eb; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; display: inline-block; box-shadow: 0 4px 6px rgba(37,99,235,0.2);">Đặt lịch phỏng vấn ngay</a>
                            </div>
                            <p>Vui lòng đăng nhập vào <strong>Candidate Portal của ARISP</strong> bằng liên kết trên để lựa chọn khung thời gian phỏng vấn phù hợp nhất với lịch trình của Anh/Chị.</p>
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
