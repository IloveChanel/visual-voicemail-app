# Visual Voicemail Pro - Environment Configuration Guide

## 🔐 Google Cloud Setup

### 1. Create Google Cloud Project
```bash
# Install Google Cloud CLI
# https://cloud.google.com/sdk/docs/install

# Create new project
gcloud projects create visualvoicemail-pro-2024

# Set current project
gcloud config set project visualvoicemail-pro-2024

# Enable required APIs
gcloud services enable speech.googleapis.com
gcloud services enable translate.googleapis.com
gcloud services enable storage.googleapis.com
gcloud services enable cloudbuild.googleapis.com
```

### 2. Create Service Account
```bash
# Create service account
gcloud iam service-accounts create visualvoicemail-sa \
    --description="Visual Voicemail Pro Service Account" \
    --display-name="Visual Voicemail SA"

# Grant necessary permissions
gcloud projects add-iam-policy-binding visualvoicemail-pro-2024 \
    --member="serviceAccount:visualvoicemail-sa@visualvoicemail-pro-2024.iam.gserviceaccount.com" \
    --role="roles/speech.admin"

gcloud projects add-iam-policy-binding visualvoicemail-pro-2024 \
    --member="serviceAccount:visualvoicemail-sa@visualvoicemail-pro-2024.iam.gserviceaccount.com" \
    --role="roles/translate.admin"

gcloud projects add-iam-policy-binding visualvoicemail-pro-2024 \
    --member="serviceAccount:visualvoicemail-sa@visualvoicemail-pro-2024.iam.gserviceaccount.com" \
    --role="roles/storage.admin"

# Create and download service account key
gcloud iam service-accounts keys create ./google-cloud-key.json \
    --iam-account=visualvoicemail-sa@visualvoicemail-pro-2024.iam.gserviceaccount.com
```

### 3. Environment Variables Setup

#### Development (.env file):
```bash
# Google Cloud Configuration
GOOGLE_APPLICATION_CREDENTIALS=./google-cloud-key.json
GOOGLE_CLOUD_PROJECT_ID=visualvoicemail-pro-2024
GOOGLE_STORAGE_BUCKET=visualvoicemail-audio-files

# Stripe Configuration
STRIPE_PUBLISHABLE_KEY=pk_test_your_publishable_key_here
STRIPE_SECRET_KEY=sk_test_your_secret_key_here
STRIPE_WEBHOOK_SECRET=whsec_your_webhook_secret_here
STRIPE_PRO_PRICE_ID=price_your_pro_price_id_here
STRIPE_BUSINESS_PRICE_ID=price_your_business_price_id_here

# Database Configuration
SQL_CONNECTION_STRING=Server=(localdb)\\mssqllocaldb;Database=VisualVoicemailPro;Trusted_Connection=true
REDIS_CONNECTION_STRING=localhost:6379

# Firebase Configuration
FIREBASE_PROJECT_ID=visualvoicemail-pro
FIREBASE_DATABASE_URL=https://visualvoicemail-pro-default-rtdb.firebaseio.com/
FIREBASE_STORAGE_BUCKET=visualvoicemail-pro.appspot.com
FIREBASE_MESSAGING_SENDER_ID=your_sender_id_here

# JWT Configuration
JWT_SECRET_KEY=your_super_secure_jwt_secret_key_here_min_32_chars

# Azure Configuration (for production)
AZURE_KEY_VAULT_NAME=visualvoicemail-keyvault
AZURE_CLIENT_ID=your_azure_client_id
AZURE_CLIENT_SECRET=your_azure_client_secret
AZURE_TENANT_ID=your_azure_tenant_id
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=...
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...
```

#### Production (Azure Key Vault):
```bash
# Install Azure CLI
# https://docs.microsoft.com/en-us/cli/azure/install-azure-cli

# Login to Azure
az login

# Create resource group
az group create --name visualvoicemail-rg --location eastus

# Create Key Vault
az keyvault create --name visualvoicemail-keyvault --resource-group visualvoicemail-rg --location eastus

# Add secrets to Key Vault
az keyvault secret set --vault-name visualvoicemail-keyvault --name "GoogleCloudProjectId" --value "visualvoicemail-pro-2024"
az keyvault secret set --vault-name visualvoicemail-keyvault --name "StripeSecretKey" --value "sk_live_your_live_secret_key"
az keyvault secret set --vault-name visualvoicemail-keyvault --name "JwtSecretKey" --value "your_production_jwt_secret"
# ... add all other secrets
```

## 🔧 Development Setup

### PowerShell Setup Script:
```powershell
# setup-development.ps1

# Check if Google Cloud SDK is installed
if (-not (Get-Command "gcloud" -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Google Cloud SDK not found. Please install it first:" -ForegroundColor Red
    Write-Host "https://cloud.google.com/sdk/docs/install" -ForegroundColor Yellow
    exit 1
}

# Check if service account key exists
if (-not (Test-Path "google-cloud-key.json")) {
    Write-Host "❌ Google Cloud service account key not found." -ForegroundColor Red
    Write-Host "Please create and download the service account key as described above." -ForegroundColor Yellow
    exit 1
}

# Set environment variable for current session
$env:GOOGLE_APPLICATION_CREDENTIALS = "$(Get-Location)\google-cloud-key.json"

# Verify Google Cloud authentication
try {
    $projectId = gcloud config get-value project
    Write-Host "✅ Google Cloud authenticated. Project: $projectId" -ForegroundColor Green
} catch {
    Write-Host "❌ Google Cloud authentication failed." -ForegroundColor Red
    exit 1
}

# Test Google Cloud APIs
Write-Host "🧪 Testing Google Cloud APIs..." -ForegroundColor Yellow

# Test Speech API
try {
    gcloud services list --enabled --filter="name:speech.googleapis.com" --format="value(name)" | Out-Null
    Write-Host "✅ Speech API enabled" -ForegroundColor Green
} catch {
    Write-Host "❌ Speech API not enabled" -ForegroundColor Red
}

# Test Translation API
try {
    gcloud services list --enabled --filter="name:translate.googleapis.com" --format="value(name)" | Out-Null
    Write-Host "✅ Translation API enabled" -ForegroundColor Green
} catch {
    Write-Host "❌ Translation API not enabled" -ForegroundColor Red
}

Write-Host "🎉 Development environment setup complete!" -ForegroundColor Green
Write-Host "You can now run: dotnet run --project backend" -ForegroundColor Cyan
```

## 📱 Mobile Platform Configuration

### Android Development:
```bash
# Install Android Studio and SDK
# https://developer.android.com/studio

# Set ANDROID_HOME environment variable
export ANDROID_HOME=$HOME/Android/Sdk
export PATH=$PATH:$ANDROID_HOME/emulator:$ANDROID_HOME/tools:$ANDROID_HOME/platform-tools

# Install required packages
sdkmanager "platforms;android-33" "build-tools;33.0.0" "system-images;android-33;google_apis;x86_64"

# Create AVD for testing
avdmanager create avd -n "VisualVoicemail_Test" -k "system-images;android-33;google_apis;x86_64"
```

### iOS Development (macOS only):
```bash
# Install Xcode from App Store
# https://apps.apple.com/us/app/xcode/id497799835

# Install iOS Simulator
xcode-select --install

# Create iOS development certificate
# Follow Apple Developer documentation for certificate creation
```

## 🏗️ Production Deployment

### Azure App Service Deployment:
```bash
# Create App Service Plan
az appservice plan create --name visualvoicemail-plan --resource-group visualvoicemail-rg --sku P1V2 --is-linux

# Create Web App
az webapp create --resource-group visualvoicemail-rg --plan visualvoicemail-plan --name visualvoicemail-api --runtime "DOTNETCORE|8.0"

# Configure App Settings (use Key Vault references)
az webapp config appsettings set --resource-group visualvoicemail-rg --name visualvoicemail-api --settings \
    "GoogleCloudProjectId=@Microsoft.KeyVault(VaultName=visualvoicemail-keyvault;SecretName=GoogleCloudProjectId)" \
    "StripeSecretKey=@Microsoft.KeyVault(VaultName=visualvoicemail-keyvault;SecretName=StripeSecretKey)" \
    "JwtSecretKey=@Microsoft.KeyVault(VaultName=visualvoicemail-keyvault;SecretName=JwtSecretKey)"

# Deploy application
az webapp deployment source config-zip --resource-group visualvoicemail-rg --name visualvoicemail-api --src ./backend-deployment.zip
```

## 🔒 Security Best Practices

1. **Never commit secrets to version control**
2. **Use Azure Key Vault for production secrets**
3. **Rotate service account keys regularly**
4. **Monitor API usage and set up billing alerts**
5. **Use least privilege principle for IAM roles**
6. **Enable audit logging for all services**
7. **Implement rate limiting and DDoS protection**
8. **Use HTTPS everywhere with TLS 1.2+**

## 📊 Monitoring and Logging

### Application Insights Setup:
```bash
# Create Application Insights
az monitor app-insights component create --app visualvoicemail-insights --location eastus --resource-group visualvoicemail-rg

# Get instrumentation key
az monitor app-insights component show --app visualvoicemail-insights --resource-group visualvoicemail-rg --query "instrumentationKey"
```

### Log Analytics Workspace:
```bash
# Create Log Analytics Workspace
az monitor log-analytics workspace create --resource-group visualvoicemail-rg --workspace-name visualvoicemail-logs --location eastus
```

This comprehensive setup ensures your Visual Voicemail Pro application has secure, scalable, and production-ready configuration management with proper secret handling, monitoring, and deployment capabilities.