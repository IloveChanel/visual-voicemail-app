using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Gms.Ads;

namespace VisualVoicemailPro.Platforms.Android
{
    /// <summary>
    /// Main Android Activity for Visual Voicemail Pro
    /// Initializes AdMob SDK and handles app lifecycle
    /// </summary>
    [Activity(
        Theme = "@style/Maui.SplashTheme", 
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density
    )]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Initialize AdMob SDK
            InitializeAdMob();
        }

        private void InitializeAdMob()
        {
            try
            {
                // Initialize Google Mobile Ads SDK
                MobileAds.Initialize(ApplicationContext, initializationStatus =>
                {
                    System.Diagnostics.Debug.WriteLine("AdMob SDK initialized successfully");
                    
                    // Log adapter status for debugging
                    var adapterStatusMap = initializationStatus.AdapterStatusMap;
                    foreach (var adapterClass in adapterStatusMap.Keys)
                    {
                        var status = adapterStatusMap[adapterClass];
                        System.Diagnostics.Debug.WriteLine($"Adapter: {adapterClass}, Status: {status.InitializationState}, Description: {status.Description}");
                    }
                });

                System.Diagnostics.Debug.WriteLine("AdMob initialization started");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize AdMob: {ex.Message}");
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            
            // Resume any paused ad activities if needed
            System.Diagnostics.Debug.WriteLine("MainActivity resumed");
        }

        protected override void OnPause()
        {
            base.OnPause();
            
            // Pause any active ad activities if needed
            System.Diagnostics.Debug.WriteLine("MainActivity paused");
        }
    }
}