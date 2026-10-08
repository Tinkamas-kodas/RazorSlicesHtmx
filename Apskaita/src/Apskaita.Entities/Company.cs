namespace Apskaita.Entities;

public class Company : BaseEntity
{
    public Guid Id { get; set; }
    public CompanyInfo Info { get; set; } = new();
    public bool IsActive { get; set; } = true;
}
