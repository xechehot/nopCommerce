namespace Nop.Plugin.DiscountRules.OrderSubtotalMinimum;

/// <summary>
/// Represents defaults for the discount requirement rule
/// </summary>
public static class DiscountRequirementDefaults
{
    /// <summary>
    /// The system name of the discount requirement rule
    /// </summary>
    public static string SystemName => "DiscountRequirement.OrderSubtotalMinimum";

    /// <summary>
    /// The key of the settings to save minimum subtotal amount
    /// </summary>
    public static string SettingsKey => "DiscountRequirement.OrderSubtotalMinimum-{0}";

    /// <summary>
    /// The key of the settings to save whether to include tax in subtotal calculation
    /// </summary>
    public static string IncludingTaxSettingsKey => "DiscountRequirement.OrderSubtotalMinimum.IncludingTax-{0}";

    /// <summary>
    /// The HTML field prefix for discount requirements
    /// </summary>
    public static string HtmlFieldPrefix => "DiscountRulesOrderSubtotalMinimum{0}";

    /// <summary>
    /// Gets the configuration route name
    /// </summary>
    public static string ConfigurationRouteName => "DiscountRequirement.OrderSubtotalMinimum.Configure";
}
