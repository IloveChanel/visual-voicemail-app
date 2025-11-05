import Foundation

// MARK: - User Model
struct User: Codable, Identifiable {
    let id: String
    let firebaseUid: String
    let email: String
    let phoneNumber: String
    let displayName: String?
    let profilePicture: String?
    let isVerified: Bool
    let subscription: Subscription
    let preferences: UserPreferences
    let deviceTokens: [String]
    let lastLoginAt: Date?
    let createdAt: Date
    let updatedAt: Date
    let isActive: Bool
}

// MARK: - Subscription Model
struct Subscription: Codable {
    let isActive: Bool
    let plan: SubscriptionPlan
    let startDate: Date?
    let endDate: Date?
    let stripeCustomerId: String?
    let stripeSubscriptionId: String?
}

enum SubscriptionPlan: String, Codable, CaseIterable {
    case free = "free"
    case premium = "premium"
    
    var displayName: String {
        switch self {
        case .free: return "Free"
        case .premium: return "Premium"
        }
    }
    
    var monthlyPrice: Double {
        switch self {
        case .free: return 0.0
        case .premium: return 1.99
        }
    }
}

// MARK: - User Preferences
struct UserPreferences: Codable {
    let autoTranscription: Bool
    let spamBlocking: Bool
    let notifications: NotificationPreferences
    let audioQuality: AudioQuality
}

struct NotificationPreferences: Codable {
    let newVoicemail: Bool
    let spamDetection: Bool
    let promotions: Bool
}

// MARK: - Voicemail Model
struct Voicemail: Codable, Identifiable {
    let id: String
    let userId: String
    let callerNumber: String
    let callerName: String?
    let duration: Int // seconds
    let audioUrl: String
    let transcription: String?
    let transcriptionStatus: TranscriptionStatus
    let isSpam: Bool
    let spamConfidence: Int? // 0-100
    let isRead: Bool
    let isArchived: Bool
    let isFavorite: Bool
    let timestamp: Date
    let metadata: VoicemailMetadata
    let tags: [String]
    let createdAt: Date
    let updatedAt: Date
    
    // Computed properties
    var formattedDuration: String {
        let minutes = duration / 60
        let seconds = duration % 60
        return String(format: "%d:%02d", minutes, seconds)
    }
    
    var formattedTimestamp: String {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .abbreviated
        return formatter.localizedString(for: timestamp, relativeTo: Date())
    }
    
    var callerDisplayName: String {
        return callerName ?? callerNumber
    }
}

// MARK: - Supporting Enums
enum TranscriptionStatus: String, Codable {
    case pending = "pending"
    case completed = "completed"
    case failed = "failed"
    case notRequested = "not_requested"
    
    var displayName: String {
        switch self {
        case .pending: return "Processing..."
        case .completed: return "Completed"
        case .failed: return "Failed"
        case .notRequested: return "Not Available"
        }
    }
}

enum AudioQuality: String, Codable, CaseIterable {
    case low = "low"
    case medium = "medium"
    case high = "high"
    
    var displayName: String {
        switch self {
        case .low: return "Low"
        case .medium: return "Medium"
        case .high: return "High"
        }
    }
}

enum VoicemailSource: String, Codable {
    case carrier = "carrier"
    case app = "app"
    case `import` = "import"
}

// MARK: - Voicemail Metadata
struct VoicemailMetadata: Codable {
    let fileSize: Int?
    let audioFormat: String?
    let quality: AudioQuality
    let source: VoicemailSource
}

// MARK: - API Response Models
struct APIResponse<T: Codable>: Codable {
    let message: String?
    let data: T?
    let error: String?
    let details: [String]?
}

struct VoicemailListResponse: Codable {
    let voicemails: [Voicemail]
    let pagination: Pagination
    let stats: VoicemailStats
    let filter: String
}

struct Pagination: Codable {
    let page: Int
    let limit: Int
    let total: Int
    let totalPages: Int
    let hasNext: Bool
    let hasPrev: Bool
}

struct VoicemailStats: Codable {
    let total: Int
    let unread: Int
    let spam: Int
    let favorites: Int
}

// MARK: - Authentication Models
struct AuthRequest: Codable {
    let firebaseToken: String
    let phoneNumber: String?
    let email: String?
    let displayName: String?
}

struct AuthResponse: Codable {
    let message: String
    let user: User
}

// MARK: - Subscription Models
struct SubscriptionStatus: Codable {
    let subscription: Subscription
    let features: SubscriptionFeatures
}

struct SubscriptionFeatures: Codable {
    let unlimitedVoicemails: Bool
    let transcription: Bool
    let advancedSpamDetection: Bool
    let noAds: Bool
}

struct CreateSubscriptionRequest: Codable {
    let priceId: String
    let paymentMethodId: String
}

// MARK: - Spam Detection Models
struct SpamCheckRequest: Codable {
    let phoneNumber: String
}

struct SpamCheckResponse: Codable {
    let phoneNumber: String
    let isSpam: Bool
    let confidence: Int
    let source: String
}

// MARK: - Error Models
struct APIError: Error, Codable {
    let error: String
    let message: String
    let details: [String]?
    
    var localizedDescription: String {
        return message
    }
}

// MARK: - Filter Types
enum VoicemailFilter: String, CaseIterable {
    case all = "all"
    case unread = "unread"
    case spam = "spam"
    case favorites = "favorites"
    case archived = "archived"
    
    var displayName: String {
        switch self {
        case .all: return "All"
        case .unread: return "Unread"
        case .spam: return "Spam"
        case .favorites: return "Favorites"
        case .archived: return "Archived"
        }
    }
    
    var systemImage: String {
        switch self {
        case .all: return "tray.full"
        case .unread: return "envelope.badge"
        case .spam: return "exclamationmark.shield"
        case .favorites: return "heart.fill"
        case .archived: return "archivebox"
        }
    }
}