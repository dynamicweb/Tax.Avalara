using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Extensibility.Notifications;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Notifications;

/// <summary>
/// Args class to get the customer code to send to Avalara.
/// </summary>
public class OnGetCustomerCodeArgs : NotificationArgs
{
    public Order Order { get; set; }

    public string CustomerCode { get; set; }
}
