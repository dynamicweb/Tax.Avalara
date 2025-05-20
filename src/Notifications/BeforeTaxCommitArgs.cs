using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Extensibility.Notifications;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Notifications;

/// <summary>
/// Before tax commit arguments
/// </summary>
public class BeforeTaxCommitArgs : CancelableNotificationArgs
{
    public Order Order { get; set; }
}
