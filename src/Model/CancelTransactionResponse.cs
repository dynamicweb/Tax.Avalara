using Avalara.AvaTax.RestClient;
using System;
using System.Collections.Generic;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model
{
    internal class CancelTransactionResponse
    {
        public long id { get; set; }
        public string code { get; set; }
        public int companyId { get; set; }
        public string date { get; set; }
        public string paymentDate { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public string batchCode { get; set; }
        public string currencyCode { get; set; }
        public string exchangeRateCurrencyCode { get; set; }
        public string customerUsageType { get; set; }
        public string entityUseCode { get; set; }
        public string customerVendorCode { get; set; }
        public string customerCode { get; set; }
        public string exemptNo { get; set; }
        public bool reconciled { get; set; }
        public string locationCode { get; set; }
        public string reportingLocationCode { get; set; }
        public string purchaseOrderNo { get; set; }
        public string referenceCode { get; set; }
        public string salespersonCode { get; set; }
        public string taxOverrideType { get; set; }
        public double taxOverrideAmount { get; set; }
        public string taxOverrideReason { get; set; }
        public double totalAmount { get; set; }
        public double totalExempt { get; set; }
        public double totalDiscount { get; set; }
        public double totalTax { get; set; }
        public double totalTaxable { get; set; }
        public double totalTaxCalculated { get; set; }
        public string adjustmentReason { get; set; }
        public string adjustmentDescription { get; set; }
        public bool locked { get; set; }
        public string region { get; set; }
        public string country { get; set; }
        public int version { get; set; }
        public string softwareVersion { get; set; }
        public long originAddressId { get; set; }
        public long destinationAddressId { get; set; }
        public string exchangeRateEffectiveDate { get; set; }
        public double exchangeRate { get; set; }
        public bool isSellerImporterOfRecord { get; set; }
        public string description { get; set; }
        public string email { get; set; }
        public string businessIdentificationNo { get; set; }
        public DateTime modifiedDate { get; set; }
        public int modifiedUserId { get; set; }
        public string taxDate { get; set; }
        public List<LineItemModel> lines { get; set; }
        public List<Address> addresses { get; set; }
        public List<LocationModel> locationTypes { get; set; }
        public List<Summary> summary { get; set; }
        public List<Message> messages { get; set; }
    }
}
