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

app.MapPost("/api/vaults/usage", async ([FromBody] List<string> vaultUris, KeyVaultManagementService service) =>
{
    var stats = await service.GetVaultUsageStatsAsync(vaultUris);
    return Results.Ok(stats);
})
.WithName("GetVaultsUsage");

app.MapGet("/api/subscriptions", async (KeyVaultManagementService service) =>
{
    var subs = await service.GetSubscriptionsAsync();
    return Results.Ok(subs);
})
.WithName("GetSubscriptions");

app.Run();
