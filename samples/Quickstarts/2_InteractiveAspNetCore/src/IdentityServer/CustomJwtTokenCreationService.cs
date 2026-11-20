using Duende.IdentityServer.Services;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication;

namespace IdentityServer
{
    /// <summary>
    /// Custom token creation service that uses "JWT" instead of "at+jwt" for access tokens
    /// This allows testing with older OSCAR code that doesn't support "at+jwt"
    /// </summary>
    public class CustomJwtTokenCreationService : DefaultTokenCreationService
    {
        public CustomJwtTokenCreationService(
            TimeProvider clock,
            IKeyMaterialService keys,
            IdentityServerOptions options,
            ILogger<DefaultTokenCreationService> logger)
            : base(clock, keys, options, logger)
        {
        }

        protected override async Task<Dictionary<string, object>> CreateHeaderElementsAsync(Token token)
        {
            // Get the default header elements (await, don't block a thread on .Result)
            var elements = await base.CreateHeaderElementsAsync(token);

            // Override the "typ" header to use "JWT" instead of "at+jwt"
            if (token.Type == "access_token")
            {
                elements["typ"] = "JWT";  // Force legacy JWT type
            }

            return elements;
        }
    }
}
