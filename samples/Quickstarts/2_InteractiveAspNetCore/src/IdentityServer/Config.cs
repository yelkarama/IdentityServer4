using IdentityServer4.Models;
using System.Collections.Generic;

namespace IdentityServer
{
    public static class Config
    {
        public static IEnumerable<IdentityResource> IdentityResources =>
            new IdentityResource[]
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                // Add email scope
                new IdentityResources.Email(),
                // Custom profile scope with OSCAR-specific claims
                new IdentityResource("oscar_profile", "OSCAR User Profile", new[] {
                    "accountNt",
                    "provider_no",
                    "role"
                })
            };

        public static IEnumerable<ApiScope> ApiScopes =>
            new ApiScope[]
            {
                // Define API scope that includes user claims
                new ApiScope("api1", "My API", new[] { 
                    "name", 
                    "email", 
                    "given_name", 
                    "family_name",
                    "accountNt",
                    "provider_no" 
                })
            };

        public static IEnumerable<Client> Clients =>
            new Client[]
            {
                // OSCAR EMR Client Configuration
                // Matches what OidcFilter.java expects
                new Client
                {
                    ClientId = "EMR-client",
                    ClientName = "OSCAR EMR",
                    
                    // Authorization Code Flow with PKCE
                    AllowedGrantTypes = GrantTypes.Code,
                    RequirePkce = true,
                    RequireClientSecret = false,  // Public client
                    
                    // Redirect URIs - OSCAR expects /oauth2/callback
                    RedirectUris = {
                        "http://localhost:8080/oscar/oauth2/callback",
                        "http://localhost:8080/Ontario/oauth2/callback",
                        "https://localhost:8443/oscar/oauth2/callback",
                        "https://localhost:8443/Ontario/oauth2/callback"
                    },
                    
                    // Post-logout redirect URIs
                    PostLogoutRedirectUris = {
                        "http://localhost:8080/oscar/index.jsp",
                        "http://localhost:8080/Ontario/index.jsp"
                    },
                    
                    // OSCAR requires these scopes
                    AllowedScopes = { 
                        "openid",           // Required
                        "profile",          // For name claims
                        "email",            // For email claim
                        "offline_access",   // For refresh tokens (REQUIRED!)
                        "oscar_profile",    // OSCAR-specific claims (accountNt, provider_no)
                        "api1"              // API scope with user claims
                    },
                    
                    // Enable refresh tokens (CRITICAL!)
                    AllowOfflineAccess = true,
                    
                    // Token settings - MIMIC PRODUCTION
                    // Set short for testing race conditions
                    AccessTokenLifetime = 10,  // 10 seconds (production: 120 = 2 minutes)
                    
                    // CRITICAL: Set access token type to JWT (not at+jwt reference token)
                    AccessTokenType = AccessTokenType.Jwt,
                    
                    // ROTATING REFRESH TOKENS (matches production IDP)
                    RefreshTokenUsage = TokenUsage.OneTimeOnly,  // CRITICAL: Single-use tokens
                    RefreshTokenExpiration = TokenExpiration.Sliding,
                    SlidingRefreshTokenLifetime = 28800,  // 8 hours
                    AbsoluteRefreshTokenLifetime = 86400,  // 24 hours
                    
                    // Update token on refresh (for testing rotation)
                    UpdateAccessTokenClaimsOnRefresh = true,
                    
                    // CRITICAL: Include user claims in access token (not just ID token)
                    // OSCAR validates access token and requires "name" claim (line 866)
                    AlwaysSendClientClaims = true,
                    AlwaysIncludeUserClaimsInIdToken = true,  // Include in ID token
                    
                    // Include standard profile claims in access token
                    // This ensures "name", "given_name", "family_name", "email" are present
                    IncludeJwtId = true,
                }
            };
    }
}
