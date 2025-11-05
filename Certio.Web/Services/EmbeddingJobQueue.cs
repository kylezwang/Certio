using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;

namespace Certio.Web.Services;

public sealed class EmbeddingJobQueue : IEmbeddingJobQueue
{
    private readonly Channel<DocumentIndexRequest> _channel;

    public EmbeddingJobQueue()
    {
        var options = new BoundedChannelOptions(200)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<DocumentIndexRequest>(options);
    }

    public ValueTask EnqueueAsync(DocumentIndexRequest request, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public ValueTask<DocumentIndexRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}

