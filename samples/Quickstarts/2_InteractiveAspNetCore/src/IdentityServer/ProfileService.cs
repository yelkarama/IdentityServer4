using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using System;
using System.Threading;
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

        public Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken cancellationToken)
        {
            // Get user from test user store
            var user = _users.FindBySubjectId(context.Subject.GetSubjectId());
            
            if (user != null)
            {
                // ALWAYS include accountNt claim (CRITICAL for OSCAR)
                var claims = user.Claims.ToList();

                // Add ALL user claims to both ID token and access token
                // This ensures accountNt is ALWAYS present, regardless of requested scopes
                context.IssuedClaims.AddRange(claims);
            }
            
            return Task.CompletedTask;
        }

        public Task IsActiveAsync(IsActiveContext context, CancellationToken cancellationToken)
        {
            var user = _users.FindBySubjectId(context.Subject.GetSubjectId());
            context.IsActive = (user != null);
            return Task.CompletedTask;
        }
    }
}
