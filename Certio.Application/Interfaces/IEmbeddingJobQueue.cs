using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;

namespace Certio.Application.Interfaces;

public interface IEmbeddingJobQueue
{
    ValueTask EnqueueAsync(DocumentIndexRequest request, CancellationToken cancellationToken = default);

    ValueTask<DocumentIndexRequest> DequeueAsync(CancellationToken cancellationToken);
}
