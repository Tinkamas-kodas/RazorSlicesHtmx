using Apskaita.Web.Features.Admin.ClientsRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.ClientsRegistry.Services;

internal sealed class ClientsContentService
{
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public async Task<ClientListModel> CreateListModelAsync(
        IAdminStore store,
        ClientSearchModel search,
        ClientListQuery query,
        CancellationToken ct)
    {
        var items = store.Clients.AsNoTracking()
            .Select(c => new ClientRowModel
            {
                Id = c.Id,
                Name = c.Info.Name,
                CompanyCode = c.Info.CompanyCode,
                VatCode = c.Info.VatCode,
                ContactEmail = c.Info.ContactEmail,
                IsActive = c.IsActive
            });

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            items = items.Where(c =>
                c.Name.Contains(term) ||
                (c.CompanyCode != null && c.CompanyCode.Contains(term)) ||
                (c.VatCode != null && c.VatCode.Contains(term)) ||
                (c.ContactEmail != null && c.ContactEmail.Contains(term)));
        }

        var paged = await items.ToPagedListAsync(query, ct);
        return new ClientListModel(paged.Total, search, query, paged.Items);
    }

    public ClientUpsertRequest CreateCreateFormModel() => new()
    {
        IsActive = true,
        Errors = EmptyErrors
    };

    public ClientUpsertRequest CreateEditFormModel(Client client) => new()
    {
        Id = client.Id,
        Name = client.Info.Name,
        CompanyCode = client.Info.CompanyCode,
        VatCode = client.Info.VatCode,
        AddressLine1 = client.Info.AddressLine1,
        AddressLine2 = client.Info.AddressLine2,
        City = client.Info.City,
        PostalCode = client.Info.PostalCode,
        CountryCode = client.Info.CountryCode,
        ContactName = client.Info.ContactName,
        ContactEmail = client.Info.ContactEmail,
        ContactPhone = client.Info.ContactPhone,
        BankName = client.Info.BankName,
        BankAccount = client.Info.BankAccount,
        BankSwift = client.Info.BankSwift,
        VatPayerCode = client.Info.VatPayerCode,
        IsActive = client.IsActive,
        Errors = EmptyErrors
    };

    public ClientDeleteDialogModel CreateDeleteDialogModel(Client client) =>
        new(client.Id, client.Info.Name);

    public void TrimRequest(ClientUpsertRequest request)
    {
        request.Name = request.Name?.Trim();
        request.CompanyCode = request.CompanyCode?.Trim();
        request.VatCode = request.VatCode?.Trim();
        request.AddressLine1 = request.AddressLine1?.Trim();
        request.AddressLine2 = request.AddressLine2?.Trim();
        request.City = request.City?.Trim();
        request.PostalCode = request.PostalCode?.Trim();
        request.CountryCode = request.CountryCode?.Trim();
        request.ContactName = request.ContactName?.Trim();
        request.ContactEmail = request.ContactEmail?.Trim();
        request.ContactPhone = request.ContactPhone?.Trim();
        request.BankName = request.BankName?.Trim();
        request.BankAccount = request.BankAccount?.Trim();
        request.BankSwift = request.BankSwift?.Trim();
        request.VatPayerCode = request.VatPayerCode?.Trim();
    }

    public Task<Client?> FindByIdAsync(IAdminStore store, Guid id, CancellationToken ct) =>
        store.Clients.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task CreateAsync(IAdminStore store, ClientUpsertRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var entity = new Client
        {
            Id = Guid.NewGuid(),
            Info = ToCompanyInfo(request),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = Guid.Empty,
            UpdatedByUserId = Guid.Empty
        };

        store.Clients.Add(entity);
        await store.SaveChangesAsync(ct);
    }

    public async Task ApplyAsync(IAdminStore store, Client client, ClientUpsertRequest request, CancellationToken ct)
    {
        client.Info = ToCompanyInfo(request);
        client.UpdatedAtUtc = DateTime.UtcNow;
        client.UpdatedByUserId = Guid.Empty;

        await store.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(IAdminStore store, Client client, CancellationToken ct)
    {
        store.Clients.Remove(client);
        await store.SaveChangesAsync(ct);
    }

    private static CompanyInfo ToCompanyInfo(ClientUpsertRequest request) => new()
    {
        Name = request.Name ?? string.Empty,
        CompanyCode = request.CompanyCode,
        VatCode = request.VatCode,
        AddressLine1 = request.AddressLine1,
        AddressLine2 = request.AddressLine2,
        City = request.City,
        PostalCode = request.PostalCode,
        CountryCode = request.CountryCode,
        ContactName = request.ContactName,
        ContactEmail = request.ContactEmail,
        ContactPhone = request.ContactPhone,
        BankName = request.BankName,
        BankAccount = request.BankAccount,
        BankSwift = request.BankSwift,
        VatPayerCode = request.VatPayerCode
    };
}
