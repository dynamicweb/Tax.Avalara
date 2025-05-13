using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionRequest;
using Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.CreateTransactionResponse;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.ResolveAddressResponse;

[DataContract]
internal sealed class ResolveAddressResponse
{
    [DataMember(Name = "address")]
    public AddressLocationInfo Address { get; set; }

    [DataMember(Name = "validatedAddresses")]
    public IEnumerable<ValidatedAddressInfo> ValidatedAddresses { get; set; }

    [DataMember(Name = "messages")]
    public IEnumerable<AvaTaxMessage> Messages { get; set; }
}