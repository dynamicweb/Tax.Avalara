using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionResponse;

[DataContract]
internal sealed class TransactionAddress
{
    [DataMember(Name = "id")]
    public long? Id { get; set; }

    [DataMember(Name = "transactionId")]
    public long? TransactionId { get; set; }

    [DataMember(Name = "boundaryLevel")]
    public string BoundaryLevel { get; set; }

    [DataMember(Name = "line1")]
    public string Line1 { get; set; }

    [DataMember(Name = "line2")]
    public string Line2 { get; set; }

    [DataMember(Name = "line3")]
    public string Line3 { get; set; }

    [DataMember(Name = "city")]
    public string City { get; set; }

    [DataMember(Name = "region")]
    public string Region { get; set; }

    [DataMember(Name = "postalCode")]
    public string PostalCode { get; set; }

    [DataMember(Name = "country")]
    public string Country { get; set; }

    [DataMember(Name = "taxRegionId")]
    public int? TaxRegionId { get; set; }

    [DataMember(Name = "latitude")]
    public string Latitude { get; set; }

    [DataMember(Name = "longitude")]
    public string Longitude { get; set; }
}
