using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VisualVoicemailPro.Data;
using VisualVoicemailPro.Repositories;
using VisualVoicemailPro.Controllers;
using Stripe;
using Stripe.Checkout;
using System.Text.Json;
using System.Text;

/// <summary>
/// Enhanced Visual Voicemail Pro API with Stripe Integration, Coupons, and Developer Whitelist
/// Production-ready service for $3.49/month subscription with flexible promotion system
/// Built with Entity Framework, JWT authentication, and comprehensive admin controls
/// </summary>

var builder = WebApplication.CreateBuilder(args);

// Add Entity Framework with SQL Server
builder.Services.AddDbContext<VisualVoicemailDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
        "Server=(localdb)\\mssqllocaldb;Database=VisualVoicemailPro;Trusted_Connection=true;MultipleActiveResultSets=true"));

// Add JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "VisualVoicemailPro",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "VisualVoicemailPro",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["Jwt:SecretKey"] ?? "VisualVoicemailPro_SuperSecretKey_2024_ChangeInProduction"))
    };
});

builder.Services.AddAuthorization();

// Add Controllers
builder.Services.AddControllers();

// Configure services
builder.Services.AddLogging();

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<IWhitelistRepository, WhitelistRepository>();

// Register services
builder.Services.AddScoped<PaymentService>(provider =>
{
    var userRepo = provider.GetRequiredService<IUserRepository>();
    var couponRepo = provider.GetRequiredService<ICouponRepository>();
    var whitelistRepo = provider.GetRequiredService<IWhitelistRepository>();
    var context = provider.GetRequiredService<VisualVoicemailDbContext>();
    var logger = provider.GetRequiredService<ILogger<PaymentService>>();
    var apiKey = builder.Configuration["Stripe:SecretKey"] ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "sk_test_default";
    
    return new PaymentService(userRepo, couponRepo, whitelistRepo, context, logger, apiKey);
});

builder.Services.AddScoped<StripeIntegrationService>();

// Register translation services
builder.Services.AddScoped<IMultilingualTranslationService, MultilingualTranslationService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<IGoogleTranslationProvider, GoogleTranslationProvider>();
builder.Services.AddScoped<IDeepLTranslationProvider, DeepLTranslationProvider>();
builder.Services.AddScoped<IMicrosoftTranslationProvider, MicrosoftTranslationProvider>();

// Configure translation settings
builder.Services.Configure<TranslationConfiguration>(
    builder.Configuration.GetSection("Translation"));

// Add memory cache for translation and localization caching
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<VoicemailProcessor>(provider => 
    new VoicemailProcessor(builder.Configuration["GoogleCloud:ProjectId"] ?? "visual-voicemail-pro"));

// Set your Stripe secret key - Replace with your actual keys
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"] ?? 
    Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? 
    "sk_test_51QLrGvLkdIwHu7ix..."; // Replace with your test key

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => 
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var stripeService = app.Services.GetRequiredService<StripeIntegrationService>();
var paymentService = app.Services.GetRequiredService<PaymentService>();
var voicemailProcessor = app.Services.GetRequiredService<VoicemailProcessor>();

logger.LogInformation("🚀 Visual Voicemail Pro API Starting - Version 2.0");
logger.LogInformation($"📱 Supporting subscription tiers: Free, Pro ($3.49/month), Business ($9.99/month)");

#region Subscription Management Endpoints

/// <summary>
/// Create Stripe Checkout Session for Visual Voicemail Pro
/// Enhanced version of your original /create-checkout-session endpoint
/// </summary>
app.MapPost("/create-checkout-session", async (
    [FromBody] CheckoutRequest request,
    HttpContext context) =>
{
    try
    {
        logger.LogInformation($"🎯 Creating checkout session for user: {request.UserId}, tier: {request.Tier}");

        var result = await stripeService.CreateCheckoutSessionAsync(
            request.UserId,
            request.Tier,
            request.CustomerEmail,
            request.PhoneNumber
        );

        if (!result.Success)
        {
            logger.LogError($"❌ Checkout session creation failed: {result.ErrorMessage}");
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        logger.LogInformation($"✅ Checkout session created: {result.SessionId}");
        
        return Results.Json(new
        {
            success = true,
            sessionId = result.SessionId,
            paymentUrl = result.PaymentUrl,
            tier = result.Tier,
            monthlyPrice = result.MonthlyPrice,
            trialDays = result.TrialDays,
            message = $"7-day free trial for Visual Voicemail Pro at ${result.MonthlyPrice}/month"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "💥 Checkout session creation error");
        return Results.Problem("Failed to create checkout session");
    }
});

/// <summary>
/// Get current subscription status for a user
/// </summary>
app.MapGet("/subscription/status/{userId}", async (string userId) =>
{
    try
    {
        var status = await stripeService.GetSubscriptionStatusAsync(userId);
        
        return Results.Json(new
        {
            success = true,
            isActive = status.IsActive,
            tier = status.Tier,
            status = status.Status,
            monthlyPrice = status.MonthlyPrice,
            features = status.Features,
            currentPeriodEnd = status.CurrentPeriodEnd,
            trialEnd = status.TrialEnd,
            cancelAtPeriodEnd = status.CancelAtPeriodEnd
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Failed to get subscription status for user {userId}");
        return Results.Problem("Failed to retrieve subscription status");
    }
});

/// <summary>
/// Cancel subscription (at period end)
/// </summary>
app.MapPost("/subscription/cancel/{userId}", async (string userId) =>
{
    try
    {
        var result = await stripeService.CancelSubscriptionAsync(userId);
        
        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        logger.LogInformation($"📅 Subscription canceled for user {userId}, access until: {result.AccessUntil}");

        return Results.Json(new
        {
            success = true,
            message = "Subscription will cancel at the end of your billing period",
            accessUntil = result.AccessUntil
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Failed to cancel subscription for user {userId}");
        return Results.Problem("Failed to cancel subscription");
    }
});

#endregion

#region Webhook Handling

/// <summary>
/// Stripe Webhook Handler - Critical for subscription lifecycle management
/// </summary>
app.MapPost("/webhook/stripe", async (HttpContext context) =>
{
    try
    {
        var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var stripeSignature = context.Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(stripeSignature))
        {
            logger.LogWarning("⚠️ Stripe webhook received without signature");
            return Results.BadRequest("Missing Stripe signature");
        }

        var result = await stripeService.ProcessWebhookAsync(requestBody, stripeSignature);
        
        if (!result.Success)
        {
            logger.LogError($"❌ Webhook processing failed: {result.ErrorMessage}");
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        logger.LogInformation($"✅ Webhook processed: {result.EventType} - {result.EventId}");
        return Results.Ok();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "💥 Webhook processing error");
        return Results.Problem("Webhook processing failed");
    }
});

#endregion

#region Voicemail Processing API

/// <summary>
/// Process voicemail with transcription, translation, and spam detection
/// </summary>
app.MapPost("/voicemail/process", async ([FromBody] VoicemailProcessRequest request) =>
{
    try
    {
        // Check if user has active subscription for advanced features
        var subscriptionStatus = await stripeService.GetSubscriptionStatusAsync(request.UserId);
        
        logger.LogInformation($"🎙️ Processing voicemail for user: {request.UserId}, caller: {request.CallerNumber}");

        // Process voicemail with appropriate feature set based on subscription
        var result = await voicemailProcessor.ProcessVoicemailAsync(
            request.AudioFilePath,
            request.CallerNumber,
            request.PreferredLanguage ?? "en"
        );

        // Apply subscription tier limitations
        if (!subscriptionStatus.IsActive || subscriptionStatus.Tier == "free")
        {
            // Free tier: Basic transcription only
            result.TranslatedText = null;
            result.Category = "general";
            result.Priority = "low";
            result.Summary = null;
        }

        logger.LogInformation($"✅ Voicemail processed: {result.ProcessingStatus}, spam: {result.IsSpam}");

        return Results.Json(new
        {
            success = true,
            voicemail = result,
            subscriptionTier = subscriptionStatus.Tier,
            processingFeatures = subscriptionStatus.Features
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Failed to process voicemail for user {request.UserId}");
        return Results.Problem("Voicemail processing failed");
    }
});

/// <summary>
/// Get voicemail analytics and insights (Pro/Business tier only)
/// </summary>
app.MapGet("/analytics/{userId}", async (string userId) =>
{
    try
    {
        var subscriptionStatus = await stripeService.GetSubscriptionStatusAsync(userId);
        
        if (!subscriptionStatus.IsActive || subscriptionStatus.Tier == "free")
        {
            return Results.Json(new
            {
                success = false,
                error = "Analytics require Visual Voicemail Pro subscription",
                upgradeUrl = "/create-checkout-session"
            });
        }

        var analytics = await paymentService.GetUserAnalyticsAsync(userId);
        
        return Results.Json(new
        {
            success = true,
            analytics = analytics,
            tier = subscriptionStatus.Tier
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"Failed to get analytics for user {userId}");
        return Results.Problem("Failed to retrieve analytics");
    }
});

#endregion

#region Health and Info Endpoints

/// <summary>
/// Health check endpoint
/// </summary>
app.MapGet("/health", () =>
{
    return Results.Json(new
    {
        status = "healthy",
        service = "Visual Voicemail Pro API",
        version = "2.0",
        timestamp = DateTime.UtcNow,
        features = new[]
        {
            "Stripe subscriptions ($3.49/month)",
            "Multi-language transcription",
            "Advanced spam detection",
            "Real-time translation",
            "Smart categorization",
            "7-day free trial"
        }
    });
});

/// <summary>
/// Get pricing information
/// </summary>
app.MapGet("/pricing", () =>
{
    return Results.Json(new
    {
        success = true,
        tiers = StripeIntegrationService.PricingTiers.Select(t => new
        {
            id = t.Key,
            name = t.Value.Name,
            monthlyPrice = t.Value.MonthlyPrice,
            features = t.Value.Features,
            priceId = t.Value.PriceId,
            recommended = t.Key == "pro"
        })
    });
});

#endregion

#region CORS and Static Files

app.UseCors();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Serve success/cancel pages
app.MapGet("/success", () => 
    Results.Content(@"
    <!DOCTYPE html>
    <html>
    <head>
        <title>Welcome to Visual Voicemail Pro!</title>
        <meta name='viewport' content='width=device-width, initial-scale=1'>
        <style>
            body { font-family: system-ui; text-align: center; padding: 2rem; background: #f0f9ff; }
            .container { max-width: 500px; margin: 0 auto; background: white; padding: 2rem; border-radius: 10px; }
            .success { color: #10b981; font-size: 3rem; }
            h1 { color: #1f2937; }
            p { color: #6b7280; line-height: 1.6; }
            .button { background: #3b82f6; color: white; padding: 1rem 2rem; border: none; border-radius: 5px; text-decoration: none; display: inline-block; margin-top: 1rem; }
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='success'>✅</div>
            <h1>Welcome to Visual Voicemail Pro!</h1>
            <p>Your subscription is now active. Enjoy advanced transcription, spam detection, and multi-language support for just $3.49/month.</p>
            <p>Your 7-day free trial has started. Download the app to get started!</p>
            <a href='#' class='button'>Download Mobile App</a>
        </div>
    </body>
    </html>", "text/html"));

app.MapGet("/cancel", () => 
    Results.Content(@"
    <!DOCTYPE html>
    <html>
    <head>
        <title>Subscription Canceled</title>
        <meta name='viewport' content='width=device-width, initial-scale=1'>
        <style>
            body { font-family: system-ui; text-align: center; padding: 2rem; background: #fef2f2; }
            .container { max-width: 500px; margin: 0 auto; background: white; padding: 2rem; border-radius: 10px; }
            .cancel { color: #ef4444; font-size: 3rem; }
            h1 { color: #1f2937; }
            p { color: #6b7280; line-height: 1.6; }
            .button { background: #3b82f6; color: white; padding: 1rem 2rem; border: none; border-radius: 5px; text-decoration: none; display: inline-block; margin-top: 1rem; }
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='cancel'>❌</div>
            <h1>Subscription Canceled</h1>
            <p>No worries! You can still use Visual Voicemail with basic features, or upgrade anytime to unlock advanced capabilities.</p>
            <a href='/pricing' class='button'>View Pricing</a>
        </div>
    </body>
    </html>", "text/html"));

#endregion

// Map controllers for Admin and User management
app.MapControllers();

// Start the application
logger.LogInformation("🌟 Visual Voicemail Pro API is running!");
logger.LogInformation($"📊 Ready to process voicemails with $3.49/month Pro subscriptions");
logger.LogInformation($"🔗 Stripe webhooks ready for subscription management");

app.Run();

#region Request Models

public record CheckoutRequest(
    string UserId,
    string Tier,
    string CustomerEmail,
    string PhoneNumber
);

public record VoicemailProcessRequest(
    string UserId,
    string AudioFilePath,
    string CallerNumber,
    string? PreferredLanguage
);

#endregion