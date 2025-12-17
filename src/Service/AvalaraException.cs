using System;
using System.Net;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;

/// <summary>
/// Custom exception for Avalara API errors
/// </summary>
internal sealed class AvalaraException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public ApiCommand Command { get; }

    public AvalaraException(string message, ApiCommand command, HttpStatusCode? statusCode)
        : base(message)
    {
        Command = command;
        StatusCode = statusCode;
    }

    public AvalaraException(string message, ApiCommand command, Exception innerException)
        : base(message, innerException)
    {
        Command = command;
    }
}
