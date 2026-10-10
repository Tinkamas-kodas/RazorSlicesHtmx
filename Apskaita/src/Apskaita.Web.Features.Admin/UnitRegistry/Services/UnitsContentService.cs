using Apskaita.Web.Features.Admin.UnitRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.UnitRegistry.Services;

public sealed class UnitsContentService
{

    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public async Task<UnitListModel> CreateListModelAsync(ApskaitaDbContext db, UnitSearchModel search, UnitListQuery query, CancellationToken ct)
    {

        var items = db.Units.AsNoTracking()
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

    public Task<Unit?> FindByIdAsync(ApskaitaDbContext db, Guid id, CancellationToken ct) => db.Units.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task CreateAsync(ApskaitaDbContext db, UnitUpsertRequest request, CancellationToken ct)
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

        db.Units.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task ApplyAsync(ApskaitaDbContext db, Unit unit, UnitUpsertRequest request, CancellationToken ct)
    {
        unit.Code = request.Code ?? string.Empty;
        unit.Name = request.Name ?? string.Empty;
        unit.UpdatedAtUtc = DateTime.UtcNow;
        unit.UpdatedByUserId = Guid.Empty;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ApskaitaDbContext db, Unit unit, CancellationToken ct)
    {
        db.Units.Remove(unit);
        await db.SaveChangesAsync(ct);
    }
}

