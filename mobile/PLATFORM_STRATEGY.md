# Mobile App Configuration Guide

## Platform Strategy

We use **separate native projects** for Android and iOS to maximize platform-specific optimization and control.

### Benefits of Separate Native Projects:
- ✅ Full access to platform-specific APIs and features
- ✅ Optimal performance and user experience per platform
- ✅ Independent release cycles and updates
- ✅ Platform-specific UI/UX optimization
- ✅ Easier App Store compliance and approval

## Platform-Specific Configurations

### Android (Google Play Store)
- **Package Name**: `com.visualvoicemail.android`
- **Store Name**: "Visual Voicemail Pro"
- **Target SDK**: Android 14 (API 34)
- **Min SDK**: Android 5.0 (API 21)

### iOS (Apple App Store)  
- **Bundle ID**: `com.visualvoicemail.ios`
- **Store Name**: "Visual Voicemail Pro"
- **Deployment Target**: iOS 13.0+
- **Device Support**: iPhone & iPad

## Key Differences to Handle

### 1. Store Requirements
```
Android Play Store:
- 64-bit requirement
- Target API level requirements
- Privacy policy in store listing
- In-app billing setup

iOS App Store:
- App Transport Security (ATS)
- Privacy usage descriptions
- StoreKit for subscriptions
- TestFlight for beta testing
```

### 2. Platform-Specific Features
```
Android:
- CallLog API for call history
- Telecom framework integration
- Android permissions model

iOS:
- CallKit for call handling
- CallDirectory extension for spam blocking
- iOS permission dialogs
```

### 3. Build Configurations
```
Development Builds:
- Android: Debug APK
- iOS: Development provisioning

Production Builds:
- Android: Signed AAB (Android App Bundle)
- iOS: App Store provisioning profile
```

## Deployment Strategy

### Phase 1: MVP Development
1. Build core features in React Native
2. Test on both Android and iOS simulators
3. Handle platform-specific UI differences

### Phase 2: Store Preparation
1. Create separate developer accounts:
   - Google Play Console ($25 one-time)
   - Apple Developer Program ($99/year)
2. Configure app store listings
3. Prepare marketing materials for each platform

### Phase 3: Submission
1. Submit Android version to Google Play Store
2. Submit iOS version to Apple App Store
3. Different review timelines (Android: hours, iOS: 1-7 days)

## Revenue Model Per Platform

### Subscription Pricing
- **Android**: $1.99/month (Google Play Billing)
- **iOS**: $1.99/month (Apple StoreKit)
- Both platforms take 30% commission (15% after year 1)

### Advertising Integration
- **Android**: Google AdMob
- **iOS**: Apple Search Ads + AdMob
- Different ad formats and policies per platform

## Technical Implementation

The codebase will include:
```
mobile/
├── src/
│   ├── components/common/     # Shared components
│   ├── components/android/    # Android-specific components
│   ├── components/ios/        # iOS-specific components
│   ├── services/platform/     # Platform-specific services
│   └── utils/platform.ts      # Platform detection utilities
├── android/                   # Android native code
└── ios/                      # iOS native code
```

## Next Steps

Would you like me to:
1. ✅ Continue with the single codebase approach (recommended)
2. Create separate Android and iOS projects
3. Focus on one platform first (Android is typically easier to start with)

The single codebase approach will save you significant time and money while reaching both markets effectively!