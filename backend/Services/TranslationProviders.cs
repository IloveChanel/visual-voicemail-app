using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using VisualVoicemailPro.Models;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// Google Cloud Translation API provider
    /// Comprehensive implementation with advanced features
    /// </summary>
    public interface IGoogleTranslationProvider
    {
        Task<TranslationResponse> TranslateAsync(TranslationRequest request);
        Task<LanguageDetectionResponse> DetectLanguageAsync(LanguageDetectionRequest request);
        Task<List<SupportedLanguage>> GetSupportedLanguagesAsync();
    }

    public class GoogleTranslationProvider : IGoogleTranslationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleTranslationProvider> _logger;
        private readonly TranslationProviderConfig _config;
        private const string BaseUrl = "https://translation.googleapis.com/language/translate/v2";

        public GoogleTranslationProvider(
            HttpClient httpClient,
            ILogger<GoogleTranslationProvider> logger,
            IOptions<TranslationConfiguration> config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config.Value.Providers[TranslationProvider.GoogleTranslate];
        }

        public async Task<TranslationResponse> TranslateAsync(TranslationRequest request)
        {
            try
            {
                _logger.LogInformation($"🌍 Google Translate: {request.SourceLanguage} -> {request.TargetLanguage}");

                var payload = new
                {
                    q = request.Text,
                    target = request.TargetLanguage,
                    source = request.SourceLanguage == "auto" ? null : request.SourceLanguage,
                    format = "text",
                    model = request.UseHighQuality ? "nmt" : "base",
                    key = _config.ApiKey
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(BaseUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<GoogleTranslateResponse>(responseContent);
                    
                    if (result?.Data?.Translations?.Any() == true)
                    {
                        var translation = result.Data.Translations.First();
                        
                        return new TranslationResponse
                        {
                            Success = true,
                            TranslatedText = translation.TranslatedText,
                            DetectedSourceLanguage = translation.DetectedSourceLanguage ?? request.SourceLanguage ?? "unknown",
                            Confidence = 0.95f, // Google doesn't provide confidence, estimate high
                            UsedProvider = TranslationProvider.GoogleTranslate,
                            ProviderMetadata = new Dictionary<string, object>
                            {
                                ["model"] = payload.model,
                                ["characters_billed"] = request.Text.Length
                            }
                        };
                    }
                }

                _logger.LogError($"❌ Google Translate API error: {response.StatusCode} - {responseContent}");
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = $"Google Translate API error: {response.StatusCode}",
                    UsedProvider = TranslationProvider.GoogleTranslate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Google Translate provider error");
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UsedProvider = TranslationProvider.GoogleTranslate
                };
            }
        }

        public async Task<LanguageDetectionResponse> DetectLanguageAsync(LanguageDetectionRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 Google Translate: Detecting language");

                var payload = new
                {
                    q = request.Text,
                    key = _config.ApiKey
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/detect", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<GoogleDetectResponse>(responseContent);
                    
                    if (result?.Data?.Detections?.Any() == true)
                    {
                        var detections = result.Data.Detections.First();
                        var topDetection = detections.OrderByDescending(d => d.Confidence).First();
                        
                        return new LanguageDetectionResponse
                        {
                            Success = true,
                            DetectedLanguage = topDetection.Language,
                            Confidence = topDetection.Confidence,
                            Alternatives = detections.Skip(1).Take(request.MaxAlternatives - 1)
                                .Select(d => new DetectedLanguageAlternative
                                {
                                    LanguageCode = d.Language,
                                    Confidence = d.Confidence,
                                    LanguageName = GetLanguageName(d.Language)
                                }).ToList()
                        };
                    }
                }

                return new LanguageDetectionResponse
                {
                    Success = false,
                    ErrorMessage = $"Google language detection failed: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Google language detection error");
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
                var response = await _httpClient.GetAsync($"{BaseUrl}/languages?key={_config.ApiKey}&target=en");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<GoogleLanguagesResponse>(responseContent);
                    
                    return result?.Data?.Languages?.Select(lang => new SupportedLanguage
                    {
                        Code = lang.Language,
                        Name = lang.Name ?? GetLanguageName(lang.Language),
                        NativeName = GetNativeLanguageName(lang.Language),
                        SupportsTranslation = true,
                        SupportsSpeechRecognition = IsSpeechRecognitionSupported(lang.Language),
                        SupportsTextToSpeech = IsTextToSpeechSupported(lang.Language),
                        SupportedProviders = new List<TranslationProvider> { TranslationProvider.GoogleTranslate },
                        IsRTL = IsRightToLeft(lang.Language)
                    }).ToList() ?? new List<SupportedLanguage>();
                }

                return new List<SupportedLanguage>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting Google supported languages");
                return new List<SupportedLanguage>();
            }
        }

        #region Google API Response Models

        private class GoogleTranslateResponse
        {
            public GoogleTranslateData? Data { get; set; }
        }

        private class GoogleTranslateData
        {
            public List<GoogleTranslation>? Translations { get; set; }
        }

        private class GoogleTranslation
        {
            public string TranslatedText { get; set; } = string.Empty;
            public string? DetectedSourceLanguage { get; set; }
        }

        private class GoogleDetectResponse
        {
            public GoogleDetectData? Data { get; set; }
        }

        private class GoogleDetectData
        {
            public List<List<GoogleDetection>>? Detections { get; set; }
        }

        private class GoogleDetection
        {
            public string Language { get; set; } = string.Empty;
            public float Confidence { get; set; }
            public bool IsReliable { get; set; }
        }

        private class GoogleLanguagesResponse
        {
            public GoogleLanguagesData? Data { get; set; }
        }

        private class GoogleLanguagesData
        {
            public List<GoogleLanguage>? Languages { get; set; }
        }

        private class GoogleLanguage
        {
            public string Language { get; set; } = string.Empty;
            public string? Name { get; set; }
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

        private string GetNativeLanguageName(string code)
        {
            var nativeNames = new Dictionary<string, string>
            {
                ["en"] = "English", ["es"] = "Español", ["fr"] = "Français", ["de"] = "Deutsch",
                ["it"] = "Italiano", ["pt"] = "Português", ["zh"] = "中文", ["ja"] = "日本語",
                ["ko"] = "한국어", ["ar"] = "العربية", ["ru"] = "Русский", ["hi"] = "हिन्दी",
                ["nl"] = "Nederlands", ["sv"] = "Svenska", ["no"] = "Norsk", ["da"] = "Dansk",
                ["fi"] = "Suomi", ["pl"] = "Polski", ["tr"] = "Türkçe", ["th"] = "ไทย",
                ["vi"] = "Tiếng Việt", ["id"] = "Bahasa Indonesia", ["ms"] = "Bahasa Melayu", 
                ["tl"] = "Filipino", ["sw"] = "Kiswahili", ["he"] = "עברית", ["fa"] = "فارسی", 
                ["ur"] = "اردو", ["bn"] = "বাংলা"
            };

            return nativeNames.TryGetValue(code, out var name) ? name : code.ToUpper();
        }

        private bool IsSpeechRecognitionSupported(string languageCode)
        {
            var supportedSpeechLanguages = new[]
            {
                "en", "es", "fr", "de", "it", "pt", "zh", "ja", "ko", "ar", "ru", "hi",
                "nl", "sv", "no", "da", "fi", "pl", "tr", "th", "vi", "id", "ms", "tl", "he"
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

        #endregion
    }

    /// <summary>
    /// DeepL Translation API provider
    /// High-quality translations with context awareness
    /// </summary>
    public interface IDeepLTranslationProvider
    {
        Task<TranslationResponse> TranslateAsync(TranslationRequest request);
        Task<List<SupportedLanguage>> GetSupportedLanguagesAsync();
    }

    public class DeepLTranslationProvider : IDeepLTranslationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DeepLTranslationProvider> _logger;
        private readonly TranslationProviderConfig _config;
        private const string BaseUrl = "https://api-free.deepl.com/v2"; // Use api.deepl.com for paid plans

        public DeepLTranslationProvider(
            HttpClient httpClient,
            ILogger<DeepLTranslationProvider> logger,
            IOptions<TranslationConfiguration> config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config.Value.Providers[TranslationProvider.DeepL];
        }

        public async Task<TranslationResponse> TranslateAsync(TranslationRequest request)
        {
            try
            {
                _logger.LogInformation($"🌍 DeepL Translate: {request.SourceLanguage} -> {request.TargetLanguage}");

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("text", request.Text),
                    new("target_lang", MapToDeepLLanguageCode(request.TargetLanguage)),
                    new("auth_key", _config.ApiKey)
                };

                if (!string.IsNullOrEmpty(request.SourceLanguage) && request.SourceLanguage != "auto")
                {
                    formData.Add(new("source_lang", MapToDeepLLanguageCode(request.SourceLanguage)));
                }

                if (request.UseHighQuality)
                {
                    formData.Add(new("formality", "default")); // Could be "more" or "less" for formal/informal
                }

                if (!string.IsNullOrEmpty(request.Context))
                {
                    formData.Add(new("tag_handling", "html")); // Better context handling
                }

                var content = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync($"{BaseUrl}/translate", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<DeepLTranslateResponse>(responseContent);
                    
                    if (result?.Translations?.Any() == true)
                    {
                        var translation = result.Translations.First();
                        
                        return new TranslationResponse
                        {
                            Success = true,
                            TranslatedText = translation.Text,
                            DetectedSourceLanguage = translation.DetectedSourceLanguage ?? request.SourceLanguage ?? "unknown",
                            Confidence = 0.98f, // DeepL generally has high quality
                            UsedProvider = TranslationProvider.DeepL,
                            ProviderMetadata = new Dictionary<string, object>
                            {
                                ["characters_billed"] = request.Text.Length,
                                ["detected_source"] = translation.DetectedSourceLanguage ?? "unknown"
                            }
                        };
                    }
                }

                _logger.LogError($"❌ DeepL API error: {response.StatusCode} - {responseContent}");
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = $"DeepL API error: {response.StatusCode}",
                    UsedProvider = TranslationProvider.DeepL
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ DeepL provider error");
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    UsedProvider = TranslationProvider.DeepL
                };
            }
        }

        public async Task<List<SupportedLanguage>> GetSupportedLanguagesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/languages?auth_key={_config.ApiKey}&type=target");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<List<DeepLLanguage>>(responseContent);
                    
                    return result?.Select(lang => new SupportedLanguage
                    {
                        Code = lang.Language.ToLower(),
                        Name = lang.Name,
                        NativeName = lang.Name, // DeepL doesn't provide native names
                        SupportsTranslation = true,
                        SupportsSpeechRecognition = false, // DeepL is translation only
                        SupportsTextToSpeech = false,
                        SupportedProviders = new List<TranslationProvider> { TranslationProvider.DeepL },
                        IsRTL = IsRightToLeft(lang.Language.ToLower())
                    }).ToList() ?? new List<SupportedLanguage>();
                }

                return new List<SupportedLanguage>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting DeepL supported languages");
                return new List<SupportedLanguage>();
            }
        }

        #region DeepL API Response Models

        private class DeepLTranslateResponse
        {
            public List<DeepLTranslation>? Translations { get; set; }
        }

        private class DeepLTranslation
        {
            public string Text { get; set; } = string.Empty;
            public string? DetectedSourceLanguage { get; set; }
        }

        private class DeepLLanguage
        {
            public string Language { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        #endregion

        #region Helper Methods

        private string MapToDeepLLanguageCode(string languageCode)
        {
            // DeepL uses specific language codes
            var mappings = new Dictionary<string, string>
            {
                ["en"] = "EN", ["de"] = "DE", ["fr"] = "FR", ["it"] = "IT", ["ja"] = "JA",
                ["es"] = "ES", ["nl"] = "NL", ["pl"] = "PL", ["pt"] = "PT", ["ru"] = "RU",
                ["zh"] = "ZH", ["bg"] = "BG", ["cs"] = "CS", ["da"] = "DA", ["et"] = "ET",
                ["fi"] = "FI", ["el"] = "EL", ["hu"] = "HU", ["lv"] = "LV", ["lt"] = "LT",
                ["ro"] = "RO", ["sk"] = "SK", ["sl"] = "SL", ["sv"] = "SV", ["tr"] = "TR"
            };

            return mappings.TryGetValue(languageCode.ToLower(), out var mapped) ? mapped : languageCode.ToUpper();
        }

        private bool IsRightToLeft(string languageCode)
        {
            var rtlLanguages = new[] { "ar", "he", "fa", "ur" };
            return rtlLanguages.Contains(languageCode);
        }

        #endregion
    }
}