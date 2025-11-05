import Foundation
import Combine

@MainActor
class VoicemailViewModel: ObservableObject {
    @Published var voicemails: [Voicemail] = []
    @Published var stats = VoicemailStats(total: 0, unread: 0, spam: 0, favorites: 0)
    @Published var isLoading = false
    @Published var error: String?
    @Published var canLoadMore = false
    
    private var currentPage = 1
    private var currentFilter: VoicemailFilter = .all
    private var searchQuery = ""
    private let pageSize = 20
    
    private let apiService = APIService.shared
    private var cancellables = Set<AnyCancellable>()
    
    init() {
        setupBindings()
    }
    
    private func setupBindings() {
        // Listen for real-time updates
        NotificationCenter.default.publisher(for: .newVoicemailReceived)
            .sink { [weak self] _ in
                Task { @MainActor in
                    await self?.refreshVoicemails()
                }
            }
            .store(in: &cancellables)
    }
    
    func loadVoicemails() {
        Task {
            await loadVoicemailsAsync(reset: true)
        }
    }
    
    func refreshVoicemails() async {
        await loadVoicemailsAsync(reset: true)
    }
    
    func loadMoreVoicemails() {
        guard canLoadMore, !isLoading else { return }
        
        Task {
            await loadVoicemailsAsync(reset: false)
        }
    }
    
    func setFilter(_ filter: VoicemailFilter) {
        currentFilter = filter
        loadVoicemails()
    }
    
    func searchVoicemails(query: String) {
        searchQuery = query
        // Debounce search to avoid too many API calls
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
            if self.searchQuery == query {
                self.loadVoicemails()
            }
        }
    }
    
    private func loadVoicemailsAsync(reset: Bool) async {
        if reset {
            currentPage = 1
        }
        
        isLoading = true
        error = nil
        
        do {
            let response = try await apiService.fetchVoicemails(
                page: currentPage,
                limit: pageSize,
                filter: currentFilter.rawValue,
                search: searchQuery.isEmpty ? nil : searchQuery
            )
            
            if reset {
                voicemails = response.voicemails
                stats = response.stats
            } else {
                voicemails.append(contentsOf: response.voicemails)
            }
            
            canLoadMore = response.pagination.hasNext
            if canLoadMore {
                currentPage += 1
            }
            
        } catch {
            self.error = error.localizedDescription
        }
        
        isLoading = false
    }
    
    func toggleFavorite(id: String) {
        Task {
            do {
                try await apiService.updateVoicemail(id: id, updates: ["isFavorite": !isFavoriteStatus(id: id)])
                
                // Update local state
                if let index = voicemails.firstIndex(where: { $0.id == id }) {
                    voicemails[index] = Voicemail(
                        id: voicemails[index].id,
                        userId: voicemails[index].userId,
                        callerNumber: voicemails[index].callerNumber,
                        callerName: voicemails[index].callerName,
                        duration: voicemails[index].duration,
                        audioUrl: voicemails[index].audioUrl,
                        transcription: voicemails[index].transcription,
                        transcriptionStatus: voicemails[index].transcriptionStatus,
                        isSpam: voicemails[index].isSpam,
                        spamConfidence: voicemails[index].spamConfidence,
                        isRead: voicemails[index].isRead,
                        isArchived: voicemails[index].isArchived,
                        isFavorite: !voicemails[index].isFavorite,
                        timestamp: voicemails[index].timestamp,
                        metadata: voicemails[index].metadata,
                        tags: voicemails[index].tags,
                        createdAt: voicemails[index].createdAt,
                        updatedAt: Date()
                    )
                }
                
                // Update stats
                updateStatsAfterFavoriteToggle()
                
            } catch {
                self.error = "Failed to update favorite status"
            }
        }
    }
    
    func toggleReadStatus(id: String) {
        Task {
            do {
                let currentReadStatus = isReadStatus(id: id)
                try await apiService.updateVoicemail(id: id, updates: ["isRead": !currentReadStatus])
                
                // Update local state
                if let index = voicemails.firstIndex(where: { $0.id == id }) {
                    voicemails[index] = Voicemail(
                        id: voicemails[index].id,
                        userId: voicemails[index].userId,
                        callerNumber: voicemails[index].callerNumber,
                        callerName: voicemails[index].callerName,
                        duration: voicemails[index].duration,
                        audioUrl: voicemails[index].audioUrl,
                        transcription: voicemails[index].transcription,
                        transcriptionStatus: voicemails[index].transcriptionStatus,
                        isSpam: voicemails[index].isSpam,
                        spamConfidence: voicemails[index].spamConfidence,
                        isRead: !voicemails[index].isRead,
                        isArchived: voicemails[index].isArchived,
                        isFavorite: voicemails[index].isFavorite,
                        timestamp: voicemails[index].timestamp,
                        metadata: voicemails[index].metadata,
                        tags: voicemails[index].tags,
                        createdAt: voicemails[index].createdAt,
                        updatedAt: Date()
                    )
                }
                
                // Update stats
                updateStatsAfterReadToggle()
                
            } catch {
                self.error = "Failed to update read status"
            }
        }
    }
    
    func archiveVoicemail(id: String) {
        Task {
            do {
                try await apiService.updateVoicemail(id: id, updates: ["isArchived": true])
                
                // Remove from current list if not showing archived
                if currentFilter != .archived {
                    voicemails.removeAll { $0.id == id }
                }
                
                // Update stats
                updateStatsAfterArchive()
                
            } catch {
                self.error = "Failed to archive voicemail"
            }
        }
    }
    
    func deleteVoicemail(id: String) {
        Task {
            do {
                try await apiService.deleteVoicemail(id: id)
                
                // Remove from local list
                voicemails.removeAll { $0.id == id }
                
                // Update stats
                updateStatsAfterDelete()
                
            } catch {
                self.error = "Failed to delete voicemail"
            }
        }
    }
    
    func requestTranscription(id: String) {
        Task {
            do {
                try await apiService.requestTranscription(voicemailId: id)
                
                // Update transcription status to pending
                if let index = voicemails.firstIndex(where: { $0.id == id }) {
                    voicemails[index] = Voicemail(
                        id: voicemails[index].id,
                        userId: voicemails[index].userId,
                        callerNumber: voicemails[index].callerNumber,
                        callerName: voicemails[index].callerName,
                        duration: voicemails[index].duration,
                        audioUrl: voicemails[index].audioUrl,
                        transcription: voicemails[index].transcription,
                        transcriptionStatus: .pending,
                        isSpam: voicemails[index].isSpam,
                        spamConfidence: voicemails[index].spamConfidence,
                        isRead: voicemails[index].isRead,
                        isArchived: voicemails[index].isArchived,
                        isFavorite: voicemails[index].isFavorite,
                        timestamp: voicemails[index].timestamp,
                        metadata: voicemails[index].metadata,
                        tags: voicemails[index].tags,
                        createdAt: voicemails[index].createdAt,
                        updatedAt: Date()
                    )
                }
                
            } catch {
                self.error = "Failed to request transcription"
            }
        }
    }
    
    // MARK: - Helper Methods
    
    private func isFavoriteStatus(id: String) -> Bool {
        return voicemails.first { $0.id == id }?.isFavorite ?? false
    }
    
    private func isReadStatus(id: String) -> Bool {
        return voicemails.first { $0.id == id }?.isRead ?? true
    }
    
    private func updateStatsAfterFavoriteToggle() {
        let favoriteCount = voicemails.filter { $0.isFavorite }.count
        stats = VoicemailStats(
            total: stats.total,
            unread: stats.unread,
            spam: stats.spam,
            favorites: favoriteCount
        )
    }
    
    private func updateStatsAfterReadToggle() {
        let unreadCount = voicemails.filter { !$0.isRead }.count
        stats = VoicemailStats(
            total: stats.total,
            unread: unreadCount,
            spam: stats.spam,
            favorites: stats.favorites
        )
    }
    
    private func updateStatsAfterArchive() {
        stats = VoicemailStats(
            total: max(0, stats.total - 1),
            unread: stats.unread,
            spam: stats.spam,
            favorites: stats.favorites
        )
    }
    
    private func updateStatsAfterDelete() {
        stats = VoicemailStats(
            total: max(0, stats.total - 1),
            unread: stats.unread,
            spam: stats.spam,
            favorites: stats.favorites
        )
    }
}

// MARK: - Subscription ViewModel

@MainActor
class SubscriptionViewModel: ObservableObject {
    @Published var subscriptionStatus: SubscriptionStatus?
    @Published var isLoading = false
    @Published var error: String?
    @Published var showingPaywall = false
    
    private let apiService = APIService.shared
    
    func checkSubscriptionStatus() {
        Task {
            isLoading = true
            error = nil
            
            do {
                subscriptionStatus = try await apiService.getSubscriptionStatus()
            } catch {
                self.error = error.localizedDescription
            }
            
            isLoading = false
        }
    }
    
    func purchaseSubscription(priceId: String, paymentMethodId: String) {
        Task {
            isLoading = true
            error = nil
            
            do {
                try await apiService.createSubscription(priceId: priceId, paymentMethodId: paymentMethodId)
                await checkSubscriptionStatus()
                showingPaywall = false
            } catch {
                self.error = error.localizedDescription
            }
            
            isLoading = false
        }
    }
    
    func cancelSubscription() {
        Task {
            isLoading = true
            error = nil
            
            do {
                try await apiService.cancelSubscription()
                await checkSubscriptionStatus()
            } catch {
                self.error = error.localizedDescription
            }
            
            isLoading = false
        }
    }
    
    var isPremiumUser: Bool {
        return subscriptionStatus?.subscription.isActive == true
    }
    
    var canUseFeature: Bool {
        return isPremiumUser
    }
}

// MARK: - Notifications

extension Notification.Name {
    static let newVoicemailReceived = Notification.Name("newVoicemailReceived")
    static let subscriptionStatusChanged = Notification.Name("subscriptionStatusChanged")
}