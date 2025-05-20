using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.Orders.AddressValidation;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.ResolveAddressResponse;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Extensibility.Editors;
using System;
using System.Linq;
using System.Text;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider;

/// <summary>
/// Avalara address validation provider
/// </summary>
[AddInName("Avalara address validation provider")]
public class AvalaraAddressValidatorProvider : AddressValidatorProvider
{
    [AddInParameter("Account Id"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
    public string AccountId { get; set; }

    [AddInParameter("License Key"), AddInParameterEditor(typeof(TextParameterEditor), "size=80; password=true")]
    public string LicenseKey { get; set; }

    [AddInParameter("Validate Billing Address"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
    public bool ValidateBillingAddress { get; set; }

    [AddInParameter("Validate Shipping Address"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
    public bool ValidateShippingAddress { get; set; }

    [AddInParameter("Debug"), AddInParameterEditor(typeof(YesNoParameterEditor), "infoText=Create a log of the request and response from Avalara")]
    public bool Debug { get; set; }

    [AddInParameter("Test Mode"), AddInParameterEditor(typeof(YesNoParameterEditor), "infoText=Set to use sandbox (test mode) for the API requests. Uncheck when ready for production.")]
    public bool TestMode { get; set; }

    public override void Validate(Order order)
    {
        if (ValidateBillingAddress)
        {
            AddressLocationInfo billingAddress = GetBillingAddress(order);
            var addressValidatorResult = new AddressValidatorResult(ValidatorId, AddressType.Billing);

            try
            {
                if (string.IsNullOrEmpty(billingAddress.PostalCode) && string.IsNullOrEmpty(billingAddress.Line1))
                {
                    addressValidatorResult.IsError = true;
                    addressValidatorResult.ErrorMessage = "Insufficient address information";
                }
                else
                {
                    ResolveAddressResponse validateResult = ValidateAddress(billingAddress, AddressType.Billing);

                    if (validateResult.Messages is null && validateResult.ValidatedAddresses.Any())
                    {
                        ValidatedAddressInfo validAddress = validateResult.ValidatedAddresses.FirstOrDefault();
                        addressValidatorResult.CheckAddressField(AddressFieldType.AddressLine1, order.CustomerAddress ?? "", validAddress.Line1 ?? "");
                        addressValidatorResult.CheckAddressField(AddressFieldType.AddressLine2, order.CustomerAddress2 ?? "", validAddress.Line2 ?? "");
                        addressValidatorResult.CheckAddressField(AddressFieldType.City, order.CustomerCity ?? "", validAddress.City ?? "");
                        addressValidatorResult.CheckAddressField(AddressFieldType.Region, order.CustomerRegion ?? "", validAddress.Region ?? "");
                        addressValidatorResult.CheckAddressField(AddressFieldType.ZipCode, order.CustomerZip ?? "", validAddress.PostalCode ?? "");
                    }
                    else
                    {
                        addressValidatorResult.IsError = true;
                        addressValidatorResult.ErrorMessage = GetErrorMessage(validateResult);
                    }
                }
            }
            catch (Exception exception)
            {
                addressValidatorResult.IsError = true;
                addressValidatorResult.ErrorMessage = "AvaTax threw an exception while validating address: " + exception.Message;
            }

            if (addressValidatorResult.IsError || addressValidatorResult.AddressFields.Count > 0)
                order.AddressValidatorResults.Add(addressValidatorResult);
        }

        if (ValidateShippingAddress)
        {
            AddressLocationInfo deliveryAddress = GetDeliveryAddress(order);
            var addressValidatorResult = new AddressValidatorResult(ValidatorId, AddressType.Delivery);

            try
            {
                if (string.IsNullOrEmpty(deliveryAddress.PostalCode) && string.IsNullOrEmpty(deliveryAddress.Line1))
                {
                    addressValidatorResult.IsError = true;
                    addressValidatorResult.ErrorMessage = "Insufficient address information";
                }
                else
                {
                    ResolveAddressResponse validateResult = ValidateAddress(deliveryAddress, AddressType.Delivery);

                    if (validateResult.Messages is null && validateResult.ValidatedAddresses.Any())
                    {
                        var validAddress = validateResult.ValidatedAddresses.FirstOrDefault();
                        addressValidatorResult.CheckAddressField(AddressFieldType.AddressLine1, order.DeliveryAddress ?? "", validAddress.Line1 ?? "");
                        addressValidatorResult.CheckAddressField(AddressFieldType.AddressLine2, order.DeliveryAddress2 ?? "", validAddress.Line2 ?? "");
                        addressValidatorResult.CheckAddressField(AddressFieldType.City, order.DeliveryCity ?? "", validAddress.City ?? "");
                        addressValidatorResult.CheckAddressField(AddressFieldType.Region, order.DeliveryRegion ?? "", validAddress.Region ?? "");
                        addressValidatorResult.CheckAddressField(AddressFieldType.ZipCode, order.DeliveryZip ?? "", validAddress.PostalCode ?? "");
                    }
                    else
                    {
                        addressValidatorResult.IsError = true;
                        addressValidatorResult.ErrorMessage = GetErrorMessage(validateResult);
                    }
                }
            }
            catch (Exception exception)
            {
                addressValidatorResult.IsError = true;
                addressValidatorResult.ErrorMessage = "AvaTax threw an exception while validating address: " + exception.Message;
            }

            if (addressValidatorResult.IsError || addressValidatorResult.AddressFields.Count > 0)
            {
                order.AddressValidatorResults.Add(addressValidatorResult);
            }
        }

    }

    private ResolveAddressResponse ValidateAddress(AddressLocationInfo address, AddressType addressType)
    {
        var validateResult = CheckIsAddressCached(address, addressType);

        if (validateResult is null)
        {
            var service = new AvalaraService
            {
                AccountId = AccountId,
                LicenseKey = LicenseKey,
                TestMode = TestMode,
                DebugLog = Debug
            };
            validateResult = service.ResolveAddress(address);

            CacheRateRequest(address, addressType, validateResult);
        }

        return validateResult;
    }

    private static string AddressValidatorCacheKey(int validatorId, AddressType addressType)
        => $"AddressServiceRequest_{validatorId}_{addressType}";

    private ResolveAddressResponse CheckIsAddressCached(AddressLocationInfo address, AddressType addressType)
    {
        ResolveAddressResponse validateResult = null;

        if (Context.Current.Session[AddressValidatorCacheKey(ValidatorId, addressType)] is not null)
        {
            var cachedRequest = (ValidateCache)Context.Current.Session[AddressValidatorCacheKey(ValidatorId, addressType)];

            if (string.Equals(address.Country, cachedRequest.Address.Country, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(address.Region, cachedRequest.Address.Region, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(address.PostalCode, cachedRequest.Address.PostalCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(address.Line1, cachedRequest.Address.Line1, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(address.Line2, cachedRequest.Address.Line2, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(address.Line3, cachedRequest.Address.Line3, StringComparison.OrdinalIgnoreCase))
            {
                validateResult = cachedRequest.ValidateResult;
            }
        }

        return validateResult;
    }

    private void CacheRateRequest(AddressLocationInfo address, AddressType addressType, ResolveAddressResponse validateResult)
    {
        Context.Current.Session[AddressValidatorCacheKey(ValidatorId, addressType)] = new ValidateCache
        {
            Address = address,
            ValidateResult = validateResult
        };
    }

    private class ValidateCache
    {
        public AddressLocationInfo Address;
        public ResolveAddressResponse ValidateResult;
    }

    internal static AddressLocationInfo GetBillingAddress(Order order) => new()
    {
        Line1 = order.CustomerAddress,
        Line2 = order.CustomerAddress2,
        City = order.CustomerCity,
        Region = order.CustomerRegion,
        PostalCode = order.CustomerZip,
        Country = order.CustomerCountryCode
    };

    internal static AddressLocationInfo GetDeliveryAddress(Order order) => new()
    {
        Line1 = order.DeliveryAddress,
        Line2 = order.DeliveryAddress2,
        City = order.DeliveryCity,
        Region = order.DeliveryRegion,
        PostalCode = order.DeliveryZip,
        Country = order.DeliveryCountryCode
    };

    internal static AddressLocationInfo GetOriginAddress(AvalaraTaxProvider taxProvider) => new()
    {
        Line1 = taxProvider.StreetAddress,
        Line2 = taxProvider.StreetAddress2,
        City = taxProvider.City,
        Region = taxProvider.Region,
        PostalCode = taxProvider.PostalCode,
        Country = taxProvider.Country
    };

    private string GetErrorMessage(ResolveAddressResponse validateResult)
    {
        var errMessages = new StringBuilder();

        if (validateResult.Messages?.Any() is true)
        {
            foreach (var message in validateResult.Messages)
            {
                errMessages.AppendLine($"Details: {message.Details}");
                errMessages.AppendLine($"RefersTo: {message.RefersTo}");
                errMessages.AppendLine($"Severity: {message.Severity}");
                errMessages.AppendLine($"Source: {message.Source}");
                errMessages.AppendLine($"Summary: {message.Summary}");
            }
        }

        return errMessages.ToString();
    }
}
