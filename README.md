# KeyVaultComparer: Azure Key Vault Comparer

KeyVaultComparer is a professional developer tool designed to visually compare secrets across multiple Azure Key Vault environments (e.g., Dev, Stg, UAT, QA). It allows you to instantly identify mismatches, missing secrets, and uniform values across your infrastructure.

## Table of Contents
- [Prerequisites](#prerequisites)
- [Authentication](#authentication)
- [Quick Start](#quick-start)
- [Manual Startup](#manual-startup)
- [Features & System Specifications](./FEATURES.md)
- [Azure Key Vault API Strategies](./azure-keyvault-api-strategies.md)

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (v18+ recommended)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)

## Authentication
This application uses your active Azure CLI session to authenticate against Azure Key Vaults.
Before starting the application, ensure you are logged into Azure:
```bash
az login
```

## Quick Start
To start both the backend and frontend simultaneously, simply run the provided PowerShell script from the root directory:
```powershell
.\start-all.ps1
```

## Manual Startup
If you prefer to start the services individually:

### 1. Start the Backend (.NET API)
```bash
cd KeyVaultComparer.Api
dotnet run
```
The API will be available at `http://localhost:5065`

### 2. Start the Frontend (Vue 3 + Vite)
```bash
cd keyvaultcomparer-ui
npm install
npm run dev
```
The UI will be available at `http://localhost:5173`


