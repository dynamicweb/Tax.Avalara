using Dynamicweb.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Dynamicweb.Ecommerce.TaxProviders.AvalaraTaxProvider.Service;

/// <summary>
/// Send request to Avalara and get related response.
/// </summary>
internal static class AvalaraRequest
{
    private const string JsonMediaType = "application/json";

    public static string SendRequest(string accountId, string licenseKey, string apiUrl, CommandConfiguration configuration)
    {
        using HttpMessageHandler messageHandler = CreateMessageHandler();
        using HttpClient client = CreateHttpClient(messageHandler, apiUrl, accountId, licenseKey);

        var logger = new AvalaraRequestLogger(configuration.DebugLog);
        logger.InitializeLog(apiUrl);

        try
        {
            return ExecuteRequest(client, configuration, logger);
        }
        catch (HttpRequestException requestException)
        {
            logger.LogHttpRequestException(requestException);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogUnhandledException(ex);
            throw;
        }
        finally
        {
            logger.FinalizeLog(configuration.CommandType);
        }
    }

    private static HttpMessageHandler CreateMessageHandler() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
    };

    private static string ExecuteRequest(HttpClient client, CommandConfiguration configuration, AvalaraRequestLogger logger)
    {
        string baseAddress = client.BaseAddress.ToString().TrimEnd('/');
        string apiCommand = GetCommandLink(baseAddress, configuration, true);
        LogRequestInfo(baseAddress, configuration, logger);

        Task<HttpResponseMessage> requestTask = configuration.CommandType switch
        {
            ApiCommand.ResolveAddress => client.GetAsync(apiCommand),
            ApiCommand.CreateTransaction or
            ApiCommand.VoidTransaction => client.PostAsync(apiCommand, GetStringContent(configuration, logger)),
            _ => throw new NotImplementedException($"Unknown operation was used. The operation code: {configuration.CommandType}.")
        };

        using HttpResponseMessage response = requestTask.GetAwaiter().GetResult();
        string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        logger.LogResponse(response, responseText);
        ValidateResponse(response, configuration, logger);

        return responseText;
    }

    private static void LogRequestInfo(string baseAddress, CommandConfiguration configuration, AvalaraRequestLogger logger)
    {
        if (!configuration.DebugLog)
            return;

        string readableUrl = GetCommandLink(baseAddress, configuration, false);
        logger.LogRequestInfo(readableUrl);
    }

    private static void ValidateResponse(HttpResponseMessage response, CommandConfiguration configuration, AvalaraRequestLogger logger)
    {
        if (response.IsSuccessStatusCode)
            return;

        string errorMessage = $"Command {configuration.CommandType} failed: {response.ReasonPhrase}.";
        logger.LogError(errorMessage);

        throw new AvalaraException(errorMessage, configuration.CommandType, response.StatusCode);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler, string apiUrl, string accountId, string licenseKey)
    {
        var client = new HttpClient(handler);

        client.BaseAddress = new Uri(apiUrl);
        client.Timeout = TimeSpan.FromSeconds(90);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));

        string authenticationParameter = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountId}:{licenseKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authenticationParameter);

        return client;
    }

    private static HttpContent GetStringContent(CommandConfiguration configuration, AvalaraRequestLogger logger)
    {
        if (configuration.DebugLog)
        {
            string serializedData = Converter.Serialize(configuration.Data);
            logger.LogRequestData(serializedData);
        }

        string content = Converter.SerializeCompact(configuration.Data);
        return new StringContent(content, Encoding.UTF8, JsonMediaType);
    }

    private static string GetCommandLink(string baseAddress, CommandConfiguration configuration, bool escapeParameters)
    {
        string gateway = configuration.CommandType switch
        {
            ApiCommand.CreateTransaction => "transactions/create",
            ApiCommand.ResolveAddress => "addresses/resolve",
            ApiCommand.VoidTransaction => $"companies/{configuration.OperatorId}/transactions/{configuration.OperatorSecondId}/void",
            _ => throw new NotImplementedException($"The api command is not supported. Command: {configuration.CommandType}")
        };

        string link = $"{baseAddress}/{gateway}";

        return AppendQueryParameters(link, configuration.QueryStringParameters, escapeParameters);
    }

    private static string AppendQueryParameters(string link, Dictionary<string, string> queryParameters, bool escapeParameters)
    {
        if (queryParameters?.Any() is not true)
            return link;

        var validParameters = queryParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value);

        if (!validParameters.Any())
            return link;

        IEnumerable<string> parameterStrings = validParameters.Select(param => FormatQueryParameter(param, escapeParameters));
        string queryString = string.Join("&", parameterStrings);

        return $"{link}?{queryString}";
    }

    private static string FormatQueryParameter(KeyValuePair<string, string> parameter, bool escapeParameters)
    {
        if (escapeParameters)
            return $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}";

        return $"{parameter.Key}={parameter.Value}";
    }
}