using System;
using System.Collections.Generic;

namespace KeyVaultComparer.Api.Models
{
    public class PipelineSummary
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Path { get; set; }
        public PipelineRepository? Repository { get; set; }
    }

    public class PipelineRepository
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string? Type { get; set; }
        public string? DefaultBranch { get; set; }
    }

    public class PipelinePreviewResponse
    {
        public int PipelineId { get; set; }
        public string PipelineName { get; set; } = string.Empty;
        public string FinalYaml { get; set; } = string.Empty;
        public bool IsExpanded { get; set; } = true;
    }

    public class PipelineProfileConfig
    {
        public string EnvironmentParameterName { get; set; } = "Environment";
        public string OperationParameterName { get; set; } = "Operation";
        public List<string> Environments { get; set; } = new() { "dev", "uat", "uat1", "uat2", "stg", "stg1", "prd", "prd1" };
    }

    public class ReleaseMatrixResponse
    {
        public List<string> Environments { get; set; } = new();
        public List<string> TopoSortedStages { get; set; } = new();
        public List<PipelineMatrixRow> Rows { get; set; } = new();
    }

    public class PipelineMatrixRow
    {
        public int PipelineId { get; set; }
        public string PipelineName { get; set; } = string.Empty;
        public string? YamlFilename { get; set; }
        public PipelineRepository? Repository { get; set; }
        public Dictionary<string, StageDeploymentInfo> Deployments { get; set; } = new();
        public Dictionary<string, EnvironmentDeploymentInfo> EnvironmentDeployments { get; set; } = new();
        public Dictionary<string, List<EnvironmentDeploymentInfo>> EnvironmentHistory { get; set; } = new();
        public List<PipelineRunInfo> RecentRuns { get; set; } = new();
    }

    public class PipelineYamlLink
    {
        public string Title { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsMainPipeline { get; set; }
    }

    public class PipelineYamlLinksResponse
    {
        public int PipelineId { get; set; }
        public string PipelineName { get; set; } = string.Empty;
        public List<PipelineYamlLink> Links { get; set; } = new();
    }

    public class StageDeploymentInfo
    {
        public string StageName { get; set; } = string.Empty;
        public string Status { get; set; } = "unknown";
        public int BuildId { get; set; }
        public string BuildNumber { get; set; } = string.Empty;
        public string? Branch { get; set; }
        public string? CommitSha { get; set; }
        public string? Author { get; set; }
        public string? AuthorImageUrl { get; set; }
        public string? TriggerReason { get; set; }
        public bool IsAutomated { get; set; }
        public DateTime? DeployedAt { get; set; }
        public string? BuildUrl { get; set; }
        public string? PrNumber { get; set; }
    }

    public class EnvironmentDeploymentInfo
    {
        public string Environment { get; set; } = string.Empty;
        public string Status { get; set; } = "unknown";
        public int BuildId { get; set; }
        public string BuildNumber { get; set; } = string.Empty;
        public string? Branch { get; set; }
        public string? CommitSha { get; set; }
        public string? Author { get; set; }
        public string? AuthorImageUrl { get; set; }
        public string? TriggerReason { get; set; }
        public bool IsAutomated { get; set; }
        public DateTime? DeployedAt { get; set; }
        public string? BuildUrl { get; set; }
        public string? PrNumber { get; set; }
        public string? Operation { get; set; }
        public string? Version { get; set; }
    }

    public class ReleaseMatrixRequest
    {
        public List<int> PipelineIds { get; set; } = new();
        public PipelineProfileConfig? ProfileConfig { get; set; }
        public int ScanDepth { get; set; } = 25;
        public bool OnlySucceeded { get; set; } = true;
    }

    public class PipelineRunStageInfo
    {
        public string StageName { get; set; } = string.Empty;
        public string Status { get; set; } = "unknown";
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public int? Order { get; set; }
    }

    public class PipelineRunInfo
    {
        public int BuildId { get; set; }
        public string BuildNumber { get; set; } = string.Empty;
        public string OverallStatus { get; set; } = "unknown";
        public string? Branch { get; set; }
        public string? CommitSha { get; set; }
        public string? Author { get; set; }
        public string? AuthorImageUrl { get; set; }
        public string? TriggerReason { get; set; }
        public bool IsAutomated { get; set; }
        public DateTime? DeployedAt { get; set; }
        public string? BuildUrl { get; set; }
        public string? PrNumber { get; set; }
        public Dictionary<string, PipelineRunStageInfo> Stages { get; set; } = new();
    }
}

