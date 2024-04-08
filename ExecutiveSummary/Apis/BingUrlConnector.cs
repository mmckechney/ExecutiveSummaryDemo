using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Plugins.Web;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExecutiveSummary.Apis
{
   public class BingUrlConnector : IWebSearchEngineConnector, IDisposable
   {
      internal readonly ILogger _logger;
      internal readonly HttpClientHandler _httpClientHandler;
      internal readonly HttpClient _httpClient;
      internal IConfiguration _config;
      public BingUrlConnector(IConfiguration config, ILoggerFactory loggerFactory) //: this("",logger)
      {
         _logger = loggerFactory.CreateLogger<BingUrlConnector>();
         _config = config;

         var apiKey = _config["Plugins:BingApiKey"];
         _logger = _logger ?? NullLogger<BingUrlConnector>.Instance;
         _httpClientHandler = new() { CheckCertificateRevocationList = true };
         _httpClient = new HttpClient(_httpClientHandler);
         _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

      }

      public virtual async Task<IEnumerable<T>> SearchAsync<T>(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
      {
         string freshnessFilter = $"{DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd")}..{DateTime.Now.ToString("yyyy-MM-dd")}";
         Uri uri = new($"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={count}&freshness={freshnessFilter}&responseFilter=Webpages");

         _logger.LogDebug("Sending request: {0}", uri);
         HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken);
         response.EnsureSuccessStatusCode();
         _logger.LogDebug("Response received: {0}", response.StatusCode);

         string json = await response.Content.ReadAsStringAsync();
         _logger.LogTrace("Response content received: {0}", json);

         BingSearchResponse? data = JsonSerializer.Deserialize<BingSearchResponse>(json);
         var urls = data?.WebPages?.Value?.Take(count).ToList();

         urls.ForEach(u =>
         {
            _logger.LogInformation($"Result: {u.Name}, {u.Url}, {u.Snippet}");
         });

         IEnumerable<T> result = urls.Select(u => $"{u.Name}|{u.Url}") as IEnumerable<T>;
         return result;
      }
      /// <inheritdoc/>
      //public virtual async Task<IEnumerable<string>> SearchAsync(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
      //  {
      //      string freshnessFilter = $"{DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd")}..{DateTime.Now.ToString("yyyy-MM-dd")}";
      //      Uri uri = new($"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={count}&freshness={freshnessFilter}&responseFilter=Webpages");

      //      _logger.LogDebug("Sending request: {0}", uri);
      //      HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken);
      //      response.EnsureSuccessStatusCode();
      //      _logger.LogDebug("Response received: {0}", response.StatusCode);

      //      string json = await response.Content.ReadAsStringAsync();
      //      _logger.LogTrace("Response content received: {0}", json);

      //      BingSearchResponse? data = JsonSerializer.Deserialize<BingSearchResponse>(json);
      //      var urls = data?.WebPages?.Value?.Take(count).ToList();

      //      urls.ForEach(u =>
      //      {
      //          _logger.LogInformation($"Result: {u.Name}, {u.Url}, {u.Snippet}");
      //      });


      //      return urls.Select(u => $"{u.Name}|{u.Url}").ToList() ?? new List<string>();
      //  }

      protected virtual void Dispose(bool disposing)
      {
         if (disposing)
         {
            _httpClient.Dispose();
            _httpClientHandler.Dispose();
         }
      }

      public void Dispose()
      {
         // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
         Dispose(disposing: true);
         GC.SuppressFinalize(this);
      }

      [SuppressMessage("Performance", "CA1812:Internal class that is apparently never instantiated",
          Justification = "Class is instantiated through deserialization.")]
      internal sealed class BingSearchResponse
      {
         [JsonPropertyName("webPages")]
         public WebPages? WebPages { get; set; }
      }

      [SuppressMessage("Performance", "CA1812:Internal class that is apparently never instantiated",
          Justification = "Class is instantiated through deserialization.")]
      internal sealed class WebPages
      {
         [JsonPropertyName("value")]
         public WebPage[]? Value { get; set; }
      }

      [SuppressMessage("Performance", "CA1812:Internal class that is apparently never instantiated",
          Justification = "Class is instantiated through deserialization.")]
      internal sealed class WebPage
      {
         [JsonPropertyName("name")]
         public string Name { get; set; } = string.Empty;

         [JsonPropertyName("url")]
         public string Url { get; set; } = string.Empty;

         [JsonPropertyName("snippet")]
         public string Snippet { get; set; } = string.Empty;
      }
   }

}
