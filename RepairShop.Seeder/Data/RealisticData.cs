namespace RepairShop.Seeder.Data;

internal static class RealisticData
{
    public static readonly string[] FirstNames =
    [
        "Avery", "Jordan", "Maya", "Ethan", "Sofia", "Noah", "Priya", "Mateo",
        "Chloe", "Marcus", "Leah", "Owen", "Nina", "Caleb", "Zoe", "Andre"
    ];

    public static readonly string[] LastNames =
    [
        "Bennett", "Chen", "Diaz", "Foster", "Garcia", "Hughes", "Kim", "Lewis",
        "Mitchell", "Patel", "Reed", "Robinson", "Singh", "Turner", "Walker", "Young"
    ];

    public static readonly string[] Manufacturers = ["Dell", "HP", "Lenovo", "ASUS", "Acer", "Apple"];

    public static readonly IReadOnlyDictionary<string, string[]> Models =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Dell"] = ["Inspiron 15 3530", "XPS 13 9340", "Latitude 5440"],
            ["HP"] = ["Pavilion 15", "EliteBook 840 G10", "Envy x360 14"],
            ["Lenovo"] = ["ThinkPad T14 Gen 4", "IdeaPad Slim 5", "Yoga 7i"],
            ["ASUS"] = ["VivoBook 15", "Zenbook 14", "ROG Zephyrus G14"],
            ["Acer"] = ["Aspire 5", "Swift Go 14", "Nitro V 15"],
            ["Apple"] = ["MacBook Air M2", "MacBook Air M3", "MacBook Pro 14"]
        };

    public static readonly InventoryTemplate[] Inventory =
    [
        new("SSD", 34.99m, 129.99m),
        new("RAM", 19.99m, 89.99m),
        new("Battery", 39.99m, 129.99m),
        new("Keyboard", 24.99m, 99.99m),
        new("LCD", 69.99m, 249.99m),
        new("Motherboard", 119.99m, 449.99m),
        new("Charging Port", 8.99m, 49.99m),
        new("Cooling Fan", 14.99m, 64.99m)
    ];

    public static T Pick<T>(this Random random, IReadOnlyList<T> values) =>
        values[random.Next(values.Count)];

    public static decimal Money(this Random random, decimal minimum, decimal maximum) =>
        decimal.Round(minimum + ((decimal)random.NextDouble() * (maximum - minimum)), 2);

    public static string AlphaNumeric(this Random random, int length)
    {
        const string characters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, length)
            .Select(_ => characters[random.Next(characters.Length)])
            .ToArray());
    }
}

internal sealed record InventoryTemplate(string Name, decimal MinimumCost, decimal MaximumCost);
