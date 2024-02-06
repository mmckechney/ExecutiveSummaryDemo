using ExecutiveSummary.Model;
using HtmlAgilityPack;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Text;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Humanizer.Localisation;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using static Microsoft.Graph.Constants;
using System.Text.RegularExpressions;

#pragma warning disable SKEXP0055 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace ExecutiveSummary.Apis
{
   public class AzureOpenAI
   {
      private IConfiguration _config;
      private Kernel sk;
      private ILogger log;
      private ILoggerFactory loggerFactory;
      private BingUrlConnector bingConn;
      private int maxTokens = 25000;
      private Regex urlRegex = new Regex(@"http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&+]|[!*\\(\\),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+");
      Dictionary<string, KernelFunction> yamlPrompts = new();

      private HttpClient _client;
      public AzureOpenAI(BingUrlConnector bingConn, IConfiguration config, ILoggerFactory loggerFactory)
      {
         _config = config;
         log = loggerFactory.CreateLogger<AzureOpenAI>();
         this.bingConn = bingConn;
         this.loggerFactory = loggerFactory;
         InitYamlPrompts();
      }


      private void InitYamlPrompts()
      {

         HttpClientHandler handler = new HttpClientHandler()
         {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
         };
         _client = new HttpClient(handler);

         AzureOpenAIConfiguration? azureOpenAIConfiguration = _config.GetSection("AzureOpenAI:Chat").Get<AzureOpenAIConfiguration>();
         if (azureOpenAIConfiguration != null)
         {
            var builder = Kernel.CreateBuilder()
               .AddAzureOpenAIChatCompletion(deploymentName: azureOpenAIConfiguration.DeploymentName,
                   endpoint: azureOpenAIConfiguration.Endpoint,
                   apiKey: azureOpenAIConfiguration.ApiKey,
                   modelId: azureOpenAIConfiguration.ModelName);
            builder.Services.AddSingleton(loggerFactory);

            sk = builder.Build();
         }

         WebSearchEnginePlugin bing = new(bingConn);
         sk.ImportPluginFromObject(bing, "WebSearchEngine");

         var assembly = Assembly.GetExecutingAssembly();
         var resources = assembly.GetManifestResourceNames().ToList();
         resources.ForEach(r =>
         {
            if(r.ToLower().EndsWith("yaml"))
            {
               var count = r.Split('.').Count();
               var key = count > 3 ? $"{r.Split('.')[count - 3]}.{r.Split('.')[count - 2]}" : r.Split('.')[count - 2];
               using StreamReader reader = new(Assembly.GetExecutingAssembly().GetManifestResourceStream(r)!);
               var func = sk.CreateFunctionFromPromptYaml(reader.ReadToEnd(), promptTemplateFactory: new HandlebarsPromptTemplateFactory());
               yamlPrompts.Add(key, func);
            }
         });

      }

      public async Task<(List<Executive> executives, List<string> urls)> GetCompanyExecutives(string companyName)
      {
         try
         {
            List<string> paragraphs;
            List<string> urls;
            if (!companyName.IsUrl())
            {
               var prompt = $"List of the current executives for {companyName}";
               (paragraphs, urls) = await GetBingResults(prompt, maxTokens, 3);
            }
            else
            {
               urls = new List<string>() { companyName };
               paragraphs = await GetParagraphedWebResults(urls, maxTokens);
            }


            var executives = new List<Executive>();
            List<Task<FunctionResult>> execTasks = new();
            foreach (var p in paragraphs)
            {
               execTasks.Add(sk.InvokeAsync(yamlPrompts["Company.ExecutiveList"], new() { { "input", p }, { "companyName", companyName } }));
            }
            var results = await Task.WhenAll(execTasks);
            foreach (var resp in results)
            {
               try
               {
                  var tmp = JsonSerializer.Deserialize<List<Executive>>(resp.GetValue<string>());
                  executives.AddRange(tmp);
               }
               catch { }
            }

            executives = executives.DistinctBy(n => n.Name).ToList();
            return (executives, urls);
         }
         catch (Exception exe)
         {
            return (new(), new());
         }

      }




      public async Task<List<Executive>> GetExecutiveBios(List<Executive> selectedExecs, string companyName)
      {
         List<Task<Executive>> bioTasks = new();
         foreach (var exec in selectedExecs)
         {
            bioTasks.Add(ExtractBiosAndPriorities(exec, companyName));
         }
         var results = await Task.WhenAll(bioTasks);
         selectedExecs = results.ToList();

         selectedExecs = await ValidateReferenceLinks(selectedExecs);


         return selectedExecs;
      }
      private async Task<Executive> ExtractBiosAndPriorities(Executive exec, string companyName)
      {
         List<Task<Executive>> extractTasks = new();
         extractTasks.Add(ExtractBioAsync(exec, companyName));
         extractTasks.Add(ExtractPrioritiesAsync(exec, companyName));
         var results = await Task.WhenAll(extractTasks);
         exec.Bio = results[0].Bio;
         exec.Priorities = results[1].Priorities;

         return exec;
      }
      private async Task<Executive> ExtractBioAsync(Executive exec, string companyName)
      {
         var prompt = $"Biography for {companyName} executive {exec.Name}";
         (var paragraphs, var urls) = await GetBingResults(prompt, maxTokens, 3);
         int count = 0;
         foreach (var p in paragraphs)
         {
            if (count > 3)
            {
               break;
            }
            var resp = await sk.InvokeAsync(yamlPrompts["Company.ExecutiveBios"], new() { { "execName",exec.Name}, { "companyName", companyName}, { "personInfo", p} });
            try
            {
               log.LogInformation($"[ExecutiveBios result]:  {resp.GetValue<string>()}");
               var strRes = resp.GetValue<string>();
               strRes = "{" + strRes.Substring(strRes.IndexOf("\"name\""));
               var tmp = JsonSerializer.Deserialize<Executive>(strRes.RemoveJsonDecoration());
               if (string.IsNullOrWhiteSpace(exec.Bio) && !string.IsNullOrWhiteSpace(tmp.Bio))
               {
                  exec.Bio = tmp.Bio;
                  exec.References.AddRange(tmp.References);
                  exec.References.AddRange(urls);
                  break;
               }

            }
            catch { }
            count++;
         }

         return exec;
      }
      private async Task<Executive> ExtractPrioritiesAsync(Executive exec, string companyName)
      {
         var prompt = $"Business priorities for {companyName} executive {exec.Name}";
         (var paragraphs, var urls) = await GetBingResults(prompt, maxTokens, 3);
         int count = 0;
         foreach (var p in paragraphs)
         {
            if (count > 1)
            {
               break;
            }
            var resp = await sk.InvokeAsync(yamlPrompts["Company.ExecutivePriorities"], new() { { "execName", exec.Name }, { "companyName", companyName }, { "personInfo", p } });
            try
            {
               log.LogInformation($"[ExecutivePriority result]:  {resp.GetValue<string>()}");
               var strRes = resp.GetValue<string>();
               strRes = "{" + strRes.Substring(strRes.IndexOf("\"name\""));
               var tmp = JsonSerializer.Deserialize<Executive>(strRes.RemoveJsonDecoration());
               exec.Priorities.AddRange(tmp.Priorities.Take(5));
               exec.References.AddRange(tmp.References.Take(5));
               exec.References.AddRange(urls);
            }
            catch { }
            count++;
         }

         return exec;
      }

      private async Task<List<Executive>> ValidateReferenceLinks(List<Executive> selectedExecs)
      {
         List<Task<Executive>> refTasks = new();
         foreach (var exec in selectedExecs)
         {
            refTasks.Add(ValidateReferences(exec));
         }
         var results = await Task.WhenAll(refTasks);
         return results.ToList();
      }
      private async Task<Executive> ValidateReferences(Executive exec)
      {

         var references = exec.References.Distinct().ToList();

         for (int i = 0; i < references.Count; i++)
         {
            try
            {
               var res = await _client.GetAsync(references[i]);
               if (!res.IsSuccessStatusCode)
               {
                  log.LogWarning($"Invalid reference link: {references[i]}");
                  references[i] = "";
               }
            }
            catch (Exception exe)
            {
               log.LogWarning(exe, $"Problem validating reference link: {references[i]}");
               references[i] = "";
            }
         }

         exec.References = references.Where(r => !string.IsNullOrEmpty(r)).ToList();
         return exec;
      }

      public async Task<List<Article>> GetMeetingTopicNews(string companyName, string meetingTopic, int articleCount = 3)
      {
         var prompt = $"Find articles regarding {companyName} on the following topic: {meetingTopic}.";
         var args = new KernelArguments();
         args.Add("query", prompt);
         args.Add("count", articleCount.ToString());

         var result = await sk.InvokeAsync("WebSearchEngine", "Search", args);
         var lst = JsonSerializer.Deserialize<List<string>>(result.GetValue<string>());

         var articles = lst.Select(l => new Article() { Url = l.Split('|')[1], Title = l.Split('|')[0] }).ToList();

         return await GetArticleSummaries(articles);
      }
      public async Task<List<Article>> GetArticleSummaries(List<Article> urls)
      {
         List<Task<FunctionResult>> tasks = new();
         foreach (var url in urls)
         {
            if (!string.IsNullOrWhiteSpace(url.Url))
            {
               tasks.Add(sk.InvokeAsync(yamlPrompts["Company.NewsSummary"], new() { { "input", url.Url }, { "pageUrl", url.Url } }));
            }
         }
         var results = await Task.WhenAll(tasks);

         var summaries = new List<Article>();

         foreach (var res in results)
         {
            summaries.Add(new Article() { Summary = res?.GetValue<string>() });
         }
         return summaries;
      }
       public async Task<string> GetQuarterlyStatementSummary(string companyName)
      {
         var prompt = $"What is the latest quarterly earnings report for {companyName} in {DateTime.Now.Year.ToString()}";
         (var paragraphs, var url) = await GetBingResults(prompt, maxTokens, 3);

         var sb = new StringBuilder();
         foreach (var paragraph in paragraphs)
         {
            var res2 = await sk.InvokeAsync(yamlPrompts["Company.QuarterlyResults"], new() { { "input", paragraph } });
            return res2.GetValue<string>();
         }
         var res = await sk.InvokeAsync(yamlPrompts["Company.QuarterlyResults"], new() { { "input", sb.ToString() } });
         return res.GetValue<string>();
      }
      private async Task<(List<string>, List<string>)> GetBingResults(string prompt, int maxTokens, int resultCount = 5)
      {
         try
         {
            log.LogInformation($"Bing Prompt: '{prompt}'");

            var resp = await sk.InvokeAsync("WebSearchEngine", "Search", new() { { "query", prompt }, { "count", resultCount } });
            var lst = JsonSerializer.Deserialize<List<string>>(resp.GetValue<string>());

            var urls = lst.Select(l => urlRegex.Match(l).Value).ToList();
            var paragraphs = await GetParagraphedWebResults(urls, maxTokens);
            return (paragraphs, urls);
         }
         catch (Exception ex)
         {
            log.LogError($"An error occurred in method GetBingResults: {ex.Message}");
            return (new(), new());
         }
      }



      async Task<List<string>> GetParagraphedWebResults(List<string> urls, int maxTokens)
      {
         CancellationTokenSource src = new CancellationTokenSource();
         src.CancelAfter(10000);

         List<string> chunked = new();
         List<Task<HtmlDocument?>> tasks = new();
         List<HtmlDocument> contents = new();
         foreach (var url in urls)
         {
            try
            {
               var util = new HtmlWeb();
               tasks.Add(util.LoadFromWebAsync(url, src.Token));
            }
            catch (Exception exe)
            {
               log.LogError(exe, $"Failed to get web results for '{url}': {exe.Message}");
            }
         }

         var resultTask = Task.WhenAll(tasks.ToArray());
         try
         {
            contents.AddRange(await resultTask);
         }
         catch (Exception exe)
         {
            log.LogError($"Error getting web results for '{exe.Message}'");
         }
         tasks.Where(t => t.Status == TaskStatus.RanToCompletion).ToList().ForEach(t =>
         {
            try
            {
               contents.Add(t.Result);
            }
            catch (Exception exe)
            {
               log.LogError(exe, $"Error getting web results for '{exe.Message}'");
            }
         });   

         foreach (var res in contents)
         {
            chunked.AddRange(GetChunkedContent(res.DocumentNode.InnerText, maxTokens));
         }

         return chunked;
      }
        
      
      async Task<List<string>> GetParagraphedWebResults(string url, int maxTokens)
      {
         return await GetParagraphedWebResults(new List<string>() { url }, maxTokens);
      }
      internal static List<string> GetChunkedContent(string textContent, int maxTokens)
      {
         try
         {
            var listContent = textContent.Replace("\r", "").Split("\n", StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> trimmed = new();
            listContent.ForEach(t =>
            {
               if (!string.IsNullOrWhiteSpace(t))
                  trimmed.Add(t);
            });

            var paragraphs = TextChunker.SplitPlainTextParagraphs(trimmed, maxTokens);

            return paragraphs;
         }
         catch (Exception)
         {
            return new List<string>();
         }
      }
   }
}
