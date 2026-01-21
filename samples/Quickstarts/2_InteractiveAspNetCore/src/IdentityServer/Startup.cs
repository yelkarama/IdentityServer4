using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using IdentityServer4.Services;

namespace IdentityServer
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Configure cookies for local development
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.None;
            });
            
            services.AddControllersWithViews();

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

            // For development only - use developer signing credential
            builder.AddDeveloperSigningCredential();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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
            Console.WriteLine("=========================================");
            Console.WriteLine("IdentityServer4 for OSCAR EMR Testing");
            Console.WriteLine("=========================================");
            Console.WriteLine("");
            Console.WriteLine("Configuration:");
            Console.WriteLine("  - Client ID: EMR-client");
            Console.WriteLine("  - Access Token Lifetime: 10 seconds");
            Console.WriteLine("  - Refresh Token: OneTimeOnly (rotating)");
            Console.WriteLine("  - Callback: /oauth2/callback");
            Console.WriteLine("");
            Console.WriteLine("Test Users:");
            Console.WriteLine("  - oscardoc / mac2002 (accountNt: oscardoc) ⭐ PRIMARY USER");
            Console.WriteLine("  - testdoc1 / password (accountNt: testdoc1)");
            Console.WriteLine("  - testdoc2 / password (accountNt: testdoc2)");
            Console.WriteLine("  - admin / password (accountNt: admin)");
            Console.WriteLine("  - azureuser / password (accountNt: azureuser)");
            Console.WriteLine("");
            Console.WriteLine("Endpoints:");
            Console.WriteLine("  - Discovery: /.well-known/openid-configuration");
            Console.WriteLine("  - Authorize: /connect/authorize");
            Console.WriteLine("  - Token: /connect/token");
            Console.WriteLine("  - UserInfo: /connect/userinfo");
            Console.WriteLine("  - End Session: /connect/endsession");
            Console.WriteLine("");
            Console.WriteLine("NOTE: Access tokens expire in 10 seconds to test race conditions!");
            Console.WriteLine("=========================================");
        }
    }
}
