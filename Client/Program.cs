using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var azureAd = config.GetSection("AzureAd");
var tenantId = azureAd["TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId not configured");
var clientId = azureAd["ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId not configured");
var authorityBase = azureAd["Authority"] ?? "https://login.microsoftonline.com";
var authority = $"{authorityBase.TrimEnd('/')}/{tenantId}";
var scopes = azureAd.GetSection("Scopes").Get<string[]>() ?? throw new InvalidOperationException("AzureAd:Scopes not configured");

var api = config.GetSection("Api");
var apiBase = api["BaseUrl"] ?? "http://localhost:5000";

var cachePath = config.GetSection("TokenCache")["FilePath"] ?? "tokencache.json"; // will store MSAL cache bytes

var tokenService = new MsalInteractiveTokenService(authority, clientId, cachePath);
var accessToken = await tokenService.GetAccessTokenAsync(scopes);

using var http = new HttpClient { BaseAddress = new Uri(apiBase) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

var res = await http.GetAsync("/ping");
Console.WriteLine($"API status: {(int)res.StatusCode} {res.ReasonPhrase}");
var body = await res.Content.ReadAsStringAsync();
Console.WriteLine("Response body:");
Console.WriteLine(body);

public sealed class MsalInteractiveTokenService
{
    private readonly string _authority;
    private readonly string _clientId;
    private readonly string _cachePath;
    private readonly IPublicClientApplication _pca;

    public MsalInteractiveTokenService(string authority, string clientId, string cachePath)
    {
        _authority = authority.TrimEnd('/');
        _clientId = clientId;
        _cachePath = cachePath;

        _pca = PublicClientApplicationBuilder
            .Create(_clientId)
            .WithAuthority(_authority)
            .WithRedirectUri("http://localhost") // system browser
            .Build();

        FileTokenCacheHelper.Bind(_pca.UserTokenCache, _cachePath);
    }

    public async Task<string> GetAccessTokenAsync(string[] scopes)
    {
        // Try silent first (will auto-refresh using refresh token if near expiry)
        var accounts = await _pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        if (account != null)
        {
            try
            {
                var silent = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                return silent.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                // fall through to interactive
            }
        }

        // Interactive sign-in (opens system browser)
        var interactive = await _pca
            .AcquireTokenInteractive(scopes)
            .WithPrompt(Prompt.SelectAccount)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync();

        return interactive.AccessToken;
    }
}

public static class FileTokenCacheHelper
{
    public static void Bind(ITokenCache tokenCache, string cacheFilePath)
    {
        tokenCache.SetBeforeAccess(args =>
        {
            try
            {
                if (File.Exists(cacheFilePath))
                {
                    var data = File.ReadAllBytes(cacheFilePath);
                    // If the file is not a valid MSAL cache (e.g., leftover JSON), ignore errors.
                    try { args.TokenCache.DeserializeMsalV3(data, shouldClearExistingCache: true); }
                    catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        });

        tokenCache.SetAfterAccess(args =>
        {
            try
            {
                if (args.HasStateChanged)
                {
                    var dir = Path.GetDirectoryName(cacheFilePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    var data = args.TokenCache.SerializeMsalV3();
                    File.WriteAllBytes(cacheFilePath, data);
                }
            }
            catch { /* ignore */ }
        });
    }
}
