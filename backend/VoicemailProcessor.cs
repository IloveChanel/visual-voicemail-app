using Google.Cloud.Speech.V1;
using Google.Cloud.Translate.V3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Enhanced Voicemail Processor for Visual Voicemail Pro
/// Handles transcription, translation, spam detection, and audio processing
/// </summary>
public class VoicemailProcessor
{
    private readonly SpeechClient speechClient;
    private readonly TranslationServiceClient translateClient;
    private readonly SpamDetectionService spamService;
    private readonly string googleCloudProjectId;

    // Supported languages for transcription
    private readonly Dictionary<string, string> supportedLanguages = new Dictionary<string, string>
    {
        { "en", "en-US" },
        { "es", "es-ES" },
        { "fr", "fr-FR" },
        { "de", "de-DE" },
        { "it", "it-IT" },
        { "pt", "pt-BR" },
        { "zh", "zh-CN" },
        { "ja", "ja-JP" },
        { "ko", "ko-KR" }
    };

    public VoicemailProcessor(string projectId)
    {
        speechClient = SpeechClient.Create();
        translateClient = TranslationServiceClient.Create();
        spamService = new SpamDetectionService();
        googleCloudProjectId = projectId;
    }

    /// <summary>
    /// Processes a complete voicemail: transcription, translation, spam detection
    /// </summary>
    /// <param name="audioFilePath">Path to the voicemail audio file</param>
    /// <param name="callerNumber">Phone number of the caller</param>
    /// <param name="userPreferredLanguage">User's preferred language for translation</param>
    /// <returns>Complete processed voicemail result</returns>
    public async Task<ProcessedVoicemail> ProcessVoicemailAsync(string audioFilePath, string callerNumber, string userPreferredLanguage = "en")
    {
        var result = new ProcessedVoicemail
        {
            CallerNumber = callerNumber,
            AudioFilePath = audioFilePath,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            // 1. Detect language and transcribe
            var detectedLanguage = await DetectAudioLanguageAsync(audioFilePath);
            result.DetectedLanguage = detectedLanguage;
            
            result.Transcription = await TranscribeAudioAsync(audioFilePath, detectedLanguage);
            
            if (string.IsNullOrEmpty(result.Transcription))
            {
                result.ProcessingStatus = "failed";
                result.ErrorMessage = "Transcription failed - audio may be too short or unclear";
                return result;
            }

            // 2. Translate if needed
            if (detectedLanguage != $"{userPreferredLanguage}-US" && !string.IsNullOrEmpty(result.Transcription))
            {
                result.TranslatedText = await TranslateTextAsync(result.Transcription, userPreferredLanguage);
            }

            // 3. Spam detection
            var spamAnalysis = await spamService.AnalyzeVoicemailAsync(result.Transcription, callerNumber);
            result.IsSpam = spamAnalysis.IsSpam;
            result.SpamConfidence = spamAnalysis.Confidence;
            result.SpamReasons = spamAnalysis.Reasons;

            // 4. Content analysis
            result.Sentiment = AnalyzeSentiment(result.Transcription);
            result.Category = CategorizeVoicemail(result.Transcription);
            result.Priority = CalculatePriority(result.Transcription, result.IsSpam);

            // 5. Generate summary for long voicemails
            if (result.Transcription.Length > 200)
            {
                result.Summary = GenerateSummary(result.Transcription);
            }

            result.ProcessingStatus = "completed";
            return result;
        }
        catch (Exception ex)
        {
            result.ProcessingStatus = "failed";
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Transcribes voicemail audio with enhanced accuracy settings
    /// </summary>
    /// <param name="audioFilePath">Path to audio file</param>
    /// <param name="languageCode">Language code (e.g., "en-US")</param>
    /// <returns>Transcribed text</returns>
    public async Task<string> TranscribeAudioAsync(string audioFilePath, string languageCode)
    {
        try
        {
            var config = new RecognitionConfig
            {
                LanguageCode = languageCode,
                EnableAutomaticPunctuation = true,
                EnableWordTimeOffsets = true,
                EnableWordConfidence = true,
                UseEnhanced = true, // Use enhanced model for better accuracy
                Model = "phone_call", // Optimized for phone call audio
                
                // Alternative language codes for better recognition
                AlternativeLanguageCodes = { GetAlternativeLanguages(languageCode) }
            };

            var audio = RecognitionAudio.FromFile(audioFilePath);
            var response = await speechClient.RecognizeAsync(config, audio);

            if (response.Results.Count == 0)
            {
                // Try with long running recognize for longer audio files
                return await TranscribeLongAudioAsync(audioFilePath, languageCode);
            }

            // Get the best transcript with confidence scoring
            var transcripts = response.Results
                .SelectMany(r => r.Alternatives)
                .Where(a => a.Confidence > 0.7f) // Only high confidence results
                .Select(a => a.Transcript)
                .ToList();

            return string.Join(" ", transcripts);
        }
        catch (Exception ex)
        {
            throw new VoicemailProcessingException($"Transcription failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handles longer voicemail files (over 60 seconds)
    /// </summary>
    private async Task<string> TranscribeLongAudioAsync(string audioFilePath, string languageCode)
    {
        var config = new RecognitionConfig
        {
            LanguageCode = languageCode,
            EnableAutomaticPunctuation = true,
            Model = "phone_call"
        };

        var audio = RecognitionAudio.FromFile(audioFilePath);
        var operation = await speechClient.LongRunningRecognizeAsync(config, audio);
        var response = await operation.PollUntilCompletedAsync();

        return string.Join(" ", response.Result.Results
            .SelectMany(r => r.Alternatives)
            .Where(a => a.Confidence > 0.7f)
            .Select(a => a.Transcript));
    }

    /// <summary>
    /// Detects the language of the audio automatically
    /// </summary>
    private async Task<string> DetectAudioLanguageAsync(string audioFilePath)
    {
        var config = new RecognitionConfig
        {
            LanguageCode = "en-US", // Primary language
            AlternativeLanguageCodes = { "es-ES", "fr-FR", "de-DE", "it-IT" }, // Common alternatives
            Model = "phone_call"
        };

        var audio = RecognitionAudio.FromFile(audioFilePath);
        var response = await speechClient.RecognizeAsync(config, audio);

        // Return the language with the highest confidence
        var bestResult = response.Results
            .SelectMany(r => r.Alternatives)
            .OrderByDescending(a => a.Confidence)
            .FirstOrDefault();

        return bestResult?.LanguageCode ?? "en-US";
    }

    /// <summary>
    /// Translates transcribed text to target language
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="targetLanguageCode">Target language (e.g., "es", "fr")</param>
    /// <returns>Translated text</returns>
    public async Task<string> TranslateTextAsync(string text, string targetLanguageCode)
    {
        try
        {
            var request = new TranslateTextRequest
            {
                Contents = { text },
                TargetLanguageCode = targetLanguageCode,
                Parent = $"projects/{googleCloudProjectId}/locations/global",
                MimeType = "text/plain"
            };

            var response = await translateClient.TranslateTextAsync(request);
            return response.Translations.FirstOrDefault()?.TranslatedText ?? text;
        }
        catch (Exception ex)
        {
            throw new VoicemailProcessingException($"Translation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Analyzes sentiment of the voicemail (positive, negative, neutral)
    /// </summary>
    private string AnalyzeSentiment(string transcription)
    {
        // Simple keyword-based sentiment analysis
        // In production, use Google Cloud Natural Language API
        
        var positiveKeywords = new[] { "thank", "great", "wonderful", "excellent", "happy", "please" };
        var negativeKeywords = new[] { "angry", "upset", "terrible", "horrible", "hate", "complaint", "problem" };
        var urgentKeywords = new[] { "urgent", "emergency", "asap", "immediately", "important" };

        var text = transcription.ToLower();
        
        if (urgentKeywords.Any(k => text.Contains(k))) return "urgent";
        if (negativeKeywords.Any(k => text.Contains(k))) return "negative";
        if (positiveKeywords.Any(k => text.Contains(k))) return "positive";
        
        return "neutral";
    }

    /// <summary>
    /// Categorizes voicemail by content type
    /// </summary>
    private string CategorizeVoicemail(string transcription)
    {
        var text = transcription.ToLower();
        
        if (text.Contains("appointment") || text.Contains("schedule") || text.Contains("meeting"))
            return "appointment";
        if (text.Contains("delivery") || text.Contains("package") || text.Contains("shipping"))
            return "delivery";
        if (text.Contains("payment") || text.Contains("bill") || text.Contains("invoice"))
            return "billing";
        if (text.Contains("support") || text.Contains("help") || text.Contains("technical"))
            return "support";
        if (text.Contains("family") || text.Contains("personal") || text.Contains("friend"))
            return "personal";
        if (text.Contains("business") || text.Contains("work") || text.Contains("office"))
            return "business";
            
        return "general";
    }

    /// <summary>
    /// Calculates priority level (high, medium, low)
    /// </summary>
    private string CalculatePriority(string transcription, bool isSpam)
    {
        if (isSpam) return "low";
        
        var text = transcription.ToLower();
        var urgentKeywords = new[] { "urgent", "emergency", "asap", "immediately", "important", "critical" };
        
        if (urgentKeywords.Any(k => text.Contains(k))) return "high";
        if (text.Contains("appointment") || text.Contains("meeting") || text.Contains("deadline"))
            return "medium";
            
        return "low";
    }

    /// <summary>
    /// Generates a brief summary for long voicemails
    /// </summary>
    private string GenerateSummary(string transcription)
    {
        // Simple extractive summarization
        // In production, use AI summarization service
        
        var sentences = transcription.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (sentences.Length <= 2) return transcription;
        
        // Take first and last sentences as summary
        return $"{sentences[0].Trim()}. {sentences[sentences.Length - 1].Trim()}.";
    }

    /// <summary>
    /// Gets alternative language codes for better recognition
    /// </summary>
    private IEnumerable<string> GetAlternativeLanguages(string primaryLanguage)
    {
        return primaryLanguage switch
        {
            "en-US" => new[] { "en-GB", "en-AU" },
            "es-ES" => new[] { "es-MX", "es-AR" },
            "fr-FR" => new[] { "fr-CA" },
            _ => new string[0]
        };
    }
}

/// <summary>
/// Complete processed voicemail result
/// </summary>
public class ProcessedVoicemail
{
    public string CallerNumber { get; set; }
    public string AudioFilePath { get; set; }
    public string Transcription { get; set; }
    public string TranslatedText { get; set; }
    public string DetectedLanguage { get; set; }
    public bool IsSpam { get; set; }
    public float SpamConfidence { get; set; }
    public List<string> SpamReasons { get; set; } = new();
    public string Sentiment { get; set; } // positive, negative, neutral, urgent
    public string Category { get; set; } // appointment, delivery, billing, etc.
    public string Priority { get; set; } // high, medium, low
    public string Summary { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string ProcessingStatus { get; set; } // completed, failed, processing
    public string ErrorMessage { get; set; }
}

/// <summary>
/// Spam detection service integration
/// </summary>
public class SpamDetectionService
{
    public async Task<SpamAnalysisResult> AnalyzeVoicemailAsync(string transcription, string callerNumber)
    {
        // Enhanced spam detection logic
        var result = new SpamAnalysisResult();
        var reasons = new List<string>();
        float confidence = 0f;

        // 1. Check known spam numbers (you could integrate with Truecaller API here)
        if (IsKnownSpamNumber(callerNumber))
        {
            confidence += 0.8f;
            reasons.Add("Known spam number");
        }

        // 2. Analyze transcription content
        var spamKeywords = new[] 
        {
            "congratulations", "winner", "prize", "lottery", "free", "offer",
            "limited time", "act now", "warranty", "extended warranty",
            "credit", "loan", "debt", "foreclosure", "refinance",
            "medicare", "insurance", "social security", "irs", "tax"
        };

        var text = transcription?.ToLower() ?? "";
        var keywordMatches = spamKeywords.Count(k => text.Contains(k));
        
        if (keywordMatches > 0)
        {
            confidence += keywordMatches * 0.15f;
            reasons.Add($"Contains {keywordMatches} spam keywords");
        }

        // 3. Check for robocall patterns
        if (text.Contains("press") && text.Contains("number") || 
            text.Contains("dial") && text.Contains("extension"))
        {
            confidence += 0.3f;
            reasons.Add("Robocall pattern detected");
        }

        // 4. Check for urgency tactics
        if (text.Contains("urgent") || text.Contains("expires today") || text.Contains("last chance"))
        {
            confidence += 0.2f;
            reasons.Add("Urgency tactics detected");
        }

        result.IsSpam = confidence > 0.5f;
        result.Confidence = Math.Min(confidence, 1.0f);
        result.Reasons = reasons;

        return result;
    }

    private bool IsKnownSpamNumber(string phoneNumber)
    {
        // In production, check against spam database or API
        var knownSpamNumbers = new[]
        {
            "+15551234567", "+18005551234", "+12345678900"
        };
        
        return knownSpamNumbers.Contains(phoneNumber);
    }
}

/// <summary>
/// Spam analysis result
/// </summary>
public class SpamAnalysisResult
{
    public bool IsSpam { get; set; }
    public float Confidence { get; set; }
    public List<string> Reasons { get; set; } = new();
}

/// <summary>
/// Custom exception for voicemail processing errors
/// </summary>
public class VoicemailProcessingException : Exception
{
    public VoicemailProcessingException(string message) : base(message) { }
    public VoicemailProcessingException(string message, Exception innerException) : base(message, innerException) { }
}