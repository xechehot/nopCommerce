using FluentValidation;
using Nop.Plugin.DiscountRules.OrderSubtotalMinimum.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.DiscountRules.OrderSubtotalMinimum.Validators;

/// <summary>
/// Represents a validator for <see cref="RequirementModel"/>
/// </summary>
public class RequirementModelValidator : BaseNopValidator<RequirementModel>
{
    public RequirementModelValidator(ILocalizationService localizationService)
    {
        RuleFor(model => model.DiscountId)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.DiscountRules.OrderSubtotalMinimum.Fields.DiscountId.Required"));

        RuleFor(model => model.MinimumAmount)
            .GreaterThan(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.DiscountRules.OrderSubtotalMinimum.Fields.MinimumAmount.Required"));
    }
}
