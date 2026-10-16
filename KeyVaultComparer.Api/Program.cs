using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using KeyVaultComparer.Api.Models;
using KeyVaultComparer.Api.Services;
using KeyVaultComparer.Api.Services.SecretStore;
using KeyVaultComparer.Api.Services.Cache;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Register the base AzureCli/Default credential
builder.Services.AddSingleton<DefaultAzureCredential>(sp => 
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

// Register the dynamic TokenCredential that resolves Service Principal or DefaultAzureCredential
builder.Services.AddSingleton<DynamicAzureCredential>(sp => 
{
    var secretManager = sp.GetRequiredService<SecretStoreManager>();
    var appSettings = sp.GetRequiredService<AppSettingsService>();
    var defaultCli = sp.GetRequiredService<DefaultAzureCredential>();
    return new DynamicAzureCredential(secretManager, appSettings, defaultCli);
});
builder.Services.AddSingleton<TokenCredential>(sp => sp.GetRequiredService<DynamicAzureCredential>());

builder.Services.AddSingleton<AppSettingsService>();
builder.Services.AddSingleton<EncryptedCacheService>();
builder.Services.AddSingleton<KeyVaultService>();
builder.Services.AddSingleton<KeyVaultManagementService>();
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ISecureSecretStore, WindowsCredentialStore>();
builder.Services.AddSingleton<ISecureSecretStore, EncryptedFileSecretStore>();
builder.Services.AddSingleton<SecretStoreManager>();
builder.Services.AddSingleton<AdoAuthService>();
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

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Local"))
{
    app.UseHttpsRedirection();
}
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

app.MapGet("/api/profile", async ([FromServices] ProfileService service, CancellationToken ct) =>
{
    var profile = await service.GetProfileAsync(ct);
    return Results.Ok(profile);
})
.WithName("GetProfile");

app.MapPost("/api/vaults/usage", async ([FromBody] UsageStatsRequest req, [FromQuery] int days, [FromServices] KeyVaultManagementService service) =>
{
    var stats = await service.GetVaultUsageStatsAsync(req.VaultUris, req.SecretNames, days == 0 ? 90 : days);
    return Results.Ok(stats);
})
.WithName("GetVaultsUsage");

app.MapGet("/api/subscriptions", async ([FromServices] KeyVaultManagementService service, CancellationToken ct) =>
{
    var subs = await service.GetSubscriptionsAsync(ct);
    return Results.Ok(subs);
})
.WithName("GetSubscriptions");

app.MapGet("/api/devops/variablegroups", async (
    [FromQuery] string organization, 
    [FromQuery] string project, 
    AdoAuthService adoAuthService,
    EncryptedCacheService cacheService,
    CancellationToken ct) =>
{
    try
    {
        string cacheKey = $"variable_groups:{organization.ToLower()}:{project.ToLower()}";
        var cached = await cacheService.GetAsync<string>(cacheKey, "VariableGroups", ct);
        if (!string.IsNullOrEmpty(cached))
        {
            return Results.Content(cached, "application/json");
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = await adoAuthService.GetAdoAuthHeaderAsync(ct);
        
        var url = $"https://dev.azure.com/{organization}/{project}/_apis/distributedtask/variablegroups?api-version=7.1";
        var response = await client.GetAsync(url, ct);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation || content.TrimStart().StartsWith("<"))
            {
                return Results.BadRequest(new { error = "Authentication to Azure DevOps failed. The API returned a sign-in page.", details = "Please ensure your Azure AD token has access to this DevOps organization." });
            }
            await cacheService.SetAsync(cacheKey, content, "VariableGroups", cancellationToken: ct);
            return Results.Content(content, "application/json");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync(ct);
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
    AdoAuthService adoAuthService,
    EncryptedCacheService cacheService) =>
{
    try
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = await adoAuthService.GetAdoAuthHeaderAsync();
        
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

        await cacheService.ClearCategoryAsync("VariableGroups");
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
        var scanDepth = request.ScanDepth > 0 ? request.ScanDepth : 25;
        var matrix = await pipelineService.GetReleaseMatrixAsync(organization, project, request.PipelineIds, request.ProfileConfig, scanDepth, request.OnlySucceeded, ct, bypassCache: request.ForceRefresh);
        return Results.Ok(matrix);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetReleaseMatrix");

app.MapPost("/api/devops/pipelines/{pipelineId:int}/deep-scan", async (
    int pipelineId, 
    [FromQuery] string organization, 
    [FromQuery] string project, 
    [FromBody] PipelineDeepScanRequest request, 
    AdoPipelineService pipelineService, 
    CancellationToken ct) =>
{
    try
    {
        var scanDepth = request?.ScanDepth > 0 ? request.ScanDepth : 50;
        var row = await pipelineService.DeepScanPipelineRowAsync(organization, project, pipelineId, request?.ProfileConfig, scanDepth, request?.OnlySucceeded ?? true, ct);
        return Results.Ok(row);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("DeepScanPipeline");

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

app.MapGet("/api/settings", async (
    AppSettingsService appSettingsService,
    SecretStoreManager secretStoreManager,
    CancellationToken ct) =>
{
    var settings = await appSettingsService.GetSettingsAsync(ct);
    var providers = await secretStoreManager.GetProvidersInfoAsync(ct);
    var activeProvider = await secretStoreManager.GetActiveStoreAsync(ct);
    bool hasAdoPat = await secretStoreManager.HasSecretAsync(SecretStoreManager.AdoPatKey, ct);
    bool hasGithubToken = await secretStoreManager.HasSecretAsync(SecretStoreManager.GithubTokenKey, ct);
    bool hasAzureClientSecret = await secretStoreManager.HasSecretAsync(SecretStoreManager.AzureClientSecretKey, ct);
    bool hasServicePrincipal = !string.IsNullOrWhiteSpace(settings.AzureTenantId) &&
                               !string.IsNullOrWhiteSpace(settings.AzureClientId) &&
                               hasAzureClientSecret;

    return Results.Ok(new
    {
        azureTenantId = settings.AzureTenantId ?? "",
        azureClientId = settings.AzureClientId ?? "",
        hasAzureClientSecret = hasAzureClientSecret,
        hasServicePrincipal = hasServicePrincipal,
        azureAuthMode = hasServicePrincipal ? "ServicePrincipal" : "InteractiveSSO",
        adoOrganization = settings.AdoOrganization ?? "",
        adoProject = settings.AdoProject ?? "",
        hasAdoPat = hasAdoPat,
        adoAuthMode = hasAdoPat ? "PAT" : "AzureSSO",
        githubBaseUrl = string.IsNullOrWhiteSpace(settings.GithubBaseUrl) ? "https://github.com" : settings.GithubBaseUrl,
        githubOrganization = settings.GithubOrganization ?? "",
        hasGithubToken = hasGithubToken,
        activeStorageProvider = activeProvider.ProviderName,
        isWindows = OperatingSystem.IsWindows(),
        storageProviders = providers,
        cacheEnabled = settings.CacheEnabled,
        cachePipelinesTtlMinutes = settings.CachePipelinesTtlMinutes,
        cachePipelineRunsTtlMinutes = settings.CachePipelineRunsTtlMinutes,
        cacheVaultKeysTtlMinutes = settings.CacheVaultKeysTtlMinutes,
        cacheVariableGroupsTtlMinutes = settings.CacheVariableGroupsTtlMinutes,
        azureAuthTimeoutSeconds = settings.AzureAuthTimeoutSeconds
    });
})
.WithName("GetSettings");

app.MapGet("/api/auth/status", async (
    [FromServices] TokenCredential credential,
    [FromServices] AppSettingsService appSettingsService,
    [FromServices] EncryptedCacheService cacheService,
    CancellationToken ct) =>
{
    var settings = await appSettingsService.GetSettingsAsync(ct);
    int timeoutSec = settings.AzureAuthTimeoutSeconds > 0 ? settings.AzureAuthTimeoutSeconds : 4;

    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

    try
    {
        var tokenContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
        var token = await credential.GetTokenAsync(tokenContext, timeoutCts.Token);

        if (string.IsNullOrWhiteSpace(token.Token))
        {
            return Results.Ok(new 
            { 
                isConnected = false, 
                state = "disconnected", 
                reason = "NoTokenReturned" 
            });
        }

        string email = "Authenticated User";
        string initials = "AU";
        string tenantId = "";

        try
        {
            var parts = token.Token.Split('.');
            if (parts.Length >= 2)
            {
                var payloadStr = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payloadStr.Length % 4)
                {
                    case 2: payloadStr += "=="; break;
                    case 3: payloadStr += "="; break;
                }
                var bytes = Convert.FromBase64String(payloadStr);
                using var doc = JsonDocument.Parse(bytes);
                var root = doc.RootElement;
                if (root.TryGetProperty("upn", out var u)) email = u.GetString() ?? email;
                else if (root.TryGetProperty("email", out var e)) email = e.GetString() ?? email;
                else if (root.TryGetProperty("unique_name", out var un)) email = un.GetString() ?? email;
                if (root.TryGetProperty("tid", out var t)) tenantId = t.GetString() ?? "";

                var partsEmail = email.Split('@')[0].Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (partsEmail.Length >= 2)
                {
                    initials = $"{char.ToUpper(partsEmail[0][0])}{char.ToUpper(partsEmail[1][0])}";
                }
                else if (partsEmail.Length == 1 && partsEmail[0].Length >= 2)
                {
                    initials = partsEmail[0].Substring(0, 2).ToUpper();
                }
            }
        }
        catch {}

        // Pre-warm encrypted cache session key asynchronously in RAM
        _ = Task.Run(async () =>
        {
            try { await cacheService.GetOrCreateSessionKeyAsync(CancellationToken.None); } catch {}
        });

        bool hasServicePrincipal = !string.IsNullOrWhiteSpace(settings.AzureTenantId) && 
                                   !string.IsNullOrWhiteSpace(settings.AzureClientId);

        return Results.Ok(new
        {
            isConnected = true,
            state = "connected",
            authMethod = hasServicePrincipal ? "ServicePrincipal" : "InteractiveSSO",
            profile = new
            {
                email,
                initials,
                subscriptionName = "Azure Cloud Active",
                tenantId
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            isConnected = false,
            state = "disconnected",
            reason = ex.Message
        });
    }
})
.WithName("GetAuthStatus");

app.MapPost("/api/auth/login-interactive", async (
    [FromServices] DynamicAzureCredential dynamicCred,
    CancellationToken ct) =>
{
    try
    {
        var message = await dynamicCred.LoginInteractiveAsync(ct);
        return Results.Ok(new { success = true, message });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { success = false, message = ex.Message });
    }
})
.WithName("LoginInteractive");

app.MapPost("/api/auth/logout", async (
    [FromServices] EncryptedCacheService cacheService,
    CancellationToken ct) =>
{
    cacheService.ClearSessionKey();
    DynamicAzureCredential.ClearInteractiveCredential();
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "az",
            Arguments = OperatingSystem.IsWindows() ? "/c az logout" : "logout",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc != null) await proc.WaitForExitAsync(ct);
    }
    catch {}

    return Results.Ok(new { success = true, message = "Logged out successfully." });
})
.WithName("Logout");

app.MapGet("/api/cache/stats", async (EncryptedCacheService cacheService, CancellationToken ct) =>
{
    var stats = await cacheService.GetStatsAsync(ct);
    return Results.Ok(stats);
})
.WithName("GetCacheStats");

app.MapPost("/api/cache/clear", async (HttpContext context, EncryptedCacheService cacheService, CancellationToken ct) =>
{
    string? category = context.Request.Query["category"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(category) && context.Request.HasJsonContentType())
    {
        try
        {
            var body = await context.Request.ReadFromJsonAsync<ClearCacheRequest>(cancellationToken: ct);
            category = body?.Category;
        }
        catch {}
    }

    if (!string.IsNullOrWhiteSpace(category))
    {
        await cacheService.ClearCategoryAsync(category, ct);
        return Results.Ok(new { success = true, message = $"Cache category '{category}' cleared successfully." });
    }
    await cacheService.ClearAllAsync(ct);
    return Results.Ok(new { success = true, message = "All local encrypted cache cleared successfully." });
})
.WithName("ClearCache");

app.MapPost("/api/cache/settings", async ([FromBody] CacheTtlSettings req, AppSettingsService settingsService, CancellationToken ct) =>
{
    var settings = await settingsService.GetSettingsAsync(ct);
    settings.CacheEnabled = req.Enabled;
    settings.CachePipelinesTtlMinutes = req.PipelinesTtlMinutes;
    settings.CachePipelineRunsTtlMinutes = req.PipelineRunsTtlMinutes;
    settings.CacheVaultKeysTtlMinutes = req.VaultKeysTtlMinutes;
    settings.CacheVariableGroupsTtlMinutes = req.VariableGroupsTtlMinutes;
    await settingsService.SaveSettingsAsync(settings, ct);
    return Results.Ok(new { success = true, message = "Cache settings updated successfully." });
})
.WithName("UpdateCacheSettings");

app.MapPost("/api/settings", async (
    [FromBody] UpdateSettingsDto req,
    AppSettingsService appSettingsService,
    SecretStoreManager secretStoreManager,
    CancellationToken ct) =>
{
    try
    {
        if (!string.IsNullOrWhiteSpace(req.StorageProvider))
        {
            await secretStoreManager.SwitchProviderAsync(req.StorageProvider, ct);
        }

        var settings = await appSettingsService.GetSettingsAsync(ct);
        if (req.AzureTenantId != null) settings.AzureTenantId = req.AzureTenantId;
        if (req.AzureClientId != null) settings.AzureClientId = req.AzureClientId;
        if (req.ClearAzureClientSecret)
        {
            await secretStoreManager.DeleteSecretAsync(SecretStoreManager.AzureClientSecretKey, ct);
        }
        else if (!string.IsNullOrWhiteSpace(req.AzureClientSecret))
        {
            await secretStoreManager.SetSecretAsync(SecretStoreManager.AzureClientSecretKey, req.AzureClientSecret.Trim(), ct);
        }

        if (req.AdoOrganization != null) settings.AdoOrganization = req.AdoOrganization;
        if (req.AdoProject != null) settings.AdoProject = req.AdoProject;
        if (req.GithubBaseUrl != null) settings.GithubBaseUrl = req.GithubBaseUrl;
        if (req.GithubOrganization != null) settings.GithubOrganization = req.GithubOrganization;

        if (req.CacheEnabled.HasValue) settings.CacheEnabled = req.CacheEnabled.Value;
        if (req.CachePipelinesTtlMinutes.HasValue) settings.CachePipelinesTtlMinutes = req.CachePipelinesTtlMinutes.Value;
        if (req.CachePipelineRunsTtlMinutes.HasValue) settings.CachePipelineRunsTtlMinutes = req.CachePipelineRunsTtlMinutes.Value;
        if (req.CacheVaultKeysTtlMinutes.HasValue) settings.CacheVaultKeysTtlMinutes = req.CacheVaultKeysTtlMinutes.Value;
        if (req.CacheVariableGroupsTtlMinutes.HasValue) settings.CacheVariableGroupsTtlMinutes = req.CacheVariableGroupsTtlMinutes.Value;
        if (req.AzureAuthTimeoutSeconds.HasValue) settings.AzureAuthTimeoutSeconds = req.AzureAuthTimeoutSeconds.Value;

        await appSettingsService.SaveSettingsAsync(settings, ct);

        if (req.ClearAdoPat)
        {
            await secretStoreManager.DeleteSecretAsync(SecretStoreManager.AdoPatKey, ct);
        }
        else if (!string.IsNullOrWhiteSpace(req.AdoPat))
        {
            await secretStoreManager.SetSecretAsync(SecretStoreManager.AdoPatKey, req.AdoPat.Trim(), ct);
        }

        if (req.ClearGithubToken)
        {
            await secretStoreManager.DeleteSecretAsync(SecretStoreManager.GithubTokenKey, ct);
        }
        else if (!string.IsNullOrWhiteSpace(req.GithubToken))
        {
            await secretStoreManager.SetSecretAsync(SecretStoreManager.GithubTokenKey, req.GithubToken.Trim(), ct);
        }

        return Results.Ok(new { message = "Settings saved successfully." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateSettings");

app.MapPost("/api/settings/test-ado", async (
    [FromBody] TestAdoRequest req,
    [FromServices] AdoAuthService adoAuthService,
    CancellationToken ct) =>
{
    var (success, message, authMethod) = await adoAuthService.TestAdoConnectionAsync(req.Organization, req.Project, req.Pat, ct);
    return Results.Ok(new { success, message, authMethod });
})
.WithName("TestAdoConnection");

app.MapPost("/api/settings/test-github", async (
    [FromBody] TestGithubRequest req,
    [FromServices] AdoAuthService adoAuthService,
    CancellationToken ct) =>
{
    var (success, message) = await adoAuthService.TestGithubConnectionAsync(req.BaseUrl, req.Organization, req.Token, ct);
    return Results.Ok(new { success, message });
})
.WithName("TestGithubConnection");

app.MapPost("/api/settings/test-azure", async (
    [FromBody] TestAzureRequest req,
    [FromServices] TokenCredential credential,
    CancellationToken ct) =>
{
    try
    {
        TokenCredential testCred = credential;
        if (!string.IsNullOrWhiteSpace(req.TenantId) && 
            !string.IsNullOrWhiteSpace(req.ClientId) && 
            !string.IsNullOrWhiteSpace(req.ClientSecret))
        {
            testCred = new Azure.Identity.ClientSecretCredential(req.TenantId.Trim(), req.ClientId.Trim(), req.ClientSecret.Trim());
        }

        var armClient = new Azure.ResourceManager.ArmClient(testCred);
        var sub = await armClient.GetDefaultSubscriptionAsync(ct);
        return Results.Ok(new 
        { 
            success = true, 
            message = $"Connected to Azure subscription '{sub.Data.DisplayName}' ({sub.Data.SubscriptionId})." 
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new 
        { 
            success = false, 
            message = $"Azure connection failed: {ex.Message}" 
        });
    }
})
.WithName("TestAzureConnection");

app.Run();

public record LogUrlRequest(string Url);
public record ClearCacheRequest(string? Category);
public record UpdateSettingsDto(
    string? AzureTenantId,
    string? AzureClientId,
    string? AzureClientSecret,
    bool ClearAzureClientSecret,
    string? AdoOrganization,
    string? AdoProject,
    string? AdoPat,
    bool ClearAdoPat,
    string? GithubBaseUrl,
    string? GithubOrganization,
    string? GithubToken,
    bool ClearGithubToken,
    string? StorageProvider,
    bool? CacheEnabled = null,
    int? CachePipelinesTtlMinutes = null,
    int? CachePipelineRunsTtlMinutes = null,
    int? CacheVaultKeysTtlMinutes = null,
    int? CacheVariableGroupsTtlMinutes = null,
    int? AzureAuthTimeoutSeconds = null
);
public record TestAzureRequest(string? TenantId, string? ClientId, string? ClientSecret);
public record TestAdoRequest(string Organization, string Project, string? Pat);
public record TestGithubRequest(string? BaseUrl, string? Organization, string? Token);
