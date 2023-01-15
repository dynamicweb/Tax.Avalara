using Avalara.AvaTax.RestClient;
using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.Orders.AddressValidation;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Extensibility.Editors;
using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider
{
    /// <summary>
    /// Avalara address validation provider
    /// </summary>
    [AddInName("Avalara address validation provider")]
    public class AvalaraAddressValidatorProvider : AddressValidatorProvider
    {

        #region Fields

        [AddInParameter("Account"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string Account { get; set; }

        [AddInParameter("License"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string License { get; set; }

        [AddInParameter("Company Code"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string CompanyCode { get; set; }

        [AddInParameter("Address Service Url"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string AddressServiceUrl { get; set; }

        [AddInParameter("Validate Billing Address"), AddInParameterEditor(typeof(YesNoParameterEditor), ""), AddInDescription("Create a log of the request and response from UPS")]
        public bool ValidateBillingAddress { get; set; }

        [AddInParameter("Validate Shipping Address"), AddInParameterEditor(typeof(YesNoParameterEditor), ""), AddInDescription("Create a log of the request and response from UPS")]
        public bool ValidateShippingAddress { get; set; }

        [AddInParameter("Debug"), AddInParameterEditor(typeof(YesNoParameterEditor), ""), AddInDescription("Create a log of the request and response from UPS")]
        public bool Debug { get; set; }
        #endregion

        public override void Validate(Order order)
        {
            var service = PrepareAddressSvc();

            if (ValidateBillingAddress)
            {
                var billingAddress = GetBillingAddress(order);
                var addressValidatorResult = new AddressValidatorResult(ValidatorId, AddressType.Billing);

                try
                {
                    if (string.IsNullOrEmpty(billingAddress.postalCode) && string.IsNullOrEmpty(billingAddress.line1))
                    {
                        addressValidatorResult.IsError = true;
                        addressValidatorResult.ErrorMessage = "Insufficient address information";
                    }
                    else
                    {
                        var validateResult = ValidateAddress(service, billingAddress, AddressType.Billing);

                        if (validateResult.messages is null)
                        {
                            var validAddress = validateResult.validatedAddresses[0];
                            addressValidatorResult.CheckAddressField(AddressFieldType.AddressLine1, order.CustomerAddress, validAddress.line1);
                            addressValidatorResult.CheckAddressField(AddressFieldType.AddressLine2, order.CustomerAddress2, validAddress.line2);
                            addressValidatorResult.CheckAddressField(AddressFieldType.City, order.CustomerCity, validAddress.city);
                            addressValidatorResult.CheckAddressField(AddressFieldType.Region, order.CustomerRegion, validAddress.region);
                            addressValidatorResult.CheckAddressField(AddressFieldType.ZipCode, order.CustomerZip, validAddress.postalCode);
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

            if (ValidateShippingAddress)
            {
                var deliveryAddress = GetDeliveryAddress(order);
                var addressValidatorResult = new AddressValidatorResult(ValidatorId, AddressType.Delivery);

                try
                {
                    if (string.IsNullOrEmpty(deliveryAddress.postalCode) && string.IsNullOrEmpty(deliveryAddress.line1))
                    {
                        addressValidatorResult.IsError = true;
                        addressValidatorResult.ErrorMessage = "Insufficient address information";
                    }
                    else
                    {
                        var validateResult = ValidateAddress(service, deliveryAddress, AddressType.Delivery);

                        if (validateResult.messages is null)
                        {
                            var validAddress = validateResult.validatedAddresses[0];
                            addressValidatorResult.CheckAddressField(AddressFieldType.AddressLine1, order.DeliveryAddress, validAddress.line1);
                            addressValidatorResult.CheckAddressField(AddressFieldType.AddressLine2, order.DeliveryAddress2, validAddress.line2);
                            addressValidatorResult.CheckAddressField(AddressFieldType.City, order.DeliveryCity, validAddress.city);
                            addressValidatorResult.CheckAddressField(AddressFieldType.Region, order.DeliveryRegion, validAddress.region);
                            addressValidatorResult.CheckAddressField(AddressFieldType.ZipCode, order.DeliveryZip, validAddress.postalCode);
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

        #region private functions

        private AddressResolutionModel ValidateAddress(AvaTaxClient addressService, AddressLocationInfo address, AddressType addressType)
        {
            var validateResult = CheckIsAddressCached(address, addressType);

            if (validateResult == null)
            {
                validateResult = addressService.ResolveAddress(address.line1, address.line2, null, address.city, address.region, address.postalCode, address.country, TextCase.Mixed);
                
                if (Debug)
                {
                    SaveAvaTaxLog(validateResult);
                }

                CacheRateRequest(address, addressType, validateResult);
            }

            return validateResult;
        }
        private AvaTaxClient PrepareAddressSvc()
        {
            return new AvaTaxClient("Dynamicweb AvaTax", "1.0", "Dynamicweb 9.0", new Uri(AddressServiceUrl)).WithSecurity(Account, License);
        }

        #region Cache address validator request

        private static string AddressValidatorCacheKey(int validatorId, AddressType addressType)
        {
            return string.Format("AddressServiceRequest_{0}_{1}", validatorId, addressType);
        }

        private AddressResolutionModel CheckIsAddressCached(AddressLocationInfo address, AddressType addressType)
        {
            AddressResolutionModel validateResult = null;

            if ((Context.Current.Session[AddressValidatorCacheKey(ValidatorId, addressType)] != null))
            {
                var cachedRequest = (ValidateCache)Context.Current.Session[AddressValidatorCacheKey(ValidatorId, addressType)];

                if (address.country == cachedRequest.Address.country &&
                    address.region == cachedRequest.Address.region &&
                    address.postalCode == cachedRequest.Address.postalCode &&
                    address.line1 == cachedRequest.Address.line1 &&
                    address.line2 == cachedRequest.Address.line2 &&
                    address.line3 == cachedRequest.Address.line3)
                {
                    validateResult = cachedRequest.ValidateResult;
                }
            }

            return validateResult;
        }

        private void CacheRateRequest(AddressLocationInfo address, AddressType addressType, AddressResolutionModel validateResult)
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
            public AddressResolutionModel ValidateResult;
        }

        #endregion

        #endregion

        #region public static functions

        public static AddressLocationInfo GetBillingAddress(Order order)
        {
            return new AddressLocationInfo
            {
                line1 = order.CustomerAddress,
                line2 = order.CustomerAddress2,
                city = order.CustomerCity,
                region = order.CustomerRegion,
                postalCode = order.CustomerZip,
                country = order.CustomerCountryCode
            };
        }

        public static AddressLocationInfo GetDeliveryAddress(Order order)
        {
            return new AddressLocationInfo
            {
                line1 = order.DeliveryAddress,
                line2 = order.DeliveryAddress2,
                city = order.DeliveryCity,
                region = order.DeliveryRegion,
                postalCode = order.DeliveryZip,
                country = order.DeliveryCountryCode
            };
        }

        public static AddressLocationInfo GetOriginAddress(AvalaraTaxProvider taxProvider)
        {
            return new AddressLocationInfo
            {
                line1 = taxProvider.StreetAddress,
                line2 = taxProvider.StreetAddress2,
                city = taxProvider.City,
                region = taxProvider.Region,
                postalCode = taxProvider.PostalCode,
                country = taxProvider.Country
            };
        }

        #endregion

        #region SaveAvaTaxLog

        private string GetErrorMessage(AddressResolutionModel validateResult)
        {
            var errMessages = new StringBuilder();

            if (validateResult.messages?.Count > 0)
            {
                foreach (var message in validateResult.messages)
                {
                    errMessages.AppendLine($"Details: {message.details}");
                    errMessages.AppendLine($"RefersTo: {message.refersTo}");
                    errMessages.AppendLine($"Severity: {message.severity}");
                    errMessages.AppendLine($"Source: {message.source}");
                    errMessages.AppendLine($"Summary: {message.summary}");
                }
            }

            return errMessages.ToString();
        }

        private void SaveAvaTaxLog(AddressResolutionModel validateRequest)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(AddressResolutionModel));
                var writer = new StringWriter();
                serializer.Serialize(writer, validateRequest);

                SaveLog(writer.ToString());
            }
            catch (Exception err)
            {
                SaveLog(err.ToString());
            }
        }
        #endregion
    }
}
