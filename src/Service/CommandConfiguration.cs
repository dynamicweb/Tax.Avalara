using System.Collections.Generic;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;

internal sealed class CommandConfiguration
{
    /// <summary>
    /// Create a log of the request and response from Avalara
    /// </summary>
    public bool DebugLog { get; set; }

    /// <summary>
    /// Avalara command. See operation urls in <see cref="AvalaraRequest"/> and <see cref="ApiCommand"/>
    /// </summary>
    public ApiCommand CommandType { get; set; }

    /// <summary>
    /// Command operator id, like https://.../{OperatorId}
    /// </summary>
    public string OperatorId { get; set; }

    /// <summary>
    /// Command operator id, like https://.../{OperatorId}/.../{OperatorSecondId}
    /// </summary>
    public string OperatorSecondId { get; set; }

    /// <summary>
    /// Data to serialize
    /// </summary>
    public object Data { get; set; }

    /// <summary>
    /// Query string parameters for GET request
    /// </summary>
    public Dictionary<string, string> QueryStringParameters { get; set; }
}
