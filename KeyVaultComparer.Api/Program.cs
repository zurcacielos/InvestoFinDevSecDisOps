using Azure.Core;
using Azure.Identity;
using KeyVaultComparer.Api.Models;
using KeyVaultComparer.Api.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Register the global TokenCredential
builder.Services.AddSingleton<TokenCredential>(sp => 
{
    // Use DefaultAzureCredential, but exclude slow credentials like ManagedIdentity when running locally
    var options = new DefaultAzureCredentialOptions();
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Local"))
    {
        options.ExcludeManagedIdentityCredential = true; // Prevents hanging on 169.254.169.254 timeout locally
        options.ExcludeWorkloadIdentityCredential = true;
        options.ExcludeVisualStudioCredential = true; // Prevent VS from overriding az login
        options.ExcludeVisualStudioCodeCredential = true;
        options.ExcludeAzurePowerShellCredential = true; // Prevent PowerShell from overriding az login
        options.ExcludeAzureDeveloperCliCredential = true;
    }
    return new DefaultAzureCredential(options);
});

builder.Services.AddSingleton<KeyVaultService>();
builder.Services.AddSingleton<KeyVaultManagementService>();
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AdoPipelineService>();

// Enable CORS for Vue dev server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // Common Vite ports
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex) when (ex is Azure.Identity.CredentialUnavailableException || 
                               ex is Azure.Identity.AuthenticationFailedException ||
                               (ex is Azure.RequestFailedException rfe && (rfe.Status == 401 || rfe.Status == 403)) ||
                               ex.ToString().Contains("az login", StringComparison.OrdinalIgnoreCase) ||
                               ex.ToString().Contains("AADSTS", StringComparison.OrdinalIgnoreCase) ||
                               ex.ToString().Contains("interactive authentication", StringComparison.OrdinalIgnoreCase) ||
                               ex.ToString().Contains("refresh token", StringComparison.OrdinalIgnoreCase) ||
                               ex.ToString().Contains("No subscriptions found", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "az_login_required", message = ex.Message });
    }
});

app.MapPost("/api/vaults/keys", async ([FromBody] List<string> vaultUris, KeyVaultService service) =>
{
    var result = await service.GetAllSecretNamesAsync(vaultUris);
    return Results.Ok(result);
})
.WithName("GetVaultKeys");

app.MapPost("/api/vault/values", async ([FromBody] SecretValuesRequest request, KeyVaultService service) =>
{
    var result = await service.GetSecretValuesAsync(request.VaultUri, request.SecretNames);
    return Results.Ok(result);
})
.WithName("GetVaultValues");

app.MapPost("/api/vault/apply", async ([FromBody] List<StagedChangeRequest> request, KeyVaultService service) =>
{
    var errors = await service.ApplyChangesAsync(request);
    if (errors.Any()) 
    {
        return Results.BadRequest(new { errors });
    }
    return Results.Ok(new { message = "Changes applied successfully." });
})
.WithName("ApplyVaultChanges");

app.MapGet("/api/vaults", async ([FromQuery] string? query, [FromQuery] string? subscriptionId, KeyVaultManagementService service) =>
{
    var vaults = await service.GetAvailableVaultsAsync(query, subscriptionId);
    return Results.Ok(vaults);
})
.WithName("GetVaults");

app.MapGet("/api/profile", async (ProfileService service) =>
{
    var profile = await service.GetProfileAsync();
    return Results.Ok(profile);
})
.WithName("GetProfile");

app.MapPost("/api/vaults/usage", async ([FromBody] UsageStatsRequest req, [FromQuery] int days, KeyVaultManagementService service) =>
{
    var stats = await service.GetVaultUsageStatsAsync(req.VaultUris, req.SecretNames, days == 0 ? 90 : days);
    return Results.Ok(stats);
})
.WithName("GetVaultsUsage");

app.MapGet("/api/subscriptions", async (KeyVaultManagementService service) =>
{
    var subs = await service.GetSubscriptionsAsync();
    return Results.Ok(subs);
})
.WithName("GetSubscriptions");

app.MapGet("/api/devops/variablegroups", async ([FromQuery] string organization, [FromQuery] string project, TokenCredential credential) =>
{
    try
    {
        var tokenContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
        var token = await credential.GetTokenAsync(tokenContext, default);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        
        var url = $"https://dev.azure.com/{organization}/{project}/_apis/distributedtask/variablegroups?api-version=7.1";
        var response = await client.GetAsync(url);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation || content.TrimStart().StartsWith("<"))
            {
                return Results.BadRequest(new { error = "Authentication to Azure DevOps failed. The API returned a sign-in page.", details = "Please ensure your Azure AD token has access to this DevOps organization." });
            }
            return Results.Content(content, "application/json");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            var details = error.TrimStart().StartsWith("<") ? "HTML Sign-In Page / Unauthorized" : error;
            return Results.BadRequest(new { error = $"ADO API Error ({response.StatusCode})", details = details });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetAdoVariableGroups");

app.MapPut("/api/devops/variablegroups/{groupId}/variables", async (
    int groupId, 
    [FromQuery] string organization, 
    [FromQuery] string project, 
    [FromBody] AddVariableRequest req,
    TokenCredential credential) =>
{
    try
    {
        var tokenContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
        var token = await credential.GetTokenAsync(tokenContext, default);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        
        // 1. Get the current variable group
        var getUrl = $"https://dev.azure.com/{organization}/{project}/_apis/distributedtask/variablegroups/{groupId}?api-version=7.1";
        var getResp = await client.GetAsync(getUrl);
        
        if (!getResp.IsSuccessStatusCode)
        {
            var err = await getResp.Content.ReadAsStringAsync();
            return Results.BadRequest(new { error = $"ADO API Error getting group ({getResp.StatusCode})", details = err });
        }

        var groupJson = await getResp.Content.ReadAsStringAsync();
        var groupObj = System.Text.Json.Nodes.JsonNode.Parse(groupJson)?.AsObject();
        
        if (groupObj == null || !groupObj.ContainsKey("variables"))
            return Results.BadRequest(new { error = "Invalid variable group format from ADO" });

        var variables = groupObj["variables"]?.AsObject();
        if (variables == null) return Results.BadRequest(new { error = "Variables object is null" });

        // Add the new variable (for Key Vault linked groups, we only need enabled = true)
        var newVar = new System.Text.Json.Nodes.JsonObject();
        newVar["enabled"] = true;
        variables[req.SecretName] = newVar;

        // 2. PUT the updated group
        var putUrl = $"https://dev.azure.com/{organization}/{project}/_apis/distributedtask/variablegroups/{groupId}?api-version=7.1";
        var content = new StringContent(groupObj.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var putResp = await client.PutAsync(putUrl, content);

        if (!putResp.IsSuccessStatusCode)
        {
            var err = await putResp.Content.ReadAsStringAsync();
            return Results.BadRequest(new { error = $"ADO API Error updating group ({putResp.StatusCode})", details = err });
        }

        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("AddVariableToAdoGroup");

app.MapGet("/api/devops/pipelines", async ([FromQuery] string organization, [FromQuery] string project, AdoPipelineService pipelineService, CancellationToken ct) =>
{
    try
    {
        var pipelines = await pipelineService.GetPipelinesAsync(organization, project, ct);
        return Results.Ok(pipelines);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetAdoPipelines");

app.MapPost("/api/devops/pipelines/matrix", async ([FromQuery] string organization, [FromQuery] string project, [FromBody] ReleaseMatrixRequest request, AdoPipelineService pipelineService, CancellationToken ct) =>
{
    try
    {
        if (request?.PipelineIds == null || request.PipelineIds.Count == 0)
        {
            return Results.Ok(new ReleaseMatrixResponse());
        }
        var matrix = await pipelineService.GetReleaseMatrixAsync(organization, project, request.PipelineIds, request.ProfileConfig, ct);
        return Results.Ok(matrix);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetReleaseMatrix");

app.MapPost("/api/devops/pipelines/{pipelineId:int}/preview-yaml", async (int pipelineId, [FromQuery] string organization, [FromQuery] string project, AdoPipelineService pipelineService, CancellationToken ct) =>
{
    try
    {
        var result = await pipelineService.GetPipelinePreviewYamlAsync(organization, project, pipelineId, ct);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetPipelinePreviewYaml");

app.MapGet("/api/devops/pipelines/{pipelineId:int}/github-yamls", async (
    int pipelineId, 
    [FromQuery] string organization, 
    [FromQuery] string project, 
    [FromQuery] string? githubBaseUrl,
    [FromQuery] string? githubOrg,
    AdoPipelineService pipelineService, 
    CancellationToken ct) =>
{
    try
    {
        var result = await pipelineService.GetPipelineYamlLinksAsync(organization, project, pipelineId, githubBaseUrl, githubOrg, ct);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetPipelineGithubYamls");

var avatarCache = new System.Collections.Concurrent.ConcurrentDictionary<string, (byte[] Data, string ContentType, DateTime ExpiresAt)>();

app.MapGet("/api/devops/avatar", async (
    [FromQuery] string url, 
    TokenCredential credential, 
    IHttpClientFactory httpClientFactory, 
    HttpContext httpContext,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.BadRequest(new { error = "url query parameter is required" });
    }

    if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri))
    {
        return Results.BadRequest(new { error = "Invalid URL format" });
    }

    var host = targetUri.Host.ToLowerInvariant();
    bool isAllowedHost = host.EndsWith("azure.com") || 
                         host.EndsWith("visualstudio.com") || 
                         host.EndsWith("microsoft.com") ||
                         host.EndsWith("live.com") ||
                         host.EndsWith("office365.com");

    if (!isAllowedHost)
    {
        return Results.BadRequest(new { error = "Target host is not an allowed domain for avatar proxying" });
    }

    if (avatarCache.TryGetValue(url, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
    {
        httpContext.Response.Headers.CacheControl = "public, max-age=86400, immutable";
        return Results.File(cached.Data, cached.ContentType);
    }

    try
    {
        var tokenContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
        var token = await credential.GetTokenAsync(tokenContext, ct);

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        client.DefaultRequestHeaders.Accept.ParseAdd("image/*, */*");

        var response = await client.GetAsync(targetUri, ct);
        if (!response.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);

        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase) || (bytes.Length > 0 && bytes[0] == (byte)'{'))
        {
            try
            {
                var jsonStr = System.Text.Encoding.UTF8.GetString(bytes);
                var doc = System.Text.Json.Nodes.JsonNode.Parse(jsonStr);
                var base64Val = doc?["value"]?.ToString();
                if (!string.IsNullOrEmpty(base64Val))
                {
                    bytes = Convert.FromBase64String(base64Val);
                    contentType = "image/png";
                }
            }
            catch { }
        }

        avatarCache[url] = (bytes, contentType, DateTime.UtcNow.AddHours(24));
        httpContext.Response.Headers.CacheControl = "public, max-age=86400, immutable";

        return Results.File(bytes, contentType);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetDevopsAvatar");



app.MapPost("/api/logs/url", async ([FromBody] LogUrlRequest request) =>
{
    try
    {
        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }
        var logFile = Path.Combine(logsDir, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
        await File.AppendAllTextAsync(logFile, $"[{DateTime.UtcNow:O}] Opened URL: {request.Url}{Environment.NewLine}");
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("LogUrlOpened");

app.Run();

public record LogUrlRequest(string Url);
