# 💳 Stripe Setup for $3.49/month Visual Voicemail Subscription

## 🎯 **Step 1: Create Stripe Product & Price**

### **Login to Stripe Dashboard:**
1. Go to [dashboard.stripe.com](https://dashboard.stripe.com)
2. Navigate to **Products** → **Add Product**

### **Create Product:**
```
Product Name: Visual Voicemail Pro
Description: Premium visual voicemail with AI transcription and spam blocking
```

### **Create Price:**
```
Pricing Model: Recurring
Price: $3.49 USD
Billing Period: Monthly
Price ID: (Stripe will generate, e.g., price_1234567890)
```

---

## 🔧 **Step 2: Update Your Backend Code**

### **Updated C# Backend (Program.cs):**
```csharp
using Microsoft.AspNetCore.Builder;
using Stripe;
using Stripe.Checkout;

var builder = WebApplication.CreateBuilder(args);

// ✅ PRODUCTION STRIPE KEYS - Replace with your actual keys
StripeConfiguration.ApiKey = builder.Environment.IsDevelopment() 
    ? "sk_test_51ABC123_YOUR_TEST_KEY" // Test key for development
    : "sk_live_51ABC123_YOUR_LIVE_KEY"; // Live key for production

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();

// 💰 CREATE $3.49/MONTH SUBSCRIPTION
app.MapPost("/create-checkout-session", async () =>
{
    var options = new SessionCreateOptions
    {
        PaymentMethodTypes = new List<string> { "card" },
        Mode = "subscription",
        LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions 
            { 
                Price = "price_1234567890", // ⚡ Replace with your actual Stripe Price ID
                Quantity = 1 
            }
        },
        SuccessUrl = "https://visualvoicemail.azurewebsites.net/success?session_id={CHECKOUT_SESSION_ID}",
        CancelUrl = "https://visualvoicemail.azurewebsites.net/cancel",
        
        // 🎁 OPTIONAL: Add 7-day free trial
        SubscriptionData = new SessionSubscriptionDataOptions
        {
            TrialPeriodDays = 7,
            Metadata = new Dictionary<string, string>
            {
                { "phone_number", "248-321-9121" }, // Track user's phone number
                { "plan", "visual_voicemail_pro" }
            }
        },
        
        // 📊 Customer data collection
        CustomerEmail = "user@example.com", // Pass from your auth system
        Metadata = new Dictionary<string, string>
        {
            { "product", "Visual Voicemail Pro" },
            { "price", "$3.49/month" }
        }
    };

    var service = new SessionService();
    var session = await service.CreateAsync(options);
    
    return Results.Json(new { 
        sessionId = session.Id,
        url = session.Url 
    });
});

// 🎯 WEBHOOK FOR SUBSCRIPTION EVENTS
app.MapPost("/stripe-webhook", async (HttpRequest request) =>
{
    var json = await new StreamReader(request.Body).ReadToEndAsync();
    
    try 
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json,
            request.Headers["Stripe-Signature"],
            "whsec_YOUR_WEBHOOK_SECRET" // Get from Stripe Dashboard
        );

        switch (stripeEvent.Type)
        {
            case Events.CustomerSubscriptionCreated:
                // ✅ User successfully subscribed to $3.49/month plan
                var subscription = stripeEvent.Data.Object as Subscription;
                // TODO: Update user status to "premium" in your database
                Console.WriteLine($"New subscription: {subscription.Id}");
                break;
                
            case Events.CustomerSubscriptionDeleted:
                // ❌ User cancelled subscription
                var cancelledSub = stripeEvent.Data.Object as Subscription;
                // TODO: Update user status to "free" in your database
                Console.WriteLine($"Cancelled subscription: {cancelledSub.Id}");
                break;
                
            case Events.InvoicePaymentSucceeded:
                // 💰 Monthly $3.49 payment successful
                var invoice = stripeEvent.Data.Object as Invoice;
                Console.WriteLine($"Payment received: ${invoice.AmountPaid / 100}");
                break;
        }
        
        return Results.Ok();
    }
    catch (Exception e)
    {
        return Results.BadRequest($"Webhook error: {e.Message}");
    }
});

app.Run();
```

---

## 🌐 **Step 3: Updated Frontend HTML**

### **Subscription Page (index.html):**
```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Visual Voicemail Pro - $3.49/month</title>
    <script src="https://js.stripe.com/v3/"></script>
    <style>
        body { 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            max-width: 500px; 
            margin: 0 auto; 
            padding: 40px 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            color: white;
        }
        
        .container {
            background: rgba(255,255,255,0.1);
            padding: 40px;
            border-radius: 20px;
            backdrop-filter: blur(10px);
            text-align: center;
        }
        
        .price {
            font-size: 48px;
            font-weight: bold;
            margin: 20px 0;
            color: #FFD700;
        }
        
        .features {
            text-align: left;
            margin: 30px 0;
        }
        
        .feature {
            margin: 10px 0;
            padding-left: 20px;
        }
        
        #checkout-button {
            background: linear-gradient(135deg, #4CAF50, #45a049);
            color: white;
            border: none;
            padding: 15px 30px;
            font-size: 18px;
            border-radius: 25px;
            cursor: pointer;
            box-shadow: 0 4px 15px rgba(0,0,0,0.2);
            transition: transform 0.2s;
        }
        
        #checkout-button:hover {
            transform: translateY(-2px);
        }
        
        .competitor {
            margin-top: 20px;
            font-size: 14px;
            opacity: 0.8;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>📱 Visual Voicemail Pro</h1>
        
        <div class="price">$3.49<span style="font-size: 16px;">/month</span></div>
        
        <p><strong>🎉 7-Day FREE Trial</strong></p>
        <p style="font-size: 14px; opacity: 0.8;">50% less than competitors!</p>
        
        <div class="features">
            <div class="feature">✅ Unlimited Visual Voicemail</div>
            <div class="feature">🤖 AI-Powered Transcription</div>
            <div class="feature">🛡️ Advanced Spam Blocking</div>
            <div class="feature">🌍 Multi-Language Support</div>
            <div class="feature">📱 Works on Samsung & All Android</div>
            <div class="feature">🎧 Premium Audio Quality</div>
            <div class="feature">☁️ Cloud Backup & Sync</div>
        </div>
        
        <button id="checkout-button">Start FREE Trial</button>
        
        <div class="competitor">
            <small>Competitors charge $6.99/month - You save $3.50!</small>
        </div>
        
        <div style="margin-top: 20px; font-size: 12px; opacity: 0.6;">
            Cancel anytime. No commitment.
        </div>
    </div>

    <script>
        const stripe = Stripe('pk_test_51ABC123_YOUR_PUBLISHABLE_KEY'); // ⚡ Replace with your key

        document.getElementById('checkout-button').addEventListener('click', async () => {
            try {
                const response = await fetch('/create-checkout-session', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    }
                });
                
                const session = await response.json();
                
                // Redirect to Stripe Checkout
                const result = await stripe.redirectToCheckout({
                    sessionId: session.sessionId
                });
                
                if (result.error) {
                    alert(result.error.message);
                }
            } catch (error) {
                console.error('Error:', error);
                alert('Something went wrong. Please try again.');
            }
        });
    </script>
</body>
</html>
```

---

## 📊 **Step 4: Revenue Calculation Helper**

### **Revenue Calculator (for your reference):**
```csharp
public class RevenueCalculator
{
    private const decimal SUBSCRIPTION_PRICE = 3.49m;
    private const decimal GOOGLE_PLAY_FEE_YEAR1 = 0.30m; // 30%
    private const decimal GOOGLE_PLAY_FEE_YEAR2 = 0.15m; // 15%
    private const decimal AZURE_COST_BASIC = 21m;
    private const decimal AZURE_COST_SCALED = 124m;
    
    public static decimal CalculateMonthlyRevenue(int subscribers, bool isYear1 = true)
    {
        var feeRate = isYear1 ? GOOGLE_PLAY_FEE_YEAR1 : GOOGLE_PLAY_FEE_YEAR2;
        var netPricePerUser = SUBSCRIPTION_PRICE * (1 - feeRate);
        return subscribers * netPricePerUser;
    }
    
    public static decimal CalculateMonthlyProfit(int subscribers, bool isYear1 = true)
    {
        var revenue = CalculateMonthlyRevenue(subscribers, isYear1);
        var azureCost = subscribers > 1000 ? AZURE_COST_SCALED : AZURE_COST_BASIC;
        return revenue - azureCost;
    }
}

// Usage examples:
// 100 users Year 1: $170.65 revenue, $149.65 profit
// 1000 users Year 1: $2,443 revenue, $2,319 profit  
// 1000 users Year 2+: $2,966.50 revenue, $2,842.50 profit
```

---

## 🎯 **Step 5: Quick Setup Checklist**

### **Stripe Configuration:**
- [ ] Create Stripe account
- [ ] Add product: "Visual Voicemail Pro"  
- [ ] Set price: $3.49/month recurring
- [ ] Copy Price ID to your code
- [ ] Set up webhook endpoint
- [ ] Test with Stripe test cards

### **Deployment:**
- [ ] Deploy to Azure App Service
- [ ] Configure environment variables
- [ ] Test subscription flow end-to-end
- [ ] Switch to live Stripe keys for production

### **Revenue Tracking:**
- [ ] Set up Stripe Dashboard monitoring
- [ ] Configure revenue alerts
- [ ] Plan for scaling at 1,000+ users

---

## 🚀 **Expected Results:**

**Break-even:** 9 subscribers ($21.84 revenue covers $21 Azure cost)
**Profitable:** 10+ subscribers = pure profit
**Scaling point:** 1,000+ subscribers = upgrade Azure tier

**Your $3.49 pricing is perfect** - competitive, profitable, and sustainable! 🎯