using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using System.Text.Json;
using System.Text;

/// <summary>
/// Production-ready Stripe Integration for Visual Voicemail Pro
/// Handles subscriptions, webhooks, and payment processing for $3.49/month service
/// </summary>
public class StripeIntegrationService
{
    private readonly ILogger<StripeIntegrationService> logger;
    private readonly IConfiguration configuration;
    private readonly PaymentService paymentService;
    private readonly string stripeWebhookSecret;

    // Visual Voicemail Pro pricing structure
    public static readonly Dictionary<string, PricingTier> PricingTiers = new()
    {
        {
            "free",
            new PricingTier
            {
                Name = "Free",
                PriceId = "",
                MonthlyPrice = 0,
                Features = new[] { "Basic transcription", "5 voicemails/month", "Standard spam detection" }
            }
        },
        {
            "pro",
            new PricingTier
            {
                Name = "Visual Voicemail Pro",
                PriceId = "price_1QBqwKLkdIwHu7ixeQ4Y8Z9X", // Replace with actual Stripe Price ID
                MonthlyPrice = 3.49m,
                Features = new[]
                {
                    "Unlimited transcription",
                    "Multi-language support (9 languages)",
                    "Advanced spam detection",
                    "Real-time translation",
                    "Smart categorization",
                    "Priority support",
                    "7-day free trial"
                }
            }
        },
        {
            "business",
            new PricingTier
            {
                Name = "Business Pro",
                PriceId = "price_1QBqwLLkdIwHu7ixaB3C4D5E", // Replace with actual Stripe Price ID
                MonthlyPrice = 9.99m,
                Features = new[]
                {
                    "Everything in Pro",
                    "Team management",
                    "Analytics dashboard",
                    "API access",
                    "Priority processing",
                    "Custom integrations"
                }
            }
        }
    };

    public StripeIntegrationService(ILogger<StripeIntegrationService> logger, IConfiguration configuration, PaymentService paymentService)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.paymentService = paymentService;
        this.stripeWebhookSecret = configuration["Stripe:WebhookSecret"] ?? "";
        
        // Initialize Stripe with production or test keys
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"] ?? 
            Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? 
            "sk_test_51234567890"; // Replace with your test key
    }

    /// <summary>
    /// Creates a Stripe Checkout session for Visual Voicemail Pro subscription
    /// Supports coupons, whitelist bypass, and flexible trial periods
    /// </summary>
    /// <param name="request">Checkout request with user details and optional coupon</param>
    /// <returns>Checkout session with payment link or whitelist bypass</returns>
    public async Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(CheckoutRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrEmpty(request.Email) || (!PricingTiers.ContainsKey(request.Tier) || request.Tier == "free"))
            {
                throw new ArgumentException($"Invalid request: email={request.Email}, tier={request.Tier}");
            }

            // Check if user is whitelisted for free access
            var whitelistResult = await CheckWhitelistStatusAsync(request.Email);
            if (whitelistResult.IsWhitelisted)
            {
                // Grant immediate premium access for whitelisted users
                await paymentService.GrantWhitelistAccessAsync(request.UserId, request.Email, whitelistResult.AccessLevel);
                
                logger.LogInformation($"Whitelisted user {request.Email} granted free access - Level: {whitelistResult.AccessLevel}");
                
                return new CheckoutSessionResponse
                {
                    Success = true,
                    IsWhitelisted = true,
                    Message = $"Welcome! You have been granted {whitelistResult.AccessLevel} access to Visual Voicemail Pro.",
                    AccessLevel = whitelistResult.AccessLevel,
                    SessionId = "",
                    PaymentUrl = ""
                };
            }

            // Validate and process coupon if provided
            CouponValidationResult? couponResult = null;
            if (!string.IsNullOrEmpty(request.CouponCode))
            {
                couponResult = await ValidateAndProcessCouponAsync(request.CouponCode, request.Email, request.UserId);
                if (!couponResult.IsValid)
                {
                    return new CheckoutSessionResponse
                    {
                        Success = false,
                        ErrorMessage = couponResult.ErrorMessage
                    };
                }
            }

            var pricingTier = PricingTiers[request.Tier];
            
            // Create or retrieve Stripe customer
            var customer = await CreateOrRetrieveCustomerAsync(request.Email, request.PhoneNumber, request.UserId);

            // Configure checkout session with coupon and trial support
            var options = new SessionCreateOptions
            {
                Customer = customer.Id,
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "subscription",
                
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = pricingTier.PriceId,
                        Quantity = 1
                    }
                },
                
                // Configure trial period (base + coupon bonus)
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    TrialPeriodDays = CalculateTrialPeriod(request.Tier, couponResult),
                    Metadata = new Dictionary<string, string>
                    {
                        ["user_id"] = request.UserId,
                        ["phone_number"] = request.PhoneNumber,
                        ["tier"] = request.Tier,
                        ["coupon_code"] = request.CouponCode ?? "",
                        ["coupon_discount"] = couponResult?.DiscountApplied.ToString() ?? "0",
                        ["trial_days_granted"] = couponResult?.TrialDaysGranted.ToString() ?? "0",
                        ["created_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                },
                
                // Success/Cancel URLs
                SuccessUrl = $"{configuration["App:BaseUrl"]}/subscription/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{configuration["App:BaseUrl"]}/subscription/cancel",
                
                // Additional options
                AllowPromotionCodes = true,
                BillingAddressCollection = "auto",
                
                Metadata = new Dictionary<string, string>
                {
                    ["user_id"] = request.UserId,
                    ["phone_number"] = request.PhoneNumber,
                    ["tier"] = request.Tier,
                    ["coupon_applied"] = couponResult?.CouponCode ?? ""
                }
            };

            // Apply coupon discounts if valid
            if (couponResult?.IsValid == true && !string.IsNullOrEmpty(couponResult.StripeCouponId))
            {
                options.Discounts = new List<SessionDiscountOptions>
                {
                    new SessionDiscountOptions 
                    { 
                        Coupon = couponResult.StripeCouponId 
                    }
                };
            }

            var service = new SessionService();
            var session = await service.CreateAsync(options);
            
            // Record coupon usage if applied
            if (couponResult?.IsValid == true)
            {
                await RecordCouponUsageAsync(couponResult.CouponId, request.UserId, request.Email, session.Id, couponResult.DiscountApplied, couponResult.TrialDaysGranted);
            }
            
            logger.LogInformation($"Created checkout session {session.Id} for user {request.UserId}, tier: {request.Tier}, coupon: {request.CouponCode}");

            return new CheckoutSessionResponse
            {
                SessionId = session.Id,
                PaymentUrl = session.Url,
                CustomerId = customer.Id,
                Tier = request.Tier,
                MonthlyPrice = pricingTier.MonthlyPrice,
                DiscountApplied = couponResult?.DiscountApplied ?? 0,
                TrialDays = CalculateTrialPeriod(request.Tier, couponResult),
                CouponCode = couponResult?.CouponCode,
                Success = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to create checkout session for user {request.UserId}");
            return new CheckoutSessionResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Handles Stripe webhook events for subscription lifecycle management
    /// </summary>
    /// <param name="requestBody">Raw webhook request body</param>
    /// <param name="stripeSignature">Stripe signature header</param>
    /// <returns>Processing result</returns>
    public async Task<WebhookProcessingResult> ProcessWebhookAsync(string requestBody, string stripeSignature)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                requestBody,
                stripeSignature,
                stripeWebhookSecret
            );

            logger.LogInformation($"Processing Stripe webhook: {stripeEvent.Type} - {stripeEvent.Id}");

            switch (stripeEvent.Type)
            {
                case Events.CheckoutSessionCompleted:
                    await HandleCheckoutSessionCompleted(stripeEvent);
                    break;

                case Events.InvoicePaymentSucceeded:
                    await HandleInvoicePaymentSucceeded(stripeEvent);
                    break;

                case Events.InvoicePaymentFailed:
                    await HandleInvoicePaymentFailed(stripeEvent);
                    break;

                case Events.CustomerSubscriptionUpdated:
                    await HandleSubscriptionUpdated(stripeEvent);
                    break;

                case Events.CustomerSubscriptionDeleted:
                    await HandleSubscriptionCanceled(stripeEvent);
                    break;

                case Events.CustomerSubscriptionTrialWillEnd:
                    await HandleTrialWillEnd(stripeEvent);
                    break;

                default:
                    logger.LogInformation($"Unhandled webhook event type: {stripeEvent.Type}");
                    break;
            }

            return new WebhookProcessingResult
            {
                Success = true,
                EventType = stripeEvent.Type,
                EventId = stripeEvent.Id
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to process Stripe webhook");
            return new WebhookProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Retrieves subscription status and details for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Current subscription information</returns>
    public async Task<SubscriptionStatus> GetSubscriptionStatusAsync(string userId)
    {
        try
        {
            // Get user subscription from your database first
            var userSubscription = await paymentService.GetUserSubscriptionAsync(userId);
            
            if (string.IsNullOrEmpty(userSubscription?.StripeSubscriptionId))
            {
                return new SubscriptionStatus
                {
                    IsActive = false,
                    Tier = "free",
                    Features = PricingTiers["free"].Features
                };
            }

            // Get latest status from Stripe
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(userSubscription.StripeSubscriptionId);

            var tier = subscription.Metadata.TryGetValue("tier", out var tierValue) ? tierValue : "pro";
            var isActive = subscription.Status == "active" || subscription.Status == "trialing";

            return new SubscriptionStatus
            {
                IsActive = isActive,
                Tier = tier,
                Status = subscription.Status,
                CurrentPeriodStart = subscription.CurrentPeriodStart,
                CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                TrialEnd = subscription.TrialEnd,
                MonthlyPrice = PricingTiers.ContainsKey(tier) ? PricingTiers[tier].MonthlyPrice : 0,
                Features = PricingTiers.ContainsKey(tier) ? PricingTiers[tier].Features : new string[0],
                CancelAtPeriodEnd = subscription.CancelAtPeriodEnd
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to get subscription status for user {userId}");
            return new SubscriptionStatus
            {
                IsActive = false,
                Tier = "free",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Cancels a user's subscription (at period end)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Cancellation result</returns>
    public async Task<CancellationResult> CancelSubscriptionAsync(string userId)
    {
        try
        {
            var userSubscription = await paymentService.GetUserSubscriptionAsync(userId);
            
            if (string.IsNullOrEmpty(userSubscription?.StripeSubscriptionId))
            {
                return new CancellationResult
                {
                    Success = false,
                    ErrorMessage = "No active subscription found"
                };
            }

            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.UpdateAsync(userSubscription.StripeSubscriptionId, 
                new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true
                });

            logger.LogInformation($"Scheduled subscription cancellation for user {userId} at period end: {subscription.CurrentPeriodEnd}");

            return new CancellationResult
            {
                Success = true,
                CancelAtPeriodEnd = subscription.CurrentPeriodEnd,
                AccessUntil = subscription.CurrentPeriodEnd
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to cancel subscription for user {userId}");
            return new CancellationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #region Webhook Event Handlers

    private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session?.Mode != "subscription") return;

        var userId = session.Metadata?.GetValueOrDefault("user_id");
        var phoneNumber = session.Metadata?.GetValueOrDefault("phone_number");
        var tier = session.Metadata?.GetValueOrDefault("tier");

        if (!string.IsNullOrEmpty(userId))
        {
            await paymentService.ActivateSubscriptionAsync(userId, session.SubscriptionId, tier);
            logger.LogInformation($"Activated subscription for user {userId}, session: {session.Id}");
        }
    }

    private async Task HandleInvoicePaymentSucceeded(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice?.SubscriptionId != null)
        {
            await paymentService.RecordPaymentAsync(invoice.SubscriptionId, invoice.AmountPaid / 100.0m);
            logger.LogInformation($"Recorded successful payment for subscription {invoice.SubscriptionId}");
        }
    }

    private async Task HandleInvoicePaymentFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice?.SubscriptionId != null)
        {
            await paymentService.HandleFailedPaymentAsync(invoice.SubscriptionId);
            logger.LogWarning($"Payment failed for subscription {invoice.SubscriptionId}");
        }
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription != null)
        {
            var userId = subscription.Metadata?.GetValueOrDefault("user_id");
            if (!string.IsNullOrEmpty(userId))
            {
                await paymentService.UpdateSubscriptionStatusAsync(userId, subscription.Status);
                logger.LogInformation($"Updated subscription status for user {userId}: {subscription.Status}");
            }
        }
    }

    private async Task HandleSubscriptionCanceled(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription != null)
        {
            var userId = subscription.Metadata?.GetValueOrDefault("user_id");
            if (!string.IsNullOrEmpty(userId))
            {
                await paymentService.DeactivateSubscriptionAsync(userId);
                logger.LogInformation($"Deactivated subscription for user {userId}");
            }
        }
    }

    private async Task HandleTrialWillEnd(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription != null)
        {
            var userId = subscription.Metadata?.GetValueOrDefault("user_id");
            var phoneNumber = subscription.Metadata?.GetValueOrDefault("phone_number");
            
            if (!string.IsNullOrEmpty(userId))
            {
                // Send trial ending notification (implement your notification service)
                logger.LogInformation($"Trial ending soon for user {userId}, phone: {phoneNumber}");
            }
        }
    }

    #region Helper Methods

    /// <summary>
    /// Checks if email is on the developer whitelist
    /// </summary>
    private async Task<WhitelistResult> CheckWhitelistStatusAsync(string email)
    {
        try
        {
            // Check if user is in developer whitelist
            var whitelist = await paymentService.GetWhitelistEntryAsync(email);
            
            if (whitelist != null && whitelist.IsActive && !whitelist.IsExpired)
            {
                return new WhitelistResult
                {
                    IsWhitelisted = true,
                    AccessLevel = whitelist.AccessLevel,
                    Role = whitelist.Role,
                    ExpiresAt = whitelist.ExpiresAt
                };
            }

            return new WhitelistResult { IsWhitelisted = false };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to check whitelist status for {email}");
            return new WhitelistResult { IsWhitelisted = false };
        }
    }

    /// <summary>
    /// Validates and processes coupon code
    /// </summary>
    private async Task<CouponValidationResult> ValidateAndProcessCouponAsync(string couponCode, string email, string userId)
    {
        try
        {
            var coupon = await paymentService.GetCouponAsync(couponCode);
            
            if (coupon == null)
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Coupon code not found"
                };
            }

            if (!coupon.IsValid)
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = coupon.IsExpired ? "Coupon has expired" : 
                                  coupon.IsExhausted ? "Coupon usage limit reached" : 
                                  "Coupon is not active"
                };
            }

            // Check user eligibility
            if (coupon.AllowedEmails.Any() && !coupon.AllowedEmails.Contains(email))
            {
                return new CouponValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "This coupon is not available for your email address"
                };
            }

            if (coupon.RequiredDomains.Any())
            {
                var emailDomain = "@" + email.Split('@')[1];
                if (!coupon.RequiredDomains.Contains(emailDomain))
                {
                    return new CouponValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "This coupon is only available for specific email domains"
                    };
                }
            }

            // Check if user already used this coupon
            if (coupon.MaxUsagesPerUser.HasValue)
            {
                var userUsageCount = await paymentService.GetCouponUsageCountAsync(coupon.Id, userId);
                if (userUsageCount >= coupon.MaxUsagesPerUser.Value)
                {
                    return new CouponValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "You have already used this coupon"
                    };
                }
            }

            // Check if this is first-time user only coupon
            if (coupon.IsFirstTimeOnly)
            {
                var hasExistingSubscription = await paymentService.HasExistingSubscriptionAsync(userId);
                if (hasExistingSubscription)
                {
                    return new CouponValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "This coupon is only available for first-time subscribers"
                    };
                }
            }

            // Calculate discount amount
            decimal discountApplied = 0;
            if (coupon.DiscountType == "percentage")
            {
                var basePrice = PricingTiers.ContainsKey("pro") ? PricingTiers["pro"].MonthlyPrice : 3.49m;
                discountApplied = basePrice * (coupon.DiscountPercentage / 100);
            }
            else if (coupon.DiscountType == "amount")
            {
                discountApplied = coupon.DiscountAmount;
            }

            return new CouponValidationResult
            {
                IsValid = true,
                CouponId = coupon.Id,
                CouponCode = coupon.Code,
                DiscountApplied = discountApplied,
                TrialDaysGranted = coupon.FreeTrialDays,
                StripeCouponId = coupon.StripeCouponId
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to validate coupon {couponCode}");
            return new CouponValidationResult
            {
                IsValid = false,
                ErrorMessage = "Failed to validate coupon code"
            };
        }
    }

    /// <summary>
    /// Creates or retrieves Stripe customer
    /// </summary>
    private async Task<Customer> CreateOrRetrieveCustomerAsync(string email, string phoneNumber, string userId)
    {
        var customerService = new CustomerService();
        var customers = await customerService.ListAsync(new CustomerListOptions
        {
            Email = email,
            Limit = 1
        });

        Customer customer;
        if (customers.Data.Count > 0)
        {
            customer = customers.Data[0];
            logger.LogInformation($"Found existing Stripe customer: {customer.Id} for email: {email}");
        }
        else
        {
            customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = email,
                Phone = phoneNumber,
                Metadata = new Dictionary<string, string>
                {
                    ["user_id"] = userId,
                    ["phone_number"] = phoneNumber,
                    ["app_name"] = "Visual Voicemail Pro"
                }
            });
            logger.LogInformation($"Created new Stripe customer: {customer.Id} for email: {email}");
        }

        return customer;
    }

    /// <summary>
    /// Calculates total trial period including base trial and coupon bonus
    /// </summary>
    private int CalculateTrialPeriod(string tier, CouponValidationResult? couponResult)
    {
        var baseTrial = tier == "pro" ? 7 : 0; // Base 7-day trial for Pro
        var couponTrial = couponResult?.TrialDaysGranted ?? 0;
        return baseTrial + couponTrial;
    }

    /// <summary>
    /// Records coupon usage for analytics and fraud prevention
    /// </summary>
    private async Task RecordCouponUsageAsync(string couponId, string userId, string email, string sessionId, decimal discountApplied, int trialDaysGranted)
    {
        try
        {
            await paymentService.RecordCouponUsageAsync(new CouponUsageRecord
            {
                CouponId = couponId,
                UserId = userId,
                UserEmail = email,
                StripeSessionId = sessionId,
                DiscountApplied = discountApplied,
                TrialDaysGranted = trialDaysGranted,
                UsedAt = DateTime.UtcNow
            });

            // Increment coupon usage count
            await paymentService.IncrementCouponUsageAsync(couponId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to record coupon usage for coupon {couponId}");
        }
    }

    #endregion

#region Data Models

public class PricingTier
{
    public string Name { get; set; } = "";
    public string PriceId { get; set; } = "";
    public decimal MonthlyPrice { get; set; }
    public string[] Features { get; set; } = Array.Empty<string>();
}

public class CheckoutRequest
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Tier { get; set; } = "pro";
    public string? CouponCode { get; set; }
}

public class CheckoutSessionResponse
{
    public bool Success { get; set; }
    public string SessionId { get; set; } = "";
    public string PaymentUrl { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string Tier { get; set; } = "";
    public decimal MonthlyPrice { get; set; }
    public decimal DiscountApplied { get; set; } = 0;
    public int TrialDays { get; set; }
    public string? CouponCode { get; set; }
    public string ErrorMessage { get; set; } = "";
    
    // Whitelist-specific properties
    public bool IsWhitelisted { get; set; } = false;
    public string? AccessLevel { get; set; }
    public string? Message { get; set; }
}

public class WhitelistResult
{
    public bool IsWhitelisted { get; set; }
    public string AccessLevel { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
}

public class CouponValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = "";
    public string CouponId { get; set; } = "";
    public string CouponCode { get; set; } = "";
    public decimal DiscountApplied { get; set; } = 0;
    public int TrialDaysGranted { get; set; } = 0;
    public string? StripeCouponId { get; set; }
}

public class CouponUsageRecord
{
    public string CouponId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public string? StripeSessionId { get; set; }
    public decimal DiscountApplied { get; set; }
    public int TrialDaysGranted { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}

public class WebhookProcessingResult
{
    public bool Success { get; set; }
    public string EventType { get; set; } = "";
    public string EventId { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

public class SubscriptionStatus
{
    public bool IsActive { get; set; }
    public string Tier { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? TrialEnd { get; set; }
    public decimal MonthlyPrice { get; set; }
    public string[] Features { get; set; } = Array.Empty<string>();
    public bool CancelAtPeriodEnd { get; set; }
    public string ErrorMessage { get; set; } = "";
}

public class CancellationResult
{
    public bool Success { get; set; }
    public DateTime? CancelAtPeriodEnd { get; set; }
    public DateTime? AccessUntil { get; set; }
    public string ErrorMessage { get; set; } = "";
}

#endregion