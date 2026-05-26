using System;

namespace ARISP.Domain.Entities
{
    public interface IMultiTenant
    {
        Guid OrganizationId { get; set; }
    }
}
