# Visual Voicemail Pro - Simple Package Creator
# Creates a zip file with the complete enhanced application

Write-Host "Visual Voicemail Pro - Complete Package Creator" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

$timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
$packageName = "VisualVoicemailPro-Enhanced-$timestamp"
$zipFile = "$packageName.zip"

Write-Host "Creating package: $packageName" -ForegroundColor Yellow

# Remove existing zip if present
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
    Write-Host "Removed existing zip file" -ForegroundColor Yellow
}

# Create zip file with all project contents
Write-Host "Compressing files..." -ForegroundColor Yellow

$itemsToInclude = @(
    "backend",
    "mobile-app", 
    "android-app",
    "ios-app",
    "mobile",
    ".github",
    "*.md",
    "*.json",
    "*.ps1",
    "*.js"
)

$filesToCompress = @()

foreach ($item in $itemsToInclude) {
    $found = Get-Item $item -ErrorAction SilentlyContinue
    if ($found) {
        $filesToCompress += $found
        Write-Host "  + Including: $item" -ForegroundColor Green
    }
}

try {
    Compress-Archive -Path $filesToCompress -DestinationPath $zipFile -CompressionLevel Optimal -Force
    
    $zipSize = (Get-Item $zipFile).Length / 1MB
    $fullPath = (Resolve-Path $zipFile).Path
    
    Write-Host ""
    Write-Host "SUCCESS! Package created:" -ForegroundColor Green
    Write-Host "File: $zipFile" -ForegroundColor Cyan
    Write-Host "Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Cyan
    Write-Host "Path: $fullPath" -ForegroundColor Cyan
    
    Write-Host ""
    Write-Host "Package Contents:" -ForegroundColor Yellow
    Write-Host "- Enhanced Backend API with multilingual translation" -ForegroundColor White
    Write-Host "- MAUI Mobile App with coupon system" -ForegroundColor White
    Write-Host "- Android and iOS native components" -ForegroundColor White
    Write-Host "- Complete documentation and setup guides" -ForegroundColor White
    Write-Host "- Database migrations and configuration" -ForegroundColor White
    Write-Host "- Testing and deployment scripts" -ForegroundColor White
    
    Write-Host ""
    Write-Host "Ready for deployment!" -ForegroundColor Green
    
} catch {
    Write-Host "ERROR: Failed to create package - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}