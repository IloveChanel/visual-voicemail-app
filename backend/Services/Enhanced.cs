using Google.Cloud.Speech.V1;
using Google.Cloud.Translate.V3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using VisualVoicemailPro.Models;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// Enhanced Speech Service for Visual Voicemail Pro
    /// Supports multi-language transcription with subscription-based features
    /// </summary>
    public class EnhancedSpeechService
    {
        private readonly SpeechClient speechClient;
        private readonly ILogger<EnhancedSpeechService> logger;
        private readonly IConfiguration configuration;

        public EnhancedSpeechService(ILogger<EnhancedSpeechService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.speechClient = SpeechClient.Create();
        }

        /// <summary>
        /// Transcribes voicemail with subscription-based quality levels
        /// </summary>
        /// <param name="filePath">Path to audio file</param>
        /// <param name="languageCode">Language code (e.g., "en-US")</param>
        /// <param name="subscriptionTier">User's subscription tier for feature access</param>
        /// <returns>Transcription result with confidence and metadata</returns>
        public async Task<TranscriptionResult> TranscribeAsync(string filePath, string languageCode = "en-US", string subscriptionTier = "free")
        {
            try
            {
                logger.LogInformation($"🎙️ Transcribing audio: {filePath} (Language: {languageCode}, Tier: {subscriptionTier})");

                var config = new RecognitionConfig
                {
                    LanguageCode = languageCode,
                    EnableAutomaticPunctuation = true,
                    UseEnhanced = subscriptionTier != "free", // Enhanced model for paid tiers
                    Model = subscriptionTier == "business" ? "phone_call" : "default"
                };

                // Add advanced features for Pro/Business tiers
                if (subscriptionTier == "pro" || subscriptionTier == "business")
                {
                    config.EnableWordTimeOffsets = true;
                    config.EnableWordConfidence = true;
                    config.EnableSpeakerDiarization = subscriptionTier == "business";
                    
                    // Alternative languages for better accuracy
                    if (languageCode.StartsWith("en"))
                    {
                        config.AlternativeLanguageCodes.Add("en-GB");
                        config.AlternativeLanguageCodes.Add("en-AU");
                    }
                }

                var audio = RecognitionAudio.FromFile(filePath);
                var response = await speechClient.RecognizeAsync(config, audio);

                if (response.Results.Count == 0)
                {
                    logger.LogWarning("⚠️ No transcription results found");
                    return new TranscriptionResult
                    {
                        Success = false,
                        ErrorMessage = "No speech detected in audio file"
                    };
                }

                // Process results based on subscription tier
                var alternatives = response.Results.SelectMany(r => r.Alternatives).ToList();
                var bestAlternative = alternatives.OrderByDescending(a => a.Confidence).FirstOrDefault();

                if (bestAlternative == null)
                {
                    return new TranscriptionResult
                    {
                        Success = false,
                        ErrorMessage = "No valid transcription alternatives found"
                    };
                }

                var result = new TranscriptionResult
                {
                    Success = true,
                    Transcription = bestAlternative.Transcript,
                    Confidence = bestAlternative.Confidence,
                    DetectedLanguage = languageCode,
                    ProcessingTier = subscriptionTier
                };

                // Add word-level timing for Pro/Business (useful for playback sync)
                if (subscriptionTier != "free" && bestAlternative.Words.Count > 0)
                {
                    result.WordTimings = bestAlternative.Words.Select(w => new WordTiming
                    {
                        Word = w.Word,
                        StartTime = w.StartTime?.ToTimeSpan() ?? TimeSpan.Zero,
                        EndTime = w.EndTime?.ToTimeSpan() ?? TimeSpan.Zero,
                        Confidence = w.Confidence
                    }).ToList();
                }

                logger.LogInformation($"✅ Transcription completed: {result.Transcription?.Length} characters, confidence: {result.Confidence:F2}");
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"❌ Transcription failed for {filePath}");
                return new TranscriptionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Detects the language of spoken audio (Pro/Business feature)
        /// </summary>
        public async Task<LanguageDetectionResult> DetectLanguageAsync(string filePath, string subscriptionTier)
        {
            if (subscriptionTier == "free")
            {
                return new LanguageDetectionResult
                {
                    Success = false,
                    ErrorMessage = "Language detection requires Visual Voicemail Pro subscription"
                };
            }

            try
            {
                var config = new RecognitionConfig
                {
                    LanguageCode = "en-US", // Primary
                    AlternativeLanguageCodes = { "es-ES", "fr-FR", "de-DE", "it-IT", "pt-BR", "zh-CN", "ja-JP", "ko-KR" }
                };

                var audio = RecognitionAudio.FromFile(filePath);
                var response = await speechClient.RecognizeAsync(config, audio);

                var bestResult = response.Results
                    .SelectMany(r => r.Alternatives)
                    .OrderByDescending(a => a.Confidence)
                    .FirstOrDefault();

                return new LanguageDetectionResult
                {
                    Success = true,
                    DetectedLanguage = bestResult?.LanguageCode ?? "en-US",
                    Confidence = bestResult?.Confidence ?? 0f
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Language detection failed");
                return new LanguageDetectionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    /// <summary>
    /// Enhanced Translation Service with subscription tiers
    /// </summary>
    public class EnhancedTranslationService
    {
        private readonly TranslationServiceClient translationClient;
        private readonly ILogger<EnhancedTranslationService> logger;
        private readonly string projectId;

        public EnhancedTranslationService(ILogger<EnhancedTranslationService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.translationClient = TranslationServiceClient.Create();
            this.projectId = configuration["GoogleCloud:ProjectId"] ?? "visual-voicemail-pro";
        }

        /// <summary>
        /// Translates text with subscription-based quality and language support
        /// </summary>
        public async Task<TranslationResult> TranslateAsync(string text, string targetLanguageCode, string subscriptionTier, string sourceLanguageCode = "auto")
        {
            if (subscriptionTier == "free")
            {
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = "Translation requires Visual Voicemail Pro subscription"
                };
            }

            try
            {
                logger.LogInformation($"🌍 Translating text to {targetLanguageCode} (Tier: {subscriptionTier})");

                var request = new TranslateTextRequest
                {
                    Contents = { text },
                    TargetLanguageCode = targetLanguageCode,
                    Parent = $"projects/{projectId}/locations/global",
                    MimeType = "text/plain"
                };

                // Auto-detect source language if not specified
                if (sourceLanguageCode != "auto")
                {
                    request.SourceLanguageCode = sourceLanguageCode;
                }

                var response = await translationClient.TranslateTextAsync(request);
                var translation = response.Translations.FirstOrDefault();

                if (translation == null)
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = "No translation result received"
                    };
                }

                logger.LogInformation($"✅ Translation completed: {translation.TranslatedText?.Length} characters");

                return new TranslationResult
                {
                    Success = true,
                    TranslatedText = translation.TranslatedText,
                    DetectedSourceLanguage = translation.DetectedLanguageCode,
                    TargetLanguage = targetLanguageCode,
                    ProcessingTier = subscriptionTier
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"❌ Translation failed for target language {targetLanguageCode}");
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets list of supported languages for translation
        /// </summary>
        public async Task<List<SupportedLanguage>> GetSupportedLanguagesAsync()
        {
            try
            {
                var request = new GetSupportedLanguagesRequest
                {
                    Parent = $"projects/{projectId}/locations/global"
                };

                var response = await translationClient.GetSupportedLanguagesAsync(request);
                
                return response.Languages.Select(lang => new SupportedLanguage
                {
                    Code = lang.LanguageCode,
                    Name = lang.DisplayName
                }).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get supported languages");
                return new List<SupportedLanguage>();
            }
        }
    }

    /// <summary>
    /// Enhanced Spam Detection Service with AI and subscription tiers
    /// </summary>
    public class EnhancedSpamService
    {
        private readonly ILogger<EnhancedSpamService> logger;
        private readonly HashSet<string> knownSpamNumbers;
        private readonly List<string> spamKeywords;
        
        public EnhancedSpamService(ILogger<EnhancedSpamService> logger)
        {
            this.logger = logger;
            this.knownSpamNumbers = LoadKnownSpamNumbers();
            this.spamKeywords = LoadSpamKeywords();
        }

        /// <summary>
        /// Analyzes voicemail for spam with subscription-based accuracy
        /// </summary>
        public async Task<SpamAnalysisResult> AnalyzeSpamAsync(string callerNumber, string transcription, string subscriptionTier)
        {
            try
            {
                logger.LogInformation($"🛡️ Analyzing spam for {callerNumber} (Tier: {subscriptionTier})");

                var result = new SpamAnalysisResult
                {
                    CallerNumber = callerNumber,
                    AnalyzedText = transcription,
                    ProcessingTier = subscriptionTier
                };

                float confidence = 0f;
                var reasons = new List<string>();

                // Basic spam detection (all tiers)
                if (IsKnownSpamNumber(callerNumber))
                {
                    confidence += 0.9f;
                    reasons.Add("Known spam number");
                }

                // Enhanced analysis for Pro/Business tiers
                if (subscriptionTier == "pro" || subscriptionTier == "business")
                {
                    // Keyword analysis
                    var keywordMatches = CountSpamKeywords(transcription);
                    if (keywordMatches > 0)
                    {
                        confidence += Math.Min(keywordMatches * 0.15f, 0.6f);
                        reasons.Add($"Contains {keywordMatches} spam indicators");
                    }

                    // Pattern analysis
                    if (HasRobocallPatterns(transcription))
                    {
                        confidence += 0.3f;
                        reasons.Add("Robocall patterns detected");
                    }

                    if (HasUrgencyTactics(transcription))
                    {
                        confidence += 0.2f;
                        reasons.Add("Urgency tactics detected");
                    }

                    // Business tier: Advanced ML analysis
                    if (subscriptionTier == "business")
                    {
                        var mlScore = await AdvancedMLAnalysis(transcription);
                        confidence += mlScore * 0.4f;
                        if (mlScore > 0.5f)
                        {
                            reasons.Add($"ML model confidence: {mlScore:F2}");
                        }
                    }
                }

                result.IsSpam = confidence > 0.5f;
                result.Confidence = Math.Min(confidence, 1.0f);
                result.Reasons = reasons;

                logger.LogInformation($"🎯 Spam analysis complete: {result.IsSpam} (confidence: {result.Confidence:F2})");
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Spam analysis failed");
                return new SpamAnalysisResult
                {
                    CallerNumber = callerNumber,
                    IsSpam = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public void AddBlockedNumber(string phoneNumber)
        {
            knownSpamNumbers.Add(phoneNumber);
            logger.LogInformation($"📞 Added {phoneNumber} to blocked list");
        }

        public bool IsSpam(string phoneNumber) => knownSpamNumbers.Contains(phoneNumber);

        private bool IsKnownSpamNumber(string number) => knownSpamNumbers.Contains(number);

        private int CountSpamKeywords(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var lowerText = text.ToLower();
            return spamKeywords.Count(keyword => lowerText.Contains(keyword));
        }

        private bool HasRobocallPatterns(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var lowerText = text.ToLower();
            
            var patterns = new[] { "press", "dial", "extension", "representative", "operator" };
            return patterns.Any(p => lowerText.Contains(p)) && 
                   (lowerText.Contains("number") || lowerText.Contains("key"));
        }

        private bool HasUrgencyTactics(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var lowerText = text.ToLower();
            
            var urgencyWords = new[] { "urgent", "expires", "limited time", "act now", "last chance", "final notice" };
            return urgencyWords.Any(w => lowerText.Contains(w));
        }

        private async Task<float> AdvancedMLAnalysis(string text)
        {
            // Placeholder for advanced ML model
            // In production, this would call a trained ML model
            await Task.Delay(100); // Simulate API call
            
            // Simple heuristic for demo
            var spamScore = 0f;
            if (text.Contains("congratulations", StringComparison.OrdinalIgnoreCase)) spamScore += 0.3f;
            if (text.Contains("winner", StringComparison.OrdinalIgnoreCase)) spamScore += 0.4f;
            if (text.Contains("free", StringComparison.OrdinalIgnoreCase)) spamScore += 0.2f;
            
            return Math.Min(spamScore, 1.0f);
        }

        private HashSet<string> LoadKnownSpamNumbers()
        {
            // In production, load from database or external API
            return new HashSet<string>
            {
                "+15551234567",
                "+18005551234",
                "+12345678900"
            };
        }

        private List<string> LoadSpamKeywords()
        {
            return new List<string>
            {
                "congratulations", "winner", "prize", "lottery", "free", "offer",
                "limited time", "act now", "warranty", "extended warranty",
                "credit", "loan", "debt", "foreclosure", "refinance",
                "medicare", "insurance", "social security", "irs", "tax",
                "car warranty", "student loan", "credit card"
            };
        }
    }

    #region Result Classes

    public class TranscriptionResult
    {
        public bool Success { get; set; }
        public string? Transcription { get; set; }
        public float Confidence { get; set; }
        public string? DetectedLanguage { get; set; }
        public string ProcessingTier { get; set; } = "free";
        public List<WordTiming> WordTimings { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class WordTiming
    {
        public string Word { get; set; } = "";
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public float Confidence { get; set; }
    }

    public class LanguageDetectionResult
    {
        public bool Success { get; set; }
        public string DetectedLanguage { get; set; } = "en-US";
        public float Confidence { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class TranslationResult
    {
        public bool Success { get; set; }
        public string? TranslatedText { get; set; }
        public string? DetectedSourceLanguage { get; set; }
        public string TargetLanguage { get; set; } = "";
        public string ProcessingTier { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }

    public class SupportedLanguage
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class SpamAnalysisResult
    {
        public string CallerNumber { get; set; } = "";
        public string? AnalyzedText { get; set; }
        public bool IsSpam { get; set; }
        public float Confidence { get; set; }
        public List<string> Reasons { get; set; } = new();
        public string ProcessingTier { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }

    #endregion
}