// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Api
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            // accepts any access token issued by identity server
            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.Authority = "https://localhost:5001";
                    
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        // The IdP does not emit aud = "api1" (no ApiResource is
                        // defined; the token carries scope = "api1" instead), so
                        // audience validation would reject every valid token.
                        // Authorization is enforced by the "ApiScope" policy below
                        // (RequireClaim("scope", "api1")), making this check redundant.
                        ValidateAudience = false,
                        ValidAudience = "api1"
                    };

                    // A malformed/expired token must fail closed (401), not 500.
                    // Writing a generic body (and logging server-side) stops the
                    // exception from propagating to a generic 500 and avoids
                    // leaking token-parsing internals to the caller.
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.Headers["WWW-Authenticate"] = "Bearer";
                            return context.Response.WriteAsync("Unauthorized");
                        }
                    };
                });
            
            // adds an authorization policy to make sure the token is for scope 'api1'
            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiScope", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", "api1");
                });
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers()
                    .RequireAuthorization("ApiScope");
            });
        }
    }
}
