using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using VisualVoicemailPro.Models;
using VisualVoicemailPro.Repositories;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace VisualVoicemailPro.Controllers
{
    /// <summary>
    /// User Controller for subscription management with coupon and whitelist support
    /// </summary>
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ICouponRepository _couponRepository;
        private readonly IWhitelistRepository _whitelistRepository;
        private readonly StripeIntegrationService _stripeService;
        private readonly PaymentService _paymentService;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IUserRepository userRepository,
            ICouponRepository couponRepository,
            IWhitelistRepository whitelistRepository,
            StripeIntegrationService stripeService,
            PaymentService paymentService,
            ILogger<UserController> logger)
        {
            _userRepository = userRepository;
            _couponRepository = couponRepository;
            _whitelistRepository = whitelistRepository;
            _stripeService = stripeService;
            _paymentService = paymentService;
            _logger = logger;
        }

        /// <summary>
        /// Creates checkout session with coupon and whitelist support
        /// </summary>
        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                _logger.LogInformation($"Creating checkout session for {request.Email} with coupon: {request.CouponCode}");

                var result = await _stripeService.CreateCheckoutSessionAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                // Handle whitelist bypass
                if (result.IsWhitelisted)
                {
                    return Ok(new
                    {
                        success = true,
                        isWhitelisted = true,
                        message = result.Message,
                        accessLevel = result.AccessLevel,
                        redirectUrl = "/dashboard" // Redirect to app instead of payment
                    });
                }

                // Standard checkout flow
                return Ok(new
                {
                    success = true,
                    sessionId = result.SessionId,
                    paymentUrl = result.PaymentUrl,
                    tier = result.Tier,
                    monthlyPrice = result.MonthlyPrice,
                    discountApplied = result.DiscountApplied,
                    trialDays = result.TrialDays,
                    couponCode = result.CouponCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create checkout session for {request.Email}");
                return StatusCode(500, new { message = "Failed to create checkout session", error = ex.Message });
            }
        }

        /// <summary>
        /// Validates a coupon code
        /// </summary>
        [HttpPost("validate-coupon")]
        public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.CouponCode))
                    return BadRequest(new { message = "Coupon code is required" });

                var coupon = await _couponRepository.GetByCodeAsync(request.CouponCode.ToUpper());
                
                if (coupon == null)
                {
                    return Ok(new { isValid = false, message = "Coupon code not found" });
                }

                if (!coupon.IsValid)
                {
                    var reason = coupon.IsExpired ? "Coupon has expired" :
                                 coupon.IsExhausted ? "Coupon usage limit reached" :
                                 "Coupon is not active";
                    
                    return Ok(new { isValid = false, message = reason });
                }

                // Check user eligibility
                if (!string.IsNullOrEmpty(request.Email))
                {
                    if (coupon.AllowedEmails.Any() && !coupon.AllowedEmails.Contains(request.Email))
                    {
                        return Ok(new { isValid = false, message = "This coupon is not available for your email address" });
                    }

                    if (coupon.RequiredDomains.Any())
                    {
                        var emailDomain = "@" + request.Email.Split('@')[1];
                        if (!coupon.RequiredDomains.Contains(emailDomain))
                        {
                            return Ok(new { isValid = false, message = "This coupon is only available for specific email domains" });
                        }
                    }

                    // Check usage limits for this user
                    if (!string.IsNullOrEmpty(request.UserId) && coupon.MaxUsagesPerUser.HasValue)
                    {
                        var userUsageCount = await _paymentService.GetCouponUsageCountAsync(coupon.Id, request.UserId);
                        if (userUsageCount >= coupon.MaxUsagesPerUser.Value)
                        {
                            return Ok(new { isValid = false, message = "You have already used this coupon" });
                        }
                    }
                }

                // Calculate discount preview
                decimal discountAmount = 0;
                if (coupon.DiscountType == "percentage")
                {
                    discountAmount = 3.49m * (coupon.DiscountPercentage / 100);
                }
                else if (coupon.DiscountType == "amount")
                {
                    discountAmount = coupon.DiscountAmount;
                }

                return Ok(new
                {
                    isValid = true,
                    coupon = new
                    {
                        coupon.Code,
                        coupon.Name,
                        coupon.Description,
                        coupon.DiscountPercentage,
                        coupon.DiscountAmount,
                        coupon.DiscountType,
                        coupon.FreeTrialDays,
                        coupon.ValidUntil
                    },
                    discountAmount,
                    finalPrice = Math.Max(0, 3.49m - discountAmount),
                    trialDays = coupon.FreeTrialDays,
                    message = $"Coupon applied! Save ${discountAmount:F2}" + (coupon.FreeTrialDays > 0 ? $" + {coupon.FreeTrialDays} day trial" : "")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to validate coupon {request.CouponCode}");
                return StatusCode(500, new { message = "Failed to validate coupon", error = ex.Message });
            }
        }

        /// <summary>
        /// Gets user subscription status
        /// </summary>
        [HttpGet("subscription-status")]
        [Authorize]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                var subscriptionStatus = await _stripeService.GetSubscriptionStatusAsync(userId);

                return Ok(new
                {
                    user = new
                    {
                        user.Id,
                        user.Email,
                        user.SubscriptionTier,
                        user.IsSubscriptionActive,
                        user.IsWhitelisted,
                        user.IsPremium,
                        user.AccessLevel,
                        user.TrialEndDate,
                        user.ActiveCouponCode
                    },
                    subscription = subscriptionStatus,
                    features = new
                    {
                        user.CanUseUnlimitedTranscription,
                        user.CanUseAdvancedSpamDetection,
                        user.CanUseTranslation,
                        user.CanUseAnalytics,
                        user.CanUseAdvancedFeatures,
                        user.MaxVoicemailsPerMonth
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get subscription status");
                return StatusCode(500, new { message = "Failed to get subscription status", error = ex.Message });
            }
        }

        /// <summary>
        /// Checks if email is whitelisted
        /// </summary>
        [HttpPost("check-whitelist")]
        public async Task<IActionResult> CheckWhitelist([FromBody] CheckWhitelistRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var whitelistEntry = await _whitelistRepository.GetByEmailAsync(request.Email);
                
                if (whitelistEntry != null && whitelistEntry.IsActive && !whitelistEntry.IsExpired)
                {
                    return Ok(new
                    {
                        isWhitelisted = true,
                        accessLevel = whitelistEntry.AccessLevel,
                        role = whitelistEntry.Role,
                        expiresAt = whitelistEntry.ExpiresAt,
                        message = $"Welcome! You have {whitelistEntry.AccessLevel} access as a {whitelistEntry.Role}."
                    });
                }

                return Ok(new { isWhitelisted = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to check whitelist for {request.Email}");
                return StatusCode(500, new { message = "Failed to check whitelist status", error = ex.Message });
            }
        }

        /// <summary>
        /// Gets available subscription tiers and pricing
        /// </summary>
        [HttpGet("pricing")]
        public IActionResult GetPricing()
        {
            try
            {
                var pricing = StripeIntegrationService.PricingTiers.Select(tier => new
                {
                    tier.Key,
                    tier.Value.Name,
                    tier.Value.MonthlyPrice,
                    tier.Value.Features,
                    TrialDays = tier.Key == "pro" ? 7 : 0,
                    IsPopular = tier.Key == "pro",
                    SavePercent = tier.Key == "business" ? 65 : 0 // Compared to multiple single-app subscriptions
                });

                return Ok(new
                {
                    tiers = pricing,
                    currency = "USD",
                    billingCycle = "monthly",
                    trialAvailable = true,
                    lastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pricing information");
                return StatusCode(500, new { message = "Failed to get pricing", error = ex.Message });
            }
        }

        /// <summary>
        /// Cancels user subscription
        /// </summary>
        [HttpPost("cancel-subscription")]
        [Authorize]
        public async Task<IActionResult> CancelSubscription()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _stripeService.CancelSubscriptionAsync(userId);
                
                if (!result.Success)
                {
                    return BadRequest(new { message = result.ErrorMessage });
                }

                return Ok(new
                {
                    message = "Subscription cancelled successfully",
                    accessUntil = result.AccessUntil,
                    cancelAtPeriodEnd = result.CancelAtPeriodEnd
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel subscription");
                return StatusCode(500, new { message = "Failed to cancel subscription", error = ex.Message });
            }
        }
    }

    #region Request Models

    public class ValidateCouponRequest
    {
        [Required]
        public string CouponCode { get; set; } = "";

        public string? Email { get; set; }

        public string? UserId { get; set; }
    }

    public class CheckWhitelistRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";
    }

    #endregion
}