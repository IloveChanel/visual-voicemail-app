using Microsoft.EntityFrameworkCore;
using VisualVoicemailPro.Models;
using System.ComponentModel.DataAnnotations;

namespace VisualVoicemailPro.Data
{
    /// <summary>
    /// Entity Framework Database Context for Visual Voicemail Pro
    /// Supports users, coupons, whitelist, and subscription management
    /// </summary>
    public class VisualVoicemailDbContext : DbContext
    {
        public VisualVoicemailDbContext(DbContextOptions<VisualVoicemailDbContext> options) : base(options)
        {
        }

        // Core entities
        public DbSet<User> Users { get; set; }
        public DbSet<Voicemail> Voicemails { get; set; }
        public DbSet<VoicemailAnalytics> VoicemailAnalytics { get; set; }
        public DbSet<SubscriptionUsage> SubscriptionUsages { get; set; }

        // Coupon and whitelist entities
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponUsage> CouponUsages { get; set; }
        public DbSet<DeveloperWhitelist> DeveloperWhitelists { get; set; }
        
        // Multilingual translation entities
        public DbSet<TranslationUsage> TranslationUsages { get; set; }
        public DbSet<TranslationMemoryEntry> TranslationMemoryEntries { get; set; }
        public DbSet<LocalizationResource> LocalizationResources { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.PhoneNumber).IsUnique();
                entity.HasIndex(e => e.StripeCustomerId);
                entity.HasIndex(e => e.IsWhitelisted);
                
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.SubscriptionTier).HasMaxLength(20).HasDefaultValue("free");
                entity.Property(e => e.AccessLevel).HasComputedColumnSql("CASE WHEN IsWhitelisted = 1 THEN 'Developer' WHEN SubscriptionTier = 'business' THEN 'Business' WHEN SubscriptionTier = 'pro' THEN 'Pro' ELSE 'Free' END");
            });

            // Voicemail entity configuration
            modelBuilder.Entity<Voicemail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CallerNumber);
                entity.HasIndex(e => e.ReceivedAt);
                entity.HasIndex(e => e.IsSpam);
                
                entity.Property(e => e.CallerNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ProcessingStatus).HasMaxLength(20).HasDefaultValue("pending");
                entity.Property(e => e.Sentiment).HasMaxLength(20).HasDefaultValue("neutral");
                entity.Property(e => e.Category).HasMaxLength(50).HasDefaultValue("general");
                entity.Property(e => e.Priority).HasMaxLength(20).HasDefaultValue("low");

                // JSON column for collections
                entity.Property(e => e.SpamReasons).HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
                
                entity.Property(e => e.Tags).HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

                // Relationship with User
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Coupon entity configuration
            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.ValidUntil);
                
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.DiscountType).HasMaxLength(20).HasDefaultValue("percentage");
                entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(e => e.DiscountAmount).HasPrecision(10, 2);

                // JSON columns for collections
                entity.Property(e => e.AllowedEmails).HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
                
                entity.Property(e => e.RequiredDomains).HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            });

            // CouponUsage entity configuration
            modelBuilder.Entity<CouponUsage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CouponId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.UsedAt);
                
                entity.Property(e => e.CouponId).IsRequired();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.UserEmail).IsRequired().HasMaxLength(255);
                entity.Property(e => e.DiscountApplied).HasPrecision(10, 2);
                entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("applied");

                // Relationships
                entity.HasOne(e => e.Coupon)
                    .WithMany()
                    .HasForeignKey(e => e.CouponId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // DeveloperWhitelist entity configuration
            modelBuilder.Entity<DeveloperWhitelist>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.Role);
                
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("developer");
                entity.Property(e => e.AccessLevel).HasMaxLength(20).HasDefaultValue("full");
                entity.Property(e => e.AddedBy).HasMaxLength(255);
            });

            // VoicemailAnalytics entity configuration
            modelBuilder.Entity<VoicemailAnalytics>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.PeriodStart });
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.PeriodStart);
                
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.SpamDetectionAccuracy).HasPrecision(5, 4);

                // JSON columns for dictionaries
                entity.Property(e => e.CategoryCounts).HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, int>());
                
                entity.Property(e => e.SentimentCounts).HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, int>());
                
                entity.Property(e => e.LanguageCounts).HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, int>());
                
                entity.Property(e => e.TopCallers).HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            });

            // SubscriptionUsage entity configuration
            modelBuilder.Entity<SubscriptionUsage>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.BillingPeriodStart });
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.BillingPeriodStart);
                
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.SubscriptionTier).HasMaxLength(20);
                entity.Property(e => e.CurrentCharges).HasPrecision(10, 2);

                entity.Property(e => e.FeaturesUsed).HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            });

            // TranslationUsage entity configuration
            modelBuilder.Entity<TranslationUsage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.Provider);
                
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.SourceLanguage).IsRequired().HasMaxLength(10);
                entity.Property(e => e.TargetLanguage).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Context).HasMaxLength(500);
                entity.Property(e => e.Cost).HasPrecision(10, 4);
            });

            // TranslationMemoryEntry entity configuration
            modelBuilder.Entity<TranslationMemoryEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.SourceLanguage, e.TargetLanguage });
                entity.HasIndex(e => e.Context);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.LastUsed);
                
                entity.Property(e => e.SourceText).IsRequired();
                entity.Property(e => e.TranslatedText).IsRequired();
                entity.Property(e => e.SourceLanguage).IsRequired().HasMaxLength(10);
                entity.Property(e => e.TargetLanguage).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Context).HasMaxLength(500);
                entity.Property(e => e.CreatedBy).HasMaxLength(450);
                entity.Property(e => e.QualityScore).HasPrecision(3, 2);
            });

            // LocalizationResource entity configuration
            modelBuilder.Entity<LocalizationResource>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Key, e.LanguageCode }).IsUnique();
                entity.HasIndex(e => e.LanguageCode);
                entity.HasIndex(e => e.Category);
                
                entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
                entity.Property(e => e.LanguageCode).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Category).HasMaxLength(50).HasDefaultValue("general");
                entity.Property(e => e.UpdatedBy).HasMaxLength(450);
                entity.Property(e => e.PluralForm).HasMaxLength(1000);
            });

            // Seed initial data
            SeedInitialData(modelBuilder);
        }

        private void SeedInitialData(ModelBuilder modelBuilder)
        {
            // Seed developer whitelist entries
            modelBuilder.Entity<DeveloperWhitelist>().HasData(
                new DeveloperWhitelist
                {
                    Id = "dev-001",
                    Email = "developer@visualvoicemail.com",
                    Name = "Main Developer",
                    Role = "developer",
                    AccessLevel = "full",
                    IsActive = true,
                    AddedAt = DateTime.UtcNow,
                    AddedBy = "system",
                    CanAccessAdminPanel = true,
                    CanCreateCoupons = true,
                    CanManageWhitelist = true,
                    CanViewAnalytics = true,
                    CanBypassLimits = true,
                    Notes = "Main developer account with full access"
                },
                new DeveloperWhitelist
                {
                    Id = "test-001",
                    Email = "tester@visualvoicemail.com",
                    Name = "QA Tester",
                    Role = "tester",
                    AccessLevel = "limited",
                    IsActive = true,
                    AddedAt = DateTime.UtcNow,
                    AddedBy = "system",
                    CanAccessAdminPanel = false,
                    CanCreateCoupons = false,
                    CanManageWhitelist = false,
                    CanViewAnalytics = true,
                    CanBypassLimits = true,
                    Notes = "QA testing account"
                }
            );

            // Seed sample coupons
            modelBuilder.Entity<Coupon>().HasData(
                new Coupon
                {
                    Id = "coup-welcome",
                    Code = "WELCOME30",
                    Name = "Welcome 30% Off",
                    Description = "30% off first month for new users",
                    IsActive = true,
                    DiscountPercentage = 30,
                    DiscountType = "percentage",
                    FreeTrialDays = 14,
                    TrialTier = "pro",
                    ValidFrom = DateTime.UtcNow,
                    ValidUntil = DateTime.UtcNow.AddMonths(3),
                    MaxUsages = 1000,
                    CurrentUsages = 0,
                    MaxUsagesPerUser = 1,
                    IsFirstTimeOnly = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                },
                new Coupon
                {
                    Id = "coup-dev",
                    Code = "DEVELOPER",
                    Name = "Developer Trial",
                    Description = "Extended trial for developers and reviewers",
                    IsActive = true,
                    DiscountPercentage = 100,
                    DiscountType = "percentage",
                    FreeTrialDays = 90,
                    TrialTier = "pro",
                    ValidFrom = DateTime.UtcNow,
                    ValidUntil = DateTime.UtcNow.AddYears(1),
                    MaxUsages = 50,
                    CurrentUsages = 0,
                    MaxUsagesPerUser = 1,
                    IsFirstTimeOnly = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                }
            );
            
            // Seed localization resources
            modelBuilder.Entity<LocalizationResource>().HasData(
                // English resources
                new LocalizationResource
                {
                    Id = 1,
                    Key = "app.title",
                    LanguageCode = "en",
                    Value = "Visual Voicemail Pro",
                    Category = "app",
                    Description = "Application title"
                },
                new LocalizationResource
                {
                    Id = 2,
                    Key = "voicemail.transcribing",
                    LanguageCode = "en",
                    Value = "Transcribing voicemail...",
                    Category = "voicemail"
                },
                new LocalizationResource
                {
                    Id = 3,
                    Key = "voicemail.translating",
                    LanguageCode = "en",
                    Value = "Translating voicemail...",
                    Category = "voicemail"
                },
                new LocalizationResource
                {
                    Id = 4,
                    Key = "subscription.upgrade",
                    LanguageCode = "en",
                    Value = "Upgrade to Pro",
                    Category = "subscription"
                },
                new LocalizationResource
                {
                    Id = 5,
                    Key = "spam.detected",
                    LanguageCode = "en",
                    Value = "Spam detected",
                    Category = "spam"
                },
                
                // Spanish resources
                new LocalizationResource
                {
                    Id = 6,
                    Key = "app.title",
                    LanguageCode = "es",
                    Value = "Correo de Voz Visual Pro",
                    Category = "app",
                    Description = "Título de la aplicación"
                },
                new LocalizationResource
                {
                    Id = 7,
                    Key = "voicemail.transcribing",
                    LanguageCode = "es",
                    Value = "Transcribiendo correo de voz...",
                    Category = "voicemail"
                },
                new LocalizationResource
                {
                    Id = 8,
                    Key = "voicemail.translating",
                    LanguageCode = "es",
                    Value = "Traduciendo correo de voz...",
                    Category = "voicemail"
                },
                new LocalizationResource
                {
                    Id = 9,
                    Key = "subscription.upgrade",
                    LanguageCode = "es",
                    Value = "Actualizar a Pro",
                    Category = "subscription"
                },
                new LocalizationResource
                {
                    Id = 10,
                    Key = "spam.detected",
                    LanguageCode = "es",
                    Value = "Spam detectado",
                    Category = "spam"
                }
            );
        }
    }
}

namespace VisualVoicemailPro.Repositories
{
    /// <summary>
    /// Repository interface for User operations
    /// </summary>
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(string id);
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task DeleteAsync(string id);
        Task<bool> ExistsAsync(string email);
    }

    /// <summary>
    /// Repository interface for Coupon operations
    /// </summary>
    public interface ICouponRepository
    {
        Task<Coupon?> GetByCodeAsync(string code);
        Task<Coupon?> GetByIdAsync(string id);
        Task<List<Coupon>> GetActiveAsync();
        Task<Coupon> CreateAsync(Coupon coupon);
        Task<Coupon> UpdateAsync(Coupon coupon);
        Task DeleteAsync(string id);
        Task IncrementUsageAsync(string id);
    }

    /// <summary>
    /// Repository interface for Developer Whitelist operations
    /// </summary>
    public interface IWhitelistRepository
    {
        Task<DeveloperWhitelist?> GetByEmailAsync(string email);
        Task<List<DeveloperWhitelist>> GetActiveAsync();
        Task<DeveloperWhitelist> CreateAsync(DeveloperWhitelist entry);
        Task<DeveloperWhitelist> UpdateAsync(DeveloperWhitelist entry);
        Task DeleteAsync(string id);
    }

    /// <summary>
    /// User repository implementation
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly VisualVoicemailDbContext _context;

        public UserRepository(VisualVoicemailDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User> CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task DeleteAsync(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }
    }

    /// <summary>
    /// Coupon repository implementation
    /// </summary>
    public class CouponRepository : ICouponRepository
    {
        private readonly VisualVoicemailDbContext _context;

        public CouponRepository(VisualVoicemailDbContext context)
        {
            _context = context;
        }

        public async Task<Coupon?> GetByCodeAsync(string code)
        {
            return await _context.Coupons.FirstOrDefaultAsync(c => c.Code == code);
        }

        public async Task<Coupon?> GetByIdAsync(string id)
        {
            return await _context.Coupons.FindAsync(id);
        }

        public async Task<List<Coupon>> GetActiveAsync()
        {
            return await _context.Coupons
                .Where(c => c.IsActive && (!c.ValidUntil.HasValue || c.ValidUntil > DateTime.UtcNow))
                .ToListAsync();
        }

        public async Task<Coupon> CreateAsync(Coupon coupon)
        {
            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();
            return coupon;
        }

        public async Task<Coupon> UpdateAsync(Coupon coupon)
        {
            _context.Coupons.Update(coupon);
            await _context.SaveChangesAsync();
            return coupon;
        }

        public async Task DeleteAsync(string id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon != null)
            {
                _context.Coupons.Remove(coupon);
                await _context.SaveChangesAsync();
            }
        }

        public async Task IncrementUsageAsync(string id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon != null)
            {
                coupon.CurrentUsages++;
                coupon.LastUsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Whitelist repository implementation
    /// </summary>
    public class WhitelistRepository : IWhitelistRepository
    {
        private readonly VisualVoicemailDbContext _context;

        public WhitelistRepository(VisualVoicemailDbContext context)
        {
            _context = context;
        }

        public async Task<DeveloperWhitelist?> GetByEmailAsync(string email)
        {
            return await _context.DeveloperWhitelists.FirstOrDefaultAsync(w => w.Email == email);
        }

        public async Task<List<DeveloperWhitelist>> GetActiveAsync()
        {
            return await _context.DeveloperWhitelists
                .Where(w => w.IsActive && (!w.ExpiresAt.HasValue || w.ExpiresAt > DateTime.UtcNow))
                .ToListAsync();
        }

        public async Task<DeveloperWhitelist> CreateAsync(DeveloperWhitelist entry)
        {
            _context.DeveloperWhitelists.Add(entry);
            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task<DeveloperWhitelist> UpdateAsync(DeveloperWhitelist entry)
        {
            _context.DeveloperWhitelists.Update(entry);
            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task DeleteAsync(string id)
        {
            var entry = await _context.DeveloperWhitelists.FindAsync(id);
            if (entry != null)
            {
                _context.DeveloperWhitelists.Remove(entry);
                await _context.SaveChangesAsync();
            }
        }
    }
}