using System.Text.Json;

namespace ExecutiveSummary.Apis
{
    public class BingMultiUrlConnector : BingUrlConnector
    {

        public BingMultiUrlConnector(IConfiguration config, ILoggerFactory loggerFactory) : base(config, loggerFactory)
        {

        }


        /// <inheritdoc/>
        public override async Task<IEnumerable<string>> SearchAsync(string query, int count = 3, int offset = 0, CancellationToken cancellationToken = default)
        {
            Uri uri = new($"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={count}&responseFilter=Webpages");

            _logger.LogDebug("Sending request: {0}", uri);
            HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Response received: {0}", response.StatusCode);

            string json = await response.Content.ReadAsStringAsync();
            _logger.LogTrace("Response content received: {0}", json);

            BingSearchResponse? data = JsonSerializer.Deserialize<BingSearchResponse>(json);
            List<WebPage>? results = data?.WebPages?.Value?.Take(count).ToList();

            var urls = results?.Select(x => x.Url).ToList();

            return urls.Count() == 0 ? new List<string>() : urls;
        }

    }

}

