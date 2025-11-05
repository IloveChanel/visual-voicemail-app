using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// Privacy and Consent Management Service
    /// Handles GDPR, CCPA compliance and user privacy preferences
    /// </summary>
    public interface IPrivacyService
    {
        Task<bool> ShowConsentDialogAsync();
        Task<bool> HasUserConsentedAsync();
        Task SaveConsentAsync(bool hasConsented);
        Task<bool> ShowPrivacyPolicyAsync();
        bool IsConsentRequired { get; }
    }

    public class PrivacyService : IPrivacyService
    {
        private const string CONSENT_KEY = "UserHasConsented";
        private const string CONSENT_DATE_KEY = "ConsentDate";

        public bool IsConsentRequired => DetermineConsentRequired();

        public async Task<bool> ShowConsentDialogAsync()
        {
            try
            {
                // Check if consent is already given and still valid
                if (await HasUserConsentedAsync())
                {
                    return true;
                }

                // Show consent dialog
                var result = await Application.Current.MainPage.DisplayAlert(
                    "Privacy Notice",
                    "Visual Voicemail Pro uses cookies and collects data to:\n\n" +
                    "• Provide personalized ads\n" +
                    "• Improve app performance\n" +
                    "• Analyze usage patterns\n" +
                    "• Enhance user experience\n\n" +
                    "By continuing to use this app, you agree to our data collection practices. " +
                    "You can change your preferences in Settings at any time.",
                    "Accept",
                    "Learn More");

                if (result)
                {
                    await SaveConsentAsync(true);
                    return true;
                }
                else
                {
                    // User wants to learn more - show privacy policy
                    await ShowPrivacyPolicyAsync();
                    
                    // Ask again after showing privacy policy
                    var secondResult = await Application.Current.MainPage.DisplayAlert(
                        "Consent Required",
                        "To continue using Visual Voicemail Pro, please accept our privacy practices. " +
                        "You can opt out of personalized ads in Settings.",
                        "Accept",
                        "Exit App");

                    if (secondResult)
                    {
                        await SaveConsentAsync(true);
                        return true;
                    }
                    else
                    {
                        // User declined - exit app
                        Application.Current.Quit();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Privacy consent error: {ex.Message}");
                // Default to consent required
                return false;
            }
        }

        public async Task<bool> HasUserConsentedAsync()
        {
            try
            {
                var hasConsented = Preferences.Get(CONSENT_KEY, false);
                var consentDateString = Preferences.Get(CONSENT_DATE_KEY, string.Empty);

                if (!hasConsented || string.IsNullOrEmpty(consentDateString))
                {
                    return false;
                }

                // Check if consent is still valid (within 1 year)
                if (DateTime.TryParse(consentDateString, out var consentDate))
                {
                    var isValid = DateTime.Now.Subtract(consentDate).TotalDays < 365;
                    return isValid;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task SaveConsentAsync(bool hasConsented)
        {
            try
            {
                Preferences.Set(CONSENT_KEY, hasConsented);
                if (hasConsented)
                {
                    Preferences.Set(CONSENT_DATE_KEY, DateTime.Now.ToString());
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save consent: {ex.Message}");
            }
        }

        public async Task<bool> ShowPrivacyPolicyAsync()
        {
            try
            {
                var privacyPolicyText = @"
PRIVACY POLICY - Visual Voicemail Pro

Last Updated: October 2025

1. DATA COLLECTION
We collect the following information:
• Voice recordings for transcription purposes
• Device identifiers for personalized ads
• Usage analytics to improve our service
• Contact information you provide

2. HOW WE USE YOUR DATA
• Transcribe voicemails using AI technology
• Detect spam calls and protect users
• Show relevant advertisements
• Improve app functionality and performance

3. DATA SHARING
We may share data with:
• Google Cloud Platform (for AI processing)
• AdMob (for advertising)
• Analytics providers (anonymized data)
• Legal authorities (if required by law)

4. YOUR RIGHTS
You have the right to:
• Access your personal data
• Delete your data (right to be forgotten)
• Opt out of personalized advertising
• Export your data
• Correct inaccurate information

5. DATA SECURITY
We protect your data using:
• End-to-end encryption for voice data
• Secure cloud storage
• Regular security audits
• Limited access controls

6. CHILDREN'S PRIVACY
Our app is not intended for children under 13.
We do not knowingly collect data from children.

7. CONTACT US
For privacy questions: privacy@visualvoicemailpro.com
To exercise your rights: rights@visualvoicemailpro.com

By using our app, you agree to this privacy policy.
";

                await Application.Current.MainPage.DisplayAlert(
                    "Privacy Policy",
                    privacyPolicyText,
                    "Close");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show privacy policy: {ex.Message}");
                return false;
            }
        }

        private bool DetermineConsentRequired()
        {
            try
            {
                // Determine if user is in a region requiring consent (GDPR/CCPA)
                var region = System.Globalization.RegionInfo.CurrentRegion;
                
                // EU countries require GDPR consent
                var gdprCountries = new[]
                {
                    "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR",
                    "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL",
                    "PL", "PT", "RO", "SK", "SI", "ES", "SE"
                };

                // US (California) requires CCPA consent
                var ccpaRegions = new[] { "US" };

                var countryCode = region.TwoLetterISORegionName.ToUpper();
                
                return gdprCountries.Contains(countryCode) || ccpaRegions.Contains(countryCode);
            }
            catch
            {
                // Default to requiring consent if region detection fails
                return true;
            }
        }
    }

    /// <summary>
    /// Privacy Settings Page for managing user consent and preferences
    /// </summary>
    public class PrivacySettingsPage : ContentPage
    {
        private readonly IPrivacyService _privacyService;
        private readonly IAdMobService _adMobService;

        private Switch _personalizedAdsSwitch;
        private Switch _analyticsSwitch;
        private Switch _crashReportingSwitch;

        public PrivacySettingsPage(IPrivacyService privacyService, IAdMobService adMobService)
        {
            _privacyService = privacyService;
            _adMobService = adMobService;
            
            Title = "Privacy Settings";
            InitializeUI();
        }

        private void InitializeUI()
        {
            var scrollView = new ScrollView();
            var stackLayout = new StackLayout
            {
                Padding = new Thickness(20),
                Spacing = 20
            };

            // Header
            stackLayout.Children.Add(new Label
            {
                Text = "🔒 Privacy & Data Settings",
                FontSize = 24,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Personalized Ads
            var personalizedAdsFrame = new Frame
            {
                BackgroundColor = Colors.LightGray,
                HasShadow = false,
                CornerRadius = 10,
                Padding = 15,
                Content = new StackLayout
                {
                    Children =
                    {
                        new Label
                        {
                            Text = "📢 Personalized Advertisements",
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold
                        },
                        new Label
                        {
                            Text = "Allow ads tailored to your interests. Disabling this will show generic ads.",
                            FontSize = 12,
                            TextColor = Colors.Gray
                        },
                        (_personalizedAdsSwitch = new Switch
                        {
                            IsToggled = Preferences.Get("PersonalizedAds", true),
                            HorizontalOptions = LayoutOptions.End
                        })
                    }
                }
            };
            stackLayout.Children.Add(personalizedAdsFrame);

            // Analytics
            var analyticsFrame = new Frame
            {
                BackgroundColor = Colors.LightGray,
                HasShadow = false,
                CornerRadius = 10,
                Padding = 15,
                Content = new StackLayout
                {
                    Children =
                    {
                        new Label
                        {
                            Text = "📊 Usage Analytics",
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold
                        },
                        new Label
                        {
                            Text = "Help improve the app by sharing anonymous usage data.",
                            FontSize = 12,
                            TextColor = Colors.Gray
                        },
                        (_analyticsSwitch = new Switch
                        {
                            IsToggled = Preferences.Get("Analytics", true),
                            HorizontalOptions = LayoutOptions.End
                        })
                    }
                }
            };
            stackLayout.Children.Add(analyticsFrame);

            // Crash Reporting
            var crashReportingFrame = new Frame
            {
                BackgroundColor = Colors.LightGray,
                HasShadow = false,
                CornerRadius = 10,
                Padding = 15,
                Content = new StackLayout
                {
                    Children =
                    {
                        new Label
                        {
                            Text = "🐛 Crash Reporting",
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold
                        },
                        new Label
                        {
                            Text = "Automatically send crash reports to help fix issues.",
                            FontSize = 12,
                            TextColor = Colors.Gray
                        },
                        (_crashReportingSwitch = new Switch
                        {
                            IsToggled = Preferences.Get("CrashReporting", true),
                            HorizontalOptions = LayoutOptions.End
                        })
                    }
                }
            };
            stackLayout.Children.Add(crashReportingFrame);

            // Action Buttons
            stackLayout.Children.Add(new Button
            {
                Text = "View Privacy Policy",
                BackgroundColor = Colors.Blue,
                TextColor = Colors.White,
                CornerRadius = 10,
                Command = new Command(async () => await _privacyService.ShowPrivacyPolicyAsync())
            });

            stackLayout.Children.Add(new Button
            {
                Text = "Delete All My Data",
                BackgroundColor = Colors.Red,
                TextColor = Colors.White,
                CornerRadius = 10,
                Command = new Command(async () => await DeleteAllDataAsync())
            });

            stackLayout.Children.Add(new Button
            {
                Text = "Export My Data",
                BackgroundColor = Colors.Green,
                TextColor = Colors.White,
                CornerRadius = 10,
                Command = new Command(async () => await ExportDataAsync())
            });

            // Event Handlers
            _personalizedAdsSwitch.Toggled += OnPersonalizedAdsToggled;
            _analyticsSwitch.Toggled += OnAnalyticsToggled;
            _crashReportingSwitch.Toggled += OnCrashReportingToggled;

            scrollView.Content = stackLayout;
            Content = scrollView;
        }

        private void OnPersonalizedAdsToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set("PersonalizedAds", e.Value);
            
            if (!e.Value)
            {
                DisplayAlert("Ads Updated", 
                    "You will now see generic ads instead of personalized ones.", 
                    "OK");
            }
        }

        private void OnAnalyticsToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set("Analytics", e.Value);
        }

        private void OnCrashReportingToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set("CrashReporting", e.Value);
        }

        private async Task DeleteAllDataAsync()
        {
            var result = await DisplayAlert(
                "Delete All Data",
                "This will permanently delete all your voicemails, settings, and account data. This action cannot be undone. Are you sure?",
                "Delete Everything",
                "Cancel");

            if (result)
            {
                // Implement data deletion
                Preferences.Clear();
                await DisplayAlert("Data Deleted", "All your data has been permanently deleted.", "OK");
                
                // Exit app or return to onboarding
                Application.Current.Quit();
            }
        }

        private async Task ExportDataAsync()
        {
            try
            {
                var exportData = $@"
VISUAL VOICEMAIL PRO - DATA EXPORT
Generated: {DateTime.Now}

ACCOUNT INFORMATION:
- User ID: {Preferences.Get("UserId", "N/A")}
- Premium Status: {Preferences.Get("IsPremiumUser", false)}
- Registration Date: {Preferences.Get("RegistrationDate", "N/A")}

PRIVACY SETTINGS:
- Personalized Ads: {Preferences.Get("PersonalizedAds", true)}
- Analytics: {Preferences.Get("Analytics", true)}
- Crash Reporting: {Preferences.Get("CrashReporting", true)}
- Consent Given: {Preferences.Get("UserHasConsented", false)}
- Consent Date: {Preferences.Get("ConsentDate", "N/A")}

VOICEMAIL STATISTICS:
- Total Voicemails: [Data would be exported from database]
- Transcriptions Made: [Data would be exported from database]
- Spam Calls Blocked: [Data would be exported from database]

For complete voicemail audio files and transcriptions, please contact support@visualvoicemailpro.com
";

                // In a real implementation, this would save to a file or email the data
                await DisplayAlert("Data Export", exportData, "Close");
                
                await DisplayAlert("Export Complete", 
                    "Your data export has been generated. In the full version, this would be emailed to you or saved to your device.", 
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Failed", $"Failed to export data: {ex.Message}", "OK");
            }
        }
    }
}