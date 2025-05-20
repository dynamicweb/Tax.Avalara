using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.VoidTransaction;

[DataContract]
internal sealed class VoidTransactionResponse
{
    [DataMember(Name = "id")]
    public long Id { get; set; }

    [DataMember(Name = "code")]
    public string Code { get; set; }

    [DataMember(Name = "companyId")]
    public int CompanyId { get; set; }

    [DataMember(Name = "date")]
    public string Date { get; set; }

    [DataMember(Name = "paymentDate")]
    public string PaymentDate { get; set; }

    [DataMember(Name = "status")]
    public string Status { get; set; }

    [DataMember(Name = "type")]
    public string Type { get; set; }

    [DataMember(Name = "batchCode")]
    public string BatchCode { get; set; }

    [DataMember(Name = "currencyCode")]
    public string CurrencyCode { get; set; }

    [DataMember(Name = "exchangeRateCurrencyCode")]
    public string ExchangeRateCurrencyCode { get; set; }

    [DataMember(Name = "customerUsageType")]
    public string CustomerUsageType { get; set; }

    [DataMember(Name = "entityUseCode")]
    public string EntityUseCode { get; set; }

    [DataMember(Name = "customerVendorCode")]
    public string CustomerVendorCode { get; set; }

    [DataMember(Name = "customerCode")]
    public string CustomerCode { get; set; }

    [DataMember(Name = "exemptNo")]
    public string ExemptNo { get; set; }

    [DataMember(Name = "reconciled")]
    public bool Reconciled { get; set; }

    [DataMember(Name = "locationCode")]
    public string LocationCode { get; set; }

    [DataMember(Name = "reportingLocationCode")]
    public string ReportingLocationCode { get; set; }

    [DataMember(Name = "purchaseOrderNo")]
    public string PurchaseOrderNo { get; set; }

    [DataMember(Name = "referenceCode")]
    public string ReferenceCode { get; set; }

    [DataMember(Name = "salespersonCode")]
    public string SalespersonCode { get; set; }

    [DataMember(Name = "taxOverrideType")]
    public string TaxOverrideType { get; set; }

    [DataMember(Name = "taxOverrideAmount")]
    public double TaxOverrideAmount { get; set; }

    [DataMember(Name = "taxOverrideReason")]
    public string TaxOverrideReason { get; set; }

    [DataMember(Name = "totalAmount")]
    public double TotalAmount { get; set; }

    [DataMember(Name = "totalExempt")]
    public double TotalExempt { get; set; }

    [DataMember(Name = "totalDiscount")]
    public double TotalDiscount { get; set; }

    [DataMember(Name = "totalTax")]
    public double TotalTax { get; set; }

    [DataMember(Name = "totalTaxable")]
    public double TotalTaxable { get; set; }

    [DataMember(Name = "totalTaxCalculated")]
    public double TotalTaxCalculated { get; set; }

    [DataMember(Name = "adjustmentReason")]
    public string AdjustmentReason { get; set; }

    [DataMember(Name = "adjustmentDescription")]
    public string AdjustmentDescription { get; set; }

    [DataMember(Name = "locked")]
    public bool Locked { get; set; }

    [DataMember(Name = "region")]
    public string Region { get; set; }

    [DataMember(Name = "country")]
    public string Country { get; set; }

    [DataMember(Name = "originAddressId")]
    public long OriginAddressId { get; set; }

    [DataMember(Name = "destinationAddressId")]
    public long DestinationAddressId { get; set; }

    [DataMember(Name = "exchangeRateEffectiveDate")]
    public string ExchangeRateEffectiveDate { get; set; }

    [DataMember(Name = "exchangeRate")]
    public double ExchangeRate { get; set; }

    [DataMember(Name = "isSellerImporterOfRecord")]
    public bool IsSellerImporterOfRecord { get; set; }

    [DataMember(Name = "description")]
    public string Description { get; set; }

    [DataMember(Name = "email")]
    public string Email { get; set; }

    [DataMember(Name = "businessIdentificationNo")]
    public string BusinessIdentificationNo { get; set; }

    [DataMember(Name = "modifiedDate")]
    public string ModifiedDate { get; set; }

    [DataMember(Name = "modifiedUserId")]
    public int ModifiedUserId { get; set; }

    [DataMember(Name = "taxDate")]
    public string TaxDate { get; set; }

    [DataMember(Name = "lines")]
    public IEnumerable<LineItem> Lines { get; set; }

    [DataMember(Name = "addresses")]
    public IEnumerable<Address> Addresses { get; set; }

    [DataMember(Name = "locationTypes")]
    public IEnumerable<Location> LocationTypes { get; set; }

    [DataMember(Name = "messages")]
    public IEnumerable<Message> Messages { get; set; }
}
