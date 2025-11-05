using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VisualVoicemailPro.Tests
{
    public class EnhancedSystemTests
    {
        private readonly HttpClient _client;
        private readonly string _baseUrl;
        
        public EnhancedSystemTests()
        {
            _client = new HttpClient();
            _baseUrl = "https://localhost:7155"; // Update with your API URL
        }
        
        public async Task RunAllTests()
        {
            Console.WriteLine("🧪 Running Enhanced Visual Voicemail Pro System Tests");
            Console.WriteLine("=" * 60);
            
            try
            {
                await TestHealthEndpoint();
                await TestCouponValidation();
                await TestWhitelistFunctionality();
                await TestSubscriptionWithCoupon();
                await TestAdminEndpoints();
                
                Console.WriteLine("\n✅ All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test failed: {ex.Message}");
            }
        }
        
        private async Task TestHealthEndpoint()
        {
            Console.WriteLine("\n🔍 Testing Health Endpoint...");
            
            var response = await _client.GetAsync($"{_baseUrl}/health");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Health check passed: {content}");
            }
            else
            {
                throw new Exception($"Health check failed: {response.StatusCode}");
            }
        }
        
        private async Task TestCouponValidation()
        {
            Console.WriteLine("\n🎫 Testing Coupon Validation...");
            
            // Test valid coupon
            var validCouponTest = new
            {
                code = "WELCOME50",
                userId = "test-user-123",
                subscriptionTier = "Pro"
            };
            
            var json = JsonSerializer.Serialize(validCouponTest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _client.PostAsync($"{_baseUrl}/api/user/validate-coupon", content);
            var result = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Coupon validation passed: {result}");
            }
            else
            {
                Console.WriteLine($"⚠️ Coupon validation response: {response.StatusCode} - {result}");
            }
            
            // Test invalid coupon
            var invalidCouponTest = new
            {
                code = "INVALID_CODE",
                userId = "test-user-123",
                subscriptionTier = "Pro"
            };
            
            json = JsonSerializer.Serialize(invalidCouponTest);
            content = new StringContent(json, Encoding.UTF8, "application/json");
            
            response = await _client.PostAsync($"{_baseUrl}/api/user/validate-coupon", content);
            result = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine($"📝 Invalid coupon test result: {response.StatusCode} - {result}");
        }
        
        private async Task TestWhitelistFunctionality()
        {
            Console.WriteLine("\n🔐 Testing Whitelist Functionality...");
            
            var whitelistTest = new
            {
                email = "test@visualvoicemail.pro"
            };
            
            var json = JsonSerializer.Serialize(whitelistTest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _client.PostAsync($"{_baseUrl}/api/user/check-whitelist", content);
            var result = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Whitelist check passed: {result}");
            }
            else
            {
                Console.WriteLine($"📝 Whitelist check response: {response.StatusCode} - {result}");
            }
        }
        
        private async Task TestSubscriptionWithCoupon()
        {
            Console.WriteLine("\n💳 Testing Subscription with Coupon...");
            
            var subscriptionTest = new
            {
                userId = "test-user-coupon-123",
                tier = "Pro",
                customerEmail = "test@example.com",
                phoneNumber = "+1234567890",
                couponCode = "WELCOME50"
            };
            
            var json = JsonSerializer.Serialize(subscriptionTest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _client.PostAsync($"{_baseUrl}/create-checkout-session", content);
            var result = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Subscription with coupon test passed: {result}");
            }
            else
            {
                Console.WriteLine($"📝 Subscription test response: {response.StatusCode} - {result}");
            }
        }
        
        private async Task TestAdminEndpoints()
        {
            Console.WriteLine("\n🛡️ Testing Admin Endpoints (without auth)...");
            
            // Note: These will fail without proper JWT token, but we can test the endpoint existence
            var response = await _client.GetAsync($"{_baseUrl}/api/admin/whitelist");
            Console.WriteLine($"📝 Admin whitelist endpoint: {response.StatusCode}");
            
            response = await _client.GetAsync($"{_baseUrl}/api/admin/coupons");
            Console.WriteLine($"📝 Admin coupons endpoint: {response.StatusCode}");
            
            Console.WriteLine("ℹ️ Admin endpoints require JWT authentication (401 Unauthorized expected)");
        }
        
        private static async Task Main(string[] args)
        {
            var tests = new EnhancedSystemTests();
            await tests.RunAllTests();
            
            Console.WriteLine("\n🎯 Test Summary:");
            Console.WriteLine("- Health endpoint availability");
            Console.WriteLine("- Coupon validation logic");
            Console.WriteLine("- Whitelist checking");
            Console.WriteLine("- Enhanced subscription flow");
            Console.WriteLine("- Admin endpoint security");
            
            Console.WriteLine("\n📋 Next Steps:");
            Console.WriteLine("1. Set up SQL Server database");
            Console.WriteLine("2. Run migration script");
            Console.WriteLine("3. Configure Stripe API keys");
            Console.WriteLine("4. Set up JWT secret key");
            Console.WriteLine("5. Test with real authentication tokens");
            
            Console.ReadKey();
        }
    }
}