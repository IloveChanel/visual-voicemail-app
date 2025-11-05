package com.visualvoicemail.android

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.os.Build
import androidx.hilt.work.HiltWorkerFactory
import androidx.work.Configuration
import com.google.firebase.FirebaseApp
import dagger.hilt.android.HiltAndroidApp
import javax.inject.Inject

@HiltAndroidApp
class VoicemailApplication : Application(), Configuration.Provider {

    @Inject
    lateinit var workerFactory: HiltWorkerFactory

    override fun onCreate() {
        super.onCreate()
        
        // Initialize Firebase
        FirebaseApp.initializeApp(this)
        
        // Create notification channels
        createNotificationChannels()
    }

    override fun getWorkManagerConfiguration() =
        Configuration.Builder()
            .setWorkerFactory(workerFactory)
            .build()

    private fun createNotificationChannels() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val notificationManager = getSystemService(NotificationManager::class.java)

            // New Voicemail Channel
            val newVoicemailChannel = NotificationChannel(
                CHANNEL_NEW_VOICEMAIL,
                "New Voicemails",
                NotificationManager.IMPORTANCE_HIGH
            ).apply {
                description = "Notifications for new voicemails"
                enableVibration(true)
                enableLights(true)
            }

            // Spam Detection Channel
            val spamChannel = NotificationChannel(
                CHANNEL_SPAM_DETECTION,
                "Spam Detection",
                NotificationManager.IMPORTANCE_DEFAULT
            ).apply {
                description = "Notifications for spam call detection"
            }

            // General Channel
            val generalChannel = NotificationChannel(
                CHANNEL_GENERAL,
                "General",
                NotificationManager.IMPORTANCE_DEFAULT
            ).apply {
                description = "General app notifications"
            }

            notificationManager?.createNotificationChannels(listOf(
                newVoicemailChannel,
                spamChannel,
                generalChannel
            ))
        }
    }

    companion object {
        const val CHANNEL_NEW_VOICEMAIL = "new_voicemail"
        const val CHANNEL_SPAM_DETECTION = "spam_detection"
        const val CHANNEL_GENERAL = "general"
    }
}