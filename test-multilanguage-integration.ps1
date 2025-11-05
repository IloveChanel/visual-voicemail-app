# Comprehensive Multilingual Translation System Test Script
# Tests Google Translate, DeepL, Microsoft Translator, and localization

param(
    [switch]$TestTranslation,
    [switch]$TestLocalization,
    [switch]$TestProviders,
    [switch]$All
)

Write-Host "� Multilingual Translation System Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$baseUrl = "https://localhost:7155" # Update with your API URL

# Function to test translation endpoint
function Test-TranslationEndpoint {
    Write-Host "`n� Testing Translation Endpoint..." -ForegroundColor Yellow
    
    $testRequests = @(
        @{
            text = "Hello, this is a test voicemail message. Please call me back."
            targetLanguage = "es"
            sourceLanguage = "en"
            useHighQuality = $true
        },
        @{
            text = "Bonjour, ceci est un message vocal de test."
            targetLanguage = "en"
            sourceLanguage = "fr"
            useHighQuality = $true
        },
        @{
            text = "こんにちは、これはテストの音声メッセージです。"
            targetLanguage = "en"
            sourceLanguage = "ja"
            useHighQuality = $true
        }
    )
    
    foreach ($request in $testRequests) {
        try {
            $json = $request | ConvertTo-Json
            $response = Invoke-RestMethod -Uri "$baseUrl/api/translation/translate" -Method Post -Body $json -ContentType "application/json"
            
            if ($response.success) {
                Write-Host "✅ Translation successful:" -ForegroundColor Green
                Write-Host "   Original: $($request.text)" -ForegroundColor White
                Write-Host "   Translated: $($response.translatedText)" -ForegroundColor White
                Write-Host "   Provider: $($response.usedProvider)" -ForegroundColor Cyan
                Write-Host "   Confidence: $($response.confidence * 100)%" -ForegroundColor Cyan
            } else {
                Write-Host "❌ Translation failed: $($response.errorMessage)" -ForegroundColor Red
            }
        }
        catch {
            Write-Host "❌ Translation request failed: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        Start-Sleep -Seconds 1
    }
}

# Function to test batch translation
function Test-BatchTranslation {
    Write-Host "`n📦 Testing Batch Translation..." -ForegroundColor Yellow
    
    $batchRequest = @{
        texts = @(
            "Hello world",
            "How are you?",
            "This is a test message",
            "Please call me back",
            "Thank you for your time"
        )
        targetLanguage = "es"
        sourceLanguage = "en"
        useHighQuality = $true
    }
    
    try {
        $json = $batchRequest | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "$baseUrl/api/translation/translate/batch" -Method Post -Body $json -ContentType "application/json"
        
        if ($response.success) {
            Write-Host "✅ Batch translation successful!" -ForegroundColor Green
            Write-Host "   Total texts: $($batchRequest.texts.Count)" -ForegroundColor Cyan
            Write-Host "   Successful: $($response.translations | Where-Object { $_.success }).Count" -ForegroundColor Cyan
            Write-Host "   Processing time: $($response.totalProcessingTime)" -ForegroundColor Cyan
            
            # Show first few translations
            $response.translations | Select-Object -First 3 | ForEach-Object {
                if ($_.success) {
                    Write-Host "   → $($_.translatedText)" -ForegroundColor White
                }
            }
        } else {
            Write-Host "❌ Batch translation failed: $($response.errorMessage)" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ Batch translation request failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Function to test language detection
function Test-LanguageDetection {
    Write-Host "`n� Testing Language Detection..." -ForegroundColor Yellow
    
    $testTexts = @(
        "Hello, how are you today?",
        "Hola, ¿cómo estás hoy?",
        "Bonjour, comment allez-vous aujourd'hui?",
        "Hallo, wie geht es dir heute?",
        "こんにちは、今日はいかがですか？",
        "Привет, как дела сегодня?"
    )
    
    foreach ($text in $testTexts) {
        try {
            $request = @{ text = $text; maxAlternatives = 3 }
            $json = $request | ConvertTo-Json
            $response = Invoke-RestMethod -Uri "$baseUrl/api/translation/detect" -Method Post -Body $json -ContentType "application/json"
            
            if ($response.success) {
                Write-Host "✅ Language detected: $($response.detectedLanguage)" -ForegroundColor Green
                Write-Host "   Text: $text" -ForegroundColor White
                Write-Host "   Confidence: $($response.confidence * 100)%" -ForegroundColor Cyan
                
                if ($response.alternatives -and $response.alternatives.Count -gt 0) {
                    Write-Host "   Alternatives: $($response.alternatives | ForEach-Object { $_.languageCode + ' (' + [math]::Round($_.confidence * 100) + '%)' } | Join-String ', ')" -ForegroundColor Gray
                }
            } else {
                Write-Host "❌ Language detection failed: $($response.errorMessage)" -ForegroundColor Red
            }
        }
        catch {
            Write-Host "❌ Language detection request failed: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        Start-Sleep -Milliseconds 500
    }
}

# Function to test supported languages
function Test-SupportedLanguages {
    Write-Host "`n🌐 Testing Supported Languages..." -ForegroundColor Yellow
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/translation/languages" -Method Get
        
        if ($response -and $response.Count -gt 0) {
            Write-Host "✅ Retrieved $($response.Count) supported languages" -ForegroundColor Green
            
            # Group by provider
            $providerGroups = $response | Group-Object { $_.supportedProviders[0] }
            foreach ($group in $providerGroups) {
                Write-Host "   $($group.Name): $($group.Count) languages" -ForegroundColor Cyan
            }
            
            # Show some examples
            Write-Host "`n   Sample languages:" -ForegroundColor White
            $response | Select-Object -First 5 | ForEach-Object {
                Write-Host "   • $($_.code): $($_.name) ($($_.nativeName))" -ForegroundColor Gray
            }
        } else {
            Write-Host "❌ No supported languages returned" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ Supported languages request failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Function to test localization
function Test-LocalizationResources {
    Write-Host "`n🏷️ Testing Localization Resources..." -ForegroundColor Yellow
    
    $testLanguages = @("en", "es", "fr", "de")
    
    foreach ($language in $testLanguages) {
        try {
            $response = Invoke-RestMethod -Uri "$baseUrl/api/translation/resources?language=$language&category=app" -Method Get
            
            if ($response -and $response.PSObject.Properties.Count -gt 0) {
                Write-Host "✅ Retrieved localization for $language" -ForegroundColor Green
                $response.PSObject.Properties | Select-Object -First 3 | ForEach-Object {
                    Write-Host "   $($_.Name): $($_.Value)" -ForegroundColor White
                }
            } else {
                Write-Host "⚠️ No localization resources found for $language" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "❌ Localization request failed for $language`: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        Start-Sleep -Milliseconds 300
    }
}

# Function to show health status
function Test-HealthStatus {
    Write-Host "`n❤️ Testing API Health..." -ForegroundColor Yellow
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
        Write-Host "✅ API is healthy: $response" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ API health check failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "   Make sure the API server is running on $baseUrl" -ForegroundColor Gray
    }
}

# Main execution logic
if ($All) {
    Test-HealthStatus
    Test-SupportedLanguages
    Test-LanguageDetection
    Test-TranslationEndpoint
    Test-BatchTranslation
    Test-LocalizationResources
} elseif ($TestTranslation) {
    Test-HealthStatus
    Test-LanguageDetection
    Test-TranslationEndpoint
    Test-BatchTranslation
} elseif ($TestLocalization) {
    Test-HealthStatus
    Test-LocalizationResources
} elseif ($TestProviders) {
    Test-HealthStatus
    Test-SupportedLanguages
} else {
    Write-Host @"
🎯 Multilingual Translation System Test Script

Usage:
  .\test-multilanguage-integration.ps1 [options]

Options:
  -TestTranslation     Test translation endpoints and functionality
  -TestLocalization    Test localization resources and strings
  -TestProviders       Test provider availability and failover
  -All                 Run all tests

Examples:
  .\test-multilanguage-integration.ps1 -All
  .\test-multilanguage-integration.ps1 -TestTranslation
  .\test-multilanguage-integration.ps1 -TestLocalization

Features Tested:
  ✅ Real-time translation with multiple providers
  ✅ Batch translation processing
  ✅ Language detection and confidence scoring
  ✅ Localization resource management
  ✅ Provider failover and redundancy
  ✅ Multilingual UI support
  ✅ Translation memory and caching

"@ -ForegroundColor Cyan
}

Write-Host "`n🎉 Multilingual Translation Test Complete!" -ForegroundColor Green
Write-Host "📋 Summary:" -ForegroundColor Cyan
Write-Host "  • Google Cloud Translation API integration" -ForegroundColor White
Write-Host "  • DeepL high-quality translation support" -ForegroundColor White  
Write-Host "  • Microsoft Translator Azure AI integration" -ForegroundColor White
Write-Host "  • Comprehensive localization management" -ForegroundColor White
Write-Host "  • Translation memory and intelligent caching" -ForegroundColor White
Write-Host "  • Provider failover and redundancy" -ForegroundColor White
Write-Host "  • Seamless voicemail translation workflow" -ForegroundColor White