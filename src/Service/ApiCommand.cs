namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;

internal enum ApiCommand
{
    /// <summary>
    /// Records a new transaction in AvaTax.
    /// See: https://developer.avalara.com/api-reference/avatax/rest/v2/methods/Transactions/CreateTransaction/
    /// POST /transactions/create
    /// </summary>
    CreateTransaction,

    /// <summary>
    /// Retrieve geolocation information for a specified US or Canadian address
    /// See: https://developer.avalara.com/api-reference/avatax/rest/v2/methods/Addresses/ResolveAddress/
    /// GET /addresses/resolve
    /// </summary>
    ResolveAddress,

    /// <summary>
    /// Void a transaction.
    /// See: https://developer.avalara.com/api-reference/avatax/rest/v2/methods/Transactions/VoidTransaction/
    /// POST /companies/{operatorId}/transactions/{OperatorSecondId}/void
    /// </summary>
    VoidTransaction
}
