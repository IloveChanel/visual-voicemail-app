using Stripe;
using Stripe.Checkout;
using VisualVoicemailPro.Models;
using VisualVoicemailPro.Data;
using VisualVoicemailPro.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Enhanced Payment Service for Visual Voicemail Pro ($3.49/month)
/// Handles Stripe subscriptions, coupons, whitelist, and payment processing
/// </summary>
public class PaymentService
{
    private const string VISUAL_VOICEMAIL_PRO_PRICE_ID = "price_1234567890"; // Replace with your actual Stripe Price ID
    private const decimal MONTHLY_PRICE = 3.49m;

    private readonly IUserRepository _userRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IWhitelistRepository _whitelistRepository;
    private readonly VisualVoicemailDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IUserRepository userRepository,
        ICouponRepository couponRepository,
        IWhitelistRepository whitelistRepository,
        VisualVoicemailDbContext context,
        ILogger<PaymentService> logger,
        string apiKey)
    {
        _userRepository = userRepository;
        _couponRepository = couponRepository;
        _whitelistRepository = whitelistRepository;
        _context = context;
        _logger = logger;
        StripeConfiguration.ApiKey = apiKey;
    }

    #region Whitelist Management

    /// <summary>
    /// Gets whitelist entry for an email address
    /// </summary>
    public async Task<DeveloperWhitelist?> GetWhitelistEntryAsync(string email)
    {
        return await _whitelistRepository.GetByEmailAsync(email);
    }

    /// <summary>
    /// Grants whitelist access to a user
    /// </summary>
    public async Task GrantWhitelistAccessAsync(string userId, string email, string accessLevel)
    {
        var user = await _userRepository.GetByIdAsync(userId) ?? await _userRepository.GetByEmailAsync(email);
        
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                Email = email,
                PhoneNumber = "", // Will be updated when user logs in
                CreatedAt = DateTime.UtcNow
            };
            user = await _userRepository.CreateAsync(user);
        }

        user.IsWhitelisted = true;
        user.WhitelistedAt = DateTime.UtcNow;
        user.WhitelistReason = accessLevel;
        user.IsPremium = true;
        user.SubscriptionTier = "pro"; // Grant Pro access to whitelisted users

        await _userRepository.UpdateAsync(user);
        _logger.LogInformation($"Granted whitelist access to user {userId} ({email}) with level: {accessLevel}");
    }

    #endregion

    #region Coupon Management

    /// <summary>
    /// Gets coupon by code
    /// </summary>
    public async Task<Coupon?> GetCouponAsync(string code)
    {
        return await _couponRepository.GetByCodeAsync(code);
    }

    /// <summary>
    /// Gets coupon usage count for a specific user
    /// </summary>
    public async Task<int> GetCouponUsageCountAsync(string couponId, string userId)
    {
        return await _context.CouponUsages
            .CountAsync(cu => cu.CouponId == couponId && cu.UserId == userId);
    }

    /// <summary>
    /// Records coupon usage
    /// </summary>
    public async Task RecordCouponUsageAsync(CouponUsageRecord record)
    {
        var usage = new CouponUsage
        {
            Id = Guid.NewGuid().ToString(),
            CouponId = record.CouponId,
            UserId = record.UserId,
            UserEmail = record.UserEmail,
            UsedAt = record.UsedAt,
            DiscountApplied = record.DiscountApplied,
            TrialDaysGranted = record.TrialDaysGranted,
            StripeSessionId = record.StripeSessionId,
            Status = "applied"
        };

        _context.CouponUsages.Add(usage);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Recorded coupon usage: {record.CouponId} for user {record.UserId}");
    }

    /// <summary>
    /// Increments coupon usage count
    /// </summary>
    public async Task IncrementCouponUsageAsync(string couponId)
    {
        await _couponRepository.IncrementUsageAsync(couponId);
    }

    #endregion

    #region Subscription Management

    /// <summary>
    /// Checks if user has existing subscription
    /// </summary>
    public async Task<bool> HasExistingSubscriptionAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        return user?.IsSubscriptionActive == true || !string.IsNullOrEmpty(user?.StripeSubscriptionId);
    }

    /// <summary>
    /// Gets user subscription information
    /// </summary>
    public async Task<User?> GetUserSubscriptionAsync(string userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    /// <summary>
    /// Activates subscription for a user
    /// </summary>
    public async Task ActivateSubscriptionAsync(string userId, string subscriptionId, string tier)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.StripeSubscriptionId = subscriptionId;
            user.SubscriptionTier = tier;
            user.IsSubscriptionActive = true;
            user.SubscriptionStartDate = DateTime.UtcNow;
            user.LastLoginAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);
            _logger.LogInformation($"Activated subscription {subscriptionId} for user {userId}");
        }
    }

    /// <summary>
    /// Updates subscription status
    /// </summary>
    public async Task UpdateSubscriptionStatusAsync(string userId, string status)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.IsSubscriptionActive = status == "active" || status == "trialing";
            await _userRepository.UpdateAsync(user);
            _logger.LogInformation($"Updated subscription status for user {userId}: {status}");
        }
    }

    /// <summary>
    /// Deactivates subscription for a user
    /// </summary>
    public async Task DeactivateSubscriptionAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.IsSubscriptionActive = false;
            user.SubscriptionEndDate = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            _logger.LogInformation($"Deactivated subscription for user {userId}");
        }
    }

    /// <summary>
    /// Records successful payment
    /// </summary>
    public async Task RecordPaymentAsync(string subscriptionId, decimal amount)
    {
        // Find user by subscription ID and update payment records
        var user = await _context.Users.FirstOrDefaultAsync(u => u.StripeSubscriptionId == subscriptionId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow; // Update activity
            await _userRepository.UpdateAsync(user);
            _logger.LogInformation($"Recorded payment of ${amount} for subscription {subscriptionId}");
        }
    }

    /// <summary>
    /// Handles failed payment
    /// </summary>
    public async Task HandleFailedPaymentAsync(string subscriptionId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.StripeSubscriptionId == subscriptionId);
        if (user != null)
        {
            // You might want to send notification, grace period, etc.
            _logger.LogWarning($"Payment failed for subscription {subscriptionId}, user {user.Id}");
        }
    }

    #endregion

    #region Legacy Checkout Methods (for backward compatibility)

    /// <summary>
    /// Creates a Stripe Checkout Session for $3.49/month subscription with 7-day free trial
    /// Legacy method - use StripeIntegrationService.CreateCheckoutSessionAsync for new implementations
    /// </summary>
    /// <param name="customerEmail">Customer's email address</param>
    /// <param name="phoneNumber">Customer's phone number (248-321-9121 format)</param>
    /// <param name="successUrl">URL to redirect after successful payment</param>
    /// <param name="cancelUrl">URL to redirect if payment is cancelled</param>
    /// <returns>Stripe Session object with checkout URL</returns>
    [Obsolete("Use StripeIntegrationService.CreateCheckoutSessionAsync instead")]
    public async Task<Session> CreateCheckoutSessionAsync(string customerEmail, string phoneNumber, string successUrl, string cancelUrl)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "subscription",
            CustomerEmail = customerEmail,
            
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = VISUAL_VOICEMAIL_PRO_PRICE_ID,
                    Quantity = 1,
                }
            },
            
            // 🎁 7-day free trial
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = 7,
                Metadata = new Dictionary<string, string>
                {
                    { "phone_number", phoneNumber },
                    { "plan", "visual_voicemail_pro" },
                    { "price", "$3.49/month" },
                    { "trial_days", "7" }
                }
            },
            
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            
            Metadata = new Dictionary<string, string>
            {
                { "product", "Visual Voicemail Pro" },
                { "customer_phone", phoneNumber },
                { "monthly_price", "3.49" }
            }
        };

        var service = new SessionService();
        return await service.CreateAsync(options);
    }

    /// <summary>
    /// Creates a direct subscription for existing customers
    /// </summary>
    /// <param name="customerId">Stripe Customer ID</param>
    /// <param name="priceId">Stripe Price ID (defaults to Visual Voicemail Pro)</param>
    /// <returns>Created subscription</returns>
    public async Task<Subscription> CreateSubscriptionAsync(string customerId, string priceId = null)
    {
        var options = new SubscriptionCreateOptions
        {
            Customer = customerId,
            Items = new List<SubscriptionItemOptions>
            {
                new SubscriptionItemOptions
                {
                    Price = priceId ?? VISUAL_VOICEMAIL_PRO_PRICE_ID,
                },
            },
            PaymentBehavior = "default_incomplete",
            Expand = new List<string> { "latest_invoice.payment_intent" },
            
            // Add 7-day trial for new subscriptions
            TrialPeriodDays = 7,
            
            Metadata = new Dictionary<string, string>
            {
                { "plan", "visual_voicemail_pro" },
                { "price", "$3.49/month" }
            }
        };

        var service = new SubscriptionService();
        return await service.CreateAsync(options);
    }

    /// <summary>
    /// Retrieves subscription details
    /// </summary>
    /// <param name="subscriptionId">Stripe Subscription ID</param>
    /// <returns>Subscription object with current status</returns>
    public async Task<Subscription> GetSubscriptionAsync(string subscriptionId)
    {
        var service = new SubscriptionService();
        return await service.GetAsync(subscriptionId);
    }

    /// <summary>
    /// Cancels a subscription (customer churns)
    /// </summary>
    /// <param name="subscriptionId">Stripe Subscription ID</param>
    /// <returns>Cancelled subscription</returns>
    public async Task<Subscription> CancelSubscriptionAsync(string subscriptionId)
    {
        var service = new SubscriptionService();
        return await service.CancelAsync(subscriptionId);
    }

    /// <summary>
    /// Updates subscription (e.g., pause, resume, change billing)
    /// </summary>
    /// <param name="subscriptionId">Stripe Subscription ID</param>
    /// <param name="pauseCollection">Whether to pause billing</param>
    /// <returns>Updated subscription</returns>
    public async Task<Subscription> UpdateSubscriptionAsync(string subscriptionId, bool? pauseCollection = null)
    {
        var options = new SubscriptionUpdateOptions();
        
        if (pauseCollection.HasValue)
        {
            if (pauseCollection.Value)
            {
                options.PauseCollection = new SubscriptionPauseCollectionOptions
                {
                    Behavior = "void"
                };
            }
            else
            {
                options.PauseCollection = null; // Resume billing
            }
        }

        var service = new SubscriptionService();
        return await service.UpdateAsync(subscriptionId, options);
    }

    /// <summary>
    /// Creates or retrieves a Stripe customer
    /// </summary>
    /// <param name="email">Customer email</param>
    /// <param name="phoneNumber">Customer phone number</param>
    /// <param name="name">Customer name (optional)</param>
    /// <returns>Stripe Customer object</returns>
    public async Task<Customer> CreateOrGetCustomerAsync(string email, string phoneNumber, string name = null)
    {
        var customerService = new CustomerService();
        
        // Try to find existing customer by email
        var existingCustomers = await customerService.ListAsync(new CustomerListOptions
        {
            Email = email,
            Limit = 1
        });

        if (existingCustomers.Data.Count > 0)
        {
            return existingCustomers.Data[0];
        }

        // Create new customer
        var options = new CustomerCreateOptions
        {
            Email = email,
            Phone = phoneNumber,
            Name = name,
            Metadata = new Dictionary<string, string>
            {
                { "phone_number", phoneNumber },
                { "product", "Visual Voicemail Pro" },
                { "signup_date", DateTime.UtcNow.ToString("yyyy-MM-dd") }
            }
        };

        return await customerService.CreateAsync(options);
    }

    /// <summary>
    /// Processes Stripe webhook events (call from your webhook endpoint)
    /// </summary>
    /// <param name="json">Raw JSON from Stripe webhook</param>
    /// <param name="signature">Stripe signature header</param>
    /// <param name="webhookSecret">Your Stripe webhook secret</param>
    /// <returns>Event processing result</returns>
    public async Task<WebhookProcessingResult> ProcessWebhookAsync(string json, string signature, string webhookSecret)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);

            switch (stripeEvent.Type)
            {
                case Events.CustomerSubscriptionCreated:
                    var newSubscription = stripeEvent.Data.Object as Subscription;
                    // User successfully subscribed to $3.49/month plan
                    return new WebhookProcessingResult
                    {
                        Success = true,
                        EventType = "subscription_created",
                        CustomerId = newSubscription.CustomerId,
                        SubscriptionId = newSubscription.Id,
                        Message = $"New $3.49/month subscription created: {newSubscription.Id}"
                    };

                case Events.CustomerSubscriptionDeleted:
                    var cancelledSub = stripeEvent.Data.Object as Subscription;
                    // User cancelled subscription - downgrade to free tier
                    return new WebhookProcessingResult
                    {
                        Success = true,
                        EventType = "subscription_cancelled",
                        CustomerId = cancelledSub.CustomerId,
                        SubscriptionId = cancelledSub.Id,
                        Message = $"Subscription cancelled: {cancelledSub.Id}"
                    };

                case Events.InvoicePaymentSucceeded:
                    var invoice = stripeEvent.Data.Object as Invoice;
                    // Monthly $3.49 payment successful
                    return new WebhookProcessingResult
                    {
                        Success = true,
                        EventType = "payment_succeeded",
                        CustomerId = invoice.CustomerId,
                        Amount = invoice.AmountPaid / 100m, // Convert from cents
                        Message = $"Payment received: ${invoice.AmountPaid / 100m}"
                    };

                case Events.InvoicePaymentFailed:
                    var failedInvoice = stripeEvent.Data.Object as Invoice;
                    // Payment failed - handle gracefully
                    return new WebhookProcessingResult
                    {
                        Success = true,
                        EventType = "payment_failed",
                        CustomerId = failedInvoice.CustomerId,
                        Message = $"Payment failed for customer: {failedInvoice.CustomerId}"
                    };

                default:
                    return new WebhookProcessingResult
                    {
                        Success = true,
                        EventType = "unhandled",
                        Message = $"Unhandled event type: {stripeEvent.Type}"
                    };
            }
        }
        catch (Exception ex)
        {
            return new WebhookProcessingResult
            {
                Success = false,
                Message = $"Webhook processing failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets revenue analytics for business dashboard
    /// </summary>
    /// <param name="startDate">Start date for analytics</param>
    /// <param name="endDate">End date for analytics</param>
    /// <returns>Revenue analytics object</returns>
    public async Task<RevenueAnalytics> GetRevenueAnalyticsAsync(DateTime startDate, DateTime endDate)
    {
        var subscriptionService = new SubscriptionService();
        var invoiceService = new InvoiceService();

        // Get active subscriptions
        var activeSubscriptions = await subscriptionService.ListAsync(new SubscriptionListOptions
        {
            Status = "active",
            Created = new DateRangeOptions
            {
                GreaterThanOrEqual = startDate,
                LessThanOrEqual = endDate
            }
        });

        // Get successful payments in date range
        var successfulInvoices = await invoiceService.ListAsync(new InvoiceListOptions
        {
            Status = "paid",
            Created = new DateRangeOptions
            {
                GreaterThanOrEqual = startDate,
                LessThanOrEqual = endDate
            }
        });

        var totalRevenue = successfulInvoices.Data.Sum(i => i.AmountPaid) / 100m;
        var activeSubscriberCount = activeSubscriptions.Data.Count;

        return new RevenueAnalytics
        {
            TotalRevenue = totalRevenue,
            ActiveSubscribers = activeSubscriberCount,
            MonthlyRecurringRevenue = activeSubscriberCount * MONTHLY_PRICE,
            AverageRevenuePerUser = activeSubscriberCount > 0 ? totalRevenue / activeSubscriberCount : 0,
            PeriodStart = startDate,
            PeriodEnd = endDate
        };
    }

    #endregion
}

/// <summary>
/// Result object for webhook processing
/// </summary>
public class WebhookProcessingResult
{
    public bool Success { get; set; }
    public string EventType { get; set; }
    public string CustomerId { get; set; }
    public string SubscriptionId { get; set; }
    public decimal? Amount { get; set; }
    public string Message { get; set; }
}

/// <summary>
/// Revenue analytics for business dashboard
/// </summary>
public class RevenueAnalytics
{
    public decimal TotalRevenue { get; set; }
    public int ActiveSubscribers { get; set; }
    public decimal MonthlyRecurringRevenue { get; set; }
    public decimal AverageRevenuePerUser { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}