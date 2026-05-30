using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ARISP.Application.Interfaces;

namespace ARISP.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId
        {
            get
            {
                var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? _httpContextAccessor.HttpContext?.User?.FindFirst("user_id")?.Value;

                return Guid.TryParse(idClaim, out var parsedGuid) ? parsedGuid : null;
            }
        }

        public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
                                ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value;
    }
}
