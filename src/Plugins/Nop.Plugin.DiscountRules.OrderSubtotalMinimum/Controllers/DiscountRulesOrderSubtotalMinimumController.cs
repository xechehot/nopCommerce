using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nop.Core.Domain.Discounts;
using Nop.Plugin.DiscountRules.OrderSubtotalMinimum.Models;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.DiscountRules.OrderSubtotalMinimum.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class DiscountRulesOrderSubtotalMinimumController : BasePluginController
{
    #region Fields

    protected readonly IDiscountService _discountService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public DiscountRulesOrderSubtotalMinimumController(
        IDiscountService discountService,
        ILocalizationService localizationService,
        IPermissionService permissionService,
        ISettingService settingService)
    {
        _discountService = discountService;
        _localizationService = localizationService;
        _permissionService = permissionService;
        _settingService = settingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Get errors message from model state
    /// </summary>
    /// <param name="modelState">Model state</param>
    /// <returns>Errors message</returns>
    protected IEnumerable<string> GetErrorsFromModelState(ModelStateDictionary modelState)
    {
        return ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Promotions.DISCOUNTS_VIEW)]
    public async Task<IActionResult> Configure(int discountId, int? discountRequirementId)
    {
        // Load the discount
        var discount = await _discountService.GetDiscountByIdAsync(discountId)
            ?? throw new ArgumentException("Discount could not be loaded");

        // Check whether the discount requirement exists
        if (discountRequirementId.HasValue &&
            await _discountService.GetDiscountRequirementByIdAsync(discountRequirementId.Value) is null)
            return Content("Failed to load requirement.");

        // Try to get previously saved settings
        var minimumAmount = await _settingService.GetSettingByKeyAsync<decimal>(
            string.Format(DiscountRequirementDefaults.SettingsKey, discountRequirementId ?? 0));
        var includingTax = await _settingService.GetSettingByKeyAsync<bool>(
            string.Format(DiscountRequirementDefaults.IncludingTaxSettingsKey, discountRequirementId ?? 0));

        var model = new RequirementModel
        {
            RequirementId = discountRequirementId ?? 0,
            DiscountId = discountId,
            MinimumAmount = minimumAmount,
            IncludingTax = includingTax
        };

        // Set the HTML field prefix
        ViewData.TemplateInfo.HtmlFieldPrefix = string.Format(
            DiscountRequirementDefaults.HtmlFieldPrefix, discountRequirementId ?? 0);

        return View("~/Plugins/DiscountRules.OrderSubtotalMinimum/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Promotions.DISCOUNTS_CREATE_EDIT_DELETE)]
    public async Task<IActionResult> Configure(RequirementModel model)
    {
        if (ModelState.IsValid)
        {
            // Load the discount
            var discount = await _discountService.GetDiscountByIdAsync(model.DiscountId);
            if (discount == null)
                return NotFound(new { Errors = new[] { "Discount could not be loaded" } });

            // Get the discount requirement
            var discountRequirement = await _discountService.GetDiscountRequirementByIdAsync(model.RequirementId);

            // The discount requirement does not exist, so create a new one
            if (discountRequirement == null)
            {
                discountRequirement = new DiscountRequirement
                {
                    DiscountId = discount.Id,
                    DiscountRequirementRuleSystemName = DiscountRequirementDefaults.SystemName
                };

                await _discountService.InsertDiscountRequirementAsync(discountRequirement);
            }

            // Save minimum amount setting
            await _settingService.SetSettingAsync(
                string.Format(DiscountRequirementDefaults.SettingsKey, discountRequirement.Id),
                model.MinimumAmount);

            // Save including tax setting
            await _settingService.SetSettingAsync(
                string.Format(DiscountRequirementDefaults.IncludingTaxSettingsKey, discountRequirement.Id),
                model.IncludingTax);

            return Ok(new { NewRequirementId = discountRequirement.Id });
        }

        return Ok(new { Errors = GetErrorsFromModelState(ModelState) });
    }

    #endregion
}
