# Simple Visual Voicemail Pro Package Creator
Write-Host "Creating Visual Voicemail Pro Complete Package..." -ForegroundColor Green

$timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
$zipName = "VisualVoicemailPro-Complete-$timestamp.zip"

Write-Host "Package name: $zipName" -ForegroundColor Cyan

# Remove old zip if exists
if (Test-Path $zipName) {
    Remove-Item $zipName -Force
}

# Create temp directory
$stagingDir = "temp_staging"
if (Test-Path $stagingDir) {
    Remove-Item -Path $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir | Out-Null

Write-Host "Copying files..." -ForegroundColor Yellow

# Copy directories
$directories = @("backend", "mobile-app", "android-app", "ios-app", "mobile", ".github")
foreach ($dir in $directories) {
    if (Test-Path $dir) {
        Copy-Item -Path $dir -Destination $stagingDir -Recurse -Force
        Write-Host "  Copied $dir" -ForegroundColor Gray
    }
}

# Copy files
$filePatterns = @("*.md", "*.json", "*.ps1", "*.js", "*.sln")
foreach ($pattern in $filePatterns) {
    $files = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        if ($file.Name -ne $zipName -and $file.Name -notlike "temp*") {
            Copy-Item -Path $file.FullName -Destination $stagingDir -Force
            Write-Host "  Copied $($file.Name)" -ForegroundColor Gray
        }
    }
}

# Create package info
$packageInfo = "# Visual Voicemail Pro - Complete Package`n`n"
$packageInfo += "Created: $(Get-Date)`n"
$packageInfo += "Package: $zipName`n`n"
$packageInfo += "## Contents:`n"
$packageInfo += "- Enhanced Backend (.NET 8 + Entity Framework)`n"
$packageInfo += "- Mobile Applications (MAUI, Android, iOS, React Native)`n"
$packageInfo += "- Complete documentation and setup guides`n"
$packageInfo += "- Coupon system with Stripe integration`n"
$packageInfo += "- Multilingual translation system`n"
$packageInfo += "- Developer whitelist functionality`n"
$packageInfo += "- Production deployment ready`n"

$packageInfo | Out-File -FilePath "$stagingDir\PACKAGE_INFO.md" -Encoding UTF8

Write-Host "Creating zip archive..." -ForegroundColor Yellow

# Create zip
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipName -Force

# Get size and show results
$zipSize = (Get-Item $zipName).Length / 1MB
$fullPath = (Resolve-Path $zipName).Path

Write-Host ""
Write-Host "SUCCESS! Package created:" -ForegroundColor Green
Write-Host "File: $zipName" -ForegroundColor Cyan
Write-Host "Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Cyan
Write-Host "Location: $fullPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "Your complete Visual Voicemail Pro source code is ready for download!" -ForegroundColor Green

# Cleanup
Remove-Item -Path $stagingDir -Recurse -Force

Write-Host "Package includes all backend APIs, mobile apps, and documentation!" -ForegroundColor Magenta