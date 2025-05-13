using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.VoidTransaction;

[DataContract]
internal sealed class Location
{
    [DataMember(Name = "id")]
    public int Id { get; set; }

    [DataMember(Name = "companyId")]
    public int? CompanyId { get; set; }

    [DataMember(Name = "locationCode")]
    public string LocationCode { get; set; }

    [DataMember(Name = "description")]
    public string Description { get; set; }

    [DataMember(Name = "isMarketplaceOutsideUsa")]
    public bool? IsMarketplaceOutsideUsa { get; set; }

    [DataMember(Name = "line1")]
    public string Line1 { get; set; }

    [DataMember(Name = "line2")]
    public string Line2 { get; set; }

    [DataMember(Name = "line3")]
    public string Line3 { get; set; }

    [DataMember(Name = "city")]
    public string City { get; set; }

    [DataMember(Name = "county")]
    public string County { get; set; }

    [DataMember(Name = "region")]
    public string Region { get; set; }

    [DataMember(Name = "postalCode")]
    public string PostalCode { get; set; }

    [DataMember(Name = "country")]
    public string Country { get; set; }

    [DataMember(Name = "isDefault")]
    public bool IsDefault { get; set; }

    [DataMember(Name = "isRegistered")]
    public bool IsRegistered { get; set; }

    [DataMember(Name = "dbaName")]
    public string DbaName { get; set; }

    [DataMember(Name = "outletName")]
    public string OutletName { get; set; }

    [DataMember(Name = "effectiveDate")]
    public string EffectiveDate { get; set; }

    [DataMember(Name = "endDate")]
    public string EndDate { get; set; }

    [DataMember(Name = "lastTransactionDate")]
    public string LastTransactionDate { get; set; }

    [DataMember(Name = "registeredDate")]
    public string RegisteredDate { get; set; }

    [DataMember(Name = "createdDate")]
    public string CreatedDate { get; set; }

    [DataMember(Name = "modifiedDate")]
    public string ModifiedDate { get; set; }
}