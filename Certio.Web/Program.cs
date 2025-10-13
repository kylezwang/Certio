using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Certio.Infrastructure.Data;
using Certio.Web.Hubs;
using Certio.Web.Middleware;
using Certio.Web.Security;
using Certio.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;

// Set up environment variables for Windows development
static void SetupEnvironmentVariables()
{
    // Set default environment variables if not already set
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQL_PASSWORD")))
    {
        Environment.SetEnvironmentVariable("SQL_PASSWORD", "YourStrong@Passw0rd", EnvironmentVariableTarget.Process);
        Console.WriteLine("🔧 Set default SQL_PASSWORD environment variable");
    }
    
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USE_AZURE_SQL")))
    {
        Environment.SetEnvironmentVariable("USE_AZURE_SQL", "false", EnvironmentVariableTarget.Process);
        Console.WriteLine("🔧 Set USE_AZURE_SQL to false for local development");
    }
    
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
}

// Initialize environment variables
SetupEnvironmentVariables();

// Smart database selection - use local SQL Server by default for reliability
static async Task<string> GetConnectionStringAsync(IConfiguration configuration)
{
    // Check if user explicitly wants to use Azure SQL
    var useAzureSql = Environment.GetEnvironmentVariable("USE_AZURE_SQL");
    if (useAzureSql == "true" && await HasInternetConnectivityAsync())
    {
        Console.WriteLine("🌐 Using Azure SQL Database (explicitly requested)");
        return configuration.GetConnectionString("DefaultConnection") ?? 
               throw new InvalidOperationException("Azure SQL connection string not found");
    }
    else
    {
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
}

// Check if Azure SQL database is actually accessible
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
        
        // Now try to connect to Azure SQL with a very short timeout
        var azureConnectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING");
        if (string.IsNullOrEmpty(azureConnectionString))
        {
            return false;
        }
        
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(azureConnectionString);
        
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

// Smart database selection based on internet connectivity
var connectionString = await GetConnectionStringAsync(builder.Configuration);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
}).AddEntityFrameworkStores<ApplicationDbContext>();

// Configure authentication cookies with maximum security
builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie security settings
    options.Cookie.Name = "CertioAuth";
    options.Cookie.HttpOnly = true; // Prevent XSS attacks
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTPS when available
    options.Cookie.SameSite = SameSiteMode.Lax; // Allow cross-site for logout
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
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Allow HTTP in development
    options.Cookie.SameSite = SameSiteMode.Lax; // Less restrictive for development
});

// Multi-level caching strategy (Discord/Slack style)
// 1. In-memory cache for hot data (fast, but not distributed)
builder.Services.AddMemoryCache();

// 2. Redis distributed cache for shared data across instances
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
try
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "Certio_";
    });
    Console.WriteLine($"✅ Redis cache configured: {redisConnection}");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Redis not available, using in-memory cache only: {ex.Message}");
    // Fallback to memory cache if Redis is not available
    builder.Services.AddDistributedMemoryCache();
}

// Register cache service
builder.Services.AddSingleton<Certio.Web.Services.ICacheService, Certio.Web.Services.RedisCacheService>();

// mvc/razor/controllers + signalr
builder.Services.AddRazorPages();

// Join code service
builder.Services.AddScoped<Certio.Web.Services.IJoinCodeService, Certio.Web.Services.JoinCodeService>();

// Firm relationship services
builder.Services.AddScoped<IFirmRelationshipCacheService, FirmRelationshipCacheService>();
builder.Services.AddScoped<ILawFirmRoleResolutionService, LawFirmRoleResolutionService>();
builder.Services.AddScoped<IFirmAccessAuditService, FirmAccessAuditService>();

// PHASE 1 SECURITY SERVICES
builder.Services.AddScoped<Certio.Application.Interfaces.IAuditService, AuditService>();
builder.Services.AddScoped<Certio.Web.Security.AuthorizationHelper>();

// PHASE 2 SERVICE LAYER
builder.Services.AddScoped<Certio.Application.Interfaces.IPermissionService, Certio.Application.Services.PermissionService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IOrganizationContextService, Certio.Application.Services.OrganizationContextService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IMatterService, Certio.Application.Services.MatterService>();
builder.Services.AddScoped<Certio.Application.Interfaces.ITaskService, Certio.Application.Services.TaskService>();
builder.Services.AddScoped<Certio.Application.Interfaces.ISubTaskService, Certio.Application.Services.SubTaskService>();

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddSignalR();

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
builder.Services.AddScoped<Certio.Application.Interfaces.IChatService, Certio.Web.Services.ChatService>();
builder.Services.AddSingleton<Certio.Web.Services.AIBackgroundService>();

// Channel Management Services
builder.Services.AddScoped<Certio.Web.Services.IChannelManagementService, Certio.Web.Services.ChannelManagementService>();
builder.Services.AddSingleton<Certio.Web.Services.IUserPresenceService, Certio.Web.Services.UserPresenceService>();

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

// AI Service Configuration
builder.Services.Configure<AIServiceOptions>(builder.Configuration.GetSection("AIService"));

var app = builder.Build();

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

app.MapRazorPages();
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
// API routes for chat
app.MapControllerRoute(
    name: "api_chat",
    pattern: "api/chat/{action=Index}/{id?}",
    defaults: new { controller = "Chat" });
app.MapControllerRoute(
    name: "admin",
    pattern: "admin/{controller=Admin}/{action=UserMigration}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<UpdatesHub>("/hubs/updates");
app.MapHub<Certio.Web.Hubs.ChatHub>("/hubs/chat");
app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

app.Run();

public class AIServiceOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}