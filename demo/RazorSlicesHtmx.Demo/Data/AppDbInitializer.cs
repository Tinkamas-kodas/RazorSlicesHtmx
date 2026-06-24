using RazorSlicesHtmx.Demo.Features.Items.Models;

namespace RazorSlicesHtmx.Demo.Data;

public static class AppDbInitializer
{
    public static void Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.EnsureCreated();

        if (db.Items.Any())
        {
            return;
        }

        db.Items.AddRange(
            new Item { Code = "ALPHA", Name = "Alpha item", IsEnabled = true },
            new Item { Code = "BRAVO", Name = "Bravo item", IsEnabled = false },
            new Item { Code = "CHARLIE", Name = "Charlie item", IsEnabled = true },
            new Item { Code = "DELTA", Name = "Delta item", IsEnabled = true },
            new Item { Code = "ECHO", Name = "Echo item", IsEnabled = false },
            new Item { Code = "FOXTROT", Name = "Foxtrot item", IsEnabled = true },
            new Item { Code = "GOLF", Name = "Golf item", IsEnabled = true });

        db.SaveChanges();
    }
}