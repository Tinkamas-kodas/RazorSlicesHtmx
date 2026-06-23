using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using razr_slices_htmx2.Data;
using razr_slices_htmx2.Features.Items.Models;
using razr_slices_htmx2.Features.Items.Slices;
using razr_slices_htmx2.Shared.Models;

namespace razr_slices_htmx2.Features.Items.Services;

public sealed class ItemsContentService
{
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public FeatureMetadata CreateMetadata() => new(
        200,
        "items",
        "Items",
        "/items",
        "Paging, search, sort and CRUD with HTMX");

    public PageDefinition CreatePageDefinition() => new(
        CreateMetadata(),
        httpContext =>
        {
            var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
            var query = CreateQuery(httpContext.Request.Query);
            return _ItemsPage.Create(CreateListModel(db, query));
        });

    public ItemListQuery CreateQuery(IQueryCollection query) => new ItemListQuery(
        query["Search"].ToString(),
        query["SortBy"].ToString(),
        query["SortDir"].ToString(),
        int.TryParse(query["Page"], out var page) ? page : 1,
        int.TryParse(query["PageSize"], out var pageSize) ? pageSize : 5).Normalize();

    public ItemListQuery CreateQuery(ItemUpsertRequest request) => new ItemListQuery(
        request.Search,
        request.SortBy ?? "code",
        request.SortDir ?? "asc",
        request.Page,
        request.PageSize).Normalize();

    public ItemListQuery CreateQuery(ItemDeleteRequest request) => new ItemListQuery(
        request.Search,
        request.SortBy ?? "code",
        request.SortDir ?? "asc",
        request.Page,
        request.PageSize).Normalize();

    public ItemListModel CreateListModel(AppDbContext db, ItemListQuery incomingQuery)
    {
        var query = incomingQuery.Normalize();
        IQueryable<Item> items = db.Items.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            items = items.Where(item => item.Code.Contains(search) || item.Name.Contains(search));
        }

        items = query.SortBy switch
        {
            "name" => query.SortDir == "desc"
                ? items.OrderByDescending(item => item.Name).ThenBy(item => item.Code)
                : items.OrderBy(item => item.Name).ThenBy(item => item.Code),
            "isenabled" => query.SortDir == "desc"
                ? items.OrderByDescending(item => item.IsEnabled).ThenBy(item => item.Code)
                : items.OrderBy(item => item.IsEnabled).ThenBy(item => item.Code),
            _ => query.SortDir == "desc"
                ? items.OrderByDescending(item => item.Code)
                : items.OrderBy(item => item.Code)
        };

        var totalCount = items.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)query.PageSize));
        var currentPage = Math.Min(query.Page, totalPages);
        var pageQuery = query with { Page = currentPage };

        var rows = items
            .Skip((currentPage - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList()
            .Select(item => new ItemRowModel(
                item.Id,
                item.Code,
                item.Name,
                item.IsEnabled,
                pageQuery.ToRoute($"/items/edit/{item.Id}"),
                pageQuery.ToRoute($"/items/delete/{item.Id}")))
            .ToArray();

        var fromItem = rows.Length == 0 ? 0 : ((currentPage - 1) * query.PageSize) + 1;
        var toItem = rows.Length == 0 ? 0 : fromItem + rows.Length - 1;

        return new ItemListModel(pageQuery, rows, totalCount, totalPages, currentPage, fromItem, toItem);
    }

    public ItemFormModel CreateCreateFormModel(ItemListQuery query) => new(
        "Create item",
        "Create",
        "/items/create",
        query.ToRoute("/items"),
        new ItemUpsertRequest
        {
            IsEnabled = true,
            Search = query.Search,
            SortBy = query.SortBy,
            SortDir = query.SortDir,
            Page = query.Page,
            PageSize = query.PageSize
        },
        EmptyErrors);

    public ItemFormModel CreateEditFormModel(Item item, ItemListQuery query) => new(
        "Edit item",
        "Save changes",
        $"/items/edit/{item.Id}",
        query.ToRoute("/items"),
        new ItemUpsertRequest
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            IsEnabled = item.IsEnabled,
            Search = query.Search,
            SortBy = query.SortBy,
            SortDir = query.SortDir,
            Page = query.Page,
            PageSize = query.PageSize
        },
        EmptyErrors);

    public ItemFormModel CreateInvalidFormModel(ItemUpsertRequest request, ValidationResult validationResult, bool isEdit)
    {
        TrimRequest(request);

        return new ItemFormModel(
            isEdit ? "Edit item" : "Create item",
            isEdit ? "Save changes" : "Create",
            isEdit ? $"/items/edit/{request.Id}" : "/items/create",
            CreateQuery(request).ToRoute("/items"),
            request,
            validationResult.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(error => (error.ErrorCode, error.ErrorMessage))
                        .ToArray()));
    }

    public ItemDeleteDialogModel CreateDeleteDialogModel(Item item, ItemListQuery query) => new(
        item.Id,
        item.Code,
        item.Name,
        $"/items/delete/{item.Id}",
        query);

    public void TrimRequest(ItemUpsertRequest request)
    {
        request.Code = request.Code?.Trim();
        request.Name = request.Name?.Trim();
    }

    public Item CreateEntity(ItemUpsertRequest request) => new()
    {
        Code = NormalizeCode(request.Code),
        Name = NormalizeName(request.Name),
        IsEnabled = request.IsEnabled
    };

    public void Apply(Item entity, ItemUpsertRequest request)
    {
        entity.Code = NormalizeCode(request.Code);
        entity.Name = NormalizeName(request.Name);
        entity.IsEnabled = request.IsEnabled;
    }

    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();
}