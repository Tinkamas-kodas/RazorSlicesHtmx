using Apskaita.Entities.Contracts;
using Apskaita.Feature.Admin.Persistence;
using Apskaita.Feature.Admin.SystemConfig.Models;
using Apskaita.Feature.Admin.SystemConfig.Slices;
using Apskaita.Shared;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.Admin.SystemConfig.Endpoints;

public sealed class SystemSettingsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<SystemConfigWorkspaceFactory>(resultBuilder)
{
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "system-settings", "System Settings", "", Order: 60, AuthorizationPolicy: AuthPolicies.Admin)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Admin);

        // GET — read seam (IQuery<SystemSettings>); default form when no row exists yet.
        group.MapGet("", async (IQuery<SystemSettings> settings, CancellationToken ct) =>
        {
            var row = await settings.All().FirstOrDefaultAsync(ct);
            var model = row is null
                ? SystemSettingsUpsertRequest.CreateDefault()
                : SystemSettingsUpsertRequest.FromEntity(row);
            return Result.Page(_SettingsPage.Create(model));
        });

        // POST — write seam (IAdminStore); single-row upsert.
        group.MapPost("", async (
            SystemSettingsUpsertRequest request,
            IValidator<SystemSettingsUpsertRequest> validator,
            IAdminStore store,
            CancellationToken ct) =>
        {
            request.Normalize();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                return Result.Page(_SettingsPage.Create(request));
            }

            var now = DateTime.UtcNow;
            var row = await store.SystemSettings.FirstOrDefaultAsync(ct);

            if (row is null)
            {
                row = new SystemSettings
                {
                    Id = request.Id <= 0 ? 1 : request.Id,
                    DefaultVatRatePercent = request.DefaultVatRatePercent,
                    InvoiceNumberTemplate = request.InvoiceNumberTemplate ?? string.Empty,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    CreatedByUserId = Guid.Empty,
                    UpdatedByUserId = Guid.Empty
                };
                store.SystemSettings.Add(row);
            }
            else
            {
                row.DefaultVatRatePercent = request.DefaultVatRatePercent;
                row.InvoiceNumberTemplate = request.InvoiceNumberTemplate ?? string.Empty;
                row.UpdatedAtUtc = now;
                row.UpdatedByUserId = Guid.Empty;
            }

            await store.SaveChangesAsync(ct);

            return Result.Page(_SettingsPage.Create(SystemSettingsUpsertRequest.FromEntity(row)))
                .WithToast("System settings were updated.");
        });
    }
}
