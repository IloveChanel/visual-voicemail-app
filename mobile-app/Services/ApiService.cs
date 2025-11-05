using System.Net.Http.Json;
using System.Text.Json;
using Newtonsoft.Json;
using VisualVoicemailPro.Models;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// API Service for Visual Voicemail Pro
    /// Connects mobile app to backend with subscription management
    /// </summary>
    public interface IApiService
    {
        Task<ApiResponse<List<Voicemail>>> GetVoicemailsAsync(string userId);
        Task<ApiResponse<Voicemail>> ProcessVoicemailAsync(string userId, string audioFilePath, string callerNumber);
        Task<ApiResponse<SubscriptionStatus>> GetSubscriptionStatusAsync(string userId);
        Task<ApiResponse<CheckoutSession>> CreateCheckoutSessionAsync(string userId, string tier, string email, string phone);
        Task<ApiResponse<VoicemailAnalytics>> GetAnalyticsAsync(string userId);
        Task<ApiResponse<bool>> UpdateUserPreferencesAsync(string userId, UserPreferences preferences);
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<ApiService> logger;
        private readonly JsonSerializerOptions jsonOptions;

        public ApiService(IHttpClientFactory httpClientFactory, ILogger<ApiService> logger)
        {
            this.httpClient = httpClientFactory.CreateClient("VisualVoicemailAPI");
            this.logger = logger;
            this.jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task<ApiResponse<List<Voicemail>>> GetVoicemailsAsync(string userId)
        {
            try
            {
                logger.LogInformation($"📱 Fetching voicemails for user: {userId}");

                var response = await httpClient.GetAsync($"api/voicemails/{userId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<List<Voicemail>>>(json);
                    
                    logger.LogInformation($"✅ Received {result?.Data?.Count ?? 0} voicemails");
                    return result ?? new ApiResponse<List<Voicemail>> { Success = false, ErrorMessage = "Invalid response" };
                }
                else
                {
                    logger.LogError($"❌ API Error: {response.StatusCode}");
                    return new ApiResponse<List<Voicemail>>
                    {
                        Success = false,
                        ErrorMessage = $"API Error: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch voicemails");
                return new ApiResponse<List<Voicemail>>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ApiResponse<Voicemail>> ProcessVoicemailAsync(string userId, string audioFilePath, string callerNumber)
        {
            try
            {
                logger.LogInformation($"🎙️ Processing voicemail for user: {userId}, caller: {callerNumber}");

                var request = new
                {
                    UserId = userId,
                    AudioFilePath = audioFilePath,
                    CallerNumber = callerNumber,
                    PreferredLanguage = "en"
                };

                var response = await httpClient.PostAsJsonAsync("voicemail/process", request);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<Voicemail>>(json);
                    
                    logger.LogInformation($"✅ Voicemail processed successfully");
                    return result ?? new ApiResponse<Voicemail> { Success = false, ErrorMessage = "Invalid response" };
                }
                else
                {
                    logger.LogError($"❌ Processing failed: {response.StatusCode}");
                    return new ApiResponse<Voicemail>
                    {
                        Success = false,
                        ErrorMessage = $"Processing failed: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process voicemail");
                return new ApiResponse<Voicemail>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ApiResponse<SubscriptionStatus>> GetSubscriptionStatusAsync(string userId)
        {
            try
            {
                logger.LogInformation($"💳 Checking subscription status for user: {userId}");

                var response = await httpClient.GetAsync($"subscription/status/{userId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<SubscriptionStatus>>(json);
                    
                    logger.LogInformation($"✅ Subscription status: {result?.Data?.Tier ?? "unknown"}");
                    return result ?? new ApiResponse<SubscriptionStatus> { Success = false, ErrorMessage = "Invalid response" };
                }
                else
                {
                    return new ApiResponse<SubscriptionStatus>
                    {
                        Success = false,
                        ErrorMessage = $"Failed to get subscription status: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get subscription status");
                return new ApiResponse<SubscriptionStatus>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ApiResponse<CheckoutSession>> CreateCheckoutSessionAsync(string userId, string tier, string email, string phone)
        {
            try
            {
                logger.LogInformation($"💰 Creating checkout session for {tier} subscription");

                var request = new
                {
                    UserId = userId,
                    Tier = tier,
                    CustomerEmail = email,
                    PhoneNumber = phone
                };

                var response = await httpClient.PostAsJsonAsync("create-checkout-session", request);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<CheckoutSession>>(json);
                    
                    logger.LogInformation($"✅ Checkout session created: {result?.Data?.SessionId}");
                    return result ?? new ApiResponse<CheckoutSession> { Success = false, ErrorMessage = "Invalid response" };
                }
                else
                {
                    return new ApiResponse<CheckoutSession>
                    {
                        Success = false,
                        ErrorMessage = $"Checkout creation failed: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create checkout session");
                return new ApiResponse<CheckoutSession>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ApiResponse<VoicemailAnalytics>> GetAnalyticsAsync(string userId)
        {
            try
            {
                logger.LogInformation($"📊 Fetching analytics for user: {userId}");

                var response = await httpClient.GetAsync($"analytics/{userId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<VoicemailAnalytics>>(json);
                    
                    logger.LogInformation($"✅ Analytics retrieved");
                    return result ?? new ApiResponse<VoicemailAnalytics> { Success = false, ErrorMessage = "Invalid response" };
                }
                else
                {
                    return new ApiResponse<VoicemailAnalytics>
                    {
                        Success = false,
                        ErrorMessage = $"Analytics fetch failed: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get analytics");
                return new ApiResponse<VoicemailAnalytics>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<ApiResponse<bool>> UpdateUserPreferencesAsync(string userId, UserPreferences preferences)
        {
            try
            {
                logger.LogInformation($"⚙️ Updating preferences for user: {userId}");

                var response = await httpClient.PutAsJsonAsync($"api/users/{userId}/preferences", preferences);
                
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation($"✅ Preferences updated");
                    return new ApiResponse<bool> { Success = true, Data = true };
                }
                else
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        ErrorMessage = $"Preferences update failed: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update preferences");
                return new ApiResponse<bool>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    #region Data Models

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Message { get; set; }
    }

    public class CheckoutSession
    {
        public string SessionId { get; set; } = "";
        public string PaymentUrl { get; set; } = "";
        public string Tier { get; set; } = "";
        public decimal MonthlyPrice { get; set; }
        public int TrialDays { get; set; }
    }

    public class SubscriptionStatus
    {
        public bool IsActive { get; set; }
        public string Tier { get; set; } = "free";
        public string Status { get; set; } = "";
        public decimal MonthlyPrice { get; set; }
        public string[] Features { get; set; } = Array.Empty<string>();
        public DateTime? CurrentPeriodEnd { get; set; }
        public DateTime? TrialEnd { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
    }

    public class UserPreferences
    {
        public string PreferredLanguage { get; set; } = "en";
        public bool EnableSpamDetection { get; set; } = true;
        public bool EnableTranslation { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public List<string> BlockedNumbers { get; set; } = new();
    }

    #endregion
}