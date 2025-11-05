using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VisualVoicemailPro.Models
{
    /// <summary>
    /// Enhanced Voicemail model for Visual Voicemail Pro
    /// Supports advanced features like multi-language, spam detection, and categorization
    /// </summary>
    public class Voicemail
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; } = "";

        [Required]
        public string CallerNumber { get; set; } = "";

        public string CallerName { get; set; } = "";

        [Required]
        public string FilePath { get; set; } = "";

        public string FileUrl { get; set; } = "";

        public int DurationSeconds { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }

        public bool IsRead => ReadAt.HasValue;

        // Enhanced transcription features
        public string? Transcription { get; set; }

        public string? TranslatedText { get; set; }

        public string DetectedLanguage { get; set; } = "en-US";

        public string? Summary { get; set; }

        // Spam detection
        public bool IsSpam { get; set; }

        public float SpamConfidence { get; set; }

        public List<string> SpamReasons { get; set; } = new();

        // Content analysis
        public string Sentiment { get; set; } = "neutral"; // positive, negative, neutral, urgent

        public string Category { get; set; } = "general"; // appointment, delivery, billing, support, personal, business

        public string Priority { get; set; } = "low"; // high, medium, low

        // Processing status
        public string ProcessingStatus { get; set; } = "pending"; // pending, processing, completed, failed

        public DateTime? ProcessedAt { get; set; }

        public string? ProcessingError { get; set; }

        // User actions
        public bool IsFavorite { get; set; }

        public bool IsArchived { get; set; }

        public List<string> Tags { get; set; } = new();

        public string? UserNotes { get; set; }

        // Subscription tier when processed
        public string ProcessedWithTier { get; set; } = "free"; // free, pro, business
    }

    /// <summary>
    /// User model with subscription information, whitelist, and coupon support
    /// </summary>
    public class User
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string PhoneNumber { get; set; } = "";

        public string? DisplayName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

        // Developer whitelist for free no-code access
        public bool IsWhitelisted { get; set; } = false;

        public string? WhitelistReason { get; set; } // Developer, Tester, VIP, etc.

        public DateTime? WhitelistedAt { get; set; }

        public string? WhitelistedBy { get; set; } // Admin who added to whitelist

        // Subscription information
        public string SubscriptionTier { get; set; } = "free"; // free, pro, business

        public bool IsSubscriptionActive { get; set; } = false;

        public string? StripeCustomerId { get; set; }

        public string? StripeSubscriptionId { get; set; }

        public DateTime? SubscriptionStartDate { get; set; }

        public DateTime? SubscriptionEndDate { get; set; }

        // Trial and coupon management
        public DateTime? TrialEndDate { get; set; }

        public string? ActiveCouponCode { get; set; }

        public DateTime? CouponAppliedAt { get; set; }

        public decimal CouponDiscountApplied { get; set; } = 0;

        public int ExtendedTrialDays { get; set; } = 0;

        // Computed properties
        public bool IsInTrial => TrialEndDate.HasValue && DateTime.UtcNow < TrialEndDate.Value;

        public bool IsPremium => IsWhitelisted || IsSubscriptionActive || IsInTrial;

        // Preferences
        public string PreferredLanguage { get; set; } = "en";

        public bool EnableSpamDetection { get; set; } = true;

        public bool EnableTranslation { get; set; } = true;

        public bool EnableNotifications { get; set; } = true;

        public List<string> BlockedNumbers { get; set; } = new();

        // Usage tracking for free tier limits
        public int MonthlyVoicemailCount { get; set; }

        public DateTime LastMonthlyReset { get; set; } = DateTime.UtcNow;

        // Feature access based on subscription or whitelist
        public bool CanUseUnlimitedTranscription => IsWhitelisted || (IsSubscriptionActive && (SubscriptionTier == "pro" || SubscriptionTier == "business"));

        public bool CanUseAdvancedSpamDetection => IsWhitelisted || (IsSubscriptionActive && (SubscriptionTier == "pro" || SubscriptionTier == "business"));

        public bool CanUseTranslation => IsWhitelisted || (IsSubscriptionActive && (SubscriptionTier == "pro" || SubscriptionTier == "business"));

        public bool CanUseAnalytics => IsWhitelisted || (IsSubscriptionActive && SubscriptionTier == "business");

        public bool CanUseAdvancedFeatures => IsWhitelisted || IsInTrial || IsSubscriptionActive;

        public int MaxVoicemailsPerMonth => IsWhitelisted ? int.MaxValue : SubscriptionTier switch
        {
            "free" => 5,
            "pro" => int.MaxValue,
            "business" => int.MaxValue,
            _ => 5
        };

        // Access level for display purposes
        public string AccessLevel => IsWhitelisted ? "Developer" : SubscriptionTier switch
        {
            "free" => IsInTrial ? "Free Trial" : "Free",
            "pro" => "Pro",
            "business" => "Business",
            _ => "Free"
        };
    }

    /// <summary>
    /// Coupon model for promotions, trials, and discounts
    /// </summary>
    public class Coupon
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Code { get; set; } = "";

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public bool IsActive { get; set; } = true;

        // Discount settings
        public decimal DiscountPercentage { get; set; } = 0; // 0-100%

        public decimal DiscountAmount { get; set; } = 0; // Fixed amount discount

        public string DiscountType { get; set; } = "percentage"; // percentage, amount, trial

        // Trial settings
        public int FreeTrialDays { get; set; } = 0;

        public string TrialTier { get; set; } = "pro"; // pro, business

        // Validity and usage limits
        public DateTime? ValidFrom { get; set; }

        public DateTime? ValidUntil { get; set; }

        public int? MaxUsages { get; set; } // null = unlimited

        public int CurrentUsages { get; set; } = 0;

        public int? MaxUsagesPerUser { get; set; } = 1;

        // Targeting and restrictions
        public List<string> AllowedEmails { get; set; } = new(); // Empty = all users

        public List<string> RequiredDomains { get; set; } = new(); // e.g., "@company.com"

        public bool IsFirstTimeOnly { get; set; } = false; // Only for new users

        public string? ApplicableToTier { get; set; } // null = all tiers

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CreatedBy { get; set; } = "";

        public DateTime? LastUsedAt { get; set; }

        // Stripe integration
        public string? StripeCouponId { get; set; } // If created in Stripe

        public string? StripePromotionCodeId { get; set; }

        // Computed properties
        public bool IsValid => IsActive && 
                              (!ValidFrom.HasValue || DateTime.UtcNow >= ValidFrom.Value) &&
                              (!ValidUntil.HasValue || DateTime.UtcNow <= ValidUntil.Value) &&
                              (!MaxUsages.HasValue || CurrentUsages < MaxUsages.Value);

        public bool IsExpired => ValidUntil.HasValue && DateTime.UtcNow > ValidUntil.Value;

        public bool IsExhausted => MaxUsages.HasValue && CurrentUsages >= MaxUsages.Value;

        public string Status => IsExpired ? "Expired" : 
                               IsExhausted ? "Exhausted" : 
                               !IsActive ? "Inactive" : 
                               "Active";
    }

    /// <summary>
    /// Coupon usage tracking for analytics and fraud prevention
    /// </summary>
    public class CouponUsage
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string CouponId { get; set; } = "";

        [Required]
        public string UserId { get; set; } = "";

        public string UserEmail { get; set; } = "";

        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        public decimal DiscountApplied { get; set; }

        public int TrialDaysGranted { get; set; }

        public string? StripeSessionId { get; set; } // If used in Stripe checkout

        public string? OrderId { get; set; }

        public string Status { get; set; } = "applied"; // applied, refunded, disputed

        // Navigation properties
        public virtual Coupon? Coupon { get; set; }
        public virtual User? User { get; set; }
    }

    /// <summary>
    /// Developer whitelist management for free access
    /// </summary>
    public class DeveloperWhitelist
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        public string Name { get; set; } = "";

        public string Role { get; set; } = "developer"; // developer, tester, reviewer, vip

        public string AccessLevel { get; set; } = "full"; // full, limited, readonly

        public bool IsActive { get; set; } = true;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public string AddedBy { get; set; } = "";

        public DateTime? ExpiresAt { get; set; } // null = permanent

        public string? Notes { get; set; }

        public DateTime? LastAccessAt { get; set; }

        // Feature overrides
        public bool CanAccessAdminPanel { get; set; } = false;

        public bool CanCreateCoupons { get; set; } = false;

        public bool CanManageWhitelist { get; set; } = false;

        public bool CanViewAnalytics { get; set; } = true;

        public bool CanBypassLimits { get; set; } = true;

        // Computed properties
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        public string Status => !IsActive ? "Inactive" : 
                               IsExpired ? "Expired" : 
                               "Active";
    }
    }

    /// <summary>
    /// Analytics data for voicemail insights
    /// </summary>
    public class VoicemailAnalytics
    {
        public string UserId { get; set; } = "";

        public DateTime PeriodStart { get; set; }

        public DateTime PeriodEnd { get; set; }

        public int TotalVoicemails { get; set; }

        public int SpamBlocked { get; set; }

        public int UnreadCount { get; set; }

        public Dictionary<string, int> CategoryCounts { get; set; } = new();

        public Dictionary<string, int> SentimentCounts { get; set; } = new();

        public Dictionary<string, int> LanguageCounts { get; set; } = new();

        public List<string> TopCallers { get; set; } = new();

        public TimeSpan AverageResponseTime { get; set; }

        public decimal SpamDetectionAccuracy { get; set; }
    }

    /// <summary>
    /// Subscription usage and billing information
    /// </summary>
    public class SubscriptionUsage
    {
        public string UserId { get; set; } = "";

        public string SubscriptionTier { get; set; } = "";

        public DateTime BillingPeriodStart { get; set; }

        public DateTime BillingPeriodEnd { get; set; }

        public int VoicemailsProcessed { get; set; }

        public int TranscriptionsGenerated { get; set; }

        public int TranslationsPerformed { get; set; }

        public int SpamDetectionRuns { get; set; }

        public decimal CurrentCharges { get; set; }

        public bool HasExceededLimits { get; set; }

        public List<string> FeaturesUsed { get; set; } = new();
    }
}