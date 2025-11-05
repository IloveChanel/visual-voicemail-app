using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VisualVoicemailPro.Models;
using VisualVoicemailPro.Services;

namespace VisualVoicemailPro.Controllers
{
    /// <summary>
    /// Comprehensive translation controller for Visual Voicemail Pro
    /// Supports real-time translation, language detection, and localization management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TranslationController : ControllerBase
    {
        private readonly IMultilingualTranslationService _translationService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<TranslationController> _logger;

        public TranslationController(
            IMultilingualTranslationService translationService,
            ILocalizationService localizationService,
            ILogger<TranslationController> logger)
        {
            _translationService = translationService;
            _localizationService = localizationService;
            _logger = logger;
        }

        /// <summary>
        /// Translate text using the best available provider
        /// </summary>
        [HttpPost("translate")]
        public async Task<ActionResult<TranslationResponse>> TranslateAsync([FromBody] TranslationRequest request)
        {
            try
            {
                _logger.LogInformation($"🌍 Translation request: {request.SourceLanguage} -> {request.TargetLanguage}");

                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return BadRequest(new { error = "Text cannot be empty" });
                }

                if (string.IsNullOrWhiteSpace(request.TargetLanguage))
                {
                    return BadRequest(new { error = "Target language is required" });
                }

                var result = await _translationService.TranslateAsync(request);
                
                if (result.Success)
                {
                    _logger.LogInformation($"✅ Translation successful using {result.UsedProvider}");
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning($"⚠️ Translation failed: {result.ErrorMessage}");
                    return BadRequest(new { error = result.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Translation controller error");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Translate multiple texts in batch
        /// </summary>
        [HttpPost("translate/batch")]
        public async Task<ActionResult<BatchTranslationResponse>> TranslateBatchAsync([FromBody] BatchTranslationRequest request)
        {
            try
            {
                _logger.LogInformation($"🌍 Batch translation request: {request.Texts.Count} texts to {request.TargetLanguage}");

                if (!request.Texts.Any())
                {
                    return BadRequest(new { error = "No texts provided for translation" });
                }

                if (request.Texts.Count > 100) // Limit batch size
                {
                    return BadRequest(new { error = "Batch size cannot exceed 100 texts" });
                }

                var result = await _translationService.TranslateBatchAsync(request);
                
                _logger.LogInformation($"✅ Batch translation completed: {result.Translations.Count(t => t.Success)}/{result.Translations.Count} successful");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Batch translation controller error");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Detect the language of input text
        /// </summary>
        [HttpPost("detect")]
        public async Task<ActionResult<LanguageDetectionResponse>> DetectLanguageAsync([FromBody] LanguageDetectionRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 Language detection request");

                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return BadRequest(new { error = "Text cannot be empty" });
                }

                var result = await _translationService.DetectLanguageAsync(request);
                
                if (result.Success)
                {
                    _logger.LogInformation($"✅ Language detected: {result.DetectedLanguage} (confidence: {result.Confidence:P1})");
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning($"⚠️ Language detection failed: {result.ErrorMessage}");
                    return BadRequest(new { error = result.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Language detection controller error");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get all supported languages
        /// </summary>
        [HttpGet("languages")]
        public async Task<ActionResult<List<SupportedLanguage>>> GetSupportedLanguagesAsync()
        {
            try
            {
                var languages = await _translationService.GetSupportedLanguagesAsync();
                return Ok(languages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting supported languages");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get user translation statistics
        /// </summary>
        [HttpGet("statistics/{userId}")]
        [Authorize]
        public async Task<ActionResult<TranslationStatistics>> GetUserStatisticsAsync(string userId)
        {
            try
            {
                var stats = await _translationService.GetUserStatisticsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting user statistics");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get localized string for UI
        /// </summary>
        [HttpGet("localize/{key}")]
        public async Task<ActionResult<string>> GetLocalizedStringAsync(string key, [FromQuery] string language = "en")
        {
            try
            {
                var localizedString = await _localizationService.GetLocalizedStringAsync(key, language);
                return Ok(new { key, language, value = localizedString });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error getting localized string for key '{key}'");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get all localized resources for a language and category
        /// </summary>
        [HttpGet("resources")]
        public async Task<ActionResult<Dictionary<string, string>>> GetLocalizedResourcesAsync(
            [FromQuery] string language = "en", 
            [FromQuery] string category = "general")
        {
            try
            {
                var resources = await _localizationService.GetLocalizedResourcesAsync(language, category);
                return Ok(resources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error getting localized resources for language '{language}' and category '{category}'");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get available languages for localization
        /// </summary>
        [HttpGet("available-languages")]
        public async Task<ActionResult<List<string>>> GetAvailableLanguagesAsync()
        {
            try
            {
                var languages = await _localizationService.GetAvailableLanguagesAsync();
                return Ok(languages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting available languages");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Admin endpoint: Save localization resource
        /// </summary>
        [HttpPost("admin/localize")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> SaveLocalizationResourceAsync([FromBody] SaveLocalizationRequest request)
        {
            try
            {
                await _localizationService.SaveLocalizationResourceAsync(
                    request.Key, 
                    request.LanguageCode, 
                    request.Value, 
                    request.Category ?? "general");

                return Ok(new { message = "Localization resource saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error saving localization resource");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Enhanced voicemail translation endpoint
        /// Integrates with voicemail transcription for seamless workflow
        /// </summary>
        [HttpPost("voicemail/translate")]
        [Authorize]
        public async Task<ActionResult<VoicemailTranslationResponse>> TranslateVoicemailAsync([FromBody] VoicemailTranslationRequest request)
        {
            try
            {
                _logger.LogInformation($"🎧 Voicemail translation request for voicemail {request.VoicemailId}");

                // Validate voicemail access (user owns this voicemail)
                var voicemail = await GetUserVoicemail(request.VoicemailId, request.UserId);
                if (voicemail == null)
                {
                    return NotFound(new { error = "Voicemail not found or access denied" });
                }

                if (string.IsNullOrEmpty(voicemail.Transcription))
                {
                    return BadRequest(new { error = "Voicemail must be transcribed before translation" });
                }

                // Perform translation
                var translationRequest = new TranslationRequest
                {
                    Text = voicemail.Transcription,
                    TargetLanguage = request.TargetLanguage,
                    SourceLanguage = voicemail.DetectedLanguage,
                    UserId = request.UserId,
                    Context = "voicemail_transcription",
                    UseHighQuality = true // Always use high quality for voicemails
                };

                var translationResult = await _translationService.TranslateAsync(translationRequest);

                if (translationResult.Success)
                {
                    // Update voicemail with translation
                    await UpdateVoicemailTranslation(request.VoicemailId, translationResult.TranslatedText, request.TargetLanguage);

                    var response = new VoicemailTranslationResponse
                    {
                        VoicemailId = request.VoicemailId,
                        OriginalText = voicemail.Transcription,
                        TranslatedText = translationResult.TranslatedText,
                        SourceLanguage = translationResult.DetectedSourceLanguage,
                        TargetLanguage = request.TargetLanguage,
                        Provider = translationResult.UsedProvider.ToString(),
                        Confidence = translationResult.Confidence,
                        ProcessingTime = translationResult.ProcessingTime
                    };

                    _logger.LogInformation($"✅ Voicemail translation completed using {translationResult.UsedProvider}");
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning($"⚠️ Voicemail translation failed: {translationResult.ErrorMessage}");
                    return BadRequest(new { error = translationResult.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Voicemail translation controller error");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        #region Helper Methods

        private async Task<Voicemail?> GetUserVoicemail(string voicemailId, string userId)
        {
            // In a real implementation, this would query the database
            // For now, return a mock voicemail for demonstration
            await Task.Delay(1); // Simulate async operation
            
            return new Voicemail
            {
                Id = voicemailId,
                UserId = userId,
                Transcription = "Hello, this is a test voicemail message.",
                DetectedLanguage = "en"
            };
        }

        private async Task UpdateVoicemailTranslation(string voicemailId, string translatedText, string targetLanguage)
        {
            // In a real implementation, this would update the database
            await Task.Delay(1); // Simulate async operation
            _logger.LogInformation($"📝 Updated voicemail {voicemailId} with translation to {targetLanguage}");
        }

        #endregion
    }

    #region Request/Response Models

    public class SaveLocalizationRequest
    {
        public string Key { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Category { get; set; }
    }

    public class VoicemailTranslationRequest
    {
        public string VoicemailId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
    }

    public class VoicemailTranslationResponse
    {
        public string VoicemailId { get; set; } = string.Empty;
        public string OriginalText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        public string SourceLanguage { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    #endregion
}