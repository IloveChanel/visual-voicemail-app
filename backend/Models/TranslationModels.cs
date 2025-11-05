using System.ComponentModel.DataAnnotations;

namespace VisualVoicemailPro.Models
{
    /// <summary>
    /// Comprehensive translation models for multilingual support
    /// Supports Google Cloud Translation API, DeepL, and localization
    /// </summary>

    public class TranslationRequest
    {
        [Required]
        public string Text { get; set; } = string.Empty;
        
        [Required]
        public string TargetLanguage { get; set; } = string.Empty;
        
        public string? SourceLanguage { get; set; }
        
        public string? UserId { get; set; }
        
        public TranslationProvider PreferredProvider { get; set; } = TranslationProvider.GoogleTranslate;
        
        public bool UseHighQuality { get; set; } = true;
        
        public string? Context { get; set; } // For better context-aware translation
        
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class TranslationResponse
    {
        public bool Success { get; set; }
        
        public string TranslatedText { get; set; } = string.Empty;
        
        public string DetectedSourceLanguage { get; set; } = string.Empty;
        
        public float Confidence { get; set; }
        
        public TranslationProvider UsedProvider { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public TimeSpan ProcessingTime { get; set; }
        
        public int CharacterCount { get; set; }
        
        public Dictionary<string, object>? ProviderMetadata { get; set; }
    }

    public class BatchTranslationRequest
    {
        [Required]
        public List<string> Texts { get; set; } = new();
        
        [Required]
        public string TargetLanguage { get; set; } = string.Empty;
        
        public string? SourceLanguage { get; set; }
        
        public string? UserId { get; set; }
        
        public TranslationProvider PreferredProvider { get; set; } = TranslationProvider.GoogleTranslate;
        
        public bool UseHighQuality { get; set; } = true;
        
        public bool PreserveFormatting { get; set; } = true;
    }

    public class BatchTranslationResponse
    {
        public bool Success { get; set; }
        
        public List<TranslationResponse> Translations { get; set; } = new();
        
        public string? ErrorMessage { get; set; }
        
        public TimeSpan TotalProcessingTime { get; set; }
        
        public int TotalCharacterCount { get; set; }
        
        public Dictionary<TranslationProvider, int> ProviderUsage { get; set; } = new();
    }

    public class LanguageDetectionRequest
    {
        [Required]
        public string Text { get; set; } = string.Empty;
        
        public string? UserId { get; set; }
        
        public int MaxAlternatives { get; set; } = 3;
    }

    public class LanguageDetectionResponse
    {
        public bool Success { get; set; }
        
        public string DetectedLanguage { get; set; } = string.Empty;
        
        public float Confidence { get; set; }
        
        public List<DetectedLanguageAlternative> Alternatives { get; set; } = new();
        
        public string? ErrorMessage { get; set; }
    }

    public class DetectedLanguageAlternative
    {
        public string LanguageCode { get; set; } = string.Empty;
        
        public float Confidence { get; set; }
        
        public string LanguageName { get; set; } = string.Empty;
    }

    public class SupportedLanguage
    {
        public string Code { get; set; } = string.Empty;
        
        public string Name { get; set; } = string.Empty;
        
        public string NativeName { get; set; } = string.Empty;
        
        public bool SupportsTranslation { get; set; }
        
        public bool SupportsSpeechRecognition { get; set; }
        
        public bool SupportsTextToSpeech { get; set; }
        
        public List<TranslationProvider> SupportedProviders { get; set; } = new();
        
        public string? CountryCode { get; set; }
        
        public string? Region { get; set; }
        
        public bool IsRTL { get; set; } // Right-to-left language
    }

    public class TranslationUsage
    {
        public int Id { get; set; }
        
        public string UserId { get; set; } = string.Empty;
        
        public string SourceLanguage { get; set; } = string.Empty;
        
        public string TargetLanguage { get; set; } = string.Empty;
        
        public int CharacterCount { get; set; }
        
        public TranslationProvider Provider { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public TimeSpan ProcessingTime { get; set; }
        
        public string? Context { get; set; }
        
        public bool IsHighQuality { get; set; }
        
        public decimal Cost { get; set; }
    }

    public class TranslationMemoryEntry
    {
        public int Id { get; set; }
        
        public string SourceText { get; set; } = string.Empty;
        
        public string TranslatedText { get; set; } = string.Empty;
        
        public string SourceLanguage { get; set; } = string.Empty;
        
        public string TargetLanguage { get; set; } = string.Empty;
        
        public string Context { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        
        public int UsageCount { get; set; }
        
        public float QualityScore { get; set; }
        
        public string? CreatedBy { get; set; }
        
        public bool IsApproved { get; set; }
    }

    public class LocalizationResource
    {
        public int Id { get; set; }
        
        public string Key { get; set; } = string.Empty;
        
        public string LanguageCode { get; set; } = string.Empty;
        
        public string Value { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public string Category { get; set; } = "general";
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public string? UpdatedBy { get; set; }
        
        public bool IsPlural { get; set; }
        
        public string? PluralForm { get; set; }
    }

    public enum TranslationProvider
    {
        GoogleTranslate,
        DeepL,
        MicrosoftTranslator,
        AmazonTranslate,
        Systran,
        TranslationMemory // Use cached translations
    }

    public enum TranslationQuality
    {
        Fast,      // Fast, basic translation
        Standard,  // Standard quality
        High,      // High quality with context
        Premium    // Premium with human review
    }

    public class TranslationConfiguration
    {
        public Dictionary<TranslationProvider, TranslationProviderConfig> Providers { get; set; } = new();
        
        public string DefaultSourceLanguage { get; set; } = "auto";
        
        public string DefaultTargetLanguage { get; set; } = "en";
        
        public TranslationProvider PreferredProvider { get; set; } = TranslationProvider.GoogleTranslate;
        
        public bool EnableTranslationMemory { get; set; } = true;
        
        public bool EnableFallbackProviders { get; set; } = true;
        
        public int MaxRetryAttempts { get; set; } = 3;
        
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
        
        public bool EnableCaching { get; set; } = true;
        
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(24);
        
        public int MaxCharactersPerRequest { get; set; } = 5000;
        
        public int MaxBatchSize { get; set; } = 100;
    }

    public class TranslationProviderConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        
        public string? Endpoint { get; set; }
        
        public string? Region { get; set; }
        
        public bool IsEnabled { get; set; } = true;
        
        public int Priority { get; set; } = 1; // 1 = highest priority
        
        public List<string> SupportedLanguages { get; set; } = new();
        
        public decimal CostPerCharacter { get; set; }
        
        public int RateLimitPerMinute { get; set; } = 1000;
        
        public Dictionary<string, object>? AdditionalSettings { get; set; }
    }

    public class TranslationStatistics
    {
        public string UserId { get; set; } = string.Empty;
        
        public int TotalTranslations { get; set; }
        
        public int TotalCharacters { get; set; }
        
        public Dictionary<string, int> LanguagePairs { get; set; } = new();
        
        public Dictionary<TranslationProvider, int> ProviderUsage { get; set; } = new();
        
        public decimal TotalCost { get; set; }
        
        public DateTime FirstTranslation { get; set; }
        
        public DateTime LastTranslation { get; set; }
        
        public float AverageProcessingTime { get; set; }
        
        public int ErrorCount { get; set; }
        
        public float SuccessRate { get; set; }
    }
}