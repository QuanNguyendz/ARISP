using System;

namespace ARISP.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        string? Role { get; }
    }
}
