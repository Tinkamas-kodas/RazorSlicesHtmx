using Apskaita.Web.Features.Admin.IssuerRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.IssuerRegistry.Services;

internal sealed class IssuersContentService
{
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public async Task<IssuerListModel> CreateListModelAsync(
        IAdminStore store,
        IssuerSearchModel search,
        IssuerListQuery query,
        CancellationToken ct)
    {
        var items = store.Issuers.AsNoTracking()
            .Select(c => new IssuerRowModel
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
        return new IssuerListModel(paged.Total, search, query, paged.Items);
    }

    public IssuerUpsertRequest CreateCreateFormModel() => new()
    {
        IsActive = true,
        Errors = EmptyErrors
    };

    public IssuerUpsertRequest CreateEditFormModel(Issuer issuer) => new()
    {
        Id = issuer.Id,
        Name = issuer.Info.Name,
        CompanyCode = issuer.Info.CompanyCode,
        VatCode = issuer.Info.VatCode,
        AddressLine1 = issuer.Info.AddressLine1,
        AddressLine2 = issuer.Info.AddressLine2,
        City = issuer.Info.City,
        PostalCode = issuer.Info.PostalCode,
        CountryCode = issuer.Info.CountryCode,
        ContactName = issuer.Info.ContactName,
        ContactEmail = issuer.Info.ContactEmail,
        ContactPhone = issuer.Info.ContactPhone,
        BankName = issuer.Info.BankName,
        BankAccount = issuer.Info.BankAccount,
        BankSwift = issuer.Info.BankSwift,
        VatPayerCode = issuer.Info.VatPayerCode,
        IsActive = issuer.IsActive,
        Errors = EmptyErrors
    };

    public IssuerDeleteDialogModel CreateDeleteDialogModel(Issuer issuer) =>
        new(issuer.Id, issuer.Info.Name);

    public void TrimRequest(IssuerUpsertRequest request)
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

    public Task<Issuer?> FindByIdAsync(IAdminStore store, Guid id, CancellationToken ct) =>
        store.Issuers.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task CreateAsync(IAdminStore store, IssuerUpsertRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var entity = new Issuer
        {
            Id = Guid.NewGuid(),
            Info = ToCompanyInfo(request),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = Guid.Empty,
            UpdatedByUserId = Guid.Empty
        };

        store.Issuers.Add(entity);
        await store.SaveChangesAsync(ct);
    }

    public async Task ApplyAsync(IAdminStore store, Issuer issuer, IssuerUpsertRequest request, CancellationToken ct)
    {
        issuer.Info = ToCompanyInfo(request);
        issuer.UpdatedAtUtc = DateTime.UtcNow;
        issuer.UpdatedByUserId = Guid.Empty;

        await store.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(IAdminStore store, Issuer issuer, CancellationToken ct)
    {
        store.Issuers.Remove(issuer);
        await store.SaveChangesAsync(ct);
    }

    private static CompanyInfo ToCompanyInfo(IssuerUpsertRequest request) => new()
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

