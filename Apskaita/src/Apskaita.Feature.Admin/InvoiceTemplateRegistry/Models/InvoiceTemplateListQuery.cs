using System.Linq.Expressions;

namespace Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Models;

public sealed partial class InvoiceTemplateListQuery : ListRequest<InvoiceTemplateRowModel>
{
    public override IReadOnlyDictionary<string, Expression<Func<InvoiceTemplateRowModel, object?>>> SortMap { get; } =
        new Dictionary<string, Expression<Func<InvoiceTemplateRowModel, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(InvoiceTemplateRowModel.Name)] = x => x.Name,
            [nameof(InvoiceTemplateRowModel.Code)] = x => x.Code,
            [nameof(InvoiceTemplateRowModel.LanguageCode)] = x => x.LanguageCode,
            [nameof(InvoiceTemplateRowModel.IsDefault)] = x => x.IsDefault,
            [nameof(InvoiceTemplateRowModel.IsActive)] = x => x.IsActive
        };

    public InvoiceTemplateListQuery()
    {
        SortBy = nameof(InvoiceTemplateRowModel.Name);
        Page = 1;
        PageSize = 10;
    }
}

