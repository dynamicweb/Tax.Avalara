using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;

[DataContract]
internal sealed class CreateTransactionRequest
{
    [DataMember(Name = "lines", IsRequired = true)]
    public List<LineItem> Lines { get; set; } = [];

    [DataMember(Name = "type", EmitDefaultValue = false)]
    public string Type { get; set; }

    [DataMember(Name = "companyCode", EmitDefaultValue = false)]
    public string CompanyCode { get; set; }

    [DataMember(Name = "date", IsRequired = true)]
    public DateTime Date { get; set; }

    [DataMember(Name = "customerCode", IsRequired = true)]
    public string CustomerCode { get; set; }

    [DataMember(Name = "discount", EmitDefaultValue = false)]
    public double? Discount { get; set; }

    [DataMember(Name = "exemptionNo", EmitDefaultValue = false)]
    public string ExemptionNumber { get; set; }

    [DataMember(Name = "entityUseCode", EmitDefaultValue = false)]
    public string EntityUseCode { get; set; }

    [DataMember(Name = "addresses", EmitDefaultValue = false)]
    public Addresses Addresses { get; set; }

    [DataMember(Name = "referenceCode", EmitDefaultValue = false)]
    public string ReferenceCode { get; set; }

    [DataMember(Name = "commit", EmitDefaultValue = false)]
    public bool? Commit { get; set; }

    [DataMember(Name = "taxOverride", EmitDefaultValue = false)]
    public TaxOverride TaxOverride { get; set; }

    [DataMember(Name = "currencyCode", EmitDefaultValue = false)]
    public string CurrencyCode { get; set; }
}
