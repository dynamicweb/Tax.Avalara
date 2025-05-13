using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.Prices;
using Dynamicweb.Ecommerce.Products;
using Dynamicweb.Ecommerce.Products.Taxes;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionResponse;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.VoidTransaction;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Notifications;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Extensibility.Editors;
using Dynamicweb.Extensibility.Notifications;
using Dynamicweb.Security.UserManagement;
using Dynamicweb.Security.UserManagement.Common.CustomFields;
using Dynamicweb.Security.UserManagement.Common.SystemFields;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider
{
    /// <summary>
    /// Avalara tax provider
    /// </summary>
    [AddInName("Avalara tax provider")]
    public class AvalaraTaxProvider : TaxProvider, IParameterOptions
    {
        /// <summary>
        /// Gets the names for ItemCode and TaxCode field.
        /// </summary>
        internal const string ItemCodeFieldName = "ItemCode";
        internal const string TaxCodeFieldName = "TaxCode";
        internal const string ExemptionNumberFieldName = "ExemptionNumber";
        internal const string EntityUseCodeFieldName = "EntityUseCode";
        public const string BeforeTaxCalculation = "Ecom7CartBeforeTaxCalculation";
        public const string BeforeTaxCommit = "Ecom7CartBeforeTaxCommit";
        public const string OnGetCustomerCode = "Ecom7CartAvalaraOnGetCustomerCode";

        private OrderDebuggingInfoService _orderDebuggingInfoService = new OrderDebuggingInfoService();

        [AddInParameter("Account Id"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string AccountId { get; set; }

        [AddInParameter("License Key"), AddInParameterEditor(typeof(TextParameterEditor), "size=80; password=true")]
        public string LicenseKey { get; set; }

        [AddInParameter("Company Code"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string CompanyCode { get; set; }

        [AddInParameter("Origination Street Address"), AddInParameterEditor(typeof(TextParameterEditor), "")]
        public string StreetAddress { get; set; }

        [AddInParameter("Origination Street Address 2"), AddInParameterEditor(typeof(TextParameterEditor), "")]
        public string StreetAddress2 { get; set; }

        [AddInParameter("Origination City"), AddInParameterEditor(typeof(TextParameterEditor), "")]
        public string City { get; set; }

        [AddInParameter("Origination State"), AddInParameterEditor(typeof(DropDownParameterEditor), "SortBy=Value")]
        public string Region { get; set; }

        [AddInParameter("Origination Zip Code"), AddInParameterEditor(typeof(TextParameterEditor), "")]
        public string PostalCode { get; set; }

        [AddInParameter("Tax Code for Shipping"), AddInParameterEditor(typeof(TextParameterEditor), "")]
        public string TaxCodeShipping { get; set; } = "FR020100";

        [AddInParameter("Get customer code from"), AddInParameterEditor(typeof(DropDownParameterEditor), "none=false; SortBy=Value")]
        public string GetCustomerCodeFrom { get; set; } = nameof(CustomerCodeSource.OrderCustomerAccessUserId);

        [AddInParameter("Enable Commit"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
        public bool EnableCommit { get; set; } = true;

        [AddInParameter("Don't use in product catalog"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
        public bool DontUseInProductCatalog { get; set; }

        [AddInParameter("Don’t calculate taxes if Exemption number is set"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
        public bool DontUseIfExemptionNumberIsSet { get; set; }

        [AddInParameter("Debug"), AddInParameterEditor(typeof(YesNoParameterEditor), ""), AddInDescription("Create a log of the request and response from Avalara")]
        public bool Debug { get; set; }

        [AddInParameter("Test mode"), AddInParameterEditor(typeof(YesNoParameterEditor), "infoText=Set to use sandbox (test mode) for the API requests. Uncheck when ready for production.")]
        public bool TestMode { get; set; }

        public string Country = "US";

        private AvalaraService GetService() => new()
        {
            AccountId = AccountId,
            LicenseKey = LicenseKey,
            TestMode = TestMode,
            DebugLog = Debug
        };

        /// <summary>
        /// Adds order lines to order
        /// </summary>
        /// <param name="order"></param>
        public override void AddTaxOrderLinesToOrder(Order order)
        {
            var notificationArgs = new BeforeTaxCalculationArgs();
            notificationArgs.Order = order;
            NotificationManager.Notify(BeforeTaxCalculation, notificationArgs);

            if (notificationArgs.Cancel)
                return;

            if (!IsTaxableOrder(order))
                return;

            try
            {
                CreateTransactionResponse taxResult = GetService().CreateCalculateTransaction(order, this);

                if (taxResult.Messages?.Any() is true)
                    order.TaxProviderErrors.Add(GetErrorMessage(taxResult));
                else
                    GetOrderLinesFromTaxResult(order, taxResult);
            }
            catch (Exception err)
            {
                order.TaxProviderErrors.Add(err.Message);
                SaveLog(err.ToString());
            }
        }

        /// <summary>
        /// Commits taxes for order
        /// </summary>
        /// <param name="order">Order instance</param>
        public override void CommitTaxes(Order order)
        {
            var notificationArgs = new BeforeTaxCommitArgs();
            notificationArgs.Order = order;
            NotificationManager.Notify(BeforeTaxCommit, notificationArgs);

            if (notificationArgs.Cancel)
                return;

            if (!order.Complete || !IsTaxableOrder(order))
                return;

            try
            {
                CreateTransactionResponse taxResult = GetService().CreateCommitTransaction(order, this);

                string message = $"Commited with ResultCode ({taxResult.Code})";
                if (taxResult.Messages is null)
                {
                    if (EnableCommit)
                    {
                        message += $"; TransactionId #{taxResult.Code}";
                        order.TaxTransactionNumber = taxResult.Code;
                        Services.Orders.Save(order);
                    }
                    else
                        message += "; Commit is disabled";
                }
                else
                    message += GetErrorMessage(taxResult);

                new OrderDebuggingInfoService().Save(order, message, "AvaTax");
            }
            catch (Exception err)
            {
                SaveLog(err.ToString());
            }
        }

        #region get taxes

        private void GetOrderLinesFromTaxResult(Order order, CreateTransactionResponse taxResult)
        {
            var newOrderLines = new OrderLineCollection(order);

            if (taxResult.TotalTax != 0)
            {
                foreach (TransactionLine taxLine in taxResult.Lines)
                {
                    var taxDetailNamesAndCount = new Dictionary<string, int>();
                    foreach (TransactionLineDetail taxDetail in taxLine.Details)
                    {
                        if (!taxDetailNamesAndCount.ContainsKey(taxDetail.TaxName))
                            taxDetailNamesAndCount.Add(taxDetail.TaxName, 1);
                        else
                            taxDetailNamesAndCount[taxDetail.TaxName] += 1;
                    }

                    foreach (TransactionLineDetail taxDetail in taxLine.Details)
                    {
                        if (taxDetail.Tax == 0)
                            continue;

                        var taxOrderLine = new OrderLine(order);
                        taxOrderLine.Date = DateTime.Now;
                        taxOrderLine.Modified = DateTime.Now;

                        taxOrderLine.ProductNumber = string.Format("Tax Id# {0}", taxResult.Id);
                        string taxName = taxDetail.TaxName;
                        if (taxDetailNamesAndCount.ContainsKey(taxDetail.TaxName) && taxDetailNamesAndCount[taxDetail.TaxName] > 1 && !string.IsNullOrEmpty(taxDetail.JurisName))
                            taxName += $" ({taxDetail.JurisName})";

                        taxOrderLine.ProductName = taxName;
                        taxOrderLine.ProductVariantText = Name;
                        taxOrderLine.Order = order;
                        taxOrderLine.OrderId = order.Id;
                        taxOrderLine.Quantity = 1;

                        // Info: Set price - should be before setting Type
                        Services.OrderLines.SetUnitPrice(taxOrderLine, Convert.ToDouble(taxDetail.Tax), false);
                        if (!order.Calculate)
                            Services.OrderLines.SetUnitPrice(taxOrderLine, taxOrderLine.UnitPrice, true);

                        taxOrderLine.OrderLineType = OrderLineType.Tax;
                        taxOrderLine.ParentLineId = taxLine.LineNumber;

                        newOrderLines.Add(taxOrderLine);
                    }
                }
            }

            foreach (var orderline in newOrderLines)
                order.OrderLines.Add(orderline, false);
        }

        private bool IsTaxableOrder(Order order)
        {
            bool hasTaxableOrderLines = order.OrderLines.Any(ol => (IsTaxableType(ol) || ol.HasType(OrderLineType.PointProduct)) && ol.Product is not null);

            if (hasTaxableOrderLines && DontUseIfExemptionNumberIsSet && order.CustomerAccessUserId != 0)
            {
                User customer = UserManagementServices.Users.GetUserById(order.CustomerAccessUserId);
                SystemFieldValue exemptionNumberField = customer.SystemFieldValues.FirstOrDefault(fieldValue => fieldValue.SystemField.Name.Equals(ExemptionNumberFieldName, StringComparison.Ordinal));

                if (!string.IsNullOrWhiteSpace(exemptionNumberField?.Value?.ToString() ?? ""))
                    hasTaxableOrderLines = false;
            }

            return hasTaxableOrderLines;
        }

        #endregion

        public IEnumerable<ParameterOption> GetParameterOptions(string parameterName)
        {
            try
            {
                switch (parameterName)
                {
                    case "Origination State":
                        return
                        [
                            new("Alabama", "AL"),
                            new("Alaska", "AK"),
                            new("Arizona", "AZ"),
                            new("Arkansas", "AR"),
                            new("California", "CA"),
                            new("Colorado", "CO"),
                            new("Connecticut", "CT"),
                            new("Delaware", "DE"),
                            new("District of Columbia", "DC"),
                            new("Florida", "FL"),
                            new("Georgia", "GA"),
                            new("Hawaii", "HI"),
                            new("Idaho", "ID"),
                            new("Illinois", "IL"),
                            new("Indiana", "IN"),
                            new("Iowa", "IA"),
                            new("Kansas", "KS"),
                            new("Kentucky", "KY"),
                            new("Louisiana", "LA"),
                            new("Maine", "ME"),
                            new("Maryland", "MD"),
                            new("Massachusetts", "MA"),
                            new("Michigan", "MI"),
                            new("Minnesota", "MN"),
                            new("Mississippi", "MS"),
                            new("Missouri", "MO"),
                            new("Montana", "MT"),
                            new("Nebraska", "NE"),
                            new("Nevada", "NV"),
                            new("New Hampshire", "NH"),
                            new("New Jersey", "NJ"),
                            new("New Mexico", "NM"),
                            new("New York", "NY"),
                            new("North Carolina", "NC"),
                            new("North Dakota", "ND"),
                            new("Ohio", "OH"),
                            new("Oklahoma", "OK"),
                            new("Oregon", "OR"),
                            new("Pennsylvania", "PA"),
                            new("Rhode Island", "RI"),
                            new("South Carolina", "SC"),
                            new("South Dakota", "SD"),
                            new("Tennessee", "TN"),
                            new("Texas", "TX"),
                            new("Utah", "UT"),
                            new("Vermont", "VT"),
                            new("Virginia", "VA"),
                            new("Washington", "WA"),
                            new("West Virginia", "WV"),
                            new("Wisconsin", "WI"),
                            new("Wyoming", "WY")
                        ];
                    case "Boundary level":
                        return
                        [
                            new("Address", "Address"),
                            new("Zip9", "Zip9"),
                            new("Zip5", "Zip5")
                        ];
                    case "Get customer code from":
                        return
                        [
                            new("Access User ID", CustomerCodeSource.OrderCustomerAccessUserId),
                            new("Customer Number", CustomerCodeSource.OrderCustomerNumber),
                            new("External ID", CustomerCodeSource.AccessUserExternalId)
                        ];

                    default:
                        throw new ArgumentException(string.Format("Unknown dropdown name: '{0}'", parameterName));
                }
            }
            catch (Exception ex)
            {
                SaveLog($"Unhandled exception with message: {ex.Message}");
                return [];
            }
        }

        #region CancelTaxRequest

        /// <summary>
        /// Cancels taxes for order
        /// </summary>
        /// <param name="order">Order instance</param>
        public override void CancelTaxes(Order order)
        {
            if (string.IsNullOrEmpty(order.TaxTransactionNumber) || !EnableCommit)
                return;
            try
            {
                VoidTransactionResponse cancelTaxResult = GetService().VoidTransaction(CompanyCode, order.TaxTransactionNumber);
                if (cancelTaxResult is null)
                    throw new ArgumentNullException("The cancel response was not deserialized correctly.");

                if (cancelTaxResult.Status != "Cancelled")
                {
                    var stringBuilder = new StringBuilder();
                    stringBuilder.Append("Error cancelling AvaTax transaction.");

                    if (cancelTaxResult.Messages?.Any() is true)
                    {
                        stringBuilder.Append(" Message(s) from Gateway:");

                        foreach (var message in cancelTaxResult.Messages)
                        {
                            stringBuilder.Append($" Details: {message.Details}");
                            stringBuilder.Append($" RefersTo: {message.RefersTo}");
                            stringBuilder.Append($" Severity: {message.Severity}");
                            stringBuilder.Append($" Source: {message.Source}");
                            stringBuilder.Append($" Summary: {message.Summary}");
                        }
                    }

                    _orderDebuggingInfoService.Save(order, stringBuilder.ToString(), "AvaTax");
                }
                else
                {
                    foreach (OrderLine orderLine in order.OrderLines)
                    {
                        if (orderLine.OrderLineType is OrderLineType.Tax && string.Equals(orderLine.ProductVariantText, Name, StringComparison.OrdinalIgnoreCase))
                        {
                            orderLine.ProductName += " - CANCELLED";
                            Services.OrderLines.Save(orderLine);
                        }
                    }

                    _orderDebuggingInfoService.Save(order, "Transaction was cancelled", "AvaTax");
                }

            }
            catch (Exception ex)
            {
                string message = $"Error cancelling transaction. Message: {ex.Message}";
                _orderDebuggingInfoService.Save(order, message, "AvaTax");
            }
        }

        #endregion

        #region HandleProductReturns

        /// <summary>
        /// Ajusts taxes for order
        /// </summary>
        /// <param name="order">Order instance</param>
        public override void AdjustTaxes(Order order)
        {
            if (!order.Complete)
                return;

            try
            {
                CreateTransactionResponse taxResult = GetService().CreateAdjustTransaction(order, this);

                var message = $"Taxes were adjusted with ResultCode ({taxResult.Code})";

                if (taxResult.Messages is null)
                {
                    if (EnableCommit)
                    {
                        message += $"; TransactionId #{taxResult.Code}";
                        order.TaxTransactionNumber = taxResult.Code;
                        Services.Orders.Save(order);
                    }
                    else
                        message += "; Commit is disabled";
                }
                else
                    message += GetErrorMessage(taxResult);

                _orderDebuggingInfoService.Save(order, message, "AvaTax");
            }
            catch (Exception err)
            {
                SaveLog(err.Message);
            }
        }

        /// <summary>
        /// Handles product returns
        /// </summary>
        /// <param name="order">New order</param>
        /// <param name="originalOrder">Original order</param>
        public override void HandleProductReturns(Order order, Order originalOrder)
        {
            if (!order.Complete || !IsTaxableOrder(order))
                return;

            try
            {
                CreateTransactionResponse taxResult = GetService().CreateProductReturnsTransaction(order, this, originalOrder);

                string message = $"Handle product returns with ResultCode ({taxResult.Code})";
                if (taxResult.Messages is null)
                {
                    GetOrderLinesFromTaxResult(order, taxResult);
                    if (EnableCommit)
                    {
                        message += $"; TransactionId #{taxResult.Code}";
                        order.TaxTransactionNumber = taxResult.Code;
                        Services.Orders.Save(order);
                    }
                    else
                        message += "; Commit is disabled";
                }
                else
                    message += GetErrorMessage(taxResult);

                _orderDebuggingInfoService.Save(order, message, "AvaTax");
            }
            catch (Exception err)
            {
                SaveLog(err.ToString());
            }
        }

        #endregion

        #region SaveAvaTaxLog

        private string GetErrorMessage(CreateTransactionResponse taxResult)
        {
            var errMessages = new StringBuilder();
            if (taxResult.Messages?.Any() is true)
            {
                foreach (AvaTaxMessage message in taxResult.Messages)
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

        #endregion

        #region VerifyCustomFields

        /// <summary>
        /// Gets the lock object that is used to synchronize access to the queue from multiple threads.
        /// </summary>
        private static object syncLock = new object();

        /// <summary>
        /// Verifies custom fields
        /// </summary>
        public static void VerifyCustomFields()
        {
            lock (syncLock)
            {
                var itemCodeColl = ProductField.FindProductFieldsBySystemName(ItemCodeFieldName);
                var taxCodeColl = ProductField.FindProductFieldsBySystemName(TaxCodeFieldName);

                if (itemCodeColl.Count() == 0)
                {
                    var productField = new ProductField();
                    productField.Name = ItemCodeFieldName;
                    productField.SystemName = ItemCodeFieldName;
                    productField.TemplateName = ItemCodeFieldName;
                    productField.TypeId = 1;
                    productField.TypeName = "Text";
                    productField.Save(ItemCodeFieldName);
                }

                if (taxCodeColl.Count() == 0)
                {
                    var productField = new ProductField();
                    productField.Name = TaxCodeFieldName;
                    productField.SystemName = TaxCodeFieldName;
                    productField.TemplateName = TaxCodeFieldName;
                    productField.TypeId = 15;
                    productField.TypeName = "List";
                    productField.ListPresentationType = FieldListPresentationType.DropDownList;
                    productField.Save(TaxCodeFieldName);
                }

                string tableName = "AccessUser";
                var systemFields = SystemField.GetSystemFields(tableName);

                // ExemptionNumber
                {
                    SystemField exemptionNumberField = new SystemField(ExemptionNumberFieldName, tableName, Types.Text, ExemptionNumberFieldName);
                    if (!systemFields.ContainsSystemField(exemptionNumberField))
                        exemptionNumberField.Save();
                }

                // EntityUseCode
                {
                    SystemField entityUseCodeField = new SystemField(EntityUseCodeFieldName, tableName, Types.DropDown, EntityUseCodeFieldName);
                    if (!systemFields.ContainsSystemField(entityUseCodeField))
                    {
                        var options = new CustomFieldOptions();
                        options.DataType = Types.Text;
                        options.Add(new KeyValuePair<string, object>("", ""));
                        options.Add(new KeyValuePair<string, object>("Federal government", "A"));
                        options.Add(new KeyValuePair<string, object>("State government", "B"));
                        options.Add(new KeyValuePair<string, object>("Tribe / Status Indian / Indian Band", "C"));
                        options.Add(new KeyValuePair<string, object>("Foreign diplomat", "D"));
                        options.Add(new KeyValuePair<string, object>("Charitable or benevolent org", "E"));
                        options.Add(new KeyValuePair<string, object>("Religious org", "F"));
                        options.Add(new KeyValuePair<string, object>("Education org", "M"));
                        options.Add(new KeyValuePair<string, object>("Resale", "G"));
                        options.Add(new KeyValuePair<string, object>("Commercial agricultural production", "H"));
                        options.Add(new KeyValuePair<string, object>("Industrial production / manufacturer", "I"));
                        options.Add(new KeyValuePair<string, object>("Direct pay permit", "J"));
                        options.Add(new KeyValuePair<string, object>("Direct mail", "K"));
                        options.Add(new KeyValuePair<string, object>("Other (requires Exempt Reason Desc)", "L"));
                        options.Add(new KeyValuePair<string, object>("Local government", "N"));
                        entityUseCodeField.Options = options;
                        entityUseCodeField.Save();
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Verify that all needed fields for Avalara are exist and create them if not
        /// </summary>
        public override void OnAfterSettingsSaved() => VerifyCustomFields();

        internal bool IsTaxableTypeInternal(OrderLine orderLine) => IsTaxableType(orderLine);

        internal PriceInfo GetProductPriceWithoutDiscountsInternal(OrderLine orderLine) => GetProductPriceWithoutDiscounts(orderLine);

        #region AddTaxesToProducts

        /// <summary>
        /// Adds taxes to products collection
        /// </summary>
        /// <param name="products"></param>
        public override void AddTaxesToProducts(IEnumerable<Product> products)
        {
            try
            {
                if (DontUseInProductCatalog)
                    return;

                Order order = PrepareOrder(products);
                if (order is null || !IsTaxableOrder(order))
                    return;

                CreateTransactionResponse taxResult = GetService().CreateCalculateTransaction(order, this);

                if (taxResult.Messages is null)
                {
                    if (taxResult.TotalTax > 0)
                    {
                        foreach (var taxLine in taxResult.Lines)
                        {
                            foreach (var taxDetail in taxLine.Details)
                            {
                                OrderLine orderLine = order.OrderLines.FirstOrDefault(line => line.Id.Equals(taxLine.LineNumber, StringComparison.Ordinal));
                                if (orderLine is not null)
                                    products.FirstOrDefault(obj => obj.Id == orderLine.ProductId)?.TaxCollection.Add(GetTax(orderLine.Product, taxDetail));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SaveLog(ex.ToString());
            }
        }

        private Tax GetTax(Product product, TransactionLineDetail taxDetail)
        {
            var tax = new Tax();
            tax.Name = taxDetail.TaxName;
            tax.Product = product;

            tax.Amount = new PriceRaw((double)taxDetail.Tax, Services.Currencies.GetDefaultCurrency());
            tax.CalculateVat = true; //AddVAT;

            return tax;
        }

        private Order PrepareOrder(IEnumerable<Product> products)
        {
            Order order = null;
            Order currentCart = Common.Context.Cart;

            if (!string.IsNullOrEmpty(currentCart?.CustomerZip) || !string.IsNullOrEmpty(currentCart?.DeliveryZip))
            {
                order = new Order(currentCart.Currency, currentCart.VatCountry, currentCart.Language);
                order.Id = "ProductTaxesC";
                order.Complete = false;
                order.IsCart = true;
                order.CurrencyCode = currentCart.CurrencyCode;
                order.CustomerAccessUserId = currentCart.CustomerAccessUserId;
                order.DeliveryAddress = currentCart.DeliveryAddress;
                order.DeliveryAddress2 = currentCart.DeliveryAddress2;
                order.DeliveryCity = currentCart.DeliveryCity;
                order.DeliveryRegion = currentCart.DeliveryRegion;
                order.DeliveryZip = currentCart.DeliveryZip;
                order.DeliveryCountryCode = currentCart.DeliveryCountryCode;
                order.CustomerAddress = currentCart.CustomerAddress;
                order.CustomerAddress2 = currentCart.CustomerAddress2;
                order.CustomerCity = currentCart.CustomerCity;
                order.CustomerRegion = currentCart.CustomerRegion;
                order.CustomerZip = currentCart.CustomerZip;
                order.CustomerCountryCode = currentCart.CustomerCountryCode;
            }
            else
            {
                User user = UserContext.Current.User;
                if (user is not null)
                {
                    order = new Order(Common.Context.Currency, Common.Context.Country, Common.Context.Language);
                    order.Id = "ProductTaxesU";
                    order.Complete = false;
                    order.IsCart = true;
                    order.CurrencyCode = Common.Context.Currency.Code;
                    order.CustomerCountryCode = Common.Context.Country.Code2;
                    order.CustomerAccessUserId = user.ID;
                    order.CustomerAccessUserUserName = user.UserName;

                    UserAddress defaultAddress = UserManagementServices.UserAddresses.GetDefaultAddressByUserId(user.ID);
                    if (defaultAddress is not null)
                    {
                        order.CustomerAddress = defaultAddress.Address;
                        order.CustomerAddress2 = defaultAddress.Address2;
                        order.CustomerZip = defaultAddress.Zip;
                        order.CustomerRegion = defaultAddress.State;
                        order.CustomerCity = defaultAddress.City;
                    }
                    else
                    {
                        order.CustomerAddress = user.Address;
                        order.CustomerAddress2 = user.Address2;
                        order.CustomerZip = user.Zip;
                        order.CustomerRegion = user.State;
                        order.CustomerCity = user.City;
                    }
                }
            }

            PrepareOrderDetails(order, products);

            return order;
        }

        private void PrepareOrderDetails(Order order, IEnumerable<Product> products)
        {
            if (order is null)
                return;

            for (int i = 0; i < products.Count(); i++)
            {
                Product product = products.ElementAt(i);
                OrderLine orderLine = Services.OrderLines.Create(order, product, 1d, null, null);
                orderLine.Id = i.ToString();
            }
        }

        #endregion
    }
}
