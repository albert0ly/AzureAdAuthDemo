using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

// Read Azure AD settings
var azureAd = builder.Configuration.GetSection("AzureAd");
var tenantId = azureAd["TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId not configured");
var authority = azureAd["Authority"] ?? $"https://login.microsoftonline.com/{tenantId}/v2.0";
var audience = azureAd["Audience"] ?? throw new InvalidOperationException("AzureAd:Audience (api://<API-APP-ID>) not configured");
var requiredScope = azureAd["Scope"] ?? "api.read";

// Optional: turn on detailed identity model logging during development
IdentityModelEventSource.ShowPII = builder.Environment.IsDevelopment();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;

        // Accept both audience formats: 'api://<APP-ID>' and '<APP-ID>'
        var validAudiences = audience.StartsWith("api://", StringComparison.OrdinalIgnoreCase)
            ? new[] { audience, audience.Substring("api://".Length) }
            : new[] { audience, $"api://{audience}" };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = true,
            ValidAudiences = validAudiences,
            // The Authority above pulls metadata (issuer/signing keys). No need to hardcode issuer.
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            // Handle both unmapped and mapped scope claim types
            var scopeClaim = ctx.User.FindFirst("scp")?.Value
                             ?? ctx.User.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value;
            if (string.IsNullOrWhiteSpace(scopeClaim)) return false;
            var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return scopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase);
        });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/ping", (ClaimsPrincipal user) =>
{
    var name = user.Identity?.Name ?? user.FindFirst("name")?.Value ?? "anonymous";
    return Results.Ok(new { message = "pong", user = name });
}).RequireAuthorization("ApiScope");

app.MapGet("/me", (ClaimsPrincipal user) =>
{
    var identity = new
    {
        Name = user.Identity?.Name,
        AuthenticationType = user.Identity?.AuthenticationType,
        IsAuthenticated = user.Identity?.IsAuthenticated ?? false
    };

    var claims = user.Claims
        .GroupBy(c => c.Type)
        .ToDictionary(g => g.Key, g => g.Select(c => c.Value).Distinct().ToArray());

    return Results.Ok(new { identity, claims });
}).RequireAuthorization("ApiScope");

app.Run();
