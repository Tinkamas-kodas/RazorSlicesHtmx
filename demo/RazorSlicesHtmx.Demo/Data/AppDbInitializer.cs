using RazorSlicesHtmx.Demo.Features.Items.Models;

namespace RazorSlicesHtmx.Demo.Data;

public static class AppDbInitializer
{
    public static void Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Items.Any())
        {
            return;
        }

        db.Items.AddRange(
            new Item { Code = "WIDGET-001", Name = "Widget Alpha", IsEnabled = true },
            new Item { Code = "WIDGET-002", Name = "Widget Beta", IsEnabled = true },
            new Item { Code = "GADGET-003", Name = "Gadget Gamma", IsEnabled = false },
            new Item { Code = "DOOHICKEY-004", Name = "Doohickey Delta", IsEnabled = true },
            new Item { Code = "THING-005", Name = "Thingamajig Epsilon", IsEnabled = true },
            new Item { Code = "CONTRAPTION-006", Name = "Contraption Zeta", IsEnabled = false },
            new Item { Code = "APPARATUS-007", Name = "Apparatus Eta", IsEnabled = true },
            new Item { Code = "MECHANISM-008", Name = "Mechanism Theta", IsEnabled = true },
            new Item { Code = "DEVICE-009", Name = "Device Iota", IsEnabled = false },
            new Item { Code = "IMPLEMENT-010", Name = "Implement Kappa", IsEnabled = true },
            new Item { Code = "TOOL-011", Name = "Tool Lambda", IsEnabled = true },
            new Item { Code = "INSTRUMENT-012", Name = "Instrument Mu", IsEnabled = true }
        );

        db.SaveChanges();
    }
}