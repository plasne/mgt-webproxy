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

- __CLIENT_ID__ [STRING]: The client ID of an application with the appropriate Graph permissions required to obtain a subscription.

- __CLIENT_SECRET__ [STRING]: The client secret of an application with the appropriate Graph permissions required to obtain a subscription.

## Multi-Tenant

To configure for multi-tenant, you should configure the following environmental variables:

- __INCLUDE_CREDENTIAL_TYPES__ [STRING]: Provide one or more ways to connect to an Azure Key Vault. For production, you should use "mi" if possible. For development, you will probably use "env" or "azcli".

- __KEYVAULT_URL__ [STRING]: The URL of the Key Vault that will hold the tenant secrets.

You will then create 2 Secrets in Key Vault for each tenant you intend to support:

- __tenantid_CLIENT_ID__ [STRING]: The client ID of an application with the appropriate Graph permissions required to obtain a subscription. The `tenantid` refers to the GUID of the Azure AD Directory.

- __tenantid_CLIENT_SECRET__ [STRING]: The client secret of an application with the appropriate Graph permissions required to obtain a subscription. The `tenantid` refers to the GUID of the Azure AD Directory.

## Caching of OBO Access Tokens

There are 2 modes that this solution can operate in:

- __Without Cache__: No authentication is performed. Instead, the Bearer Token is sent directly to the Microsoft Graph in an attempt to create an OBO access token every time an endpoint is called. Generally a user of the MGT ChatList will only create/renew a subscription every 10 minutes, so if there are not many users, this simple flow may be sufficient.

- __With Cache__ (CACHE_SIZE_IN_MB > 0): Authentication is performed on every call against the appropriate tenant Azure AD (based on `tid` in the user's Bearer Token). Each tenant's public signing keys are stored in memory for 1 hour. After a successful authentication, the cache is checked to see if there is already an OBO token for this user. If there is not, it will be exchanged as normal and then cached. These OBO tokens are kept in memory provided thre is enough space (CACHE_SIZE_IN_MB) and the access token has not expired.

## Setup of Accounts

## Config

Create an .env file with the following (or use any other way to set Environmental Variables):

- __ASPNETCORE_ENVIRONMENT__ [STRING, OPTIONAL]: If set to "Development", "azcli" and "env" are used for the INCLUDE_CREDENTIAL_TYPES default. Otherwise, it uses "env" and "mi".

- __PORT__ [INTEGER, DEFAULT: 5000]: The port to start this solution on.

- __CACHE_SIZE_IN_MB__ [INT, DEFAULT: 0]: If set above zero, a cache will be used for the OBO access tokens. This will reduce some of the traffic on Azure AD and improve latency (at the cost of additional memory).

- __INCLUDE_CREDENTIAL_TYPES__ [STRING, DEFAULT: varies]: If you intend to use Key Vault for CLIENT_IDs and CLIENT_SECRETs, you need to authenticate to Key Vault. This setting is a comma-delimited list of all credential types to try. It can include any of the following: "env" (Environment Variables), "mi" (Managed Identity), "token" (Shared Token Cache), "vs" (Visual Studio), "vscode" (Visual Studio Code), "azcli" (Azure CLI), and "browser". You can find out more details on those options in the documentation for `DefaultAzureCredential`. You should select as few options as possible (preferrably one) as each option will have to timeout to move to the next and it can make connections to Key Vault take minutes.

- __KEYVAULT_URL__ [STRING, OPTIONAL]: To use a Key Vault to store CLIENT_IDs and CLIENT_SECRETs, you need to supply a Key Vault URL (ex. <https://pelasne-keyvault-2.vault.azure.net/>).

- __SCOPE__ [STRING, DEFAULT: https://graph.microsoft.com/chat.read]: This determines the permission (or permissions) that are required for obtaining the OBO access token. For MGT, this should generally be one of two things, <https://graph.microsoft.com/chat.read> or <https://graph.microsoft.com/chat.readwrite> as those are the permissions required to obtain the /users/{id}/chats/getAllMessages subscription. See [Create Subscriptions](https://learn.microsoft.com/en-us/graph/api/subscription-post-subscriptions?view=graph-rest-1.0&tabs=http) for more details.

## Appendix

### 500 Internal Server Errors

This application should return HTTP messages that the user can act on in all cases where they have supplied invalid data.

# re: audience

# 2-stage keyvault  + eviction
