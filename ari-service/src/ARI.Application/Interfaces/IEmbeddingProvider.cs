using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ARI.Domain.Entities;

namespace ARI.Application.Interfaces
{
    public interface IEmbeddingProvider
    {
        Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
        Task<IEnumerable<DocumentChunk>> RetrieveAsync(Guid? sourceId, float[] queryVector, int topK = 5, CancellationToken ct = default);
    }
}
