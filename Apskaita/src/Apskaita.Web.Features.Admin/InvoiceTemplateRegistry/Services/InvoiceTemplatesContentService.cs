using Apskaita.Entities;
using Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Services;

public sealed class InvoiceTemplatesContentService
{
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public async Task<InvoiceTemplateListModel> CreateListModelAsync(ApskaitaDbContext db, InvoiceTemplateSearchModel search, InvoiceTemplateListQuery query, CancellationToken ct)
    {
        var items = db.InvoiceTemplates.AsNoTracking()
            .Select(x => new InvoiceTemplateRowModel
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                LanguageCode = x.LanguageCode,
                IsDefault = x.IsDefault,
                IsActive = x.IsActive
            });

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            items = items.Where(x => x.Name.Contains(term) || x.Code.Contains(term) || x.LanguageCode.Contains(term));
        }

        var paged = await items.ToPagedListAsync(query, ct);
        return new InvoiceTemplateListModel(paged.Total, search, query, paged.Items);
    }

    public InvoiceTemplateUpsertRequest CreateCreateFormModel() => new() { IsActive = true, Errors = EmptyErrors };

    public InvoiceTemplateUpsertRequest CreateEditFormModel(InvoiceTemplate row) => new()
    {
        Id = row.Id,
        Code = row.Code,
        Name = row.Name,
        LanguageCode = row.LanguageCode,
        Content = row.Content,
        IsDefault = row.IsDefault,
        IsActive = row.IsActive,
        Errors = EmptyErrors
    };

    public InvoiceTemplateDeleteDialogModel CreateDeleteDialogModel(InvoiceTemplate row) => new(row.Id, row.Name);

    public void TrimRequest(InvoiceTemplateUpsertRequest request)
    {
        request.Code = request.Code?.Trim();
        request.Name = request.Name?.Trim();
        request.LanguageCode = request.LanguageCode?.Trim().ToUpperInvariant();
        request.Content = request.Content?.Trim();
    }

    public Task<InvoiceTemplate?> FindByIdAsync(ApskaitaDbContext db, Guid id, CancellationToken ct) => db.InvoiceTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task CreateAsync(ApskaitaDbContext db, InvoiceTemplateUpsertRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var entity = new InvoiceTemplate
        {
            Id = Guid.NewGuid(),
            Code = request.Code ?? string.Empty,
            Name = request.Name ?? string.Empty,
            LanguageCode = request.LanguageCode ?? "LTU",
            Content = request.Content ?? string.Empty,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = Guid.Empty,
            UpdatedByUserId = Guid.Empty
        };

        if (entity.IsDefault)
        {
            await ClearDefaultAsync(db, ct);
        }

        db.InvoiceTemplates.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task ApplyAsync(ApskaitaDbContext db, InvoiceTemplate row, InvoiceTemplateUpsertRequest request, CancellationToken ct)
    {
        row.Code = request.Code ?? string.Empty;
        row.Name = request.Name ?? string.Empty;
        row.LanguageCode = request.LanguageCode ?? "LTU";
        row.Content = request.Content ?? string.Empty;
        row.IsDefault = request.IsDefault;
        row.IsActive = request.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedByUserId = Guid.Empty;

        if (row.IsDefault)
        {
            await ClearDefaultAsync(db, ct, row.Id);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ApskaitaDbContext db, InvoiceTemplate row, CancellationToken ct)
    {
        db.InvoiceTemplates.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    private static async Task ClearDefaultAsync(ApskaitaDbContext db, CancellationToken ct, Guid? exceptId = null)
    {
        var defaults = db.InvoiceTemplates.Where(x => x.IsDefault && (!exceptId.HasValue || x.Id != exceptId.Value));
        await defaults.ForEachAsync(x => x.IsDefault = false, ct);
    }
}

