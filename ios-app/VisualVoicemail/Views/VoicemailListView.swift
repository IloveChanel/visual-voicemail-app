import SwiftUI

struct VoicemailListView: View {
    @StateObject private var viewModel = VoicemailViewModel()
    @State private var selectedFilter: VoicemailFilter = .all
    @State private var searchText = ""
    @State private var showingFilterSheet = false
    
    var body: some View {
        NavigationView {
            ZStack {
                if viewModel.voicemails.isEmpty && !viewModel.isLoading {
                    EmptyStateView(
                        icon: "voicemail",
                        title: emptyStateTitle,
                        subtitle: "Your voicemails will appear here when you receive them."
                    )
                } else {
                    VoicemailList()
                }
                
                if viewModel.isLoading && viewModel.voicemails.isEmpty {
                    LoadingView()
                }
            }
            .navigationTitle("Voicemails")
            .searchable(text: $searchText, prompt: "Search voicemails")
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button(action: { showingFilterSheet = true }) {
                        Image(systemName: "line.3.horizontal.decrease.circle")
                    }
                }
            }
            .sheet(isPresented: $showingFilterSheet) {
                FilterSheet(selectedFilter: $selectedFilter)
            }
            .refreshable {
                await viewModel.refreshVoicemails()
            }
            .onAppear {
                viewModel.loadVoicemails()
            }
            .onChange(of: selectedFilter) { newFilter in
                viewModel.setFilter(newFilter)
            }
            .onChange(of: searchText) { searchQuery in
                viewModel.searchVoicemails(query: searchQuery)
            }
        }
    }
    
    private var emptyStateTitle: String {
        switch selectedFilter {
        case .all: return "No voicemails yet"
        case .unread: return "No unread voicemails"
        case .spam: return "No spam voicemails"
        case .favorites: return "No favorite voicemails"
        case .archived: return "No archived voicemails"
        }
    }
    
    @ViewBuilder
    private func VoicemailList() -> some View {
        List {
            // Stats Section
            if selectedFilter == .all && searchText.isEmpty {
                StatsSection(stats: viewModel.stats)
            }
            
            // Voicemails Section
            Section {
                ForEach(filteredVoicemails) { voicemail in
                    NavigationLink(destination: VoicemailDetailView(voicemail: voicemail)) {
                        VoicemailRowView(voicemail: voicemail)
                    }
                    .swipeActions(edge: .trailing, allowsFullSwipe: false) {
                        // Delete action
                        Button(role: .destructive) {
                            viewModel.deleteVoicemail(id: voicemail.id)
                        } label: {
                            Label("Delete", systemImage: "trash")
                        }
                        
                        // Archive action
                        Button {
                            viewModel.archiveVoicemail(id: voicemail.id)
                        } label: {
                            Label("Archive", systemImage: "archivebox")
                        }
                        .tint(.orange)
                    }
                    .swipeActions(edge: .leading, allowsFullSwipe: false) {
                        // Favorite action
                        Button {
                            viewModel.toggleFavorite(id: voicemail.id)
                        } label: {
                            Label(
                                voicemail.isFavorite ? "Unfavorite" : "Favorite",
                                systemImage: voicemail.isFavorite ? "heart.slash" : "heart"
                            )
                        }
                        .tint(.pink)
                        
                        // Mark as read/unread
                        Button {
                            viewModel.toggleReadStatus(id: voicemail.id)
                        } label: {
                            Label(
                                voicemail.isRead ? "Mark Unread" : "Mark Read",
                                systemImage: voicemail.isRead ? "envelope.badge" : "envelope.open"
                            )
                        }
                        .tint(.blue)
                    }
                }
                
                // Load more indicator
                if viewModel.canLoadMore {
                    HStack {
                        Spacer()
                        ProgressView()
                            .onAppear {
                                viewModel.loadMoreVoicemails()
                            }
                        Spacer()
                    }
                    .padding(.vertical, 8)
                }
            }
        }
        .listStyle(InsetGroupedListStyle())
    }
    
    private var filteredVoicemails: [Voicemail] {
        viewModel.voicemails.filter { voicemail in
            // Apply search filter
            if !searchText.isEmpty {
                let searchLower = searchText.lowercased()
                return voicemail.callerDisplayName.lowercased().contains(searchLower) ||
                       voicemail.transcription?.lowercased().contains(searchLower) == true ||
                       voicemail.callerNumber.contains(searchText)
            }
            
            // Apply type filter
            switch selectedFilter {
            case .all:
                return !voicemail.isArchived
            case .unread:
                return !voicemail.isRead && !voicemail.isArchived
            case .spam:
                return voicemail.isSpam
            case .favorites:
                return voicemail.isFavorite && !voicemail.isArchived
            case .archived:
                return voicemail.isArchived
            }
        }
    }
}

// MARK: - Supporting Views

struct StatsSection: View {
    let stats: VoicemailStats
    
    var body: some View {
        Section("Overview") {
            HStack {
                StatItem(title: "Total", value: stats.total, color: .blue)
                Divider()
                StatItem(title: "Unread", value: stats.unread, color: .orange)
                Divider()
                StatItem(title: "Spam", value: stats.spam, color: .red)
                Divider()
                StatItem(title: "Favorites", value: stats.favorites, color: .pink)
            }
            .padding(.vertical, 8)
        }
    }
}

struct StatItem: View {
    let title: String
    let value: Int
    let color: Color
    
    var body: some View {
        VStack(spacing: 4) {
            Text("\(value)")
                .font(.headline)
                .fontWeight(.semibold)
                .foregroundColor(color)
            
            Text(title)
                .font(.caption)
                .foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity)
    }
}

struct VoicemailRowView: View {
    let voicemail: Voicemail
    
    var body: some View {
        HStack {
            // Caller avatar or icon
            Circle()
                .fill(voicemail.isSpam ? Color.red.opacity(0.2) : Color.blue.opacity(0.2))
                .frame(width: 44, height: 44)
                .overlay {
                    Image(systemName: voicemail.isSpam ? "exclamationmark.triangle.fill" : "person.fill")
                        .foregroundColor(voicemail.isSpam ? .red : .blue)
                        .font(.system(size: 20))
                }
            
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(voicemail.callerDisplayName)
                        .font(.headline)
                        .fontWeight(voicemail.isRead ? .regular : .semibold)
                    
                    Spacer()
                    
                    Text(voicemail.formattedTimestamp)
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                
                if let transcription = voicemail.transcription, !transcription.isEmpty {
                    Text(transcription)
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                        .lineLimit(2)
                } else {
                    Text("Voicemail • \(voicemail.formattedDuration)")
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                }
                
                // Status indicators
                HStack(spacing: 8) {
                    if voicemail.isSpam {
                        Label("Spam", systemImage: "exclamationmark.shield.fill")
                            .font(.caption)
                            .foregroundColor(.red)
                    }
                    
                    if voicemail.isFavorite {
                        Image(systemName: "heart.fill")
                            .font(.caption)
                            .foregroundColor(.pink)
                    }
                    
                    if !voicemail.isRead {
                        Circle()
                            .fill(Color.blue)
                            .frame(width: 8, height: 8)
                    }
                    
                    Spacer()
                }
            }
        }
        .padding(.vertical, 4)
    }
}

struct FilterSheet: View {
    @Binding var selectedFilter: VoicemailFilter
    @Environment(\.dismiss) private var dismiss
    
    var body: some View {
        NavigationView {
            List(VoicemailFilter.allCases, id: \.self) { filter in
                HStack {
                    Label(filter.displayName, systemImage: filter.systemImage)
                        .foregroundColor(selectedFilter == filter ? .accentColor : .primary)
                    
                    Spacer()
                    
                    if selectedFilter == filter {
                        Image(systemName: "checkmark")
                            .foregroundColor(.accentColor)
                            .fontWeight(.semibold)
                    }
                }
                .contentShape(Rectangle())
                .onTapGesture {
                    selectedFilter = filter
                    dismiss()
                }
            }
            .navigationTitle("Filter Voicemails")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Done") {
                        dismiss()
                    }
                }
            }
        }
        .presentationDetents([.medium])
    }
}

struct EmptyStateView: View {
    let icon: String
    let title: String
    let subtitle: String
    
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: icon)
                .font(.system(size: 60))
                .foregroundColor(.secondary)
            
            Text(title)
                .font(.title2)
                .fontWeight(.semibold)
            
            Text(subtitle)
                .font(.body)
                .foregroundColor(.secondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal, 32)
        }
    }
}

struct LoadingView: View {
    var body: some View {
        VStack(spacing: 16) {
            ProgressView()
                .scaleEffect(1.2)
            
            Text("Loading voicemails...")
                .foregroundColor(.secondary)
        }
    }
}

#Preview {
    VoicemailListView()
}