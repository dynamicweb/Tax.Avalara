using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionResponse;

[DataContract]
internal sealed class TransactionLineDetail
{
    [DataMember(Name = "id")]
    public long? Id { get; set; }

    [DataMember(Name = "transactionLineId")]
    public long? TransactionLineId { get; set; }

    [DataMember(Name = "transactionId")]
    public long? TransactionId { get; set; }

    [DataMember(Name = "tax")]
    public double? Tax { get; set; }

    [DataMember(Name = "taxCalculated")]
    public double? TaxCalculated { get; set; }

    [DataMember(Name = "taxName")]
    public string TaxName { get; set; }

    [DataMember(Name = "jurisName")]
    public string JurisName { get; set; }
}
