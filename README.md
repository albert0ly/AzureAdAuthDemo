# AzureAdAuthDemo (.NET 8)

Two projects:
- Api: Minimal API secured by Azure AD (accepts JWT bearer with a custom scope).
- Client: Console app using interactive sign-in (OAuth2 authorization code with PKCE via MSAL). It obtains access_token + refresh_token, caches tokens, silently refreshes before expiry, and calls the API.

This setup is suitable for C2B scenarios (end users interactively signing in), and for B2B as well.

## Azure AD (Entra ID) App Registrations

1) API application (single-tenant recommended unless you target consumers)
- Name: AzureAdAuthDemo-Api
- Expose an API:
  - After creating the app, copy its Application (client) ID. Set the Application ID URI to: `api://<API-APP-ID>` (replace `<API-APP-ID>` with the Application ID you copied).
  - Add a scope:
    - Scope name: `api.read`
    - Who can consent: Admins and users
    - Admin consent display name: Read access to API
    - Save

2) Client application (Public client)
- Name: AzureAdAuthDemo-Client
- Authentication:
  - Add a platform: Mobile and desktop applications
  - Add Redirect URI: `http://localhost`
  - (Optional) "Allow public client flows" is not required for interactive auth-code, but can be enabled; it is needed only if you plan to use device code or ROPC.
- API permissions:
  - Add permission -> My APIs -> AzureAdAuthDemo-Api -> Delegated permissions -> `api.read` (grant admin consent if needed)
  - Also ensure default OpenId permissions: `openid`, `profile`, and add `offline_access` (to get refresh_token)

3) Supported account types (tenancy)
- If you need consumer sign-in (personal Microsoft accounts), set both apps to: "Accounts in any organizational directory and personal Microsoft accounts (Any Azure AD directory - Multitenant and personal Microsoft accounts)".
- For internal use, single-tenant is recommended.

4) Collect values
- Tenant ID (Directory ID)
- API app Application (client) ID -> used as Audience and inside scope URIs (`api://<API-APP-ID>/api.read`)
- Client app Application (client) ID

## Update configuration

- Api/appsettings.json
  - `AzureAd:TenantId` = your tenant ID
  - `AzureAd:Authority` = `https://login.microsoftonline.com/<TenantId>/v2.0`
  - `AzureAd:Audience` = `api://<API-APP-ID>`
  - `AzureAd:Scope` = `api.read`

- Client/appsettings.json
  - `AzureAd:TenantId` = your tenant ID
  - `AzureAd:ClientId` = `<CLIENT-APP-ID>`
  - `AzureAd:Authority` = `https://login.microsoftonline.com`
  - `AzureAd:Scopes` => replace `api://YOUR_API_APP_ID/api.read` with your API's URI; keep `offline_access`, `openid`, `profile`
  - `TokenCache:FilePath` = path to persist the MSAL token cache (defaults to `tokencache.json` in this demo). The file contains MSAL cache bytes (not JSON) and is not encrypted; for production, use a secure store.

## Run
- Terminal 1:
  - `dotnet run --project Api`
- Terminal 2:
  - `dotnet run --project Client`
  - A browser window will open to complete interactive sign-in. After sign-in, the client will call the API with the access token. The access token is refreshed automatically and silently using the refresh token when needed.

## Notes
- API listens on `http://localhost:5000` (configure in Api/appsettings.json Kestrel section).
- The API enforces the scope `api.read` (configurable). The token must have `scp` that includes this value and `aud` equal to `api://<API-APP-ID>`.
- Token cache: The client uses MSAL's token cache persisted to `tokencache.json` by default. To reset the session (e.g., after changing scopes or tenants), delete this file and sign in again.
- Flow used: Public client interactive sign-in using Authorization Code + PKCE via MSAL, launching the system browser and listening on the loopback redirect URI `http://localhost`.
- For national clouds (e.g., Azure Government), adjust the authority base URL accordingly in the Client configuration.

## Troubleshooting
- 401/403 from API:
  - Verify the token’s `aud` equals `api://<API-APP-ID>` and `scp` contains `api.read`.
  - Ensure the client app has the delegated permission to your API and that admin consent is granted if required.
- `AADSTS65001` (consent required): Grant admin consent or have the user consent to the permissions.
- Redirect URI mismatch: Confirm the client app registration has `http://localhost` configured under "Mobile and desktop applications".
- Switching scopes/tenants: Remove `tokencache.json` and sign in again.
- Personal Microsoft accounts: Ensure both app registrations are configured for "Accounts in any organizational directory and personal Microsoft accounts" if targeting consumers.
