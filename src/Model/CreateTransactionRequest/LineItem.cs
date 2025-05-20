using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;

[DataContract]
internal sealed class LineItem
{
    [DataMember(Name = "number", EmitDefaultValue = false)]
    public string Number { get; set; }

    [DataMember(Name = "quantity", EmitDefaultValue = false)]
    public double? Quantity { get; set; }

    [DataMember(Name = "amount", IsRequired = true)]
    public double Amount { get; set; }

    [DataMember(Name = "addresses", EmitDefaultValue = false)]
    public Addresses Addresses { get; set; }

    [DataMember(Name = "taxCode", EmitDefaultValue = false)]
    public string TaxCode { get; set; }

    [DataMember(Name = "itemCode", EmitDefaultValue = false)]
    public string ItemCode { get; set; }

    [DataMember(Name = "discounted", EmitDefaultValue = false)]
    public bool? Discounted { get; set; }

    [DataMember(Name = "description", EmitDefaultValue = false)]
    public string Description { get; set; }
}
