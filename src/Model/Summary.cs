namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Model
{
    internal class Summary
    {
        public string country { get; set; }
        public string region { get; set; }
        public string jurisType { get; set; }
        public string jurisCode { get; set; }
        public string jurisName { get; set; }
        public int taxAuthorityType { get; set; }
        public string stateAssignedNo { get; set; }
        public string taxType { get; set; }
        public string taxSubType { get; set; }
        public string taxName { get; set; }
        public string rateType { get; set; }
        public double taxable { get; set; }
        public double rate { get; set; }
        public double tax { get; set; }
        public double taxCalculated { get; set; }
        public double nonTaxable { get; set; }
        public double exemption { get; set; }
    }
}
