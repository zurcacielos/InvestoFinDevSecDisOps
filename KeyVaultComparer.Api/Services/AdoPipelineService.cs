using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using KeyVaultComparer.Api.Models;

namespace KeyVaultComparer.Api.Services
{
    public class AdoPipelineService
    {
        private readonly TokenCredential _credential;
        private readonly IHttpClientFactory _httpClientFactory;

        public AdoPipelineService(TokenCredential credential, IHttpClientFactory httpClientFactory)
        {
            _credential = credential;
            _httpClientFactory = httpClientFactory;
        }

        private async Task<string> GetAdoTokenAsync(CancellationToken cancellationToken = default)
        {
            var tokenContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
            var token = await _credential.GetTokenAsync(tokenContext, cancellationToken);
            return token.Token;
        }

        public async Task<List<PipelineSummary>> GetPipelinesAsync(string organization, string project, CancellationToken cancellationToken = default)
        {
            var token = await GetAdoTokenAsync(cancellationToken);
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"https://dev.azure.com/{organization}/{project}/_apis/build/definitions?api-version=7.1";
            var response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"ADO API Error ({response.StatusCode}): {errContent}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var root = JsonNode.Parse(content);
            var valueArray = root?["value"]?.AsArray();

            var pipelines = new List<PipelineSummary>();
            if (valueArray != null)
            {
                foreach (var item in valueArray)
                {
                    if (item == null) continue;

                    var summary = new PipelineSummary
                    {
                        Id = item["id"]?.GetValue<int>() ?? 0,
                        Name = item["name"]?.GetValue<string>() ?? string.Empty,
                        Path = item["path"]?.GetValue<string>()
                    };

                    var repoNode = item["repository"];
                    if (repoNode != null)
                    {
                        summary.Repository = new PipelineRepository
                        {
                            Id = repoNode["id"]?.ToString(),
                            Name = repoNode["name"]?.ToString(),
                            Url = repoNode["url"]?.ToString(),
                            Type = repoNode["type"]?.ToString(),
                            DefaultBranch = repoNode["defaultBranch"]?.ToString()
                        };
                    }

                    pipelines.Add(summary);
                }
            }

            return pipelines.OrderBy(p => p.Name).ToList();
        }

        public async Task<PipelinePreviewResponse> GetPipelinePreviewYamlAsync(string organization, string project, int pipelineId, CancellationToken cancellationToken = default)
        {
            var token = await GetAdoTokenAsync(cancellationToken);
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Fetch pipeline name from definition first
            var defUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/definitions/{pipelineId}?api-version=7.1";
            var defResp = await client.GetAsync(defUrl, cancellationToken);
            string pipelineName = $"Pipeline {pipelineId}";
            if (defResp.IsSuccessStatusCode)
            {
                var defJson = await defResp.Content.ReadAsStringAsync(cancellationToken);
                var defNode = JsonNode.Parse(defJson);
                pipelineName = defNode?["name"]?.ToString() ?? pipelineName;
            }

            // Call Pipelines Preview API
            var previewUrl = $"https://dev.azure.com/{organization}/{project}/_apis/pipelines/{pipelineId}/preview?api-version=7.1-preview.1";
            var requestContent = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync(previewUrl, requestContent, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorMsg = "Azure DevOps Preview API returned an error.";
                try
                {
                    var errNode = JsonNode.Parse(content);
                    var msg = errNode?["message"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        errorMsg = msg;
                    }
                }
                catch { }

                throw new InvalidOperationException($"Preview failed for '{pipelineName}': {errorMsg}");
            }

            var root = JsonNode.Parse(content);
            var finalYaml = root?["finalYaml"]?.ToString();

            if (string.IsNullOrWhiteSpace(finalYaml))
            {
                throw new InvalidOperationException($"No 'finalYaml' property found in ADO preview response for '{pipelineName}'.");
            }

            return new PipelinePreviewResponse
            {
                PipelineId = pipelineId,
                PipelineName = pipelineName,
                FinalYaml = finalYaml,
                IsExpanded = true
            };
        }

        public async Task<PipelineYamlLinksResponse> GetPipelineYamlLinksAsync(
            string organization,
            string project,
            int pipelineId,
            string? githubBaseUrl = null,
            string? defaultGithubOrg = null,
            CancellationToken cancellationToken = default)
        {
            var token = await GetAdoTokenAsync(cancellationToken);
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Definition Details
            var defUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/definitions/{pipelineId}?api-version=7.1";
            var defResp = await client.GetAsync(defUrl, cancellationToken);
            if (!defResp.IsSuccessStatusCode)
            {
                var err = await defResp.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"ADO API Error ({defResp.StatusCode}): {err}");
            }

            var defJson = await defResp.Content.ReadAsStringAsync(cancellationToken);
            var defNode = JsonNode.Parse(defJson);
            var pipelineName = defNode?["name"]?.ToString() ?? $"Pipeline {pipelineId}";
            var yamlFilename = defNode?["process"]?["yamlFilename"]?.ToString() ?? "azure-pipelines.yml";

            var repoNode = defNode?["repository"];
            var repoName = repoNode?["name"]?.ToString() ?? string.Empty;
            var repoType = repoNode?["type"]?.ToString() ?? "GitHub";
            var defaultBranch = repoNode?["defaultBranch"]?.ToString() ?? "main";
            if (defaultBranch.StartsWith("refs/heads/"))
            {
                defaultBranch = defaultBranch.Substring("refs/heads/".Length);
            }
            var connectedServiceId = repoNode?["properties"]?["connectedServiceId"]?.ToString();

            var baseUrl = !string.IsNullOrWhiteSpace(githubBaseUrl) 
                ? githubBaseUrl.TrimEnd('/') 
                : "https://github.com";

            string BuildGithubUrl(string rName, string branch, string path)
            {
                var targetRepo = rName.Trim();
                if (!targetRepo.Contains('/') && !string.IsNullOrWhiteSpace(defaultGithubOrg))
                {
                    targetRepo = $"{defaultGithubOrg}/{targetRepo}";
                }
                var cleanBranch = branch.StartsWith("refs/heads/") ? branch.Substring("refs/heads/".Length) : branch;
                if (string.IsNullOrWhiteSpace(cleanBranch)) cleanBranch = "main";
                var cleanPath = path.TrimStart('/');
                return $"{baseUrl}/{targetRepo}/blob/{cleanBranch}/{cleanPath}";
            }

            var links = new List<PipelineYamlLink>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Main Pipeline YAML is the first tab
            var mainUrl = BuildGithubUrl(repoName, defaultBranch, yamlFilename);
            seenUrls.Add(mainUrl);
            links.Add(new PipelineYamlLink
            {
                Title = $"Main Pipeline ({System.IO.Path.GetFileName(yamlFilename)})",
                Repository = repoName,
                Branch = defaultBranch,
                Path = yamlFilename,
                Url = mainUrl,
                IsMainPipeline = true
            });

            // 2. Fetch YAML content to discover templates
            string? yamlContent = null;
            try
            {
                var spUrl = $"https://dev.azure.com/{organization}/{project}/_apis/sourceProviders/{repoType}/filecontents?repository={Uri.EscapeDataString(repoName)}&commitOrBranch={Uri.EscapeDataString(defaultBranch)}&path={Uri.EscapeDataString(yamlFilename)}&serviceEndpointId={connectedServiceId}&api-version=7.1";
                var spResp = await client.GetAsync(spUrl, cancellationToken);
                if (spResp.IsSuccessStatusCode)
                {
                    yamlContent = await spResp.Content.ReadAsStringAsync(cancellationToken);
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(yamlContent) && string.Equals(repoType, "tfsgit", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var repoId = repoNode?["id"]?.ToString();
                    var gitUrl = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repoId}/items?path={Uri.EscapeDataString(yamlFilename)}&includeContent=true&api-version=7.1";
                    var gitResp = await client.GetAsync(gitUrl, cancellationToken);
                    if (gitResp.IsSuccessStatusCode)
                    {
                        var gitJson = await gitResp.Content.ReadAsStringAsync(cancellationToken);
                        var gitNode = JsonNode.Parse(gitJson);
                        yamlContent = gitNode?["content"]?.ToString();
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                try
                {
                    var preview = await GetPipelinePreviewYamlAsync(organization, project, pipelineId, cancellationToken);
                    yamlContent = preview.FinalYaml;
                }
                catch { }
            }

            // 3. Parse templates if YAML content found
            if (!string.IsNullOrWhiteSpace(yamlContent))
            {
                var repoAliases = new Dictionary<string, (string RepoName, string? Branch)>(StringComparer.OrdinalIgnoreCase);

                var lines = yamlContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                string? currentAlias = null;
                string? currentName = null;
                string? currentRef = null;
                bool inRepositoriesBlock = false;

                foreach (var rawLine in lines)
                {
                    var trimmed = rawLine.Trim();
                    if (trimmed.StartsWith("repositories:"))
                    {
                        inRepositoriesBlock = true;
                        continue;
                    }

                    if (inRepositoriesBlock)
                    {
                        if (!rawLine.StartsWith(" ") && !rawLine.StartsWith("\t") && !string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("-"))
                        {
                            inRepositoriesBlock = false;
                        }
                    }

                    if (inRepositoriesBlock)
                    {
                        var repoMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^-\s+repository:\s*['""]?([^'""]+)['""]?");
                        if (repoMatch.Success)
                        {
                            if (!string.IsNullOrEmpty(currentAlias) && !string.IsNullOrEmpty(currentName))
                            {
                                repoAliases[currentAlias] = (currentName, currentRef);
                            }
                            currentAlias = repoMatch.Groups[1].Value.Trim();
                            currentName = null;
                            currentRef = null;
                            continue;
                        }

                        var nameMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^name:\s*['""]?([^'""]+)['""]?");
                        if (nameMatch.Success)
                        {
                            currentName = nameMatch.Groups[1].Value.Trim();
                            continue;
                        }

                        var refMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^ref:\s*['""]?([^'""]+)['""]?");
                        if (refMatch.Success)
                        {
                            currentRef = refMatch.Groups[1].Value.Trim();
                            continue;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(currentAlias) && !string.IsNullOrEmpty(currentName))
                {
                    repoAliases[currentAlias] = (currentName, currentRef);
                }

                var templateMatches = System.Text.RegularExpressions.Regex.Matches(yamlContent, @"template:\s*['""]?([^'""\r\n\s]+)['""]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match m in templateMatches)
                {
                    var templateStr = m.Groups[1].Value.Trim();
                    if (string.IsNullOrWhiteSpace(templateStr) || templateStr.StartsWith("$")) continue;

                    string tPath = templateStr;
                    string targetRepo = repoName;
                    string targetBranch = defaultBranch;

                    if (templateStr.Contains("@"))
                    {
                        var parts = templateStr.Split('@');
                        tPath = parts[0].Trim();
                        var alias = parts[1].Trim();

                        if (repoAliases.TryGetValue(alias, out var target))
                        {
                            targetRepo = target.RepoName;
                            if (!string.IsNullOrWhiteSpace(target.Branch))
                            {
                                targetBranch = target.Branch;
                            }
                        }
                        else
                        {
                            targetRepo = alias;
                        }
                    }

                    tPath = tPath.TrimStart('/');
                    var tUrl = BuildGithubUrl(targetRepo, targetBranch, tPath);
                    if (!seenUrls.Contains(tUrl))
                    {
                        seenUrls.Add(tUrl);
                        links.Add(new PipelineYamlLink
                        {
                            Title = $"Template: {System.IO.Path.GetFileName(tPath)} ({tPath})",
                            Repository = targetRepo,
                            Branch = targetBranch,
                            Path = tPath,
                            Url = tUrl,
                            IsMainPipeline = false
                        });
                    }
                }

                foreach (var kvp in repoAliases)
                {
                    var extRepo = kvp.Value.RepoName;
                    var extBranch = kvp.Value.Branch ?? defaultBranch;
                    if (!links.Any(l => string.Equals(l.Repository, extRepo, StringComparison.OrdinalIgnoreCase)))
                    {
                        var extUrl = $"{baseUrl}/{extRepo}";
                        if (!seenUrls.Contains(extUrl))
                        {
                            seenUrls.Add(extUrl);
                            links.Add(new PipelineYamlLink
                            {
                                Title = $"Template Repo: {extRepo}",
                                Repository = extRepo,
                                Branch = extBranch,
                                Path = "",
                                Url = extUrl,
                                IsMainPipeline = false
                            });
                        }
                    }
                }
            }

            return new PipelineYamlLinksResponse
            {
                PipelineId = pipelineId,
                PipelineName = pipelineName,
                Links = links
            };
        }

        public async Task<ReleaseMatrixResponse> GetReleaseMatrixAsync(
            string organization, 
            string project, 
            List<int> pipelineIds, 
            PipelineProfileConfig? profileConfig = null, 
            CancellationToken cancellationToken = default)
        {
            profileConfig ??= new PipelineProfileConfig();
            var token = await GetAdoTokenAsync(cancellationToken);
            var collectedEdges = new System.Collections.Concurrent.ConcurrentBag<(string From, string To)>();

            var tasks = pipelineIds.Select(id => FetchPipelineRowAsync(organization, project, id, token, profileConfig, collectedEdges, cancellationToken));
            var rows = await Task.WhenAll(tasks);

            // 1. Gather all discovered stages for the Expanded Stages DAG view
            var allStages = rows
                .SelectMany(r => r.Deployments.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 2. Perform strict DAG topological sort respecting dependencies first, then environment ranking
            var topoSortedStages = DagTopologicalSorter.Sort(allStages, collectedEdges, profileConfig.Environments);

            // 3. For Environment Matrix mode, build environment columns
            var targetEnvs = new List<string>(profileConfig.Environments);
            foreach (var row in rows)
            {
                foreach (var envKey in row.EnvironmentDeployments.Keys)
                {
                    if (!targetEnvs.Any(e => string.Equals(e, envKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        targetEnvs.Add(envKey);
                    }
                }
            }

            return new ReleaseMatrixResponse
            {
                Environments = targetEnvs,
                TopoSortedStages = topoSortedStages,
                Rows = rows.OrderBy(r => r.PipelineName).ToList()
            };
        }

        private async Task<PipelineMatrixRow> FetchPipelineRowAsync(
            string organization, 
            string project, 
            int pipelineId, 
            string token, 
            PipelineProfileConfig profileConfig,
            System.Collections.Concurrent.ConcurrentBag<(string From, string To)> collectedEdges,
            CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var row = new PipelineMatrixRow
            {
                PipelineId = pipelineId,
                PipelineName = $"Pipeline {pipelineId}"
            };

            string? repoName = null;
            string? defaultBranch = null;
            string? repoType = null;
            string? connectedServiceId = null;
            JsonNode? repoNode = null;

            // 1. Definition Details
            try
            {
                var defUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/definitions/{pipelineId}?api-version=7.1";
                var defResp = await client.GetAsync(defUrl, cancellationToken);
                if (defResp.IsSuccessStatusCode)
                {
                    var defJson = await defResp.Content.ReadAsStringAsync(cancellationToken);
                    var defNode = JsonNode.Parse(defJson);
                    row.PipelineName = defNode?["name"]?.ToString() ?? row.PipelineName;
                    row.YamlFilename = defNode?["process"]?["yamlFilename"]?.ToString() ?? "azure-pipelines.yml";

                    repoNode = defNode?["repository"];
                    if (repoNode != null)
                    {
                        repoName = repoNode["name"]?.ToString();
                        defaultBranch = repoNode["defaultBranch"]?.ToString() ?? "main";
                        repoType = repoNode["type"]?.ToString();
                        connectedServiceId = repoNode["properties"]?["connectedServiceId"]?.ToString();

                        row.Repository = new PipelineRepository
                        {
                            Id = repoNode["id"]?.ToString(),
                            Name = repoName,
                            Url = repoNode["url"]?.ToString(),
                            Type = repoType,
                            DefaultBranch = defaultBranch
                        };
                    }
                }
            }
            catch { }

            // 2. Fetch YAML to parse explicit DAG dependsOn stages
            try
            {
                if (!string.IsNullOrEmpty(repoName) && !string.IsNullOrEmpty(row.YamlFilename) && !string.IsNullOrEmpty(repoType))
                {
                    string? yamlContent = null;
                    var spUrl = $"https://dev.azure.com/{organization}/{project}/_apis/sourceProviders/{repoType}/filecontents?repository={Uri.EscapeDataString(repoName)}&commitOrBranch={Uri.EscapeDataString(defaultBranch ?? "main")}&path={Uri.EscapeDataString(row.YamlFilename)}&serviceEndpointId={connectedServiceId}&api-version=7.1";
                    var spResp = await client.GetAsync(spUrl, cancellationToken);
                    if (spResp.IsSuccessStatusCode)
                    {
                        yamlContent = await spResp.Content.ReadAsStringAsync(cancellationToken);
                    }
                    else if (string.Equals(repoType, "tfsgit", StringComparison.OrdinalIgnoreCase))
                    {
                        var repoId = repoNode?["id"]?.ToString();
                        var gitUrl = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repoId}/items?path={Uri.EscapeDataString(row.YamlFilename)}&includeContent=true&api-version=7.1";
                        var gitResp = await client.GetAsync(gitUrl, cancellationToken);
                        if (gitResp.IsSuccessStatusCode)
                        {
                            var gitJson = await gitResp.Content.ReadAsStringAsync(cancellationToken);
                            var gitNode = JsonNode.Parse(gitJson);
                            yamlContent = gitNode?["content"]?.ToString();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(yamlContent))
                    {
                        ParseYamlDagEdges(yamlContent, collectedEdges);
                    }
                }
            }
            catch { }

            // 3. Recent Builds (top 15 to find deployments across environments)
            try
            {
                var buildsUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds?definitions={pipelineId}&$top=15&api-version=7.1";
                var buildsResp = await client.GetAsync(buildsUrl, cancellationToken);
                if (buildsResp.IsSuccessStatusCode)
                {
                    var buildsJson = await buildsResp.Content.ReadAsStringAsync(cancellationToken);
                    var buildsNode = JsonNode.Parse(buildsJson);
                    var buildsArray = buildsNode?["value"]?.AsArray();

                    if (buildsArray != null && buildsArray.Count > 0)
                    {
                        foreach (var build in buildsArray)
                        {
                            if (build == null) continue;

                            var buildId = build["id"]?.GetValue<int>() ?? 0;
                            var buildNumber = build["buildNumber"]?.ToString() ?? string.Empty;
                            var sourceBranch = build["sourceBranch"]?.ToString() ?? string.Empty;
                            if (sourceBranch.StartsWith("refs/heads/"))
                            {
                                sourceBranch = sourceBranch.Substring("refs/heads/".Length);
                            }
                            var sourceVersion = build["sourceVersion"]?.ToString();
                            var author = build["requestedFor"]?["displayName"]?.ToString();
                            var authorImageUrl = build["requestedFor"]?["imageUrl"]?.ToString()
                                ?? build["requestedFor"]?["_links"]?["avatar"]?["href"]?.ToString();
                            var triggerReason = build["reason"]?.ToString();

                            bool isAutomated = false;
                            if (string.Equals(triggerReason, "schedule", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(triggerReason, "individualCI", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(triggerReason, "batchedCI", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(triggerReason, "buildCompletion", StringComparison.OrdinalIgnoreCase))
                            {
                                isAutomated = true;
                            }
                            if (!string.IsNullOrEmpty(author) &&
                                (author.Contains("Microsoft.VisualStudio.Services.TFS", StringComparison.OrdinalIgnoreCase) ||
                                 author.Contains("Build Service", StringComparison.OrdinalIgnoreCase) ||
                                 author.Contains("Automation", StringComparison.OrdinalIgnoreCase)))
                            {
                                isAutomated = true;
                            }

                            var buildUrl = build["_links"]?["web"]?["href"]?.ToString()
                                ?? $"https://dev.azure.com/{organization}/{project}/_build/results?buildId={buildId}&view=results";
                            var overallStatus = build["result"]?.ToString() ?? build["status"]?.ToString() ?? "unknown";

                            DateTime? deployedAt = null;
                            if (DateTime.TryParse(build["finishTime"]?.ToString() ?? build["startTime"]?.ToString(), out var dt))
                            {
                                deployedAt = dt;
                            }

                            string? prNumber = null;
                            var triggerInfo = build["triggerInfo"];
                            if (triggerInfo != null && triggerInfo["pr.number"] != null)
                            {
                                prNumber = triggerInfo["pr.number"]?.ToString();
                            }

                            // Extract runtime parameters (Environment, Operation, Tag/Version)
                            string? envParam = null;
                            string? opParam = null;
                            string? tagParam = null;

                            var paramsStr = build["parameters"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(paramsStr))
                            {
                                try
                                {
                                    var pJson = JsonNode.Parse(paramsStr);
                                    if (pJson != null)
                                    {
                                        envParam = pJson[profileConfig.EnvironmentParameterName]?.ToString()
                                            ?? pJson["Environment"]?.ToString()
                                            ?? pJson["env"]?.ToString()
                                            ?? pJson["targetEnv"]?.ToString();
                                        opParam = pJson[profileConfig.OperationParameterName]?.ToString()
                                            ?? pJson["Operation"]?.ToString()
                                            ?? pJson["operation"]?.ToString();
                                        tagParam = pJson["Tag"]?.ToString()
                                            ?? pJson["tag"]?.ToString()
                                            ?? pJson["Version"]?.ToString()
                                            ?? pJson["version"]?.ToString();
                                    }
                                }
                                catch { }
                            }

                            var templateParams = build["templateParameters"];
                            if (templateParams != null)
                            {
                                envParam ??= templateParams[profileConfig.EnvironmentParameterName]?.ToString()
                                    ?? templateParams["Environment"]?.ToString()
                                    ?? templateParams["env"]?.ToString();
                                opParam ??= templateParams[profileConfig.OperationParameterName]?.ToString()
                                    ?? templateParams["Operation"]?.ToString();
                                tagParam ??= templateParams["Tag"]?.ToString()
                                    ?? templateParams["Version"]?.ToString();
                            }

                            // If environment was specified via parameters, register in EnvironmentDeployments
                            if (!string.IsNullOrWhiteSpace(envParam))
                            {
                                var matchedTarget = profileConfig.Environments.FirstOrDefault(e => string.Equals(e, envParam.Trim(), StringComparison.OrdinalIgnoreCase)) ?? envParam.Trim();
                                if (!row.EnvironmentDeployments.ContainsKey(matchedTarget))
                                {
                                    row.EnvironmentDeployments[matchedTarget] = new EnvironmentDeploymentInfo
                                    {
                                        Environment = matchedTarget,
                                        Status = overallStatus,
                                        BuildId = buildId,
                                        BuildNumber = buildNumber,
                                        Branch = sourceBranch,
                                        CommitSha = sourceVersion,
                                        Author = author,
                                        AuthorImageUrl = authorImageUrl,
                                        TriggerReason = triggerReason,
                                        IsAutomated = isAutomated,
                                        DeployedAt = deployedAt,
                                        BuildUrl = buildUrl,
                                        PrNumber = prNumber,
                                        Operation = opParam,
                                        Version = tagParam
                                    };
                                }
                            }

                            // 4. Inspect Build Timeline for Stages
                            var timelineUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{buildId}/timeline?api-version=7.1";
                            var timelineResp = await client.GetAsync(timelineUrl, cancellationToken);
                            bool foundStage = false;

                            if (timelineResp.IsSuccessStatusCode)
                            {
                                var timelineJson = await timelineResp.Content.ReadAsStringAsync(cancellationToken);
                                var timelineNode = JsonNode.Parse(timelineJson);
                                var records = timelineNode?["records"]?.AsArray();

                                if (records != null)
                                {
                                    var stageRecords = records
                                        .Where(r => r?["type"]?.ToString() == "Stage")
                                        .OrderBy(r => r?["order"]?.GetValue<int>() ?? 0)
                                        .ToList();

                                    string? previousStageName = null;
                                    foreach (var rec in stageRecords)
                                    {
                                        var stageName = rec?["name"]?.ToString();
                                        if (string.IsNullOrWhiteSpace(stageName)) continue;

                                        foundStage = true;
                                        var status = rec?["result"]?.ToString() ?? rec?["state"]?.ToString() ?? "unknown";

                                        DateTime? stageTime = deployedAt;
                                        if (DateTime.TryParse(rec?["finishTime"]?.ToString() ?? rec?["startTime"]?.ToString(), out var stageDt))
                                        {
                                            stageTime = stageDt;
                                        }

                                        // Fallback sequence edge from timeline order if previous stage exists
                                        if (previousStageName != null && !string.Equals(previousStageName, stageName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            collectedEdges.Add((previousStageName, stageName));
                                        }
                                        previousStageName = stageName;

                                        // Register in Expanded Stages Deployments
                                        if (!row.Deployments.ContainsKey(stageName))
                                        {
                                            row.Deployments[stageName] = new StageDeploymentInfo
                                            {
                                                StageName = stageName,
                                                Status = status,
                                                BuildId = buildId,
                                                BuildNumber = buildNumber,
                                                Branch = sourceBranch,
                                                CommitSha = sourceVersion,
                                                Author = author,
                                                AuthorImageUrl = authorImageUrl,
                                                TriggerReason = triggerReason,
                                                IsAutomated = isAutomated,
                                                DeployedAt = stageTime,
                                                BuildUrl = buildUrl,
                                                PrNumber = prNumber
                                            };
                                        }

                                        // If this stage name corresponds to an environment, register in EnvironmentDeployments
                                        var matchedEnv = profileConfig.Environments.FirstOrDefault(e =>
                                            string.Equals(e, stageName, StringComparison.OrdinalIgnoreCase) ||
                                            stageName.IndexOf($"_{e}_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            stageName.EndsWith($"_{e}", StringComparison.OrdinalIgnoreCase) ||
                                            stageName.EndsWith($"-{e}", StringComparison.OrdinalIgnoreCase) ||
                                            stageName.EndsWith($" {e}", StringComparison.OrdinalIgnoreCase) ||
                                            stageName.StartsWith($"{e}_", StringComparison.OrdinalIgnoreCase) ||
                                            stageName.StartsWith($"{e}-", StringComparison.OrdinalIgnoreCase) ||
                                            stageName.StartsWith($"{e} ", StringComparison.OrdinalIgnoreCase));

                                        if (!string.IsNullOrEmpty(matchedEnv) && !row.EnvironmentDeployments.ContainsKey(matchedEnv))
                                        {
                                            row.EnvironmentDeployments[matchedEnv] = new EnvironmentDeploymentInfo
                                            {
                                                Environment = matchedEnv,
                                                Status = status,
                                                BuildId = buildId,
                                                BuildNumber = buildNumber,
                                                Branch = sourceBranch,
                                                CommitSha = sourceVersion,
                                                Author = author,
                                                AuthorImageUrl = authorImageUrl,
                                                TriggerReason = triggerReason,
                                                IsAutomated = isAutomated,
                                                DeployedAt = stageTime,
                                                BuildUrl = buildUrl,
                                                PrNumber = prNumber,
                                                Operation = opParam,
                                                Version = tagParam
                                            };
                                        }
                                    }
                                }
                            }

                            // Fallback if pipeline has no "Stage" timeline records (e.g. single-job build run)
                            if (!foundStage && !row.Deployments.ContainsKey("Build"))
                            {
                                row.Deployments["Build"] = new StageDeploymentInfo
                                {
                                    StageName = "Build",
                                    Status = overallStatus,
                                    BuildId = buildId,
                                    BuildNumber = buildNumber,
                                    Branch = sourceBranch,
                                    CommitSha = sourceVersion,
                                    Author = author,
                                    AuthorImageUrl = authorImageUrl,
                                    TriggerReason = triggerReason,
                                    IsAutomated = isAutomated,
                                    DeployedAt = deployedAt,
                                    BuildUrl = buildUrl,
                                    PrNumber = prNumber
                                };
                            }
                        }
                    }
                }
            }
            catch { }

            return row;
        }

        private static void ParseYamlDagEdges(string yamlContent, System.Collections.Concurrent.ConcurrentBag<(string From, string To)> edges)
        {
            try
            {
                var lines = yamlContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                string? currentStage = null;
                string? previousStage = null;
                bool inDependsOnBlock = false;
                bool stageHadExplicitDependsOn = false;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Detect stage declaration: - stage: StageName
                    var stageMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^-\s+stage:\s*['""]?([A-Za-z0-9_\-]+)['""]?");
                    if (stageMatch.Success)
                    {
                        if (currentStage != null && !stageHadExplicitDependsOn && previousStage != null)
                        {
                            edges.Add((previousStage, currentStage));
                        }

                        previousStage = currentStage;
                        currentStage = stageMatch.Groups[1].Value.Trim();
                        inDependsOnBlock = false;
                        stageHadExplicitDependsOn = false;
                        continue;
                    }

                    if (currentStage == null) continue;

                    // Detect dependsOn declaration
                    var inlineDepMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^dependsOn:\s*['""]?([A-Za-z0-9_\-]+)['""]?");
                    if (inlineDepMatch.Success)
                    {
                        var dep = inlineDepMatch.Groups[1].Value.Trim();
                        if (!string.Equals(dep, "none", StringComparison.OrdinalIgnoreCase) && !string.Equals(dep, "[]", StringComparison.OrdinalIgnoreCase))
                        {
                            edges.Add((dep, currentStage));
                        }
                        stageHadExplicitDependsOn = true;
                        inDependsOnBlock = false;
                        continue;
                    }

                    // Array syntax: dependsOn: [StageA, StageB]
                    var arrayDepMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^dependsOn:\s*\[(.*?)\]");
                    if (arrayDepMatch.Success)
                    {
                        var items = arrayDepMatch.Groups[1].Value.Split(',');
                        foreach (var item in items)
                        {
                            var clean = item.Trim().Trim('\'', '"');
                            if (!string.IsNullOrEmpty(clean))
                            {
                                edges.Add((clean, currentStage));
                            }
                        }
                        stageHadExplicitDependsOn = true;
                        inDependsOnBlock = false;
                        continue;
                    }

                    if (trimmed.StartsWith("dependsOn:"))
                    {
                        inDependsOnBlock = true;
                        stageHadExplicitDependsOn = true;
                        continue;
                    }

                    if (inDependsOnBlock)
                    {
                        if (trimmed.StartsWith("-"))
                        {
                            var depItemMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^-\s*['""]?([A-Za-z0-9_\-]+)['""]?");
                            if (depItemMatch.Success)
                            {
                                edges.Add((depItemMatch.Groups[1].Value.Trim(), currentStage));
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            inDependsOnBlock = false;
                        }
                    }
                }

                if (currentStage != null && !stageHadExplicitDependsOn && previousStage != null)
                {
                    edges.Add((previousStage, currentStage));
                }
            }
            catch { }
        }
    }
}
