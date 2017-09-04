using System;
using System.Security.Claims;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;

using Korzh.WindowsAzure.Storage;
using Korzh.Identity;
using Korzh.Identity.AzureStorage;


namespace Microsoft.Extensions.DependencyInjection
{ 
    public static class AzureIdentityExtensions
    {
        public static IdentityBuilder AddAzureStores(this IdentityBuilder builder) 
        {
            var userType = builder.UserType;
            var userStoreType = typeof(AzureUserStore<,>).MakeGenericType(userType, typeof(DefaultAzureStorageContext));
            builder.Services.TryAddScoped(typeof(IUserStore<>).MakeGenericType(userType), userStoreType);
            builder.Services.AddScoped<IRoleStore<string>, AzureRoleStore>();
            builder.Services.AddScoped<ILookupNormalizer, LowerInvariantLookupNormalizer>();
            return builder;
        }

    }

}
