using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Infrastructure.Data;
using Certio.Web.ViewModels;
using Certio.Domain.Matters;
using Certio.Web.Services;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using System.Security.Cryptography;
using System.Text;
using Certio.Domain.Documents;
using Certio.Application.Services.Documents;
using Certio.Web.Attributes;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Certio.Web.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IMatterService _matterService;
        private readonly IOrganizationService _organizationService;
        private readonly IDocumentEmbedService _embedService;
        private readonly WopiAccessTokenService _wopiTokenService;
        private readonly WopiDiscoveryService _wopiDiscoveryService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public DocumentsController(
            ApplicationDbContext db,
            IConfiguration configuration,
            IMatterService matterService,
            IOrganizationService organizationService,
            IDocumentEmbedService embedService,
            WopiAccessTokenService wopiTokenService,
            WopiDiscoveryService wopiDiscoveryService,
            IHttpClientFactory httpClientFactory,
            IDataProtectionProvider dataProtectionProvider)
        {
            _db = db;
            _configuration = configuration;
            _matterService = matterService;
            _organizationService = organizationService;
            _embedService = embedService;
            _wopiTokenService = wopiTokenService;
            _wopiDiscoveryService = wopiDiscoveryService;
            _httpClientFactory = httpClientFactory;
            _dataProtectionProvider = dataProtectionProvider;
        }

        // GET /Client/{orgId}/Documents
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Documents")]
        public async Task<IActionResult> Index(int orgId)
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.CurrentUserId = customUser.Id;
            ViewBag.CurrentUserName = $"{customUser.FirstName} {customUser.LastName}".Trim();
            ViewBag.UserTimeZone = string.IsNullOrWhiteSpace(customUser.TimeZone) ? "America/New_York" : customUser.TimeZone;

            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? OrganizationType.Client;
            ViewBag.OrganizationEntity = org; // For custom terminology
            
            // Get the document orgId (convert from int to Guid)
            var documentOrgId = CreateDeterministicGuid("certio:organization", orgId);
            var documentUserId = CreateDeterministicGuid("certio:user", customUser.Id);

            // Don't load all documents upfront - use lazy loading instead
            // Just get counts for the sidebar
            var documentCount = await _db.Documents
                .Where(d => d.OrgId == documentOrgId && d.DeletedAt == null)
                .CountAsync();
            
            // For initial load, get a small sample (lazy loading will handle the rest)
            var documents = await _db.Documents
                .Where(d => d.OrgId == documentOrgId && d.DeletedAt == null)
                .OrderByDescending(d => d.ModifiedAt)
                .Take(25) // Only load first 25, rest will load lazily
                .ToListAsync();

            // Get accurate counts for sidebar (from database, not just loaded documents)
            var oneDriveCount = await _db.Documents
                .Where(d => d.OrgId == documentOrgId && d.DeletedAt == null && d.SourceType == Certio.Domain.Documents.DocumentSourceType.OneDrive)
                .CountAsync();
            var googleDriveCount = await _db.Documents
                .Where(d => d.OrgId == documentOrgId && d.DeletedAt == null && d.SourceType == Certio.Domain.Documents.DocumentSourceType.GoogleDrive)
                .CountAsync();
            var internalCount = await _db.Documents
                .Where(d => d.OrgId == documentOrgId && d.DeletedAt == null && d.SourceType == Certio.Domain.Documents.DocumentSourceType.InternalUpload)
                .CountAsync();

            var folders = new List<Certio.Domain.Documents.FolderInfo>
            {
                new Certio.Domain.Documents.FolderInfo { Name = "All Documents", Count = documentCount, Icon = "FileText" },
                new Certio.Domain.Documents.FolderInfo { Name = "OneDrive", Count = oneDriveCount, Icon = "OneDrive" },
                new Certio.Domain.Documents.FolderInfo { Name = "Google Drive", Count = googleDriveCount, Icon = "GoogleDrive" }
            };

            // Fetch storage from connected Google Drive and OneDrive
            var storage = await GetStorageInfoAsync(documentOrgId, documentUserId);

            // ALWAYS prioritize real documents over sample data
            // Only show sample data if explicitly enabled AND no real documents exist
            var useSample = _configuration.GetValue<bool>("Features:UseSampleData");
            DocumentsViewModel model;
            
            // If we have real documents, always use them regardless of UseSampleData setting
            if (documents.Count > 0)
            {
                model = new DocumentsViewModel { Documents = documents, Folders = folders, Storage = storage };
            }
            // Only show sample data if no real documents exist AND sample data is enabled
            else if (useSample && documents.Count == 0)
            {
                // Only use sample data if no real documents exist
                var sampleOrgId = Guid.NewGuid();
                var sampleDocuments = new List<Certio.Domain.Documents.Document>
                {
                    new()
                    {
                        OrgId = sampleOrgId,
                        SourceType = Certio.Domain.Documents.DocumentSourceType.InternalUpload,
                        Title = "Morrison Industries - Service Agreement",
                        FileType = "application/pdf",
                        FileSizeBytes = 2400000,
                        Status = Certio.Domain.Documents.DocumentStatus.Final,
                        Category = "Contract",
                        IsPrivate = true,
                        CreatedAt = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc),
                        ModifiedAt = new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Utc),
                        Tags = new List<string> { "Contract", "Client", "Morrison" },
                        Metadata = new Dictionary<string, string?> { { "provider", "internal" } },
                        PreviewUrl = "https://docs.example.com/preview/1",
                        DownloadUrl = "https://docs.example.com/download/1",
                        EmbedUrl = "https://docs.example.com/embed/1",
                        Author = "Rachel Adams"
                    },
                    new()
                    {
                        OrgId = sampleOrgId,
                        SourceType = Certio.Domain.Documents.DocumentSourceType.GoogleDrive,
                        Title = "Compliance Audit Report Q4 2023",
                        FileType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        FileSizeBytes = 5100000,
                        Status = Certio.Domain.Documents.DocumentStatus.Published,
                        Category = "Compliance",
                        IsPrivate = false,
                        CreatedAt = new DateTime(2023, 12, 22, 15, 0, 0, DateTimeKind.Utc),
                        ModifiedAt = new DateTime(2024, 1, 12, 10, 0, 0, DateTimeKind.Utc),
                        Tags = new List<string> { "Compliance", "Audit", "Q4" },
                        Metadata = new Dictionary<string, string?>
                        {
                            { "provider", "google" },
                            { "driveFolder", "Audits" }
                        },
                        PreviewUrl = "https://drive.google.com/preview/abc",
                        DownloadUrl = "https://drive.google.com/download/abc",
                        EmbedUrl = "https://drive.google.com/embed/abc",
                        Author = "Compliance Team"
                    },
                    new()
                    {
                        OrgId = sampleOrgId,
                        SourceType = Certio.Domain.Documents.DocumentSourceType.InternalUpload,
                        Title = "Legal Research - AI Regulations",
                        FileType = "application/pdf",
                        FileSizeBytes = 1800000,
                        Status = Certio.Domain.Documents.DocumentStatus.Draft,
                        Category = "Research",
                        IsPrivate = true,
                        CreatedAt = new DateTime(2024, 1, 5, 9, 0, 0, DateTimeKind.Utc),
                        ModifiedAt = new DateTime(2024, 1, 10, 14, 45, 0, DateTimeKind.Utc),
                        Tags = new List<string> { "Research", "AI", "Regulations" },
                        Metadata = new Dictionary<string, string?> { { "provider", "internal" } },
                        PreviewUrl = "https://docs.example.com/preview/3",
                        DownloadUrl = "https://docs.example.com/download/3",
                        EmbedUrl = "https://docs.example.com/embed/3",
                        Author = "Legal Research Group"
                    },
                    new()
                    {
                        OrgId = sampleOrgId,
                        SourceType = Certio.Domain.Documents.DocumentSourceType.OneDrive,
                        Title = "Client Onboarding Presentation",
                        FileType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                        FileSizeBytes = 12300000,
                        Status = Certio.Domain.Documents.DocumentStatus.Review,
                        Category = "Presentation",
                        IsPrivate = false,
                        CreatedAt = new DateTime(2023, 12, 30, 16, 0, 0, DateTimeKind.Utc),
                        ModifiedAt = new DateTime(2024, 1, 8, 11, 15, 0, DateTimeKind.Utc),
                        Tags = new List<string> { "Presentation", "Onboarding" },
                        Metadata = new Dictionary<string, string?>
                        {
                            { "provider", "microsoft" },
                            { "sharepointSite", "ClientSuccess" }
                        },
                        PreviewUrl = "https://onedrive.live.com/preview/xyz",
                        DownloadUrl = "https://onedrive.live.com/download/xyz",
                        EmbedUrl = "https://onedrive.live.com/embed/xyz",
                        Author = "Client Success"
                    },
                    new()
                    {
                        OrgId = sampleOrgId,
                        SourceType = Certio.Domain.Documents.DocumentSourceType.InternalUpload,
                        Title = "Contract Template Library",
                        FileType = "application/zip",
                        FileSizeBytes = 8700000,
                        Status = Certio.Domain.Documents.DocumentStatus.Final,
                        Category = "Templates",
                        IsPrivate = false,
                        CreatedAt = new DateTime(2023, 11, 2, 18, 0, 0, DateTimeKind.Utc),
                        ModifiedAt = new DateTime(2024, 1, 5, 13, 20, 0, DateTimeKind.Utc),
                        Tags = new List<string> { "Templates", "Contracts" },
                        Metadata = new Dictionary<string, string?> { { "provider", "internal" } },
                        PreviewUrl = "https://docs.example.com/preview/5",
                        DownloadUrl = "https://docs.example.com/download/5",
                        EmbedUrl = "https://docs.example.com/embed/5",
                        Author = "Template Working Group"
                    }
                };

                var sampleFolders = new List<Certio.Domain.Documents.FolderInfo>
                {
                    new Certio.Domain.Documents.FolderInfo { Name = "Contracts", Count = 24, Icon = "FileText" },
                    new Certio.Domain.Documents.FolderInfo { Name = "Compliance", Count = 12, Icon = "File" },
                    new Certio.Domain.Documents.FolderInfo { Name = "Research", Count = 18, Icon = "FileText" },
                    new Certio.Domain.Documents.FolderInfo { Name = "Templates", Count = 8, Icon = "Archive" },
                    new Certio.Domain.Documents.FolderInfo { Name = "Client Files", Count = 35, Icon = "File" }
                };

                var sampleStorage = new Certio.Domain.Documents.StorageInfo { UsedGB = 156.7, TotalGB = 500, DocumentCount = 247, SharedCount = 42, RecentCount = 15 };

                model = new DocumentsViewModel { Documents = sampleDocuments, Folders = sampleFolders, Storage = sampleStorage };
            }
            else
            {
                model = new DocumentsViewModel { Documents = documents, Folders = folders, Storage = storage };
            }

            // Fetch real matters from all accessible organizations (including relationships)
            var accessibleOrgsResult = await _organizationService.GetAccessibleOrganizationsAsync(customUser.Id);
            List<MatterDto> allMatters = new List<MatterDto>();
            
            if (accessibleOrgsResult.Success)
            {
                // Get matters from all accessible organizations
                foreach (var accessibleOrg in accessibleOrgsResult.Data!)
                {
                    var mattersResult = await _matterService.ListMattersAsync(customUser.Id, accessibleOrg.Id);
                    if (mattersResult.Success && mattersResult.Data != null)
                    {
                        allMatters.AddRange(mattersResult.Data);
                    }
                }
            }

            // Map DTOs to entities for view
            model.Matters = allMatters.Select(dto => new Matter
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Status = dto.Status,
                PracticeArea = dto.PracticeArea,
                CreatedAt = dto.CreatedAt,
                OrganizationId = dto.OrganizationId,
                DueDate = dto.DueDate,
                StartDate = dto.StartDate,
                CompletedDate = dto.CompletedDate,
                AccessLevel = dto.AccessLevel,
                TeamId = dto.TeamId,
                ClientId = dto.ClientId,
                Assignments = dto.Assignments.Select(a => new MatterAssignment
                {
                    Id = a.Id,
                    MatterId = a.MatterId,
                    UserId = a.UserId,
                    AssignmentType = a.AssignmentType,
                    Role = a.Role,
                    IsNotifyRecipient = a.IsNotifyRecipient,
                    AssignedAt = a.AssignedAt,
                    User = a.User != null ? new User
                    {
                        Id = a.User.Id,
                        FirstName = a.User.FirstName,
                        LastName = a.User.LastName,
                        Email = a.User.Email
                    } : null!
                }).ToList(),
                TaskItems = Enumerable.Range(0, dto.TasksCompleted)
                    .Select(_ => new Certio.Domain.Tasks.TaskItem { Status = "Completed" })
                    .Concat(Enumerable.Range(0, dto.TotalTasks - dto.TasksCompleted)
                        .Select(_ => new Certio.Domain.Tasks.TaskItem { Status = "Pending" }))
                    .ToList()
            }).ToList();

            return View("~/Views/Home/Documents.cshtml", model);
        }

        // GET /Client/{orgId}/Documents/{documentId}
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Documents/{documentId:guid}")]
        [ViewAudit("Document", "documentId")]
        public async Task<IActionResult> View(int orgId, Guid documentId)
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            if (customUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.FindAsync(orgId);
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? OrganizationType.Client;
            ViewBag.OrganizationEntity = org; // For custom terminology

            // Get the document orgId (convert from int to Guid)
            var documentOrgId = CreateDeterministicGuid("certio:organization", orgId);
            var documentUserId = CreateDeterministicGuid("certio:user", customUser.Id);

            // Load document from database
            var document = await _db.Documents
                .Where(d => d.Id == documentId && d.OrgId == documentOrgId && d.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (document == null)
            {
                return NotFound();
            }

            // Build embed URL - use WOPI for OneDrive documents
            string embedUrl;
            string openUrl;

            if (document.SourceType == DocumentSourceType.OneDrive && !string.IsNullOrWhiteSpace(document.ExternalFileId))
            {
                // Use WOPI for OneDrive documents
                embedUrl = await BuildWopiEmbedUrlAsync(document, documentOrgId, documentUserId);
                openUrl = document.PreviewUrl ?? document.EmbedUrl ?? document.DownloadUrl ?? string.Empty;
            }
            else
            {
                // Use existing logic for Google Drive and other documents
                embedUrl = BuildEmbedUrl(document);
                openUrl = document.EmbedUrl ?? document.PreviewUrl ?? document.DownloadUrl ?? string.Empty;
            }

            var viewModel = new DocumentViewViewModel
            {
                Document = document,
                EmbedUrl = embedUrl,
                OpenUrl = openUrl
            };

            return View("~/Views/Documents/View.cshtml", viewModel);
        }

        private async Task<string> BuildWopiEmbedUrlAsync(Document document, Guid orgId, Guid userId)
        {
            try
            {
                // Check if we're running on localhost - WOPI won't work due to Office Online being external
                var host = Request.Host.ToString();
                var isLocalhost = host.Contains("localhost") || host.StartsWith("127.0.0.1") || host.StartsWith("0.0.0.0");
                
                if (isLocalhost)
                {
                    // For localhost development, fallback to Office Online public viewer
                    // WOPI requires publicly accessible URLs that Office Online servers can reach
                    Console.WriteLine("WOPI: Running on localhost - falling back to Office Online viewer");
                    return BuildEmbedUrl(document);
                }

                // Generate WOPI access token
                var accessToken = _wopiTokenService.GenerateAccessToken(document.Id, orgId, userId, TimeSpan.FromHours(8));

                // Get WOPI discovery data
                var discovery = await _wopiDiscoveryService.GetDiscoveryDataAsync();
                if (discovery == null)
                {
                    Console.WriteLine("WOPI: Discovery data not available - falling back");
                    return BuildEmbedUrl(document);
                }

                // Determine the app and action based on file type
                var (appName, extension) = GetAppNameAndExtension(document);
                var wopiSrc = _wopiDiscoveryService.GetActionUrl(appName, "embedview", extension);

                if (string.IsNullOrWhiteSpace(wopiSrc))
                {
                    // Try "view" action if "embedview" is not available
                    wopiSrc = _wopiDiscoveryService.GetActionUrl(appName, "view", extension);
                }

                if (string.IsNullOrWhiteSpace(wopiSrc))
                {
                    Console.WriteLine($"WOPI: No action URL found for {appName} - falling back");
                    return BuildEmbedUrl(document);
                }

                // Build WOPI URL with production host
                var scheme = Request.Scheme;
                var wopiFileUrl = $"{scheme}://{host}/wopi/files/{document.Id}";

                Console.WriteLine($"WOPI: Building URL with host: {host}");
                Console.WriteLine($"WOPI: WOPISrc: {wopiFileUrl}");
                Console.WriteLine($"WOPI: Action URL template: {wopiSrc}");

                // Replace placeholders in WOPI action URL
                var wopiUrl = wopiSrc
                    .Replace("<ui=UI_LLCC&>", "")
                    .Replace("<rs=DC_LLCC&>", "")
                    .Replace("<dchat=DISABLE_CHAT&>", "dchat=1&")
                    .Replace("<embed=EMBEDDED&>", "embed=1&")
                    .Replace("<hid=HOST_SESSION_ID&>", "")
                    .Replace("<sc=SESSION_CONTEXT&>", "")
                    .Replace("<showpagestats=PERFSTATS&>", "");

                // Add WOPI source and access token
                var separator = wopiUrl.Contains('?') ? '&' : '?';
                var fullUrl = $"{wopiUrl}{separator}WOPISrc={Uri.EscapeDataString(wopiFileUrl)}&access_token={Uri.EscapeDataString(accessToken)}";

                Console.WriteLine($"WOPI: Final URL: {fullUrl.Substring(0, Math.Min(200, fullUrl.Length))}...");
                return fullUrl;
            }
            catch (Exception ex)
            {
                // Log error and fallback to old method
                Console.WriteLine($"WOPI Error: {ex.Message}");
                Console.WriteLine($"WOPI Stack: {ex.StackTrace}");
                return BuildEmbedUrl(document);
            }
        }

        private static (string appName, string extension) GetAppNameAndExtension(Document document)
        {
            var fileType = document.FileType?.ToLowerInvariant() ?? string.Empty;
            var title = document.Title?.ToLowerInvariant() ?? string.Empty;

            // Determine app name and extension
            if (fileType.Contains("word") || fileType.Contains("document") || title.EndsWith(".docx") || title.EndsWith(".doc"))
            {
                return ("Word", ".docx");
            }
            else if (fileType.Contains("excel") || fileType.Contains("spreadsheet") || title.EndsWith(".xlsx") || title.EndsWith(".xls"))
            {
                return ("Excel", ".xlsx");
            }
            else if (fileType.Contains("powerpoint") || fileType.Contains("presentation") || title.EndsWith(".pptx") || title.EndsWith(".ppt"))
            {
                return ("PowerPoint", ".pptx");
            }
            else if (fileType.Contains("pdf") || title.EndsWith(".pdf"))
            {
                return ("Word", ".pdf"); // Office Online can view PDFs through Word viewer
            }
            else
            {
                return ("Word", ".docx"); // Default to Word
            }
        }

        private static string BuildEmbedUrl(Document document)
        {
            var provider = GetDocumentProvider(document);
            var embedUrl = document.EmbedUrl ?? document.PreviewUrl ?? document.DownloadUrl ?? string.Empty;

            if (string.IsNullOrWhiteSpace(embedUrl))
            {
                // If no URL but we have external file ID for Google, construct it
                if (provider == "google" && !string.IsNullOrWhiteSpace(document.ExternalFileId))
                {
                    embedUrl = $"https://docs.google.com/document/d/{document.ExternalFileId}/edit";
                }
                else
                {
                    return string.Empty;
                }
            }

            try
            {
                var uri = new Uri(embedUrl);

                // Handle Google Docs
                if (provider == "google" || document.SourceType == DocumentSourceType.GoogleDrive)
                {
                    var builder = new UriBuilder(uri);
                    var existingQuery = uri.Query.TrimStart('?');
                    var queryParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(existingQuery))
                    {
                        queryParts.Add(existingQuery);
                    }
                    queryParts.Add("rm=embedded");
                    queryParts.Add("embedded=true");
                    builder.Query = string.Join("&", queryParts);
                    return builder.ToString();
                }

                // Handle Microsoft OneDrive/Office Online
                if (provider == "microsoft" || provider == "onedrive" || document.SourceType == DocumentSourceType.OneDrive)
                {
                    // For Office documents (Word, Excel, PowerPoint), use Office Online embed viewer
                    // This is the most reliable method for embedding OneDrive documents
                    var isOfficeDocument = document.FileType?.ToLowerInvariant() switch
                    {
                        var ft when ft.Contains("word") => true,
                        var ft when ft.Contains("excel") => true,
                        var ft when ft.Contains("powerpoint") => true,
                        var ft when ft.Contains("openxmlformats") => true,
                        "application/vnd.ms-word" => true,
                        "application/vnd.ms-excel" => true,
                        "application/vnd.ms-powerpoint" => true,
                        _ => false
                    };

                    // First, try to use the download URL with Office Online embed viewer
                    // This works for most OneDrive documents that are shared or accessible
                    if (isOfficeDocument && !string.IsNullOrWhiteSpace(document.DownloadUrl))
                    {
                        return $"https://view.officeapps.live.com/op/embed.aspx?src={Uri.EscapeDataString(document.DownloadUrl)}";
                    }

                    // For SharePoint-hosted documents, add web=1 parameter
                    if (uri.Host.Contains("sharepoint.com"))
                    {
                        var builder = new UriBuilder(uri);
                        var existingQuery = uri.Query.TrimStart('?');
                        var queryParts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(existingQuery))
                        {
                            queryParts.Add(existingQuery);
                        }
                        queryParts.Add("web=1");
                        queryParts.Add("wdAllowInteractivity=1");
                        var action = ExtractQueryParam(uri.Query, "action") ?? "embedview";
                        queryParts.Add($"action={Uri.EscapeDataString(action)}");
                        builder.Query = string.Join("&", queryParts);
                        return builder.ToString();
                    }

                    // For onedrive.live.com URLs, try to extract embed parameters
                    if (uri.Host.Contains("onedrive.live.com"))
                    {
                        var query = uri.Query;
                        var resid = ExtractQueryParam(query, "resid");
                        var cid = ExtractQueryParam(query, "cid");
                        var authkey = ExtractQueryParam(query, "authkey");

                        // If we have the necessary parameters, construct a proper embed URL
                        if (!string.IsNullOrWhiteSpace(resid))
                        {
                            var embedParams = new System.Text.StringBuilder();
                            embedParams.Append($"resid={Uri.EscapeDataString(resid)}");
                            if (!string.IsNullOrWhiteSpace(cid))
                                embedParams.Append($"&cid={Uri.EscapeDataString(cid)}");
                            if (!string.IsNullOrWhiteSpace(authkey))
                                embedParams.Append($"&authkey={Uri.EscapeDataString(authkey)}");
                            embedParams.Append("&action=embedview");
                            embedParams.Append("&em=2");
                            embedParams.Append("&wdAllowInteractivity=1");
                            embedParams.Append("&wdDownloadButton=0");
                            embedParams.Append("&wdInConfigurator=1");
                            return $"https://onedrive.live.com/embed?{embedParams}";
                        }

                        // Fallback: If no proper embed parameters but we have a download URL, use Office Online viewer
                        if (isOfficeDocument && !string.IsNullOrWhiteSpace(document.DownloadUrl))
                        {
                            return $"https://view.officeapps.live.com/op/embed.aspx?src={Uri.EscapeDataString(document.DownloadUrl)}";
                        }
                    }

                    // Last resort: Use Office Online viewer for any Office document with a download URL
                    if (isOfficeDocument && !string.IsNullOrWhiteSpace(document.DownloadUrl))
                    {
                        return $"https://view.officeapps.live.com/op/embed.aspx?src={Uri.EscapeDataString(document.DownloadUrl)}";
                    }
                }

                // For PDFs, use Google Docs viewer as fallback
                if (!string.IsNullOrWhiteSpace(document.DownloadUrl) && 
                    document.FileType?.ToLowerInvariant().Contains("pdf") == true)
                {
                    return $"https://docs.google.com/gview?embedded=1&url={Uri.EscapeDataString(document.DownloadUrl)}";
                }

                return embedUrl;
            }
            catch
            {
                return embedUrl; // Return original URL if parsing fails
            }
        }

        private static string? ExtractQueryParam(string query, string paramName)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            query = query.TrimStart('?');
            var parts = query.Split('&');
            foreach (var part in parts)
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length == 2 && keyValue[0].Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(keyValue[1]);
                }
            }
            return null;
        }

        private static string GetDocumentProvider(Document document)
        {
            if (document.Metadata != null && document.Metadata.TryGetValue("provider", out var provider) && !string.IsNullOrWhiteSpace(provider))
            {
                return provider.ToLowerInvariant();
            }

            return document.SourceType switch
            {
                DocumentSourceType.GoogleDrive => "google",
                DocumentSourceType.OneDrive => "microsoft",
                _ => "internal"
            };
        }

        private async Task<Certio.Domain.Documents.StorageInfo> GetStorageInfoAsync(Guid orgId, Guid userId)
        {
            double usedGB = 0;
            double totalGB = 0;

            // Get Google Drive connection
            var googleConnection = await _db.ExternalConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(ec => ec.OrgId == orgId && ec.UserId == userId && ec.Provider == ExternalConnectionProvider.Google);

            // Get OneDrive connection
            var oneDriveConnection = await _db.ExternalConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(ec => ec.OrgId == orgId && ec.UserId == userId && ec.Provider == ExternalConnectionProvider.Microsoft);

            // Fetch Google Drive storage quota if connected
            if (googleConnection != null && googleConnection.TokenExpiry > DateTime.UtcNow.AddMinutes(5))
            {
                try
                {
                    var googleStorage = await GetGoogleDriveStorageAsync(googleConnection.AccessToken);
                    if (googleStorage.HasValue)
                    {
                        usedGB += googleStorage.Value.UsedGB;
                        totalGB += googleStorage.Value.TotalGB;
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - continue with other storage sources
                    Console.WriteLine($"Error fetching Google Drive storage: {ex.Message}");
                }
            }

            // Fetch OneDrive storage quota if connected
            if (oneDriveConnection != null && oneDriveConnection.TokenExpiry > DateTime.UtcNow.AddMinutes(5))
            {
                try
                {
                    var oneDriveStorage = await GetOneDriveStorageAsync(oneDriveConnection.AccessToken);
                    if (oneDriveStorage.HasValue)
                    {
                        usedGB += oneDriveStorage.Value.UsedGB;
                        totalGB += oneDriveStorage.Value.TotalGB;
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - continue with other storage sources
                    Console.WriteLine($"Error fetching OneDrive storage: {ex.Message}");
                }
            }

            // Get document counts and calculate internal storage
            var allDocuments = await _db.Documents
                .Where(d => d.OrgId == orgId && d.DeletedAt == null)
                .ToListAsync();

            // Only show storage for OAuth-connected services (Google Drive and OneDrive)
            // Internal storage is not included in the storage progress bar
            // Check if connections exist (regardless of token expiry) to determine if we should show progress bar
            // If we have connections but couldn't fetch storage (API failure or expired token), still show progress bar
            bool hasConnection = googleConnection != null || oneDriveConnection != null;
            
            if (!hasConnection)
            {
                totalGB = 0; // No connections, so no storage to display
            }
            else if (hasConnection && totalGB == 0)
            {
                // We have a connection but storage fetch failed or token expired - use a default to show progress bar
                // This handles cases where API calls fail, tokens expired, or storage quota is unavailable
                totalGB = 15; // Default 15GB (typical free tier) - user will see 0/X until API succeeds
            }

            return new Certio.Domain.Documents.StorageInfo
            {
                UsedGB = usedGB,
                TotalGB = totalGB,
                DocumentCount = allDocuments.Count,
                SharedCount = allDocuments.Count(d => !d.IsPrivate),
                RecentCount = allDocuments.Count(d => d.ModifiedAt > DateTime.UtcNow.AddDays(-7))
            };
        }

        private async Task<(double UsedGB, double TotalGB)?> GetGoogleDriveStorageAsync(string encryptedToken)
        {
            try
            {
                var protector = _dataProtectionProvider.CreateProtector("DriveOAuthTokens");
                var accessToken = protector.Unprotect(encryptedToken);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetAsync("https://www.googleapis.com/drive/v3/about?fields=storageQuota");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("storageQuota", out var storageQuota))
                {
                    return null;
                }

                var limit = storageQuota.TryGetProperty("limit", out var limitProp) && limitProp.ValueKind == JsonValueKind.String
                    ? long.TryParse(limitProp.GetString(), out var parsedLimit) ? parsedLimit : 0
                    : 0;
                var usage = storageQuota.TryGetProperty("usage", out var usageProp) && usageProp.ValueKind == JsonValueKind.String
                    ? long.TryParse(usageProp.GetString(), out var parsedUsage) ? parsedUsage : 0
                    : 0;

                return (usage / (1024.0 * 1024.0 * 1024.0), limit / (1024.0 * 1024.0 * 1024.0));
            }
            catch
            {
                return null;
            }
        }

        private async Task<(double UsedGB, double TotalGB)?> GetOneDriveStorageAsync(string encryptedToken)
        {
            try
            {
                var protector = _dataProtectionProvider.CreateProtector("DriveOAuthTokens");
                var accessToken = protector.Unprotect(encryptedToken);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me/drive?$select=quota");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("quota", out var quota))
                {
                    return null;
                }

                var total = quota.TryGetProperty("total", out var totalProp) ? totalProp.GetInt64() : 0;
                var used = quota.TryGetProperty("used", out var usedProp) ? usedProp.GetInt64() : 0;

                return (used / (1024.0 * 1024.0 * 1024.0), total / (1024.0 * 1024.0 * 1024.0));
            }
            catch
            {
                return null;
            }
        }

        private static Guid CreateDeterministicGuid(string namespacePrefix, int value)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{namespacePrefix}:{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, 16).CopyTo(guidBytes);
            guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // Version 4
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // Variant RFC 4122
            return new Guid(guidBytes);
        }
    }
}

