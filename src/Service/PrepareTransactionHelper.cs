using Dynamicweb.Core;
using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.Prices;
using Dynamicweb.Ecommerce.Products;
using Dynamicweb.Ecommerce.Products.Taxes;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.Enums;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Notifications;
using Dynamicweb.Extensibility.Notifications;
using Dynamicweb.Security.UserManagement;
using Dynamicweb.Security.UserManagement.Common.CustomFields;
using System;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;

internal sealed class PrepareTransactionHelper
{
    public Order Order { get; }
    public AvalaraTaxProvider Provider { get; }

    public PrepareTransactionHelper(Order order, AvalaraTaxProvider provider)
    {
        Order = order ?? throw new ArgumentNullException(nameof(order));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public CreateTransactionRequest PrepareTransactionRequest(TransactionType transactionType)
    {
        if (transactionType is TransactionType.ProductReturns)
            throw new InvalidOperationException($"Use {nameof(PrepareProductReturnRequest)} for TransactionType.ProductReturns.");

        var request = InitializeBaseRequest(transactionType);

        if (transactionType is TransactionType.Commit)
        {
            request.Commit = true;
            request.Date = Order.Date;
        }
        else if (transactionType is TransactionType.Adjust)
            SetAdjustData(request, Provider.EnableCommit);

        PopulateCommonDataAndLines(request);

        return request;
    }

    public CreateTransactionRequest PrepareProductReturnRequest(Order originalOrder)
    {
        if (originalOrder is null)
            throw new ArgumentNullException(nameof(originalOrder), "Original Order must be set for Product Returns tax request");

        var request = InitializeBaseRequest(TransactionType.ProductReturns);

        SetReturnData(request, originalOrder, Provider.EnableCommit);
        PopulateCommonDataAndLines(request);

        return request;
    }

    private static DocumentType GetDocumentType(TransactionType transactionType) => transactionType switch
    {
        TransactionType.Adjust or TransactionType.Commit => DocumentType.SalesInvoice,
        TransactionType.Calculate => DocumentType.SalesOrder,
        TransactionType.ProductReturns => DocumentType.ReturnInvoice,
        _ => throw new NotImplementedException($"Unknown or unsupported transaction type: {transactionType}")
    };

    private CreateTransactionRequest InitializeBaseRequest(TransactionType transactionType)
    {
        var request = new CreateTransactionRequest
        {
            CompanyCode = Provider.CompanyCode,
            CustomerCode = GetCustomerCode(),
            Date = DateTime.Now,
            Type = GetDocumentType(transactionType).ToString(),
            ReferenceCode = Order.Id,
            CurrencyCode = Order.CurrencyCode
        };

        SetAddress(request);
        SetCustomerExemptionData(request);

        return request;
    }

    private void PopulateCommonDataAndLines(CreateTransactionRequest request)
    {
        var priceContext = new PriceContext(Order.Currency, Order.VatCountry);
        int index = 0;
        double orderDiscount = 0;

        foreach (OrderLine orderLine in Order.OrderLines)
        {
            if (Provider.IsTaxableTypeInternal(orderLine) || orderLine.HasType(OrderLineType.PointProduct))
            {
                if (orderLine.Product is null)
                    continue;

                LineItem line = GetTaxLine(orderLine, index++, request.Addresses.ShipFrom, request.Addresses.ShipTo);
                request.Lines.Add(line);
                if (orderLine.HasType(OrderLineType.PointProduct))
                    orderDiscount += -Convert.ToDouble(orderLine.Product.GetPrice(priceContext).PriceWithoutVAT);
            }
            else if (orderLine.HasType(OrderLineType.Discount) && string.IsNullOrEmpty(orderLine.GiftCardCode))
                orderDiscount += Convert.ToDouble(orderLine.Price.PriceWithoutVAT);
        }

        orderDiscount = Math.Abs(orderDiscount);
        if (orderDiscount > 0)
        {
            foreach (LineItem line in request.Lines)
            {
                if (string.Equals(line.TaxCode, Provider.TaxCodeShipping, StringComparison.Ordinal))
                    line.Discounted = true;
            }
            request.Discount = orderDiscount;
        }

        LineItem shippingLine = GetShippingTaxLine(request.Addresses.ShipFrom, request.Addresses.ShipTo);
        if (shippingLine.Amount > 0)
            request.Lines.Add(shippingLine);
    }

    private void SetAdjustData(CreateTransactionRequest request, bool enableCommit)
    {
        request.TaxOverride = new()
        {
            Type = "TaxDate",
            TaxAmount = 0,
            Reason = "Adjust",
            TaxDate = Order.Date
        };
        request.Commit = !string.IsNullOrEmpty(Order.TaxTransactionNumber) && enableCommit;
    }

    private void SetReturnData(CreateTransactionRequest request, Order originalOrder, bool enableCommit)
    {
        request.TaxOverride = new()
        {
            Type = "TaxDate",
            TaxAmount = 0,
            Reason = "Return",
            TaxDate = originalOrder.Date
        };

        request.Date = Order.Date;
        request.ReferenceCode = originalOrder.Id;
        request.Commit = !string.IsNullOrEmpty(originalOrder.TaxTransactionNumber) && enableCommit;
    }

    private void SetCustomerExemptionData(CreateTransactionRequest request)
    {
        if (Order.CustomerAccessUserId <= 0)
            return;

        if (UserManagementServices.Users.GetUserById(Order.CustomerAccessUserId) is not User customer)
            return;

        foreach (CustomFieldValue fieldValue in customer.CustomFieldValues)
        {
            if (string.Equals(fieldValue.CustomField.SystemName, AvalaraTaxProvider.ExemptionNumberFieldName, StringComparison.OrdinalIgnoreCase) && fieldValue.Value is not null)
                request.ExemptionNumber = fieldValue.Value.ToString();
            else if (string.Equals(fieldValue.CustomField.SystemName, AvalaraTaxProvider.EntityUseCodeFieldName, StringComparison.OrdinalIgnoreCase) && fieldValue.Value is not null)
                request.EntityUseCode = fieldValue.Value.ToString();
        }
    }

    private void SetAddress(CreateTransactionRequest request)
    {
        request.Addresses = new Addresses();
        request.Addresses.ShipFrom = AvalaraAddressValidatorProvider.GetOriginAddress(Provider);

        var destinationAddress = new AddressLocationInfo();
        destinationAddress = !string.IsNullOrEmpty(Order.DeliveryZip)
            ? AvalaraAddressValidatorProvider.GetDeliveryAddress(Order)
            : AvalaraAddressValidatorProvider.GetBillingAddress(Order);

        if (string.IsNullOrEmpty(destinationAddress.PostalCode))
            throw new InvalidOperationException("Make sure that the address is provided with a zip code.");

        request.Addresses.ShipTo = destinationAddress;
    }

    private LineItem GetTaxLine(OrderLine orderLine, int index, AddressLocationInfo originAddress, AddressLocationInfo destinationAddress)
    {
        var line = new LineItem();
        var priceContext = new PriceContext(Order.Currency, Order.VatCountry);

        if (orderLine.Product is null)
            throw new InvalidOperationException($"OrderLine {orderLine.Id} (Product: {orderLine.ProductName}) is missing associated Product data.");

        PriceInfo price = orderLine.HasType(OrderLineType.PointProduct)
            ? orderLine.Product.GetPrice(priceContext)
            : Provider.GetProductPriceWithoutDiscountsInternal(orderLine);

        line.Amount = Convert.ToDouble(price.PriceWithoutVAT);
        line.Description = orderLine.ProductName;
        line.Addresses = new()
        {
            ShipFrom = originAddress,
            ShipTo = destinationAddress
        };

        line.Number = !string.IsNullOrEmpty(orderLine.Id) ? orderLine.Id : index.ToString();
        line.Quantity = Math.Abs((double)orderLine.Quantity);

        line.ItemCode = Services.Products.GetProductFieldValue(orderLine.Product, AvalaraTaxProvider.ItemCodeFieldName).ToString();
        line.TaxCode = Services.Products.GetProductFieldValue(orderLine.Product, AvalaraTaxProvider.TaxCodeFieldName).ToString();

        return line;
    }

    /// <remarks>
    /// "FR020100" - Avalara System TaxCode for SHIPPING
    /// </remarks>
    private LineItem GetShippingTaxLine(AddressLocationInfo originAddress, AddressLocationInfo destinationAddress)
    {
        var line = new LineItem();
        line.Amount = Converter.ToDouble(Order.ShippingFee?.PriceWithoutVAT);

        line.Description = "SHIPPING";
        line.Addresses = new Addresses
        {
            ShipFrom = originAddress,
            ShipTo = destinationAddress
        };

        line.Number = TaxProvider.ShippingCode;
        line.TaxCode = Provider.TaxCodeShipping;

        return line;
    }

    private string GetCustomerCode()
    {
        var notificationArgs = new OnGetCustomerCodeArgs { Order = Order };
        NotificationManager.Notify(AvalaraTaxProvider.OnGetCustomerCode, notificationArgs);

        if (!string.IsNullOrEmpty(notificationArgs.CustomerCode))
            return notificationArgs.CustomerCode;

        if (!string.IsNullOrEmpty(Provider.GetCustomerCodeFrom))
        {
            return Provider.GetCustomerCodeFrom switch
            {
                nameof(CustomerCodeSource.OrderCustomerAccessUserId) => Order.CustomerAccessUserId.ToString(),
                nameof(CustomerCodeSource.OrderCustomerNumber) => Order.CustomerNumber ?? "",
                nameof(CustomerCodeSource.AccessUserExternalId) => Order.CustomerAccessUserId > 0
                    ? UserManagementServices.Users.GetUserById(Order.CustomerAccessUserId)?.ExternalID ?? ""
                    : string.Empty,
                _ => throw new NotImplementedException($"Unsupported option is used: {Provider.GetCustomerCodeFrom}")
            };
        }

        return Order.CustomerAccessUserId.ToString();
    }
}
