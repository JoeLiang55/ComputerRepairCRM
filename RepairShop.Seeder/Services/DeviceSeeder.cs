using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Configuration;
using RepairShop.Seeder.Data;
using RepairShop.Seeder.Models;

namespace RepairShop.Seeder.Services;

internal sealed class DeviceSeeder(
    IOrganizationService service,
    DeviceSchema schema,
    ContactSchema contactSchema,
    Random random)
{
    public IReadOnlyList<SeededDevice> Seed(
        IReadOnlyList<SeededContact> contacts,
        string seedTag)
    {
        var devices = new List<SeededDevice>(contacts.Count);

        foreach (SeededContact contact in contacts)
        {
            string manufacturer = random.Pick(RealisticData.Manufacturers);
            string model = random.Pick(RealisticData.Models[manufacturer]);
            string serialNumber = random.AlphaNumeric(12);
            string displayName = $"{manufacturer} {model} - {serialNumber[^4..]}";
            var device = new Entity(schema.Entity)
            {
                [schema.Name] = displayName,
                [schema.Manufacturer] = manufacturer,
                [schema.Model] = model,
                [schema.SerialNumber] = serialNumber,
                [schema.WarrantyExpiry] = DateTime.UtcNow.Date.AddMonths(random.Next(-6, 37)),
                [schema.ContactLookup] = new EntityReference(contactSchema.Entity, contact.Id),
                [schema.SeedBatch] = seedTag
            };

            Guid id = service.Create(device);
            devices.Add(new SeededDevice(id, contact.Id, displayName));
        }

        return devices;
    }
}
