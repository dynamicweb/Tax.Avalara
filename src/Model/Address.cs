namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model
{
    internal class Address
    {
        public object id { get; set; }
        public object transactionId { get; set; }
        public string boundaryLevel { get; set; }
        public string line1 { get; set; }
        public string line2 { get; set; }
        public string line3 { get; set; }
        public string city { get; set; }
        public string region { get; set; }
        public string postalCode { get; set; }
        public string country { get; set; }
        public int taxRegionId { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }
    }
}
