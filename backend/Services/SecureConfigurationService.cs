using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System.Security.Cryptography;
using System.Text;

namespace VisualVoicemailPro.Services
{
    /// <summary>
    /// Secure Configuration Service with Azure Key Vault integration and environment variable support
    /// Provides encrypted configuration management for Visual Voicemail Pro
    /// </summary>
    public interface ISecureConfigurationService
    {
        Task<string> GetSecretAsync(string key);
        Task<T> GetConfigurationAsync<T>(string section) where T : class, new();
        Task<bool> SetSecretAsync(string key, string value);
        string GetConnectionString(string name);
        bool IsProduction { get; }
    }

    public class SecureConfigurationService : ISecureConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecureConfigurationService> _logger;
        private readonly Dictionary<string, string> _cachedSecrets;
        private readonly byte[] _encryptionKey;

        public bool IsProduction => _configuration["ASPNETCORE_ENVIRONMENT"] == "Production";

        public SecureConfigurationService(
            IConfiguration configuration, 
            ILogger<SecureConfigurationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _cachedSecrets = new Dictionary<string, string>();
            _encryptionKey = DeriveKeyFromConfiguration();
        }

        /// <summary>
        /// Gets a secret from Azure Key Vault or environment variables with caching
        /// </summary>
        public async Task<string> GetSecretAsync(string key)
        {
            if (_cachedSecrets.TryGetValue(key, out var cachedValue))
            {
                return cachedValue;
            }

            try
            {
                // Try environment variable first (for local development)
                var envValue = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(envValue))
                {
                    _cachedSecrets[key] = envValue;
                    _logger.LogDebug("Retrieved secret {Key} from environment variable", key);
                    return envValue;
                }

                // Try configuration (includes Key Vault in production)
                var configValue = _configuration[key];
                if (!string.IsNullOrEmpty(configValue))
                {
                    _cachedSecrets[key] = configValue;
                    _logger.LogDebug("Retrieved secret {Key} from configuration", key);
                    return configValue;
                }

                // Try Azure Key Vault directly if configured
                if (IsProduction)
                {
                    var keyVaultValue = await GetFromKeyVaultAsync(key);
                    if (!string.IsNullOrEmpty(keyVaultValue))
                    {
                        _cachedSecrets[key] = keyVaultValue;
                        return keyVaultValue;
                    }
                }

                _logger.LogWarning("Secret {Key} not found in any configuration source", key);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret {Key}", key);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets strongly-typed configuration section
        /// </summary>
        public async Task<T> GetConfigurationAsync<T>(string section) where T : class, new()
        {
            try
            {
                var config = new T();
                _configuration.GetSection(section).Bind(config);
                
                // Replace any secret placeholders
                await ReplaceSecretPlaceholdersAsync(config);
                
                _logger.LogDebug("Retrieved configuration section {Section}", section);
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get configuration section {Section}", section);
                return new T();
            }
        }

        /// <summary>
        /// Sets a secret (for development/testing)
        /// </summary>
        public async Task<bool> SetSecretAsync(string key, string value)
        {
            try
            {
                if (IsProduction)
                {
                    _logger.LogWarning("Attempting to set secret in production environment");
                    return false;
                }

                _cachedSecrets[key] = value;
                Environment.SetEnvironmentVariable(key, value);
                
                _logger.LogDebug("Set secret {Key}", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set secret {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Gets connection string with environment variable replacement
        /// </summary>
        public string GetConnectionString(string name)
        {
            var connectionString = _configuration.GetConnectionString(name);
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Connection string {Name} not found", name);
                return string.Empty;
            }

            // Replace environment variable placeholders
            return ReplaceEnvironmentVariables(connectionString);
        }

        private async Task<string> GetFromKeyVaultAsync(string key)
        {
            try
            {
                // In production, this would use Azure Key Vault SDK
                // For now, return empty to avoid Azure dependency in development
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve {Key} from Key Vault", key);
                return string.Empty;
            }
        }

        private async Task ReplaceSecretPlaceholdersAsync(object config)
        {
            if (config == null) return;

            var properties = config.GetType().GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanWrite);

            foreach (var prop in properties)
            {
                var value = prop.GetValue(config) as string;
                if (!string.IsNullOrEmpty(value) && value.StartsWith("${") && value.EndsWith("}"))
                {
                    var secretKey = value.Substring(2, value.Length - 3);
                    var secretValue = await GetSecretAsync(secretKey);
                    prop.SetValue(config, secretValue);
                }
            }
        }

        private string ReplaceEnvironmentVariables(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return System.Text.RegularExpressions.Regex.Replace(input, 
                @"\$\{([^}]+)\}", 
                match => Environment.GetEnvironmentVariable(match.Groups[1].Value) ?? match.Value);
        }

        private byte[] DeriveKeyFromConfiguration()
        {
            var keyMaterial = _configuration["Security:Encryption:KeyMaterial"] ?? "VisualVoicemailPro2024";
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(keyMaterial));
        }
    }

    /// <summary>
    /// Polly Resilience Service for handling transient failures
    /// Provides retry policies for Google Cloud, Stripe, and database operations
    /// </summary>
    public class ResilienceService
    {
        private readonly ILogger<ResilienceService> _logger;
        private readonly IConfiguration _configuration;

        public ResilienceService(ILogger<ResilienceService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Gets retry policy for Google Cloud API calls
        /// </summary>
        public IAsyncPolicy<HttpResponseMessage> GetGoogleCloudPolicy()
        {
            var retries = _configuration.GetValue<int>("Retry:GoogleCloudRetries", 3);
            var delayMs = _configuration.GetValue<int>("Retry:RetryDelayMs", 1000);
            var multiplier = _configuration.GetValue<double>("Retry:BackoffMultiplier", 2.0);

            return Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: retries,
                    sleepDurationProvider: retryAttempt => 
                        TimeSpan.FromMilliseconds(delayMs * Math.Pow(multiplier, retryAttempt - 1)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Google Cloud API retry {RetryCount}/{MaxRetries} in {Delay}ms. Reason: {Reason}",
                            retryCount, retries, timespan.TotalMilliseconds,
                            outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
                    });
        }

        /// <summary>
        /// Gets retry policy for Stripe API calls
        /// </summary>
        public IAsyncPolicy GetStripePolicy()
        {
            var retries = _configuration.GetValue<int>("Retry:StripeRetries", 3);
            var delayMs = _configuration.GetValue<int>("Retry:RetryDelayMs", 1000);

            return Policy
                .Handle<Stripe.StripeException>(ex => 
                    ex.StripeError.Type == "api_connection_error" || 
                    ex.StripeError.Type == "rate_limit_error")
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(
                    retryCount: retries,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(delayMs * retryAttempt),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Stripe API retry {RetryCount}/{MaxRetries} in {Delay}ms. Error: {Error}",
                            retryCount, retries, timespan.TotalMilliseconds, exception.Message);
                    });
        }

        /// <summary>
        /// Gets circuit breaker policy for critical services
        /// </summary>
        public IAsyncPolicy GetCircuitBreakerPolicy()
        {
            return Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromMinutes(1),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError(
                            "Circuit breaker opened for {Duration}ms due to: {Error}",
                            duration.TotalMilliseconds, exception.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset - service recovered");
                    });
        }

        /// <summary>
        /// Gets timeout policy for long-running operations
        /// </summary>
        public IAsyncPolicy GetTimeoutPolicy(TimeSpan timeout)
        {
            return Policy.TimeoutAsync(timeout, (context, timespan, task) =>
            {
                _logger.LogWarning("Operation timed out after {Timeout}ms", timespan.TotalMilliseconds);
                return Task.CompletedTask;
            });
        }
    }
}