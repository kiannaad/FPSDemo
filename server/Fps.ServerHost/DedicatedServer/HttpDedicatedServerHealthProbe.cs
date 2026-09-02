using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fps.ServerHost.DedicatedServer;

public sealed class HttpDedicatedServerHealthProbe : IDedicatedServerHealthProbe
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient httpClient;

    public HttpDedicatedServerHealthProbe(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<DedicatedServerHealth?> ProbeAsync(
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(endpoint, cancellationToken);
        }
        catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        using (response)
        {
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<DedicatedServerHealth>(JsonOptions, cancellationToken);
        }
    }
}
