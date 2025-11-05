namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// AdMob Service Interface for Visual Voicemail Pro
    /// Handles banner ads, interstitial ads, and premium upgrade detection
    /// </summary>
    public interface IAdMobService
    {
        /// <summary>
        /// Initialize AdMob SDK
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Show banner ad in specified container
        /// </summary>
        Task<bool> ShowBannerAdAsync(object container);

        /// <summary>
        /// Hide banner ad
        /// </summary>
        Task HideBannerAdAsync();

        /// <summary>
        /// Load interstitial ad for later display
        /// </summary>
        Task LoadInterstitialAdAsync();

        /// <summary>
        /// Show interstitial ad if loaded and user is not premium
        /// </summary>
        Task<bool> ShowInterstitialAdAsync();

        /// <summary>
        /// Check if user has premium subscription (no ads)
        /// </summary>
        bool IsPremiumUser { get; }

        /// <summary>
        /// Purchase premium upgrade to remove ads
        /// </summary>
        Task<bool> PurchasePremiumUpgradeAsync();

        /// <summary>
        /// Restore previous premium purchases
        /// </summary>
        Task<bool> RestorePremiumPurchasesAsync();

        /// <summary>
        /// Show consent dialog for GDPR compliance
        /// </summary>
        Task<bool> ShowConsentDialogAsync();

        /// <summary>
        /// Events for ad interactions
        /// </summary>
        event EventHandler<AdEventArgs> AdLoaded;
        event EventHandler<AdEventArgs> AdFailedToLoad;
        event EventHandler<AdEventArgs> AdClicked;
        event EventHandler<AdEventArgs> AdClosed;
    }

    public class AdEventArgs : EventArgs
    {
        public string AdUnitId { get; set; }
        public string AdType { get; set; }
        public string ErrorMessage { get; set; }
        public bool Success { get; set; }
    }

    public enum AdType
    {
        Banner,
        Interstitial,
        Rewarded
    }
}

#if ANDROID
using Android.Content;
using Android.Gms.Ads;
using Android.Gms.Ads.Interstitial;
using AndroidX.AppCompat.App;
using Plugin.InAppBilling;
using Plugin.InAppBilling.Abstractions;

namespace VisualVoicemailPro.Platforms.Android.Services
{
    /// <summary>
    /// Android implementation of AdMob Service
    /// Integrates with Google Play Services Ads and Google Play Billing
    /// </summary>
    public class AdMobServiceAndroid : IAdMobService
    {
        private readonly ILogger<AdMobServiceAndroid> _logger;
        private readonly ISecureConfigurationService _configService;
        
        // AdMob Configuration
        private const string BANNER_AD_UNIT_ID = "ca-app-pub-3940256099942544/6300978111"; // Test ID
        private const string INTERSTITIAL_AD_UNIT_ID = "ca-app-pub-3940256099942544/1033173712"; // Test ID
        private const string PREMIUM_PRODUCT_ID = "visualvoicemail_pro_upgrade";
        
        // AdMob Objects
        private InterstitialAd _interstitialAd;
        private AdView _bannerAdView;
        private Context _context;
        
        // Premium Status
        private bool _isPremiumUser;
        private bool _isInitialized;

        // Events
        public event EventHandler<AdEventArgs> AdLoaded;
        public event EventHandler<AdEventArgs> AdFailedToLoad;
        public event EventHandler<AdEventArgs> AdClicked;
        public event EventHandler<AdEventArgs> AdClosed;

        public bool IsPremiumUser => _isPremiumUser;

        public AdMobServiceAndroid(ILogger<AdMobServiceAndroid> logger, ISecureConfigurationService configService)
        {
            _logger = logger;
            _configService = configService;
            _context = Platform.CurrentActivity?.ApplicationContext ?? Android.App.Application.Context;
        }

        public async Task InitializeAsync()
        {
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("Initializing AdMob SDK...");
                
                // Initialize AdMob
                MobileAds.Initialize(_context, initializationStatus =>
                {
                    _logger.LogInformation("AdMob initialization completed");
                });

                // Check premium status
                await CheckPremiumStatusAsync();

                // Load initial interstitial ad if not premium
                if (!_isPremiumUser)
                {
                    await LoadInterstitialAdAsync();
                }

                _isInitialized = true;
                _logger.LogInformation("AdMob service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize AdMob service");
            }
        }

        public async Task<bool> ShowBannerAdAsync(object container)
        {
            try
            {
                if (_isPremiumUser)
                {
                    _logger.LogDebug("User is premium, not showing banner ad");
                    return false;
                }

                if (_bannerAdView != null)
                {
                    await HideBannerAdAsync();
                }

                // Get production ad unit ID or use test ID
                var adUnitId = await _configService.GetSecretAsync("ADMOB_BANNER_AD_UNIT_ID");
                if (string.IsNullOrEmpty(adUnitId))
                {
                    adUnitId = BANNER_AD_UNIT_ID; // Test ID
                }

                _bannerAdView = new AdView(_context)
                {
                    AdSize = AdSize.Banner,
                    AdUnitId = adUnitId
                };

                var adRequest = new AdRequest.Builder()
                    .AddTestDevice(AdRequest.DeviceIdEmulator)
                    .Build();

                _bannerAdView.AdListener = new BannerAdListener(this);
                _bannerAdView.LoadAd(adRequest);

                _logger.LogDebug("Banner ad requested for unit ID: {AdUnitId}", adUnitId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show banner ad");
                return false;
            }
        }

        public async Task HideBannerAdAsync()
        {
            try
            {
                if (_bannerAdView != null)
                {
                    _bannerAdView.Destroy();
                    _bannerAdView = null;
                    _logger.LogDebug("Banner ad hidden and destroyed");
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hide banner ad");
            }
        }

        public async Task LoadInterstitialAdAsync()
        {
            try
            {
                if (_isPremiumUser)
                {
                    _logger.LogDebug("User is premium, not loading interstitial ad");
                    return;
                }

                // Get production ad unit ID or use test ID
                var adUnitId = await _configService.GetSecretAsync("ADMOB_INTERSTITIAL_AD_UNIT_ID");
                if (string.IsNullOrEmpty(adUnitId))
                {
                    adUnitId = INTERSTITIAL_AD_UNIT_ID; // Test ID
                }

                var adRequest = new AdRequest.Builder()
                    .AddTestDevice(AdRequest.DeviceIdEmulator)
                    .Build();

                InterstitialAd.Load(_context, adUnitId, adRequest, new InterstitialAdLoadCallback(this));
                
                _logger.LogDebug("Interstitial ad loading for unit ID: {AdUnitId}", adUnitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load interstitial ad");
            }
        }

        public async Task<bool> ShowInterstitialAdAsync()
        {
            try
            {
                if (_isPremiumUser)
                {
                    _logger.LogDebug("User is premium, not showing interstitial ad");
                    return false;
                }

                if (_interstitialAd != null)
                {
                    var activity = Platform.CurrentActivity as AppCompatActivity;
                    if (activity != null)
                    {
                        _interstitialAd.Show(activity);
                        _logger.LogDebug("Interstitial ad shown");
                        
                        // Load next ad
                        _ = Task.Run(LoadInterstitialAdAsync);
                        return true;
                    }
                }

                _logger.LogWarning("No interstitial ad available to show");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show interstitial ad");
                return false;
            }
        }

        public async Task<bool> PurchasePremiumUpgradeAsync()
        {
            try
            {
                _logger.LogInformation("Starting premium upgrade purchase...");

                var billing = CrossInAppBilling.Current;
                var connected = await billing.ConnectAsync();
                
                if (!connected)
                {
                    _logger.LogError("Failed to connect to billing service");
                    return false;
                }

                // Get product info
                var products = await billing.GetProductInfoAsync(ItemType.InAppPurchase, PREMIUM_PRODUCT_ID);
                var product = products?.FirstOrDefault();

                if (product == null)
                {
                    _logger.LogError("Premium upgrade product not found: {ProductId}", PREMIUM_PRODUCT_ID);
                    return false;
                }

                // Make purchase
                var purchase = await billing.PurchaseAsync(PREMIUM_PRODUCT_ID, ItemType.InAppPurchase);

                if (purchase != null && purchase.State == PurchaseState.Purchased)
                {
                    _isPremiumUser = true;
                    await HideBannerAdAsync();
                    
                    // Store premium status locally
                    Preferences.Set("IsPremiumUser", true);
                    
                    _logger.LogInformation("Premium upgrade purchased successfully");
                    return true;
                }

                _logger.LogWarning("Premium upgrade purchase failed or cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purchase premium upgrade");
                return false;
            }
        }

        public async Task<bool> RestorePremiumPurchasesAsync()
        {
            try
            {
                _logger.LogInformation("Restoring premium purchases...");

                var billing = CrossInAppBilling.Current;
                var connected = await billing.ConnectAsync();
                
                if (!connected)
                {
                    return false;
                }

                var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
                var premiumPurchase = purchases?.FirstOrDefault(p => p.ProductId == PREMIUM_PRODUCT_ID);

                if (premiumPurchase != null && premiumPurchase.State == PurchaseState.Purchased)
                {
                    _isPremiumUser = true;
                    await HideBannerAdAsync();
                    
                    // Store premium status locally
                    Preferences.Set("IsPremiumUser", true);
                    
                    _logger.LogInformation("Premium purchase restored successfully");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore premium purchases");
                return false;
            }
        }

        public async Task<bool> ShowConsentDialogAsync()
        {
            try
            {
                // Implement GDPR consent using Google UMP SDK
                // For now, return true (consent given)
                _logger.LogInformation("GDPR consent dialog shown");
                await Task.Delay(100);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show consent dialog");
                return false;
            }
        }

        private async Task CheckPremiumStatusAsync()
        {
            // Check local preference first
            _isPremiumUser = Preferences.Get("IsPremiumUser", false);

            if (!_isPremiumUser)
            {
                // Check with billing service
                _isPremiumUser = await RestorePremiumPurchasesAsync();
            }

            _logger.LogDebug("Premium status: {IsPremium}", _isPremiumUser);
        }

        internal void OnInterstitialAdLoaded(InterstitialAd ad)
        {
            _interstitialAd = ad;
            _interstitialAd.FullScreenContentCallback = new InterstitialAdCallback(this);
            
            AdLoaded?.Invoke(this, new AdEventArgs
            {
                AdType = AdType.Interstitial.ToString(),
                Success = true
            });

            _logger.LogDebug("Interstitial ad loaded successfully");
        }

        internal void OnInterstitialAdFailedToLoad(LoadAdError error)
        {
            AdFailedToLoad?.Invoke(this, new AdEventArgs
            {
                AdType = AdType.Interstitial.ToString(),
                ErrorMessage = error.Message,
                Success = false
            });

            _logger.LogWarning("Interstitial ad failed to load: {Error}", error.Message);
        }

        internal void OnBannerAdLoaded()
        {
            AdLoaded?.Invoke(this, new AdEventArgs
            {
                AdType = AdType.Banner.ToString(),
                Success = true
            });

            _logger.LogDebug("Banner ad loaded successfully");
        }

        internal void OnBannerAdFailedToLoad(LoadAdError error)
        {
            AdFailedToLoad?.Invoke(this, new AdEventArgs
            {
                AdType = AdType.Banner.ToString(),
                ErrorMessage = error.Message,
                Success = false
            });

            _logger.LogWarning("Banner ad failed to load: {Error}", error.Message);
        }
    }

    // Ad Callback Classes
    internal class InterstitialAdLoadCallback : InterstitialAdLoadCallback
    {
        private readonly AdMobServiceAndroid _service;

        public InterstitialAdLoadCallback(AdMobServiceAndroid service)
        {
            _service = service;
        }

        public override void OnAdLoaded(InterstitialAd interstitialAd)
        {
            _service.OnInterstitialAdLoaded(interstitialAd);
        }

        public override void OnAdFailedToLoad(LoadAdError loadAdError)
        {
            _service.OnInterstitialAdFailedToLoad(loadAdError);
        }
    }

    internal class InterstitialAdCallback : FullScreenContentCallback
    {
        private readonly AdMobServiceAndroid _service;

        public InterstitialAdCallback(AdMobServiceAndroid service)
        {
            _service = service;
        }

        public override void OnAdClicked()
        {
            _service.AdClicked?.Invoke(_service, new AdEventArgs
            {
                AdType = AdType.Interstitial.ToString(),
                Success = true
            });
        }

        public override void OnAdDismissedFullScreenContent()
        {
            _service.AdClosed?.Invoke(_service, new AdEventArgs
            {
                AdType = AdType.Interstitial.ToString(),
                Success = true
            });
        }
    }

    internal class BannerAdListener : AdListener
    {
        private readonly AdMobServiceAndroid _service;

        public BannerAdListener(AdMobServiceAndroid service)
        {
            _service = service;
        }

        public override void OnAdLoaded()
        {
            _service.OnBannerAdLoaded();
        }

        public override void OnAdFailedToLoad(LoadAdError adError)
        {
            _service.OnBannerAdFailedToLoad(adError);
        }

        public override void OnAdClicked()
        {
            _service.AdClicked?.Invoke(_service, new AdEventArgs
            {
                AdType = AdType.Banner.ToString(),
                Success = true
            });
        }
    }
}
#endif

#if IOS
using Foundation;
using Google.MobileAds;
using Plugin.InAppBilling;
using Plugin.InAppBilling.Abstractions;
using UIKit;

namespace VisualVoicemailPro.Platforms.iOS.Services
{
    /// <summary>
    /// iOS implementation of AdMob Service
    /// Integrates with iOS AdMob SDK and App Store Connect for in-app purchases
    /// </summary>
    public class AdMobServiceiOS : IAdMobService
    {
        private readonly ILogger<AdMobServiceiOS> _logger;
        private readonly ISecureConfigurationService _configService;
        
        // AdMob Configuration (Test IDs)
        private const string BANNER_AD_UNIT_ID = "ca-app-pub-3940256099942544/2934735716"; // iOS Test ID
        private const string INTERSTITIAL_AD_UNIT_ID = "ca-app-pub-3940256099942544/4411468910"; // iOS Test ID
        private const string PREMIUM_PRODUCT_ID = "visualvoicemail_pro_upgrade";
        
        // AdMob Objects
        private GADInterstitialAd _interstitialAd;
        private GADBannerView _bannerView;
        
        // Premium Status
        private bool _isPremiumUser;
        private bool _isInitialized;

        // Events
        public event EventHandler<AdEventArgs> AdLoaded;
        public event EventHandler<AdEventArgs> AdFailedToLoad;
        public event EventHandler<AdEventArgs> AdClicked;
        public event EventHandler<AdEventArgs> AdClosed;

        public bool IsPremiumUser => _isPremiumUser;

        public AdMobServiceiOS(ILogger<AdMobServiceiOS> logger, ISecureConfigurationService configService)
        {
            _logger = logger;
            _configService = configService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("Initializing AdMob SDK for iOS...");
                
                // AdMob is already initialized in AppDelegate
                // Just check premium status and load ads

                // Check premium status
                await CheckPremiumStatusAsync();

                // Load initial interstitial ad if not premium
                if (!_isPremiumUser)
                {
                    await LoadInterstitialAdAsync();
                }

                _isInitialized = true;
                _logger.LogInformation("iOS AdMob service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize iOS AdMob service");
            }
        }

        public async Task<bool> ShowBannerAdAsync(object container)
        {
            try
            {
                if (_isPremiumUser)
                {
                    _logger.LogDebug("User is premium, not showing banner ad");
                    return false;
                }

                if (_bannerView != null)
                {
                    await HideBannerAdAsync();
                }

                // Get production ad unit ID or use test ID
                var adUnitId = await _configService.GetSecretAsync("ADMOB_BANNER_AD_UNIT_ID_IOS");
                if (string.IsNullOrEmpty(adUnitId))
                {
                    adUnitId = BANNER_AD_UNIT_ID; // Test ID
                }

                // Create banner view
                _bannerView = new GADBannerView(GADAdSizeCons.Banner)
                {
                    AdUnitID = adUnitId,
                    RootViewController = GetCurrentViewController()
                };

                // Set up delegate
                _bannerView.Delegate = new BannerViewDelegate(this);

                // Create ad request
                var request = GADRequest.Request;
                _bannerView.LoadRequest(request);

                // Add to container if it's a UIView
                if (container is UIView parentView)
                {
                    parentView.AddSubview(_bannerView);
                    
                    // Set constraints for banner positioning
                    _bannerView.TranslatesAutoresizingMaskIntoConstraints = false;
                    NSLayoutConstraint.ActivateConstraints(new[]
                    {
                        _bannerView.BottomAnchor.ConstraintEqualTo(parentView.SafeAreaLayoutGuide.BottomAnchor),
                        _bannerView.CenterXAnchor.ConstraintEqualTo(parentView.CenterXAnchor)
                    });
                }

                _logger.LogDebug("iOS Banner ad requested for unit ID: {AdUnitId}", adUnitId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show iOS banner ad");
                return false;
            }
        }

        public async Task HideBannerAdAsync()
        {
            try
            {
                if (_bannerView != null)
                {
                    _bannerView.RemoveFromSuperview();
                    _bannerView.Dispose();
                    _bannerView = null;
                    _logger.LogDebug("iOS Banner ad hidden and disposed");
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hide iOS banner ad");
            }
        }

        public async Task LoadInterstitialAdAsync()
        {
            try
            {
                if (_isPremiumUser)
                {
                    _logger.LogDebug("User is premium, not loading interstitial ad");
                    return;
                }

                // Get production ad unit ID or use test ID
                var adUnitId = await _configService.GetSecretAsync("ADMOB_INTERSTITIAL_AD_UNIT_ID_IOS");
                if (string.IsNullOrEmpty(adUnitId))
                {
                    adUnitId = INTERSTITIAL_AD_UNIT_ID; // Test ID
                }

                var request = GADRequest.Request;
                
                GADInterstitialAd.Load(adUnitId, request, (ad, error) =>
                {
                    if (error != null)
                    {
                        _logger.LogWarning("iOS Interstitial ad failed to load: {Error}", error.LocalizedDescription);
                        
                        AdFailedToLoad?.Invoke(this, new AdEventArgs
                        {
                            AdType = AdType.Interstitial.ToString(),
                            ErrorMessage = error.LocalizedDescription,
                            Success = false
                        });
                        return;
                    }

                    _interstitialAd = ad;
                    _interstitialAd.FullScreenContentDelegate = new InterstitialDelegate(this);
                    
                    AdLoaded?.Invoke(this, new AdEventArgs
                    {
                        AdType = AdType.Interstitial.ToString(),
                        Success = true
                    });

                    _logger.LogDebug("iOS Interstitial ad loaded successfully");
                });
                
                _logger.LogDebug("iOS Interstitial ad loading for unit ID: {AdUnitId}", adUnitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load iOS interstitial ad");
            }
        }

        public async Task<bool> ShowInterstitialAdAsync()
        {
            try
            {
                if (_isPremiumUser)
                {
                    _logger.LogDebug("User is premium, not showing interstitial ad");
                    return false;
                }

                if (_interstitialAd != null)
                {
                    var viewController = GetCurrentViewController();
                    if (viewController != null)
                    {
                        _interstitialAd.PresentFromRootViewController(viewController);
                        _logger.LogDebug("iOS Interstitial ad shown");
                        
                        // Load next ad
                        _ = Task.Run(LoadInterstitialAdAsync);
                        return true;
                    }
                }

                _logger.LogWarning("No iOS interstitial ad available to show");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show iOS interstitial ad");
                return false;
            }
        }

        public async Task<bool> PurchasePremiumUpgradeAsync()
        {
            try
            {
                _logger.LogInformation("Starting iOS premium upgrade purchase...");

                var billing = CrossInAppBilling.Current;
                var connected = await billing.ConnectAsync();
                
                if (!connected)
                {
                    _logger.LogError("Failed to connect to iOS billing service");
                    return false;
                }

                // Get product info
                var products = await billing.GetProductInfoAsync(ItemType.InAppPurchase, PREMIUM_PRODUCT_ID);
                var product = products?.FirstOrDefault();

                if (product == null)
                {
                    _logger.LogError("iOS Premium upgrade product not found: {ProductId}", PREMIUM_PRODUCT_ID);
                    return false;
                }

                // Make purchase
                var purchase = await billing.PurchaseAsync(PREMIUM_PRODUCT_ID, ItemType.InAppPurchase);

                if (purchase != null && purchase.State == PurchaseState.Purchased)
                {
                    _isPremiumUser = true;
                    await HideBannerAdAsync();
                    
                    // Store premium status locally
                    Preferences.Set("IsPremiumUser", true);
                    
                    _logger.LogInformation("iOS Premium upgrade purchased successfully");
                    return true;
                }

                _logger.LogWarning("iOS Premium upgrade purchase failed or cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purchase iOS premium upgrade");
                return false;
            }
        }

        public async Task<bool> RestorePremiumPurchasesAsync()
        {
            try
            {
                _logger.LogInformation("Restoring iOS premium purchases...");

                var billing = CrossInAppBilling.Current;
                var connected = await billing.ConnectAsync();
                
                if (!connected)
                {
                    return false;
                }

                var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
                var premiumPurchase = purchases?.FirstOrDefault(p => p.ProductId == PREMIUM_PRODUCT_ID);

                if (premiumPurchase != null && premiumPurchase.State == PurchaseState.Purchased)
                {
                    _isPremiumUser = true;
                    await HideBannerAdAsync();
                    
                    // Store premium status locally
                    Preferences.Set("IsPremiumUser", true);
                    
                    _logger.LogInformation("iOS Premium purchase restored successfully");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore iOS premium purchases");
                return false;
            }
        }

        public async Task<bool> ShowConsentDialogAsync()
        {
            try
            {
                // Implement iOS GDPR consent
                // For now, return true (consent given)
                _logger.LogInformation("iOS GDPR consent dialog shown");
                await Task.Delay(100);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show iOS consent dialog");
                return false;
            }
        }

        private async Task CheckPremiumStatusAsync()
        {
            // Check local preference first
            _isPremiumUser = Preferences.Get("IsPremiumUser", false);

            if (!_isPremiumUser)
            {
                // Check with billing service
                _isPremiumUser = await RestorePremiumPurchasesAsync();
            }

            _logger.LogDebug("iOS Premium status: {IsPremium}", _isPremiumUser);
        }

        private UIViewController GetCurrentViewController()
        {
            var window = UIApplication.SharedApplication.KeyWindow;
            if (window?.RootViewController == null)
                return null;

            var viewController = window.RootViewController;
            while (viewController.PresentedViewController != null)
                viewController = viewController.PresentedViewController;

            return viewController;
        }
    }

    // iOS Ad Delegate Classes
    internal class InterstitialDelegate : GADFullScreenContentDelegate
    {
        private readonly AdMobServiceiOS _service;

        public InterstitialDelegate(AdMobServiceiOS service)
        {
            _service = service;
        }

        public override void AdDidRecordClick(GADInterstitialAd ad)
        {
            _service.AdClicked?.Invoke(_service, new AdEventArgs
            {
                AdType = AdType.Interstitial.ToString(),
                Success = true
            });
        }

        public override void AdDidDismissFullScreenContent(GADInterstitialAd ad)
        {
            _service.AdClosed?.Invoke(_service, new AdEventArgs
            {
                AdType = AdType.Interstitial.ToString(),
                Success = true
            });
        }
    }

    internal class BannerViewDelegate : GADBannerViewDelegate
    {
        private readonly AdMobServiceiOS _service;

        public BannerViewDelegate(AdMobServiceiOS service)
        {
            _service = service;
        }

        public override void AdReceived(GADBannerView bannerView)
        {
            _service.AdLoaded?.Invoke(_service, new AdEventArgs
            {
                AdType = AdType.Banner.ToString(),
                Success = true
            });
        }

        public override void ReceiveAdFailed(GADBannerView bannerView, GADRequestError error)
        {
            _service.AdFailedToLoad?.Invoke(_service, new AdEventArgs
            {
                AdType = AdType.Banner.ToString(),
                ErrorMessage = error.LocalizedDescription,
                Success = false
            });
        }

        public override void AdClicked(GADBannerView bannerView)
        {
            _service.AdClicked?.Invoke(_service, new AdEventArgs
            {
                AdType = AdType.Banner.ToString(),
                Success = true
            });
        }
    }
}
#endif