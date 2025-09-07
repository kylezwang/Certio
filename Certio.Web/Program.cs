using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Certio.Web.Data;
using Certio.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

// db + identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<ApplicationDbContext>();

// mvc/razor/controllers + signalr
builder.Services.AddRazorPages();
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

app.UseRouting();

// tenant middleware
app.Use(async (ctx, next) =>
{
    var tenant = ctx.Request.Headers["X-Tenant"].FirstOrDefault() ?? "default";
    ctx.RequestServices.GetRequiredService<TenantContext>().CurrentTenant = tenant;
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
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
