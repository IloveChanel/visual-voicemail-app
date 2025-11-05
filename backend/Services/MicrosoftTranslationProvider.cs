using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using VisualVoicemailPro.Models;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// Microsoft Translator API provider
    /// Part of Azure AI services with real-time and batch translation
    /// </summary>
    public interface IMicrosoftTranslationProvider
    {
        Task<TranslationResponse> TranslateAsync(TranslationRequest request);
        Task<LanguageDetectionResponse> DetectLanguageAsync(LanguageDetectionRequest request);
        Task<List<SupportedLanguage>> GetSupportedLanguagesAsync();
    }

    public class MicrosoftTranslationProvider : IMicrosoftTranslationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MicrosoftTranslationProvider> _logger;
        private readonly TranslationProviderConfig _config;
        private const string BaseUrl = "https://api.cognitive.microsofttranslator.com";

        public MicrosoftTranslationProvider(
            HttpClient httpClient,
            ILogger<MicrosoftTranslationProvider> logger,
            IOptions<TranslationConfiguration> config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config.Value.Providers[TranslationProvider.MicrosoftTranslator];
            
            // Set required headers for Microsoft Translator
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _config.ApiKey);
            if (!string.IsNullOrEmpty(_config.Region))
            {
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", _config.Region);
            }
        }

        public async Task<TranslationResponse> TranslateAsync(TranslationRequest request)
        {
            try
            {
                _logger.LogInformation($"🌍 Microsoft Translator: {request.SourceLanguage} -> {request.TargetLanguage}");

                var requestBody = new[]
                {
                    new { Text = request.Text }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{BaseUrl}/translate?api-version=3.0&to={request.TargetLanguage}";
                
                if (!string.IsNullOrEmpty(request.SourceLanguage) && request.SourceLanguage != "auto")
                {
                    url += $"&from={request.SourceLanguage}";
                }

                if (request.UseHighQuality)
                {
                    url += "&category=generalnn"; // Use neural machine translation
                }

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<List<MicrosoftTranslateResponse>>(responseContent);
                    
                    if (result?.Any() == true && result.First().Translations?.Any() == true)
                    {
                        var translation = result.First().Translations.First();
                        var detectedLanguage = result.First().DetectedLanguage?.Language ?? request.SourceLanguage ?? "unknown";
                        
                        return new TranslationResponse
                        {
                            Success = true,
                            TranslatedText = translation.Text,
                            DetectedSourceLanguage = detectedLanguage,
                            Confidence = result.First().DetectedLanguage?.Score ?? 0.90f,
                            UsedProvider = TranslationProvider.MicrosoftTranslator,
                            ProviderMetadata = new Dictionary<string, object>
                            {
                                ["characters_billed"] = request.Text.Length,
                                ["detected_score"] = result.First().DetectedLanguage?.Score ?? 0
                            }
                        };
                    }
                }

                _logger.LogError($"❌ Microsoft Translator API error: {response.StatusCode} - {responseContent}");
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = $"Microsoft Translator API error: {response.StatusCode}",
                    UsedProvider = TranslationProvider.MicrosoftTranslator
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Microsoft Translator provider error");
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UsedProvider = TranslationProvider.MicrosoftTranslator
                };
            }
        }

        public async Task<LanguageDetectionResponse> DetectLanguageAsync(LanguageDetectionRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 Microsoft Translator: Detecting language");

                var requestBody = new[]
                {
                    new { Text = request.Text }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/detect?api-version=3.0", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<List<MicrosoftDetectResponse>>(responseContent);
                    
                    if (result?.Any() == true)
                    {
                        var detection = result.First();
                        var alternatives = detection.Alternatives?.Take(request.MaxAlternatives - 1)
                            .Select(alt => new DetectedLanguageAlternative
                            {
                                LanguageCode = alt.Language,
                                Confidence = alt.Score,
                                LanguageName = GetLanguageName(alt.Language)
                            }).ToList() ?? new List<DetectedLanguageAlternative>();
                        
                        return new LanguageDetectionResponse
                        {
                            Success = true,
                            DetectedLanguage = detection.Language,
                            Confidence = detection.Score,
                            Alternatives = alternatives
                        };
                    }
                }

                return new LanguageDetectionResponse
                {
                    Success = false,
                    ErrorMessage = $"Microsoft language detection failed: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Microsoft language detection error");
                return new LanguageDetectionResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<List<SupportedLanguage>> GetSupportedLanguagesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/languages?api-version=3.0&scope=translation");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<MicrosoftLanguagesResponse>(responseContent);
                    
                    return result?.Translation?.Select(kvp => new SupportedLanguage
                    {
                        Code = kvp.Key,
                        Name = kvp.Value.Name,
                        NativeName = kvp.Value.NativeName,
                        SupportsTranslation = true,
                        SupportsSpeechRecognition = IsSpeechRecognitionSupported(kvp.Key),
                        SupportsTextToSpeech = IsTextToSpeechSupported(kvp.Key),
                        SupportedProviders = new List<TranslationProvider> { TranslationProvider.MicrosoftTranslator },
                        IsRTL = IsRightToLeft(kvp.Key),
                        Region = GetRegionForLanguage(kvp.Key)
                    }).ToList() ?? new List<SupportedLanguage>();
                }

                return new List<SupportedLanguage>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting Microsoft supported languages");
                return new List<SupportedLanguage>();
            }
        }

        #region Microsoft API Response Models

        private class MicrosoftTranslateResponse
        {
            public List<MicrosoftTranslation>? Translations { get; set; }
            public MicrosoftDetectedLanguage? DetectedLanguage { get; set; }
        }

        private class MicrosoftTranslation
        {
            public string Text { get; set; } = string.Empty;
            public string To { get; set; } = string.Empty;
        }

        private class MicrosoftDetectedLanguage
        {
            public string Language { get; set; } = string.Empty;
            public float Score { get; set; }
        }

        private class MicrosoftDetectResponse
        {
            public string Language { get; set; } = string.Empty;
            public float Score { get; set; }
            public bool IsTranslationSupported { get; set; }
            public bool IsTransliterationSupported { get; set; }
            public List<MicrosoftDetectionAlternative>? Alternatives { get; set; }
        }

        private class MicrosoftDetectionAlternative
        {
            public string Language { get; set; } = string.Empty;
            public float Score { get; set; }
            public bool IsTranslationSupported { get; set; }
        }

        private class MicrosoftLanguagesResponse
        {
            public Dictionary<string, MicrosoftLanguageInfo>? Translation { get; set; }
        }

        private class MicrosoftLanguageInfo
        {
            public string Name { get; set; } = string.Empty;
            public string NativeName { get; set; } = string.Empty;
            public string Dir { get; set; } = "ltr";
        }

        #endregion

        #region Helper Methods

        private string GetLanguageName(string code)
        {
            var languageNames = new Dictionary<string, string>
            {
                ["en"] = "English", ["es"] = "Spanish", ["fr"] = "French", ["de"] = "German",
                ["it"] = "Italian", ["pt"] = "Portuguese", ["zh"] = "Chinese", ["ja"] = "Japanese",
                ["ko"] = "Korean", ["ar"] = "Arabic", ["ru"] = "Russian", ["hi"] = "Hindi",
                ["nl"] = "Dutch", ["sv"] = "Swedish", ["no"] = "Norwegian", ["da"] = "Danish",
                ["fi"] = "Finnish", ["pl"] = "Polish", ["tr"] = "Turkish", ["th"] = "Thai",
                ["vi"] = "Vietnamese", ["id"] = "Indonesian", ["ms"] = "Malay", ["tl"] = "Filipino",
                ["sw"] = "Swahili", ["he"] = "Hebrew", ["fa"] = "Persian", ["ur"] = "Urdu", ["bn"] = "Bengali"
            };

            return languageNames.TryGetValue(code, out var name) ? name : code.ToUpper();
        }

        private bool IsSpeechRecognitionSupported(string languageCode)
        {
            var supportedSpeechLanguages = new[]
            {
                "en", "es", "fr", "de", "it", "pt", "zh", "ja", "ko", "ar", "ru", "hi",
                "nl", "sv", "no", "da", "fi", "pl", "tr", "th", "vi", "id", "ms", "he"
            };

            return supportedSpeechLanguages.Contains(languageCode);
        }

        private bool IsTextToSpeechSupported(string languageCode)
        {
            var supportedTTSLanguages = new[]
            {
                "en", "es", "fr", "de", "it", "pt", "zh", "ja", "ko", "ar", "ru", "hi",
                "nl", "sv", "no", "da", "fi", "pl", "tr", "th", "vi", "id", "he"
            };

            return supportedTTSLanguages.Contains(languageCode);
        }

        private bool IsRightToLeft(string languageCode)
        {
            var rtlLanguages = new[] { "ar", "he", "fa", "ur" };
            return rtlLanguages.Contains(languageCode);
        }

        private string? GetRegionForLanguage(string languageCode)
        {
            var regionMappings = new Dictionary<string, string>
            {
                ["en"] = "Americas", ["es"] = "Americas", ["fr"] = "Europe", ["de"] = "Europe",
                ["it"] = "Europe", ["pt"] = "Americas", ["zh"] = "Asia", ["ja"] = "Asia",
                ["ko"] = "Asia", ["ar"] = "Middle East", ["ru"] = "Europe", ["hi"] = "Asia"
            };

            return regionMappings.TryGetValue(languageCode, out var region) ? region : null;
        }

        #endregion
    }

    /// <summary>
    /// Comprehensive localization service for Visual Voicemail Pro
    /// Manages UI strings, culture settings, and resource management
    /// </summary>
    public interface ILocalizationService
    {
        Task<string> GetLocalizedStringAsync(string key, string languageCode, params object[] args);
        Task<Dictionary<string, string>> GetLocalizedResourcesAsync(string languageCode, string category = "general");
        Task SaveLocalizationResourceAsync(string key, string languageCode, string value, string category = "general");
        Task<List<string>> GetAvailableLanguagesAsync();
        string GetCurrentCulture();
        void SetCurrentCulture(string languageCode);
    }

    public class LocalizationService : ILocalizationService
    {
        private readonly VisualVoicemailDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LocalizationService> _logger;
        private string _currentCulture = "en";

        public LocalizationService(
            VisualVoicemailDbContext context,
            IMemoryCache cache,
            ILogger<LocalizationService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<string> GetLocalizedStringAsync(string key, string languageCode, params object[] args)
        {
            try
            {
                var cacheKey = $"localization_{languageCode}_{key}";
                
                if (_cache.TryGetValue(cacheKey, out string? cachedValue) && !string.IsNullOrEmpty(cachedValue))
                {
                    return args.Length > 0 ? string.Format(cachedValue, args) : cachedValue;
                }

                var resource = await _context.LocalizationResources
                    .FirstOrDefaultAsync(r => r.Key == key && r.LanguageCode == languageCode);

                string value;
                if (resource != null)
                {
                    value = resource.Value;
                }
                else
                {
                    // Fallback to English
                    var fallbackResource = await _context.LocalizationResources
                        .FirstOrDefaultAsync(r => r.Key == key && r.LanguageCode == "en");
                    
                    value = fallbackResource?.Value ?? key; // Use key as last resort
                    
                    _logger.LogWarning($"⚠️ Missing localization for key '{key}' in language '{languageCode}', using fallback");
                }

                // Cache for 1 hour
                _cache.Set(cacheKey, value, TimeSpan.FromHours(1));

                return args.Length > 0 ? string.Format(value, args) : value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error getting localized string for key '{key}' in language '{languageCode}'");
                return key; // Fallback to key
            }
        }

        public async Task<Dictionary<string, string>> GetLocalizedResourcesAsync(string languageCode, string category = "general")
        {
            try
            {
                var cacheKey = $"localization_resources_{languageCode}_{category}";
                
                if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cachedResources))
                {
                    return cachedResources!;
                }

                var resources = await _context.LocalizationResources
                    .Where(r => r.LanguageCode == languageCode && r.Category == category)
                    .ToDictionaryAsync(r => r.Key, r => r.Value);

                // If no resources found for this language, try English fallback
                if (!resources.Any() && languageCode != "en")
                {
                    resources = await _context.LocalizationResources
                        .Where(r => r.LanguageCode == "en" && r.Category == category)
                        .ToDictionaryAsync(r => r.Key, r => r.Value);
                }

                // Cache for 1 hour
                _cache.Set(cacheKey, resources, TimeSpan.FromHours(1));

                return resources;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error getting localized resources for language '{languageCode}' and category '{category}'");
                return new Dictionary<string, string>();
            }
        }

        public async Task SaveLocalizationResourceAsync(string key, string languageCode, string value, string category = "general")
        {
            try
            {
                var existing = await _context.LocalizationResources
                    .FirstOrDefaultAsync(r => r.Key == key && r.LanguageCode == languageCode && r.Category == category);

                if (existing != null)
                {
                    existing.Value = value;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _context.LocalizationResources.Update(existing);
                }
                else
                {
                    var resource = new LocalizationResource
                    {
                        Key = key,
                        LanguageCode = languageCode,
                        Value = value,
                        Category = category,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.LocalizationResources.Add(resource);
                }

                await _context.SaveChangesAsync();

                // Clear cache for this language/category
                var cacheKey = $"localization_resources_{languageCode}_{category}";
                _cache.Remove(cacheKey);
                
                var specificCacheKey = $"localization_{languageCode}_{key}";
                _cache.Remove(specificCacheKey);

                _logger.LogInformation($"✅ Saved localization resource: {key} = {value} ({languageCode})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error saving localization resource for key '{key}' in language '{languageCode}'");
            }
        }

        public async Task<List<string>> GetAvailableLanguagesAsync()
        {
            try
            {
                var cacheKey = "available_languages";
                
                if (_cache.TryGetValue(cacheKey, out List<string>? cachedLanguages))
                {
                    return cachedLanguages!;
                }

                var languages = await _context.LocalizationResources
                    .Select(r => r.LanguageCode)
                    .Distinct()
                    .OrderBy(l => l)
                    .ToListAsync();

                // Cache for 2 hours
                _cache.Set(cacheKey, languages, TimeSpan.FromHours(2));

                return languages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting available languages");
                return new List<string> { "en" }; // Default fallback
            }
        }

        public string GetCurrentCulture()
        {
            return _currentCulture;
        }

        public void SetCurrentCulture(string languageCode)
        {
            _currentCulture = languageCode;
            _logger.LogInformation($"🌍 Culture set to: {languageCode}");
        }
    }
}