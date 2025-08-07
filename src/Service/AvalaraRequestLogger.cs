using Dynamicweb.Logging;
using System;
using System.Net.Http;
using System.Text;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;

/// <summary>
/// Handles logging for Avalara requests and responses
/// </summary>
internal sealed class AvalaraRequestLogger
{
    private readonly StringBuilder _logBuilder;
    private readonly bool _debugEnabled;

    public AvalaraRequestLogger(bool debugEnabled)
    {
        _debugEnabled = debugEnabled;
        _logBuilder = debugEnabled ? new StringBuilder() : null;
    }

    public void InitializeLog(string apiUrl)
    {
        if (!_debugEnabled)
            return;

        _logBuilder.AppendLine("Avalara Interaction Log:");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine("--- BASE SERVICE URL ---");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine($"URL: {apiUrl}");
    }

    public void LogRequestInfo(string url)
    {
        if (!_debugEnabled)
            return;

        _logBuilder.AppendLine();
        _logBuilder.AppendLine("--- REQUEST ---");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine($"URL: {url}");
    }

    public void LogRequestData(string data)
    {
        if (!_debugEnabled)
            return;

        _logBuilder.AppendLine();
        _logBuilder.AppendLine("--- REQUEST DATA ---");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine(data);
    }

    public void LogResponse(HttpResponseMessage response, string responseText)
    {
        if (!_debugEnabled)
            return;

        _logBuilder.AppendLine();
        _logBuilder.AppendLine("--- RESPONSE ---");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine($"HttpStatusCode: {response.StatusCode} ({response.ReasonPhrase})");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine($"Response Text: {responseText}");
    }

    public void LogError(string errorMessage)
    {
        if (!_debugEnabled)
            return;

        _logBuilder.AppendLine();
        _logBuilder.AppendLine("--- HTTP ERROR ---");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine(errorMessage);
    }

    public void LogHttpRequestException(HttpRequestException requestException)
    {
        if (!_debugEnabled)
            return;

        _logBuilder.AppendLine();
        _logBuilder.AppendLine("--- EXCEPTION CAUGHT (HttpRequestException) ---");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine($"Message: {requestException.Message}");

        if (requestException.StatusCode.HasValue)
            _logBuilder.AppendLine($"StatusCode: {requestException.StatusCode}");

        _logBuilder.AppendLine($"Stack Trace: {requestException.StackTrace}");
    }

    public void LogUnhandledException(Exception exception)
    {
        if (!_debugEnabled)
            return;

        _logBuilder.AppendLine();
        _logBuilder.AppendLine($"--- UNEXPECTED EXCEPTION CAUGHT ({exception.GetType().Name}) ---");
        _logBuilder.AppendLine();
        _logBuilder.AppendLine($"Message: {exception.Message}");
        _logBuilder.AppendLine($"Stack Trace: {exception.StackTrace}");
    }

    public void FinalizeLog(ApiCommand commandType)
    {
        if (!_debugEnabled)
            return;

        _logBuilder.AppendLine();
        _logBuilder.AppendLine("--- END OF INTERACTION ---");

        string message = _logBuilder.ToString();

        if (commandType is not ApiCommand.ResolveAddress)
        {
            string fullName = typeof(AvalaraTaxProvider).FullName;
            LogManager.Current.GetLogger($"/eCom/TaxProvider/{fullName}").Info(message);
            LogManager.System.GetLogger("Provider", fullName).Info(message);
        }
        else
        {
            string fullName = typeof(AvalaraAddressValidatorProvider).FullName;
            LogManager.Current.GetLogger(string.Format("/eCom/AddressValidatorProvider/{0}", fullName)).Info(message);
            LogManager.System.GetLogger(LogCategory.Provider, fullName).Info(message);
        }
    }
}
