using IdentityModel;
using IdentityServer4.Test;
using System.Collections.Generic;
using System.Security.Claims;

namespace IdentityServer
{
    public class TestUsers
    {
        public static List<TestUser> Users =>
            new List<TestUser>
            {
                // CUSTOM USER: oscardoc (as requested)
                new TestUser
                {
                    SubjectId = "999",
                    Username = "oscardoc",
                    Password = "mac2002",
                    Claims = new List<Claim>
                    {
                        // CRITICAL CLAIMS that OSCAR expects
                        new Claim("accountNt", "oscardoc"),  // REQUIRED: OidcFilter.java line 965
                        
                        // Standard OIDC claims
                        new Claim(JwtClaimTypes.Name, "Oscar Doctor"),
                        new Claim(JwtClaimTypes.GivenName, "Oscar"),
                        new Claim(JwtClaimTypes.FamilyName, "Doctor"),
                        new Claim(JwtClaimTypes.Email, "oscardoc@example.com"),
                        
                        // OSCAR-specific claims
                        new Claim("given_name", "Oscar"),
                        new Claim("family_name", "Doctor"),
                        new Claim("email", "oscardoc@example.com"),
                        
                        // Additional claims
                        new Claim("provider_no", "999998"),  // UPDATED
                        new Claim("role", "doctor"),
                    }
                },
                
                // Test Doctor 1 - Mimics CSC Active Directory user
                new TestUser
                {
                    SubjectId = "1",
                    Username = "testdoc1",
                    Password = "password",
                    Claims = new List<Claim>
                    {
                        // CRITICAL CLAIMS that OSCAR expects
                        new Claim("accountNt", "testdoc1"),  // REQUIRED: OidcFilter.java line 965
                        
                        // Standard OIDC claims
                        new Claim(JwtClaimTypes.Name, "Dr. Test Doctor"),
                        new Claim(JwtClaimTypes.GivenName, "Test"),
                        new Claim(JwtClaimTypes.FamilyName, "Doctor"),
                        new Claim(JwtClaimTypes.Email, "testdoc1@example.com"),
                        
                        // OSCAR-specific claims (optional but useful)
                        new Claim("given_name", "Test"),      // OidcFilter.java line 969
                        new Claim("family_name", "Doctor"),   // OidcFilter.java line 970
                        new Claim("email", "testdoc1@example.com"),  // OidcFilter.java line 968
                        
                        // Additional claims for testing
                        new Claim("provider_no", "999998"),
                        new Claim("role", "doctor"),
                    }
                },
                
                // Test Doctor 2 - Another test user
                new TestUser
                {
                    SubjectId = "2",
                    Username = "testdoc2",
                    Password = "password",
                    Claims = new List<Claim>
                    {
                        // CRITICAL: accountNt must be present
                        new Claim("accountNt", "testdoc2"),
                        
                        new Claim(JwtClaimTypes.Name, "Dr. Jane Smith"),
                        new Claim(JwtClaimTypes.GivenName, "Jane"),
                        new Claim(JwtClaimTypes.FamilyName, "Smith"),
                        new Claim(JwtClaimTypes.Email, "jane.smith@example.com"),
                        
                        new Claim("given_name", "Jane"),
                        new Claim("family_name", "Smith"),
                        new Claim("email", "jane.smith@example.com"),
                        
                        new Claim("provider_no", "999997"),
                        new Claim("role", "doctor"),
                    }
                },
                
                // Test Admin - For testing admin features
                new TestUser
                {
                    SubjectId = "3",
                    Username = "admin",
                    Password = "password",
                    Claims = new List<Claim>
                    {
                        new Claim("accountNt", "admin"),
                        
                        new Claim(JwtClaimTypes.Name, "Admin User"),
                        new Claim(JwtClaimTypes.GivenName, "Admin"),
                        new Claim(JwtClaimTypes.FamilyName, "User"),
                        new Claim(JwtClaimTypes.Email, "admin@example.com"),
                        
                        new Claim("given_name", "Admin"),
                        new Claim("family_name", "User"),
                        new Claim("email", "admin@example.com"),
                        
                        new Claim("provider_no", "999999"),
                        new Claim("role", "admin"),
                    }
                },
                
                // Azure AD style user (for testing Azure IDP claims fallback)
                // OidcFilter.java lines 972-981 handle Azure-specific claim names
                new TestUser
                {
                    SubjectId = "4",
                    Username = "azureuser",
                    Password = "password",
                    Claims = new List<Claim>
                    {
                        new Claim("accountNt", "azureuser"),
                        
                        // Azure-style claims (with full URIs)
                        new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", "azureuser@example.com"),
                        new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname", "Azure"),
                        new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname", "User"),
                        
                        new Claim(JwtClaimTypes.Name, "Azure User"),
                        new Claim("provider_no", "999996"),
                    }
                }
            };
    }
}
