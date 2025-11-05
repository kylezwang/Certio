using System;
using System.Threading;
using System.Threading.Tasks;

namespace Certio.Application.Interfaces;

public interface IDocumentEmbedService
{
    Task<string> CreateEmbedUrlAsync(Guid documentId, Guid orgId, Guid requestingUserId, CancellationToken cancellationToken = default);
}
