using Apskaita.Web.Features.Admin.ProjectRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.ProjectRegistry.Services;

/// <summary>
/// Projects read helper: populates the create/edit form's client and template option lists from the
/// read seam (<c>IQuery&lt;T&gt;</c>). Pure request behavior (construct/normalize/map) now lives on
/// <c>ProjectUpsertRequest</c> itself; writes go through <c>IAdminStore</c> in the endpoints.
/// </summary>
internal static class ProjectForms
{
    public static async Task PopulateOptionsAsync(ProjectUpsertRequest model, IQuery<Client> clients, IQuery<InvoiceTemplate> templates, CancellationToken ct)
    {
        model.Clients = await clients.All().OrderBy(x => x.Info.Name)
            .Select(x => new ProjectOptionModel(x.Id, x.Info.Name))
            .ToListAsync(ct);

        model.Templates = await templates.All().OrderBy(x => x.Name)
            .Select(x => new ProjectOptionModel(x.Id, x.Name))
            .ToListAsync(ct);
    }
}
