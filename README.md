# AzureAdAuthDemo (.NET 8)

Two projects:
- Api: Minimal API secured by Azure AD (accepts JWT bearer with custom scope).
- Client: Console app using OAuth2 Device Code flow, obtaining access_token + refresh_token, auto-refreshes before expiry, and calls the API.

## Azure AD (Entra ID) App Registrations

1) API application (single-tenant recommended)
- Name: AzureAdAuthDemo-Api
- Expose an API:
  - Set Application ID URI to: api://<API-APP-ID> (after creating the app, copy its Application (client) ID and replace <API-APP-ID> with it)
  - Add a scope:
    - Scope name: api.read
    - Who can consent: Admins and users
    - Admin consent display name: Read access to API
    - Save

2) Client application
- Name: AzureAdAuthDemo-Client
- Authentication:
  - Allow public client flows (mobile & desktop): Enabled
- API permissions:
  - Add permission -> My APIs -> AzureAdAuthDemo-Api -> Delegated permissions -> api.read (grant admin consent if needed)
  - Also ensure default Microsoft Graph OpenId permissions: openid, profile, and add offline_access (to get refresh_token)

3) Collect values
- Tenant ID (Directory ID)
- API app Application (client) ID -> used as Audience and inside scope URIs (api://<API-APP-ID>/api.read)
- Client app Application (client) ID

4) Update configuration
- Api/appsettings.json
  - AzureAd:TenantId = your tenant ID
  - AzureAd:Audience = api://<API-APP-ID>
  - AzureAd:Scope = api.read
- Client/appsettings.json
  - AzureAd:TenantId = your tenant ID
  - AzureAd:ClientId = <CLIENT-APP-ID>
  - AzureAd:Scopes => replace api://YOUR_API_APP_ID/api.read with your API's URI, keep offline_access, openid, profile

## Run
- Terminal 1:
  dotnet run --project Api
- Terminal 2:
  dotnet run --project Client
  Follow the printed device code instructions, sign in, then the client will call the API with the access token. The access token is refreshed automatically using the refresh token when within 2 minutes of expiry.

## Notes
- API listens on http://localhost:5000 (configure in Api/appsettings.json Kestrel section).
- The API enforces the scope "api.read" (configurable). The token must have scp that includes this value and aud equal to api://<API-APP-ID>.
- The client persists tokens in tokencache.json (not encrypted; for demo only). For production, use secure storage.
- If you change scopes or permissions, you may need to remove tokencache.json and re-authenticate.
