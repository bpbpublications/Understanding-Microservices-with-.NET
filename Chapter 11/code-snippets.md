# Chapter 11: Implementing Security Across Microservices - Code Snippets

## Setting up IdentityServer4 in the Identity Microservice

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add IdentityServer4 services
builder.Services.AddIdentityServer(options =>
{
    options.EmitStaticAudienceClaim = true; // ensure 'aud' claim is present
})
.AddDeveloperSigningCredential() // for demo purposes; in production use a persistent key/certificate
.AddInMemoryApiScopes(new[] {
    new ApiScope("catalog_api", "Catalog API"),
    new ApiScope("ordering_api", "Ordering API"),
    new ApiScope("basket_api", "Basket API"),
    new ApiScope("shipping_api", "Shipping API")
})
.AddInMemoryClients(new[] {
    new Client {
        ClientId = "ecommerce_web_client",
        AllowedGrantTypes = GrantTypes.Code, // using OIDC code flow for a web app or Single Page App
        RedirectUris = { "https://localhost:5001/signin-oidc" }, // client app's redirect after login
        AllowedScopes = { "openid", "profile", "catalog_api", "ordering_api", "basket_api", "shipping_api" },
        RequirePkce = true,
        RequireClientSecret = false // (for a public SPA client; true for confidential clients)
    },
    new Client {
        ClientId = "ordering_service_client",
        ClientSecrets = { new Secret("P@ssw0rd".Sha256()) },
        AllowedGrantTypes = GrantTypes.ClientCredentials,
        AllowedScopes = { "catalog_api" } // allows Ordering service to call Catalog service
    }
})
.AddTestUsers(TestUsers.List); // optional: add some test users

var app = builder.Build();

// Use IdentityServer middleware
app.UseIdentityServer();

app.Run();
```

## Configuring JWT Bearer Authentication in a Microservice (Catalog Example)

```csharp
var builder = WebApplication.CreateBuilder(args);

// ... (other service registrations like controllers, DB, etc.)

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.Authority = "https://localhost:5005"; // URL of Identity microservice
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = "https://localhost:5005", // must match Identity server's issuer
        ValidateAudience = true,
        ValidAudience = "catalog_api", // token must be intended for the Catalog API
        ValidateLifetime = true
        // (Other options like clock skew, etc., can be set as needed)
    };
    options.RequireHttpsMetadata = false; // for development, allow HTTP. In production, use true (HTTPS only).
});

builder.Services.AddAuthorization(options =>
{
    // Define a default policy that requires authenticated users
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    // (We will add specific role/claim-based policies in the next sections)
});

// ... build app
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// ... map controllers, run app
```

## Configuring Policy-Based Access Control - Department Policy Example

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ShippingDeptOnly", policy =>
        policy.RequireClaim("department", "Shipping"));
});
```

## Configuring Policy-Based Access Control - Scope Policy Example

```csharp
options.AddPolicy("ScopeShippingAPI", policy =>
    policy.RequireClaim("scope", "shipping_api"));
```

## Configuring Policy-Based Access Control - Combined Requirements Example

```csharp
options.AddPolicy("CanShipOrder", policy =>
{
    policy.RequireRole("WarehouseStaff");
    policy.RequireClaim("scope", "shipping_api");
});
```

## Service-to-Service Communication using Client Credentials

```csharp
// Inside Ordering service, preparing an HTTP call to Catalog service

using var httpClient = new HttpClient();

// 1. Obtain a token from IdentityServer using client credentials
var tokenResponse = await httpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
{
    Address = "https://localhost:5005/connect/token", // IdentityServer token endpoint
    ClientId = "ordering_service_client",
    ClientSecret = "P@ssw0rd",
    Scope = "catalog_api"
});

if (tokenResponse.IsError) throw new Exception(tokenResponse.Error);

// 2. Attach the obtained token to the outgoing request
string accessToken = tokenResponse.AccessToken;
var request = new HttpRequestMessage(HttpMethod.Get, $"{catalogServiceBaseUrl}/api/Products/123");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

// 3. Send request to Catalog service
HttpResponseMessage catalogResponse = await httpClient.SendAsync(request);

if(catalogResponse.IsSuccessStatusCode)
{
    var productData = await catalogResponse.Content.ReadAsStringAsync();
    // ... use productData (e.g., verify price or get details) in the Ordering workflow
}
```
