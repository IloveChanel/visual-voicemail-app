# Visual Voicemail Pro - Package Installation Script
# This script helps install all required packages for the Visual Voicemail Pro project

Write-Host "🎙️ Visual Voicemail Pro - Package Installation" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

# Function to check if a command exists
function Test-CommandExists {
    param($Command)
    try {
        Get-Command $Command -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

# Check for .NET SDK
Write-Host "`n📦 Checking for .NET SDK..." -ForegroundColor Yellow

if (Test-CommandExists "dotnet") {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK found: $dotnetVersion" -ForegroundColor Green
    
    Write-Host "`n📱 Installing packages for Visual Voicemail Pro..." -ForegroundColor Yellow
    
    # Navigate to mobile app directory
    Set-Location "C:\Users\selli\OneDrive\visial voicemail app\mobile-app"
    
    # Core MAUI packages
    Write-Host "Installing Microsoft.Maui.Controls..." -ForegroundColor Cyan
    dotnet add package Microsoft.Maui.Controls --version 8.0.90
    
    Write-Host "Installing Microsoft.Maui.Essentials..." -ForegroundColor Cyan  
    dotnet add package Microsoft.Maui.Essentials --version 8.0.90
    
    # Google Cloud packages for AI features
    Write-Host "Installing Google.Cloud.Speech.V1..." -ForegroundColor Cyan
    dotnet add package Google.Cloud.Speech.V1 --version 3.6.0
    
    Write-Host "Installing Google.Cloud.Translate.V3..." -ForegroundColor Cyan
    dotnet add package Google.Cloud.Translate.V3 --version 3.4.0
    
    # Stripe for payments
    Write-Host "Installing Stripe.net..." -ForegroundColor Cyan
    dotnet add package Stripe.net --version 44.13.0
    
    # JSON processing
    Write-Host "Installing Newtonsoft.Json..." -ForegroundColor Cyan
    dotnet add package Newtonsoft.Json --version 13.0.3
    
    # In-app billing
    Write-Host "Installing Plugin.InAppBilling..." -ForegroundColor Cyan
    dotnet add package Plugin.InAppBilling --version 7.1.1
    
    # Community Toolkit
    Write-Host "Installing CommunityToolkit.Mvvm..." -ForegroundColor Cyan
    dotnet add package CommunityToolkit.Mvvm --version 8.2.2
    
    Write-Host "Installing CommunityToolkit.Maui..." -ForegroundColor Cyan
    dotnet add package CommunityToolkit.Maui --version 9.0.3
    
    # Audio processing
    Write-Host "Installing Plugin.AudioRecorder..." -ForegroundColor Cyan
    dotnet add package Plugin.AudioRecorder --version 1.1.0
    
    # Restore packages
    Write-Host "`n🔄 Restoring packages..." -ForegroundColor Yellow
    dotnet restore
    
    Write-Host "`n✅ Package installation completed!" -ForegroundColor Green
    Write-Host "📱 Your Visual Voicemail Pro mobile app is ready for development" -ForegroundColor Green
    
} else {
    Write-Host "❌ .NET SDK not found!" -ForegroundColor Red
    Write-Host "`n📥 To install .NET SDK:" -ForegroundColor Yellow
    Write-Host "1. Visit: https://dotnet.microsoft.com/download" -ForegroundColor White
    Write-Host "2. Download .NET 8.0 SDK" -ForegroundColor White
    Write-Host "3. Run the installer" -ForegroundColor White
    Write-Host "4. Restart PowerShell and run this script again" -ForegroundColor White
    
    Write-Host "`n💡 Alternative: Use Visual Studio 2022 with .NET MAUI workload" -ForegroundColor Cyan
}

# Check for Visual Studio
Write-Host "`n🛠️ Checking for development environment..." -ForegroundColor Yellow

$vsPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\Common7\IDE\devenv.exe"
if (Test-Path $vsPath) {
    Write-Host "✅ Visual Studio 2022 found" -ForegroundColor Green
    Write-Host "💡 Open the solution file in Visual Studio to continue development" -ForegroundColor Cyan
} else {
    Write-Host "⚠️ Visual Studio 2022 not found" -ForegroundColor Yellow
    Write-Host "📥 Download from: https://visualstudio.microsoft.com/downloads/" -ForegroundColor White
    Write-Host "🎯 Make sure to install the .NET Multi-platform App UI workload" -ForegroundColor White
}

Write-Host "`n📋 Project Structure Created:" -ForegroundColor Yellow
Write-Host "- Backend API: Enhanced C# services with Stripe integration" -ForegroundColor White
Write-Host "- Mobile App: MAUI project for Android and iOS" -ForegroundColor White
Write-Host "- Package Configuration: All required NuGet packages defined" -ForegroundColor White

Write-Host "`n🚀 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Install .NET 8.0 SDK if not already installed" -ForegroundColor White
Write-Host "2. Open Visual Studio 2022 with .NET MAUI workload" -ForegroundColor White
Write-Host "3. Open the project and restore packages" -ForegroundColor White
Write-Host "4. Configure Google Cloud and Stripe API keys" -ForegroundColor White
Write-Host "5. Build and test your Visual Voicemail Pro app!" -ForegroundColor White

Write-Host "`n💰 Business Model Ready:" -ForegroundColor Green
Write-Host "- Free tier: 5 voicemails/month" -ForegroundColor White
Write-Host "- Pro tier: `$3.49/month with advanced features" -ForegroundColor White
Write-Host "- Break-even: 9 subscribers" -ForegroundColor White

Read-Host "`nPress Enter to continue..."