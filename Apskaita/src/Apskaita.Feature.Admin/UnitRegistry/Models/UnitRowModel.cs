namespace Apskaita.Web.Features.Admin.UnitRegistry.Models;

public sealed class UnitRowModel
{
    
    [SortDisable] public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }

    
}

