# Visual Voicemail App - Complete Setup Guide

## 🚀 Quick Start

This guide will help you set up and run your Visual Voicemail app on both Android and iOS platforms.

## 📋 Prerequisites

### Development Environment
- **Node.js** 18+ (for backend)
- **MongoDB** (local or cloud)
- **Android Studio** (for Android development)
- **Xcode** (for iOS development - Mac only)
- **Git** (for version control)

### Accounts & Services
- **Firebase Project** (Authentication, Cloud Messaging, Analytics)
- **Google Cloud Platform** (Speech-to-Text API, Cloud Storage)
- **Stripe Account** (Payment processing)
- **Apple Developer Account** ($99/year - for iOS App Store)
- **Google Play Console** ($25 one-time - for Play Store)

## 🔧 Installation Steps

### 1. Backend Setup

```powershell
# Navigate to backend directory
cd "C:\Users\selli\OneDrive\visial voicemail app\backend"

# Install dependencies
npm install

# Copy environment file
copy .env.example .env

# Edit .env file with your credentials
# notepad .env
```

#### Configure Environment Variables (.env)
```env
NODE_ENV=development
PORT=3000
MONGODB_URI=mongodb://localhost:27017/visual_voicemail

# Firebase Configuration
FIREBASE_PROJECT_ID=your-firebase-project-id
FIREBASE_PRIVATE_KEY_ID=your-firebase-private-key-id
FIREBASE_PRIVATE_KEY="-----BEGIN PRIVATE KEY-----\nyour-private-key\n-----END PRIVATE KEY-----\n"
FIREBASE_CLIENT_EMAIL=firebase-adminsdk@your-project.iam.gserviceaccount.com

# Stripe Configuration  
STRIPE_SECRET_KEY=sk_test_your_stripe_secret_key
STRIPE_WEBHOOK_SECRET=whsec_your_webhook_secret

# Google Cloud Configuration
GOOGLE_CLOUD_PROJECT_ID=your-google-cloud-project
```

#### Build and Start Backend
```powershell
# Build TypeScript
npm run build

# Start development server
npm run dev
```

### 2. Android App Setup

```powershell
# Navigate to Android project
cd "C:\Users\selli\OneDrive\visial voicemail app\android-app"

# Open in Android Studio
# Or use command line if you have Android CLI tools
```

#### Android Configuration Steps:

1. **Open Android Studio**
   - File → Open → Select `android-app` folder
   - Wait for Gradle sync

2. **Configure Firebase**
   - Download `google-services.json` from Firebase Console
   - Place in `android-app/app/` directory

3. **Update Package Name**
   - In `app/build.gradle`, verify `applicationId "com.visualvoicemail.android"`

4. **Build & Run**
   ```bash
   # Via Android Studio: Build → Make Project
   # Or command line:
   ./gradlew assembleDebug
   ```

### 3. iOS App Setup

```bash
# Navigate to iOS project
cd "C:\Users\selli\OneDrive\visial voicemail app\ios-app"

# Open in Xcode (Mac only)
open VisualVoicemail.xcodeproj
```

#### iOS Configuration Steps:

1. **Configure Firebase**
   - Download `GoogleService-Info.plist` from Firebase Console
   - Add to Xcode project

2. **Update Bundle Identifier**
   - In Xcode: Target → General → Bundle Identifier: `com.visualvoicemail.ios`

3. **Configure Signing**
   - Xcode → Signing & Capabilities
   - Select your Apple Developer Team

4. **Build & Run**
   - Xcode → Product → Build
   - Run on simulator or device

## 🔑 Service Configuration

### Firebase Setup
1. **Create Firebase Project**
   - Go to [Firebase Console](https://console.firebase.google.com)
   - Create new project
   - Enable Authentication, Cloud Messaging, Analytics

2. **Add Apps**
   - Add Android app with package: `com.visualvoicemail.android`
   - Add iOS app with bundle ID: `com.visualvoicemail.ios`

3. **Configure Authentication**
   - Authentication → Sign-in method
   - Enable Email/Password and Phone

### Stripe Setup
1. **Create Stripe Account**
   - Go to [Stripe Dashboard](https://dashboard.stripe.com)
   - Get API keys (Publishable & Secret)

2. **Create Products**
   - Products → Add Product
   - Name: "Visual Voicemail Premium"
   - Price: $1.99/month

3. **Configure Webhooks**
   - Webhooks → Add endpoint
   - URL: `https://your-api.com/api/subscription/webhook`
   - Events: `subscription.created`, `subscription.updated`, `subscription.deleted`

### Google Cloud Setup
1. **Enable APIs**
   - Cloud Speech-to-Text API
   - Cloud Storage API

2. **Create Service Account**
   - IAM → Service Accounts
   - Download JSON key
   - Add credentials to backend `.env`

## 📱 Development Workflow

### Running the Complete Stack

1. **Start Backend** (Terminal 1)
   ```powershell
   cd backend
   npm run dev
   ```

2. **Start Android** (Terminal 2)
   ```powershell
   cd android-app
   # Open Android Studio and run
   ```

3. **Start iOS** (Terminal 3 - Mac only)
   ```bash
   cd ios-app
   # Open Xcode and run
   ```

### Testing the App

1. **Backend Health Check**
   ```
   GET http://localhost:3000/health
   ```

2. **Register Test User**
   ```
   POST http://localhost:3000/api/auth/register
   {
     "firebaseToken": "test-token",
     "email": "test@example.com",
     "phoneNumber": "+1234567890"
   }
   ```

3. **Test Mobile Apps**
   - Register new user
   - Upload test voicemail
   - Test spam detection
   - Test subscription flow

## 📦 Deployment

### Backend Deployment (Production)

1. **Choose Hosting**
   - AWS EC2, Google Cloud Compute, or DigitalOcean
   - Docker deployment recommended

2. **Environment Setup**
   ```bash
   # Production environment
   NODE_ENV=production
   MONGODB_URI=mongodb://your-production-db
   # All other production secrets
   ```

3. **Deploy**
   ```bash
   # Build
   npm run build
   
   # Start production server
   npm start
   ```

### Android Deployment

1. **Generate Signing Key**
   ```bash
   keytool -genkey -v -keystore my-release-key.keystore -alias my-key-alias -keyalg RSA -keysize 2048 -validity 10000
   ```

2. **Build Release APK**
   ```bash
   ./gradlew assembleRelease
   ```

3. **Upload to Play Store**
   - Create Play Console account
   - Upload AAB file
   - Fill store listing
   - Submit for review

### iOS Deployment

1. **Archive Build**
   - Xcode → Product → Archive

2. **Upload to App Store**
   - Xcode Organizer → Upload to App Store Connect

3. **App Store Connect**
   - Fill app information
   - Upload screenshots
   - Submit for review

## 💰 Monetization Setup

### Subscription Configuration

**Android (Google Play Billing)**
```kotlin
// In-app product IDs
premium_monthly = "premium_monthly_199"
```

**iOS (StoreKit)**
```swift
// Product IDs in App Store Connect
premium_monthly = "premium_monthly_199"
```

### Ad Integration

**AdMob Setup**
1. Create AdMob account
2. Add app in AdMob console
3. Get App IDs:
   - Android: `ca-app-pub-XXXXXXXX~XXXXXXXXX`
   - iOS: `ca-app-pub-XXXXXXXX~XXXXXXXXX`

## 🔒 Security & Privacy

### Required Privacy Policies
- **Data Collection**: What data you collect
- **Third-party Services**: Firebase, Stripe, AdMob
- **User Rights**: GDPR, CCPA compliance

### App Store Requirements
- Privacy nutrition labels
- Data usage descriptions
- Permission explanations

## 📊 Analytics & Monitoring

### Firebase Analytics Events
```javascript
// Track key events
user_registration
voicemail_received  
subscription_purchased
spam_blocked
```

### Error Monitoring
- Firebase Crashlytics (mobile apps)
- Sentry (backend monitoring)

## 🚀 Launch Checklist

### Pre-Launch
- [ ] Backend deployed and tested
- [ ] Database populated with test data
- [ ] Firebase configured correctly
- [ ] Stripe payments working
- [ ] Push notifications working
- [ ] Both apps built and tested

### App Store Submissions
- [ ] Android: Uploaded to Play Console
- [ ] iOS: Uploaded to App Store Connect  
- [ ] Privacy policies created
- [ ] Screenshots and descriptions ready
- [ ] Pricing configured ($1.99/month)

### Marketing
- [ ] Landing page created
- [ ] Social media accounts
- [ ] App Store Optimization (ASO)
- [ ] Launch announcement ready

## 🆘 Troubleshooting

### Common Issues

1. **Firebase Connection**
   ```bash
   # Check config files exist
   android-app/app/google-services.json
   ios-app/VisualVoicemail/GoogleService-Info.plist
   ```

2. **Backend Database Connection**
   ```bash
   # Check MongoDB is running
   mongosh
   ```

3. **Build Errors**
   ```bash
   # Clean builds
   # Android:
   ./gradlew clean
   
   # iOS:
   # Xcode → Product → Clean Build Folder
   ```

### Support Resources
- Firebase Documentation
- Stripe Documentation  
- React Native/Android/iOS official docs
- Stack Overflow for specific issues

## 📈 Next Steps

1. **Launch MVP** with core features
2. **Gather user feedback**
3. **Iterate and improve**
4. **Add advanced features**:
   - AI-powered spam detection
   - Voice assistant integration
   - Multi-language support
   - Advanced analytics

---

**🎉 Congratulations!** Your Visual Voicemail app is now ready for development and deployment. The complete codebase includes:

- ✅ **Backend API** (Node.js + TypeScript)
- ✅ **Android App** (Kotlin + Jetpack Compose)  
- ✅ **iOS App** (Swift + SwiftUI)
- ✅ **Subscription System** ($1.99/month)
- ✅ **Spam Detection & Call Blocking**
- ✅ **Push Notifications**
- ✅ **Ad Integration**

Start with the backend, then build and test each mobile platform. Good luck with your app launch! 🚀