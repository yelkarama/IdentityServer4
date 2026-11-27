using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Reflection;
using Serilog;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Storage;
using IdentityServer4.EntityFramework.Stores;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Application session cookie. The IdP is served over HTTPS only
            // (https://localhost:5001), so the cookie must never traverse HTTP.
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            });
            
            services.AddControllersWithViews();

            // Persist data protection keys (auth cookies/antiforgery) to a stable
            // location so they survive container recreation. In Docker, mount a
            // volume at DATAPROTECTION_KEYS_DIR. On Linux the key ring writes key
            // files with 0600 permissions, so the "no XML encryptor" startup
            // warning is informational (encryptors are DPAPI/X509, neither fits
            // a Linux container).
            var keysDir = Environment.GetEnvironmentVariable("DATAPROTECTION_KEYS_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".oscar-idp", "DataProtection-Keys");
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysDir));

            // Persisted grants (refresh tokens) in SQLite so they survive
            // restarts. Registered before AddIdentityServer so its in-memory
            // TryAddSingleton<IPersistedGrantStore> is skipped.
            var grantsDb = Environment.GetEnvironmentVariable("IS_GRANTS_DB_PATH")
                ?? Path.Combine(Directory.GetCurrentDirectory(), "idp.db");
            services.AddOperationalDbContext(o => o.ConfigureDbContext = b => b.UseSqlite($"Data Source={grantsDb}"));
            services.AddTransient<IPersistedGrantStore, PersistedGrantStore>();

            var builder = services.AddIdentityServer(options =>
            {
                options.EmitStaticAudienceClaim = true;
                
                // CRITICAL: Cookie settings for local development
                options.Authentication.CookieSameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                
                // Logging for debugging
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
            })
                .AddInMemoryIdentityResources(Config.IdentityResources)
                .AddInMemoryApiScopes(Config.ApiScopes)
                .AddInMemoryClients(Config.Clients)
                .AddTestUsers(TestUsers.Users)
                .AddProfileService<ProfileService>();  // CRITICAL: Include claims in access token

            // Override token creation to use "JWT" instead of "at+jwt" for legacy compatibility
            builder.Services.AddTransient<ITokenCreationService, CustomJwtTokenCreationService>();

            // For development only - use developer signing credential.
            // IS4 4.x persists it as a JWK file (default: tempkey.jwk in the
            // working dir). In Docker, point IS_SIGNING_KEY_PATH at a
            // volume-mounted path so issued tokens survive container recreation.
            var signingKeyPath = Environment.GetEnvironmentVariable("IS_SIGNING_KEY_PATH")
                ?? Path.Combine(Directory.GetCurrentDirectory(), "tempkey.jwk");
            builder.AddDeveloperSigningCredential(filename: signingKeyPath);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Create the SQLite schema on first run (no migrations for SQLite in the fork).
            using (var scope = app.ApplicationServices.CreateScope())
                scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.EnsureCreated();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();
            
            app.UseIdentityServer();
            app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
            
            // Log startup info
            Log.Information(
                "=========================================\n" +
                "IdentityServer4 for OSCAR EMR Testing\n" +
                "=========================================\n" +
                "\n" +
                "Configuration:\n" +
                "  - Client ID: EMR-client\n" +
                "  - Access Token Lifetime: 10 seconds\n" +
                "  - Refresh Token: OneTimeOnly (rotating)\n" +
                "  - Callback: /oauth2/callback\n" +
                "\n" +
                "Endpoints:\n" +
                "  - Discovery: /.well-known/openid-configuration\n" +
                "  - Authorize: /connect/authorize\n" +
                "  - Token: /connect/token\n" +
                "  - UserInfo: /connect/userinfo\n" +
                "  - End Session: /connect/endsession\n" +
                "\n" +
                "NOTE: Access tokens expire in 10 seconds to test race conditions!\n" +
                "=========================================");
            Log.Information("Build: {Version}", typeof(Startup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
        }
    }
}
