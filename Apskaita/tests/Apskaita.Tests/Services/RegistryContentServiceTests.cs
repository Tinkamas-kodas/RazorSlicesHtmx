using Apskaita.Entities;
using Apskaita.Web.Features.Admin.ClientsRegistry.Models;
using Apskaita.Web.Features.Admin.ClientsRegistry.Services;
using Apskaita.Web.Features.Admin.UnitRegistry.Models;
using Apskaita.Web.Features.Admin.UnitRegistry.Services;
using Apskaita.Web.Features.Admin.UserRegistry.Services;

namespace Apskaita.Tests.Services;

/// <summary>
/// P3: content-service behaviour against the EF In-Memory provider. Locks the recently-established
/// invariants: create always yields an active record, edit (ApplyAsync) never changes IsActive
/// (the toggle action owns it), the user toggle flips state, and list search filters.
/// </summary>
public sealed class RegistryContentServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ---- Clients ----

    [Fact]
    public async Task Clients_Create_ForcesActiveRegardlessOfRequest()
    {
        await using var db = TestDb.New();
        var service = new ClientsContentService();
        var request = new ClientUpsertRequest { Name = "Acme", IsActive = false };

        await service.CreateAsync(db, request, Ct);

        Assert.True(db.Clients.Single().IsActive);
    }

    [Fact]
    public async Task Clients_Apply_DoesNotChangeIsActive()
    {
        await using var db = TestDb.New();
        var service = new ClientsContentService();
        var client = new Client { Id = Guid.NewGuid(), Info = new CompanyInfo { Name = "Old" }, IsActive = false };
        db.Clients.Add(client);
        await db.SaveChangesAsync(Ct);

        var request = new ClientUpsertRequest { Id = client.Id, Name = "Renamed", IsActive = true };
        await service.ApplyAsync(db, client, request, Ct);

        Assert.False(client.IsActive, "ApplyAsync must not map IsActive from the request.");
        Assert.Equal("Renamed", client.Info.Name);
    }

    // ---- Units ----

    [Fact]
    public async Task Units_Create_ForcesActiveRegardlessOfRequest()
    {
        await using var db = TestDb.New();
        var service = new UnitsContentService();
        var request = new UnitUpsertRequest { Code = "h", Name = "hours", IsActive = false };

        await service.CreateAsync(db, request, Ct);

        Assert.True(db.Units.Single().IsActive);
    }

    [Fact]
    public async Task Units_Apply_DoesNotChangeIsActive()
    {
        await using var db = TestDb.New();
        var service = new UnitsContentService();
        var unit = new Unit { Id = Guid.NewGuid(), Code = "h", Name = "hours", IsActive = false };
        db.Units.Add(unit);
        await db.SaveChangesAsync(Ct);

        var request = new UnitUpsertRequest { Id = unit.Id, Code = "h", Name = "hours edited", IsActive = true };
        await service.ApplyAsync(db, unit, request, Ct);

        Assert.False(unit.IsActive, "ApplyAsync must not map IsActive from the request.");
        Assert.Equal("hours edited", unit.Name);
    }

    [Fact]
    public async Task Units_List_SearchFiltersByName()
    {
        await using var db = TestDb.New();
        var service = new UnitsContentService();
        db.Units.Add(new Unit { Id = Guid.NewGuid(), Code = "aa", Name = "Alpha", IsActive = true });
        db.Units.Add(new Unit { Id = Guid.NewGuid(), Code = "bb", Name = "Beta", IsActive = true });
        await db.SaveChangesAsync(Ct);

        var model = await service.CreateListModelAsync(db, new UnitSearchModel("Beta"), new UnitListQuery(), Ct);

        Assert.Equal(1, model.Total);
        Assert.Equal("Beta", model.Items.Single().Name);
    }

    // ---- Users ----

    [Fact]
    public async Task Users_ToggleActive_FlipsState()
    {
        await using var db = TestDb.New();
        var service = new UsersContentService();
        var user = new User { Id = Guid.NewGuid(), DisplayName = "Test", Email = "test@example.com", Provider = "entra", SubjectId = "oid", IsActive = true };
        db.Set<User>().Add(user);
        await db.SaveChangesAsync(Ct);

        var loaded = await service.FindByIdAsync(db, user.Id, Ct);
        await service.ToggleActiveAsync(db, loaded!, Ct);
        Assert.False(user.IsActive);

        loaded = await service.FindByIdAsync(db, user.Id, Ct);
        await service.ToggleActiveAsync(db, loaded!, Ct);
        Assert.True(user.IsActive);
    }
}
