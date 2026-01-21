using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Test;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IdentityServer
{
    public class ProfileService : IProfileService
    {
        private readonly TestUserStore _users;

        public ProfileService(TestUserStore users)
        {
            _users = users;
        }

        public Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            // Get user from test user store
            var user = _users.FindBySubjectId(context.Subject.GetSubjectId());
            
            if (user != null)
            {
                // ALWAYS include accountNt claim (CRITICAL for OSCAR)
                var claims = user.Claims.ToList();
                
                // DEBUG: Log what we're sending
                Console.WriteLine($"[ProfileService] Adding claims to {context.Caller}:");
                foreach (var claim in claims)
                {
                    Console.WriteLine($"  - {claim.Type}: {claim.Value}");
                }
                
                // Add ALL user claims to both ID token and access token
                // This ensures accountNt is ALWAYS present, regardless of requested scopes
                context.IssuedClaims.AddRange(claims);
            }
            
            return Task.CompletedTask;
        }

        public Task IsActiveAsync(IsActiveContext context)
        {
            var user = _users.FindBySubjectId(context.Subject.GetSubjectId());
            context.IsActive = (user != null);
            return Task.CompletedTask;
        }
    }
}
