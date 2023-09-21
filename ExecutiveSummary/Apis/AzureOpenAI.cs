using ExecutiveSummary.Model;
using HtmlAgilityPack;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.Text;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ExecutiveSummary.Apis
{
    public class AzureOpenAI
    {
        private IConfiguration _config;
        private IKernel sk;
        private ILogger log;
        private ILoggerFactory loggerFactory;
        private BingUrlConnector bingConn;
  
        private HttpClient _client;
        public AzureOpenAI(BingUrlConnector bingConn, IConfiguration config, ILoggerFactory loggerFactory)
        {
            _config = config;
            sk = new KernelBuilder().WithLogger(loggerFactory.CreateLogger<Kernel>()).Build();
            log = loggerFactory.CreateLogger<AzureOpenAI>();
            this.bingConn = bingConn;
            this.loggerFactory = loggerFactory;
            InitSkills();
        }

        private bool InitSkills()
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _client = new HttpClient(handler);

            AzureOpenAIConfiguration? azureOpenAIConfiguration = _config.GetSection("AzureOpenAI:Chat").Get<AzureOpenAIConfiguration>();
            if (azureOpenAIConfiguration != null)
            {
                sk = new KernelBuilder()
                   .WithAzureChatCompletionService(azureOpenAIConfiguration.DeploymentName, azureOpenAIConfiguration.Endpoint, azureOpenAIConfiguration.ApiKey, true)
                   .WithLogger(loggerFactory.CreateLogger<Kernel>()).Build();
            }

            var skillsDir = _config["Skills:Directory"];
            if (skillsDir == null)
            {
                string currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                skillsDir = Path.GetFullPath(Path.Combine(currentAssemblyDirectory, "skills"));
            }

            sk.ImportSemanticSkillFromDirectory(skillsDir, "SummarizeSkill", "CompanySkill");

            WebSearchEngineSkill bing = new(bingConn);
            sk.ImportSkill(bing, "WebSearchEngine");

            return true;
        }

        public async Task<(List<Executive>, string)> GetCompanyExecutives(string companyName)
        {
            try
            {
                List<string> paragraphs;
                string url;
                int maxTokens = 1000;
                if (!companyName.IsUrl())
                {
                    var prompt = $"Get the current list of executives for {companyName}";
                    (paragraphs, url) = await GetBingResults(prompt, maxTokens);
                }
                else
                {
                    url = companyName;
                    paragraphs = await GetParagraphedWebResults(url, maxTokens);
                }

                var executives = new List<Executive>();
                List<Task<SKContext>> execTasks = new();
                foreach (var p in paragraphs)
                {
                    execTasks.Add(sk.RunAsync(p, this.sk.Skills.GetFunction("CompanySkill", "ExecutiveList")));
                }

                var results = await Task.WhenAll(execTasks);
                foreach (var resp in results)
                {
                    try
                    {
                        var tmp = JsonSerializer.Deserialize<List<Executive>>(resp.Result);
                        executives.AddRange(tmp);
                    }
                    catch { }
                }

                executives = executives.DistinctBy(n => n.Name).ToList();
                return (executives, url);
            }
            catch (Exception exe)
            {
                return (new List<Executive>(), "");
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
            (var paragraphs, var url) = await GetBingResults(prompt, 1000);
            int count = 0;
            foreach (var p in paragraphs)
            {
                if (count > 3)
                {
                    break;
                }
                var ctxVars = new ContextVariables();
                ctxVars.Set("execName", exec.Name);
                ctxVars.Set("companyName", companyName);
                ctxVars.Set("personInfo", p);

                var resp = await sk.RunAsync(ctxVars, this.sk.Skills.GetFunction("CompanySkill", "ExecutiveBios"));
                try
                {
                    log.LogInformation($"[ExecutiveBios result]:  {resp.Result}");
                    var strRes = resp.Result;
                    strRes = "{" + strRes.Substring(strRes.IndexOf("\"name\""));
                    var tmp = JsonSerializer.Deserialize<Executive>(strRes);
                    if (string.IsNullOrWhiteSpace(exec.Bio) && !string.IsNullOrWhiteSpace(tmp.Bio))
                    {
                        exec.Bio = tmp.Bio;
                        exec.References.AddRange(tmp.References);
                        exec.References.Add(url);
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
            (var paragraphs, var url) = await GetBingResults(prompt, 1000);
            int count = 0;
            foreach (var p in paragraphs)
            {
                if (count > 1)
                {
                    break;
                }
                var ctxVars = new ContextVariables();
                ctxVars.Set("execName", exec.Name);
                ctxVars.Set("companyName", companyName);
                ctxVars.Set("personInfo", p);

                var resp = await sk.RunAsync(ctxVars, this.sk.Skills.GetFunction("CompanySkill", "ExecutivePriorities"));
                try
                {
                    log.LogInformation($"[ExecutivePriority result]:  {resp.Result}");
                    var strRes = resp.Result;
                    strRes = "{" + strRes.Substring(strRes.IndexOf("\"name\""));
                    var tmp = JsonSerializer.Deserialize<Executive>(strRes);
                    exec.Priorities.AddRange(tmp.Priorities.Take(5));
                    exec.References.AddRange(tmp.References.Take(5));
                    exec.References.Add(url);
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
            WebSearchEngineSkill bing = new(bingConn);
            var bingMultiSkill = sk.ImportSkill(bing, "WebSearchEngineSkill");
            var prompt = $"Find articles regarding {companyName} on the following topic: {meetingTopic}.";
            ContextVariables ctx = new ContextVariables();
            ctx.Set("input", prompt);
            ctx.Set("count", articleCount.ToString());

            SKContext result = await sk.RunAsync(ctx, bingMultiSkill["Search"]);
            var articles = result.Result.Split(',').ToList().Select(l => new Article() { Url = l }).ToList();

            return await GetArticleSummaries(articles);
        }
        public async Task<List<Article>> GetArticleSummaries(List<Article> urls)
        {
            foreach (var url in urls)
            {
                if (!string.IsNullOrWhiteSpace(url.Url))
                {
                    SKContext result = await sk.RunAsync(url.Url, this.sk.Skills.GetFunction("CompanySkill", "NewsSummary"));
                    url.Summary = result.Result;
                }
            }
            return urls;
        }
        public async Task<string> Get10KInsights(string companyName)
        {

            var prompt = $"site:www.sec.gov {companyName} 10K";
            (var paragraphs, var url) = await GetBingResults(prompt, 1000);

            var sb = new StringBuilder();
            foreach (var paragraph in paragraphs)
            {
                var res2 = await sk.RunAsync(paragraph, this.sk.Skills.GetFunction("CompanySkill", "10KSummary"));
                sb.AppendLine(res2.Result);
            }
            SKContext res = await sk.RunAsync(sb.ToString(), this.sk.Skills.GetFunction("CompanySkill", "10KSummary"));
            return res.Result;

        }
        public async Task<string> GetQuarterlyStatementSummary(string companyName)
        {
            var prompt = $"Latest quarterly earnings report for {companyName} in {DateTime.Now.Year.ToString()}";
            (var paragraphs, var url) = await GetBingResults(prompt, 1000);

            var sb = new StringBuilder();
            foreach (var paragraph in paragraphs)
            {
                var res2 = await sk.RunAsync(paragraph, this.sk.Skills.GetFunction("CompanySkill", "QuarterlyResults"));
                return res2.Result;
                //sb.AppendLine(res2.Result);
            }
            SKContext res = await sk.RunAsync(sb.ToString(), this.sk.Skills.GetFunction("CompanySkill", "QuarterlyResults"));
            return res.Result;
        }
        private async Task<(List<string>, string)> GetBingResults(string prompt, int maxTokens)
        {
            try
            {
                log.LogInformation($"Bing Prompt: '{prompt}'");

                WebSearchEngineSkill bing = new(bingConn);
                var bingSkill = sk.ImportSkill(bing, "WebSearchEngine");
                var resp = await sk.RunAsync(prompt, bingSkill["Search"]);
                var url = resp.Result;
                var paragraphs = await GetParagraphedWebResults(url, maxTokens);
                return (paragraphs, url);
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred in method GetBingResults: {ex.Message}");
                return (new List<string>(), string.Empty);
            }
        }



        async Task<List<string>> GetParagraphedWebResults(string url, int maxTokens)
        {
            try
            {
                var util = new HtmlWeb();
                var webContent = await util.LoadFromWebAsync(url);
                var textContent = webContent.DocumentNode.InnerText;
                return GetChunkedContent(textContent, maxTokens);

            }
            catch (Exception exe)
            {
                log.LogError(exe, $"Failed to get web results for '{url}': {exe.Message}");
                return new List<string>();
            }
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
                // log.LogInformation($"Trimmed Web Result: {trimmed}");

                var paragraphs = TextChunker.SplitPlainTextParagraphs(trimmed, maxTokens);

                //if (log.IsEnabled(LogLevel.Information))
                //{
                //    log.LogInformation("Extracted Split Text:");
                //    for (int i = 0; i < paragraphs.Count; i++)
                //    {
                //        log.LogInformation($"[{i}]:  {paragraphs[i]}");
                //    }
                //}
                return paragraphs;
            }
            catch (Exception exe)
            {
                //log.LogError(exe, $"Failed to chunk text: {exe.Message}");
                return new List<string>();
            }
        }
    }
}
