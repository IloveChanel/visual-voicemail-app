using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using VisualVoicemailPro.Models;
using VisualVoicemailPro.Repositories;
using VisualVoicemailPro.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace VisualVoicemailPro.Controllers
{
    /// <summary>
    /// Admin Controller for managing whitelist, coupons, and developer access
    /// Requires JWT authentication with admin privileges
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin,Developer")]
    public class AdminController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ICouponRepository _couponRepository;
        private readonly IWhitelistRepository _whitelistRepository;
        private readonly VisualVoicemailDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IUserRepository userRepository,
            ICouponRepository couponRepository,
            IWhitelistRepository whitelistRepository,
            VisualVoicemailDbContext context,
            ILogger<AdminController> logger)
        {
            _userRepository = userRepository;
            _couponRepository = couponRepository;
            _whitelistRepository = whitelistRepository;
            _context = context;
            _logger = logger;
        }

        #region Whitelist Management

        /// <summary>
        /// Adds user to developer whitelist for free access
        /// </summary>
        [HttpPost("whitelist/add")]
        public async Task<IActionResult> AddToWhitelist([FromBody] AddWhitelistRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "system";

                // Check if email already exists in whitelist
                var existing = await _whitelistRepository.GetByEmailAsync(request.Email);
                if (existing != null)
                {
                    return Conflict(new { message = "Email already exists in whitelist", status = existing.Status });
                }

                var whitelistEntry = new DeveloperWhitelist
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = request.Email,
                    Name = request.Name,
                    Role = request.Role,
                    AccessLevel = request.AccessLevel,
                    IsActive = true,
                    AddedAt = DateTime.UtcNow,
                    AddedBy = adminEmail,
                    ExpiresAt = request.ExpiresAt,
                    Notes = request.Notes,
                    CanAccessAdminPanel = request.CanAccessAdminPanel,
                    CanCreateCoupons = request.CanCreateCoupons,
                    CanManageWhitelist = request.CanManageWhitelist,
                    CanViewAnalytics = request.CanViewAnalytics,
                    CanBypassLimits = request.CanBypassLimits
                };

                await _whitelistRepository.CreateAsync(whitelistEntry);

                // Update user record if exists
                var user = await _userRepository.GetByEmailAsync(request.Email);
                if (user != null)
                {
                    user.IsWhitelisted = true;
                    user.WhitelistReason = request.Role;
                    user.WhitelistedAt = DateTime.UtcNow;
                    user.WhitelistedBy = adminEmail;
                    user.IsPremium = true;
                    user.SubscriptionTier = "pro";
                    await _userRepository.UpdateAsync(user);
                }

                _logger.LogInformation($"Added {request.Email} to whitelist by {adminEmail}");

                return Ok(new
                {
                    message = "User successfully added to whitelist",
                    email = request.Email,
                    accessLevel = request.AccessLevel,
                    expiresAt = request.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to add {request.Email} to whitelist");
                return StatusCode(500, new { message = "Failed to add user to whitelist", error = ex.Message });
            }
        }

        /// <summary>
        /// Removes user from whitelist
        /// </summary>
        [HttpDelete("whitelist/{email}")]
        public async Task<IActionResult> RemoveFromWhitelist(string email)
        {
            try
            {
                var whitelistEntry = await _whitelistRepository.GetByEmailAsync(email);
                if (whitelistEntry == null)
                {
                    return NotFound(new { message = "Email not found in whitelist" });
                }

                await _whitelistRepository.DeleteAsync(whitelistEntry.Id);

                // Update user record
                var user = await _userRepository.GetByEmailAsync(email);
                if (user != null)
                {
                    user.IsWhitelisted = false;
                    user.WhitelistReason = null;
                    user.WhitelistedAt = null;
                    user.WhitelistedBy = null;
                    // Note: Don't automatically downgrade subscription as they might have paid
                    await _userRepository.UpdateAsync(user);
                }

                _logger.LogInformation($"Removed {email} from whitelist by {User.FindFirst(ClaimTypes.Email)?.Value}");

                return Ok(new { message = "User successfully removed from whitelist", email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to remove {email} from whitelist");
                return StatusCode(500, new { message = "Failed to remove user from whitelist", error = ex.Message });
            }
        }

        /// <summary>
        /// Gets all whitelist entries
        /// </summary>
        [HttpGet("whitelist")]
        public async Task<IActionResult> GetWhitelist()
        {
            try
            {
                var whitelist = await _whitelistRepository.GetActiveAsync();
                return Ok(whitelist.Select(w => new
                {
                    w.Id,
                    w.Email,
                    w.Name,
                    w.Role,
                    w.AccessLevel,
                    w.IsActive,
                    w.AddedAt,
                    w.AddedBy,
                    w.ExpiresAt,
                    w.LastAccessAt,
                    w.Status,
                    w.Notes
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get whitelist");
                return StatusCode(500, new { message = "Failed to retrieve whitelist", error = ex.Message });
            }
        }

        #endregion

        #region Coupon Management

        /// <summary>
        /// Creates a new coupon
        /// </summary>
        [HttpPost("coupons")]
        public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Check if coupon code already exists
                var existing = await _couponRepository.GetByCodeAsync(request.Code);
                if (existing != null)
                {
                    return Conflict(new { message = "Coupon code already exists" });
                }

                var coupon = new Coupon
                {
                    Id = Guid.NewGuid().ToString(),
                    Code = request.Code.ToUpper(),
                    Name = request.Name,
                    Description = request.Description,
                    IsActive = request.IsActive,
                    DiscountPercentage = request.DiscountPercentage,
                    DiscountAmount = request.DiscountAmount,
                    DiscountType = request.DiscountType,
                    FreeTrialDays = request.FreeTrialDays,
                    TrialTier = request.TrialTier,
                    ValidFrom = request.ValidFrom,
                    ValidUntil = request.ValidUntil,
                    MaxUsages = request.MaxUsages,
                    MaxUsagesPerUser = request.MaxUsagesPerUser,
                    AllowedEmails = request.AllowedEmails ?? new List<string>(),
                    RequiredDomains = request.RequiredDomains ?? new List<string>(),
                    IsFirstTimeOnly = request.IsFirstTimeOnly,
                    ApplicableToTier = request.ApplicableToTier,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.FindFirst(ClaimTypes.Email)?.Value ?? "system"
                };

                await _couponRepository.CreateAsync(coupon);

                _logger.LogInformation($"Created coupon {request.Code} by {coupon.CreatedBy}");

                return CreatedAtAction(nameof(GetCoupon), new { code = coupon.Code }, new
                {
                    coupon.Id,
                    coupon.Code,
                    coupon.Name,
                    coupon.Description,
                    coupon.DiscountPercentage,
                    coupon.DiscountAmount,
                    coupon.FreeTrialDays,
                    coupon.Status,
                    coupon.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create coupon {request.Code}");
                return StatusCode(500, new { message = "Failed to create coupon", error = ex.Message });
            }
        }

        /// <summary>
        /// Gets coupon details by code
        /// </summary>
        [HttpGet("coupons/{code}")]
        public async Task<IActionResult> GetCoupon(string code)
        {
            try
            {
                var coupon = await _couponRepository.GetByCodeAsync(code);
                if (coupon == null)
                {
                    return NotFound(new { message = "Coupon not found" });
                }

                // Get usage statistics
                var usages = await _context.CouponUsages
                    .Where(cu => cu.CouponId == coupon.Id)
                    .GroupBy(cu => cu.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                return Ok(new
                {
                    coupon.Id,
                    coupon.Code,
                    coupon.Name,
                    coupon.Description,
                    coupon.IsActive,
                    coupon.DiscountPercentage,
                    coupon.DiscountAmount,
                    coupon.DiscountType,
                    coupon.FreeTrialDays,
                    coupon.TrialTier,
                    coupon.ValidFrom,
                    coupon.ValidUntil,
                    coupon.MaxUsages,
                    coupon.CurrentUsages,
                    coupon.MaxUsagesPerUser,
                    coupon.IsFirstTimeOnly,
                    coupon.Status,
                    coupon.CreatedAt,
                    coupon.CreatedBy,
                    coupon.LastUsedAt,
                    UsageStats = usages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get coupon {code}");
                return StatusCode(500, new { message = "Failed to retrieve coupon", error = ex.Message });
            }
        }

        /// <summary>
        /// Gets all coupons with usage statistics
        /// </summary>
        [HttpGet("coupons")]
        public async Task<IActionResult> GetCoupons([FromQuery] bool? activeOnly = null)
        {
            try
            {
                var coupons = activeOnly == true
                    ? await _couponRepository.GetActiveAsync()
                    : await _context.Coupons.ToListAsync();

                var result = coupons.Select(c => new
                {
                    c.Id,
                    c.Code,
                    c.Name,
                    c.Description,
                    c.DiscountPercentage,
                    c.DiscountAmount,
                    c.FreeTrialDays,
                    c.CurrentUsages,
                    c.MaxUsages,
                    c.Status,
                    c.CreatedAt,
                    c.LastUsedAt,
                    UsageRate = c.MaxUsages.HasValue ? (double)c.CurrentUsages / c.MaxUsages.Value : 0
                }).OrderByDescending(c => c.CreatedAt);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get coupons");
                return StatusCode(500, new { message = "Failed to retrieve coupons", error = ex.Message });
            }
        }

        /// <summary>
        /// Updates coupon status (activate/deactivate)
        /// </summary>
        [HttpPatch("coupons/{code}/status")]
        public async Task<IActionResult> UpdateCouponStatus(string code, [FromBody] UpdateCouponStatusRequest request)
        {
            try
            {
                var coupon = await _couponRepository.GetByCodeAsync(code);
                if (coupon == null)
                {
                    return NotFound(new { message = "Coupon not found" });
                }

                coupon.IsActive = request.IsActive;
                await _couponRepository.UpdateAsync(coupon);

                _logger.LogInformation($"Updated coupon {code} status to {(request.IsActive ? "active" : "inactive")} by {User.FindFirst(ClaimTypes.Email)?.Value}");

                return Ok(new
                {
                    message = $"Coupon {(request.IsActive ? "activated" : "deactivated")} successfully",
                    code = coupon.Code,
                    status = coupon.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update coupon {code} status");
                return StatusCode(500, new { message = "Failed to update coupon status", error = ex.Message });
            }
        }

        #endregion

        #region Analytics and Monitoring

        /// <summary>
        /// Gets admin analytics dashboard data
        /// </summary>
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics([FromQuery] int days = 30)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days);

                var userStats = await _context.Users
                    .Where(u => u.CreatedAt >= startDate)
                    .GroupBy(u => u.SubscriptionTier)
                    .Select(g => new { Tier = g.Key, Count = g.Count() })
                    .ToListAsync();

                var whitelistStats = await _context.DeveloperWhitelists
                    .Where(w => w.AddedAt >= startDate)
                    .GroupBy(w => w.Role)
                    .Select(g => new { Role = g.Key, Count = g.Count() })
                    .ToListAsync();

                var couponStats = await _context.CouponUsages
                    .Where(cu => cu.UsedAt >= startDate)
                    .GroupBy(cu => cu.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count(), TotalDiscount = g.Sum(x => x.DiscountApplied) })
                    .ToListAsync();

                var topCoupons = await _context.CouponUsages
                    .Where(cu => cu.UsedAt >= startDate)
                    .Join(_context.Coupons, cu => cu.CouponId, c => c.Id, (cu, c) => new { cu, c })
                    .GroupBy(x => x.c.Code)
                    .Select(g => new
                    {
                        Code = g.Key,
                        Uses = g.Count(),
                        TotalDiscount = g.Sum(x => x.cu.DiscountApplied)
                    })
                    .OrderByDescending(x => x.Uses)
                    .Take(10)
                    .ToListAsync();

                return Ok(new
                {
                    Period = new { StartDate = startDate, EndDate = DateTime.UtcNow, Days = days },
                    UserStats = userStats,
                    WhitelistStats = whitelistStats,
                    CouponStats = couponStats,
                    TopCoupons = topCoupons,
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalWhitelisted = await _context.DeveloperWhitelists.CountAsync(w => w.IsActive),
                    TotalActiveCoupons = await _context.Coupons.CountAsync(c => c.IsActive),
                    GeneratedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get admin analytics");
                return StatusCode(500, new { message = "Failed to retrieve analytics", error = ex.Message });
            }
        }

        #endregion
    }

    #region Request/Response Models

    public class AddWhitelistRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Name { get; set; } = "";

        public string Role { get; set; } = "developer";

        public string AccessLevel { get; set; } = "full";

        public DateTime? ExpiresAt { get; set; }

        public string? Notes { get; set; }

        public bool CanAccessAdminPanel { get; set; } = false;

        public bool CanCreateCoupons { get; set; } = false;

        public bool CanManageWhitelist { get; set; } = false;

        public bool CanViewAnalytics { get; set; } = true;

        public bool CanBypassLimits { get; set; } = true;
    }

    public class CreateCouponRequest
    {
        [Required]
        public string Code { get; set; } = "";

        [Required]
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public bool IsActive { get; set; } = true;

        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; } = 0;

        [Range(0, double.MaxValue)]
        public decimal DiscountAmount { get; set; } = 0;

        public string DiscountType { get; set; } = "percentage";

        [Range(0, 365)]
        public int FreeTrialDays { get; set; } = 0;

        public string TrialTier { get; set; } = "pro";

        public DateTime? ValidFrom { get; set; }

        public DateTime? ValidUntil { get; set; }

        public int? MaxUsages { get; set; }

        public int? MaxUsagesPerUser { get; set; } = 1;

        public List<string>? AllowedEmails { get; set; }

        public List<string>? RequiredDomains { get; set; }

        public bool IsFirstTimeOnly { get; set; } = false;

        public string? ApplicableToTier { get; set; }
    }

    public class UpdateCouponStatusRequest
    {
        [Required]
        public bool IsActive { get; set; }
    }

    #endregion
}