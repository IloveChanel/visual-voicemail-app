# Enhanced Visual Voicemail Pro Backend

## 🚀 Overview

Enhanced backend system with comprehensive **coupon, trial, and promo flexibility** integrated with Stripe, plus a **developer whitelist** allowing free no-code access for selected testers. Complete API and backend logic to control access accordingly.

## ✨ New Enhanced Features

### 🎫 Coupon System
- **Flexible discount types**: Percentage and fixed amount discounts
- **Usage tracking**: Per-user and global usage limits
- **Expiration management**: Time-based coupon expiration
- **Tier-specific coupons**: Apply to specific subscription tiers
- **Real-time validation**: Instant coupon validation during checkout

### 🔐 Developer Whitelist
- **Free access bypass**: Whitelisted developers get free premium access
- **Role-based permissions**: Admin, Developer, Tester roles
- **No-code testing**: Instant access without payment processing
- **Admin management**: Secure endpoints to manage whitelist

### 💳 Enhanced Stripe Integration
- **Automatic coupon application**: Seamless discount integration
- **Whitelist bypass**: Skip payment for authorized developers
- **Trial period flexibility**: Configurable trial lengths
- **Enhanced metadata**: Rich subscription tracking

### 🛡️ Security & Authentication
- **JWT authentication**: Secure API access with role validation
- **Admin-only endpoints**: Protected administrative functions
- **Rate limiting ready**: Prepared for production deployment
- **Audit logging**: Comprehensive activity tracking

## 📊 Database Schema

### Core Tables
- **Users**: Enhanced with whitelist and premium flags
- **Coupons**: Complete coupon management system
- **DeveloperWhitelists**: Authorized developer access
- **CouponUsages**: Usage tracking and analytics
- **Voicemails**: Original functionality maintained

### Key Relationships
```
Users (1:N) CouponUsages (N:1) Coupons
Users (1:1) DeveloperWhitelists (Email match)
Users (1:N) Voicemails (Original relationship)
```

## 🔧 Setup Instructions

### 1. Database Setup
```powershell
# Run the setup script with database initialization
.\setup-enhanced-backend.ps1 -SetupDatabase
```

Or manually:
```sql
-- Execute the migration script
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "VisualVoicemailPro" -i .\Migrations\InitialEnhancedMigration.sql
```

### 2. Configuration
Update `appsettings.json` or `appsettings.Enhanced.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your SQL Server connection string"
  },
  "JWT": {
    "SecretKey": "YourSuperSecretJWTKeyThatIsAtLeast32CharactersLong!@#$%",
    "Issuer": "VisualVoicemailPro",
    "Audience": "VisualVoicemailProUsers"
  },
  "Stripe": {
    "SecretKey": "sk_test_your_stripe_secret_key",
    "WebhookSecret": "whsec_your_webhook_secret"
  }
}
```

### 3. Environment Variables
```bash
# Set these environment variables
STRIPE_SECRET_KEY=sk_test_your_actual_stripe_key
JWT_SECRET_KEY=your_jwt_secret_key
GOOGLE_CLOUD_PROJECT_ID=your_project_id
```

### 4. Start the Server
```powershell
# Full setup and start
.\setup-enhanced-backend.ps1 -All

# Or just start the server
dotnet run
```

## 📡 API Endpoints

### 🎫 Coupon Management

#### User Endpoints (Public)
```
POST /api/user/validate-coupon
POST /api/user/check-whitelist
POST /api/user/subscription-status
```

#### Admin Endpoints (JWT Required)
```
GET    /api/admin/coupons          # List all coupons
POST   /api/admin/coupons          # Create new coupon
PUT    /api/admin/coupons/{id}     # Update coupon
DELETE /api/admin/coupons/{id}     # Delete coupon

GET    /api/admin/whitelist        # List whitelist entries
POST   /api/admin/whitelist        # Add to whitelist
DELETE /api/admin/whitelist/{id}   # Remove from whitelist

GET    /api/admin/usage-analytics  # Coupon usage analytics
```

### 💳 Enhanced Subscription Flow

#### Create Checkout Session (Enhanced)
```
POST /create-checkout-session
{
  "userId": "user123",
  "tier": "Pro",
  "customerEmail": "user@example.com",
  "phoneNumber": "+1234567890",
  "couponCode": "WELCOME50"  // Optional
}
```

## 🧪 Testing

### Run System Tests
```powershell
# Run comprehensive tests
.\setup-enhanced-backend.ps1 -RunTests

# Or manually
dotnet run --project Tests
```

### Test Scenarios Covered
- ✅ Coupon validation (valid/invalid/expired)
- ✅ Whitelist checking and bypass
- ✅ Enhanced subscription flow
- ✅ Admin endpoint security
- ✅ JWT authentication

### Sample Test Data
The migration includes these default test coupons:
- `WELCOME50` - 50% off first month
- `FREEMONTH` - 100% off (free month)
- `SAVE20` - 20% off any subscription
- `NEWUSER2024` - $2 fixed discount

Default whitelist entries:
- `developer@visualvoicemail.pro` (Admin)
- `test@visualvoicemail.pro` (Developer)
- `copilot@visualvoicemail.pro` (Developer)

## 🔐 Authentication & Authorization

### JWT Token Structure
```json
{
  "sub": "user_id",
  "email": "user@example.com",
  "role": "Admin|Developer|User",
  "isWhitelisted": true,
  "exp": 1640995200
}
```

### Role Permissions
- **Admin**: Full access to all endpoints
- **Developer**: Whitelist access + user endpoints
- **User**: Public endpoints only

### Getting an Admin Token
```csharp
// Use the UserController login endpoint
POST /api/user/login
{
  "email": "admin@visualvoicemail.pro",
  "password": "your_password"
}
```

## 🚀 Deployment

### Production Checklist
- [ ] Update connection string for production SQL Server
- [ ] Set production Stripe API keys
- [ ] Configure secure JWT secret (32+ characters)
- [ ] Enable HTTPS in production
- [ ] Set up proper logging
- [ ] Configure CORS for your domain
- [ ] Set up monitoring and alerts

### Environment-Specific Settings
```json
// appsettings.Production.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Production SQL Server connection"
  },
  "Stripe": {
    "SecretKey": "sk_live_production_key"
  },
  "Security": {
    "RequireHttps": true,
    "EnableRateLimiting": true
  }
}
```

## 📊 Monitoring & Analytics

### Built-in Analytics
- Coupon usage tracking
- Subscription conversion rates
- Whitelist access patterns
- Revenue impact of discounts

### Logging
Comprehensive logging for:
- Authentication attempts
- Coupon validations
- Subscription events
- Admin actions
- Error tracking

## 🛠️ Development

### Adding New Coupon Types
1. Update `CouponDiscountType` enum in `Models/Enhanced.cs`
2. Modify validation logic in `StripeIntegrationService.cs`
3. Update admin UI for new coupon type

### Extending Whitelist Functionality
1. Add new fields to `DeveloperWhitelist` model
2. Update validation in controllers
3. Modify JWT claims as needed

## 📞 Support

### Common Issues

**Database Connection Issues**
- Ensure SQL Server LocalDB is installed
- Check connection string format
- Verify database exists and migrations ran

**Stripe Integration Issues**
- Verify API keys are correct (test vs live)
- Check webhook endpoint configuration
- Ensure proper error handling

**Authentication Issues**
- Verify JWT secret key is set
- Check token expiration
- Ensure proper role claims

### Getting Help
1. Check logs in console output
2. Run health endpoint: `GET /health`
3. Use the test script to validate setup
4. Review error messages in API responses

## 🎯 Next Steps

1. **Frontend Integration**
   - Add coupon code input fields
   - Implement whitelist login flow
   - Update subscription UI

2. **Enhanced Features**
   - Email notifications for coupons
   - Advanced analytics dashboard
   - Bulk whitelist management

3. **Production Deployment**
   - Set up CI/CD pipeline
   - Configure production database
   - Implement monitoring

---

**🎉 Your enhanced Visual Voicemail Pro backend is ready with comprehensive coupon, trial, and whitelist functionality!**