using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionResponse;

[DataContract]
internal sealed class TransactionLine
{
    [DataMember(Name = "id")]
    public long? Id { get; set; }

    [DataMember(Name = "transactionId")]
    public long? TransactionId { get; set; }

    [DataMember(Name = "lineNumber")]
    public string LineNumber { get; set; }

    [DataMember(Name = "details")]
    public IEnumerable<TransactionLineDetail> Details { get; set; }
}
