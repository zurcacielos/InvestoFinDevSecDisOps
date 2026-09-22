# Azure Key Vault Component Strategies for Server-to-Server Integrations

This guide outlines when to use Azure Key Vault Keys, Secrets, and Certificates, specifically for microservices or Web APIs connecting server-to-server to external platforms.

## Azure Key Vault Components

1. **Keys**: Used for cryptographic operations (signing, encryption) where the private key material *never* leaves the Key Vault.  
2. **Secrets**: Used for storing raw key material (like SSH keys, API keys, Client Secrets) that must be extracted into application memory to function.  
3. **Certificates**: Used when a private key is tied to an X.509 certificate. Key Vault manages the lifecycle, public cert, and private key together.

## API Integration Strategies

### 1\. Salesforce (OAuth 2.0 JWT Bearer Flow)

* **Component**: Certificate  
* **Reason**: Server-to-server connection requires signing a JWT. Upload the public X.509 certificate to Salesforce. The application uses the Key Vault Certificate to retrieve the PFX for local signing, or utilizes Key Vault Keys for remote signing without exposing the key.

### 2\. Okta (Identity Provider)

* **SAML Integration (Service Provider)**  
  * **Component**: Certificate  
  * **Reason**: SAML relies on X.509 certificates to decrypt assertions or sign authentication requests. Applications retrieve the raw PFX/PEM secret from the Certificate object.  
* **OIDC/OAuth 2.0 Integration**  
  * **Component**: Secret  
  * **Reason**: Uses a symmetric "Client Secret" (alphanumeric string) for basic client authentication. There is no associated certificate.

### 3\. Plaid (Financial Data Network)

* **Component**: Secret  
* **Reason**: Plaid API authenticates server-to-server requests using a `client_id` and a `secret` (a symmetric alphanumeric string). It does not use asymmetric cryptography or X.509 certificates for client authentication.

### 4\. GitHub (GitHub Apps)

* **Component**: Secret  
* **Reason**: GitHub Apps authenticate as an installation by signing a JWT with a PEM-encoded RSA private key. Because most JWT libraries expect to load the raw PEM into memory, it is typically stored as a Secret.