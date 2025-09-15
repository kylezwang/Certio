using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Certio.Web.Data;
using Certio.Web.Hubs;
using Certio.Web.Middleware;
using Microsoft.AspNetCore.Http;

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
        return "Server=localhost,1433;Database=CertioLocal;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;";
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
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            "Server=tcp:your-server.database.windows.net,1433;Initial Catalog=Certio;Persist Security Info=False;User ID=sql-login;Password=F1r3B@ll2025;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=3;");
        
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
                    WorkingDirectory = "/Users/chloetang/Documents/Certio",
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

// mvc/razor/controllers + signalr
builder.Services.AddRazorPages();

// Join code service
builder.Services.AddScoped<Certio.Web.Services.IJoinCodeService, Certio.Web.Services.JoinCodeService>();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// per-request tenant
builder.Services.AddScoped<TenantContext>();

// AI Services
builder.Services.AddHttpClient<Certio.Application.Services.IAIAgentService, Certio.Application.Services.AIAgentService>();
builder.Services.AddScoped<Certio.Application.Services.IChatService, Certio.Web.Services.ChatService>();
builder.Services.AddSingleton<Certio.Web.Services.AIBackgroundService>();

// User Sync Services
builder.Services.AddScoped<Certio.Web.Services.IUserSyncService, Certio.Web.Services.UserSyncService>();

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

// tenant middleware
app.Use(async (ctx, next) =>
{
    var tenant = ctx.Request.Headers["X-Tenant"].FirstOrDefault() ?? "default";
    ctx.RequestServices.GetRequiredService<TenantContext>().CurrentTenant = tenant;
    await next();
});

app.UseAuthentication();
app.UseUserSync(); // Automatically sync Identity users with custom User records
app.UseAuthorization();

app.MapRazorPages();
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

public class TenantContext { public string CurrentTenant { get; set; } = "default"; }

public class AIServiceOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
}