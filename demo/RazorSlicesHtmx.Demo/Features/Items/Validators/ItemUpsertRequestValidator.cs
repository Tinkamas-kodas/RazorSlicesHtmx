using FluentValidation;
using Microsoft.EntityFrameworkCore;
using razr_slices_htmx2.Data;
using razr_slices_htmx2.Features.Items.Models;
using razr_slices_htmx2.Features.Items.Services;

namespace razr_slices_htmx2.Features.Items.Validators;

public sealed class ItemUpsertRequestValidator : AbstractValidator<ItemUpsertRequest>
{
    public ItemUpsertRequestValidator(AppDbContext db)
    {
        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("Code is required.")
            .Length(2, 32).WithMessage("Code must be between 2 and 32 characters.")
            .Must((request, code) => BeUniqueCode(db, request, code)).WithMessage("Code must be unique.");

        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Length(2, 128).WithMessage("Name must be between 2 and 128 characters.");
    }

    private static bool BeUniqueCode(AppDbContext db, ItemUpsertRequest request, string? code)
    {
        var normalizedCode = ItemsContentService.NormalizeCode(code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return true;
        }

        return !db.Items.AsNoTracking().Any(item =>
            item.Code.ToUpper() == normalizedCode && item.Id != (request.Id ?? 0));
    }
}