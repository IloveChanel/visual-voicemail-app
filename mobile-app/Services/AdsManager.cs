#if ANDROID
using Android.Gms.Ads;
using Android.Gms.Ads.Interstitial;
using Android.Content;
using AndroidX.AppCompat.App;
#endif

#if IOS
using Google.MobileAds;
using UIKit;
#endif

using Plugin.InAppBilling;
using Plugin.InAppBilling.Abstractions;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// Cross-platform Ads Manager for Visual Voicemail Pro
    /// Handles AdMob integration and premium upgrade functionality
    /// </summary>
    public interface IAdsManager
    {
        bool IsPremiumUser { get; }
        bool ShowAds { get; }
        
        Task InitializeAsync();
        Task LoadInterstitialAdAsync();
        Task ShowInterstitialAdAsync();
        Task<bool> PurchasePremiumAsync();
        Task<bool> RestorePurchasesAsync();
        
        event EventHandler<bool> PremiumStatusChanged;
    }

#if ANDROID
    public class AdsManager : IAdsManager
    {
        private InterstitialAd _interstitialAd;
        private Context _context;
        
        // Test Ad Unit IDs (replace with your production IDs)
        private readonly string _interstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
        private readonly string _premiumProductId = "visualvoicemail_pro_upgrade";
        
        private bool _isPremiumUser;
        public bool IsPremiumUser 
        { 
            get => _isPremiumUser;
            private set
            {
                if (_isPremiumUser != value)
                {
                    _isPremiumUser = value;
                    Preferences.Set("IsPremiumUser", value);
                    PremiumStatusChanged?.Invoke(this, value);
                }
            }
        }
        
        public bool ShowAds => !IsPremiumUser;

        public event EventHandler<bool> PremiumStatusChanged;

        public AdsManager()
        {
            _context = Platform.CurrentActivity?.ApplicationContext ?? Android.App.Application.Context;
            _isPremiumUser = Preferences.Get("IsPremiumUser", false);
        }

        public async Task InitializeAsync()
        {
            try
            {
                // AdMob is already initialized in MainActivity
                await CheckPremiumStatusAsync();
                
                if (!IsPremiumUser)
                {
                    await LoadInterstitialAdAsync();
                }
                
                System.Diagnostics.Debug.WriteLine("AdsManager initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AdsManager initialization failed: {ex.Message}");
            }
        }

        public async Task LoadInterstitialAdAsync()
        {
            try
            {
                if (IsPremiumUser) return;

                var adRequest = new AdRequest.Builder().Build();
                
                InterstitialAd.Load(_context, _interstitialUnitId, adRequest,
                    new InterstitialAdLoadCallback()
                    {
                        OnAdLoaded = (ad) => 
                        {
                            _interstitialAd = ad;
                            System.Diagnostics.Debug.WriteLine("Interstitial ad loaded");
                        },
                        OnAdFailedToLoad = (error) => 
                        {
                            System.Diagnostics.Debug.WriteLine($"Interstitial ad failed to load: {error.Message}");
                        }
                    });
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load interstitial ad: {ex.Message}");
            }
        }

        public async Task ShowInterstitialAdAsync()
        {
            try
            {
                if (IsPremiumUser || _interstitialAd == null) return;

                var activity = Platform.CurrentActivity as AppCompatActivity;
                if (activity != null)
                {
                    _interstitialAd.Show(activity);
                    System.Diagnostics.Debug.WriteLine("Interstitial ad shown");
                    
                    // Load next ad
                    _ = Task.Run(LoadInterstitialAdAsync);
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show interstitial ad: {ex.Message}");
            }
        }

        public async Task<bool> PurchasePremiumAsync()
        {
            try
            {
                var billing = CrossInAppBilling.Current;
                var connected = await billing.ConnectAsync();
                
                if (!connected)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to connect to billing service");
                    return false;
                }

                var products = await billing.GetProductInfoAsync(ItemType.InAppPurchase, _premiumProductId);
                var product = products?.FirstOrDefault();

                if (product == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Premium product not found: {_premiumProductId}");
                    return false;
                }

                var purchase = await billing.PurchaseAsync(_premiumProductId, ItemType.InAppPurchase);

                if (purchase?.State == PurchaseState.Purchased)
                {
                    IsPremiumUser = true;
                    System.Diagnostics.Debug.WriteLine("Premium upgrade purchased successfully");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Premium purchase failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RestorePurchasesAsync()
        {
            try
            {
                var billing = CrossInAppBilling.Current;
                var connected = await billing.ConnectAsync();
                
                if (!connected) return false;

                var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
                var premiumPurchase = purchases?.FirstOrDefault(p => p.ProductId == _premiumProductId);

                if (premiumPurchase?.State == PurchaseState.Purchased)
                {
                    IsPremiumUser = true;
                    System.Diagnostics.Debug.WriteLine("Premium purchase restored");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Restore purchases failed: {ex.Message}");
                return false;
            }
        }

        private async Task CheckPremiumStatusAsync()
        {
            // First check local preferences
            var localPremium = Preferences.Get("IsPremiumUser", false);
            
            if (localPremium)
            {
                IsPremiumUser = true;
                return;
            }
            
            // Then verify with store
            var restored = await RestorePurchasesAsync();
            if (!restored)
            {
                IsPremiumUser = false;
            }
        }
    }

#elif IOS
    public class AdsManager : IAdsManager
    {
        private GADInterstitialAd _interstitialAd;
        
        // Test Ad Unit IDs (replace with your production IDs)
        private readonly string _interstitialUnitId = "ca-app-pub-3940256099942544/4411468910";
        private readonly string _premiumProductId = "visualvoicemail_pro_upgrade";
        
        private bool _isPremiumUser;
        public bool IsPremiumUser 
        { 
            get => _isPremiumUser;
            private set
            {
                if (_isPremiumUser != value)
                {
                    _isPremiumUser = value;
                    Preferences.Set("IsPremiumUser", value);
                    PremiumStatusChanged?.Invoke(this, value);
                }
            }
        }
        
        public bool ShowAds => !IsPremiumUser;

        public event EventHandler<bool> PremiumStatusChanged;

        public AdsManager()
        {
            _isPremiumUser = Preferences.Get("IsPremiumUser", false);
        }

        public async Task InitializeAsync()
        {
            try
            {
                // AdMob is already initialized in AppDelegate
                await CheckPremiumStatusAsync();
                
                if (!IsPremiumUser)
                {
                    await LoadInterstitialAdAsync();
                }
                
                System.Diagnostics.Debug.WriteLine("iOS AdsManager initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iOS AdsManager initialization failed: {ex.Message}");
            }
        }

        public async Task LoadInterstitialAdAsync()
        {
            try
            {
                if (IsPremiumUser) return;

                var request = GADRequest.Request;
                
                GADInterstitialAd.Load(_interstitialUnitId, request, (ad, error) =>
                {
                    if (error != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"iOS Interstitial ad failed to load: {error.LocalizedDescription}");
                        return;
                    }

                    _interstitialAd = ad;
                    System.Diagnostics.Debug.WriteLine("iOS Interstitial ad loaded");
                });
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load iOS interstitial ad: {ex.Message}");
            }
        }

        public async Task ShowInterstitialAdAsync()
        {
            try
            {
                if (IsPremiumUser || _interstitialAd == null) return;

                var viewController = GetCurrentViewController();
                if (viewController != null)
                {
                    _interstitialAd.PresentFromRootViewController(viewController);
                    System.Diagnostics.Debug.WriteLine("iOS Interstitial ad shown");
                    
                    // Load next ad
                    _ = Task.Run(LoadInterstitialAdAsync);
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show iOS interstitial ad: {ex.Message}");
            }
        }

        public async Task<bool> PurchasePremiumAsync()
        {
            try
            {
                var billing = CrossInAppBilling.Current;
                var connected = await billing.ConnectAsync();
                
                if (!connected) return false;

                var products = await billing.GetProductInfoAsync(ItemType.InAppPurchase, _premiumProductId);
                var product = products?.FirstOrDefault();

                if (product == null) return false;

                var purchase = await billing.PurchaseAsync(_premiumProductId, ItemType.InAppPurchase);

                if (purchase?.State == PurchaseState.Purchased)
                {
                    IsPremiumUser = true;
                    System.Diagnostics.Debug.WriteLine("iOS Premium upgrade purchased successfully");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iOS Premium purchase failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RestorePurchasesAsync()
        {
            try
            {
                var billing = CrossInAppBilling.Current;
                var connected = await billing.ConnectAsync();
                
                if (!connected) return false;

                var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
                var premiumPurchase = purchases?.FirstOrDefault(p => p.ProductId == _premiumProductId);

                if (premiumPurchase?.State == PurchaseState.Purchased)
                {
                    IsPremiumUser = true;
                    System.Diagnostics.Debug.WriteLine("iOS Premium purchase restored");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iOS Restore purchases failed: {ex.Message}");
                return false;
            }
        }

        private async Task CheckPremiumStatusAsync()
        {
            var localPremium = Preferences.Get("IsPremiumUser", false);
            
            if (localPremium)
            {
                IsPremiumUser = true;
                return;
            }
            
            var restored = await RestorePurchasesAsync();
            if (!restored)
            {
                IsPremiumUser = false;
            }
        }

        private UIViewController GetCurrentViewController()
        {
            var window = UIApplication.SharedApplication.KeyWindow;
            if (window?.RootViewController == null) return null;

            var viewController = window.RootViewController;
            while (viewController.PresentedViewController != null)
                viewController = viewController.PresentedViewController;

            return viewController;
        }
    }

#else
    // Stub for other platforms (Windows, macOS, etc.)
    public class AdsManager : IAdsManager
    {
        public bool IsPremiumUser { get; private set; } = false;
        public bool ShowAds => !IsPremiumUser;

        public event EventHandler<bool> PremiumStatusChanged;

        public async Task InitializeAsync()
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("Stub AdsManager initialized");
        }

        public async Task LoadInterstitialAdAsync()
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("Stub: Interstitial ad loaded");
        }

        public async Task ShowInterstitialAdAsync()
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("Stub: Interstitial ad shown");
        }

        public async Task<bool> PurchasePremiumAsync()
        {
            await Task.Delay(1000);
            IsPremiumUser = true;
            PremiumStatusChanged?.Invoke(this, true);
            System.Diagnostics.Debug.WriteLine("Stub: Premium upgrade purchased");
            return true;
        }

        public async Task<bool> RestorePurchasesAsync()
        {
            await Task.Delay(1000);
            System.Diagnostics.Debug.WriteLine("Stub: Purchases restored");
            return false;
        }
    }
#endif

    /// <summary>
    /// Interstitial Ad Load Callback for Android
    /// </summary>
#if ANDROID
    public class InterstitialAdLoadCallback : Java.Lang.Object, Android.Gms.Ads.Interstitial.IInterstitialAdLoadCallback
    {
        public Action<InterstitialAd> OnAdLoaded { get; set; }
        public Action<LoadAdError> OnAdFailedToLoad { get; set; }

        public void OnAdLoaded(Java.Lang.Object interstitialAd)
        {
            OnAdLoaded?.Invoke(interstitialAd as InterstitialAd);
        }

        public void OnAdFailedToLoad(LoadAdError loadAdError)
        {
            OnAdFailedToLoad?.Invoke(loadAdError);
        }
    }
#endif
}