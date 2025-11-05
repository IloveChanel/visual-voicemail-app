using VisualVoicemailPro.Services;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// Stub implementation of AdMob Service for testing on unsupported platforms
    /// </summary>
    public class AdMobServiceStub : IAdMobService
    {
        public bool IsPremiumUser { get; private set; } = false;

        public event EventHandler<AdEventArgs> AdLoaded;
        public event EventHandler<AdEventArgs> AdFailedToLoad;
        public event EventHandler<AdEventArgs> AdClicked;
        public event EventHandler<AdEventArgs> AdClosed;

        public async Task InitializeAsync()
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("AdMob Stub: Initialized");
        }

        public async Task<bool> ShowBannerAdAsync(object container)
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("AdMob Stub: Banner ad shown");
            return true;
        }

        public async Task HideBannerAdAsync()
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("AdMob Stub: Banner ad hidden");
        }

        public async Task LoadInterstitialAdAsync()
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("AdMob Stub: Interstitial ad loaded");
        }

        public async Task<bool> ShowInterstitialAdAsync()
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("AdMob Stub: Interstitial ad shown");
            return true;
        }

        public async Task<bool> PurchasePremiumUpgradeAsync()
        {
            await Task.Delay(1000);
            IsPremiumUser = true;
            System.Diagnostics.Debug.WriteLine("AdMob Stub: Premium upgrade purchased");
            return true;
        }

        public async Task<bool> RestorePremiumPurchasesAsync()
        {
            await Task.Delay(1000);
            System.Diagnostics.Debug.WriteLine("AdMob Stub: Purchases restored");
            return false;
        }

        public async Task<bool> ShowConsentDialogAsync()
        {
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("AdMob Stub: Consent dialog shown");
            return true;
        }
    }
}