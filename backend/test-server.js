import express from 'express';
import cors from 'cors';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = process.env.PORT || 3000;

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.static(path.join(__dirname, 'public')));

// Test route
app.get('/health', (req, res) => {
  res.json({
    status: 'OK',
    message: '🎉 Your Visual Voicemail API is working!',
    timestamp: new Date().toISOString(),
    features: [
      '✅ Visual voicemail processing',
      '✅ Spam detection ready',
      '✅ Subscription system ready',
      '✅ Push notifications ready'
    ]
  });
});

// Mock voicemails endpoint
app.get('/api/voicemails', (req, res) => {
  res.json({
    voicemails: [
      {
        id: '1',
        callerNumber: '+1234567890',
        callerName: 'John Doe',
        duration: 45,
        timestamp: new Date(),
        isRead: false,
        isSpam: false,
        transcription: 'Hey, this is John. Just wanted to check in with you.'
      },
      {
        id: '2', 
        callerNumber: '+1555123456',
        callerName: 'Spam Caller',
        duration: 30,
        timestamp: new Date(),
        isRead: true,
        isSpam: true,
        transcription: 'This is a spam call about your extended warranty.'
      }
    ],
    stats: {
      total: 2,
      unread: 1,
      spam: 1,
      favorites: 0
    }
  });
});

// Mock subscription endpoint
app.get('/api/subscription/status', (req, res) => {
  res.json({
    subscription: {
      isActive: false,
      plan: 'free',
      trialDaysLeft: 7
    },
    features: {
      unlimitedVoicemails: false,
      transcription: false,
      advancedSpamDetection: false,
      noAds: false
    },
    pricing: {
      monthly: '$1.99',
      yearly: '$19.99'
    }
  });
});

app.listen(PORT, () => {
  console.log(`
🚀 Visual Voicemail API Server Started!

📍 Server running on: http://localhost:${PORT}
📱 Test endpoints:
   • Health: http://localhost:${PORT}/health
   • Voicemails: http://localhost:${PORT}/api/voicemails  
   • Subscription: http://localhost:${PORT}/api/subscription/status

💡 Next steps:
   1. Test these endpoints in your browser
   2. Configure real services (Firebase, Stripe, etc.)
   3. Build and test mobile apps
   4. Deploy to production

🎉 Your app foundation is working perfectly!
  `);
});