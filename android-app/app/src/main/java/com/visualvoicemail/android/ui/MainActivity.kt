package com.visualvoicemail.android.ui

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.lifecycle.lifecycleScope
import com.visualvoicemail.android.ui.navigation.VoicemailNavigation
import com.visualvoicemail.android.ui.theme.VisualVoicemailTheme
import com.visualvoicemail.android.utils.PermissionManager
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.launch
import javax.inject.Inject

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject
    lateinit var permissionManager: PermissionManager

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        permissions.entries.forEach { (permission, granted) ->
            if (granted) {
                // Handle granted permission
                handlePermissionGranted(permission)
            } else {
                // Handle denied permission
                handlePermissionDenied(permission)
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        // Install splash screen
        installSplashScreen()

        super.onCreate(savedInstanceState)

        setContent {
            VisualVoicemailTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    VoicemailApp()
                }
            }
        }

        // Request necessary permissions
        lifecycleScope.launch {
            requestRequiredPermissions()
        }
    }

    @Composable
    private fun VoicemailApp() {
        VoicemailNavigation()
    }

    private suspend fun requestRequiredPermissions() {
        val requiredPermissions = permissionManager.getRequiredPermissions()
        val missingPermissions = requiredPermissions.filter { permission ->
            !permissionManager.isPermissionGranted(permission)
        }

        if (missingPermissions.isNotEmpty()) {
            permissionLauncher.launch(missingPermissions.toTypedArray())
        }
    }

    private fun handlePermissionGranted(permission: String) {
        when (permission) {
            android.Manifest.permission.READ_PHONE_STATE -> {
                // Initialize phone state monitoring
            }
            android.Manifest.permission.READ_CALL_LOG -> {
                // Initialize call log access
            }
            android.Manifest.permission.READ_CONTACTS -> {
                // Initialize contact lookup
            }
            android.Manifest.permission.RECORD_AUDIO -> {
                // Initialize audio recording capabilities
            }
            android.Manifest.permission.POST_NOTIFICATIONS -> {
                // Initialize notification services
            }
        }
    }

    private fun handlePermissionDenied(permission: String) {
        // Show explanation or guide user to settings
        // You can implement a dialog or snackbar here
    }
}