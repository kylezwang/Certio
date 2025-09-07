using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Certio.Web.Hubs
{
    [Authorize]
    public class UpdatesHub : Hub { }
}
