using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RazorSlicesHtmx.Demo.Data;
using RazorSlicesHtmx.Demo.Features.Items.Models;
using RazorSlicesHtmx.Demo.Features.Items.Services;

namespace RazorSlicesHtmx.Demo.Features.Items.Validators;

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