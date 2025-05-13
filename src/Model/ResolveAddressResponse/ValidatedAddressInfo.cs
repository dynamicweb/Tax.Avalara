using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.ResolveAddressResponse;

[DataContract]
internal sealed class ValidatedAddressInfo
{
    [DataMember(Name = "addressType", EmitDefaultValue = false)]
    public string AddressType { get; set; }

    [DataMember(Name = "line1", EmitDefaultValue = false)]
    public string Line1 { get; set; }

    [DataMember(Name = "line2", EmitDefaultValue = false)]
    public string Line2 { get; set; }

    [DataMember(Name = "line3", EmitDefaultValue = false)]
    public string Line3 { get; set; }

    [DataMember(Name = "city", EmitDefaultValue = false)]
    public string City { get; set; }

    [DataMember(Name = "region", EmitDefaultValue = false)]
    public string Region { get; set; }

    [DataMember(Name = "country", EmitDefaultValue = false)]
    public string Country { get; set; }

    [DataMember(Name = "postalCode", EmitDefaultValue = false)]
    public string PostalCode { get; set; }

    [DataMember(Name = "latitude", EmitDefaultValue = false)]
    public double? Latitude { get; set; }

    [DataMember(Name = "longitude", EmitDefaultValue = false)]
    public double? Longitude { get; set; }
}