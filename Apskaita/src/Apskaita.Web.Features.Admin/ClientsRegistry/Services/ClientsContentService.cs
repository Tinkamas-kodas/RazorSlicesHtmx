using Apskaita.Entities;
using Apskaita.Web.Features.Admin.ClientsRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.ClientsRegistry.Services;

public sealed class ClientsContentService
{
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public async Task<ClientListModel> CreateListModelAsync(
        ApskaitaDbContext db,
        ClientSearchModel search,
        ClientListQuery query,
        CancellationToken ct)
    {
        var items = db.Clients.AsNoTracking()
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

    public Task<Client?> FindByIdAsync(ApskaitaDbContext db, Guid id, CancellationToken ct) =>
        db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task CreateAsync(ApskaitaDbContext db, ClientUpsertRequest request, CancellationToken ct)
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

        db.Clients.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task ApplyAsync(ApskaitaDbContext db, Client client, ClientUpsertRequest request, CancellationToken ct)
    {
        client.Info = ToCompanyInfo(request);
        client.UpdatedAtUtc = DateTime.UtcNow;
        client.UpdatedByUserId = Guid.Empty;

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ApskaitaDbContext db, Client client, CancellationToken ct)
    {
        db.Clients.Remove(client);
        await db.SaveChangesAsync(ct);
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
