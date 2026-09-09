using System;
using ARI.Domain.Entities;

namespace ARI.Application.Offers
{
    /// <summary>Thư mời nhận việc, góc nhìn NHÂN SỰ — có cả ghi chú nội bộ.</summary>
    public class OfferDto
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public Guid JobPostingId { get; set; }
        public string Status { get; set; } = string.Empty;

        public string? CandidateName { get; set; }
        public string? CandidateEmail { get; set; }
        public string? JobTitle { get; set; }

        public string? Position { get; set; }
        public decimal? SalaryAmount { get; set; }
        public string? SalaryCurrency { get; set; }
        public string? SalaryPeriod { get; set; }
        public string? Bonus { get; set; }
        public string? Benefits { get; set; }
        public string? EmploymentType { get; set; }
        public string? WorkLocation { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Nội bộ — DTO của ứng viên KHÔNG có trường này.</summary>
        public string? Notes { get; set; }

        public string? ApprovalNote { get; set; }
        public string? RejectedReason { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public DateTimeOffset? RespondedAt { get; set; }
        public string? CandidateResponseNote { get; set; }
        public string? WithdrawnReason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public static OfferDto FromEntity(
            Offer offer, ARI.Domain.Entities.Application? app = null, JobPosting? job = null) => new()
        {
            Id = offer.Id,
            ApplicationId = offer.ApplicationId,
            JobPostingId = offer.JobPostingId,
            Status = offer.Status,
            CandidateName = app?.CandidateName,
            CandidateEmail = app?.CandidateEmail,
            JobTitle = job?.Title,
            Position = offer.Position,
            SalaryAmount = offer.SalaryAmount,
            SalaryCurrency = offer.SalaryCurrency,
            SalaryPeriod = offer.SalaryPeriod,
            Bonus = offer.Bonus,
            Benefits = offer.Benefits,
            EmploymentType = offer.EmploymentType,
            WorkLocation = offer.WorkLocation,
            StartDate = offer.StartDate,
            ExpiresAt = offer.ExpiresAt,
            Notes = offer.Notes,
            ApprovalNote = offer.ApprovalNote,
            RejectedReason = offer.RejectedReason,
            SentAt = offer.SentAt,
            RespondedAt = offer.RespondedAt,
            CandidateResponseNote = offer.CandidateResponseNote,
            WithdrawnReason = offer.WithdrawnReason,
            CreatedAt = offer.CreatedAt,
        };
    }

    /// <summary>
    /// Thư mời nhận việc, góc nhìn ỨNG VIÊN.
    ///
    /// Lớp RIÊNG chứ không dùng lại <see cref="OfferDto"/> rồi xoá trường: quên xoá một lần là
    /// ghi chú đàm phán lương nội bộ rơi thẳng vào Portal. Ở đây trường đó không tồn tại để mà quên.
    /// </summary>
    public class CandidateOfferDto
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? Position { get; set; }
        public decimal? SalaryAmount { get; set; }
        public string? SalaryCurrency { get; set; }
        public string? SalaryPeriod { get; set; }
        public string? Bonus { get; set; }
        public string? Benefits { get; set; }
        public string? EmploymentType { get; set; }
        public string? WorkLocation { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public DateTimeOffset? RespondedAt { get; set; }
        public string? CandidateResponseNote { get; set; }
        public string? OfferLetterFileUrl { get; set; }

        public static CandidateOfferDto FromEntity(Offer offer, JobPosting? job = null) => new()
        {
            Id = offer.Id,
            ApplicationId = offer.ApplicationId,
            Status = offer.Status,
            JobTitle = job?.Title,
            Position = offer.Position,
            SalaryAmount = offer.SalaryAmount,
            SalaryCurrency = offer.SalaryCurrency,
            SalaryPeriod = offer.SalaryPeriod,
            Bonus = offer.Bonus,
            Benefits = offer.Benefits,
            EmploymentType = offer.EmploymentType,
            WorkLocation = offer.WorkLocation,
            StartDate = offer.StartDate,
            ExpiresAt = offer.ExpiresAt,
            SentAt = offer.SentAt,
            RespondedAt = offer.RespondedAt,
            CandidateResponseNote = offer.CandidateResponseNote,
            OfferLetterFileUrl = offer.OfferLetterFileUrl,
        };
    }

    /// <summary>Body tạo/sửa offer.</summary>
    public class UpsertOfferRequest
    {
        public Guid ApplicationId { get; set; }
        public string? Position { get; set; }
        public decimal? SalaryAmount { get; set; }
        public string? SalaryCurrency { get; set; }
        public string? SalaryPeriod { get; set; }
        public string? Bonus { get; set; }
        public string? Benefits { get; set; }
        public string? EmploymentType { get; set; }
        public string? WorkLocation { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public string? Notes { get; set; }
    }
}
