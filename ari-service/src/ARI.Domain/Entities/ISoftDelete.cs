using System;

namespace ARI.Domain.Entities
{
    public interface ISoftDelete
    {
        DateTimeOffset? DeletedAt { get; set; }
    }
}
