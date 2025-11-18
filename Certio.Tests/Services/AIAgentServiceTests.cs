using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.Interfaces;
using Certio.Application.Services;
using Certio.Application.Services.Documents;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Certio.Tests.Services;

public class AIAgentServiceTests
{
    [Fact]
    public async Task SummarizeConversationAsync_AddsApiKeyHeader()
    {
        // Arrange
        var handler = new RecordingHandler();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"summary\":\"Test\",\"key_points\":[],\"sentiment\":\"Neutral\",\"urgency\":\"Low\",\"suggested_actions\":[]}", Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"AIService:BaseUrl", "http://localhost"},
                {"AIService:ApiKey", "test-api-key"},
                {"AIService:TimeoutSeconds", "30"}
            })
            .Build();

        var ragContextService = new Mock<IRagContextService>();
        var documentContentService = new Mock<IDocumentContentService>();
        documentContentService
            .Setup(s => s.FetchContentAsync(It.IsAny<Certio.Domain.Documents.Document>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DocumentContentResult.Empty("not_used"));

        var userDataContextService = new Mock<IUserDataContextService>();

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new ApplicationDbContext(dbOptions);

        var service = new AIAgentService(httpClient, configuration, ragContextService.Object, dbContext, documentContentService.Object, userDataContextService.Object, null);

        // Act
        await service.SummarizeConversationAsync("123", new List<ChatMessage>());

        // Assert
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest!.Headers.TryGetValues("X-API-Key", out var values));
        Assert.Contains("test-api-key", values);
    }

    private class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public HttpResponseMessage Response { get; set; } = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }
}

