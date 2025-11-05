package com.visualvoicemail.android.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.visualvoicemail.android.R
import com.visualvoicemail.android.data.VoicemailEntity
import com.visualvoicemail.android.ui.components.VoicemailItem
import com.visualvoicemail.android.ui.components.EmptyState
import com.visualvoicemail.android.viewmodels.VoicemailListViewModel
import java.text.SimpleDateFormat
import java.util.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun VoicemailListScreen(
    onVoicemailClick: (String) -> Unit,
    viewModel: VoicemailListViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    var showFilterMenu by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier.fillMaxSize()
    ) {
        // Top App Bar
        TopAppBar(
            title = { 
                Text(
                    text = stringResource(R.string.voicemails),
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            },
            actions = {
                // Filter menu
                IconButton(onClick = { showFilterMenu = true }) {
                    Icon(
                        imageVector = Icons.Default.FilterList,
                        contentDescription = "Filter voicemails"
                    )
                }
                
                DropdownMenu(
                    expanded = showFilterMenu,
                    onDismissRequest = { showFilterMenu = false }
                ) {
                    DropdownMenuItem(
                        text = { Text("All") },
                        onClick = {
                            viewModel.setFilter(VoicemailListViewModel.Filter.ALL)
                            showFilterMenu = false
                        }
                    )
                    DropdownMenuItem(
                        text = { Text("Unread") },
                        onClick = {
                            viewModel.setFilter(VoicemailListViewModel.Filter.UNREAD)
                            showFilterMenu = false
                        }
                    )
                    DropdownMenuItem(
                        text = { Text("Spam") },
                        onClick = {
                            viewModel.setFilter(VoicemailListViewModel.Filter.SPAM)
                            showFilterMenu = false
                        }
                    )
                    DropdownMenuItem(
                        text = { Text("Favorites") },
                        onClick = {
                            viewModel.setFilter(VoicemailListViewModel.Filter.FAVORITES)
                            showFilterMenu = false
                        }
                    )
                }

                // Search
                IconButton(onClick = { viewModel.toggleSearch() }) {
                    Icon(
                        imageVector = Icons.Default.Search,
                        contentDescription = "Search voicemails"
                    )
                }
            }
        )

        // Search bar
        if (uiState.showSearch) {
            OutlinedTextField(
                value = uiState.searchQuery,
                onValueChange = viewModel::setSearchQuery,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                placeholder = { Text("Search voicemails...") },
                leadingIcon = {
                    Icon(Icons.Default.Search, contentDescription = null)
                },
                trailingIcon = {
                    if (uiState.searchQuery.isNotEmpty()) {
                        IconButton(onClick = { viewModel.setSearchQuery("") }) {
                            Icon(Icons.Default.Clear, contentDescription = "Clear search")
                        }
                    }
                }
            )
        }

        // Content
        when {
            uiState.isLoading && uiState.voicemails.isEmpty() -> {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator()
                }
            }
            
            uiState.error != null -> {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally
                    ) {
                        Icon(
                            imageVector = Icons.Default.Error,
                            contentDescription = null,
                            modifier = Modifier.size(48.dp),
                            tint = MaterialTheme.colorScheme.error
                        )
                        Spacer(modifier = Modifier.height(16.dp))
                        Text(
                            text = uiState.error,
                            color = MaterialTheme.colorScheme.error
                        )
                        Spacer(modifier = Modifier.height(16.dp))
                        Button(onClick = viewModel::retry) {
                            Text("Retry")
                        }
                    }
                }
            }
            
            uiState.voicemails.isEmpty() -> {
                EmptyState(
                    icon = Icons.Default.Voicemail,
                    title = when (uiState.currentFilter) {
                        VoicemailListViewModel.Filter.UNREAD -> "No unread voicemails"
                        VoicemailListViewModel.Filter.SPAM -> "No spam voicemails"
                        VoicemailListViewModel.Filter.FAVORITES -> "No favorite voicemails"
                        else -> "No voicemails yet"
                    },
                    subtitle = "Your voicemails will appear here"
                )
            }
            
            else -> {
                LazyColumn(
                    modifier = Modifier.fillMaxSize(),
                    contentPadding = PaddingValues(vertical = 8.dp)
                ) {
                    items(
                        items = uiState.voicemails,
                        key = { it.id }
                    ) { voicemail ->
                        VoicemailItem(
                            voicemail = voicemail,
                            onClick = { onVoicemailClick(voicemail.id) },
                            onFavoriteClick = { 
                                viewModel.toggleFavorite(voicemail.id)
                            },
                            onArchiveClick = {
                                viewModel.archiveVoicemail(voicemail.id)
                            },
                            onDeleteClick = {
                                viewModel.deleteVoicemail(voicemail.id)
                            },
                            modifier = Modifier.fillMaxWidth()
                        )
                    }

                    // Show loading more indicator
                    if (uiState.isLoading && uiState.voicemails.isNotEmpty()) {
                        item {
                            Box(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(16.dp),
                                contentAlignment = Alignment.Center
                            ) {
                                CircularProgressIndicator(
                                    modifier = Modifier.size(24.dp)
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}