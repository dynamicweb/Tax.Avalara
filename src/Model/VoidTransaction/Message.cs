using System.Runtime.Serialization;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model.VoidTransaction;

[DataContract]
internal sealed class Message
{
    [DataMember(Name = "details")]
    public string Details { get; set; }

    [DataMember(Name = "helpLink")]
    public string HelpLink { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "refersTo")]
    public string RefersTo { get; set; }

    [DataMember(Name = "severity")]
    public string Severity { get; set; }

    [DataMember(Name = "source")]
    public string Source { get; set; }

    [DataMember(Name = "summary")]
    public string Summary { get; set; }
}
