using Microsoft.PowerPlatform.Dataverse.Client;
using RepairShop.Seeder.Configuration;

namespace RepairShop.Seeder.Infrastructure;

internal static class DataverseConnectionFactory
{
    // Microsoft-owned public client used by Microsoft's Dataverse ServiceClient samples.
    // This is an application identifier, not a credential or secret.
    private const string DataversePublicClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

    public static ServiceClient Connect(DataverseOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            Console.WriteLine("Connecting to Dataverse with the configured connection string...");
            return Connect(options.ConnectionString);
        }

        Uri environmentUrl = ResolveEnvironmentUrl(options.Url);
        Console.WriteLine($"Opening Microsoft sign-in for {environmentUrl}...");

        string connectionString =
            $"AuthType=OAuth;Url={environmentUrl.AbsoluteUri};" +
            $"AppId={DataversePublicClientId};RedirectUri=http://localhost;" +
            "LoginPrompt=Auto;RequireNewInstance=true";

        return Connect(connectionString);
    }

    private static ServiceClient Connect(string connectionString)
    {
        var client = new ServiceClient(connectionString);
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

    private static Uri ResolveEnvironmentUrl(string configuredUrl)
    {
        string? value = configuredUrl;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (Console.IsInputRedirected)
            {
                throw new InvalidOperationException(
                    "Interactive authentication requires a Dataverse URL. Set Dataverse:Url " +
                    "in appsettings.json or run the seeder from an interactive terminal.");
            }

            Console.Write("Dataverse environment URL (for example, https://org.crm.dynamics.com): ");
            value = Console.ReadLine();
        }

        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "The Dataverse URL must be an absolute HTTPS environment URL, such as " +
                "https://org.crm.dynamics.com.");
        }

        return new Uri(uri.GetLeftPart(UriPartial.Authority));
    }
}
