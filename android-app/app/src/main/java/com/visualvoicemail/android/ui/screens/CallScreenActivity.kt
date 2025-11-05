package com.visualvoicemail.android.ui.screens

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * 📞 CUSTOM CALL SCREEN FOR YOUR 248-321-9121 NUMBER
 * 
 * This appears when someone calls you, showing:
 * - Caller ID verification
 * - Spam detection results  
 * - Accept/Decline/Voicemail options
 * - Real-time caller information
 */
@Composable
fun CallScreenActivity(
    phoneNumber: String,
    callerName: String?,
    callerBusiness: String?,
    isSpam: Boolean,
    spamConfidence: Float,
    onAcceptCall: () -> Unit,
    onDeclineCall: () -> Unit,
    onSendToVoicemail: () -> Unit,
    onMarkAsSpam: () -> Unit
) {
    val context = LocalContext.current
    
    // 🎨 Dynamic background based on spam status
    val backgroundGradient = if (isSpam) {
        Brush.verticalGradient(
            colors = listOf(
                Color(0xFFFF6B6B), // Red for spam
                Color(0xFFFF8E8E)
            )
        )
    } else {
        Brush.verticalGradient(
            colors = listOf(
                Color(0xFF4CAF50), // Green for safe
                Color(0xFF81C784)
            )
        )
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(backgroundGradient)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            
            Spacer(modifier = Modifier.height(60.dp))
            
            // 🔔 Incoming Call Header
            Text(
                text = "📞 Incoming Call",
                fontSize = 18.sp,
                color = Color.White,
                fontWeight = FontWeight.Medium
            )
            
            Text(
                text = "to 248-321-9121", // Your number
                fontSize = 14.sp,
                color = Color.White.copy(alpha = 0.8f)
            )
            
            Spacer(modifier = Modifier.height(32.dp))
            
            // 👤 Caller Avatar/Initial
            Box(
                modifier = Modifier
                    .size(120.dp)
                    .clip(CircleShape)
                    .background(Color.White.copy(alpha = 0.2f)),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = callerName?.firstOrNull()?.toString()?.uppercase() ?: "?",
                    fontSize = 48.sp,
                    color = Color.White,
                    fontWeight = FontWeight.Bold
                )
            }
            
            Spacer(modifier = Modifier.height(24.dp))
            
            // 📋 Caller Information
            CallInfoCard(
                phoneNumber = phoneNumber,
                callerName = callerName,
                callerBusiness = callerBusiness,
                isSpam = isSpam,
                spamConfidence = spamConfidence
            )
            
            Spacer(modifier = Modifier.weight(1f))
            
            // 🎯 Action Buttons
            CallActionButtons(
                isSpam = isSpam,
                onAcceptCall = onAcceptCall,
                onDeclineCall = onDeclineCall,
                onSendToVoicemail = onSendToVoicemail,
                onMarkAsSpam = onMarkAsSpam
            )
            
            Spacer(modifier = Modifier.height(40.dp))
        }
    }
}

@Composable
private fun CallInfoCard(
    phoneNumber: String,
    callerName: String?,
    callerBusiness: String?,
    isSpam: Boolean,
    spamConfidence: Float
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(
            containerColor = Color.White.copy(alpha = 0.9f)
        )
    ) {
        Column(
            modifier = Modifier.padding(20.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            
            // 📱 Phone Number
            Text(
                text = phoneNumber,
                fontSize = 24.sp,
                fontWeight = FontWeight.Bold,
                color = Color(0xFF333333)
            )
            
            // 👤 Caller Name
            if (callerName != null) {
                Text(
                    text = callerName,
                    fontSize = 18.sp,
                    color = Color(0xFF666666),
                    modifier = Modifier.padding(top = 4.dp)
                )
            }
            
            // 🏢 Business Info
            if (callerBusiness != null) {
                Text(
                    text = callerBusiness,
                    fontSize = 14.sp,
                    color = Color(0xFF888888),
                    modifier = Modifier.padding(top = 2.dp)
                )
            }
            
            Spacer(modifier = Modifier.height(16.dp))
            
            // 🛡️ Spam Detection Results
            SpamIndicator(isSpam = isSpam, confidence = spamConfidence)
        }
    }
}

@Composable
private fun SpamIndicator(isSpam: Boolean, confidence: Float) {
    val (color, icon, text) = when {
        isSpam && confidence > 0.8f -> Triple(
            Color(0xFFFF4444), 
            Icons.Default.Block, 
            "🚨 High Risk Spam (${(confidence * 100).toInt()}%)"
        )
        isSpam -> Triple(
            Color(0xFFFF8800), 
            Icons.Default.Warning, 
            "⚠️ Possible Spam (${(confidence * 100).toInt()}%)"
        )
        else -> Triple(
            Color(0xFF4CAF50), 
            Icons.Default.Verified, 
            "✅ Likely Safe Caller"
        )
    }
    
    Row(
        verticalAlignment = Alignment.CenterVertically,
        modifier = Modifier
            .background(
                color = color.copy(alpha = 0.1f),
                shape = RoundedCornerShape(20.dp)
            )
            .padding(horizontal = 12.dp, vertical = 6.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = color,
            modifier = Modifier.size(16.dp)
        )
        
        Spacer(modifier = Modifier.width(8.dp))
        
        Text(
            text = text,
            color = color,
            fontSize = 12.sp,
            fontWeight = FontWeight.Medium
        )
    }
}

@Composable
private fun CallActionButtons(
    isSpam: Boolean,
    onAcceptCall: () -> Unit,
    onDeclineCall: () -> Unit,
    onSendToVoicemail: () -> Unit,
    onMarkAsSpam: () -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceEvenly
    ) {
        
        // ❌ Decline Call
        FloatingActionButton(
            onClick = onDeclineCall,
            modifier = Modifier.size(64.dp),
            containerColor = Color(0xFFFF4444)
        ) {
            Icon(
                imageVector = Icons.Default.CallEnd,
                contentDescription = "Decline",
                tint = Color.White,
                modifier = Modifier.size(28.dp)
            )
        }
        
        // 📧 Send to Voicemail
        FloatingActionButton(
            onClick = onSendToVoicemail,
            modifier = Modifier.size(64.dp),
            containerColor = Color(0xFF2196F3)
        ) {
            Icon(
                imageVector = Icons.Default.Voicemail,
                contentDescription = "Voicemail",
                tint = Color.White,
                modifier = Modifier.size(28.dp)
            )
        }
        
        // ✅ Accept Call (only if not high-risk spam)
        if (!isSpam || true) { // Always show for user choice
            FloatingActionButton(
                onClick = onAcceptCall,
                modifier = Modifier.size(64.dp),
                containerColor = Color(0xFF4CAF50)
            ) {
                Icon(
                    imageVector = Icons.Default.Call,
                    contentDescription = "Accept",
                    tint = Color.White,
                    modifier = Modifier.size(28.dp)
                )
            }
        }
    }
    
    // 🛡️ Additional spam options
    if (isSpam) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 16.dp),
            horizontalArrangement = Arrangement.Center
        ) {
            TextButton(
                onClick = onMarkAsSpam,
                colors = ButtonDefaults.textButtonColors(
                    contentColor = Color.White
                )
            ) {
                Icon(
                    imageVector = Icons.Default.Report,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp)
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text("Block & Report Spam")
            }
        }
    }
}