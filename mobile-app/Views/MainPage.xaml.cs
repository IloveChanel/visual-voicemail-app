using Microsoft.Maui.Controls;
using System;
using VisualVoicemailPro.ViewModels;

namespace VisualVoicemailPro.Views
{
    /// <summary>
    /// Main page for Visual Voicemail Pro with multi-language support
    /// </summary>
    public partial class MainPage : ContentPage
    {
        private EnhancedMainViewModel ViewModel => BindingContext as EnhancedMainViewModel;

        public MainPage()
        {
            InitializeComponent();
            
            // Initialize with sample data for testing
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, EventArgs e)
        {
            // Load sample voicemails for demonstration
            if (ViewModel != null)
            {
                ViewModel.LoadSampleData();
                
                // Initialize AdMob
                await ViewModel.InitializeAdMobAsync();
                
                // Show banner ad for free users
                if (ViewModel.ShowAds)
                {
                    await ViewModel.ShowBannerAdAsync(BannerAdContainer);
                }
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Refresh ad status when page appears
            if (ViewModel != null)
            {
                // Update UI bindings
                OnPropertyChanged(nameof(ViewModel.ShowAds));
                OnPropertyChanged(nameof(ViewModel.IsPremiumUser));
            }
        }

        private void OnSpeechLanguageSelected(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            if (picker?.SelectedItem != null && ViewModel != null)
            {
                ViewModel.SelectedSpeechLanguage = picker.SelectedItem.ToString();
            }
        }

        private void OnTranslationLanguageSelected(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            if (picker?.SelectedItem != null && ViewModel != null)
            {
                ViewModel.SelectedTranslationLanguage = picker.SelectedItem.ToString();
            }
        }
    }
}