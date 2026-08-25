using Microsoft.Xrm.Sdk;
using RepairShop.Seeder.Data;
using RepairShop.Seeder.Infrastructure;
using RepairShop.Seeder.Models;
using RepairShop.Seeder.Schema;

namespace RepairShop.Seeder.Services;

internal sealed class DeviceSeeder(
    IOrganizationService service,
    string seedBatchAttribute,
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
            DateTime purchaseDate = DateTime.UtcNow.Date.AddMonths(-random.Next(1, 49));
            var device = new Entity(DataverseSchema.Device.Entity)
            {
                [DataverseSchema.Device.Name] = displayName,
                [DataverseSchema.Device.Manufacturer] = manufacturer,
                [DataverseSchema.Device.Model] = model,
                [DataverseSchema.Device.SerialNumber] = serialNumber,
                [DataverseSchema.Device.WarrantyExpiry] = purchaseDate.AddMonths(random.Next(12, 37)),
                [DataverseSchema.Device.PurchaseDate] = purchaseDate,
                [DataverseSchema.Device.Customer] = new EntityReference(
                    DataverseSchema.Contact.Entity,
                    contact.Id)
            };
            SeedTag.Apply(device, seedBatchAttribute, seedTag);

            Guid id = service.Create(device);
            devices.Add(new SeededDevice(id, contact.Id, displayName));
        }

        return devices;
    }
}
