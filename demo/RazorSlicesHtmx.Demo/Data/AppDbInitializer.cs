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
            Enumerable.Range(1, 100).Select(i => new Item
            {
                Code = $"ITEM-{i:D3}",
                Name = $"Item number {i}",
                IsEnabled = i % 3 != 0
            }));

        db.SaveChanges();
    }
}