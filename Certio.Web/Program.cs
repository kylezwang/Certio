using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Certio.Infrastructure.Data;
using Certio.Web.Hubs;
using Certio.Web.Middleware;
using Certio.Web.Security;
using Certio.Web.Services;
using Certio.Application.Configuration;
using Certio.Application.Interfaces;
using Certio.Application.Services.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Azure;
using Azure.AI.DocumentIntelligence;
using System.Text.RegularExpressions;

// Set up environment variables for Windows development
static void SetupEnvironmentVariables()
{
    // Load from .env file if it exists
    var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (File.Exists(envFile))
    {
        Console.WriteLine("📁 Loading environment variables from .env file...");
        var lines = File.ReadAllLines(envFile);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;
                
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
                Console.WriteLine($"🔧 Loaded {key} from .env");
            }
        }
    }
    else
    {
        // .env file not found, using system environment variables
    }

    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQL_PASSWORD")))
    {
        Console.WriteLine("❌ SQL_PASSWORD environment variable is not set. Add it to your environment or .env file.");
    }
}

// Validate critical production configuration
static void ValidateProductionConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
{
    if (!environment.IsProduction())
        return;

    var errors = new List<string>();

    // Email webhook secrets are REQUIRED in production
    var gmailSecret = configuration["EmailIntegration:GmailVerificationToken"];
    var webhookSecret = configuration["EmailIntegration:WebhookSecret"];
    
    if (string.IsNullOrWhiteSpace(gmailSecret))
    {
        errors.Add("EmailIntegration:GmailVerificationToken is required in production");
    }
    
    if (string.IsNullOrWhiteSpace(webhookSecret))
    {
        errors.Add("EmailIntegration:WebhookSecret is required in production");
    }

    // USE_AZURE_SQL must be explicitly set in production
    var useAzureSql = Environment.GetEnvironmentVariable("USE_AZURE_SQL");
    if (useAzureSql != "true")
    {
        errors.Add("USE_AZURE_SQL environment variable must be set to 'true' in production");
    }

    if (errors.Any())
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n🚨 PRODUCTION CONFIGURATION ERRORS:");
        foreach (var error in errors)
        {
            Console.WriteLine($"   ❌ {error}");
        }
        Console.ResetColor();
        Console.WriteLine("\nApplication cannot start in production without required configuration.\n");
        throw new InvalidOperationException($"Production configuration validation failed: {string.Join("; ", errors)}");
    }
}

// Initialize environment variables
SetupEnvironmentVariables();

// Smart database selection - use local SQL Server by default for reliability
static async Task<string> GetConnectionStringAsync(IConfiguration configuration, IWebHostEnvironment environment)
{
    // Check if user explicitly wants to use Azure SQL
    var useAzureSql = Environment.GetEnvironmentVariable("USE_AZURE_SQL");
    if (useAzureSql == "true")
    {
        // In production, trust the connection string is correct and skip connectivity check
        if (environment.IsProduction())
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Azure SQL connection string (DefaultConnection) not found in production. Check your Azure App Service connection strings.");
            }
            
            // If connection string contains ${DB_PASSWORD} placeholder, replace it with actual password
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            if (!string.IsNullOrWhiteSpace(dbPassword) && connectionString.Contains("${DB_PASSWORD}"))
    {
                connectionString = connectionString.Replace("${DB_PASSWORD}", dbPassword);
                Console.WriteLine("🔧 Replaced ${DB_PASSWORD} placeholder in connection string");
            }
            else if (connectionString.Contains("${DB_PASSWORD}"))
            {
                Console.WriteLine("❌ ERROR: DB_PASSWORD environment variable is not set but connection string contains ${DB_PASSWORD}");
                throw new InvalidOperationException("DB_PASSWORD environment variable is required when connection string contains ${DB_PASSWORD} placeholder");
            }
            
            // Log connection string (mask password for security)
            var maskedConnectionString = System.Text.RegularExpressions.Regex.Replace(
                connectionString, 
                @"Password=([^;]+)", 
                "Password=***MASKED***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Console.WriteLine($"🌐 Using Azure SQL Database (production)");
            Console.WriteLine($"📋 Connection string (masked): {maskedConnectionString}");
            
            return connectionString;
        }
        
        // In development, optionally check connectivity but don't fail if check fails
        if (await HasInternetConnectivityAsync())
        {
            var devConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(devConnectionString))
            {
               throw new InvalidOperationException("Azure SQL connection string not found");
            }
            
            // Replace password placeholder if needed
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            if (!string.IsNullOrWhiteSpace(dbPassword) && devConnectionString.Contains("${DB_PASSWORD}"))
            {
                devConnectionString = devConnectionString.Replace("${DB_PASSWORD}", dbPassword);
            }
            
            Console.WriteLine("🌐 Using Azure SQL Database (explicitly requested)");
            return devConnectionString;
    }
    else
    {
            Console.WriteLine("⚠️ Azure SQL connectivity check failed, falling back to local SQL Server");
        }
    }
    
    // Fall back to local SQL Server (development only)
    if (environment.IsProduction())
    {
        throw new InvalidOperationException("USE_AZURE_SQL must be set to 'true' in production. Local SQL Server is not available in Azure.");
    }
    
        Console.WriteLine("📱 Using local SQL Server (default)");
        // Start local SQL Server if not running
        await EnsureLocalSqlServerRunningAsync();
        var localPassword = Environment.GetEnvironmentVariable("SQL_PASSWORD");
        if (string.IsNullOrEmpty(localPassword))
        {
            throw new InvalidOperationException("SQL_PASSWORD environment variable is required for local development");
        }
        
        return $"Server=localhost,1433;Database=CertioLocal;User Id=sa;Password={localPassword};TrustServerCertificate=true;";
}

// Check if Azure SQL database is actually accessible (development only)
static async Task<bool> HasInternetConnectivityAsync()
{
    try
    {
        // First check if we have internet connectivity
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(3);
        
        // Try to ping a reliable internet service
        var response = await client.GetAsync("https://www.google.com");
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        
        // Try to get connection string from configuration or environment
        var azureConnectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING");
        if (string.IsNullOrEmpty(azureConnectionString))
        {
            // If not in env var, try to get from DefaultConnection (for development)
            return false; // Skip connectivity check if connection string not explicitly provided
        }
        
        // Try to connect to Azure SQL with a very short timeout
        // Add Connection Timeout=3 to the connection string if not already present
        var testConnectionString = azureConnectionString;
        if (!testConnectionString.Contains("Connection Timeout", StringComparison.OrdinalIgnoreCase))
        {
            testConnectionString += ";Connection Timeout=3";
        }
        else
        {
            // Replace existing timeout with shorter one for test
            testConnectionString = Regex.Replace(
                testConnectionString, 
                @"Connection\s+Timeout\s*=\s*\d+", 
                "Connection Timeout=3", 
                RegexOptions.IgnoreCase);
        }
        
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(testConnectionString);
        await connection.OpenAsync();
        return true;
    }
    catch
    {
        return false;
    }
}

// Ensure local SQL Server is running
static async Task EnsureLocalSqlServerRunningAsync()
{
    try
    {
        // Check if SQL Server container is running
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps --filter name=certio-sqlserver --format {{.Status}}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        if (!output.Contains("Up"))
        {
            Console.WriteLine("🚀 Starting local SQL Server...");
            var startProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker-compose",
                    Arguments = "up -d sqlserver",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            startProcess.Start();
            await startProcess.WaitForExitAsync();
            
            // Wait for SQL Server to be ready
            await Task.Delay(10000);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Could not start local SQL Server: {ex.Message}");
        Console.WriteLine("Please run: docker-compose up -d sqlserver");
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var isDevelopment = builder.Environment.IsDevelopment();

// Map flat env vars to hierarchical configuration keys (so .env or shell vars override appsettings)
var googleMapsEnvKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
if (!string.IsNullOrWhiteSpace(googleMapsEnvKey))
{
    builder.Configuration["GoogleMaps:ApiKey"] = googleMapsEnvKey;
}

var aiApiKey = Environment.GetEnvironmentVariable("AI_API_KEY");
if (!string.IsNullOrWhiteSpace(aiApiKey))
{
    builder.Configuration["AIService:ApiKey"] = aiApiKey;
}

// Map EmailIntegration environment variables
var gmailClientId = Environment.GetEnvironmentVariable("GMAIL_CLIENT_ID");
if (!string.IsNullOrWhiteSpace(gmailClientId))
{
    builder.Configuration["EmailIntegration:Gmail:ClientId"] = gmailClientId;
}

var gmailClientSecret = Environment.GetEnvironmentVariable("GMAIL_CLIENT_SECRET");
if (!string.IsNullOrWhiteSpace(gmailClientSecret))
{
    builder.Configuration["EmailIntegration:Gmail:ClientSecret"] = gmailClientSecret;
}

var gmailRedirectUri = Environment.GetEnvironmentVariable("GMAIL_REDIRECT_URI");
if (!string.IsNullOrWhiteSpace(gmailRedirectUri))
{
    // Trim any leading/trailing whitespace and remove leading = if present (Azure App Service quirk)
    gmailRedirectUri = gmailRedirectUri.Trim().TrimStart('=');
    builder.Configuration["EmailIntegration:Gmail:RedirectUri"] = gmailRedirectUri;
}

var outlookClientId = Environment.GetEnvironmentVariable("OUTLOOK_CLIENT_ID");
if (!string.IsNullOrWhiteSpace(outlookClientId))
{
    builder.Configuration["EmailIntegration:Outlook:ClientId"] = outlookClientId;
}

var outlookClientSecret = Environment.GetEnvironmentVariable("OUTLOOK_CLIENT_SECRET");
if (!string.IsNullOrWhiteSpace(outlookClientSecret))
{
    builder.Configuration["EmailIntegration:Outlook:ClientSecret"] = outlookClientSecret;
}

var outlookRedirectUri = Environment.GetEnvironmentVariable("OUTLOOK_REDIRECT_URI");
if (!string.IsNullOrWhiteSpace(outlookRedirectUri))
{
    // Trim any leading/trailing whitespace and remove leading = if present (Azure App Service quirk)
    outlookRedirectUri = outlookRedirectUri.Trim().TrimStart('=');
    builder.Configuration["EmailIntegration:Outlook:RedirectUri"] = outlookRedirectUri;
}

// Map DocumentIntegration environment variables
var googleDriveClientId = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_ID");
if (!string.IsNullOrWhiteSpace(googleDriveClientId))
{
    builder.Configuration["DocumentIntegration:GoogleDrive:ClientId"] = googleDriveClientId;
}

var googleDriveClientSecret = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CLIENT_SECRET");
if (!string.IsNullOrWhiteSpace(googleDriveClientSecret))
{
    builder.Configuration["DocumentIntegration:GoogleDrive:ClientSecret"] = googleDriveClientSecret;
}

var googleDriveRedirectUri = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_REDIRECT_URI");
if (!string.IsNullOrWhiteSpace(googleDriveRedirectUri))
{
    // Trim any leading/trailing whitespace and remove leading = if present (Azure App Service quirk)
    googleDriveRedirectUri = googleDriveRedirectUri.Trim().TrimStart('=');
    builder.Configuration["DocumentIntegration:GoogleDrive:RedirectUri"] = googleDriveRedirectUri;
}

var oneDriveClientId = Environment.GetEnvironmentVariable("ONEDRIVE_CLIENT_ID");
if (!string.IsNullOrWhiteSpace(oneDriveClientId))
{
    builder.Configuration["DocumentIntegration:OneDrive:ClientId"] = oneDriveClientId;
}

var oneDriveClientSecret = Environment.GetEnvironmentVariable("ONEDRIVE_CLIENT_SECRET");
if (!string.IsNullOrWhiteSpace(oneDriveClientSecret))
{
    builder.Configuration["DocumentIntegration:OneDrive:ClientSecret"] = oneDriveClientSecret;
}

var oneDriveRedirectUri = Environment.GetEnvironmentVariable("ONEDRIVE_REDIRECT_URI");
if (!string.IsNullOrWhiteSpace(oneDriveRedirectUri))
{
    builder.Configuration["DocumentIntegration:OneDrive:RedirectUri"] = oneDriveRedirectUri;
}

// Map CalendarIntegration environment variables
var googleCalendarClientId = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_CLIENT_ID");
if (!string.IsNullOrWhiteSpace(googleCalendarClientId))
{
    builder.Configuration["CalendarIntegration:GoogleCalendar:ClientId"] = googleCalendarClientId;
}

var googleCalendarClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_CLIENT_SECRET");
if (!string.IsNullOrWhiteSpace(googleCalendarClientSecret))
{
    builder.Configuration["CalendarIntegration:GoogleCalendar:ClientSecret"] = googleCalendarClientSecret;
}

var googleCalendarRedirectUri = Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_REDIRECT_URI");
if (!string.IsNullOrWhiteSpace(googleCalendarRedirectUri))
{
    googleCalendarRedirectUri = googleCalendarRedirectUri.Trim().TrimStart('=');
    builder.Configuration["CalendarIntegration:GoogleCalendar:RedirectUri"] = googleCalendarRedirectUri;
}

var outlookCalendarClientId = Environment.GetEnvironmentVariable("OUTLOOK_CALENDAR_CLIENT_ID");
if (!string.IsNullOrWhiteSpace(outlookCalendarClientId))
{
    builder.Configuration["CalendarIntegration:OutlookCalendar:ClientId"] = outlookCalendarClientId;
}

var outlookCalendarClientSecret = Environment.GetEnvironmentVariable("OUTLOOK_CALENDAR_CLIENT_SECRET");
if (!string.IsNullOrWhiteSpace(outlookCalendarClientSecret))
{
    builder.Configuration["CalendarIntegration:OutlookCalendar:ClientSecret"] = outlookCalendarClientSecret;
}

var outlookCalendarRedirectUri = Environment.GetEnvironmentVariable("OUTLOOK_CALENDAR_REDIRECT_URI");
if (!string.IsNullOrWhiteSpace(outlookCalendarRedirectUri))
{
    outlookCalendarRedirectUri = outlookCalendarRedirectUri.Trim().TrimStart('=');
    builder.Configuration["CalendarIntegration:OutlookCalendar:RedirectUri"] = outlookCalendarRedirectUri;
}

var documentIntelligenceEndpoint = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT");
if (!string.IsNullOrWhiteSpace(documentIntelligenceEndpoint))
{
    builder.Configuration["DocumentExtraction:Endpoint"] = documentIntelligenceEndpoint;
}

var documentIntelligenceApiKey = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_API_KEY");
if (!string.IsNullOrWhiteSpace(documentIntelligenceApiKey))
{
    builder.Configuration["DocumentExtraction:ApiKey"] = documentIntelligenceApiKey;
}

// Map TwoFactorEmail/SMTP environment variables (supports SendGrid and Azure Communication Services Email)
var sendGridApiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
if (!string.IsNullOrWhiteSpace(sendGridApiKey))
{
    builder.Configuration["Security:TwoFactorEmail:SendGridApiKey"] = sendGridApiKey;
}

var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
if (!string.IsNullOrWhiteSpace(smtpHost))
{
    builder.Configuration["Security:TwoFactorEmail:SmtpHost"] = smtpHost;
}

var smtpPort = Environment.GetEnvironmentVariable("SMTP_PORT");
if (!string.IsNullOrWhiteSpace(smtpPort))
{
    builder.Configuration["Security:TwoFactorEmail:SmtpPort"] = smtpPort;
}

var smtpSecure = Environment.GetEnvironmentVariable("SMTP_SECURE");
if (!string.IsNullOrWhiteSpace(smtpSecure))
{
    builder.Configuration["Security:TwoFactorEmail:SmtpSecure"] = smtpSecure;
}

var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
if (!string.IsNullOrWhiteSpace(smtpUser))
{
    builder.Configuration["Security:TwoFactorEmail:SmtpUser"] = smtpUser;
}

var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
if (!string.IsNullOrWhiteSpace(smtpPassword))
{
    builder.Configuration["Security:TwoFactorEmail:SmtpPassword"] = smtpPassword;
}

var smtpFromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL");
if (!string.IsNullOrWhiteSpace(smtpFromEmail))
{
    builder.Configuration["Security:TwoFactorEmail:SmtpFromEmail"] = smtpFromEmail;
}

// Add HTTP Context Accessor for audit interceptor
builder.Services.AddHttpContextAccessor();

// Register audit interceptor as singleton
builder.Services.AddSingleton<Certio.Infrastructure.Interceptors.AuditInterceptor>();

// Validate production configuration before proceeding
ValidateProductionConfiguration(builder.Configuration, builder.Environment);

// Smart database selection based on internet connectivity
var connectionString = await GetConnectionStringAsync(builder.Configuration, builder.Environment);
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var interceptor = serviceProvider.GetRequiredService<Certio.Infrastructure.Interceptors.AuditInterceptor>();
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    })
           .AddInterceptors(interceptor);
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.SignIn.RequireConfirmedPhoneNumber = false;

    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 4;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddSignInManager()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Configure external OAuth providers (Google & Microsoft)
var authBuilder = builder.Services.AddAuthentication();

// Google OAuth
var googleClientId = builder.Configuration["Authentication:Google:ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.SaveTokens = true;
    });
    Console.WriteLine("[OAuth] Google authentication configured");
}
else
{
    Console.WriteLine("[OAuth] Google authentication not configured - missing ClientId or ClientSecret");
}

// Microsoft OAuth
var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_ID");
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_SECRET");
if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftClientSecret))
{
    authBuilder.AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;
        options.CallbackPath = "/signin-microsoft";
        options.SaveTokens = true;
    });
    Console.WriteLine("[OAuth] Microsoft authentication configured");
}
else
{
    Console.WriteLine("[OAuth] Microsoft authentication not configured - missing ClientId or ClientSecret");
}

// Configure authentication cookies with maximum security
builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie security settings
    options.Cookie.Name = "CertioAuth";
    options.Cookie.HttpOnly = true; // Prevent XSS attacks
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.Strict;
    options.Cookie.IsEssential = true; // Required for functionality
    options.Cookie.Path = "/"; // Explicit path
    
    // Session management
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Very short session timeout
    options.SlidingExpiration = true; // Reset timeout on activity
    options.SessionStore = null; // Use in-memory session store
    
    // Paths
    options.LoginPath = "/Home/Index";
    options.LogoutPath = "/Home/Index";
    options.AccessDeniedPath = "/Home/Index";
    
    // Additional security
    options.ReturnUrlParameter = "returnUrl";
});

// Configure data protection for additional security
builder.Services.AddDataProtection();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.Strict;
});

// Multi-level caching strategy (Discord/Slack style)
// 1. In-memory cache for hot data (fast, but not distributed)
builder.Services.AddMemoryCache();

// 2. Distributed cache selection
// Use Redis ONLY when an explicit connection string is provided.
// Otherwise use in-memory distributed cache to avoid timeouts in production.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
var useRedis = (builder.Configuration["Caching:UseRedis"] ?? Environment.GetEnvironmentVariable("USE_REDIS")) == "true";
var redisEnabled = !string.IsNullOrWhiteSpace(redisConnection) && (useRedis || redisConnection.Contains(".redis.cache.windows.net", StringComparison.OrdinalIgnoreCase));

if (redisEnabled)
{
    try
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "Certio_";
        });
        Console.WriteLine($"[Cache] Redis cache configured: {redisConnection}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Cache] Redis not available, using in-memory cache only: {ex.Message}");
        builder.Services.AddDistributedMemoryCache();
        redisEnabled = false;
    }
}
else
{
    Console.WriteLine("[Cache] Redis connection string not provided or USE_REDIS!=true. Using in-memory distributed cache.");
    builder.Services.AddDistributedMemoryCache();
}

// Record the resolved backend so the readiness probe can report it and distinguish
// "Redis is down" from "Redis was never configured".
builder.Services.AddSingleton(new Certio.Web.HealthChecks.CacheBackendInfo(
    redisEnabled
        ? Certio.Web.HealthChecks.CacheBackendInfo.Redis
        : Certio.Web.HealthChecks.CacheBackendInfo.InMemory));

// Readiness checks. These back /healthz, which the production deploy workflow uses as its final gate.
// Timeouts are deliberately shorter than that workflow's 15s HTTP timeout: without them an unreachable
// SQL server takes ~18s to report, so the probe would time out client-side instead of returning a
// diagnosable ok=false body.
builder.Services.AddHealthChecks()
    .AddCheck<Certio.Web.HealthChecks.DatabaseHealthCheck>(
        "database", failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(5))
    .AddCheck<Certio.Web.HealthChecks.DistributedCacheHealthCheck>(
        "distributed-cache", failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3));

// Register performance monitoring services
builder.Services.AddSingleton<Certio.Web.Services.CacheMetricsService>();
builder.Services.AddHostedService<Certio.Web.Services.MetricsReportingService>();
builder.Services.AddHostedService<Certio.Web.Services.EmailSyncService>();
builder.Services.AddHostedService<Certio.Web.Services.ChangeNoticeAutoReminderService>();

// Register cache service
builder.Services.AddSingleton<Certio.Web.Services.ICacheService, Certio.Web.Services.RedisCacheService>();
builder.Services.AddSingleton<Certio.Web.Services.ITwoFactorSessionStore, Certio.Web.Services.DistributedTwoFactorSessionStore>();

// mvc/razor/controllers + signalr
builder.Services.AddRazorPages();

// Join code service
builder.Services.AddScoped<Certio.Web.Services.IJoinCodeService, Certio.Web.Services.JoinCodeService>();

// Firm relationship services
builder.Services.AddScoped<IFirmRelationshipCacheService, FirmRelationshipCacheService>();
builder.Services.AddScoped<ILawFirmRoleResolutionService, LawFirmRoleResolutionService>();
builder.Services.AddScoped<IFirmAccessAuditService, FirmAccessAuditService>();

// PHASE 1 SECURITY SERVICES
builder.Services.AddScoped<Certio.Application.Interfaces.IAuditService, Certio.Application.Services.AuditService>();
builder.Services.AddScoped<Certio.Application.Interfaces.INotificationService, Certio.Web.Services.NotificationService>();
builder.Services.AddScoped<Certio.Web.Services.IBriefingMessageService, Certio.Web.Services.BriefingMessageService>();
builder.Services.AddScoped<Certio.Web.Security.AuthorizationHelper>();

// PHASE 2 SERVICE LAYER
// Register base PermissionService (without caching)
builder.Services.AddScoped<Certio.Application.Services.PermissionService>();
// Register CachedPermissionService as the IPermissionService implementation (PHASE 3)
builder.Services.AddScoped<Certio.Application.Interfaces.IPermissionService, Certio.Web.Services.CachedPermissionService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IOrganizationContextService, Certio.Application.Services.OrganizationContextService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IMatterService, Certio.Application.Services.MatterService>();
builder.Services.AddScoped<Certio.Application.Interfaces.ITaskService, Certio.Application.Services.TaskService>();
builder.Services.AddScoped<Certio.Application.Interfaces.ISubTaskService, Certio.Application.Services.SubTaskService>();
builder.Services.AddScoped<Certio.Application.Interfaces.ICalendarService, Certio.Application.Services.CalendarService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IOrganizationService, Certio.Application.Services.OrganizationService>();
builder.Services.AddScoped<Certio.Application.Interfaces.ITeamService, Certio.Application.Services.TeamService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IOrganizationRelationshipService, Certio.Application.Services.OrganizationRelationshipService>();

// AGENT ACTIONS AND UNIFIED INBOX (Phase 1 Agentic AI Workflows)
builder.Services.AddScoped<Certio.Application.Interfaces.IAgentActionService, Certio.Application.Services.AgentActionService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IUnifiedInboxService, Certio.Application.Services.UnifiedInboxService>();

// BILLING SERVICES
builder.Services.AddScoped<Certio.Application.Interfaces.IBillingService, Certio.Application.Services.BillingService>();

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Without a shared backplane, each SignalR hub (chat/direct/notifications/updates)
// only knows about connections on its own process. Behind a load balancer with
// more than one instance, a message sent from instance A never reaches a client
// connected to instance B - notifications, chat messages, and typing indicators
// silently fail to deliver depending on which instance handled the request. Reuse
// the same Redis connection already configured for distributed caching above.
var signalRBuilder = builder.Services.AddSignalR();
if (redisEnabled)
{
    signalRBuilder.AddStackExchangeRedis(redisConnection!, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Certio_SignalR");
    });
    Console.WriteLine("[SignalR] Redis backplane enabled - hubs will fan out across all instances");
}
else
{
    Console.WriteLine("[SignalR] Redis backplane disabled (single-instance mode) - configure ConnectionStrings:Redis before scaling to multiple instances");
}

// swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Client context + authorization
builder.Services.AddScoped<IClientContextAccessor, ClientContextAccessor>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationNotFoundMiddleware>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrgMember", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new OrgMemberRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, OrgMemberAuthorizationHandler>();

// AI Services
builder.Services.AddHttpClient<Certio.Application.Services.IAIAgentService, Certio.Application.Services.AIAgentService>();

// Named HttpClient for UserDataContextService to use
builder.Services.AddHttpClient("AIAgentService", client =>
{
    var aiServiceUrl = builder.Configuration["AIService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(aiServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("AIService:TimeoutSeconds", 30));
    
    var apiKey = builder.Configuration["AIService:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }
});

builder.Services.AddScoped<Certio.Application.Interfaces.IChatService, Certio.Web.Services.ChatService>();
builder.Services.AddSingleton<Certio.Web.Services.AIBackgroundService>();

// AI Usage Tracking Service
builder.Services.AddScoped<Certio.Web.Services.IAIUsageService, Certio.Web.Services.AIUsageService>();

// Direct Message Services
builder.Services.AddScoped<Certio.Application.Interfaces.IDirectMessageService, Certio.Web.Services.DirectMessageService>();

// User Data Context Service for comprehensive AI RAG across all modules
builder.Services.AddScoped<Certio.Application.Interfaces.IUserDataContextService, Certio.Application.Services.UserDataContextService>();

// Email Integration Services
builder.Services.AddScoped<Certio.Application.Interfaces.IEmailService, Certio.Web.Services.EmailService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IEmailToDmService, Certio.Web.Services.EmailToDmService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IEmailSendingService, Certio.Web.Services.EmailSendingService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IChangeNoticeService, Certio.Web.Services.ChangeNoticeService>();

// Channel Management Services
builder.Services.AddScoped<Certio.Web.Services.IChannelManagementService, Certio.Web.Services.ChannelManagementService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IChannelManagementService, Certio.Web.Services.ChannelManagementService>();
builder.Services.AddSingleton<Certio.Web.Services.IUserPresenceService, Certio.Web.Services.UserPresenceService>();

// Unified Document System Services
builder.Services.Configure<SecureDocumentExtractionOptions>(builder.Configuration.GetSection("DocumentExtraction"));
builder.Services.Configure<DocumentIntegrationOptions>(builder.Configuration.GetSection("DocumentIntegration"));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));

// Azure Document Intelligence (optional - only if configured)
builder.Services.AddSingleton<DocumentIntelligenceClient?>(sp =>
{
    var options = sp.GetRequiredService<IOptions<SecureDocumentExtractionOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    static bool IsUnset(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return true;
        }

        var trimmed = candidate.Trim().Trim('\"', '\'');

        if (trimmed.Length == 0)
        {
            return true;
        }

        if ((trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal)) ||
            (trimmed.StartsWith("%", StringComparison.Ordinal) && trimmed.EndsWith("%", StringComparison.Ordinal)))
        {
            return true;
        }

        return false;
    }

    if (IsUnset(options.Endpoint) || IsUnset(options.ApiKey))
    {
        logger.LogInformation("Azure Document Intelligence configuration missing or unresolved environment variables. Document extraction will use fallback methods.");
        return null; // Gracefully skip if not configured
    }

    if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
    {
        logger.LogWarning("Azure Document Intelligence endpoint '{Endpoint}' is not a valid absolute URI. Document extraction will use fallback methods.", options.Endpoint);
        return null;
    }

    try
    {
        var credential = new AzureKeyCredential(options.ApiKey);
        return new DocumentIntelligenceClient(endpoint, credential);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to initialize Azure Document Intelligence client. Document extraction will use fallback methods.");
        return null;
    }
});

builder.Services.AddScoped<IDriveSyncService>(sp =>
{
    var dbContext = sp.GetRequiredService<ApplicationDbContext>();
    var logger = sp.GetRequiredService<ILogger<DriveSyncService>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var dataProtectionProvider = sp.GetRequiredService<IDataProtectionProvider>();
    var protector = dataProtectionProvider.CreateProtector("DriveOAuthTokens");
    var documentIndexerService = sp.GetRequiredService<IDocumentIndexerService>();
    var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    
    Func<string, string> decryptFunc = encryptedToken => protector.Unprotect(encryptedToken);
    return new DriveSyncService(dbContext, logger, httpClientFactory, decryptFunc, documentIndexerService, serviceScopeFactory);
});

builder.Services.AddScoped<ICalendarSyncService>(sp =>
{
    var dbContext = sp.GetRequiredService<ApplicationDbContext>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<Certio.Application.Services.Calendar.CalendarSyncService>>();
    
    return new Certio.Application.Services.Calendar.CalendarSyncService(dbContext, httpClientFactory, configuration, logger);
});

builder.Services.AddScoped<IDocumentContentService>(sp =>
{
    var dbContext = sp.GetRequiredService<ApplicationDbContext>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<DocumentContentService>>();
    var dataProtectionProvider = sp.GetRequiredService<IDataProtectionProvider>();
    var protector = dataProtectionProvider.CreateProtector("DriveOAuthTokens");
    var options = sp.GetRequiredService<IOptions<SecureDocumentExtractionOptions>>();
    var integrationOptions = sp.GetRequiredService<IOptions<DocumentIntegrationOptions>>();
    var documentIntelligenceClient = sp.GetService<DocumentIntelligenceClient>(); // GetService returns null if not registered

    Func<string, string> decryptFunc = encrypted => protector.Unprotect(encrypted);
    Func<string, string> encryptFunc = plain => protector.Protect(plain);
    var fileStorageService = sp.GetRequiredService<IFileStorageService>();
    return new DocumentContentService(dbContext, httpClientFactory, decryptFunc, encryptFunc, options, integrationOptions, documentIntelligenceClient, fileStorageService, logger);
});
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IDocumentIndexerService, DocumentIndexerService>();
builder.Services.AddScoped<IVectorStoreService, VectorStoreService>();
builder.Services.AddScoped<IRagContextService, RagContextService>();
builder.Services.AddScoped<IDocumentAuditService, DocumentAuditService>();
builder.Services.AddScoped<IWebhookHandlerService, WebhookHandlerService>();
builder.Services.AddScoped<IDocumentEmbedService, DocumentEmbedService>();
builder.Services.AddSingleton<IEmbeddingJobQueue, EmbeddingJobQueue>();
builder.Services.AddHostedService<BackgroundEmbeddingWorker>();

// WOPI Services for Office Online Integration
builder.Services.AddScoped<WopiAccessTokenService>();
builder.Services.AddHttpClient(); // Register HttpClient factory
builder.Services.AddSingleton<WopiDiscoveryService>();

// User Sync Services
builder.Services.AddScoped<Certio.Web.Services.IUserSyncService, Certio.Web.Services.UserSyncService>();

// User Deletion Services
// Configure anonymization settings
builder.Services.Configure<Certio.Web.Configuration.AnonymizationSettings>(builder.Configuration.GetSection("AnonymizationSettings"));

// Configure Google Maps settings
builder.Services.Configure<Certio.Web.Configuration.GoogleMapsConfiguration>(builder.Configuration.GetSection("GoogleMaps"));

builder.Services.AddScoped<Certio.Web.Services.IUserDeletionService, Certio.Web.Services.UserDeletionService>();

// 2FA Services
builder.Services.AddScoped<Certio.Web.Services.ITwoFactorService, Certio.Web.Services.TwoFactorService>();
builder.Services.AddScoped<Certio.Web.Services.ITrustedDeviceService, Certio.Web.Services.TrustedDeviceService>();

// AI Service Configuration
builder.Services.Configure<AIServiceOptions>(builder.Configuration.GetSection("AIService"));

var app = builder.Build();

// First in the pipeline so the headers apply to every response, including static files and the
// exception handler's output.
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Performance monitoring middleware - should be early in the pipeline
app.UseMiddleware<Certio.Web.Middleware.PerformanceMonitoringMiddleware>();

app.UseSession();
app.UseRouting();

app.UseAuthentication();
app.UseUserSync(); // Automatically sync Identity users with custom User records

// Build client context AFTER user sync so CustomUser is available
app.UseMiddleware<ClientContextMiddleware>();

// Initialize default channels for organizations
app.UseChannelInitialization();

// Client access guard - must come after ClientContextMiddleware
app.UseClientAccessGuard();

app.UseAuthorization();

// Request-level audit logging (after auth/context so user info is available)
app.UseMiddleware<Certio.Web.Middleware.RequestAuditMiddleware>();

app.MapRazorPages();
// Enable attribute routing for API controllers
app.MapControllers();
// Client-scoped routes needed for MatterController (Create/Edit/etc.) under /Client/{orgId}/Matter
app.MapControllerRoute(
    name: "client_matter",
    pattern: "Client/{orgId:int}/Matter/{action=Index}/{id?}",
    defaults: new { controller = "Matter" });
// Client-scoped routes for ChatController under /Client/{orgId}/Chat
app.MapControllerRoute(
    name: "client_chat",
    pattern: "Client/{orgId:int}/Chat/{action=Index}/{id?}",
    defaults: new { controller = "Chat" });
app.MapControllerRoute(
    name: "admin",
    pattern: "admin/{controller=Admin}/{action=UserMigration}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<UpdatesHub>("/hubs/updates");
app.MapHub<Certio.Web.Hubs.ChatHub>("/hubs/chat");
app.MapHub<Certio.Web.Hubs.DirectHub>("/hubs/direct");
app.MapHub<Certio.Web.Hubs.NotificationHub>("/hubs/notifications");
// Readiness: probes SQL and the distributed cache. Returns 200 with ok=true only when every dependency
// answers, and 503 with ok=false otherwise. The production deploy workflow gates on the `ok` field, so a
// broken deployment now fails the gate instead of sailing through.
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

// Liveness: is the process up and serving? Intentionally has no dependency checks, so a transient SQL or
// Redis outage does not get the container killed and restarted while it is still able to recover.
app.MapHealthChecks("/livez", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    // Deliberately terse: this endpoint is unauthenticated, so it must not leak connection strings,
    // server names, or exception detail. Names and states only.
    var payload = new
    {
        ok = report.Status == HealthStatus.Healthy,
        status = report.Status.ToString(),
        durationMs = (int)report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            durationMs = (int)e.Value.Duration.TotalMilliseconds
        })
    };

    return context.Response.WriteAsJsonAsync(payload);
}

app.Run();

public class AIServiceOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}