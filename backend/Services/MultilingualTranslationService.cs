using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VisualVoicemailPro.Models;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// Comprehensive multilingual translation service
    /// Supports Google Cloud Translation API, DeepL, Microsoft Translator
    /// with translation memory, caching, and fallback providers
    /// </summary>
    public interface IMultilingualTranslationService
    {
        Task<TranslationResponse> TranslateAsync(TranslationRequest request);
        Task<BatchTranslationResponse> TranslateBatchAsync(BatchTranslationRequest request);
        Task<LanguageDetectionResponse> DetectLanguageAsync(LanguageDetectionRequest request);
        Task<List<SupportedLanguage>> GetSupportedLanguagesAsync();
        Task<TranslationStatistics> GetUserStatisticsAsync(string userId);
        Task SaveTranslationMemoryAsync(string sourceText, string translatedText, string sourceLanguage, string targetLanguage, string context = "");
        Task<string?> GetFromTranslationMemoryAsync(string sourceText, string sourceLanguage, string targetLanguage);
    }

    public class MultilingualTranslationService : IMultilingualTranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MultilingualTranslationService> _logger;
        private readonly TranslationConfiguration _config;
        private readonly VisualVoicemailDbContext _context;

        // Provider-specific services
        private readonly IGoogleTranslationProvider _googleProvider;
        private readonly IDeepLTranslationProvider _deepLProvider;
        private readonly IMicrosoftTranslationProvider _microsoftProvider;

        public MultilingualTranslationService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<MultilingualTranslationService> logger,
            IOptions<TranslationConfiguration> config,
            VisualVoicemailDbContext context,
            IGoogleTranslationProvider googleProvider,
            IDeepLTranslationProvider deepLProvider,
            IMicrosoftTranslationProvider microsoftProvider)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _config = config.Value;
            _context = context;
            _googleProvider = googleProvider;
            _deepLProvider = deepLProvider;
            _microsoftProvider = microsoftProvider;
        }

        public async Task<TranslationResponse> TranslateAsync(TranslationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation($"🌍 Starting translation: {request.SourceLanguage} -> {request.TargetLanguage}");

                // Validate request
                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return new TranslationResponse
                    {
                        Success = false,
                        ErrorMessage = "Text cannot be empty"
                    };
                }

                // Check cache first
                if (_config.EnableCaching)
                {
                    var cachedResult = GetFromCache(request);
                    if (cachedResult != null)
                    {
                        _logger.LogInformation("✅ Translation retrieved from cache");
                        return cachedResult;
                    }
                }

                // Check translation memory
                if (_config.EnableTranslationMemory)
                {
                    var memoryResult = await GetFromTranslationMemoryAsync(
                        request.Text, 
                        request.SourceLanguage ?? "auto", 
                        request.TargetLanguage);
                    
                    if (!string.IsNullOrEmpty(memoryResult))
                    {
                        _logger.LogInformation("✅ Translation retrieved from memory");
                        var memoryResponse = new TranslationResponse
                        {
                            Success = true,
                            TranslatedText = memoryResult,
                            DetectedSourceLanguage = request.SourceLanguage ?? "auto",
                            Confidence = 1.0f,
                            UsedProvider = TranslationProvider.TranslationMemory,
                            ProcessingTime = DateTime.UtcNow - startTime,
                            CharacterCount = request.Text.Length
                        };
                        
                        SaveToCache(request, memoryResponse);
                        return memoryResponse;
                    }
                }

                // Detect source language if not provided
                if (string.IsNullOrEmpty(request.SourceLanguage) || request.SourceLanguage == "auto")
                {
                    var detectionResult = await DetectLanguageAsync(new LanguageDetectionRequest
                    {
                        Text = request.Text,
                        UserId = request.UserId
                    });

                    if (detectionResult.Success)
                    {
                        request.SourceLanguage = detectionResult.DetectedLanguage;
                    }
                }

                // Skip translation if source and target are the same
                if (request.SourceLanguage == request.TargetLanguage)
                {
                    return new TranslationResponse
                    {
                        Success = true,
                        TranslatedText = request.Text,
                        DetectedSourceLanguage = request.SourceLanguage,
                        Confidence = 1.0f,
                        UsedProvider = TranslationProvider.TranslationMemory,
                        ProcessingTime = DateTime.UtcNow - startTime,
                        CharacterCount = request.Text.Length
                    };
                }

                // Get ordered providers based on preference and availability
                var providers = GetOrderedProviders(request);
                
                TranslationResponse? response = null;
                var attempts = 0;

                // Try each provider until successful or all exhausted
                foreach (var provider in providers)
                {
                    if (attempts >= _config.MaxRetryAttempts) break;

                    try
                    {
                        _logger.LogInformation($"🔄 Attempting translation with {provider}");
                        
                        response = provider switch
                        {
                            TranslationProvider.GoogleTranslate => await _googleProvider.TranslateAsync(request),
                            TranslationProvider.DeepL => await _deepLProvider.TranslateAsync(request),
                            TranslationProvider.MicrosoftTranslator => await _microsoftProvider.TranslateAsync(request),
                            _ => throw new NotSupportedException($"Provider {provider} not supported")
                        };

                        if (response.Success)
                        {
                            response.ProcessingTime = DateTime.UtcNow - startTime;
                            response.CharacterCount = request.Text.Length;
                            
                            // Save to cache and translation memory
                            SaveToCache(request, response);
                            
                            if (_config.EnableTranslationMemory)
                            {
                                await SaveTranslationMemoryAsync(
                                    request.Text,
                                    response.TranslatedText,
                                    response.DetectedSourceLanguage,
                                    request.TargetLanguage,
                                    request.Context ?? ""
                                );
                            }

                            // Log usage statistics
                            await LogTranslationUsageAsync(request, response);
                            
                            _logger.LogInformation($"✅ Translation successful with {provider}");
                            break;
                        }

                        attempts++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"⚠️ Translation failed with {provider}: {ex.Message}");
                        attempts++;
                    }
                }

                return response ?? new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = "All translation providers failed",
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Translation service error");
                return new TranslationResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<BatchTranslationResponse> TranslateBatchAsync(BatchTranslationRequest request)
        {
            var startTime = DateTime.UtcNow;
            var response = new BatchTranslationResponse();

            try
            {
                _logger.LogInformation($"🌍 Starting batch translation of {request.Texts.Count} texts");

                // Split into smaller batches if needed
                var batches = SplitIntoBatches(request.Texts, _config.MaxBatchSize);
                
                foreach (var batch in batches)
                {
                    var batchTasks = batch.Select(async text =>
                    {
                        var translationRequest = new TranslationRequest
                        {
                            Text = text,
                            TargetLanguage = request.TargetLanguage,
                            SourceLanguage = request.SourceLanguage,
                            UserId = request.UserId,
                            PreferredProvider = request.PreferredProvider,
                            UseHighQuality = request.UseHighQuality
                        };

                        return await TranslateAsync(translationRequest);
                    });

                    var batchResults = await Task.WhenAll(batchTasks);
                    response.Translations.AddRange(batchResults);
                }

                response.Success = response.Translations.Any(t => t.Success);
                response.TotalProcessingTime = DateTime.UtcNow - startTime;
                response.TotalCharacterCount = response.Translations.Sum(t => t.CharacterCount);

                // Calculate provider usage statistics
                foreach (var translation in response.Translations.Where(t => t.Success))
                {
                    if (response.ProviderUsage.ContainsKey(translation.UsedProvider))
                        response.ProviderUsage[translation.UsedProvider]++;
                    else
                        response.ProviderUsage[translation.UsedProvider] = 1;
                }

                _logger.LogInformation($"✅ Batch translation completed: {response.Translations.Count(t => t.Success)}/{response.Translations.Count} successful");
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Batch translation error");
                response.Success = false;
                response.ErrorMessage = ex.Message;
                response.TotalProcessingTime = DateTime.UtcNow - startTime;
                return response;
            }
        }

        public async Task<LanguageDetectionResponse> DetectLanguageAsync(LanguageDetectionRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 Detecting language");

                // Try providers in order of preference
                var providers = GetOrderedProviders(new TranslationRequest());

                foreach (var provider in providers.Take(2)) // Try top 2 providers for detection
                {
                    try
                    {
                        var result = provider switch
                        {
                            TranslationProvider.GoogleTranslate => await _googleProvider.DetectLanguageAsync(request),
                            TranslationProvider.MicrosoftTranslator => await _microsoftProvider.DetectLanguageAsync(request),
                            _ => null
                        };

                        if (result?.Success == true)
                        {
                            _logger.LogInformation($"✅ Language detected: {result.DetectedLanguage} (confidence: {result.Confidence:P1})");
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"⚠️ Language detection failed with {provider}: {ex.Message}");
                    }
                }

                return new LanguageDetectionResponse
                {
                    Success = false,
                    ErrorMessage = "Language detection failed with all providers"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Language detection error");
                return new LanguageDetectionResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<List<SupportedLanguage>> GetSupportedLanguagesAsync()
        {
            var cacheKey = "supported_languages";
            
            if (_cache.TryGetValue(cacheKey, out List<SupportedLanguage>? cachedLanguages))
            {
                return cachedLanguages!;
            }

            try
            {
                var languages = new List<SupportedLanguage>();

                // Combine supported languages from all providers
                var googleLanguages = await _googleProvider.GetSupportedLanguagesAsync();
                var deepLLanguages = await _deepLProvider.GetSupportedLanguagesAsync();
                var microsoftLanguages = await _microsoftProvider.GetSupportedLanguagesAsync();

                // Merge and deduplicate languages
                var allLanguages = googleLanguages
                    .Concat(deepLLanguages)
                    .Concat(microsoftLanguages)
                    .GroupBy(l => l.Code)
                    .Select(g => new SupportedLanguage
                    {
                        Code = g.Key,
                        Name = g.First().Name,
                        NativeName = g.First().NativeName,
                        SupportsTranslation = g.Any(l => l.SupportsTranslation),
                        SupportsSpeechRecognition = g.Any(l => l.SupportsSpeechRecognition),
                        SupportsTextToSpeech = g.Any(l => l.SupportsTextToSpeech),
                        SupportedProviders = g.SelectMany(l => l.SupportedProviders).Distinct().ToList(),
                        CountryCode = g.First().CountryCode,
                        Region = g.First().Region,
                        IsRTL = g.First().IsRTL
                    })
                    .OrderBy(l => l.Name)
                    .ToList();

                // Cache for 1 hour
                _cache.Set(cacheKey, allLanguages, TimeSpan.FromHours(1));
                
                return allLanguages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting supported languages");
                return new List<SupportedLanguage>();
            }
        }

        public async Task<TranslationStatistics> GetUserStatisticsAsync(string userId)
        {
            try
            {
                var usages = await _context.TranslationUsages
                    .Where(u => u.UserId == userId)
                    .ToListAsync();

                if (!usages.Any())
                {
                    return new TranslationStatistics
                    {
                        UserId = userId,
                        SuccessRate = 0
                    };
                }

                var stats = new TranslationStatistics
                {
                    UserId = userId,
                    TotalTranslations = usages.Count,
                    TotalCharacters = usages.Sum(u => u.CharacterCount),
                    TotalCost = usages.Sum(u => u.Cost),
                    FirstTranslation = usages.Min(u => u.CreatedAt),
                    LastTranslation = usages.Max(u => u.CreatedAt),
                    AverageProcessingTime = (float)usages.Average(u => u.ProcessingTime.TotalMilliseconds),
                    SuccessRate = 1.0f // We only log successful translations
                };

                // Language pairs
                stats.LanguagePairs = usages
                    .GroupBy(u => $"{u.SourceLanguage}->{u.TargetLanguage}")
                    .ToDictionary(g => g.Key, g => g.Count());

                // Provider usage
                stats.ProviderUsage = usages
                    .GroupBy(u => u.Provider)
                    .ToDictionary(g => g.Key, g => g.Count());

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting user statistics");
                return new TranslationStatistics { UserId = userId };
            }
        }

        public async Task SaveTranslationMemoryAsync(string sourceText, string translatedText, string sourceLanguage, string targetLanguage, string context = "")
        {
            try
            {
                // Check if this translation already exists
                var existing = await _context.TranslationMemoryEntries
                    .FirstOrDefaultAsync(tm => 
                        tm.SourceText == sourceText &&
                        tm.SourceLanguage == sourceLanguage &&
                        tm.TargetLanguage == targetLanguage);

                if (existing != null)
                {
                    existing.LastUsed = DateTime.UtcNow;
                    existing.UsageCount++;
                    _context.TranslationMemoryEntries.Update(existing);
                }
                else
                {
                    var entry = new TranslationMemoryEntry
                    {
                        SourceText = sourceText,
                        TranslatedText = translatedText,
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage,
                        Context = context,
                        UsageCount = 1,
                        QualityScore = 1.0f,
                        IsApproved = true
                    };

                    _context.TranslationMemoryEntries.Add(entry);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error saving translation memory");
            }
        }

        public async Task<string?> GetFromTranslationMemoryAsync(string sourceText, string sourceLanguage, string targetLanguage)
        {
            try
            {
                var entry = await _context.TranslationMemoryEntries
                    .FirstOrDefaultAsync(tm =>
                        tm.SourceText == sourceText &&
                        tm.SourceLanguage == sourceLanguage &&
                        tm.TargetLanguage == targetLanguage &&
                        tm.IsApproved);

                return entry?.TranslatedText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving from translation memory");
                return null;
            }
        }

        #region Private Helper Methods

        private List<TranslationProvider> GetOrderedProviders(TranslationRequest request)
        {
            var availableProviders = _config.Providers
                .Where(p => p.Value.IsEnabled)
                .OrderBy(p => p.Value.Priority)
                .Select(p => p.Key)
                .ToList();

            // Move preferred provider to front if specified
            if (availableProviders.Contains(request.PreferredProvider))
            {
                availableProviders.Remove(request.PreferredProvider);
                availableProviders.Insert(0, request.PreferredProvider);
            }

            return availableProviders;
        }

        private TranslationResponse? GetFromCache(TranslationRequest request)
        {
            var cacheKey = GenerateCacheKey(request);
            return _cache.Get<TranslationResponse>(cacheKey);
        }

        private void SaveToCache(TranslationRequest request, TranslationResponse response)
        {
            var cacheKey = GenerateCacheKey(request);
            _cache.Set(cacheKey, response, _config.CacheExpiration);
        }

        private string GenerateCacheKey(TranslationRequest request)
        {
            var keyComponents = new[]
            {
                request.Text,
                request.SourceLanguage ?? "auto",
                request.TargetLanguage,
                request.PreferredProvider.ToString()
            };

            var combined = string.Join("|", keyComponents);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hash);
        }

        private async Task LogTranslationUsageAsync(TranslationRequest request, TranslationResponse response)
        {
            try
            {
                if (!response.Success || string.IsNullOrEmpty(request.UserId)) return;

                var usage = new TranslationUsage
                {
                    UserId = request.UserId,
                    SourceLanguage = response.DetectedSourceLanguage,
                    TargetLanguage = request.TargetLanguage,
                    CharacterCount = response.CharacterCount,
                    Provider = response.UsedProvider,
                    ProcessingTime = response.ProcessingTime,
                    Context = request.Context ?? "",
                    IsHighQuality = request.UseHighQuality,
                    Cost = CalculateTranslationCost(response.UsedProvider, response.CharacterCount)
                };

                _context.TranslationUsages.Add(usage);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error logging translation usage");
            }
        }

        private decimal CalculateTranslationCost(TranslationProvider provider, int characterCount)
        {
            if (_config.Providers.TryGetValue(provider, out var config))
            {
                return config.CostPerCharacter * characterCount;
            }
            return 0;
        }

        private List<List<T>> SplitIntoBatches<T>(List<T> items, int batchSize)
        {
            var batches = new List<List<T>>();
            for (int i = 0; i < items.Count; i += batchSize)
            {
                batches.Add(items.Skip(i).Take(batchSize).ToList());
            }
            return batches;
        }

        #endregion
    }
}