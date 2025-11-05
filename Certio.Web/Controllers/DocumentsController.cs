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

namespace Certio.Web.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IMatterService _matterService;
        private readonly IOrganizationService _organizationService;

        public DocumentsController(
            ApplicationDbContext db,
            IConfiguration configuration,
            IMatterService matterService,
            IOrganizationService organizationService)
        {
            _db = db;
            _configuration = configuration;
            _matterService = matterService;
            _organizationService = organizationService;
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

            ViewBag.OrganizationId = orgId;
            var org = await _db.Organizations.Where(o => o.Id == orgId).FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";
            ViewBag.OrganizationType = org?.Type ?? OrganizationType.Client;
            
            // Get the document orgId (convert from int to Guid)
            var documentOrgId = CreateDeterministicGuid("certio:organization", orgId);
            var documentUserId = CreateDeterministicGuid("certio:user", customUser.Id);

            // Load real documents from database
            var documents = await _db.Documents
                .Where(d => d.OrgId == documentOrgId && d.DeletedAt == null)
                .OrderByDescending(d => d.ModifiedAt)
                .Take(100) // Limit to 100 most recent documents
                .ToListAsync();

            var folders = new List<Certio.Domain.Documents.FolderInfo>
            {
                new Certio.Domain.Documents.FolderInfo { Name = "All Documents", Count = documents.Count, Icon = "FileText" },
                new Certio.Domain.Documents.FolderInfo { Name = "OneDrive", Count = documents.Count(d => d.SourceType == Certio.Domain.Documents.DocumentSourceType.OneDrive), Icon = "File" },
                new Certio.Domain.Documents.FolderInfo { Name = "Google Drive", Count = documents.Count(d => d.SourceType == Certio.Domain.Documents.DocumentSourceType.GoogleDrive), Icon = "File" },
                new Certio.Domain.Documents.FolderInfo { Name = "Internal", Count = documents.Count(d => d.SourceType == Certio.Domain.Documents.DocumentSourceType.InternalUpload), Icon = "FileText" }
            };

            var totalSize = documents.Count > 0 ? documents.Sum(d => d.FileSizeBytes) : 0;
            var storage = new Certio.Domain.Documents.StorageInfo
            {
                UsedGB = totalSize / (1024.0 * 1024.0 * 1024.0),
                TotalGB = 500,
                DocumentCount = documents.Count,
                SharedCount = documents.Count(d => !d.IsPrivate),
                RecentCount = documents.Count(d => d.ModifiedAt > DateTime.UtcNow.AddDays(-7))
            };

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

