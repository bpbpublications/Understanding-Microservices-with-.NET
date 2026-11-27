# Chapter 11 · Implementing Security Across Microservices

[All exercise solutions](README.md)

## 1. Public reads and protected writes

[Catalog](labs/Catalog/Program.cs) validates JWT access tokens. Its [controller](labs/Catalog/ProductsController.cs) allows anonymous GET and marks POST, PUT and DELETE with Authorize. Use `Identity__Authority=https://localhost:5005` to connect the chapter's identity service. Register an API resource named `catalog_api` associated with the scope `catalog_api` so the issuer supplies the audience the API validates. The chapter's scope-only configuration may instead emit a static `/resources` audience. Keep issuer, signature, expiry and audience validation enabled.

For a stand-alone equivalent, use `dotnet user-jwts` as shown in the [lab instructions](labs/README.md#local-catalog-tokens). Test one request both ways:

```sh
curl -i http://localhost:5080/products/1
data='{"name":"Test","priceCents":1200,"categoryId":1,"brandId":1}'
curl -i -H 'Content-Type: application/json' -d "$data" http://localhost:5080/products
curl -i -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "$data" http://localhost:5080/products
```

Expected responses are 200, 401 and 201. Use an access token issued for Catalog. An ID token serves a different purpose. [JWT bearer configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)

## 2. Claim-based policy

GET `/color` uses HasFavoriteColor, registered with `RequireAuthenticatedUser().RequireClaim("favorite_color")`. Request it with no token (401), a valid token without the claim (403), then a newly issued token containing `favorite_color=blue` (200). Configure the issuer to include the claim in its **access token**. Changing a signed token's payload invalidates its signature.

## 3. Ordering calls Catalog

Use a confidential client registered for the Catalog scope. Supply its secret from configuration. The following code is the body of an async method with an HttpClient named `http` and trusted HTTPS `issuer` and `catalog` base URIs.

```csharp
using var form = new FormUrlEncodedContent(new Dictionary<string,string> {
    ["grant_type"] = "client_credentials", ["client_id"] = "ordering_service_client",
    ["client_secret"] = secret, ["scope"] = "catalog_api"
});
using var tokenReply = await http.PostAsync(new Uri(issuer, "/connect/token"), form);
tokenReply.EnsureSuccessStatusCode();
using var payload = JsonDocument.Parse(await tokenReply.Content.ReadAsStringAsync());
var token = payload.RootElement.GetProperty("access_token").GetString();
using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(catalog, "/internal/products/1"));
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
using var result = await http.SendAsync(request);
result.EnsureSuccessStatusCode();
```

With the seeded product, expect 200 and its ID, name and price. The route requires both the Catalog scope and the registered Ordering client ID. A missing token returns 401. An authenticated client with the wrong ID or scope gets 403. Cache a successful token until shortly before its expiry. Never log the secret or bearer token.
