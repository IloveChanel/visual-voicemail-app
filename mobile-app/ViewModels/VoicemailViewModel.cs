using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using VisualVoicemailPro.Models;
using VisualVoicemailPro.Services;

namespace VisualVoicemailPro.ViewModels
{
    /// <summary>
    /// Main ViewModel for Visual Voicemail Pro
    /// Integrates AdMob monetization with AI-powered voicemail processing
    /// </summary>
    public partial class VoicemailViewModel : ObservableObject
    {
        #region Services
        private readonly IApiService _apiService;
        private readonly IAdsManager _adsManager;
        private readonly ILogger<VoicemailViewModel> _logger;
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private ObservableCollection<Voicemail> _voicemails = new();

        [ObservableProperty]
        private ObservableCollection<Language> _supportedLanguages = new();

        [ObservableProperty]
        private Language _selectedLanguage;

        [ObservableProperty]
        private User _currentUser;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isPremiumUser;

        [ObservableProperty]
        private bool _showAds;

        [ObservableProperty]
        private string _selectedVoicemailId;

        [ObservableProperty]
        private Analytics _userAnalytics;
        #endregion

        #region Commands
        public ICommand RefreshVoicemailsCommand { get; }
        public ICommand ProcessVoicemailCommand { get; }
        public ICommand PlayVoicemailCommand { get; }
        public ICommand DeleteVoicemailCommand { get; }
        public ICommand ReportSpamCommand { get; }
        public ICommand UpgradeToPremiumCommand { get; }
        public ICommand ShowInterstitialAdCommand { get; }
        public ICommand ChangeLanguageCommand { get; }
        public ICommand TranslateVoicemailCommand { get; }
        #endregion

        #region Constructor
        public VoicemailViewModel(IApiService apiService, IAdsManager adsManager, ILogger<VoicemailViewModel> logger)
        {
            _apiService = apiService;
            _adsManager = adsManager;
            _logger = logger;

            // Initialize commands
            RefreshVoicemailsCommand = new AsyncRelayCommand(RefreshVoicemailsAsync);
            ProcessVoicemailCommand = new AsyncRelayCommand<string>(ProcessVoicemailAsync);
            PlayVoicemailCommand = new RelayCommand<string>(PlayVoicemail);
            DeleteVoicemailCommand = new AsyncRelayCommand<string>(DeleteVoicemailAsync);
            ReportSpamCommand = new AsyncRelayCommand<string>(ReportSpamAsync);
            UpgradeToPremiumCommand = new AsyncRelayCommand(UpgradeToPremiumAsync);
            ShowInterstitialAdCommand = new AsyncRelayCommand(ShowInterstitialAdAsync);
            ChangeLanguageCommand = new AsyncRelayCommand<string>(ChangeLanguageAsync);
            TranslateVoicemailCommand = new AsyncRelayCommand<string>(TranslateVoicemailAsync);

            // Subscribe to ads manager events
            _adsManager.PremiumStatusChanged += OnPremiumStatusChanged;

            // Initialize default values
            SelectedLanguage = new Language { Code = "en-US", Name = "English (US)", IsDefault = true };
            UserAnalytics = new Analytics { UserId = "" };
        }
        #endregion

        #region Public Methods
        public async Task InitializeAsync()
        {
            try
            {
                _logger?.LogInformation("🚀 Initializing VoicemailViewModel");
                IsLoading = true;
                StatusMessage = "Initializing...";

                // Initialize AdMob
                await _adsManager.InitializeAsync();
                IsPremiumUser = _adsManager.IsPremiumUser;
                ShowAds = _adsManager.ShowAds;

                // Load supported languages
                await LoadSupportedLanguagesAsync();

                // Load user data if authenticated
                var userId = await SecureStorage.GetAsync("user_id");
                if (!string.IsNullOrEmpty(userId))
                {
                    CurrentUser = new User { Id = userId };
                    await LoadUserDataAsync();
                }

                StatusMessage = "Ready";
                _logger?.LogInformation("✅ VoicemailViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Failed to initialize VoicemailViewModel");
                StatusMessage = "Initialization failed";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadUserDataAsync()
        {
            if (CurrentUser?.Id == null) return;

            try
            {
                // Load voicemails
                await RefreshVoicemailsAsync();

                // Load analytics
                UserAnalytics = await _apiService.GetAnalyticsAsync(CurrentUser.Id);

                // Show interstitial ad for free users (with frequency control)
                if (!IsPremiumUser && ShouldShowInterstitialAd())
                {
                    await _adsManager.ShowInterstitialAdAsync();
                    UpdateLastAdShownTime();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load user data");
            }
        }
        #endregion

        #region Command Handlers
        private async Task RefreshVoicemailsAsync()
        {
            try
            {
                if (CurrentUser?.Id == null) return;

                IsLoading = true;
                StatusMessage = "Loading voicemails...";

                var voicemails = await _apiService.GetVoicemailsAsync(CurrentUser.Id);
                
                Voicemails.Clear();
                foreach (var vm in voicemails)
                {
                    Voicemails.Add(vm);
                }

                StatusMessage = $"Loaded {voicemails.Count} voicemails";
                _logger?.LogInformation($"📱 Loaded {voicemails.Count} voicemails");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to refresh voicemails");
                StatusMessage = "Failed to load voicemails";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ProcessVoicemailAsync(string voicemailId)
        {
            try
            {
                if (string.IsNullOrEmpty(voicemailId)) return;

                IsProcessing = true;
                StatusMessage = "Processing voicemail...";

                // Show interstitial ad for free users before processing
                if (!IsPremiumUser)
                {
                    await _adsManager.ShowInterstitialAdAsync();
                }

                var processedVoicemail = await _apiService.ProcessVoicemailAsync(voicemailId, SelectedLanguage.Code);
                
                if (processedVoicemail != null)
                {
                    // Update the voicemail in the collection
                    var existingVm = Voicemails.FirstOrDefault(vm => vm.Id == voicemailId);
                    if (existingVm != null)
                    {
                        var index = Voicemails.IndexOf(existingVm);
                        Voicemails[index] = processedVoicemail;
                    }

                    StatusMessage = "Voicemail processed successfully";
                    _logger?.LogInformation($"✅ Processed voicemail: {voicemailId}");
                }
                else
                {
                    StatusMessage = "Processing failed";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to process voicemail: {voicemailId}");
                StatusMessage = "Processing failed";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void PlayVoicemail(string voicemailId)
        {
            try
            {
                if (string.IsNullOrEmpty(voicemailId)) return;

                var voicemail = Voicemails.FirstOrDefault(vm => vm.Id == voicemailId);
                if (voicemail != null)
                {
                    // TODO: Implement audio playback
                    StatusMessage = $"Playing: {voicemail.CallerNumber}";
                    _logger?.LogInformation($"🔊 Playing voicemail: {voicemailId}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to play voicemail: {voicemailId}");
                StatusMessage = "Playback failed";
            }
        }

        private async Task DeleteVoicemailAsync(string voicemailId)
        {
            try
            {
                if (string.IsNullOrEmpty(voicemailId)) return;

                var voicemail = Voicemails.FirstOrDefault(vm => vm.Id == voicemailId);
                if (voicemail != null)
                {
                    Voicemails.Remove(voicemail);
                    StatusMessage = "Voicemail deleted";
                    _logger?.LogInformation($"🗑️ Deleted voicemail: {voicemailId}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to delete voicemail: {voicemailId}");
                StatusMessage = "Delete failed";
            }
        }

        private async Task ReportSpamAsync(string voicemailId)
        {
            try
            {
                if (string.IsNullOrEmpty(voicemailId)) return;

                var success = await _apiService.ReportSpamAsync(voicemailId);
                
                if (success)
                {
                    var voicemail = Voicemails.FirstOrDefault(vm => vm.Id == voicemailId);
                    if (voicemail != null)
                    {
                        voicemail.IsSpam = true;
                        voicemail.SpamConfidence = 1.0f;
                    }

                    StatusMessage = "Spam reported";
                    _logger?.LogInformation($"🚫 Reported spam: {voicemailId}");
                }
                else
                {
                    StatusMessage = "Failed to report spam";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to report spam: {voicemailId}");
                StatusMessage = "Spam report failed";
            }
        }

        private async Task UpgradeToPremiumAsync()
        {
            try
            {
                StatusMessage = "Processing premium upgrade...";
                
                var success = await _adsManager.PurchasePremiumAsync();
                
                if (success)
                {
                    // Update subscription in backend
                    if (CurrentUser?.Id != null)
                    {
                        await _apiService.UpdateSubscriptionAsync(CurrentUser.Id, SubscriptionTier.Pro);
                    }

                    StatusMessage = "Welcome to Premium!";
                    _logger?.LogInformation("💎 Premium upgrade successful");
                }
                else
                {
                    StatusMessage = "Upgrade failed";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Premium upgrade failed");
                StatusMessage = "Upgrade failed";
            }
        }

        private async Task ShowInterstitialAdAsync()
        {
            try
            {
                if (!IsPremiumUser)
                {
                    await _adsManager.ShowInterstitialAdAsync();
                    _logger?.LogInformation("📺 Showed interstitial ad");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show interstitial ad");
            }
        }

        private async Task ChangeLanguageAsync(string languageCode)
        {
            try
            {
                var language = SupportedLanguages.FirstOrDefault(l => l.Code == languageCode);
                if (language != null)
                {
                    SelectedLanguage = language;
                    Preferences.Set("SelectedLanguage", languageCode);
                    StatusMessage = $"Language changed to {language.Name}";
                    _logger?.LogInformation($"🌐 Language changed to: {language.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to change language: {languageCode}");
            }
        }

        private async Task TranslateVoicemailAsync(string voicemailId)
        {
            try
            {
                if (string.IsNullOrEmpty(voicemailId)) return;

                // Show interstitial ad for free users
                if (!IsPremiumUser)
                {
                    await _adsManager.ShowInterstitialAdAsync();
                }

                var voicemail = Voicemails.FirstOrDefault(vm => vm.Id == voicemailId);
                if (voicemail != null && !string.IsNullOrEmpty(voicemail.Transcription))
                {
                    // For demo purposes, we'll simulate translation
                    // In production, this would call the translation API
                    voicemail.Translations[SelectedLanguage.Code] = $"[Translated to {SelectedLanguage.Name}] {voicemail.Transcription}";
                    
                    StatusMessage = $"Translated to {SelectedLanguage.Name}";
                    _logger?.LogInformation($"🔄 Translated voicemail to: {SelectedLanguage.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to translate voicemail: {voicemailId}");
                StatusMessage = "Translation failed";
            }
        }
        #endregion

        #region Private Methods
        private async Task LoadSupportedLanguagesAsync()
        {
            try
            {
                var languages = await _apiService.GetSupportedLanguagesAsync();
                
                SupportedLanguages.Clear();
                foreach (var lang in languages)
                {
                    SupportedLanguages.Add(lang);
                }

                // Set saved language or default
                var savedLanguage = Preferences.Get("SelectedLanguage", "en-US");
                SelectedLanguage = SupportedLanguages.FirstOrDefault(l => l.Code == savedLanguage) 
                    ?? SupportedLanguages.First();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load supported languages");
            }
        }

        private void OnPremiumStatusChanged(object sender, bool isPremium)
        {
            IsPremiumUser = isPremium;
            ShowAds = !isPremium;
            
            StatusMessage = isPremium ? "Premium activated!" : "Premium cancelled";
            _logger?.LogInformation($"Premium status changed: {isPremium}");
        }

        private bool ShouldShowInterstitialAd()
        {
            // Show ad every 3 voicemail operations or 5 minutes, whichever comes first
            var lastAdShown = Preferences.Get("LastAdShown", DateTime.MinValue);
            var adOperationCount = Preferences.Get("AdOperationCount", 0);
            
            return DateTime.UtcNow.Subtract(lastAdShown).TotalMinutes >= 5 || adOperationCount >= 3;
        }

        private void UpdateLastAdShownTime()
        {
            Preferences.Set("LastAdShown", DateTime.UtcNow);
            Preferences.Set("AdOperationCount", 0);
        }
        #endregion

        #region Cleanup
        public void Dispose()
        {
            if (_adsManager != null)
            {
                _adsManager.PremiumStatusChanged -= OnPremiumStatusChanged;
            }
        }
        #endregion
    }

    /// <summary>
    /// Analytics data for the current user
    /// </summary>
    public class Analytics
    {
        public string UserId { get; set; } = "";
        public int TotalVoicemails { get; set; }
        public int TranscribedCount { get; set; }
        public int SpamDetectedCount { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public Dictionary<string, int> LanguageUsage { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public int MonthlyVoicemails { get; set; }
        public float AverageSpamScore { get; set; }
        public string MostUsedLanguage => LanguageUsage.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "en-US";
    }
}