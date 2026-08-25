using System.Text.Json;
using System.Text.Json.Nodes;

namespace RepairShop.Seeder.Configuration;

internal static class ConfigurationLoader
{
    private const string ConnectionStringEnvironmentVariable = "DATAVERSE_CONNECTION_STRING";

    public static SeederOptions Load()
    {
        string basePath = FindConfigurationFile("appsettings.json")
            ?? throw new InvalidOperationException("appsettings.json was not found.");

        JsonNode root = Parse(basePath);
        string? localPath = FindConfigurationFile("appsettings.Local.json");
        string? localConnectionString = null;
        if (localPath is not null)
        {
            JsonNode localRoot = Parse(localPath);
            localConnectionString = localRoot["Dataverse"]?["ConnectionString"]?.GetValue<string>();
            Merge(root, localRoot);
        }

        var options = root.Deserialize<SeederOptions>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Configuration could not be loaded.");

        string? environmentConnectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        options.Dataverse.ConnectionString = !string.IsNullOrWhiteSpace(environmentConnectionString)
            ? environmentConnectionString
            : localConnectionString ?? string.Empty;

        ValidateDefaults(options.Defaults);
        return options;
    }

    private static JsonNode Parse(string path)
    {
        return JsonNode.Parse(
            File.ReadAllText(path),
            documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }) ?? throw new InvalidOperationException($"Configuration file '{path}' is empty.");
    }

    private static string? FindConfigurationFile(string fileName)
    {
        string currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        string outputPath = Path.Combine(AppContext.BaseDirectory, fileName);
        return File.Exists(outputPath) ? outputPath : null;
    }

    private static void Merge(JsonNode target, JsonNode source)
    {
        if (target is not JsonObject targetObject || source is not JsonObject sourceObject)
        {
            throw new InvalidOperationException("Configuration roots must be JSON objects.");
        }

        foreach ((string propertyName, JsonNode? sourceValue) in sourceObject)
        {
            if (sourceValue is JsonObject sourceChild &&
                targetObject[propertyName] is JsonObject targetChild)
            {
                Merge(targetChild, sourceChild);
            }
            else
            {
                targetObject[propertyName] = sourceValue?.DeepClone();
            }
        }
    }

    private static void ValidateDefaults(DefaultSeedOptions defaults)
    {
        if (defaults.ContactCount <= 0 || defaults.CaseCount <= 0 || defaults.InventoryCount <= 0)
        {
            throw new InvalidOperationException("All default seed counts must be greater than zero.");
        }
    }
}
