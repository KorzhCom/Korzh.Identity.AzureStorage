using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

using Korzh.WindowsAzure.Storage;

namespace Korzh.Identity.AzureStorage
{

    public class AzureUserStore<TUser, TAzureStorageContext> : IUserRoleStore<TUser>, IUserPasswordStore<TUser>, IQueryableUserStore<TUser>, IUserEmailStore<TUser> 
            where TUser: class, IAzureStorageIdentity, new()
            where TAzureStorageContext : AzureStorageContext
    {
        private TableStorageService<TUser> userTable;
        private readonly ILookupNormalizer _normalizer;

        public IQueryable<TUser> Users => GetAllUsersAsync().Result.AsQueryable();

        private string _partitionKey = "Users";


        public AzureUserStore(TAzureStorageContext context, ILookupNormalizer normalizer) {
            this.userTable = new TableStorageService<TUser>(context, "Users");
            this._normalizer = normalizer;
        }

        public Task<IEnumerable<TUser>> GetAllUsersAsync() {
            return userTable.GetEntitiesByFilterAsync();
        }


        //------------ IUserRoleStore -------------
        public async Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken) {
            return await Task.FromResult(user.RowKey);
        }

        public async Task<string> GetUserNameAsync(TUser user, CancellationToken cancellationToken) {
            return await Task.FromResult(user.Email);
        }

        public Task<TUser> FindByIdAsync(string userId, CancellationToken cancellationToken = default(CancellationToken)) {
            return userTable.GetEntityByKeysAsync(_partitionKey, userId);
        }

        public async Task<TUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) {
            var users = await userTable.ListEntitiesByFilterAsync(new Dictionary<string, object> {
                {"PartitionKey", _partitionKey },
                { nameof(IAzureStorageIdentity.NormalizedEmail), normalizedUserName }
            });

            return users.FirstOrDefault();
        }

        public async Task<string> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken) {
            return await Task.FromResult(user.NormalizedEmail);
        }

        private void NormalizeUserBeforeInsertOrUpdate(TUser user) {
            if (user.RowKey == null) {
                user.RowKey = Guid.NewGuid().ToString();
            }

            user.PartitionKey = _partitionKey;
        }

        public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                NormalizeUserBeforeInsertOrUpdate(user);
                await userTable.InsertOrMergeEntityAsync(user);
                return IdentityResult.Success;
            }
            catch (Exception ex) {
                return IdentityResult.Failed(new IdentityError {
                    Code = ex.GetType().Name,
                    Description = ex.Message
                });
            }
        }

        public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                await userTable.DeleteEntityAsync(user);
                return IdentityResult.Success;
            }
            catch (Exception ex) {
                return IdentityResult.Failed( new IdentityError {
                        Code = ex.GetType().Name,
                        Description = ex.Message
                    });
            }
        }

        public async Task SetNormalizedUserNameAsync(TUser user, string normalizedName, CancellationToken cancellationToken) {
            user.NormalizedEmail = normalizedName;
            await userTable.InsertOrMergeEntityAsync(user);
        }

        public async Task SetUserNameAsync(TUser user, string userName, CancellationToken cancellationToken) {
            user.Email = userName;
            await userTable.InsertOrMergeEntityAsync(user);
        }

        public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken) {
            try {
                NormalizeUserBeforeInsertOrUpdate(user);
                await userTable.InsertOrMergeEntityAsync(user);
                return IdentityResult.Success;
            }
            catch (Exception ex) {
                return IdentityResult.Failed(new IdentityError { Code = ex.GetType().Name, Description = ex.Message });
            }
        }


        public async Task AddToRoleAsync(TUser user, string roleName, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(roleName))
                throw new ArgumentException("Empty rolename", nameof(roleName));

            var roles = user.RolesStr != null ? user.RolesStr.Split(',').ToHashSet() : new HashSet<string>();
            roles.Add(roleName);
            user.RolesStr = string.Join(",", roles);

            NormalizeUserBeforeInsertOrUpdate(user);
            await userTable.InsertOrMergeEntityAsync(user);
        }

        public Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken) {
            var roles = user.RolesStr != null ? user.RolesStr.Split(',').ToList() : new List<string>();

            return Task.FromResult<IList<string>>(roles);
        }


        public async Task<IList<TUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken) {
            return (await userTable.GetEntitiesByFilterAsync(""))
                .Where(u => u.PartitionKey == _partitionKey && u.RolesStr.IndexOf(roleName) >= 0)
                .ToList();
        }

        public Task<bool> IsInRoleAsync(TUser user, string roleName, CancellationToken cancellationToken) {
            return Task.FromResult(!string.IsNullOrEmpty(user.RolesStr) && user.RolesStr.Contains(roleName));
        }

        public async Task RemoveFromRoleAsync(TUser user, string roleName, CancellationToken cancellationToken) {
            if (!string.IsNullOrEmpty(roleName)) {
                var roles = user.RolesStr != null ? user.RolesStr.Split(',').ToHashSet() : new HashSet<string>();
                roles.Remove(roleName);
                user.RolesStr = string.Join(",", roles);

                NormalizeUserBeforeInsertOrUpdate(user);
                await userTable.InsertOrMergeEntityAsync(user);
            }
        }

        public void Dispose() {
        }


        // --------------- IUserPasswordStore -------------
        public async Task SetPasswordHashAsync(TUser user, string passwordHash, CancellationToken cancellationToken) {
            user.PasswordHash = passwordHash;

            NormalizeUserBeforeInsertOrUpdate(user);
            await userTable.InsertOrMergeEntityAsync(user);
        }

        public Task<string> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken) {
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken) {
            return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
        }

        //--------------- IUserEmailStore -------------
        public async Task<TUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default(CancellationToken)) {
            var users = await userTable.ListEntitiesByFilterAsync(new Dictionary<string, object>() {
                {"PartitionKey", _partitionKey },
                {nameof(IAzureStorageIdentity.NormalizedEmail), normalizedEmail}
            });

            return users.FirstOrDefault();
        }

        public async Task SetEmailAsync(TUser user, string email, CancellationToken cancellationToken) {
            user.Email = email;
            NormalizeUserBeforeInsertOrUpdate(user);
            await userTable.InsertOrMergeEntityAsync(user);
        }

        public Task<string> GetEmailAsync(TUser user, CancellationToken cancellationToken) {
            return Task.FromResult(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken) {
            return Task.FromResult(user.EmailConfirmed);
        }

        public async Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken) {
            user.EmailConfirmed = confirmed;
            NormalizeUserBeforeInsertOrUpdate(user);
            await userTable.InsertOrMergeEntityAsync(user);
        }


        public Task<string> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken) {
            return Task.FromResult(user.NormalizedEmail);
        }

        public async Task SetNormalizedEmailAsync(TUser user, string normalizedEmail, CancellationToken cancellationToken) {
            user.NormalizedEmail = normalizedEmail;

            NormalizeUserBeforeInsertOrUpdate(user);
            await userTable.InsertOrMergeEntityAsync(user);
        }
    }


    public static class EnumarableExtensions {
        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source) => source.ToHashSet(comparer: null);

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source, IEqualityComparer<TSource> comparer) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            // Don't pre-allocate based on knowledge of size, as potentially many elements will be dropped.
            return new HashSet<TSource>(source, comparer);
        }
    }
   
}