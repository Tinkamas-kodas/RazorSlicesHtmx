using Apskaita.Web.Features.Admin.UnitRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.UnitRegistry.Services;

internal sealed class UnitsContentService
{

    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public async Task<UnitListModel> CreateListModelAsync(IAdminStore store, UnitSearchModel search, UnitListQuery query, CancellationToken ct)
    {

        var items = store.Units.AsNoTracking()
            .Select(x => new UnitRowModel
            {
                Id = x.Id,
                Code = x.Code,
                IsActive = x.IsActive,
                Name = x.Name
            });

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            items = items.Where(x => x.Name.Contains(term) || x.Code.Contains(term));
        }

        var paged = await items.ToPagedListAsync(query, ct);
        return new UnitListModel(paged.Total, search, query, paged.Items);
    }

    public UnitUpsertRequest CreateCreateFormModel() => new() { IsActive = true, Errors = EmptyErrors };

    public UnitUpsertRequest CreateEditFormModel(Unit unit) => new()
    {
        Id = unit.Id,
        Code = unit.Code,
        Name = unit.Name,
        IsActive = unit.IsActive,
        Errors = EmptyErrors
    };

    public UnitDeleteDialogModel CreateDeleteDialogModel(Unit unit) => new(unit.Id, unit.Name);

    public void TrimRequest(UnitUpsertRequest request)
    {
        request.Code = request.Code?.Trim();
        request.Name = request.Name?.Trim();
    }

    public Task<Unit?> FindByIdAsync(IAdminStore store, Guid id, CancellationToken ct) => store.Units.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task CreateAsync(IAdminStore store, UnitUpsertRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var entity = new Unit
        {
            Id = Guid.NewGuid(),
            Code = request.Code ?? string.Empty,
            Name = request.Name ?? string.Empty,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = Guid.Empty,
            UpdatedByUserId = Guid.Empty
        };

        store.Units.Add(entity);
        await store.SaveChangesAsync(ct);
    }

    public async Task ApplyAsync(IAdminStore store, Unit unit, UnitUpsertRequest request, CancellationToken ct)
    {
        unit.Code = request.Code ?? string.Empty;
        unit.Name = request.Name ?? string.Empty;
        unit.UpdatedAtUtc = DateTime.UtcNow;
        unit.UpdatedByUserId = Guid.Empty;
        await store.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(IAdminStore store, Unit unit, CancellationToken ct)
    {
        store.Units.Remove(unit);
        await store.SaveChangesAsync(ct);
    }
}

