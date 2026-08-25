using Microsoft.PowerPlatform.Dataverse.Client;
using RepairShop.Seeder.Configuration;

namespace RepairShop.Seeder.Infrastructure;

internal static class DataverseConnectionFactory
{
    public static ServiceClient Connect(DataverseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "No Dataverse connection string was supplied. Set DATAVERSE_CONNECTION_STRING " +
                "or Dataverse:ConnectionString in appsettings.Local.json.");
        }

        var client = new ServiceClient(options.ConnectionString);
        if (!client.IsReady)
        {
            string detail = string.IsNullOrWhiteSpace(client.LastError)
                ? "No additional error detail was returned."
                : client.LastError;
            client.Dispose();
            throw new InvalidOperationException($"Dataverse connection failed: {detail}");
        }

        return client;
    }
}
