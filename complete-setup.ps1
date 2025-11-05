#!/usr/bin/env pwsh

# Visual Voicemail Pro - Complete Installation and Setup Script
# Installs all packages, configures environment, and verifies setup

param(
    [switch]$SkipPackages,
    [switch]$SkipTests,
    [switch]$Production,
    [string]$Environment = "Development"
)

Write-Host "🎙️ Visual Voicemail Pro - Complete Setup Script" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

$ErrorActionPreference = "Stop"
$startTime = Get-Date

# Helper function to run commands with error handling
function Invoke-SafeCommand {
    param([string]$Command, [string]$Description)
    
    Write-Host "`n🔄 $Description..." -ForegroundColor Yellow
    try {
        Invoke-Expression $Command
        Write-Host "✅ $Description completed successfully" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "❌ $Description failed: $_" -ForegroundColor Red
        return $false
    }
}

# Step 1: Verify Prerequisites
Write-Host "`n📋 Checking Prerequisites..." -ForegroundColor Cyan

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET SDK not found. Please install .NET 8 SDK" -ForegroundColor Red
    Write-Host "https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Check PowerShell version
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -lt 5) {
    Write-Host "❌ PowerShell 5.0 or higher required. Current: $psVersion" -ForegroundColor Red
    exit 1
}
Write-Host "✅ PowerShell: $psVersion" -ForegroundColor Green

# Step 2: Install Backend Packages
if (-not $SkipPackages) {
    Write-Host "`n📦 Installing Backend Packages..." -ForegroundColor Cyan
    Set-Location "backend"
    
    $backendPackages = @(
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Configuration.AzureKeyVault",
        "Microsoft.Extensions.Configuration.EnvironmentVariables",
        "Microsoft.Extensions.Configuration.UserSecrets",
        "Polly",
        "Polly.Extensions.Http", 
        "Polly.Contrib.WaitAndRetry",
        "xunit",
        "xunit.runner.visualstudio",
        "Microsoft.NET.Test.Sdk",
        "Microsoft.AspNetCore.Mvc.Testing",
        "Moq",
        "FluentAssertions",
        "Microsoft.Extensions.Diagnostics.HealthChecks",
        "Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore",
        "Microsoft.ApplicationInsights.AspNetCore",
        "Serilog.Sinks.ApplicationInsights"
    )
    
    foreach ($package in $backendPackages) {
        $success = Invoke-SafeCommand "dotnet add package $package" "Adding $package to backend"
        if (-not $success) {
            Write-Host "⚠️ Failed to install $package, continuing..." -ForegroundColor Yellow
        }
    }
    
    Set-Location ".."
}

# Step 3: Install Mobile App Packages
if (-not $SkipPackages) {
    Write-Host "`n📱 Installing Mobile App Packages..." -ForegroundColor Cyan
    Set-Location "mobile-app"
    
    $mobilePackages = @(
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Configuration.Json",
        "Microsoft.Extensions.Configuration.EnvironmentVariables",
        "Polly",
        "Polly.Extensions.Http",
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.Logging.Console",
        "Serilog.Extensions.Logging",
        "Plugin.MediaManager",
        "Plugin.MediaManager.Forms",
        "xunit",
        "xunit.runner.visualstudio",
        "Microsoft.NET.Test.Sdk",
        "Moq",
        "Microsoft.AspNetCore.DataProtection",
        "System.Security.Cryptography.Algorithms"
    )
    
    foreach ($package in $mobilePackages) {
        $success = Invoke-SafeCommand "dotnet add package $package" "Adding $package to mobile app"
        if (-not $success) {
            Write-Host "⚠️ Failed to install $package, continuing..." -ForegroundColor Yellow
        }
    }
    
    Set-Location ".."
}

# Step 4: Restore and Build Projects
Write-Host "`n🔨 Building Projects..." -ForegroundColor Cyan

# Build backend
$success = Invoke-SafeCommand "dotnet restore backend" "Restoring backend packages"
if ($success) {
    $success = Invoke-SafeCommand "dotnet build backend --configuration Release" "Building backend project"
}

# Build mobile app
$success = Invoke-SafeCommand "dotnet restore mobile-app" "Restoring mobile app packages"
if ($success) {
    $success = Invoke-SafeCommand "dotnet build mobile-app --configuration Release" "Building mobile app project"
}

# Step 5: Run Tests
if (-not $SkipTests) {
    Write-Host "`n🧪 Running Unit Tests..." -ForegroundColor Cyan
    
    # Backend tests
    if (Test-Path "backend\Tests") {
        Invoke-SafeCommand "dotnet test backend\Tests --configuration Release --verbosity normal" "Running backend tests"
    } else {
        Write-Host "⚠️ Backend tests directory not found, skipping..." -ForegroundColor Yellow
    }
    
    # Mobile app tests
    if (Test-Path "mobile-app\Tests") {
        Invoke-SafeCommand "dotnet test mobile-app\Tests --configuration Release --verbosity normal" "Running mobile app tests"
    } else {
        Write-Host "⚠️ Mobile app tests directory not found, skipping..." -ForegroundColor Yellow
    }
}

# Step 6: Environment Configuration
Write-Host "`n🔧 Configuring Environment ($Environment)..." -ForegroundColor Cyan

if ($Environment -eq "Development") {
    # Check for development configuration files
    $configFiles = @(
        "backend\appsettings.Development.json",
        "backend\appsettings.json"
    )
    
    foreach ($configFile in $configFiles) {
        if (Test-Path $configFile) {
            Write-Host "✅ Found configuration: $configFile" -ForegroundColor Green
        } else {
            Write-Host "⚠️ Missing configuration: $configFile" -ForegroundColor Yellow
        }
    }
    
    # Create example .env file if it doesn't exist
    if (-not (Test-Path ".env.example")) {
        Write-Host "📝 Creating example environment file..." -ForegroundColor Cyan
        
        $envContent = @"
# Visual Voicemail Pro - Environment Configuration
# Copy this to .env and fill in your actual values

# Google Cloud Configuration
GOOGLE_APPLICATION_CREDENTIALS=./google-cloud-key.json
GOOGLE_CLOUD_PROJECT_ID=your-google-cloud-project-id
GOOGLE_STORAGE_BUCKET=visualvoicemail-audio-files

# Stripe Configuration
STRIPE_PUBLISHABLE_KEY=pk_test_your_publishable_key_here
STRIPE_SECRET_KEY=sk_test_your_secret_key_here
STRIPE_WEBHOOK_SECRET=whsec_your_webhook_secret_here
STRIPE_PRO_PRICE_ID=price_your_pro_price_id_here
STRIPE_BUSINESS_PRICE_ID=price_your_business_price_id_here

# Database Configuration
SQL_CONNECTION_STRING=Server=(localdb)\mssqllocaldb;Database=VisualVoicemailPro;Trusted_Connection=true
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
"@
        
        $envContent | Out-File -FilePath ".env.example" -Encoding UTF8
        Write-Host "✅ Created .env.example file" -ForegroundColor Green
    }
}

# Step 7: Verify Google Cloud Setup
Write-Host "`n☁️ Verifying Google Cloud Setup..." -ForegroundColor Cyan

if (Get-Command "gcloud" -ErrorAction SilentlyContinue) {
    try {
        $projectId = gcloud config get-value project 2>$null
        if ($projectId) {
            Write-Host "✅ Google Cloud CLI configured. Project: $projectId" -ForegroundColor Green
            
            # Check enabled APIs
            $enabledApis = gcloud services list --enabled --format="value(name)" 2>$null
            
            if ($enabledApis -match "speech.googleapis.com") {
                Write-Host "✅ Speech API enabled" -ForegroundColor Green
            } else {
                Write-Host "⚠️ Speech API not enabled. Run: gcloud services enable speech.googleapis.com" -ForegroundColor Yellow
            }
            
            if ($enabledApis -match "translate.googleapis.com") {
                Write-Host "✅ Translation API enabled" -ForegroundColor Green
            } else {
                Write-Host "⚠️ Translation API not enabled. Run: gcloud services enable translate.googleapis.com" -ForegroundColor Yellow
            }
        } else {
            Write-Host "⚠️ Google Cloud CLI not configured. Run: gcloud auth login" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠️ Error checking Google Cloud configuration: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️ Google Cloud CLI not found. Install from: https://cloud.google.com/sdk/docs/install" -ForegroundColor Yellow
}

# Step 8: Final Verification
Write-Host "`n🔍 Final Verification..." -ForegroundColor Cyan

# Check project structure
$requiredFiles = @(
    "backend\VisualVoicemailPro.Backend.csproj",
    "mobile-app\VisualVoicemailPro.csproj",
    "backend\Services\SecureConfigurationService.cs",
    "backend\Services\Enhanced.cs",
    "backend\ViewModels\EnhancedMainViewModel.cs",
    "mobile-app\Views\MainPage.xaml",
    "mobile-app\Converters\UIConverters.cs"
)

$missingFiles = @()
foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Host "✅ $file" -ForegroundColor Green
    } else {
        Write-Host "❌ $file" -ForegroundColor Red
        $missingFiles += $file
    }
}

# Summary Report
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host "`n📊 Setup Summary Report" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host "⏱️ Total setup time: $($duration.TotalMinutes.ToString('F1')) minutes" -ForegroundColor Cyan
Write-Host "📦 Packages: $(if ($SkipPackages) { 'Skipped' } else { 'Installed' })" -ForegroundColor Cyan
Write-Host "🧪 Tests: $(if ($SkipTests) { 'Skipped' } else { 'Executed' })" -ForegroundColor Cyan
Write-Host "🌍 Environment: $Environment" -ForegroundColor Cyan

if ($missingFiles.Count -eq 0) {
    Write-Host "✅ All required files present" -ForegroundColor Green
} else {
    Write-Host "❌ Missing files: $($missingFiles.Count)" -ForegroundColor Red
    foreach ($file in $missingFiles) {
        Write-Host "   • $file" -ForegroundColor Red
    }
}

# Next Steps
Write-Host "`n🚀 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Copy .env.example to .env and fill in your API keys" -ForegroundColor Cyan
Write-Host "2. Set up Google Cloud service account and download key" -ForegroundColor Cyan  
Write-Host "3. Configure Stripe webhooks for subscription management" -ForegroundColor Cyan
Write-Host "4. Run: dotnet run --project backend (to start API server)" -ForegroundColor Cyan
Write-Host "5. Run: dotnet build mobile-app -t:Run -p:TargetFramework=net8.0-android (for Android)" -ForegroundColor Cyan

# Helpful Commands
Write-Host "`n💡 Helpful Commands:" -ForegroundColor Yellow
Write-Host "• Test multilanguage integration: .\test-multilanguage-integration.ps1" -ForegroundColor Cyan
Write-Host "• Run backend API: dotnet run --project backend" -ForegroundColor Cyan
Write-Host "• Build Android APK: dotnet publish mobile-app -f net8.0-android -c Release" -ForegroundColor Cyan
Write-Host "• View logs: Get-Content backend\logs\*.log -Tail 50" -ForegroundColor Cyan

Write-Host "`n🎉 Visual Voicemail Pro setup complete!" -ForegroundColor Green
Write-Host "Visit the GOOGLE_CLOUD_SETUP.md file for detailed configuration steps." -ForegroundColor Cyan