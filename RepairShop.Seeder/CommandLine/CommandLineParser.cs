namespace RepairShop.Seeder.CommandLine;

internal static class CommandLineParser
{
    private const int MaximumRecordCount = 10_000;

    public static SeederCommand Parse(string[] args)
    {
        bool seedAll = false;
        bool clear = false;
        bool inventory = false;
        bool showHelp = false;
        int? contacts = null;
        int? cases = null;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index].ToLowerInvariant())
            {
                case "--seed":
                    seedAll = true;
                    break;
                case "--clear":
                    clear = true;
                    break;
                case "--inventory":
                    inventory = true;
                    break;
                case "--contacts":
                    contacts = ReadCount(args, ref index, "--contacts");
                    break;
                case "--cases":
                    cases = ReadCount(args, ref index, "--cases");
                    break;
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'. Use --help for usage.");
            }
        }

        int operationCount = (seedAll ? 1 : 0) + (clear ? 1 : 0) +
            (contacts.HasValue || cases.HasValue || inventory ? 1 : 0);
        if (!showHelp && operationCount == 0)
        {
            throw new ArgumentException("Specify --seed, --clear, --contacts, --cases, or --inventory.");
        }

        if (!showHelp && operationCount > 1)
        {
            throw new ArgumentException(
                "--seed and --clear must be used alone. Individual seed commands may be combined.");
        }

        return new SeederCommand(seedAll, clear, contacts, cases, inventory, showHelp);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("RepairShop.Seeder - Dataverse development data utility");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  RepairShop.Seeder --seed");
        Console.WriteLine("  RepairShop.Seeder --clear");
        Console.WriteLine("  RepairShop.Seeder --contacts <count>");
        Console.WriteLine("  RepairShop.Seeder --cases <count>");
        Console.WriteLine("  RepairShop.Seeder --inventory");
        Console.WriteLine();
        Console.WriteLine("Individual commands can be combined, for example: --cases 100 --inventory");
        Console.WriteLine("--cases creates a supporting Contact and Device pool for the requested Cases.");
    }

    private static int ReadCount(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || !int.TryParse(args[index], out int count))
        {
            throw new ArgumentException($"{option} requires a numeric count.");
        }

        if (count <= 0 || count > MaximumRecordCount)
        {
            throw new ArgumentOutOfRangeException(
                option,
                $"Count must be between 1 and {MaximumRecordCount:N0}.");
        }

        return count;
    }
}
