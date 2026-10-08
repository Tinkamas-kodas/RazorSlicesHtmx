using Apskaita.Web.Features.Admin.SystemConfig.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.SystemConfig.Services;

public sealed class SystemSettingsContentService
{
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public async Task<SystemSettingsUpsertRequest> CreateModelAsync(ApskaitaDbContext db, CancellationToken ct)
    {
        var row = await db.SystemSettings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return new SystemSettingsUpsertRequest
            {
                Id = 1,
                DefaultVatRatePercent = 21,
                InvoiceNumberTemplate = "INV-{yyyy}-{seq:0000}",
                Errors = EmptyErrors
            };
        }

        return new SystemSettingsUpsertRequest
        {
            Id = row.Id,
            DefaultVatRatePercent = row.DefaultVatRatePercent,
            InvoiceNumberTemplate = row.InvoiceNumberTemplate,
            Errors = EmptyErrors
        };
    }

    public void TrimRequest(SystemSettingsUpsertRequest request)
    {
        request.InvoiceNumberTemplate = request.InvoiceNumberTemplate?.Trim();
    }

    public async Task SaveAsync(ApskaitaDbContext db, SystemSettingsUpsertRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = await db.SystemSettings.FirstOrDefaultAsync(ct);

        if (row is null)
        {
            row = new Entities.SystemSettings
            {
                Id = request.Id <= 0 ? 1 : request.Id,
                DefaultVatRatePercent = request.DefaultVatRatePercent,
                InvoiceNumberTemplate = request.InvoiceNumberTemplate ?? string.Empty,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = Guid.Empty,
                UpdatedByUserId = Guid.Empty
            };
            db.SystemSettings.Add(row);
        }
        else
        {
            row.DefaultVatRatePercent = request.DefaultVatRatePercent;
            row.InvoiceNumberTemplate = request.InvoiceNumberTemplate ?? string.Empty;
            row.UpdatedAtUtc = now;
            row.UpdatedByUserId = Guid.Empty;
        }

        await db.SaveChangesAsync(ct);
    }
}


