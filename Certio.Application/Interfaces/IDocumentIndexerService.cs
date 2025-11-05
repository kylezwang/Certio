using System;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;

namespace Certio.Application.Interfaces;

public interface IDocumentIndexerService
{
    Task QueueEmbeddingAsync(Guid documentId, Guid? versionId, CancellationToken cancellationToken = default);

    Task<DocumentIndexRequest> BuildIndexRequestAsync(Guid documentId, Guid? versionId, CancellationToken cancellationToken = default);

    Task ProcessIndexRequestAsync(DocumentIndexRequest request, CancellationToken cancellationToken = default);
}
