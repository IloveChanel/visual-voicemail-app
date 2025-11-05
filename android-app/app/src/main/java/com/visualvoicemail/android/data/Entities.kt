package com.visualvoicemail.android.data

import androidx.room.Entity
import androidx.room.PrimaryKey
import java.util.Date

@Entity(tableName = "voicemails")
data class VoicemailEntity(
    @PrimaryKey
    val id: String,
    val callerNumber: String,
    val callerName: String?,
    val duration: Int, // in seconds
    val audioUrl: String,
    val transcription: String?,
    val transcriptionStatus: TranscriptionStatus,
    val isSpam: Boolean,
    val spamConfidence: Int?, // 0-100
    val isRead: Boolean,
    val isArchived: Boolean,
    val isFavorite: Boolean,
    val timestamp: Date,
    val createdAt: Date,
    val updatedAt: Date,
    // Metadata
    val fileSize: Long?,
    val audioFormat: String?,
    val quality: AudioQuality,
    val source: VoicemailSource
)

enum class TranscriptionStatus {
    PENDING,
    COMPLETED,
    FAILED,
    NOT_REQUESTED
}

enum class AudioQuality {
    LOW,
    MEDIUM,
    HIGH
}

enum class VoicemailSource {
    CARRIER,
    APP,
    IMPORT
}

@Entity(tableName = "users")
data class UserEntity(
    @PrimaryKey
    val id: String,
    val firebaseUid: String,
    val email: String,
    val phoneNumber: String,
    val displayName: String?,
    val profilePicture: String?,
    val isVerified: Boolean,
    // Subscription
    val subscriptionIsActive: Boolean,
    val subscriptionPlan: SubscriptionPlan,
    val subscriptionStartDate: Date?,
    val subscriptionEndDate: Date?,
    val stripeCustomerId: String?,
    val stripeSubscriptionId: String?,
    // Preferences
    val autoTranscription: Boolean,
    val spamBlocking: Boolean,
    val notificationNewVoicemail: Boolean,
    val notificationSpamDetection: Boolean,
    val notificationPromotions: Boolean,
    val audioQuality: AudioQuality,
    // Metadata
    val lastLoginAt: Date?,
    val createdAt: Date,
    val updatedAt: Date
)

enum class SubscriptionPlan {
    FREE,
    PREMIUM
}

@Entity(tableName = "blocked_numbers")
data class BlockedNumberEntity(
    @PrimaryKey
    val phoneNumber: String,
    val isSpam: Boolean,
    val userBlocked: Boolean, // true if manually blocked by user
    val confidence: Int?, // spam confidence score
    val source: String?, // where the spam info came from
    val createdAt: Date,
    val updatedAt: Date
}