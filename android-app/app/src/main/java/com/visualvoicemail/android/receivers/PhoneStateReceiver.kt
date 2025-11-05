package com.visualvoicemail.android.receivers

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.telephony.TelephonyManager
import android.util.Log
import com.visualvoicemail.android.services.SpamDetectionService
import com.visualvoicemail.android.services.CallScreeningService
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject

/**
 * 📞 REAL CALL INTERCEPTION FOR YOUR 248-321-9121 NUMBER
 * 
 * This intercepts ALL incoming calls to your Samsung phone and:
 * 1. Checks if caller is spam
 * 2. Verifies caller identity
 * 3. Gives you accept/decline/voicemail options
 * 4. Processes voicemail with transcription
 */
@AndroidEntryPoint
class PhoneStateReceiver : BroadcastReceiver() {

    @Inject
    lateinit var spamDetectionService: SpamDetectionService
    
    @Inject
    lateinit var callScreeningService: CallScreeningService

    companion object {
        private const val TAG = "PhoneStateReceiver"
        const val YOUR_PHONE_NUMBER = "248-321-9121" // Your actual number
    }

    override fun onReceive(context: Context, intent: Intent) {
        Log.d(TAG, "📞 Call intercepted on ${YOUR_PHONE_NUMBER}!")
        
        when (intent.action) {
            TelephonyManager.ACTION_PHONE_STATE_CHANGED -> {
                handlePhoneStateChange(context, intent)
            }
        }
    }

    private fun handlePhoneStateChange(context: Context, intent: Intent) {
        val state = intent.getStringExtra(TelephonyManager.EXTRA_STATE)
        val incomingNumber = intent.getStringExtra(TelephonyManager.EXTRA_INCOMING_NUMBER)

        Log.d(TAG, "📱 Phone state: $state, Number: $incomingNumber")

        when (state) {
            TelephonyManager.EXTRA_STATE_RINGING -> {
                // 🚨 INCOMING CALL TO YOUR 248-321-9121!
                incomingNumber?.let { number ->
                    handleIncomingCall(context, number)
                }
            }
            
            TelephonyManager.EXTRA_STATE_OFFHOOK -> {
                // ✅ Call answered
                Log.d(TAG, "📞 Call answered")
            }
            
            TelephonyManager.EXTRA_STATE_IDLE -> {
                // 📱 Call ended - check for voicemail
                Log.d(TAG, "📞 Call ended - checking for voicemail")
                checkForNewVoicemail(context)
            }
        }
    }

    /**
     * 🎯 THIS IS WHERE THE MAGIC HAPPENS!
     * When someone calls your 248-321-9121 number:
     */
    private fun handleIncomingCall(context: Context, phoneNumber: String) {
        Log.d(TAG, "🚨 Incoming call from: $phoneNumber to your ${YOUR_PHONE_NUMBER}")
        
        // 1️⃣ SPAM DETECTION
        spamDetectionService.checkSpam(phoneNumber) { isSpam, confidence ->
            
            // 2️⃣ CALLER ID LOOKUP  
            callScreeningService.identifyCaller(phoneNumber) { callerInfo ->
                
                // 3️⃣ SHOW CALL SCREEN WITH OPTIONS
                showCallScreen(context, phoneNumber, callerInfo, isSpam, confidence)
            }
        }
    }

    /**
     * 📱 Shows your custom call screen with options:
     * - ✅ Accept Call
     * - ❌ Decline & Block  
     * - 📧 Send to Voicemail
     * - 🛡️ Mark as Spam
     */
    private fun showCallScreen(
        context: Context, 
        phoneNumber: String, 
        callerInfo: CallerInfo,
        isSpam: Boolean,
        confidence: Float
    ) {
        val callScreenIntent = Intent(context, CallScreenActivity::class.java).apply {
            putExtra("phone_number", phoneNumber)
            putExtra("caller_name", callerInfo.name)
            putExtra("caller_business", callerInfo.business)
            putExtra("is_spam", isSpam)
            putExtra("spam_confidence", confidence)
            putExtra("your_number", YOUR_PHONE_NUMBER)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        }
        
        context.startActivity(callScreenIntent)
        
        Log.d(TAG, "🎯 Call screen shown for $phoneNumber")
    }

    /**
     * 📧 Checks for new voicemails after call ends
     */
    private fun checkForNewVoicemail(context: Context) {
        // Start voicemail processing service
        val voicemailIntent = Intent(context, VoicemailProcessingService::class.java)
        context.startForegroundService(voicemailIntent)
    }
}

/**
 * 👤 Caller information from lookup
 */
data class CallerInfo(
    val name: String?,
    val business: String?,
    val photoUrl: String?,
    val isVerified: Boolean = false
)