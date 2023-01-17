using Avalara.AvaTax.RestClient;
using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.Prices;
using Dynamicweb.Ecommerce.Products;
using Dynamicweb.Ecommerce.Products.Taxes;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model;
using Dynamicweb.Extensibility;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Extensibility.Editors;
using Dynamicweb.Extensibility.Notifications;
using Dynamicweb.Security.UserManagement;
using Dynamicweb.Security.UserManagement.Common.CustomFields;
using Dynamicweb.Security.UserManagement.Common.SystemFields;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider
{
    /// <summary>
    /// Avalara tax provider
    /// </summary>
    [AddInName("Avalara tax provider")]
    public class AvalaraTaxProvider : TaxProvider, IDropDownOptions
    {
        /// <summary>
        /// Gets the names for ItemCode and TaxCode field.
        /// </summary>
        private const string ItemCodeFieldName = "ItemCode";
        private const string TaxCodeFieldName = "TaxCode";
        private const string ExemptionNumberFieldName = "ExemptionNumber";
        private const string EntityUseCodeFieldName = "EntityUseCode";
        public const string BeforeTaxCalculation = "Ecom7CartBeforeTaxCalculation";
        public const string BeforeTaxCommit = "Ecom7CartBeforeTaxCommit";
        public const string OnGetCustomerCode = "Ecom7CartAvalaraOnGetCustomerCode";
        private OrderDebuggingInfoService _orderDebuggingInfoService = new OrderDebuggingInfoService();

        private enum TransactionType
        {
            Calculate,
            Commit,
            Cancel,
            Adjust,
            ProductReturns
        }

        private enum CustomerCodeSource
        {
            OrderCustomerAccessUserId,
            OrderCustomerNumber,
            AccessUserExternalId
        }

        private Order originalOrder = null;

        #region Fields

        [AddInParameter("Account"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string Account { get; set; }

        [AddInParameter("License"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string License { get; set; }

        [AddInParameter("Company Code"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string CompanyCode { get; set; }

        [AddInParameter("Tax Service Url"), AddInParameterEditor(typeof(TextParameterEditor), "size=80")]
        public string TaxServiceUrl { get; set; }

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

        public string Country = "US";

        [AddInParameter("Tax Code for Shipping"), AddInParameterEditor(typeof(TextParameterEditor), "")]
        public string TaxCodeShipping { get; set; }

        [AddInParameter("Boundary level"), AddInParameterEditor(typeof(DropDownParameterEditor), "none=false; SortBy=Value")]
        public string BoundaryLevel { get; set; }

        [AddInParameter("Get customer code from"), AddInParameterEditor(typeof(DropDownParameterEditor), "none=false; SortBy=Value")]
        public string GetCustomerCodeFrom { get; set; }

        [AddInParameter("Enable Commit"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
        public bool EnableCommit { get; set; }

        [AddInParameter("Don't use in product catalog"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
        public bool DontUseInProductCatalog { get; set; }

        [AddInParameter("Don’t calculate taxes if {Exemption number} is set"), AddInParameterEditor(typeof(YesNoParameterEditor), "")]
        public Boolean DontUseIfExemptionNumberIsSet { get; set; }

        [AddInParameter("Debug"), AddInParameterEditor(typeof(YesNoParameterEditor), ""), AddInDescription("Create a log of the request and response from UPS")]
        public bool Debug { get; set; }

        #endregion

        /// <summary>
        /// Default constructor
        /// </summary>
        public AvalaraTaxProvider()
        {
            if (Context.Current == null || Context.Current.Request.Form.Count == 0)
            {
                EnableCommit = true;
                BoundaryLevel = "Zip9";
                TaxCodeShipping = "FR020100"; // Avalara System TaxCode for SHIPPING
            }
            GetCustomerCodeFrom = nameof(CustomerCodeSource.OrderCustomerAccessUserId);
        }

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
            {
                return;
            }

            if (!IsTaxableOrder(order))
            {
                return;
            }

            try
            {
                var taxResult = GetTaxes(order, TransactionType.Calculate);

                if (Debug)
                {
                    SaveAvaTaxLog(taxResult);
                }

                if (taxResult.messages?.Count > 0)
                {
                    order.TaxProviderErrors.Add(GetErrorMessage(taxResult));
                }
                else
                {
                    GetOrderLinesFromTaxResult(order, taxResult);
                }
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
            {
                return;
            }

            if (!order.Complete || !IsTaxableOrder(order))
            {
                return;
            }

            try
            {
                var taxResult = GetTaxes(order, TransactionType.Commit);

                if (Debug)
                {
                    SaveAvaTaxLog(taxResult);
                }
                string message = string.Format("Commited with ResultCode ({0})", taxResult.code);
                if (taxResult.messages is null)
                {
                    if (EnableCommit)
                    {
                        message += string.Format("; TransactionId #{0}", taxResult.code);
                        order.TaxTransactionNumber = taxResult.code;
                        Services.Orders.Save(order);
                    }
                    else
                    {
                        message += string.Format("; Commit is disabled");
                    }
                }
                else
                {
                    message += GetErrorMessage(taxResult);
                }
                new OrderDebuggingInfoService().Save(order, message, "AvaTax");
            }
            catch (Exception err)
            {
                SaveLog(err.ToString());
            }
        }

        #region get taxes

        private TransactionModel GetTaxes(Order order, TransactionType transactionType)
        {
            var taxRequest = PrepareTaxRequest(order, transactionType).Create();

            if (Debug)
            {
                SaveAvaTaxLog(taxRequest);
            }

            return taxRequest;
        }

        private AvaTaxClient PrepareTaxSvc()
        {
            return new AvaTaxClient("Dynamicweb AvaTax", "1.0", "Dynamicweb 9.0", new Uri(TaxServiceUrl)).WithSecurity(Account, License);
        }
        private T PostToAvalara<T>(string method, string jsonObject)
        {
            string url = $"{TaxServiceUrl}/api/v2/";
            using (var client = new HttpClient())
            {
                string authenticationScheme = "Basic";
                string authenticationParameter = Convert.ToBase64String(Encoding.Default.GetBytes($"{Account}:{License}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(authenticationScheme, authenticationParameter);
                var content = new StringContent(jsonObject);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                using (var response = client.PostAsync(url + method, content).GetAwaiter().GetResult())
                {
                    string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return JsonSerializer.Deserialize<T>(responseText);
                }
            }
        }

        /// <remarks>
        /// "FR020100" - Avalara System TaxCode for SHIPPING
        /// </remarks>
        private TransactionBuilder PrepareTaxRequest(Order order, TransactionType transactionType)
        {
            TransactionBuilder result;
            if (transactionType == TransactionType.Commit)
            {
                result = new TransactionBuilder(PrepareTaxSvc(), CompanyCode, DocumentType.SalesInvoice, GetCustomerCode(order));
                result.WithCommit();
                result.WithDate(order.Date);
                result.WithReferenceCode(order.Id);
            }
            else if (transactionType == TransactionType.Calculate)
            {
                result = new TransactionBuilder(PrepareTaxSvc(), CompanyCode, DocumentType.SalesOrder, GetCustomerCode(order));
                result.WithDate(DateTime.Now);
                result.WithReferenceCode(order.Id);
            }
            else if (transactionType == TransactionType.Adjust)
            {
                result = new TransactionBuilder(PrepareTaxSvc(), CompanyCode, DocumentType.SalesInvoice, GetCustomerCode(order));
                if (!string.IsNullOrEmpty(order.TaxTransactionNumber) && EnableCommit)
                {
                    result.WithCommit();
                }
                result.WithDate(DateTime.Now);
                result.WithTaxOverride(TaxOverrideType.TaxDate, "Adjust", 0, order.Date);
                result.WithReferenceCode(order.Id);
            }
            else if (transactionType == TransactionType.ProductReturns)
            {
                result = new TransactionBuilder(PrepareTaxSvc(), CompanyCode, DocumentType.ReturnInvoice, GetCustomerCode(order));
                if (!string.IsNullOrEmpty(originalOrder.TaxTransactionNumber) && EnableCommit)
                {
                    result.WithCommit();
                }
                result.WithDate(order.Date);
                result.WithReferenceCode(originalOrder.Id);
                result.WithTaxOverride(TaxOverrideType.TaxDate, "Return", 0, originalOrder.Date);
            }
            else
            {
                throw new Exception(string.Format("Unknown transaction type: {0}", transactionType));
            }

            result.WithCurrencyCode(order.CurrencyCode);

            if (order.CustomerAccessUserId != 0)
            {
                var customer = User.GetUserByID(order.CustomerAccessUserId);

                foreach (var fieldValue in customer.SystemFieldValues)
                {
                    if (fieldValue.SystemField.Name == ExemptionNumberFieldName && fieldValue.Value != null)
                    {
                        result.WithExemptionNumber(fieldValue.Value.ToString());
                    }
                    else if (fieldValue.SystemField.Name == EntityUseCodeFieldName && fieldValue.Value != null)
                    {
                        result.WithUsageType(fieldValue.Value.ToString());
                    }
                }
            }

            AddressLocationInfo originAddress = AvalaraAddressValidatorProvider.GetOriginAddress(this);
            result.WithAddress(TransactionAddressType.ShipFrom, originAddress.line1, originAddress.line2, null, originAddress.city, originAddress.region, originAddress.postalCode, originAddress.country);

            var destinationAddress = new AddressLocationInfo();
            if (!string.IsNullOrEmpty(order.DeliveryZip))
            {
                destinationAddress = AvalaraAddressValidatorProvider.GetDeliveryAddress(order);
            }
            else
            {
                destinationAddress = AvalaraAddressValidatorProvider.GetBillingAddress(order);
            }
            if (string.IsNullOrEmpty(destinationAddress.postalCode))
            {
                throw new Exception("Make sure that the address is provided with a zip code.");
            }
            result.WithAddress(TransactionAddressType.ShipTo, destinationAddress.line1, destinationAddress.line2, null, destinationAddress.city, destinationAddress.region, destinationAddress.postalCode, destinationAddress.country);

            int index = 0;
            decimal orderDiscount = 0M;
            CreateTransactionModel transactionModel = result.GetCreateTransactionModel();
            var priceContext = new PriceContext(order.Currency, order.VatCountry);

            foreach (var orderLine in order.OrderLines)
            {
                if (IsTaxableType(orderLine) || orderLine.HasType(OrderLineType.PointProduct))
                {
                    if (orderLine.Product != null)
                    {
                        var line = GetTaxLine(order, orderLine, index++, destinationAddress, originAddress);
                        transactionModel.lines.Add(line);
                        if (orderLine.HasType(OrderLineType.PointProduct))
                        {
                            orderDiscount += (-Convert.ToDecimal(orderLine.Product.GetPrice(priceContext).PriceWithoutVAT));
                        }
                    }
                }
                else if (orderLine.HasType(OrderLineType.Discount) && string.IsNullOrEmpty(orderLine.GiftCardCode))
                {
                    orderDiscount += Convert.ToDecimal(orderLine.Price.PriceWithoutVAT);
                }
            }

            orderDiscount = Math.Abs(orderDiscount);
            if (orderDiscount > 0M)
            {
                foreach (LineItemModel line in transactionModel.lines)
                {
                    if (line.taxCode != TaxCodeShipping)
                    {
                        line.discounted = true;
                    }
                }
                result.WithDiscountAmount(orderDiscount);
            }
            var shippingLine = GetShippingTaxLine(order, destinationAddress, originAddress);

            if (shippingLine.amount > 0)
            {
                transactionModel.lines.Add(shippingLine);
            }

            return result;
        }

        private string GetCustomerCode(Order order)
        {
            var notificationArgs = new OnGetCustomerCodeArgs { Order = order };
            NotificationManager.Notify(OnGetCustomerCode, notificationArgs);
            if (!string.IsNullOrEmpty(notificationArgs.CustomerCode))
            {
                return notificationArgs.CustomerCode;
            }

            if (!string.IsNullOrEmpty(GetCustomerCodeFrom))
            {
                switch (GetCustomerCodeFrom)
                {
                    case nameof(CustomerCodeSource.OrderCustomerAccessUserId):
                        return order.CustomerAccessUserId.ToString();
                    case nameof(CustomerCodeSource.OrderCustomerNumber):
                        return order.CustomerNumber;
                    case nameof(CustomerCodeSource.AccessUserExternalId):
                        if (order.CustomerAccessUserId > 0)
                        {
                            var customer = User.GetUserByID(order.CustomerAccessUserId);
                            return customer != null ? customer.ExternalID : string.Empty;
                        }
                        return string.Empty;
                    default:
                        throw new Exception("Unsupported option: " + GetCustomerCodeFrom);
                }
            }
            return order.CustomerAccessUserId.ToString();
        }

        private LineItemModel GetTaxLine(Order order, OrderLine orderLine, int index, AddressLocationInfo destinationAddress, AddressLocationInfo originAddress)
        {
            LineItemModel line = new LineItemModel();
            var priceContext = new PriceContext(order.Currency, order.VatCountry);
            var price = (orderLine.HasType(OrderLineType.PointProduct)) ? orderLine.Product.GetPrice(priceContext) : GetProductPriceWithoutDiscounts(orderLine);
            line.amount = Convert.ToDecimal(price.PriceWithoutVAT);
            line.description = orderLine.ProductName;
            line.addresses = new AddressesModel
            {
                shipTo = destinationAddress,
                shipFrom = originAddress
            };

            line.number = string.IsNullOrEmpty(orderLine.Id) ? index.ToString() : orderLine.Id;
            line.quantity = Math.Abs((decimal)orderLine.Quantity);
            try
            {
                line.itemCode = Services.Products.GetProductFieldValue(orderLine.Product, ItemCodeFieldName).ToString();
                line.taxCode = Services.Products.GetProductFieldValue(orderLine.Product, TaxCodeFieldName).ToString();
            }
            catch (ArgumentException)
            {
                VerifyCustomFields();
            }

            return line;
        }

        /// <remarks>
		/// "FR020100" - Avalara System TaxCode for SHIPPING
		/// </remarks>
		private LineItemModel GetShippingTaxLine(Order order, AddressLocationInfo destinationAddress, AddressLocationInfo originAddress)
        {
            LineItemModel line = new LineItemModel();
            line.amount = Convert.ToDecimal(order.ShippingFee.PriceWithoutVAT);

            line.description = "SHIPPING"; 
            line.addresses = new AddressesModel
            {
                shipTo = destinationAddress,
                shipFrom = originAddress
            };

            line.number = ShippingCode;
            line.taxCode = TaxCodeShipping;

            return line;
        }

        private void GetOrderLinesFromTaxResult(Order order, TransactionModel taxResult)
        {
            var newOrderLines = new OrderLineCollection(order);

            if (taxResult.totalTax != 0)
            {
                foreach (var taxLine in taxResult.lines)
                {
                    var taxDetailNamesAndCount = new Dictionary<string, int>();
                    foreach (var taxDetail in taxLine.details)
                    {
                        if (!taxDetailNamesAndCount.ContainsKey(taxDetail.taxName))
                        {
                            taxDetailNamesAndCount.Add(taxDetail.taxName, 1);
                        }
                        else
                        {
                            taxDetailNamesAndCount[taxDetail.taxName] += 1;
                        }

                    }

                    foreach (var taxDetail in taxLine.details)
                    {
                        if (taxDetail.tax != 0M)
                        {
                            var taxOrderLine = new OrderLine(order);
                            taxOrderLine.Date = DateTime.Now;
                            taxOrderLine.Modified = DateTime.Now;

                            taxOrderLine.ProductNumber = string.Format("Tax Id# {0}", taxResult.id);
                            var taxName = taxDetail.taxName;
                            if (taxDetailNamesAndCount.ContainsKey(taxDetail.taxName) && taxDetailNamesAndCount[taxDetail.taxName] > 1 && !string.IsNullOrEmpty(taxDetail.jurisName))
                            {
                                taxName += " (" + taxDetail.jurisName + ")";
                            }
                            taxOrderLine.ProductName = taxName;
                            taxOrderLine.ProductVariantText = Name;
                            taxOrderLine.Order = order;
                            taxOrderLine.OrderId = order.Id;
                            taxOrderLine.Quantity = 1;

                            // Info: Set price - should be before setting Type
                            Services.OrderLines.SetUnitPrice(taxOrderLine, Convert.ToDouble(taxDetail.tax), false);
                            if (!order.Calculate)
                            {
                                Services.OrderLines.SetUnitPrice(taxOrderLine, taxOrderLine.UnitPrice, true);
                            }

                            taxOrderLine.OrderLineType = OrderLineType.Tax;
                            taxOrderLine.ParentLineId = taxLine.lineNumber;

                            newOrderLines.Add(taxOrderLine);
                        }
                    }
                }
            }

            foreach (var orderline in newOrderLines)
            {
                order.OrderLines.Add(orderline, false);
            }
        }

        private bool IsTaxableOrder(Order order)
        {
            var ret = order.OrderLines.Any(ol => (IsTaxableType(ol) || ol.HasType(OrderLineType.PointProduct)) && ol.Product != null);

            if (ret && DontUseIfExemptionNumberIsSet && order.CustomerAccessUserId != 0)
            {
                var customer = User.GetUserByID(order.CustomerAccessUserId);
                var exemptionNumberField = customer.SystemFieldValues.FirstOrDefault(f => f.SystemField.Name == ExemptionNumberFieldName);

                if (exemptionNumberField != null && exemptionNumberField.Value != null && !string.IsNullOrEmpty(exemptionNumberField.Value.ToString()))
                {
                    ret = false;
                }
            }

            return ret;
        }

        #endregion

        Hashtable IDropDownOptions.GetOptions(string name)
        {
            var options = new Hashtable();

            switch (name)
            {
                case "Origination State":
                    options.Add("AL", "Alabama");
                    options.Add("AK", "Alaska");
                    options.Add("AZ", "Arizona");
                    options.Add("AR", "Arkansas");
                    options.Add("CA", "California");
                    options.Add("CO", "Colorado");
                    options.Add("CT", "Connecticut");
                    options.Add("DE", "Delaware");
                    options.Add("DC", "District of Columbia");
                    options.Add("FL", "Florida");
                    options.Add("GA", "Georgia");
                    options.Add("HI", "Hawaii");
                    options.Add("ID", "Idaho");
                    options.Add("IL", "Illinois");
                    options.Add("IN", "Indiana");
                    options.Add("IA", "Iowa");
                    options.Add("KS", "Kansas");
                    options.Add("KY", "Kentucky");
                    options.Add("LA", "Louisiana");
                    options.Add("ME", "Maine");
                    options.Add("MD", "Maryland");
                    options.Add("MA", "Massachusetts");
                    options.Add("MI", "Michigan");
                    options.Add("MN", "Minnesota");
                    options.Add("MS", "Mississippi");
                    options.Add("MO", "Missouri");
                    options.Add("MT", "Montana");
                    options.Add("NE", "Nebraska");
                    options.Add("NV", "Nevada");
                    options.Add("NH", "New Hampshire");
                    options.Add("NJ", "New Jersey");
                    options.Add("NM", "New Mexico");
                    options.Add("NY", "New York");
                    options.Add("NC", "North Carolina");
                    options.Add("ND", "North Dakota");
                    options.Add("OH", "Ohio");
                    options.Add("OK", "Oklahoma");
                    options.Add("OR", "Oregon");
                    options.Add("PA", "Pennsylvania");
                    options.Add("RI", "Rhode Island");
                    options.Add("SC", "South Carolina");
                    options.Add("SD", "South Dakota");
                    options.Add("TN", "Tennessee");
                    options.Add("TX", "Texas");
                    options.Add("UT", "Utah");
                    options.Add("VT", "Vermont");
                    options.Add("VA", "Virginia");
                    options.Add("WA", "Washington");
                    options.Add("WV", "West Virginia");
                    options.Add("WI", "Wisconsin");
                    options.Add("WY", "Wyoming");

                    break;
                case "Boundary level":
                    options.Add("Address", "Address");
                    options.Add("Zip9", "Zip9");
                    options.Add("Zip5", "Zip5");

                    break;
                case "Get customer code from":
                    options.Add(CustomerCodeSource.OrderCustomerAccessUserId, "Access User ID");
                    options.Add(CustomerCodeSource.OrderCustomerNumber, "Customer Number");
                    options.Add(CustomerCodeSource.AccessUserExternalId, "External ID");
                    break;
            }

            return options;
        }

        #region CancelTaxRequest

        /// <summary>
        /// Cancels taxes for order
        /// </summary>
        /// <param name="order">Order instance</param>
        public override void CancelTaxes(Order order)
        {
            if (string.IsNullOrEmpty(order.TaxTransactionNumber) || !EnableCommit)
            {
                return;
            }

            VoidTransactionModel voidTransactionModel = new VoidTransactionModel
            {
                code = VoidReasonCode.DocVoided
            };

            CancelTransactionResponse cancelTaxResult;

            try
            {
                cancelTaxResult = PostToAvalara<CancelTransactionResponse>($"companies/{CompanyCode}/transactions/{order.TaxTransactionNumber}/void", JsonSerializer.Serialize(voidTransactionModel));
                if (Debug)
                {
                    SaveAvaTaxLog(cancelTaxResult);
                }
            }
            catch (Exception)
            {
                cancelTaxResult = null;
                _orderDebuggingInfoService.Save(order, "Error cancelling transaction.", "AvaTax");
            }

            if (cancelTaxResult != null)
            {
                if (cancelTaxResult.status != "Cancelled")
                {
                    var stringBuilder = new StringBuilder();
                    stringBuilder.Append("Error cancelling AvaTax transaction.");

                    if (cancelTaxResult.messages?.Count > 0)
                    {
                        stringBuilder.Append(" Message(s) from Gateway:");

                        foreach (var message in cancelTaxResult.messages)
                        {
                            stringBuilder.Append($" Details: {message.details}");
                            stringBuilder.Append($" RefersTo: {message.refersTo}");
                            stringBuilder.Append($" Severity: {message.severity}");
                            stringBuilder.Append($" Source: {message.source}");
                            stringBuilder.Append($" Summary: {message.summary}");
                        }
                    }

                    _orderDebuggingInfoService.Save(order, stringBuilder.ToString(), "AvaTax");
                }
                else
                {
                    foreach (OrderLine orderLine in order.OrderLines)
                    {
                        if (orderLine.OrderLineType == OrderLineType.Tax && orderLine.ProductVariantText == Name)
                        {
                            orderLine.ProductName += " - CANCELLED";
                            Services.OrderLines.Save(orderLine);
                        }
                    }

                    _orderDebuggingInfoService.Save(order, "Transaction was cancelled", "AvaTax");
                }
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
            if (!order.Complete) return;

            try
            {
                var taxResult = PrepareTaxRequest(order, TransactionType.Adjust).Create();

                if (Debug)
                {
                    SaveAvaTaxLog(taxResult);
                }
                var message = string.Format("Taxes were adjusted with ResultCode ({0})", taxResult.code);

                if (taxResult.messages is null)
                {
                    if (EnableCommit)
                    {
                        message += string.Format("; TransactionId #{0}", taxResult.code);
                        order.TaxTransactionNumber = taxResult.code;
                        Services.Orders.Save(order);
                    }
                    else
                    {
                        message += string.Format("; Commit is disabled");
                    }

                }
                else
                {
                    message += GetErrorMessage(taxResult);
                }
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
            {
                return;
            }

            try
            {
                this.originalOrder = originalOrder;
                var taxResult = GetTaxes(order, TransactionType.ProductReturns);

                if (Debug)
                {
                    SaveAvaTaxLog(taxResult);
                }
                string message = string.Format("Handle product returns with ResultCode ({0})", taxResult.code);
                if (taxResult.messages is null)
                {
                    GetOrderLinesFromTaxResult(order, taxResult);
                    if (EnableCommit)
                    {
                        message += string.Format("; TransactionId #{0}", taxResult.code);
                        order.TaxTransactionNumber = taxResult.code;
                        Services.Orders.Save(order);
                    }
                    else
                    {
                        message += string.Format("; Commit is disabled");
                    }
                }
                else
                {
                    message += GetErrorMessage(taxResult);
                }
                _orderDebuggingInfoService.Save(order, message, "AvaTax");
            }
            catch (Exception err)
            {
                SaveLog(err.ToString());
            }
        }

        #endregion

        #region SaveAvaTaxLog
        private string GetErrorMessage(TransactionModel taxResult)
        {
            var errMessages = new StringBuilder();
            if (taxResult.messages?.Count > 0)
            {
                foreach (var message in taxResult.messages)
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

        private void SaveAvaTaxLog<T>(T taxRequest)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                var writer = new StringWriter();
                serializer.Serialize(writer, taxRequest);

                SaveLog(writer.ToString());
            }
            catch (Exception err)
            {
                SaveLog(err.ToString());
            }
        }

        #endregion

        #region TestConnection

        /// <summary>
        /// Tests tax service connection
        /// </summary>
        /// <returns>list of result information lines</returns>
        public ArrayList TestConnection()
        {
            var taxSvc = PrepareTaxSvc();

            var list = new ArrayList();
            try
            {
                var result = taxSvc.Ping();

                if (!result.authenticated.GetValueOrDefault())
                {
                    list.Add("Ping was not successfull!");
                }
                else
                {
                    list.Add(string.Format("Is authenticated: {0}", result.authenticated.Value));
                    list.Add(string.Format("Version: {0}", result.version));
                }
            }
            catch (Exception ex)
            {
                list.Add(ex.Message);
                SaveLog(ex.ToString());
            }

            return list;
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
                    {
                        exemptionNumberField.Save();
                    }
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
        public override void OnAfterSettingsSaved()
        {
            VerifyCustomFields();
        }

        /// <summary>
        /// Before tax calculation arguments
        /// </summary>
        public class BeforeTaxCalculationArgs : CancelableNotificationArgs
        {
            public Order Order { get; set; }
        }

        /// <summary>
        /// Before tax commit arguments
        /// </summary>
        public class BeforeTaxCommitArgs : CancelableNotificationArgs
        {
            public Order Order { get; set; }
        }

        /// <summary>
        /// Args class to get the customer code to send to Avalara.
        /// </summary>
        public class OnGetCustomerCodeArgs : NotificationArgs
        {
            public Order Order { get; set; }
            public string CustomerCode { get; set; }
        }


        #region AddTaxesToProducts

        /// <summary>
        /// Adds taxes to products collection
        /// </summary>
        /// <param name="products"></param>
        public override void AddTaxesToProducts(IEnumerable<Product> products)
        {
            try
            {
                if (DontUseInProductCatalog) return;

                var order = PrepareOrder(products);
                if (order == null || !IsTaxableOrder(order))
                {
                    return;
                }

                var taxResult = GetTaxes(order, TransactionType.Calculate);
                if (Debug)
                {
                    SaveAvaTaxLog(taxResult);
                }
                if (taxResult.messages is null)
                {
                    if (taxResult.totalTax > 0)
                    {
                        foreach (var taxLine in taxResult.lines)
                        {
                            foreach (var taxDetail in taxLine.details)
                            {
                                var orderLine = order.OrderLines.FirstOrDefault(line => line.Id == taxLine.lineNumber);
                                if (orderLine != null)
                                {
                                    products.FirstOrDefault(obj => obj.Id == orderLine.ProductId)?.TaxCollection.Add(GetTax(orderLine.Product, taxDetail));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // error
            }
        }

        private Tax GetTax(Product product, TransactionLineDetailModel taxDetail)
        {
            var tax = new Tax();
            tax.Name = taxDetail.taxName;
            tax.Product = product;

            tax.Amount = new PriceRaw((double)taxDetail.tax, Services.Currencies.GetDefaultCurrency());
            tax.CalculateVat = true; //AddVAT;

            return tax;
        }

        private Order PrepareOrder(IEnumerable<Product> products)
        {
            Order order = null;
            var currentCart = Common.Context.Cart;

            if (currentCart != null && (!string.IsNullOrEmpty(currentCart.CustomerZip) || !string.IsNullOrEmpty(currentCart.DeliveryZip)))
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
                var user = User.GetCurrentUser(PagePermissionLevels.Frontend);

                if (user != null)
                {
                    order = new Order(Common.Context.Currency, Common.Context.Country, Common.Context.Language);
                    order.Id = "ProductTaxesU";
                    order.Complete = false;
                    order.IsCart = true;
                    order.CurrencyCode = Common.Context.Currency.Code;
                    order.CustomerCountryCode = Common.Context.Country.Code2;
                    order.CustomerAccessUserId = user.ID;
                    order.CustomerAccessUserUserName = user.UserName;

                    var defaultAddress = UserAddress.GetUserDefaultAddress(user.ID);
                    if (defaultAddress != null)
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
            if (order != null)
            {
                var i = 0;
                foreach (Product product in products)
                {
                    var orderLine = Services.OrderLines.Create(order, product, 1d, null, null);
                    orderLine.Id = (i++).ToString();
                }
            }
        }

        #endregion
    }
}
