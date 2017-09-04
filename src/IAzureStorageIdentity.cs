using Microsoft.WindowsAzure.Storage.Table;


namespace Korzh.Identity.AzureStorage
{
    public interface IAzureStorageIdentity : ITableEntity {
        string Email { get; set; }

        string NormalizedEmail { get; set; }

        bool EmailConfirmed { get; set; }

        string RolesStr { get; set; }


        string PasswordHash { get; set; }

    }
}
