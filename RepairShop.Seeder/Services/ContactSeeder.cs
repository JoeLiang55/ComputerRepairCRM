using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Data;
using RepairShop.Seeder.Models;

namespace RepairShop.Seeder.Services;

internal sealed class ContactSeeder(
    IOrganizationService service,
    ContactSchema schema,
    Random random)
{
    public IReadOnlyList<SeededContact> Seed(int count, string seedTag)
    {
        var contacts = new List<SeededContact>(count);

        for (int index = 0; index < count; index++)
        {
            string firstName = random.Pick(RealisticData.FirstNames);
            string lastName = random.Pick(RealisticData.LastNames);
            string emailSuffix = seedTag[^8..].ToLowerInvariant();
            var contact = new Entity(schema.Entity)
            {
                [schema.FirstName] = firstName,
                [schema.LastName] = lastName,
                [schema.Email] = $"{firstName}.{lastName}.{emailSuffix}.{index + 1}@example.test".ToLowerInvariant(),
                [schema.Phone] = $"+1 (555) {random.Next(200, 999):000}-{random.Next(0, 10_000):0000}",
                [schema.SeedBatch] = seedTag
            };

            contacts.Add(new SeededContact(service.Create(contact)));
        }

        return contacts;
    }
}
