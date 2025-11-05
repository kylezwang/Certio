using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;

namespace Certio.Application.Interfaces;

public interface IWebhookHandlerService
{
    Task HandleGoogleDriveAsync(GoogleDriveChangeNotification notification, CancellationToken cancellationToken = default);

    Task HandleMicrosoftGraphAsync(MicrosoftGraphChangeNotification notification, CancellationToken cancellationToken = default);
}
