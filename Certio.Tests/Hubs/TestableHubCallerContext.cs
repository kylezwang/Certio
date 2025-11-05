using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;

namespace Certio.Tests.Hubs
{
    /// <summary>
    /// Testable HubCallerContext implementation for testing SignalR hubs
    /// </summary>
    internal class TestableHubCallerContext : HubCallerContext
    {
        private readonly HttpContext _httpContext;
        private readonly string _connectionId;
        private readonly ClaimsPrincipal _user;
        private readonly string _userIdentifier;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public TestableHubCallerContext(HttpContext httpContext, string connectionId, ClaimsPrincipal user, string userIdentifier)
        {
            _httpContext = httpContext;
            _connectionId = connectionId;
            _user = user;
            _userIdentifier = userIdentifier;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public override string ConnectionId => _connectionId;
        public override ClaimsPrincipal User => _user;
        public override string UserIdentifier => _userIdentifier;
        public override CancellationToken ConnectionAborted => _cancellationTokenSource.Token;
        public override IFeatureCollection Features => _httpContext.Features;
        public override IDictionary<object, object?> Items => _httpContext.Items;

        public override void Abort()
        {
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }
}

