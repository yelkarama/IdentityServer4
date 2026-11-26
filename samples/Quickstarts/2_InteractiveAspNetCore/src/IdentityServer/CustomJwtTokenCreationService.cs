using IdentityServer4.Services;
using IdentityServer4.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using IdentityServer4.Configuration;
using Microsoft.AspNetCore.Authentication;

namespace IdentityServer
{
    /// <summary>
    /// Custom token creation service that uses "JWT" instead of "at+jwt" for access tokens
    /// This allows testing with older OSCAR code that doesn't support "at+jwt"
    /// </summary>
    public class CustomJwtTokenCreationService : DefaultTokenCreationService
    {
        // ISystemClock is the ASP.NET Core type (Microsoft.AspNetCore.Authentication),
        // obsolete in favour of TimeProvider — but IS4's DefaultTokenCreationService
        // base constructor still requires it, so the parameter is forced.
#pragma warning disable CS0618
        public CustomJwtTokenCreationService(
            ISystemClock clock,
            IKeyMaterialService keys,
            IdentityServerOptions options,
            ILogger<DefaultTokenCreationService> logger)
            : base(clock, keys, options, logger)
        {
        }
#pragma warning restore CS0618

        protected override Task<JwtHeader> CreateHeaderAsync(Token token)
        {
            // Get the default header
            var header = base.CreateHeaderAsync(token).Result;

            // Override the "typ" header to use "JWT" instead of "at+jwt"
            if (token.Type == "access_token")
            {
                header["typ"] = "JWT";  // Force legacy JWT type
            }

            return Task.FromResult(header);
        }
    }
}
