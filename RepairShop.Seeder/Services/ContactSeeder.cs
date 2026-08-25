using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Data;
using RepairShop.Seeder.Infrastructure;
using RepairShop.Seeder.Models;
using RepairShop.Seeder.Schema;

namespace RepairShop.Seeder.Services;

internal sealed class ContactSeeder(
    IOrganizationService service,
    string seedBatchAttribute,
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
            var contact = new Entity(DataverseSchema.Contact.Entity)
            {
                [DataverseSchema.Contact.FirstName] = firstName,
                [DataverseSchema.Contact.LastName] = lastName,
                [DataverseSchema.Contact.Email] = $"{firstName}.{lastName}.{emailSuffix}.{index + 1}@example.test".ToLowerInvariant(),
                [DataverseSchema.Contact.Phone] = $"+1 (555) {random.Next(200, 999):000}-{random.Next(0, 10_000):0000}"
            };
            SeedTag.Apply(contact, seedBatchAttribute, seedTag);

            contacts.Add(new SeededContact(service.Create(contact)));
        }

        return contacts;
    }
}
