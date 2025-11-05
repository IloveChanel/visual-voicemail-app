import SwiftUI

struct ContentView: View {
    @StateObject private var voicemailViewModel = VoicemailViewModel()
    @StateObject private var subscriptionViewModel = SubscriptionViewModel()
    
    var body: some View {
        TabView {
            VoicemailListView()
                .environmentObject(voicemailViewModel)
                .tabItem {
                    Image(systemName: "voicemail")
                    Text("Voicemails")
                }
            
            SpamBlockingView()
                .tabItem {
                    Image(systemName: "phone.badge.plus")
                    Text("Spam Blocking")
                }
            
            SettingsView()
                .environmentObject(subscriptionViewModel)
                .tabItem {
                    Image(systemName: "gear")
                    Text("Settings")
                }
        }
        .accentColor(.blue)
        .onAppear {
            voicemailViewModel.loadVoicemails()
            subscriptionViewModel.checkSubscriptionStatus()
        }
    }
}

#Preview {
    ContentView()
}