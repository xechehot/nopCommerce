using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.DiscountRules.OrderSubtotalMinimum.Models;

/// <summary>
/// Represents the requirement model for minimum order subtotal
/// </summary>
public class RequirementModel
{
    /// <summary>
    /// Gets or sets the minimum subtotal amount required
    /// </summary>
    [NopResourceDisplayName("Plugins.DiscountRules.OrderSubtotalMinimum.Fields.MinimumAmount")]
    public decimal MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to calculate subtotal including tax
    /// </summary>
    [NopResourceDisplayName("Plugins.DiscountRules.OrderSubtotalMinimum.Fields.IncludingTax")]
    public bool IncludingTax { get; set; }

    /// <summary>
    /// Gets or sets the discount identifier
    /// </summary>
    public int DiscountId { get; set; }

    /// <summary>
    /// Gets or sets the requirement identifier
    /// </summary>
    public int RequirementId { get; set; }
}
