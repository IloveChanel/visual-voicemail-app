using Foundation;
using Google.MobileAds;
using UIKit;

namespace VisualVoicemailPro.Platforms.iOS
{
    /// <summary>
    /// iOS App Delegate for Visual Voicemail Pro
    /// Initializes AdMob SDK and handles iOS app lifecycle
    /// </summary>
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            // Initialize AdMob SDK for iOS
            InitializeAdMob();

            return base.FinishedLaunching(app, options);
        }

        private void InitializeAdMob()
        {
            try
            {
                // Initialize Google Mobile Ads SDK
                MobileAds.SharedInstance.Start((initializationStatus) =>
                {
                    System.Diagnostics.Debug.WriteLine("AdMob SDK initialized successfully on iOS");
                    
                    // Log adapter status for debugging
                    if (initializationStatus?.AdapterStatusesByClassName != null)
                    {
                        foreach (var adapter in initializationStatus.AdapterStatusesByClassName)
                        {
                            var status = adapter.Value;
                            System.Diagnostics.Debug.WriteLine($"iOS Adapter: {adapter.Key}, Status: {status.State}, Description: {status.Description}");
                        }
                    }
                });

                // Set request configuration for iOS
                var requestConfiguration = MobileAds.SharedInstance.RequestConfiguration;
                requestConfiguration.TestDeviceIdentifiers = new string[] { GADSimulatorID.Identifier };
                MobileAds.SharedInstance.RequestConfiguration = requestConfiguration;

                System.Diagnostics.Debug.WriteLine("AdMob initialization started on iOS");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize AdMob on iOS: {ex.Message}");
            }
        }

        public override void WillEnterForeground(UIApplication application)
        {
            base.WillEnterForeground(application);
            System.Diagnostics.Debug.WriteLine("iOS App entering foreground");
        }

        public override void DidEnterBackground(UIApplication application)
        {
            base.DidEnterBackground(application);
            System.Diagnostics.Debug.WriteLine("iOS App entering background");
        }
    }
}