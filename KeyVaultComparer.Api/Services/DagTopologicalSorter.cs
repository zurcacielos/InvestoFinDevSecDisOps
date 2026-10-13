using System;
using System.Collections.Generic;
using System.Linq;

namespace KeyVaultComparer.Api.Services
{
    public static class DagTopologicalSorter
    {
        public static List<string> Sort(
            IEnumerable<string> nodes, 
            IEnumerable<(string From, string To)> edges, 
            IEnumerable<string>? customEnvOrder = null)
        {
            var nodeSet = new HashSet<string>(nodes, StringComparer.OrdinalIgnoreCase);
            if (nodeSet.Count == 0) return new List<string>();

            var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var adjList = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in nodeSet)
            {
                inDegree[node] = 0;
                adjList[node] = new List<string>();
            }

            foreach (var (from, to) in edges)
            {
                if (nodeSet.Contains(from) && nodeSet.Contains(to) && !string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                {
                    adjList[from].Add(to);
                    inDegree[to]++;
                }
            }

            var envList = customEnvOrder?
                .Select(e => e.Trim().ToLowerInvariant())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            int GetRank(string name)
            {
                var lower = name.ToLowerInvariant();
                if (envList != null && envList.Count > 0)
                {
                    for (int i = 0; i < envList.Count; i++)
                    {
                        var target = envList[i];
                        if (lower == target || lower.Contains(target))
                        {
                            return 20 + i;
                        }
                    }
                }
                return GetEnvironmentRank(name);
            }

            var result = new List<string>();
            var ready = new List<string>();

            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                {
                    ready.Add(kvp.Key);
                }
            }

            while (ready.Count > 0)
            {
                // Tie-breaker: lowest rank first, then alphabetical (case-insensitive)
                var current = ready
                    .OrderBy(GetRank)
                    .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .First();

                ready.Remove(current);
                result.Add(current);

                if (adjList.TryGetValue(current, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                    {
                        inDegree[neighbor]--;
                        if (inDegree[neighbor] == 0)
                        {
                            ready.Add(neighbor);
                        }
                    }
                }
            }

            // If any disconnected or cyclic nodes remain, append by rank
            if (result.Count < nodeSet.Count)
            {
                var remaining = nodeSet.Except(result, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(GetRank)
                    .ThenBy(n => n, StringComparer.OrdinalIgnoreCase);
                result.AddRange(remaining);
            }

            return result;
        }

        public static int GetEnvironmentRank(string name)
        {
            var lower = name.ToLowerInvariant();
            if (lower == "build" || lower.Contains("build") || lower.Contains("compile") || lower.Contains("publish")) return 5;
            if (lower.Contains("test") || lower.Contains("scan") || lower.Contains("check") || lower.Contains("sonar") || lower.Contains("nexus")) return 8;
            if (lower == "dev" || lower.StartsWith("dev-") || lower.StartsWith("dev_")) return 10;
            if (lower.Contains("dev")) return 15;
            if (lower == "test" || lower.Contains("test") || lower == "qa" || lower.Contains("qa")) return 20;
            if (lower == "uat" || lower.StartsWith("uat-") || lower.StartsWith("uat_")) return 30;
            if (lower.Contains("uat1")) return 31;
            if (lower.Contains("uat2")) return 32;
            if (lower.Contains("uat")) return 35;
            if (lower == "stg" || lower.StartsWith("stg-") || lower.StartsWith("stg_")) return 40;
            if (lower.Contains("stg1")) return 41;
            if (lower.Contains("stg2")) return 42;
            if (lower.Contains("stg") || lower.Contains("staging")) return 45;
            if (lower.Contains("preprod")) return 50;
            if (lower == "prd" || lower == "prod" || lower.StartsWith("prd-") || lower.StartsWith("prd_")) return 60;
            if (lower.Contains("prd1") || lower.Contains("prod1")) return 61;
            if (lower.Contains("prd2") || lower.Contains("prod2")) return 62;
            if (lower.Contains("prd") || lower.Contains("prod")) return 65;
            return 100;
        }
    }
}
