using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;

namespace Certio.Application.Interfaces;

public interface IRagContextService
{
    Task<RagContextResult> BuildContextAsync(RagContextRequest request, CancellationToken cancellationToken = default);

    Task CacheContextAsync(RagContextResult result, CancellationToken cancellationToken = default);
}
