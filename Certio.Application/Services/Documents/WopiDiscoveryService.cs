using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services.Documents;

public sealed class WopiDiscoveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WopiDiscoveryService> _logger;
    private WopiDiscoveryData? _cachedDiscovery;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public WopiDiscoveryService(IHttpClientFactory httpClientFactory, ILogger<WopiDiscoveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<WopiDiscoveryData?> GetDiscoveryDataAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedDiscovery != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedDiscovery;
        }

        try
        {
            // First, try to load from local discovery.xml file in project root
            var baseDirectory = AppContext.BaseDirectory;
            var localPath = Path.Combine(baseDirectory, "discovery.xml");
            
            // Also check in parent directories for development scenarios
            var possiblePaths = new[]
            {
                localPath,
                Path.Combine(Directory.GetCurrentDirectory(), "discovery.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "discovery.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "discovery.xml")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation("Loading WOPI discovery from local file: {Path}", path);
                    var localXml = await File.ReadAllTextAsync(path, cancellationToken);
                    _cachedDiscovery = ParseDiscoveryXml(localXml);
                    _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
                    return _cachedDiscovery;
                }
            }

            // If no local file, fetch from Microsoft
            _logger.LogInformation("WOPI discovery.xml not found locally, fetching from Microsoft");
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await httpClient.GetAsync(
                "https://onenote.officeapps.live.com/hosting/discovery", 
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch WOPI discovery: {StatusCode}", response.StatusCode);
                return null;
            }

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Save to local file for future use (in the current directory for convenience)
            try
            {
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "discovery.xml");
                await File.WriteAllTextAsync(savePath, xml, cancellationToken);
                _logger.LogInformation("Saved WOPI discovery to local file: {Path}", savePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save WOPI discovery to local file");
            }

            _cachedDiscovery = ParseDiscoveryXml(xml);
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            return _cachedDiscovery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching WOPI discovery data");
            return null;
        }
    }

    private WopiDiscoveryData ParseDiscoveryXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        
        var actions = new Dictionary<string, Dictionary<string, WopiActionInfo>>();

        var apps = doc.Descendants(ns + "app");
        foreach (var app in apps)
        {
            var appName = app.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(appName))
                continue;

            var appActions = new Dictionary<string, WopiActionInfo>();

            foreach (var action in app.Descendants(ns + "action"))
            {
                var actionName = action.Attribute("name")?.Value;
                var ext = action.Attribute("ext")?.Value;
                var urlSrc = action.Attribute("urlsrc")?.Value;

                if (string.IsNullOrWhiteSpace(actionName) || string.IsNullOrWhiteSpace(urlSrc))
                    continue;

                var key = string.IsNullOrWhiteSpace(ext) ? actionName : $"{actionName}_{ext}";
                appActions[key] = new WopiActionInfo
                {
                    Name = actionName,
                    Extension = ext,
                    UrlSrc = urlSrc,
                    RequiresHttps = action.Attribute("requires")?.Value?.Contains("https") ?? false
                };
            }

            actions[appName.ToLowerInvariant()] = appActions;
        }

        _logger.LogInformation("Parsed WOPI discovery data with {AppCount} apps", actions.Count);
        return new WopiDiscoveryData { Actions = actions };
    }

    public string? GetActionUrl(string appName, string actionName, string? extension = null)
    {
        if (_cachedDiscovery == null)
        {
            _logger.LogWarning("WOPI discovery data not loaded");
            return null;
        }

        appName = appName.ToLowerInvariant();
        if (!_cachedDiscovery.Actions.TryGetValue(appName, out var appActions))
        {
            _logger.LogWarning("App {AppName} not found in WOPI discovery", appName);
            return null;
        }

        // Try with extension first
        if (!string.IsNullOrWhiteSpace(extension))
        {
            var keyWithExt = $"{actionName}_{extension.TrimStart('.')}";
            if (appActions.TryGetValue(keyWithExt, out var actionInfo))
            {
                return actionInfo.UrlSrc;
            }
        }

        // Fallback to action without extension
        if (appActions.TryGetValue(actionName, out var fallbackAction))
        {
            return fallbackAction.UrlSrc;
        }

        _logger.LogWarning("Action {ActionName} not found for app {AppName}", actionName, appName);
        return null;
    }
}

public sealed class WopiDiscoveryData
{
    public Dictionary<string, Dictionary<string, WopiActionInfo>> Actions { get; set; } = new();
}

public sealed class WopiActionInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string UrlSrc { get; set; } = string.Empty;
    public bool RequiresHttps { get; set; }
}

