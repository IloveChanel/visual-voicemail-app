using System;
using System.Collections.Generic;

namespace VisualVoicemailPro.Models
{
    /// <summary>
    /// Enhanced Voicemail model for Visual Voicemail Pro
    /// Supports advanced features like spam detection, translations, and analytics
    /// </summary>
    public class Voicemail
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public string FilePath { get; set; }
        public string CallerNumber { get; set; }
        public string CallerName { get; set; }
        public DateTime ReceivedAt { get; set; }
        public int DurationSeconds { get; set; }
        
        // Core Features
        public string Transcription { get; set; }
        public string TranslatedText { get; set; }
        public float TranscriptionConfidence { get; set; }
        
        // Premium Features
        public bool IsSpam { get; set; }
        public float SpamConfidence { get; set; }
        public List<string> SpamReasons { get; set; } = new List<string>();
        public string Category { get; set; } // personal, business, spam, unknown
        public string Priority { get; set; } // high, medium, low
        
        // User Interaction
        public bool IsRead { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ReadAt { get; set; }
        
        // Processing Status
        public string ProcessingStatus { get; set; } = "pending"; // pending, processing, completed, failed
        public DateTime? ProcessedAt { get; set; }
        
        // Analytics (Premium)
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        
        // UI Helpers
        public string DisplayCallerInfo => !string.IsNullOrEmpty(CallerName) ? CallerName : CallerNumber;
        public string FormattedDuration => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");
        public string ReceivedTimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - ReceivedAt;
                if (timeSpan.TotalDays >= 1)
                    return $"{(int)timeSpan.TotalDays}d ago";
                if (timeSpan.TotalHours >= 1)
                    return $"{(int)timeSpan.TotalHours}h ago";
                if (timeSpan.TotalMinutes >= 1)
                    return $"{(int)timeSpan.TotalMinutes}m ago";
                return "Just now";
            }
        }
        
        public bool HasTranscription => !string.IsNullOrEmpty(Transcription);
        public bool HasTranslation => !string.IsNullOrEmpty(TranslatedText);
    }

    /// <summary>
    /// User model with subscription information
    /// </summary>
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string DisplayName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Subscription Information
        public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public string StripeCustomerId { get; set; }
        public string StripeSubscriptionId { get; set; }
        
        // Usage Tracking
        public int TranscriptionsThisMonth { get; set; }
        public int TranslationsThisMonth { get; set; }
        public DateTime? LastActiveDate { get; set; }
        
        // Preferences
        public string PreferredLanguage { get; set; } = "en-US";
        public string PreferredTranslationLanguage { get; set; } = "es";
        public bool EnableSpamDetection { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        
        // UI Properties
        public bool IsActive => SubscriptionTier != SubscriptionTier.Free || 
                               (SubscriptionEndDate.HasValue && SubscriptionEndDate > DateTime.Now);
        
        public string SubscriptionStatusText
        {
            get
            {
                return SubscriptionTier switch
                {
                    SubscriptionTier.Free => "Free Plan",
                    SubscriptionTier.Pro => $"Pro Plan (${3.49:F2}/month)",
                    SubscriptionTier.Business => $"Business Plan (${9.99:F2}/month)",
                    _ => "Unknown Plan"
                };
            }
        }
    }

    /// <summary>
    /// Subscription tiers for Visual Voicemail Pro
    /// </summary>
    public enum SubscriptionTier
    {
        Free = 0,
        Pro = 1,
        Business = 2
    }

    /// <summary>
    /// Language support model
    /// </summary>
    public class Language
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Flag { get; set; }
        public bool SupportsSpeechRecognition { get; set; }
        public bool SupportsTranslation { get; set; }
        
        public override string ToString() => DisplayName ?? Name;
    }
}