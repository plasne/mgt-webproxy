# MGT Web Proxy

The Microsoft Graph Toolkit provides functionality for integrating chat into 3rd party applications similar to Teams functionality. If you plan to use the ChatList component (a list of chat threads), this requires a metered Graph API. Those metered Graph APIs require a confidential client (something like a Web API) unlike the Single-Page Application login that MGT offers. This project serves as an example on how to build a proxy for subscribing to metered notifications for use with MGT.

Simply, this project has 2 endpoints:

- __POST /subscriptions__: The user passes a subscription payload like they normally would to the [MSFT Graph POST /subscriptions](https://learn.microsoft.com/en-us/graph/api/subscription-post-subscriptions?view=graph-rest-1.0&tabs=http) endpoint. They use a Bearer Token that is configured for an on-behalf-of flow. That token is converted into an access token obtained via a confidential client OBO flow in combination with a stored CLIENT_ID and CLIENT_SECRET for that customer. The token is used to create a subscription in the Graph and then that subscription is used to negotiate a Signal-R connection. Both the subscription and negotiation information are returned.

- __PATCH /subscriptions/{id}__: The user passes a subscription payload like they normally would to the [MSFT Graph PATCH /subscriptions/{id}](https://learn.microsoft.com/en-us/graph/api/subscription-update?view=graph-rest-1.0&tabs=http) endoint to renew a subscription. The authentication process is similar to the above. The endpoint returns just the new subscription information.

## Public vs. Confidential Client

Public client flows typically include Single-Page Applications (SPAs) and mobile applications. There is no secret exchanged for the application itself. The flow is protected with additional features such as [PKCE](https://oauth.net/2/pkce/).

Confidential client flows typically include Web APIs where the secrets are stored and exchanged from a secure server and never passed to the client.

## Single-Tenant

This solution is designed to support both single-tenant and multi-tenant configurations. To configure for single-tenant, you should configure the following environmental variables:

- __CLIENT_ID__ [STRING]: The client ID of an application with the appropriate Graph permissions required to obtain a subscription. (AppB)

- __CLIENT_SECRET__ [STRING]: The client secret of an application with the appropriate Graph permissions required to obtain a subscription. (AppB)

## Multi-Tenant

To configure for multi-tenant, you should configure the following environmental variables:

- __INCLUDE_CREDENTIAL_TYPES__ [STRING]: Provide one or more ways to connect to an Azure Key Vault. For production, you should use "mi" if possible. For development, you will probably use "env" or "azcli".

- __KEYVAULT_URL__ [STRING]: The URL of the Key Vault that will hold the tenant secrets.

You will then create 2 Secrets in Key Vault for each tenant you intend to support:

- __tenantid_CLIENT_ID__ [STRING]: The client ID of an application with the appropriate Graph permissions required to obtain a subscription. The `tenantid` refers to the GUID of the Azure AD Directory. (AppB)

- __tenantid_CLIENT_SECRET__ [STRING]: The client secret of an application with the appropriate Graph permissions required to obtain a subscription. The `tenantid` refers to the GUID of the Azure AD Directory. (AppB)

## Caching of OBO Access Tokens

There are 2 modes that this solution can operate in:

- __Without Cache__: No authentication is performed. Instead, the Bearer Token is sent directly to the Microsoft Graph in an attempt to create an OBO access token every time an endpoint is called. Generally a user of the MGT ChatList will only create/renew a subscription every 10 minutes, so if there are not many users, this simple flow may be sufficient.

- __With Cache__ (CACHE_SIZE_IN_MB > 0): Authentication is performed on every call against the appropriate tenant Azure AD (based on `tid` in the user's Bearer Token). Each tenant's public signing keys are stored in memory for 1 hour. After a successful authentication, the cache is checked to see if there is already an OBO token for this user. If there is not, it will be exchanged as normal and then cached. These OBO tokens are kept in memory provided thre is enough space (CACHE_SIZE_IN_MB) and the access token has not expired.

## Setup of Applications

There are 2 applications required per customer for this solution to work. The customer should perform these steps:

1. Go to <https://entra.microsoft.com/>.
2. Navigate to "Applications / App registrations".
3. Follow these steps to create AppA:
    1. Click "New registration".
    2. Name your application "obo-graph-subscriber".
    3. Leave the selection on "Single tenant".
    4. Choose "Single-page application (SPA)" and provide a local address of "<http://localhost:5000>". Later you can change or add your deployed address.
    5. Click "Register".
    6. Go to "Owners".
    7. Click "Add owners".
    8. Add yourself as an owner.
    9. Navigate to "Applications / App registrations".
4. Follow these steps to create AppB:
    1. Click "New registration".
    2. Name your application "actual-graph-subscriber".
    3. Leave the selection on "Single tenant".
    4. Click "Register".
    5. Go to "API permissions".
    6. Remove all existing permissions.
    7. Add a "Microsoft Graph" permission that is "Delegated" of "Chat.Read".
    8. Click "Grant admin consent" and confirm your selection.
    9. Go to "Expose an API".
    10. Click "add" and "Save". This will create an "Application ID URI".
    11. Click "Add a scope".
    12. Use a "Scope name" of "API.All".
    13. Leave it on "Admins only".
    14. Give it an "Admin consent display name" of "Subscribe and renew subscriptions".
    15. Give it an "Admin consent description" of "Subscribe and renew subscriptions".
    16. Click "Add scope".
    17. Go to "Owners".
    18. Click "Add owners".
    19. Add yourself as an owner.
    20. Go to "Certificates & secrets".
    21. Click "New client secret".
    22. Give it an appropriate name and expiry.
    23. Click "Add".
    24. Copy the Value (client secret) somewhere so you can use that in the configuration.
    25. Navigate to "Applications / App registrations".
5. Refresh the screen if necessary.
6. Click on "obo-graph-subscriber" under "Owned applications".
    1. Click on "API permissions".
    2. Remove all existing permissions.
    3. Click "Add a permission".
    4. Go to the "My APIs" tab.
    5. Select the "actual-graph-subscriber".
    6. Select the "API.All" permission.
    7. Click "Add permissions".
    8. Click "Grant admin consent" and confirm your selection.

## Testing Website

To make this solution easier to test, there is a small website included that you can configure to generate access tokens (in a public client) that can be used to initiate the OBO flow. To configure this website, you need to provide configuration parameters in the wwwroot/config.json file.

```json
{
  "auth": {
    "clientId": "???",
    "authority": "https://login.microsoftonline.com/???",
    "redirectUri": "http://localhost:5000"
  },
  "cache": {
    "cacheLocation": "sessionStorage",
    "storeAuthStateInCookie": false
  },
  "scopes": [
    "api://???/API.All"
  ]
}
```

Then to enable the testing website (ONLY FOR DEVELOPMENT), add an Environment Variable of: `HOST_TEST_SITE=true`.

All the values to replace are stubbed with "???".

- Fill in "clientId" with the appropriate value from AppA.
- Add the Directory (tenant) ID at the end of "authority".
- Change the "redirectUri" if you are hosting on a different port.
- Replace the "???" with the Application (client) ID of the Graph application (AppB) in "scopes".

You can access the website by going to <http://localhost:5000/default.html>. You will see a pop-up and then a text box should populate with a Bearer Token. You can use that to query the APIs.

## Config

__Create an .env file__ with the following (or use any other way to set Environmental Variables):

- __ASPNETCORE_ENVIRONMENT__ [STRING, OPTIONAL]: If set to "Development", "azcli" and "env" are used for the INCLUDE_CREDENTIAL_TYPES default. Otherwise, it uses "env" and "mi".

- __PORT__ [INTEGER, DEFAULT: 5000]: The port to start this solution on.

- __CACHE_SIZE_IN_MB__ [INT, DEFAULT: 0]: If set above zero, a cache will be used for the OBO access tokens. This will reduce some of the traffic on Azure AD and improve latency (at the cost of additional memory).

- __INCLUDE_CREDENTIAL_TYPES__ [STRING, DEFAULT: varies]: If you intend to use Key Vault for CLIENT_IDs and CLIENT_SECRETs, you need to authenticate to Key Vault. This setting is a comma-delimited list of all credential types to try. It can include any of the following: "env" (Environment Variables), "mi" (Managed Identity), "token" (Shared Token Cache), "vs" (Visual Studio), "vscode" (Visual Studio Code), "azcli" (Azure CLI), and "browser". You can find out more details on those options in the documentation for `DefaultAzureCredential`. You should select as few options as possible (preferrably one) as each option will have to timeout to move to the next and it can make connections to Key Vault take minutes.

    You can find out more about the requirements for each of the login types by going to <https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet> and clicking on the appropriate login type. For example, if you are using `env` will need to create an application registration and provide environment variables for AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET.

    If the proxy and the Key Vault are running in Azure in the same tenant, the preferred authentication mechanism is "mi".

- __KEYVAULT_URL__ [STRING, OPTIONAL]: To use a Key Vault to store CLIENT_IDs and CLIENT_SECRETs, you need to supply a Key Vault URL (ex. <https://pelasne-keyvault-2.vault.azure.net/>).

- __SCOPE__ [STRING, DEFAULT: https://graph.microsoft.com/chat.read]: This determines the permission (or permissions) that are required for obtaining the OBO access token. For MGT, this should generally be one of two things, <https://graph.microsoft.com/chat.read> or <https://graph.microsoft.com/chat.readwrite> as those are the permissions required to obtain the /users/{id}/chats/getAllMessages subscription. See [Create Subscriptions](https://learn.microsoft.com/en-us/graph/api/subscription-post-subscriptions?view=graph-rest-1.0&tabs=http) for more details.

- __HOST_TEST_SITE__ [BOOLEAN, DEFAULT: false]: If true (or yes or 1), the test web site will be available. This should ONLY be used for local testing.

## Example Create Subscription

REQUEST:

```bash
curl --location 'http://localhost:5000/api/subscriptions' \
--header 'Content-Type: application/json' \
--header 'Authorization: Bearer ey...redacted...PQ' \
--data '{
    "changeType": "created,updated,deleted",
    "notificationUrl": "websockets:?groupId=328403284",
    "resource": "/users/9eb5efe7-b31a-4fa2-afa8-1ae5a4923e61/chats/getAllmessages",
    "expirationDateTime": "2024-04-18T00:30:00.000000Z",
    "includeResourceData": true,
    "clientState": "wsssecret"
}'
```

RESPONSE:

```json
{
    "subscription": {
        "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#subscriptions/$entity",
        "id": "50c664e1-c835-4dac-a696-bb94d2901deb",
        "resource": "/users/9eb5efe7-b31a-4fa2-afa8-1ae5a4923e61/chats/getAllmessages",
        "applicationId": "c4248217-7e87-495f-a7aa-88882dea9e5b",
        "changeType": "created,updated,deleted",
        "clientState": "wsssecret",
        "notificationUrl": "websockets:https://...redacted...lt",
        "notificationQueryOptions": null,
        "lifecycleNotificationUrl": null,
        "expirationDateTime": "2024-04-18T00:30:00Z",
        "creatorId": "9eb5efe7-b31a-4fa2-afa8-1ae5a4923e61",
        "includeResourceData": true,
        "latestSupportedTlsVersion": null,
        "encryptionCertificate": null,
        "encryptionCertificateId": null,
        "notificationUrlAppId": null
    },
    "negotiate": {
        "@odata.context": "https://graph.microsoft.com/beta/$metadata#microsoft.graph.websocket",
        "negotiateVersion": 0,
        "url": "https://not...redacted...3D",
        "accessToken": "ey...redacted...mc"
    }
}
```

## Example Renew Subscription

REQUEST:

```bash
curl --location --request PATCH 'http://localhost:5000/api/subscriptions/50c664e1-c835-4dac-a696-bb94d2901deb' \
--header 'Content-Type: application/json' \
--header 'Authorization: Bearer ey...redacted...PQ' \
--data '{
    "expirationDateTime": "2024-04-18T00:30:00.000000Z"
}'
```

RESPONSE:

```json
{
    "subscription": {
        "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#subscriptions/$entity",
        "id": "50c664e1-c835-4dac-a696-bb94d2901deb",
        "resource": "/users/9eb5efe7-b31a-4fa2-afa8-1ae5a4923e61/chats/getAllmessages",
        "applicationId": "c4248217-7e87-495f-a7aa-88882dea9e5b",
        "changeType": "created,updated,deleted",
        "clientState": "wsssecret",
        "notificationUrl": "websockets:https://...redacted...lt",
        "notificationQueryOptions": null,
        "lifecycleNotificationUrl": null,
        "expirationDateTime": "2024-04-18T00:30:00.001Z",
        "creatorId": "9eb5efe7-b31a-4fa2-afa8-1ae5a4923e61",
        "includeResourceData": true,
        "latestSupportedTlsVersion": null,
        "encryptionCertificate": null,
        "encryptionCertificateId": null,
        "notificationUrlAppId": null
    }
}
```

## Appendix

### Errors

This application should return HTTP messages that the user can act on in all cases where they have supplied invalid data. In all other cases, the errors will be logged to the console and the user will receive an HTTP 500.

## Audience

NOTE: This section only applies when running with cache.

The Bearer Token supplied from the user will have an audience (aud) which will be a reference to the application (ex. api://e557da30-f7a8-4111-a72b-aac07f91ee5b) responsible for creating the Graph subscriptions (AppB).

The audience is not matched against any source when the token is validated locally. When the token is sent to Graph to get the OBO token, it is of course validated by the Microsoft Graph. Subsequently, after the token is validated locally and it is discovered that there is an OBO token in cache, the audience is checked to make they it is the same as the original. This should be sufficient security, but should you want to restrict which Bearer Tokens are accepted in the local validation, you could get an audience from the customer and put that into a datastore to match against.

## 2-Stage Key Vault

The current Key Vault implementation is a single Key Vault that stores the customer's CLIENT_ID and CLIENT_SECRET for the application that will create Graph subscriptions (AppB). This solution is as secure as it can be - the entity hosting this proxy has access to the keys. There is an alternate case where users could manage their own keys - the hosting entity still has access.

This proposal would involve each customer hosting their own Key Vault with thier own CLIENT_ID and CLIENT_SECRET in that vault. They would give the hosting entity access to read those keys. The secret to read those keys would be stored in a Key Vault managed by the hosting entity.
