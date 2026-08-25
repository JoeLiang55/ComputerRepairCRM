using Microsoft.PowerPlatform.Dataverse.Client;
using RepairShop.Seeder.CommandLine;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Infrastructure;
using RepairShop.Seeder.Models;
using RepairShop.Seeder.Services;

namespace RepairShop.Seeder;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            SeederCommand command = CommandLineParser.Parse(args);
            if (command.ShowHelp)
            {
                CommandLineParser.PrintUsage();
                return 0;
            }

            SeederOptions options = ConfigurationLoader.Load();
            using ServiceClient client = DataverseConnectionFactory.Connect(options.Dataverse);

            SchemaRequirementSet requirements = SchemaRequirements.ForCommand(command, options.Schema);
            Console.WriteLine("Validating configured tables, columns, and lookup targets...");
            new SchemaValidator(client).Validate(requirements);
            Console.WriteLine("Schema validation passed.");

            if (command.Clear)
            {
                return Clear(client, options.Schema);
            }

            var random = options.Defaults.RandomSeed.HasValue
                ? new Random(options.Defaults.RandomSeed.Value)
                : new Random();
            SeedRunResult result = new DemoDataSeeder(client, options, random).Seed(command);
            Console.WriteLine();
            Console.WriteLine($"Seed batch {result.BatchId:D} completed successfully.");
            return 0;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Argument error: {exception.Message}");
            Console.Error.WriteLine();
            CommandLineParser.PrintUsage();
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Seeder failed: {exception.Message}");
            return 1;
        }
    }

    private static int Clear(ServiceClient client, SchemaOptions schema)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Clear cancelled: interactive confirmation is required.");
            return 3;
        }

        Console.WriteLine();
        Console.WriteLine("WARNING: This will delete records tagged with 'RepairShop.Seeder:'.");
        Console.WriteLine("Records without that tag will not be selected.");
        Console.Write("Type CLEAR to continue: ");
        string? confirmation = Console.ReadLine();
        if (!string.Equals(confirmation, "CLEAR", StringComparison.Ordinal))
        {
            Console.WriteLine("Clear cancelled. No records were deleted.");
            return 0;
        }

        int deleted = new DemoDataCleaner(client, schema).Clear();
        Console.WriteLine($"Clear completed. Deleted {deleted} seeder-owned records.");
        return 0;
    }
}
