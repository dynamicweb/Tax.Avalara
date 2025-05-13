using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.VoidTransaction;

[DataContract]
internal sealed class VoidTransactionRequest
{
    [DataMember(Name = "code")]
    public string Code { get; set; }
}
