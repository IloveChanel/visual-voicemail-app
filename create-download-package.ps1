# Visual Voicemail Pro - Complete Package Creator for Download
# Creates comprehensive zip with all source code and documentation

Write-Host "🚀 Creating Visual Voicemail Pro Complete Package for Download..." -ForegroundColor Green

# Get current timestamp
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
$zipName = "VisualVoicemailPro-Complete-$timestamp.zip"

Write-Host "📦 Package name: $zipName" -ForegroundColor Cyan

# Create temporary staging directory
$stagingDir = ".\temp_staging_$timestamp"
if (Test-Path $stagingDir) {
    Remove-Item -Path $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir | Out-Null

Write-Host "📁 Copying project files..." -ForegroundColor Yellow

# Copy all essential directories and files
$itemsToCopy = @(
    "backend",
    "mobile-app", 
    "android-app",
    "ios-app",
    "mobile",
    ".github",
    "*.md",
    "*.json", 
    "*.ps1",
    "*.js",
    "*.sln"
)

foreach ($item in $itemsToCopy) {
    $sourcePath = Join-Path (Get-Location) $item
    if ($item.Contains("*")) {
        # Handle wildcard patterns
        $files = Get-ChildItem -Path $item -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            if ($file.Name -ne $zipName -and $file.Name -notlike "*temp*") {
                Copy-Item -Path $file.FullName -Destination $stagingDir -Force
                Write-Host "  ✅ $($file.Name)" -ForegroundColor Gray
            }
        }
    } else {
        # Handle directories and specific files
        if (Test-Path $sourcePath) {
            $destPath = Join-Path $stagingDir (Split-Path $item -Leaf)
            Copy-Item -Path $sourcePath -Destination $destPath -Recurse -Force
            Write-Host "  ✅ $item" -ForegroundColor Gray
        }
    }
}

Write-Host "📝 Creating package documentation..." -ForegroundColor Yellow

# Create package info file with proper escaping
$packageInfoContent = @"
# Visual Voicemail Pro - Complete Source Package

**Created**: $(Get-Date -Format "MMMM dd, yyyy 'at' HH:mm")
**Package**: $zipName

## 🚀 What's Included

### 💻 Enhanced Backend (.NET 8 + Entity Framework)
* Complete coupon and subscription system
* Developer whitelist functionality  
* Multilingual translation (Google Cloud + DeepL + Microsoft)
* JWT authentication with role-based access
* Stripe integration with advanced billing
* Entity Framework database with migrations

### 📱 Mobile Applications
* **MAUI Cross-Platform App** (/mobile-app/) - Windows/Android/iOS
* **Native Android App** (/android-app/) - Kotlin + Jetpack Compose
* **Native iOS App** (/ios-app/) - Swift + SwiftUI
* **React Native App** (/mobile/) - Cross-platform alternative

### 🔧 Key Features Implemented
* ✅ Coupon codes with validation and usage tracking
* ✅ Developer whitelist for free testing access
* ✅ Multilingual translation with provider failover
* ✅ Subscription management with Stripe
* ✅ JWT security and role-based authorization
* ✅ Complete database schema with seeded data
* ✅ AdMob integration for free tier monetization

### 📋 Documentation & Setup
* **ANDROID_STUDIO_SETUP.md** - Complete Android development guide
* **SETUP_GUIDE.md** - Full project setup instructions
* **AZURE_DEPLOYMENT_GUIDE.md** - Cloud deployment instructions
* **GOOGLE_CLOUD_SETUP.md** - API configuration guide
* **BUILD_PERSONAL_APK.md** - Personal APK building
* **COMPLETE_CHECKLIST.md** - Production launch checklist

### 🎯 Business Model Ready
* **Freemium**: Free tier with ads, premium subscriptions
* **Pricing**: 3.49/month Pro, 9.99/month Business
* **Revenue Streams**: Subscriptions + AdMob + Enterprise licensing
* **Market Ready**: App store submission preparation included

## 🚀 Next Steps After Download
1. Extract the zip file
2. Review ANDROID_STUDIO_SETUP.md for immediate Android development
3. Follow SETUP_GUIDE.md for complete environment setup
4. Configure Firebase and Stripe API keys
5. Build and test on your preferred platform

## 💡 Technical Highlights
* **Backend**: ASP.NET Core 8.0 with Entity Framework Core
* **Mobile**: Kotlin + Jetpack Compose for Android
* **Database**: SQL Server with comprehensive migrations
* **APIs**: REST APIs with JWT authentication
* **Translation**: Google Cloud Translation API + DeepL + Microsoft
* **Payments**: Stripe with coupon and subscription support
* **Security**: Role-based access, encrypted data, secure endpoints

**🎉 This package contains everything needed for a complete Visual Voicemail Pro deployment!**
"@

$packageInfoContent | Out-File -FilePath "$stagingDir\PACKAGE_INFO.md" -Encoding UTF8

Write-Host "📦 Creating zip archive..." -ForegroundColor Yellow

# Create the zip file
try {
    Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipName -Force
    
    # Get file size
    $zipSize = (Get-Item $zipName).Length / 1MB
    
    Write-Host ""
    Write-Host "✅ SUCCESS! Package created successfully!" -ForegroundColor Green
    Write-Host "📦 File: $zipName" -ForegroundColor Cyan
    Write-Host "📊 Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Cyan
    Write-Host "📍 Location: $(Resolve-Path $zipName)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "🚀 Your complete Visual Voicemail Pro source code package is ready!" -ForegroundColor Green
    Write-Host "   This includes all backend APIs, mobile apps, documentation, and setup scripts." -ForegroundColor Gray
    Write-Host ""
    
    # Clean up staging directory
    Remove-Item -Path $stagingDir -Recurse -Force
    
} catch {
    Write-Host "❌ Error creating zip file: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "🎯 Package Contents Summary:" -ForegroundColor Magenta
Write-Host "   • Enhanced Backend with coupon/translation system" -ForegroundColor Gray
Write-Host "   • Android Studio ready project with Kotlin + Compose" -ForegroundColor Gray  
Write-Host "   • Complete documentation and setup guides" -ForegroundColor Gray
Write-Host "   • All configuration files and scripts" -ForegroundColor Gray
Write-Host "   • Business model implementation (subscriptions + ads)" -ForegroundColor Gray
Write-Host ""
Write-Host "Ready for download and development on any platform! 🚀" -ForegroundColor Green