# Enhanced Visual Voicemail Pro Backend Setup and Test Script
# Run this script to set up the enhanced coupon and whitelist system

param(
    [switch]$SetupDatabase,
    [switch]$RunTests,
    [switch]$StartServer,
    [switch]$All
)

Write-Host "🚀 Enhanced Visual Voicemail Pro Backend Setup" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Function to check if SQL Server LocalDB is available
function Test-SqlServerLocalDB {
    try {
        $result = sqllocaldb info MSSQLLocalDB 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ SQL Server LocalDB is available" -ForegroundColor Green
            return $true
        }
    }
    catch {
        Write-Host "❌ SQL Server LocalDB not found" -ForegroundColor Red
        return $false
    }
    return $false
}

# Function to set up the database
function Setup-Database {
    Write-Host "`n🗄️ Setting up Enhanced Database..." -ForegroundColor Yellow
    
    if (-not (Test-SqlServerLocalDB)) {
        Write-Host "⚠️ Please install SQL Server LocalDB or update connection string" -ForegroundColor Yellow
        return
    }
    
    try {
        # Check if sqlcmd is available
        $sqlcmdPath = Get-Command sqlcmd -ErrorAction SilentlyContinue
        if (-not $sqlcmdPath) {
            Write-Host "❌ sqlcmd not found. Please install SQL Server Command Line Utilities" -ForegroundColor Red
            return
        }
        
        # Run the migration script
        $migrationScript = ".\Migrations\InitialEnhancedMigration.sql"
        if (Test-Path $migrationScript) {
            Write-Host "📜 Running database migration..." -ForegroundColor Blue
            sqlcmd -S "(localdb)\MSSQLLocalDB" -d "master" -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'VisualVoicemailPro') CREATE DATABASE VisualVoicemailPro"
            sqlcmd -S "(localdb)\MSSQLLocalDB" -d "VisualVoicemailPro" -i $migrationScript
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Database migration completed successfully!" -ForegroundColor Green
            } else {
                Write-Host "❌ Database migration failed" -ForegroundColor Red
            }
        } else {
            Write-Host "❌ Migration script not found: $migrationScript" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ Database setup failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Function to install required packages
function Install-Dependencies {
    Write-Host "`n📦 Installing Enhanced Dependencies..." -ForegroundColor Yellow
    
    # Check if we're in the right directory
    if (-not (Test-Path "VisualVoicemailPro.Backend.csproj")) {
        Write-Host "❌ Not in the backend project directory" -ForegroundColor Red
        return
    }
    
    try {
        Write-Host "🔧 Restoring NuGet packages..." -ForegroundColor Blue
        dotnet restore
        
        Write-Host "🔧 Building project..." -ForegroundColor Blue
        dotnet build
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Dependencies installed and project built successfully!" -ForegroundColor Green
        } else {
            Write-Host "❌ Build failed" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ Dependency installation failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Function to run tests
function Run-Tests {
    Write-Host "`n🧪 Running Enhanced System Tests..." -ForegroundColor Yellow
    
    if (Test-Path "Tests\EnhancedSystemTests.cs") {
        try {
            Write-Host "🔧 Compiling and running tests..." -ForegroundColor Blue
            dotnet run --project Tests
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Tests completed!" -ForegroundColor Green
            } else {
                Write-Host "⚠️ Some tests may have failed (check output above)" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "❌ Test execution failed: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ Test files not found" -ForegroundColor Red
    }
}

# Function to start the server
function Start-Server {
    Write-Host "`n🌐 Starting Enhanced Visual Voicemail Pro API Server..." -ForegroundColor Yellow
    
    try {
        Write-Host "🚀 Launching server with enhanced features..." -ForegroundColor Blue
        Write-Host "📊 Features enabled: Coupons, Whitelist, Trials" -ForegroundColor Cyan
        Write-Host "🔗 Admin endpoints: /api/admin/*" -ForegroundColor Cyan
        Write-Host "👤 User endpoints: /api/user/*" -ForegroundColor Cyan
        Write-Host ""
        
        dotnet run
    }
    catch {
        Write-Host "❌ Server startup failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Function to validate configuration
function Test-Configuration {
    Write-Host "`n🔍 Validating Configuration..." -ForegroundColor Yellow
    
    $issues = @()
    
    # Check required files
    $requiredFiles = @(
        "appsettings.json",
        "appsettings.Enhanced.json",
        "Models\Enhanced.cs",
        "Data\VisualVoicemailDbContext.cs",
        "StripeIntegrationService.cs"
    )
    
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path $file)) {
            $issues += "Missing file: $file"
        }
    }
    
    # Check environment variables
    $envVars = @(
        "STRIPE_SECRET_KEY",
        "JWT_SECRET_KEY"
    )
    
    foreach ($var in $envVars) {
        if (-not [Environment]::GetEnvironmentVariable($var)) {
            $issues += "Missing environment variable: $var"
        }
    }
    
    if ($issues.Count -eq 0) {
        Write-Host "✅ Configuration validation passed!" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Configuration issues found:" -ForegroundColor Yellow
        foreach ($issue in $issues) {
            Write-Host "  - $issue" -ForegroundColor Red
        }
    }
}

# Function to show help
function Show-Help {
    Write-Host @"
🎯 Enhanced Visual Voicemail Pro Backend Setup Script

Usage:
  .\setup-enhanced-backend.ps1 [options]

Options:
  -SetupDatabase    Set up the enhanced database schema
  -RunTests         Run system tests
  -StartServer      Start the API server
  -All              Run all setup steps

Examples:
  .\setup-enhanced-backend.ps1 -All
  .\setup-enhanced-backend.ps1 -SetupDatabase -StartServer
  .\setup-enhanced-backend.ps1 -RunTests

Features Included:
  ✅ Coupon system with validation
  ✅ Developer whitelist functionality  
  ✅ Enhanced Stripe integration
  ✅ JWT authentication
  ✅ Admin and user endpoints
  ✅ Database migrations
  ✅ Comprehensive testing

"@ -ForegroundColor Cyan
}

# Main execution logic
if ($All) {
    Test-Configuration
    Install-Dependencies
    Setup-Database
    Run-Tests
    Start-Server
} elseif ($SetupDatabase) {
    Setup-Database
} elseif ($RunTests) {
    Run-Tests
} elseif ($StartServer) {
    Start-Server
} else {
    Show-Help
}

Write-Host "`n🎉 Enhanced Visual Voicemail Pro Backend Setup Complete!" -ForegroundColor Green
Write-Host "📋 Next steps:" -ForegroundColor Cyan
Write-Host "  1. Update Stripe API keys in appsettings.json" -ForegroundColor White
Write-Host "  2. Configure JWT secret key" -ForegroundColor White
Write-Host "  3. Test admin endpoints with proper authentication" -ForegroundColor White
Write-Host "  4. Integrate with your mobile app frontend" -ForegroundColor White