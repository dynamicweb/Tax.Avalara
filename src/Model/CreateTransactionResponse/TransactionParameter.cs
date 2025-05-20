using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionResponse;

[DataContract]
internal sealed class TransactionParameter
{
    [DataMember(Name = "name", EmitDefaultValue = false)]
    public string Name { get; set; }

    [DataMember(Name = "value", EmitDefaultValue = false)]
    public string Value { get; set; }

    [DataMember(Name = "unit", EmitDefaultValue = false)]
    public string Unit { get; set; }
}
