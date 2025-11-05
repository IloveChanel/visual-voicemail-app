using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using VisualVoicemailPro.Models;
using VisualVoicemailPro.Services;

namespace VisualVoicemailPro.ViewModels
{
    /// <summary>
    /// Enhanced MainViewModel for Visual Voicemail Pro
    /// Supports subscription tiers, advanced features, and analytics
    /// </summary>
    public class EnhancedMainViewModel : INotifyPropertyChanged
    {
        #region Fields and Properties

        private readonly EnhancedSpeechService speechService;
        private readonly IMultilingualTranslationService translationService;
        private readonly ILocalizationService localizationService;
        private readonly EnhancedSpamService spamService;
        private readonly StripeIntegrationService stripeService;
        private readonly IAdMobService adMobService;
        
        private User currentUser;
        private bool isProcessing;
        private string statusMessage = "Ready";
        private string selectedSpeechLanguage = "en-US";
        private string selectedTranslationLanguage = "es";

        public ObservableCollection<Voicemail> Voicemails { get; }
        public ObservableCollection<Voicemail> FilteredVoicemails { get; }
        public ObservableCollection<string> SpeechLanguages { get; }
        public ObservableCollection<string> TranslationLanguages { get; }
        
        public User CurrentUser
        {
            get => currentUser;
            set
            {
                currentUser = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubscriptionStatusText));
                OnPropertyChanged(nameof(CanUseAdvancedFeatures));
                RefreshCommands();
            }
        }

        public bool IsProcessing
        {
            get => isProcessing;
            set
            {
                isProcessing = value;
                OnPropertyChanged();
                RefreshCommands();
            }
        }

        public string StatusMessage
        {
            get => statusMessage;
            set
            {
                statusMessage = value;
                OnPropertyChanged();
            }
        }

        // AdMob Properties
        public bool ShowAds => !IsPremiumUser && adMobService != null;
        
        public bool IsPremiumUser => adMobService?.IsPremiumUser ?? false;
        
        public string AdStatusText => IsPremiumUser ? "Premium - No Ads" : "Free with Ads";

        public string SubscriptionStatusText => CurrentUser?.IsSubscriptionActive == true 
            ? $"Visual Voicemail {CurrentUser.SubscriptionTier.ToUpper()} - Active" 
            : CurrentUser?.IsInTrial == true 
                ? $"Free Trial - {(CurrentUser.TrialEndDate - DateTime.UtcNow)?.Days} days left"
                : "Free Plan - 5 voicemails/month";

        public bool CanUseAdvancedFeatures => CurrentUser?.CanUseUnlimitedTranscription == true;

        public string SelectedSpeechLanguage
        {
            get => selectedSpeechLanguage;
            set
            {
                selectedSpeechLanguage = value;
                OnPropertyChanged();
                StatusMessage = $"Speech language set to {GetLanguageDisplayName(value)}";
            }
        }

        public string SelectedTranslationLanguage
        {
            get => selectedTranslationLanguage;
            set
            {
                selectedTranslationLanguage = value;
                OnPropertyChanged();
                StatusMessage = $"Translation language set to {GetLanguageDisplayName(value)}";
            }
        }

        #endregion

        #region Commands

        public ICommand TranscribeCommand { get; private set; }
        public ICommand TranslateCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand MarkAsReadCommand { get; private set; }
        public ICommand ToggleFavoriteCommand { get; private set; }
        public ICommand BlockCallerCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand UpgradeSubscriptionCommand { get; private set; }
        public ICommand ProcessAllCommand { get; private set; }
        public ICommand FilterCommand { get; private set; }
        
        // AdMob Commands
        public ICommand RemoveAdsCommand { get; private set; }
        public ICommand RestorePurchasesCommand { get; private set; }

        #endregion

        #region Constructor

        public EnhancedMainViewModel(
            EnhancedSpeechService speechService,
            IMultilingualTranslationService translationService,
            ILocalizationService localizationService,
            EnhancedSpamService spamService,
            StripeIntegrationService stripeService,
            IAdMobService adMobService)
        {
            this.speechService = speechService;
            this.translationService = translationService;
            this.localizationService = localizationService;
            this.spamService = spamService;
            this.stripeService = stripeService;
            this.adMobService = adMobService;

            Voicemails = new ObservableCollection<Voicemail>();
            FilteredVoicemails = new ObservableCollection<Voicemail>();
            SpeechLanguages = new ObservableCollection<string>();
            TranslationLanguages = new ObservableCollection<string>();
            
            InitializeLanguages();
            InitializeCommands();
            
            // Initialize with default user (would be loaded from auth in real app)
            CurrentUser = new User
            {
                Id = "user_001",
                Email = "test@visualvoicemail.com",
                PhoneNumber = "248-321-9121",
                DisplayName = "Test User",
                SubscriptionTier = "free"
            };

            StatusMessage = "Welcome to Visual Voicemail Pro";
        }

        #endregion

        #region Command Implementations

        private void InitializeCommands()
        {
            TranscribeCommand = new Command<Voicemail>(async vm => await TranscribeVoicemail(vm), vm => !IsProcessing && vm != null);
            TranslateCommand = new Command<Voicemail>(async vm => await TranslateVoicemail(vm), vm => CanUseTranslation(vm));
            DeleteCommand = new Command<Voicemail>(DeleteVoicemail, vm => vm != null);
            MarkAsReadCommand = new Command<Voicemail>(MarkAsRead, vm => vm != null && !vm.IsRead);
            ToggleFavoriteCommand = new Command<Voicemail>(ToggleFavorite, vm => vm != null);
            BlockCallerCommand = new Command<Voicemail>(async vm => await BlockCaller(vm), vm => vm != null);
            RefreshCommand = new Command(async () => await RefreshVoicemails(), () => !IsProcessing);
            UpgradeSubscriptionCommand = new Command(async () => await UpgradeSubscription());
            ProcessAllCommand = new Command(async () => await ProcessAllVoicemails(), () => !IsProcessing && CanUseAdvancedFeatures);
            FilterCommand = new Command<string>(ApplyFilter);
            
            // AdMob Commands
            RemoveAdsCommand = new Command(async () => await RemoveAdsAsync(), () => !IsPremiumUser);
            RestorePurchasesCommand = new Command(async () => await RestorePurchasesAsync());
        }

        private async Task TranscribeVoicemail(Voicemail voicemail)
        {
            if (voicemail == null || IsProcessing) return;

            try
            {
                IsProcessing = true;
                StatusMessage = $"Transcribing voicemail from {voicemail.CallerNumber}...";

                // Check subscription limits for free users
                if (!CanProcessMoreVoicemails())
                {
                    StatusMessage = "Monthly limit reached. Upgrade to Pro for unlimited transcription.";
                    return;
                }

                voicemail.ProcessingStatus = "processing";
                
                var result = await speechService.TranscribeAsync(
                    voicemail.FilePath,
                    SelectedSpeechLanguage,
                    CurrentUser.SubscriptionTier
                );

                if (result.Success)
                {
                    voicemail.Transcription = result.Transcription;
                    voicemail.DetectedLanguage = result.DetectedLanguage ?? "en-US";
                    voicemail.ProcessingStatus = "completed";
                    voicemail.ProcessedAt = DateTime.UtcNow;
                    voicemail.ProcessedWithTier = CurrentUser.SubscriptionTier;

                // Automatically run spam detection if enabled
                if (CurrentUser.EnableSpamDetection)
                {
                    await AnalyzeSpam(voicemail);
                }

                // Auto-translate if different language detected and translation enabled
                if (CurrentUser.EnableTranslation && 
                    CanUseAdvancedFeatures &&
                    result.DetectedLanguage != SelectedSpeechLanguage)
                {
                    await TranslateVoicemail(voicemail);
                }                    StatusMessage = $"Transcription completed for {voicemail.CallerNumber}";
                }
                else
                {
                    voicemail.ProcessingStatus = "failed";
                    voicemail.ProcessingError = result.ErrorMessage;
                    StatusMessage = $"Transcription failed: {result.ErrorMessage}";
                }

                OnPropertyChanged(nameof(Voicemails));
            }
            catch (Exception ex)
            {
                voicemail.ProcessingStatus = "failed";
                voicemail.ProcessingError = ex.Message;
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task TranslateVoicemail(Voicemail voicemail)
        {
            if (voicemail == null || string.IsNullOrEmpty(voicemail.Transcription) || !CanUseAdvancedFeatures)
                return;

            try
            {
                StatusMessage = await localizationService.GetLocalizedStringAsync("voicemail.translating", localizationService.GetCurrentCulture());

                var translationRequest = new TranslationRequest
                {
                    Text = voicemail.Transcription,
                    TargetLanguage = SelectedTranslationLanguage,
                    SourceLanguage = voicemail.DetectedLanguage,
                    UserId = CurrentUser.Id,
                    Context = "voicemail_transcription",
                    UseHighQuality = CurrentUser.SubscriptionTier != "free"
                };

                var result = await translationService.TranslateAsync(translationRequest);

                if (result.Success)
                {
                    voicemail.TranslatedText = result.TranslatedText;
                    voicemail.TranslationProvider = result.UsedProvider.ToString();
                    voicemail.TranslationConfidence = result.Confidence;
                    StatusMessage = await localizationService.GetLocalizedStringAsync("translation.completed", localizationService.GetCurrentCulture());
                }
                else
                {
                    StatusMessage = $"Translation failed: {result.ErrorMessage}";
                }

                OnPropertyChanged(nameof(Voicemails));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Translation error: {ex.Message}";
            }
        }

        private async Task AnalyzeSpam(Voicemail voicemail)
        {
            if (voicemail == null) return;

            try
            {
                var result = await spamService.AnalyzeSpamAsync(
                    voicemail.CallerNumber,
                    voicemail.Transcription ?? "",
                    CurrentUser.SubscriptionTier
                );

                voicemail.IsSpam = result.IsSpam;
                voicemail.SpamConfidence = result.Confidence;
                voicemail.SpamReasons = result.Reasons;

                if (result.IsSpam)
                {
                    StatusMessage = $"Spam detected: {voicemail.CallerNumber} (confidence: {result.Confidence:P0})";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Spam analysis error: {ex.Message}";
            }
        }

        private void DeleteVoicemail(Voicemail voicemail)
        {
            if (voicemail == null) return;

            Voicemails.Remove(voicemail);
            ApplyCurrentFilter();
            StatusMessage = $"Deleted voicemail from {voicemail.CallerNumber}";
        }

        private void MarkAsRead(Voicemail voicemail)
        {
            if (voicemail == null) return;

            voicemail.ReadAt = DateTime.UtcNow;
            OnPropertyChanged(nameof(Voicemails));
            StatusMessage = $"Marked voicemail as read";
        }

        private void ToggleFavorite(Voicemail voicemail)
        {
            if (voicemail == null) return;

            voicemail.IsFavorite = !voicemail.IsFavorite;
            OnPropertyChanged(nameof(Voicemails));
            StatusMessage = voicemail.IsFavorite ? "Added to favorites" : "Removed from favorites";
        }

        private async Task BlockCaller(Voicemail voicemail)
        {
            if (voicemail == null) return;

            try
            {
                CurrentUser.BlockedNumbers.Add(voicemail.CallerNumber);
                spamService.AddBlockedNumber(voicemail.CallerNumber);
                
                // Mark existing voicemails from this caller as spam
                foreach (var vm in Voicemails.Where(v => v.CallerNumber == voicemail.CallerNumber))
                {
                    vm.IsSpam = true;
                    vm.SpamReasons.Add("Blocked by user");
                }

                OnPropertyChanged(nameof(Voicemails));
                StatusMessage = $"Blocked {voicemail.CallerNumber}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error blocking caller: {ex.Message}";
            }
        }

        private async Task RefreshVoicemails()
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "Refreshing voicemails...";

                // In a real app, this would fetch from API
                await Task.Delay(1000);

                StatusMessage = "Voicemails refreshed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task UpgradeSubscription()
        {
            try
            {
                StatusMessage = "Opening subscription upgrade...";

                // In a real app, this would open the subscription page
                var checkoutResult = await stripeService.CreateCheckoutSessionAsync(
                    CurrentUser.Id,
                    "pro",
                    CurrentUser.Email,
                    CurrentUser.PhoneNumber
                );

                if (checkoutResult.Success)
                {
                    // Open browser or in-app purchase
                    StatusMessage = "Redirecting to subscription upgrade...";
                }
                else
                {
                    StatusMessage = $"Upgrade error: {checkoutResult.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Subscription error: {ex.Message}";
            }
        }

        private async Task ProcessAllVoicemails()
        {
            if (!CanUseAdvancedFeatures) return;

            try
            {
                IsProcessing = true;
                StatusMessage = "Processing all unprocessed voicemails...";

                var unprocessed = Voicemails.Where(vm => 
                    vm.ProcessingStatus == "pending" || 
                    string.IsNullOrEmpty(vm.Transcription)
                ).ToList();

                foreach (var voicemail in unprocessed)
                {
                    await TranscribeVoicemail(voicemail);
                }

                StatusMessage = $"Processed {unprocessed.Count} voicemails";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Batch processing error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ApplyFilter(string filter)
        {
            FilteredVoicemails.Clear();

            var filtered = filter?.ToLower() switch
            {
                "unread" => Voicemails.Where(vm => !vm.IsRead),
                "spam" => Voicemails.Where(vm => vm.IsSpam),
                "favorites" => Voicemails.Where(vm => vm.IsFavorite),
                "important" => Voicemails.Where(vm => vm.Priority == "high"),
                _ => Voicemails
            };

            foreach (var vm in filtered.OrderByDescending(vm => vm.ReceivedAt))
            {
                FilteredVoicemails.Add(vm);
            }

            StatusMessage = $"Showing {FilteredVoicemails.Count} voicemails";
        }

        #endregion

        #region Helper Methods

        private void InitializeLanguages()
        {
            // Speech recognition languages with full locale codes
            var speechLanguages = new[]
            {
                "en-US", "en-GB", "en-AU", "en-CA",
                "es-ES", "es-MX", "es-AR", "es-US",
                "fr-FR", "fr-CA",
                "de-DE", "de-AT", "de-CH",
                "it-IT",
                "pt-BR", "pt-PT",
                "zh-CN", "zh-TW",
                "ja-JP",
                "ko-KR",
                "ar-SA", "ar-EG",
                "ru-RU",
                "hi-IN",
                "nl-NL",
                "sv-SE",
                "no-NO",
                "da-DK",
                "fi-FI",
                "pl-PL",
                "tr-TR"
            };

            // Translation languages with simple codes
            var translationLanguages = new[]
            {
                "en", "es", "fr", "de", "it", "pt", "zh", "ja", "ko",
                "ar", "ru", "hi", "nl", "sv", "no", "da", "fi", "pl", "tr",
                "cs", "hu", "ro", "bg", "hr", "sk", "sl", "et", "lv", "lt",
                "th", "vi", "id", "ms", "tl", "sw", "he", "fa", "ur", "bn"
            };

            SpeechLanguages.Clear();
            TranslationLanguages.Clear();

            foreach (var lang in speechLanguages.OrderBy(l => GetLanguageDisplayName(l)))
            {
                SpeechLanguages.Add(lang);
            }

            foreach (var lang in translationLanguages.OrderBy(l => GetLanguageDisplayName(l)))
            {
                TranslationLanguages.Add(lang);
            }
        }

        private string GetLanguageDisplayName(string languageCode)
        {
            var languageNames = new Dictionary<string, string>
            {
                // English variants
                ["en-US"] = "English (United States)",
                ["en-GB"] = "English (United Kingdom)", 
                ["en-AU"] = "English (Australia)",
                ["en-CA"] = "English (Canada)",
                ["en"] = "English",

                // Spanish variants
                ["es-ES"] = "Spanish (Spain)",
                ["es-MX"] = "Spanish (Mexico)",
                ["es-AR"] = "Spanish (Argentina)",
                ["es-US"] = "Spanish (United States)",
                ["es"] = "Spanish",

                // French variants
                ["fr-FR"] = "French (France)",
                ["fr-CA"] = "French (Canada)",
                ["fr"] = "French",

                // German variants
                ["de-DE"] = "German (Germany)",
                ["de-AT"] = "German (Austria)",
                ["de-CH"] = "German (Switzerland)",
                ["de"] = "German",

                // Other major languages
                ["it-IT"] = "Italian (Italy)",
                ["it"] = "Italian",
                ["pt-BR"] = "Portuguese (Brazil)",
                ["pt-PT"] = "Portuguese (Portugal)",
                ["pt"] = "Portuguese",
                ["zh-CN"] = "Chinese (Simplified)",
                ["zh-TW"] = "Chinese (Traditional)",
                ["zh"] = "Chinese",
                ["ja-JP"] = "Japanese (Japan)",
                ["ja"] = "Japanese",
                ["ko-KR"] = "Korean (South Korea)",
                ["ko"] = "Korean",
                ["ar-SA"] = "Arabic (Saudi Arabia)",
                ["ar-EG"] = "Arabic (Egypt)",
                ["ar"] = "Arabic",
                ["ru-RU"] = "Russian (Russia)",
                ["ru"] = "Russian",
                ["hi-IN"] = "Hindi (India)",
                ["hi"] = "Hindi",
                ["nl-NL"] = "Dutch (Netherlands)",
                ["nl"] = "Dutch",
                ["sv-SE"] = "Swedish (Sweden)",
                ["sv"] = "Swedish",
                ["no-NO"] = "Norwegian (Norway)",
                ["no"] = "Norwegian",
                ["da-DK"] = "Danish (Denmark)",
                ["da"] = "Danish",
                ["fi-FI"] = "Finnish (Finland)",
                ["fi"] = "Finnish",
                ["pl-PL"] = "Polish (Poland)",
                ["pl"] = "Polish",
                ["tr-TR"] = "Turkish (Turkey)",
                ["tr"] = "Turkish",
                ["th"] = "Thai",
                ["vi"] = "Vietnamese",
                ["id"] = "Indonesian",
                ["ms"] = "Malay",
                ["tl"] = "Filipino",
                ["sw"] = "Swahili",
                ["he"] = "Hebrew",
                ["fa"] = "Persian",
                ["ur"] = "Urdu",
                ["bn"] = "Bengali"
            };

            return languageNames.TryGetValue(languageCode, out var name) ? name : languageCode;
        }

        private bool CanProcessMoreVoicemails()
        {
            if (CurrentUser.CanUseUnlimitedTranscription) return true;

            // Reset monthly counter if needed
            if (DateTime.UtcNow.Month != CurrentUser.LastMonthlyReset.Month)
            {
                CurrentUser.MonthlyVoicemailCount = 0;
                CurrentUser.LastMonthlyReset = DateTime.UtcNow;
            }

            return CurrentUser.MonthlyVoicemailCount < CurrentUser.MaxVoicemailsPerMonth;
        }

        private bool CanUseTranslation(Voicemail voicemail)
        {
            return !IsProcessing && 
                   voicemail != null && 
                   !string.IsNullOrEmpty(voicemail.Transcription) &&
                   CurrentUser.CanUseTranslation;
        }

        private void ApplyCurrentFilter()
        {
            // Reapply current filter after changes
            ApplyFilter("all");
        }

        private void RefreshCommands()
        {
            // Refresh command availability
            ((Command)TranscribeCommand).ChangeCanExecute();
            ((Command)TranslateCommand).ChangeCanExecute();
            ((Command)ProcessAllCommand).ChangeCanExecute();
            ((Command)RefreshCommand).ChangeCanExecute();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Sample Data for Testing

        public void LoadSampleData()
        {
            var sampleVoicemails = new[]
            {
                new Voicemail
                {
                    Id = "vm_001",
                    UserId = CurrentUser.Id,
                    CallerNumber = "+1-248-321-9121",
                    CallerName = "Dr. Smith's Office",
                    ReceivedAt = DateTime.Now.AddHours(-2),
                    DurationSeconds = 45,
                    Transcription = "Hi, this is Dr. Smith's office. Your appointment tomorrow at 2 PM is confirmed. Please arrive 15 minutes early.",
                    Category = "appointment",
                    Priority = "medium",
                    ProcessingStatus = "completed"
                },
                new Voicemail
                {
                    Id = "vm_002",
                    UserId = CurrentUser.Id,
                    CallerNumber = "+1-555-0123",
                    CallerName = "Unknown",
                    ReceivedAt = DateTime.Now.AddHours(-5),
                    DurationSeconds = 12,
                    Transcription = "Congratulations! You've won a free cruise. Call back now to claim your prize!",
                    IsSpam = true,
                    SpamConfidence = 0.95f,
                    SpamReasons = new List<string> { "Contains spam keywords", "Robocall pattern" },
                    Category = "spam",
                    Priority = "low",
                    ProcessingStatus = "completed"
                },
                new Voicemail
                {
                    Id = "vm_003",
                    UserId = CurrentUser.Id,
                    CallerNumber = "+1-800-555-1234",
                    CallerName = "Amazon Support",
                    ReceivedAt = DateTime.Now.AddDays(-1),
                    DurationSeconds = 67,
                    Transcription = "Hello, this is Amazon customer service. We're calling about your recent order delivery.",
                    Category = "delivery",
                    Priority = "medium",
                    ProcessingStatus = "completed"
                }
            };

            Voicemails.Clear();
            foreach (var vm in sampleVoicemails)
            {
                Voicemails.Add(vm);
            }

            ApplyFilter("all");
            StatusMessage = $"Loaded {Voicemails.Count} sample voicemails";
        }

        #endregion

        #region AdMob Methods

        /// <summary>
        /// Initialize AdMob service and show initial ads for free users
        /// </summary>
        public async Task InitializeAdMobAsync()
        {
            try
            {
                if (adMobService != null)
                {
                    await adMobService.InitializeAsync();
                    
                    // Subscribe to ad events
                    adMobService.AdLoaded += OnAdLoaded;
                    adMobService.AdFailedToLoad += OnAdFailedToLoad;
                    adMobService.AdClicked += OnAdClicked;
                    adMobService.AdClosed += OnAdClosed;

                    // Refresh UI properties
                    OnPropertyChanged(nameof(ShowAds));
                    OnPropertyChanged(nameof(IsPremiumUser));
                    OnPropertyChanged(nameof(AdStatusText));
                    RefreshCommands();

                    StatusMessage = IsPremiumUser ? "Premium user - ads disabled" : "Ads initialized";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to initialize ads: {ex.Message}";
            }
        }

        /// <summary>
        /// Show banner ad in the specified container
        /// </summary>
        public async Task ShowBannerAdAsync(object container)
        {
            try
            {
                if (adMobService != null && !IsPremiumUser)
                {
                    await adMobService.ShowBannerAdAsync(container);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to show banner ad: {ex.Message}";
            }
        }

        /// <summary>
        /// Show interstitial ad (called at strategic points)
        /// </summary>
        public async Task ShowInterstitialAdAsync()
        {
            try
            {
                if (adMobService != null && !IsPremiumUser)
                {
                    var adShown = await adMobService.ShowInterstitialAdAsync();
                    if (adShown)
                    {
                        StatusMessage = "Thank you for supporting the app!";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ad error: {ex.Message}";
            }
        }

        /// <summary>
        /// Purchase premium upgrade to remove ads
        /// </summary>
        private async Task RemoveAdsAsync()
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "Processing premium upgrade...";

                if (adMobService != null)
                {
                    var purchased = await adMobService.PurchasePremiumUpgradeAsync();
                    
                    if (purchased)
                    {
                        StatusMessage = "🎉 Premium upgrade successful! Ads removed.";
                        
                        // Update UI properties
                        OnPropertyChanged(nameof(ShowAds));
                        OnPropertyChanged(nameof(IsPremiumUser));
                        OnPropertyChanged(nameof(AdStatusText));
                        OnPropertyChanged(nameof(CanUseAdvancedFeatures));
                        RefreshCommands();
                        
                        // Also upgrade user subscription in backend
                        if (CurrentUser != null)
                        {
                            CurrentUser.SubscriptionTier = SubscriptionTier.Pro;
                            OnPropertyChanged(nameof(CurrentUser));
                        }
                    }
                    else
                    {
                        StatusMessage = "Premium upgrade cancelled or failed";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Premium upgrade failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Restore previous premium purchases
        /// </summary>
        private async Task RestorePurchasesAsync()
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "Restoring purchases...";

                if (adMobService != null)
                {
                    var restored = await adMobService.RestorePremiumPurchasesAsync();
                    
                    if (restored)
                    {
                        StatusMessage = "✅ Purchases restored successfully!";
                        
                        // Update UI properties
                        OnPropertyChanged(nameof(ShowAds));
                        OnPropertyChanged(nameof(IsPremiumUser));
                        OnPropertyChanged(nameof(AdStatusText));
                        RefreshCommands();
                        
                        // Update user subscription
                        if (CurrentUser != null)
                        {
                            CurrentUser.SubscriptionTier = SubscriptionTier.Pro;
                            OnPropertyChanged(nameof(CurrentUser));
                        }
                    }
                    else
                    {
                        StatusMessage = "No previous purchases found";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Restore failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Show ad at strategic points (after voicemail actions)
        /// </summary>
        private async Task ShowAdAfterActionAsync(string action)
        {
            try
            {
                // Show ads after certain actions for engagement
                var showAdActions = new[] { "transcribe", "translate", "process_all" };
                
                if (showAdActions.Contains(action.ToLower()) && !IsPremiumUser)
                {
                    // Add small delay for better UX
                    await Task.Delay(1000);
                    await ShowInterstitialAdAsync();
                }
            }
            catch (Exception ex)
            {
                // Don't interrupt user flow for ad errors
                System.Diagnostics.Debug.WriteLine($"Ad display error: {ex.Message}");
            }
        }

        // Ad Event Handlers
        private void OnAdLoaded(object sender, AdEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Ad loaded: {e.AdType}");
        }

        private void OnAdFailedToLoad(object sender, AdEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Ad failed to load: {e.AdType} - {e.ErrorMessage}");
        }

        private void OnAdClicked(object sender, AdEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Ad clicked: {e.AdType}");
        }

        private void OnAdClosed(object sender, AdEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Ad closed: {e.AdType}");
        }

        #endregion
    }
}