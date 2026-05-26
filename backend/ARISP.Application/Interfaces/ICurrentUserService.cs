using System;

namespace ARISP.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        Guid? OrganizationId { get; }
        string? Role { get; }
    }
}
