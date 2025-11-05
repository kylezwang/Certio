using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Domain.Documents;

namespace Certio.Application.Interfaces;

public interface IVectorStoreService
{
    Task UpsertAsync(VectorUpsertRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentVector>> SearchAsync(Guid orgId, string query, int topK, Guid? matterId, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid documentId, CancellationToken cancellationToken = default);
}
