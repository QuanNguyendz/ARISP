using System;

namespace ARISP.Domain.Entities
{
    public interface ISoftDelete
    {
        DateTimeOffset? DeletedAt { get; set; }
    }
}
