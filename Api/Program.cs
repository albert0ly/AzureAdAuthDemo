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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = audience,
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
            var scopeClaim = ctx.User.FindFirst("scp")?.Value; // space-separated scopes
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

app.Run();
