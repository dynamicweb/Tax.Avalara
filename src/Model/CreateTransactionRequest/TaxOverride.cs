using System;
using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;

[DataContract]
internal sealed class TaxOverride
{
    [DataMember(Name = "type", EmitDefaultValue = false)]
    public string Type { get; set; }

    [DataMember(Name = "taxAmount", EmitDefaultValue = false)]
    public double? TaxAmount { get; set; }

    [DataMember(Name = "taxDate", EmitDefaultValue = false)]
    public DateTime TaxDate { get; set; }

    [DataMember(Name = "reason", EmitDefaultValue = false)]
    public string Reason { get; set; }
}
