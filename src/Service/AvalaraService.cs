using Dynamicweb.Core;
using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionResponse;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.ResolveAddressResponse;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.VoidTransaction;
using System;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;

internal sealed class AvalaraService
{
    public string AccountId { get; set; }

    public string LicenseKey { get; set; }

    public bool DebugLog { get; set; }

    public bool TestMode { get; set; }

    public CreateTransactionResponse CreateAdjustTransaction(Order order, AvalaraTaxProvider providerData) => CreateTransaction(order, providerData, TransactionType.Adjust);

    public CreateTransactionResponse CreateCalculateTransaction(Order order, AvalaraTaxProvider providerData) => CreateTransaction(order, providerData, TransactionType.Calculate);

    public CreateTransactionResponse CreateCommitTransaction(Order order, AvalaraTaxProvider providerData) => CreateTransaction(order, providerData, TransactionType.Commit);

    public CreateTransactionResponse CreateProductReturnsTransaction(Order order, AvalaraTaxProvider providerData, Order originalOrder)
    {
        AvalaraTaxProvider.VerifyCustomFields();

        var transactionHelper = new PrepareTransactionHelper(order, providerData);
        CreateTransactionRequest request = transactionHelper.PrepareProductReturnRequest(originalOrder);

        return SendTransactionRequest(request);
    }

    public ResolveAddressResponse ResolveAddress(AddressLocationInfo address)
    {
        var configuration = new CommandConfiguration
        {
            CommandType = ApiCommand.ResolveAddress,
            DebugLog = DebugLog,
            QueryStringParameters = new(StringComparer.OrdinalIgnoreCase)
            {
                ["line1"] = address.Line1,
                ["line2"] = address.Line2,
                ["line3"] = address.Line3,
                ["city"] = address.City,
                ["region"] = address.Region,
                ["postalCode"] = address.PostalCode,
                ["country"] = address.Country,
                ["textCase"] = "Mixed"
            }
        };

        string response = AvalaraRequest.SendRequest(AccountId, LicenseKey, GetBaseAddress(), configuration);

        return Converter.Deserialize<ResolveAddressResponse>(response);
    }

    public VoidTransactionResponse VoidTransaction(string companyCode, string taxTransactionNumber)
    {
        var configuration = new CommandConfiguration
        {
            CommandType = ApiCommand.VoidTransaction,
            DebugLog = DebugLog,
            OperatorId = companyCode,
            OperatorSecondId = taxTransactionNumber,
            Data = new VoidTransactionRequest { Code = "DocVoided" }
        };

        string response = AvalaraRequest.SendRequest(AccountId, LicenseKey, GetBaseAddress(), configuration);

        return Converter.Deserialize<VoidTransactionResponse>(response);
    }

    private CreateTransactionResponse CreateTransaction(Order order, AvalaraTaxProvider providerData, TransactionType transactionType)
    {
        AvalaraTaxProvider.VerifyCustomFields();

        var transactionHelper = new PrepareTransactionHelper(order, providerData);
        CreateTransactionRequest request = transactionHelper.PrepareTransactionRequest(transactionType);

        return SendTransactionRequest(request);
    }

    private CreateTransactionResponse SendTransactionRequest(CreateTransactionRequest request)
    {
        string response = AvalaraRequest.SendRequest(AccountId, LicenseKey, GetBaseAddress(), new()
        {
            CommandType = ApiCommand.CreateTransaction,
            DebugLog = DebugLog,
            Data = request
        });

        return Converter.Deserialize<CreateTransactionResponse>(response);
    }

    private string GetBaseAddress() => TestMode
        ? "https://sandbox-rest.avatax.com/api/v2"
        : "https://rest.avatax.com/api/v2";
}
