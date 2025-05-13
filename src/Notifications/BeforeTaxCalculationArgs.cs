using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Extensibility.Notifications;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Notifications;

/// <summary>
/// Before tax calculation arguments
/// </summary>
public class BeforeTaxCalculationArgs : CancelableNotificationArgs
{
    public Order Order { get; set; }
}
