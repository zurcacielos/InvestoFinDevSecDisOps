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

        public async Task<ReleaseMatrixResponse> GetReleaseMatrixAsync(string organization, string project, List<int> pipelineIds, CancellationToken cancellationToken = default)
        {
            var token = await GetAdoTokenAsync(cancellationToken);
            var tasks = pipelineIds.Select(id => FetchPipelineRowAsync(organization, project, id, token, cancellationToken));
            var rows = await Task.WhenAll(tasks);

            var allStages = rows
                .SelectMany(r => r.Deployments.Keys)
                .Distinct()
                .OrderBy(GetEnvironmentRank)
                .ThenBy(name => name)
                .ToList();

            return new ReleaseMatrixResponse
            {
                Environments = allStages,
                Rows = rows.OrderBy(r => r.PipelineName).ToList()
            };
        }

        private async Task<PipelineMatrixRow> FetchPipelineRowAsync(string organization, string project, int pipelineId, string token, CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var row = new PipelineMatrixRow
            {
                PipelineId = pipelineId,
                PipelineName = $"Pipeline {pipelineId}"
            };

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

                    var repoNode = defNode?["repository"];
                    if (repoNode != null)
                    {
                        row.Repository = new PipelineRepository
                        {
                            Id = repoNode["id"]?.ToString(),
                            Name = repoNode["name"]?.ToString(),
                            Url = repoNode["url"]?.ToString(),
                            Type = repoNode["type"]?.ToString(),
                            DefaultBranch = repoNode["defaultBranch"]?.ToString()
                        };
                    }
                }
            }
            catch { }

            // 2. Recent Builds (up to 5 completed or in-progress)
            try
            {
                var buildsUrl = $"https://dev.azure.com/{organization}/{project}/_apis/build/builds?definitions={pipelineId}&$top=5&api-version=7.1";
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
                            var buildUrl = build["_links"]?["web"]?["href"]?.ToString()
                                ?? $"https://dev.azure.com/{organization}/{project}/_build/results?buildId={buildId}&view=results";

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

                            // 3. Inspect Build Timeline for Stages
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
                                    foreach (var rec in records)
                                    {
                                        if (rec?["type"]?.ToString() == "Stage")
                                        {
                                            var stageName = rec["name"]?.ToString();
                                            if (string.IsNullOrWhiteSpace(stageName)) continue;

                                            foundStage = true;
                                            var status = rec["result"]?.ToString() ?? rec["state"]?.ToString() ?? "unknown";

                                            // Only record if we haven't seen this stage yet from a newer build
                                            if (!row.Deployments.ContainsKey(stageName))
                                            {
                                                DateTime? stageTime = deployedAt;
                                                if (DateTime.TryParse(rec["finishTime"]?.ToString() ?? rec["startTime"]?.ToString(), out var stageDt))
                                                {
                                                    stageTime = stageDt;
                                                }

                                                row.Deployments[stageName] = new StageDeploymentInfo
                                                {
                                                    StageName = stageName,
                                                    Status = status,
                                                    BuildId = buildId,
                                                    BuildNumber = buildNumber,
                                                    Branch = sourceBranch,
                                                    CommitSha = sourceVersion,
                                                    Author = author,
                                                    DeployedAt = stageTime,
                                                    BuildUrl = buildUrl,
                                                    PrNumber = prNumber
                                                };
                                            }
                                        }
                                    }
                                }
                            }

                            // Fallback if pipeline has no "Stage" timeline records (e.g., standard build run)
                            if (!foundStage && !row.Deployments.ContainsKey("Build"))
                            {
                                var overallStatus = build["result"]?.ToString() ?? build["status"]?.ToString() ?? "unknown";
                                row.Deployments["Build"] = new StageDeploymentInfo
                                {
                                    StageName = "Build",
                                    Status = overallStatus,
                                    BuildId = buildId,
                                    BuildNumber = buildNumber,
                                    Branch = sourceBranch,
                                    CommitSha = sourceVersion,
                                    Author = author,
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

        private static int GetEnvironmentRank(string name)
        {
            var lower = name.ToLowerInvariant();
            if (lower == "dev" || lower.StartsWith("dev-") || lower.StartsWith("dev_")) return 10;
            if (lower.Contains("dev")) return 15;
            if (lower == "test" || lower.Contains("test") || lower == "qa" || lower.Contains("qa")) return 20;
            if (lower.Contains("uat")) return 30;
            if (lower.Contains("stg") || lower.Contains("staging")) return 40;
            if (lower.Contains("preprod")) return 50;
            if (lower == "prd" || lower == "prod" || lower.Contains("prod")) return 60;
            return 100;
        }
    }
}
