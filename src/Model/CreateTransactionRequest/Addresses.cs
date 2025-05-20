using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;

[DataContract]
internal sealed class Addresses
{
    [DataMember(Name = "shipFrom", EmitDefaultValue = false)]
    public AddressLocationInfo ShipFrom { get; set; }

    [DataMember(Name = "shipTo", EmitDefaultValue = false)]
    public AddressLocationInfo ShipTo { get; set; }
}
