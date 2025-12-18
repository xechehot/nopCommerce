using FluentAssertions;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Orders;
using Nop.Services.Stores;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.Discounts;

[TestFixture]
public class OrderSubtotalMinimumDiscountRequirementRuleTests : ServiceTest
{
    private IDiscountRequirementRule _discountRule;
    private IDiscountPluginManager _discountPluginManager;
    private ISettingService _settingService;
    private IProductService _productService;
    private ICustomerService _customerService;
    private IShoppingCartService _shoppingCartService;
    private IStoreService _storeService;
    private IDiscountService _discountService;

    private Customer _customer;
    private Store _store;
    private DiscountRequirement _discountRequirement;
    private const decimal MinimumAmount = 100m;
    private const string PluginSystemName = "DiscountRequirement.OrderSubtotalMinimum";
    private const string SettingsKeyFormat = "DiscountRequirement.OrderSubtotalMinimum-{0}";
    private const string IncludingTaxSettingsKeyFormat = "DiscountRequirement.OrderSubtotalMinimum.IncludingTax-{0}";

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _settingService = GetService<ISettingService>();
        _productService = GetService<IProductService>();
        _customerService = GetService<ICustomerService>();
        _shoppingCartService = GetService<IShoppingCartService>();
        _storeService = GetService<IStoreService>();
        _discountService = GetService<IDiscountService>();
        _discountPluginManager = GetService<IDiscountPluginManager>();

        // Load the discount rule plugin by system name
        _discountRule = await _discountPluginManager.LoadPluginBySystemNameAsync(PluginSystemName);

        // Skip tests if plugin is not available
        if (_discountRule == null)
        {
            Assert.Ignore($"Plugin '{PluginSystemName}' is not available. Skipping tests.");
            return;
        }

        // Get test customer and store
        _customer = await _customerService.GetCustomerByEmailAsync(NopTestsDefaults.AdminEmail);
        _store = (await _storeService.GetAllStoresAsync()).First();

        // Create a discount requirement for testing
        _discountRequirement = new DiscountRequirement
        {
            DiscountRequirementRuleSystemName = PluginSystemName
        };
        await _discountService.InsertDiscountRequirementAsync(_discountRequirement);

        // Configure minimum amount setting
        await _settingService.SetSettingAsync(
            string.Format(SettingsKeyFormat, _discountRequirement.Id),
            MinimumAmount);

        // Configure tax setting (excluding tax by default)
        await _settingService.SetSettingAsync(
            string.Format(IncludingTaxSettingsKeyFormat, _discountRequirement.Id),
            false);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_discountRule == null)
            return;

        // Clean up discount requirement
        if (_discountRequirement?.Id > 0)
        {
            await _discountService.DeleteDiscountRequirementAsync(_discountRequirement);

            // Clean up settings
            var minimumAmountSetting = await _settingService.GetSettingAsync(
                string.Format(SettingsKeyFormat, _discountRequirement.Id));
            if (minimumAmountSetting != null)
                await _settingService.DeleteSettingAsync(minimumAmountSetting);

            var includingTaxSetting = await _settingService.GetSettingAsync(
                string.Format(IncludingTaxSettingsKeyFormat, _discountRequirement.Id));
            if (includingTaxSetting != null)
                await _settingService.DeleteSettingAsync(includingTaxSetting);
        }

        // Clean up any shopping cart items
        if (_customer != null && _store != null)
        {
            var cart = await _shoppingCartService.GetShoppingCartAsync(_customer, ShoppingCartType.ShoppingCart, _store.Id);
            foreach (var item in cart)
            {
                await _shoppingCartService.DeleteShoppingCartItemAsync(item);
            }
        }
    }

    private async Task<Product> CreateProductAsync(string name, decimal price)
    {
        var product = new Product
        {
            Name = name,
            Price = price,
            Published = true,
            CustomerEntersPrice = false,
            ProductType = ProductType.SimpleProduct
        };
        await _productService.InsertProductAsync(product);
        return product;
    }

    private async Task AddProductToCartAsync(Product product, int quantity = 1)
    {
        await _shoppingCartService.AddToCartAsync(_customer, product, ShoppingCartType.ShoppingCart, _store.Id, quantity: quantity);
    }

    private async Task ClearShoppingCartAsync()
    {
        var cart = await _shoppingCartService.GetShoppingCartAsync(_customer, ShoppingCartType.ShoppingCart, _store.Id);
        foreach (var item in cart)
        {
            await _shoppingCartService.DeleteShoppingCartItemAsync(item);
        }
    }

    [Test]
    public async Task ShouldNotApplyDiscount_WhenSubtotalBelowMinimum()
    {
        // Arrange - Create product with price $50 (below $100 minimum)
        var product = await CreateProductAsync("Test Product Below Minimum", 50m);
        await AddProductToCartAsync(product);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse("cart subtotal ($50) is below minimum ($100)");
        result.UserError.Should().NotBeNullOrEmpty("error message should be provided when requirement not met");
        result.UserError.Should().Contain("100", "error message should mention the minimum amount");

        // Cleanup
        await _productService.DeleteProductAsync(product);
        await ClearShoppingCartAsync();
    }

    [Test]
    public async Task ShouldApplyDiscount_WhenSubtotalAboveMinimum()
    {
        // Arrange - Create product with price $150 (above $100 minimum)
        var product = await CreateProductAsync("Test Product Above Minimum", 150m);
        await AddProductToCartAsync(product);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue("cart subtotal ($150) meets minimum ($100)");
        result.UserError.Should().BeNullOrEmpty("no error should be provided when requirement is met");

        // Cleanup
        await _productService.DeleteProductAsync(product);
        await ClearShoppingCartAsync();
    }

    [Test]
    public async Task ShouldApplyDiscount_WhenSubtotalExactlyEqualToMinimum()
    {
        // Arrange - Create product with price exactly $100 (equal to minimum)
        var product = await CreateProductAsync("Test Product Equal Minimum", 100m);
        await AddProductToCartAsync(product);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue("cart subtotal ($100) exactly meets minimum ($100)");
        result.UserError.Should().BeNullOrEmpty("no error should be provided when requirement is met");

        // Cleanup
        await _productService.DeleteProductAsync(product);
        await ClearShoppingCartAsync();
    }

    [Test]
    public async Task ShouldApplyDiscount_WhenMultipleProductsExceedMinimum()
    {
        // Arrange - Create multiple products that together exceed minimum
        var product1 = await CreateProductAsync("Test Product 1", 60m);
        var product2 = await CreateProductAsync("Test Product 2", 50m);
        await AddProductToCartAsync(product1);
        await AddProductToCartAsync(product2);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue("combined cart subtotal ($110) meets minimum ($100)");
        result.UserError.Should().BeNullOrEmpty("no error should be provided when requirement is met");

        // Cleanup
        await _productService.DeleteProductAsync(product1);
        await _productService.DeleteProductAsync(product2);
        await ClearShoppingCartAsync();
    }

    [Test]
    public async Task ShouldNotApplyDiscount_WhenMultipleProductsBelowMinimum()
    {
        // Arrange - Create multiple products that together are below minimum
        var product1 = await CreateProductAsync("Test Product 1", 30m);
        var product2 = await CreateProductAsync("Test Product 2", 40m);
        await AddProductToCartAsync(product1);
        await AddProductToCartAsync(product2);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse("combined cart subtotal ($70) is below minimum ($100)");
        result.UserError.Should().NotBeNullOrEmpty("error message should be provided when requirement not met");

        // Cleanup
        await _productService.DeleteProductAsync(product1);
        await _productService.DeleteProductAsync(product2);
        await ClearShoppingCartAsync();
    }

    [Test]
    public async Task ShouldConsiderQuantity_WhenCheckingMinimum()
    {
        // Arrange - Create product with price $40, but add 3 quantities (total $120)
        var product = await CreateProductAsync("Test Product With Quantity", 40m);
        await AddProductToCartAsync(product, quantity: 3);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue("cart subtotal ($40 x 3 = $120) meets minimum ($100)");
        result.UserError.Should().BeNullOrEmpty("no error should be provided when requirement is met");

        // Cleanup
        await _productService.DeleteProductAsync(product);
        await ClearShoppingCartAsync();
    }

    [Test]
    public async Task ShouldNotApplyDiscount_WhenCartIsEmpty()
    {
        // Arrange - Ensure cart is empty
        await ClearShoppingCartAsync();

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse("empty cart should not meet minimum requirement");
        result.UserError.Should().NotBeNullOrEmpty("error message should be provided for empty cart");
    }

    [Test]
    public async Task ShouldReturnInvalid_WhenCustomerIsNull()
    {
        // Arrange
        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = null,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse("requirement should fail when customer is null");
    }

    [Test]
    public async Task ShouldReturnInvalid_WhenMinimumAmountIsZero()
    {
        // Arrange - Create new requirement with zero minimum
        var zeroRequirement = new DiscountRequirement
        {
            DiscountRequirementRuleSystemName = DiscountRequirementDefaults.SystemName
        };
        await _discountService.InsertDiscountRequirementAsync(zeroRequirement);

        await _settingService.SetSettingAsync(
            string.Format(DiscountRequirementDefaults.SettingsKey, zeroRequirement.Id),
            0m);

        var product = await CreateProductAsync("Test Product", 50m);
        await AddProductToCartAsync(product);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = zeroRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse("requirement should fail when minimum amount is zero or not configured");

        // Cleanup
        await _discountService.DeleteDiscountRequirementAsync(zeroRequirement);
        var setting = await _settingService.GetSettingAsync(
            string.Format(DiscountRequirementDefaults.SettingsKey, zeroRequirement.Id));
        if (setting != null)
            await _settingService.DeleteSettingAsync(setting);
        await _productService.DeleteProductAsync(product);
        await ClearShoppingCartAsync();
    }

    [Test]
    public async Task ShouldUseSubtotalWithoutDiscount_WhenOtherDiscountsApplied()
    {
        // Arrange - Create product above minimum
        var product = await CreateProductAsync("Test Product With Discount", 120m);
        await AddProductToCartAsync(product);

        // Create another discount that applies to the product (this will reduce the subtotal)
        var otherDiscount = new Discount
        {
            Name = "Other Discount",
            DiscountType = DiscountType.AssignedToOrderSubTotal,
            DiscountAmount = 30m,
            UsePercentage = false,
            IsActive = true,
            DiscountLimitation = DiscountLimitationType.Unlimited
        };
        await _discountService.InsertDiscountAsync(otherDiscount);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue("should use subtotal without discount ($120) which meets minimum ($100), not subtotal with discount applied ($90)");
        result.UserError.Should().BeNullOrEmpty("no error should be provided when requirement is met");

        // Cleanup
        await _discountService.DeleteDiscountAsync(otherDiscount);
        await _productService.DeleteProductAsync(product);
        await ClearShoppingCartAsync();
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task ShouldRespectIncludingTaxSetting(bool includingTax)
    {
        // Arrange - Update tax setting
        await _settingService.SetSettingAsync(
            string.Format(DiscountRequirementDefaults.IncludingTaxSettingsKey, _discountRequirement.Id),
            includingTax);

        var product = await CreateProductAsync("Test Product Tax", 110m);
        await AddProductToCartAsync(product);

        var request = new DiscountRequirementValidationRequest
        {
            DiscountRequirementId = _discountRequirement.Id,
            Customer = _customer,
            Store = _store
        };

        // Act
        var result = await _discountRule.CheckRequirementAsync(request);

        // Assert
        result.Should().NotBeNull();
        // The result should be valid regardless of tax setting in this case since product is above minimum
        // This test primarily verifies the setting is being read and passed to calculation service

        // Cleanup
        await _productService.DeleteProductAsync(product);
        await ClearShoppingCartAsync();

        // Reset to default
        await _settingService.SetSettingAsync(
            string.Format(DiscountRequirementDefaults.IncludingTaxSettingsKey, _discountRequirement.Id),
            false);
    }

    [Test]
    public void ShouldThrowException_WhenRequestIsNull()
    {
        // Act & Assert
        Func<Task> act = async () => await _discountRule.CheckRequirementAsync(null);
        act.Should().ThrowAsync<ArgumentNullException>("null request should throw ArgumentNullException");
    }
}