using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Plugins;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.DiscountRules.OrderSubtotalMinimum;

/// <summary>
/// Discount requirement rule that validates minimum order subtotal
/// </summary>
public class OrderSubtotalMinimumDiscountRequirementRule : BasePlugin, IDiscountRequirementRule
{
    #region Fields

    protected readonly IDiscountService _discountService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INopUrlHelper _nopUrlHelper;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly ISettingService _settingService;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public OrderSubtotalMinimumDiscountRequirementRule(
        IDiscountService discountService,
        ILocalizationService localizationService,
        INopUrlHelper nopUrlHelper,
        IOrderTotalCalculationService orderTotalCalculationService,
        IPriceFormatter priceFormatter,
        ISettingService settingService,
        IShoppingCartService shoppingCartService,
        IStoreContext storeContext,
        IWebHelper webHelper)
    {
        _discountService = discountService;
        _localizationService = localizationService;
        _nopUrlHelper = nopUrlHelper;
        _orderTotalCalculationService = orderTotalCalculationService;
        _priceFormatter = priceFormatter;
        _settingService = settingService;
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _webHelper = webHelper;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Check discount requirement
    /// </summary>
    /// <param name="request">Object that contains all information required to check the requirement</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the validation result
    /// </returns>
    public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Invalid by default
        var result = new DiscountRequirementValidationResult();

        if (request.Customer == null)
            return result;

        // Get the configured minimum amount for this requirement
        var minimumAmount = await _settingService.GetSettingByKeyAsync<decimal>(
            string.Format(DiscountRequirementDefaults.SettingsKey, request.DiscountRequirementId));

        if (minimumAmount <= decimal.Zero)
            return result;

        // Get the tax setting for this requirement
        var includingTax = await _settingService.GetSettingByKeyAsync<bool>(
            string.Format(DiscountRequirementDefaults.IncludingTaxSettingsKey, request.DiscountRequirementId));

        // Get the current store
        var store = await _storeContext.GetCurrentStoreAsync();

        // Get shopping cart for the customer
        var cart = await _shoppingCartService.GetShoppingCartAsync(
            request.Customer,
            ShoppingCartType.ShoppingCart,
            store.Id);

        if (!cart.Any())
        {
            result.UserError = await _localizationService.GetResourceAsync(
                "Plugins.DiscountRules.OrderSubtotalMinimum.NotEnoughSubtotal");
            return result;
        }

        // Calculate the shopping cart subtotal
        var (_, _, subTotalWithoutDiscount, _, _) = await _orderTotalCalculationService
            .GetShoppingCartSubTotalAsync(cart, includingTax);

        // Check if subtotal meets the minimum requirement
        if (subTotalWithoutDiscount >= minimumAmount)
        {
            result.IsValid = true;
        }
        else
        {
            // Format the minimum amount for display
            var formattedMinimum = await _priceFormatter.FormatPriceAsync(minimumAmount);
            var formattedCurrent = await _priceFormatter.FormatPriceAsync(subTotalWithoutDiscount);
            var formattedDifference = await _priceFormatter.FormatPriceAsync(minimumAmount - subTotalWithoutDiscount);

            result.UserError = string.Format(
                await _localizationService.GetResourceAsync("Plugins.DiscountRules.OrderSubtotalMinimum.NotEnoughSubtotal.Detailed"),
                formattedMinimum,
                formattedCurrent,
                formattedDifference);
        }

        return result;
    }

    /// <summary>
    /// Get URL for rule configuration
    /// </summary>
    /// <param name="discountId">Discount identifier</param>
    /// <param name="discountRequirementId">Discount requirement identifier (if editing)</param>
    /// <returns>URL</returns>
    public string GetConfigurationUrl(int discountId, int? discountRequirementId)
    {
        return _nopUrlHelper.RouteUrl(DiscountRequirementDefaults.ConfigurationRouteName,
            new { discountId, discountRequirementId },
            _webHelper.GetCurrentRequestProtocol());
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        // Add locale resources
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.DiscountRules.OrderSubtotalMinimum.Fields.MinimumAmount"] = "Minimum order subtotal",
            ["Plugins.DiscountRules.OrderSubtotalMinimum.Fields.MinimumAmount.Hint"] = "Enter the minimum order subtotal required for this discount to apply.",
            ["Plugins.DiscountRules.OrderSubtotalMinimum.Fields.MinimumAmount.Required"] = "Minimum amount must be greater than zero",
            ["Plugins.DiscountRules.OrderSubtotalMinimum.Fields.IncludingTax"] = "Include tax in subtotal",
            ["Plugins.DiscountRules.OrderSubtotalMinimum.Fields.IncludingTax.Hint"] = "Check if the subtotal should be calculated including tax.",
            ["Plugins.DiscountRules.OrderSubtotalMinimum.Fields.DiscountId.Required"] = "Discount is required",
            ["Plugins.DiscountRules.OrderSubtotalMinimum.NotEnoughSubtotal"] = "Your cart subtotal does not meet the minimum requirement for this discount.",
            ["Plugins.DiscountRules.OrderSubtotalMinimum.NotEnoughSubtotal.Detailed"] = "This discount requires a minimum subtotal of {0}. Your current subtotal is {1}. Add {2} more to qualify."
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UninstallAsync()
    {
        // Delete all discount requirements created by this rule
        var discountRequirements = (await _discountService.GetAllDiscountRequirementsAsync())
            .Where(dr => dr.DiscountRequirementRuleSystemName == DiscountRequirementDefaults.SystemName);

        foreach (var discountRequirement in discountRequirements)
        {
            // Delete the associated settings
            var minimumAmountSetting = await _settingService.GetSettingAsync(
                string.Format(DiscountRequirementDefaults.SettingsKey, discountRequirement.Id));
            if (minimumAmountSetting != null)
                await _settingService.DeleteSettingAsync(minimumAmountSetting);

            var includingTaxSetting = await _settingService.GetSettingAsync(
                string.Format(DiscountRequirementDefaults.IncludingTaxSettingsKey, discountRequirement.Id));
            if (includingTaxSetting != null)
                await _settingService.DeleteSettingAsync(includingTaxSetting);

            // Delete the requirement
            await _discountService.DeleteDiscountRequirementAsync(discountRequirement, false);
        }

        // Delete locale resources
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.DiscountRules.OrderSubtotalMinimum");

        await base.UninstallAsync();
    }

    #endregion
}
