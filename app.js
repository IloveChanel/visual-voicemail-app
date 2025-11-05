// Visual Voicemail App
class VoicemailApp {
    constructor() {
        this.voicemails = [];
        this.init();
    }

    init() {
        this.loadVoicemails();
        this.render();
    }

    loadVoicemails() {
        // Sample voicemail data
        this.voicemails = [
            {
                id: 1,
                caller: 'John Doe',
                number: '+1 (555) 123-4567',
                timestamp: new Date('2025-11-05T10:30:00'),
                duration: '1:23',
                listened: false
            },
            {
                id: 2,
                caller: 'Jane Smith',
                number: '+1 (555) 987-6543',
                timestamp: new Date('2025-11-05T14:15:00'),
                duration: '0:45',
                listened: false
            },
            {
                id: 3,
                caller: 'Support Team',
                number: '+1 (555) 555-5555',
                timestamp: new Date('2025-11-04T16:20:00'),
                duration: '2:10',
                listened: true
            }
        ];
    }

    formatTimestamp(date) {
        const now = new Date();
        const diff = now - date;
        const hours = Math.floor(diff / (1000 * 60 * 60));
        
        if (hours < 24) {
            return `${hours} hours ago`;
        } else {
            const days = Math.floor(hours / 24);
            return `${days} day${days > 1 ? 's' : ''} ago`;
        }
    }

    render() {
        const listElement = document.getElementById('voicemailList');
        const emptyState = document.getElementById('emptyState');

        if (this.voicemails.length === 0) {
            emptyState.style.display = 'block';
            listElement.style.display = 'none';
            return;
        }

        emptyState.style.display = 'none';
        listElement.style.display = 'block';
        listElement.innerHTML = '';

        this.voicemails.forEach(voicemail => {
            const item = document.createElement('div');
            item.className = 'voicemail-item';
            item.innerHTML = `
                <div class="voicemail-header">
                    <span class="caller-name">${voicemail.caller}</span>
                    <span class="timestamp">${this.formatTimestamp(voicemail.timestamp)}</span>
                </div>
                <div class="voicemail-details">
                    <span class="phone-number">${voicemail.number}</span>
                    <span class="duration">${voicemail.duration}</span>
                </div>
            `;
            
            item.addEventListener('click', () => {
                this.playVoicemail(voicemail.id);
            });

            listElement.appendChild(item);
        });
    }

    playVoicemail(id) {
        const voicemail = this.voicemails.find(v => v.id === id);
        console.log(`Playing voicemail from ${voicemail.caller} (${voicemail.number})`);
        // In a real application, this would integrate with audio playback
        // For now, we'll just log to the console
    }
}

// Initialize the app when the DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    new VoicemailApp();
});
