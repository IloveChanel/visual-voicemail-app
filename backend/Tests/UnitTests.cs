using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using VisualVoicemailPro.Services;
using VisualVoicemailPro.Models;
using VisualVoicemailPro.ViewModels;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VisualVoicemailPro.Tests
{
    /// <summary>
    /// Unit Tests for Enhanced Main ViewModel
    /// Tests multi-language functionality, subscription features, and API integration
    /// </summary>
    public class EnhancedMainViewModelTests
    {
        private readonly Mock<ILogger<EnhancedMainViewModel>> _mockLogger;
        private readonly Mock<ISecureConfigurationService> _mockConfigService;
        private readonly Mock<EnhancedSpeechService> _mockSpeechService;
        private readonly Mock<EnhancedTranslationService> _mockTranslationService;
        private readonly Mock<StripeIntegrationService> _mockStripeService;
        private readonly EnhancedMainViewModel _viewModel;

        public EnhancedMainViewModelTests()
        {
            _mockLogger = new Mock<ILogger<EnhancedMainViewModel>>();
            _mockConfigService = new Mock<ISecureConfigurationService>();
            _mockSpeechService = new Mock<EnhancedSpeechService>();
            _mockTranslationService = new Mock<EnhancedTranslationService>();
            _mockStripeService = new Mock<StripeIntegrationService>();

            _viewModel = new EnhancedMainViewModel(
                _mockLogger.Object,
                _mockConfigService.Object,
                _mockSpeechService.Object,
                _mockTranslationService.Object,
                _mockStripeService.Object
            );
        }

        [Fact]
        public async Task InitializeLanguages_Should_LoadAllSupportedLanguages()
        {
            // Act
            await _viewModel.InitializeAsync();

            // Assert
            _viewModel.SpeechLanguages.Should().NotBeEmpty();
            _viewModel.TranslationLanguages.Should().NotBeEmpty();
            _viewModel.SpeechLanguages.Should().Contain("en-US");
            _viewModel.SpeechLanguages.Should().Contain("es-ES");
            _viewModel.TranslationLanguages.Should().Contain("en");
            _viewModel.TranslationLanguages.Should().Contain("es");
        }

        [Theory]
        [InlineData("en-US", "English (United States)")]
        [InlineData("es-ES", "Spanish (Spain)")]
        [InlineData("fr-FR", "French (France)")]
        [InlineData("de-DE", "German (Germany)")]
        public void GetLanguageDisplayName_Should_ReturnCorrectDisplayName(string languageCode, string expectedName)
        {
            // Act
            var displayName = _viewModel.GetLanguageDisplayName(languageCode);

            // Assert
            displayName.Should().Contain(expectedName);
        }

        [Fact]
        public async Task TranscribeVoicemail_Should_UseSelectedLanguage()
        {
            // Arrange
            var voicemail = new EnhancedVoicemail
            {
                Id = "test-123",
                AudioFilePath = "test-audio.wav",
                CallerNumber = "+1234567890"
            };

            _viewModel.SelectedSpeechLanguage = "es-ES";

            var expectedTranscription = "Hola, este es un mensaje de prueba";
            _mockSpeechService
                .Setup(x => x.TranscribeAudioAsync(It.IsAny<string>(), "es-ES"))
                .ReturnsAsync(expectedTranscription);

            // Act
            await _viewModel.TranscribeVoicemailAsync(voicemail);

            // Assert
            voicemail.Transcription.Should().Be(expectedTranscription);
            _mockSpeechService.Verify(x => x.TranscribeAudioAsync(It.IsAny<string>(), "es-ES"), Times.Once);
        }

        [Fact]
        public async Task TranslateVoicemail_Should_RequireProSubscription()
        {
            // Arrange
            var voicemail = new EnhancedVoicemail
            {
                Id = "test-123",
                Transcription = "Hello, this is a test message"
            };

            _viewModel.SelectedTranslationLanguage = "es";
            _viewModel.UserSubscription = SubscriptionTier.Free; // Free tier

            // Act
            await _viewModel.TranslateVoicemailAsync(voicemail);

            // Assert
            voicemail.TranslatedText.Should().BeNull();
            _mockTranslationService.Verify(x => x.TranslateTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task TranslateVoicemail_Should_WorkForProSubscription()
        {
            // Arrange
            var voicemail = new EnhancedVoicemail
            {
                Id = "test-123",
                Transcription = "Hello, this is a test message"
            };

            _viewModel.SelectedTranslationLanguage = "es";
            _viewModel.UserSubscription = SubscriptionTier.Pro;

            var expectedTranslation = "Hola, este es un mensaje de prueba";
            _mockTranslationService
                .Setup(x => x.TranslateTextAsync("Hello, this is a test message", "es"))
                .ReturnsAsync(expectedTranslation);

            // Act
            await _viewModel.TranslateVoicemailAsync(voicemail);

            // Assert
            voicemail.TranslatedText.Should().Be(expectedTranslation);
            _mockTranslationService.Verify(x => x.TranslateTextAsync("Hello, this is a test message", "es"), Times.Once);
        }

        [Fact]
        public void CanUseAdvancedFeatures_Should_ReturnTrueForProAndBusiness()
        {
            // Test Pro subscription
            _viewModel.UserSubscription = SubscriptionTier.Pro;
            _viewModel.CanUseAdvancedFeatures.Should().BeTrue();

            // Test Business subscription
            _viewModel.UserSubscription = SubscriptionTier.Business;
            _viewModel.CanUseAdvancedFeatures.Should().BeTrue();

            // Test Free subscription
            _viewModel.UserSubscription = SubscriptionTier.Free;
            _viewModel.CanUseAdvancedFeatures.Should().BeFalse();
        }

        [Fact]
        public async Task UpgradeToProAsync_Should_CallStripeService()
        {
            // Arrange
            _mockStripeService
                .Setup(x => x.CreateSubscriptionAsync(It.IsAny<string>(), "price_pro_monthly"))
                .ReturnsAsync(true);

            // Act
            var result = await _viewModel.UpgradeToProAsync();

            // Assert
            result.Should().BeTrue();
            _mockStripeService.Verify(x => x.CreateSubscriptionAsync(It.IsAny<string>(), "price_pro_monthly"), Times.Once);
        }
    }

    /// <summary>
    /// Unit Tests for Secure Configuration Service
    /// Tests secret management, environment variables, and Azure Key Vault integration
    /// </summary>
    public class SecureConfigurationServiceTests
    {
        private readonly Mock<ILogger<SecureConfigurationService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly SecureConfigurationService _configService;

        public SecureConfigurationServiceTests()
        {
            _mockLogger = new Mock<ILogger<SecureConfigurationService>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _configService = new SecureConfigurationService(_mockConfiguration.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetSecretAsync_Should_ReturnEnvironmentVariable()
        {
            // Arrange
            var secretKey = "TEST_SECRET";
            var secretValue = "test-secret-value";
            Environment.SetEnvironmentVariable(secretKey, secretValue);

            // Act
            var result = await _configService.GetSecretAsync(secretKey);

            // Assert
            result.Should().Be(secretValue);

            // Cleanup
            Environment.SetEnvironmentVariable(secretKey, null);
        }

        [Fact]
        public async Task GetSecretAsync_Should_ReturnConfigurationValue_WhenEnvironmentVariableNotFound()
        {
            // Arrange
            var secretKey = "STRIPE_SECRET_KEY";
            var secretValue = "sk_test_12345";
            _mockConfiguration.Setup(x => x[secretKey]).Returns(secretValue);

            // Act
            var result = await _configService.GetSecretAsync(secretKey);

            // Assert
            result.Should().Be(secretValue);
        }

        [Fact]
        public void GetConnectionString_Should_ReplaceEnvironmentVariables()
        {
            // Arrange
            var connectionStringTemplate = "Server=${SQL_SERVER};Database=VoicemailPro;";
            var expectedServer = "localhost";
            Environment.SetEnvironmentVariable("SQL_SERVER", expectedServer);

            _mockConfiguration.Setup(x => x.GetConnectionString("DefaultConnection"))
                .Returns(connectionStringTemplate);

            // Act
            var result = _configService.GetConnectionString("DefaultConnection");

            // Assert
            result.Should().Contain(expectedServer);
            result.Should().Be("Server=localhost;Database=VoicemailPro;");

            // Cleanup
            Environment.SetEnvironmentVariable("SQL_SERVER", null);
        }
    }

    /// <summary>
    /// Integration Tests for Enhanced Speech Service
    /// Tests Google Cloud Speech API integration with retry policies
    /// </summary>
    public class EnhancedSpeechServiceTests
    {
        private readonly Mock<ILogger<EnhancedSpeechService>> _mockLogger;
        private readonly Mock<ISecureConfigurationService> _mockConfigService;
        private readonly EnhancedSpeechService _speechService;

        public EnhancedSpeechServiceTests()
        {
            _mockLogger = new Mock<ILogger<EnhancedSpeechService>>();
            _mockConfigService = new Mock<ISecureConfigurationService>();

            _mockConfigService
                .Setup(x => x.GetSecretAsync("GOOGLE_CLOUD_PROJECT_ID"))
                .ReturnsAsync("test-project");

            _speechService = new EnhancedSpeechService(_mockLogger.Object, _mockConfigService.Object);
        }

        [Fact]
        public async Task TranscribeAudioAsync_Should_ReturnEmptyString_ForInvalidFile()
        {
            // Arrange
            var invalidFilePath = "non-existent-file.wav";

            // Act
            var result = await _speechService.TranscribeAudioAsync(invalidFilePath, "en-US");

            // Assert
            result.Should().BeEmpty();
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("de-DE")]
        public void IsLanguageSupported_Should_ReturnTrue_ForSupportedLanguages(string languageCode)
        {
            // Act
            var result = _speechService.IsLanguageSupported(languageCode);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsLanguageSupported_Should_ReturnFalse_ForUnsupportedLanguages()
        {
            // Act
            var result = _speechService.IsLanguageSupported("xyz-ZZ");

            // Assert
            result.Should().BeFalse();
        }
    }

    /// <summary>
    /// Unit Tests for Stripe Integration Service
    /// Tests subscription management, payment processing, and webhook handling
    /// </summary>
    public class StripeIntegrationServiceTests
    {
        private readonly Mock<ILogger<StripeIntegrationService>> _mockLogger;
        private readonly Mock<ISecureConfigurationService> _mockConfigService;
        private readonly StripeIntegrationService _stripeService;

        public StripeIntegrationServiceTests()
        {
            _mockLogger = new Mock<ILogger<StripeIntegrationService>>();
            _mockConfigService = new Mock<ISecureConfigurationService>();

            _mockConfigService
                .Setup(x => x.GetSecretAsync("STRIPE_SECRET_KEY"))
                .ReturnsAsync("sk_test_12345");

            _stripeService = new StripeIntegrationService(_mockLogger.Object, _mockConfigService.Object);
        }

        [Theory]
        [InlineData(SubscriptionTier.Pro, "price_pro_monthly")]
        [InlineData(SubscriptionTier.Business, "price_business_monthly")]
        public void GetPriceId_Should_ReturnCorrectPriceId(SubscriptionTier tier, string expectedPriceId)
        {
            // Act
            var priceId = _stripeService.GetPriceId(tier);

            // Assert
            priceId.Should().Be(expectedPriceId);
        }

        [Fact]
        public void GetPriceId_Should_ThrowException_ForFreeTier()
        {
            // Act & Assert
            _stripeService.Invoking(x => x.GetPriceId(SubscriptionTier.Free))
                .Should().Throw<ArgumentException>()
                .WithMessage("*Free tier*");
        }
    }

    /// <summary>
    /// Performance Tests for Critical Operations
    /// </summary>
    public class PerformanceTests
    {
        [Fact]
        public async Task LanguageInitialization_Should_CompleteWithin_OneSecond()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EnhancedMainViewModel>>();
            var mockConfig = new Mock<ISecureConfigurationService>();
            var mockSpeech = new Mock<EnhancedSpeechService>();
            var mockTranslation = new Mock<EnhancedTranslationService>();
            var mockStripe = new Mock<StripeIntegrationService>();

            var viewModel = new EnhancedMainViewModel(
                mockLogger.Object, mockConfig.Object, mockSpeech.Object,
                mockTranslation.Object, mockStripe.Object);

            // Act & Assert
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await viewModel.InitializeAsync();
            stopwatch.Stop();

            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
                "Language initialization should complete within 1 second");
        }
    }
}