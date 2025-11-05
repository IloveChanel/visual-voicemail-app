using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using VisualVoicemailPro.Services;
using VisualVoicemailPro.ViewModels;
using VisualVoicemailPro.Views;

namespace VisualVoicemailPro;

/// <summary>
/// Visual Voicemail Pro - MAUI App Entry Point
/// Supports $3.49/month subscription with advanced AI features
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Add logging
        builder.Services.AddLogging(configure => configure.AddDebug());

        // Register HTTP client for API calls
        builder.Services.AddHttpClient("VisualVoicemailAPI", client =>
        {
            client.BaseAddress = new Uri("https://your-api.azurewebsites.net/");
            client.DefaultRequestHeaders.Add("User-Agent", "VisualVoicemailPro/2.0");
        });

        // Register services
        builder.Services.AddSingleton<IApiService, ApiService>();
        builder.Services.AddSingleton<IVoicemailService, VoicemailService>();
        builder.Services.AddSingleton<ISubscriptionService, SubscriptionService>();
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddSingleton<IStorageService, StorageService>();
        
        // Enhanced AI Services for Pro features
        builder.Services.AddSingleton<EnhancedSpeechService>();
        builder.Services.AddSingleton<EnhancedTranslationService>();
        builder.Services.AddSingleton<StripeIntegrationService>();
        
        // AdMob Service (platform-specific)
#if ANDROID
        builder.Services.AddSingleton<IAdMobService, VisualVoicemailPro.Platforms.Android.Services.AdMobServiceAndroid>();
#elif IOS
        builder.Services.AddSingleton<IAdMobService, VisualVoicemailPro.Platforms.iOS.Services.AdMobServiceiOS>();
#else
        builder.Services.AddSingleton<IAdMobService, AdMobServiceStub>(); // For testing on other platforms
#endif
        
        // Register ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<EnhancedMainViewModel>();
        builder.Services.AddTransient<VoicemailViewModel>(); // New integrated ViewModel
        builder.Services.AddTransient<SubscriptionViewModel>();
        builder.Services.AddTransient<VoicemailDetailViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Register new AdsManager service
        builder.Services.AddSingleton<IAdsManager, AdsManager>();

        // Register Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SubscriptionPage>();
        builder.Services.AddTransient<VoicemailDetailPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}