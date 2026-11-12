using WalkieTalkie.Components;
using WalkieTalkie.Data;
using WalkieTalkie.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace WalkieTalkie
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = 1024 * 1024;
            });

            builder.Services.AddHttpContextAccessor();

            // Get connection string
            var cs = builder.Configuration.GetConnectionString("AuthDb") ?? "Data Source=auth.db";

            // SQLite relative paths are relative to the *process working directory*, which can vary
            // depending on how the app is launched. Resolve to the content root for stability.
            const string dataSourcePrefix = "Data Source=";
            if (cs.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var src = cs.Substring(dataSourcePrefix.Length).Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(src)
                    && !Path.IsPathRooted(src)
                    && !src.Contains(";")
                    && !src.Contains("://", StringComparison.OrdinalIgnoreCase))
                {
                    // For Azure App Service, use the temporary directory for better reliability
                    var basePath = builder.Environment.IsProduction() 
                        ? Path.GetTempPath() 
                        : builder.Environment.ContentRootPath;
                    
                    var abs = Path.Combine(basePath, src);
                    cs = dataSourcePrefix + abs;
                    
                    // Log the database path for debugging
                    Console.WriteLine($"Database path: {abs} (Environment: {builder.Environment.EnvironmentName})");
                }
            }

            // Register DbContextFactory for both singleton and scoped services
            // Use pooled factory which handles both scenarios correctly
            builder.Services.AddPooledDbContextFactory<AppDbContext>(
                options => options.UseSqlite(cs),
                poolSize: 128);
            
            // Register scoped DbContext for services that need direct context injection
            // This uses the factory internally
            builder.Services.AddScoped(sp => 
                sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

            builder.Services.AddServerSideBlazor()
        .AddHubOptions(options =>
        {
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.EnableDetailedErrors = true;
        });

            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<DbAuthenticationStateProvider>();
            builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<DbAuthenticationStateProvider>());

            builder.Services.AddAuthorization();

            // Register the chat and voice lobby services so all components share the same state.
            builder.Services.AddSingleton<LobbyChatService>();
            builder.Services.AddSingleton<VoiceLobbyService>();
            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddSingleton<UsableMsVoiceService>();
            builder.Services.AddSingleton<AppLogService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DbSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
                DbSeeder.ValidateSchemaHealthAsync(db).GetAwaiter().GetResult();
            }

            using (var scope = app.Services.CreateScope())
            {
                var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
                var cleared = auth.ClearAllIsLoggedInAsync().GetAwaiter().GetResult();
                Console.WriteLine($"Startup login reset: cleared {cleared} logged-in user flag(s)");
            }

            using (var scope = app.Services.CreateScope())
            {
                var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
                settings.LoadAsync().GetAwaiter().GetResult();
            }

            using (var scope = app.Services.CreateScope())
            {
                var usableMsVoices = scope.ServiceProvider.GetRequiredService<UsableMsVoiceService>();
                usableMsVoices.LoadAsync().GetAwaiter().GetResult();
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            //app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AllowAnonymous();

            app.MapGet("/api/admin/download-database", async (HttpContext ctx, AuthService auth, AppDbContext db, string? contextId) =>
            {
                var user = await auth.GetUserByContextIdAsync(contextId ?? string.Empty, touch: true);
                var isAdmin = user is not null && (user.Roles ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));

                Console.WriteLine($"Download request received. ContextId present: {!string.IsNullOrWhiteSpace(contextId)}, User: {user?.Name}, IsAdmin: {isAdmin}");

                if (!isAdmin)
                {
                    Console.WriteLine("Download request rejected: invalid DB auth context or non-admin user");
                    return Results.Unauthorized();
                }

                try
                {
                    var connectionString = db.Database.GetDbConnection().ConnectionString;
                    var dbPath = ResolveSqliteDataSourcePath(connectionString);
                    Console.WriteLine($"Database path from connection string: {dbPath ?? "<unresolved>"}");
                    Console.WriteLine($"Connection string: {connectionString}");
                    
                    if (string.IsNullOrWhiteSpace(dbPath) || !System.IO.File.Exists(dbPath))
                    {
                        Console.WriteLine("Database file not found");
                        return Results.NotFound("Database file not found at resolved path");
                    }

                    // Create a temporary copy to avoid file locking issues
                    var tempPath = Path.Combine(Path.GetTempPath(), $"auth_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");
                    Console.WriteLine($"Creating temporary copy: {tempPath}");
                    
                    try
                    {
                        System.IO.File.Copy(dbPath, tempPath, true);
                        var fileBytes = System.IO.File.ReadAllBytes(tempPath);
                        var fileName = $"auth_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
                        Console.WriteLine($"Returning file: {fileName}, size: {fileBytes.Length} bytes");
                        
                        return Results.File(fileBytes, "application/octet-stream", fileName);
                    }
                    finally
                    {
                        // Clean up temporary file
                        if (System.IO.File.Exists(tempPath))
                        {
                            try
                            {
                                System.IO.File.Delete(tempPath);
                                Console.WriteLine($"Cleaned up temporary file: {tempPath}");
                            }
                            catch (Exception cleanupEx)
                            {
                                Console.WriteLine($"Failed to cleanup temporary file: {cleanupEx}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database download error: {ex}");
                    return Results.Problem($"Error downloading database: {ex.Message}");
                }

                static string? ResolveSqliteDataSourcePath(string? connectionString)
                {
                    if (string.IsNullOrWhiteSpace(connectionString)) return null;

                    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var dataSourcePart = parts.FirstOrDefault(p =>
                        p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));

                    if (string.IsNullOrWhiteSpace(dataSourcePart)) return null;

                    var value = dataSourcePart.Substring("Data Source=".Length).Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(value)) return null;

                    return value;
                }
            });

            // Legacy debug/test endpoints removed.

            app.MapGet("/clear-cache", async (HttpContext ctx, AuthService auth) =>
            {
                await auth.ClearAllIsLoggedInAsync();

                // Clear all cookies including any persistent auth cookies
                foreach (var cookie in ctx.Request.Cookies.Keys.ToList())
                {
                    ctx.Response.Cookies.Delete(cookie, new CookieOptions 
                    { 
                        Path = "/", 
                        Domain = null, 
                        Expires = DateTime.UtcNow.AddDays(-1)
                    });
                }
                
                // Clear response headers for cache
                ctx.Response.Headers.Clear();
                
                // Set headers to prevent caching
                ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                ctx.Response.Headers["Pragma"] = "no-cache";
                ctx.Response.Headers["Expires"] = "0";
                ctx.Response.Headers["Clear-Site-Data"] = "\"cache\", \"cookies\", \"storage\", \"executionContexts\"";
                
                return Results.Redirect("/?from-clear-cache=true", permanent: false);
            });

            app.MapGet("/hard-reset", async (HttpContext ctx, AuthService auth) =>
            {
                await auth.ClearAllIsLoggedInAsync();

                // Clear all cookies including any persistent auth cookies
                foreach (var cookie in ctx.Request.Cookies.Keys.ToList())
                {
                    ctx.Response.Cookies.Delete(cookie, new CookieOptions 
                    { 
                        Path = "/", 
                        Domain = null, 
                        Expires = DateTime.UtcNow.AddDays(-1)
                    });
                }
                
                // Clear response headers for cache
                ctx.Response.Headers.Clear();
                
                // Set headers to prevent caching
                ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                ctx.Response.Headers["Pragma"] = "no-cache";
                ctx.Response.Headers["Expires"] = "0";
                ctx.Response.Headers["Clear-Site-Data"] = "\"cache\", \"cookies\", \"storage\", \"executionContexts\"";
                
                return Results.Redirect("/", permanent: false);
            });

            app.Run();
        }
    }
}
