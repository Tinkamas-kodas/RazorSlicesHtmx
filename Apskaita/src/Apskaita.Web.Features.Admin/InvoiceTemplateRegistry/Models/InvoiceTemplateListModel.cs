namespace Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Models;

public sealed record InvoiceTemplateListModel(int Total, InvoiceTemplateSearchModel Search, InvoiceTemplateListQuery Request, List<InvoiceTemplateRowModel> Items)
    : ListResponse<InvoiceTemplateRowModel, InvoiceTemplateListQuery>(Total, Request, Items);

