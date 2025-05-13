using Dynamicweb.Core;
using Dynamicweb.Logging;
using System;
using System.Collections.Generic;
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
    public static string SendRequest(string accountId, string licenseKey, string apiUrl, CommandConfiguration configuration)
    {
        using (var messageHandler = GetMessageHandler())
        {
            using (var client = new HttpClient(messageHandler))
            {
                client.BaseAddress = new Uri(apiUrl);
                client.Timeout = new TimeSpan(0, 0, 0, 90);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string authenticationParameter = Convert.ToBase64String(Encoding.Default.GetBytes($"{accountId}:{licenseKey}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authenticationParameter);

                string apiCommand = GetCommandLink(apiUrl, configuration.CommandType, configuration.OperatorId, configuration.OperatorSecondId, configuration.QueryStringParameters);
                Task<HttpResponseMessage> requestTask = configuration.CommandType switch
                {
                    //GET
                    ApiCommand.ResolveAddress => client.GetAsync(apiCommand),
                    //POST
                    ApiCommand.CreateTransaction or
                    ApiCommand.VoidTransaction => client.PostAsync(apiCommand, GetContent()),
                    _ => throw new NotSupportedException($"Unknown operation was used. The operation code: {configuration.CommandType}.")
                };

                try
                {
                    using (HttpResponseMessage response = requestTask.GetAwaiter().GetResult())
                    {
                        string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (configuration.DebugLog)
                        {
                            var logText = new StringBuilder("Remote server response:");
                            logText.AppendLine($"HttpStatusCode = {response.StatusCode}");
                            logText.AppendLine($"HttpStatusDescription = {response.ReasonPhrase}");
                            logText.AppendLine($"Response Text: {responseText}");

                            if (configuration.CommandType is ApiCommand.ResolveAddress)
                                LogAddressValidator(logText.ToString());
                            else
                                Log(logText.ToString());
                        }

                        if (!response.IsSuccessStatusCode)
                            throw new Exception($"Unhandled exception. Operation failed: {response.ReasonPhrase}. Response text: ${responseText}");

                        return responseText;
                    }
                }
                catch (HttpRequestException requestException)
                {
                    throw new Exception($"An error occurred during Avalara request. Error code: {requestException.StatusCode}");
                }
            }
        }

        HttpMessageHandler GetMessageHandler() => new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
        };

        HttpContent GetContent()
        {
            string content = Converter.SerializeCompact(configuration.Data);

            return new StringContent(content, Encoding.UTF8, "application/json");
        }
    }

    private static string GetCommandLink(string baseAddress, ApiCommand command, string operatorId, string operatorSecondId, Dictionary<string, string> queryParameters)
    {
        return command switch
        {
            ApiCommand.CreateTransaction => GetCommandLink("transactions/create"),
            ApiCommand.ResolveAddress => GetCommandLink("addresses/resolve", queryParameters),
            ApiCommand.VoidTransaction => GetCommandLink($"companies/{operatorId}/transactions/{operatorSecondId}/void"),
            _ => throw new NotSupportedException($"The api command is not supported. Command: {command}")
        };

        string GetCommandLink(string gateway, Dictionary<string, string> queryParameters = null)
        {
            string link = $"{baseAddress}/{gateway}";

            if (queryParameters?.Count is 0 or null)
                return link;

            string parameters = string.Join("&", queryParameters.Select(parameter => $"{parameter.Key}={parameter.Value}"));

            return $"{link}?{parameters}";
        }
    }

    private static void Log(string message)
    {
        string fullName = typeof(AvalaraTaxProvider).FullName;
        LogManager.Current.GetLogger($"/eCom/TaxProvider/{fullName}").Info(message);
        LogManager.System.GetLogger("Provider", fullName).Info(message);
    }

    private static void LogAddressValidator(string message)
    {
        string name = typeof(AvalaraAddressValidatorProvider).FullName ?? "AddressValidationProvider";
        LogManager.Current.GetLogger(string.Format("/eCom/AddressValidatorProvider/{0}", name)).Info(message);
        LogManager.System.GetLogger(LogCategory.Provider, name).Info(message);
    }
}
