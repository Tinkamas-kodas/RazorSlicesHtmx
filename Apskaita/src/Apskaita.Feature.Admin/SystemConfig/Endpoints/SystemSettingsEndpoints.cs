using Apskaita.Web.Features.Admin.SystemConfig.Models;
using Apskaita.Web.Features.Admin.SystemConfig.Services;
using Apskaita.Web.Features.Admin.SystemConfig.Slices;
using Apskaita.Web.Shared;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.SystemConfig.Endpoints;

public sealed class SystemSettingsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<SystemConfigWorkspaceFactory>(resultBuilder)
{
    private readonly SystemSettingsContentService _content = new();
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "system-settings", "System Settings", "", Order: 60, AuthorizationPolicy: AuthPolicies.Admin)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Admin);

        group.MapGet("", async (ApskaitaDbContext db, CancellationToken ct) =>
        {
            var model = await _content.CreateModelAsync(db, ct);
            return Result.Page(_SettingsPage.Create(model));
        });

        group.MapPost("", async (
            SystemSettingsUpsertRequest request,
            IValidator<SystemSettingsUpsertRequest> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            _content.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                return Result.Page(_SettingsPage.Create(request));
            }

            await _content.SaveAsync(db, request, ct);
            var model = await _content.CreateModelAsync(db, ct);
            return Result.Page(_SettingsPage.Create(model)).WithToast("System settings were updated.");
        });
    }
}

