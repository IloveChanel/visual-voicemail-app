const express = require('express');
const cors = require('cors');
const path = require('path');

const app = express();
const PORT = 3000;

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.static('public'));

// Mock services for testing
class MockPaymentService {
    async getUserSubscriptionAsync(userId) {
        console.log(`📊 Getting subscription for user: ${userId}`);
        return {
            userId: userId,
            tier: 'free',
            isActive: false,
            stripeSubscriptionId: null
        };
    }

    async activateSubscriptionAsync(userId, subscriptionId, tier) {
        console.log(`✅ Activating ${tier} subscription for user ${userId}: ${subscriptionId}`);
        return true;
    }

    async getUserAnalyticsAsync(userId) {
        return {
            totalVoicemails: 42,
            spamBlocked: 15,
            languagesDetected: ['en', 'es'],
            monthlySavings: '$12.50'
        };
    }
}

// Mock Stripe integration for testing
class MockStripeService {
    constructor() {
        this.pricingTiers = {
            'pro': {
                name: 'Visual Voicemail Pro',
                monthlyPrice: 3.49,
                priceId: 'price_1QBqwKLkdIwHu7ixeQ4Y8Z9X'
            },
            'business': {
                name: 'Business Pro',
                monthlyPrice: 9.99,
                priceId: 'price_1QBqwLLkdIwHu7ixaB3C4D5E'
            }
        };
    }

    async createCheckoutSessionAsync(userId, tier, customerEmail, phoneNumber) {
        console.log(`🎯 Creating checkout session:`);
        console.log(`   User: ${userId}`);
        console.log(`   Tier: ${tier} ($${this.pricingTiers[tier]?.monthlyPrice}/month)`);
        console.log(`   Email: ${customerEmail}`);
        console.log(`   Phone: ${phoneNumber}`);

        if (!this.pricingTiers[tier]) {
            return {
                success: false,
                errorMessage: `Invalid tier: ${tier}`
            };
        }

        // Simulate Stripe checkout session creation
        const sessionId = `cs_test_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        
        return {
            success: true,
            sessionId: sessionId,
            paymentUrl: `https://checkout.stripe.com/pay/${sessionId}`,
            customerId: `cus_${Math.random().toString(36).substr(2, 9)}`,
            tier: tier,
            monthlyPrice: this.pricingTiers[tier].monthlyPrice,
            trialDays: tier === 'pro' ? 7 : 0
        };
    }

    async getSubscriptionStatusAsync(userId) {
        // Mock subscription status
        return {
            isActive: false,
            tier: 'free',
            status: 'active',
            monthlyPrice: 0,
            features: ['Basic transcription', '5 voicemails/month', 'Standard spam detection'],
            trialEnd: null,
            cancelAtPeriodEnd: false
        };
    }
}

// Initialize mock services
const mockPaymentService = new MockPaymentService();
const mockStripeService = new MockStripeService();

console.log('🚀 Visual Voicemail Pro Test API Starting...');
console.log('📱 Testing Stripe integration for $3.49/month subscriptions');

// Subscription endpoints
app.post('/create-checkout-session', async (req, res) => {
    try {
        const { userId, tier, customerEmail, phoneNumber } = req.body;
        
        console.log('💳 Checkout session request received:', {
            userId, tier, customerEmail, phoneNumber
        });

        const result = await mockStripeService.createCheckoutSessionAsync(
            userId, tier, customerEmail, phoneNumber
        );

        if (!result.success) {
            return res.status(400).json({ error: result.errorMessage });
        }

        console.log('✅ Checkout session created successfully');
        res.json({
            success: true,
            sessionId: result.sessionId,
            paymentUrl: result.paymentUrl,
            tier: result.tier,
            monthlyPrice: result.monthlyPrice,
            trialDays: result.trialDays,
            message: `Ready for ${tier} subscription at $${result.monthlyPrice}/month with ${result.trialDays}-day free trial`
        });

    } catch (error) {
        console.error('❌ Checkout session error:', error);
        res.status(500).json({ error: 'Failed to create checkout session' });
    }
});

app.get('/subscription/status/:userId', async (req, res) => {
    try {
        const { userId } = req.params;
        const status = await mockStripeService.getSubscriptionStatusAsync(userId);
        
        res.json({
            success: true,
            ...status
        });
    } catch (error) {
        console.error('❌ Status check error:', error);
        res.status(500).json({ error: 'Failed to get subscription status' });
    }
});

app.get('/pricing', (req, res) => {
    res.json({
        success: true,
        tiers: [
            {
                id: 'free',
                name: 'Free',
                monthlyPrice: 0,
                features: ['Basic transcription', '5 voicemails/month', 'Standard spam detection'],
                recommended: false
            },
            {
                id: 'pro',
                name: 'Visual Voicemail Pro',
                monthlyPrice: 3.49,
                features: [
                    'Unlimited transcription',
                    'Multi-language support (9 languages)',
                    'Advanced spam detection',
                    'Real-time translation',
                    'Smart categorization',
                    'Priority support',
                    '7-day free trial'
                ],
                recommended: true
            },
            {
                id: 'business',
                name: 'Business Pro', 
                monthlyPrice: 9.99,
                features: [
                    'Everything in Pro',
                    'Team management',
                    'Analytics dashboard',
                    'API access',
                    'Priority processing',
                    'Custom integrations'
                ],
                recommended: false
            }
        ]
    });
});

// Health check
app.get('/health', (req, res) => {
    res.json({
        status: 'healthy',
        service: 'Visual Voicemail Pro Test API',
        version: '2.0',
        timestamp: new Date().toISOString(),
        features: [
            'Stripe subscriptions ($3.49/month)',
            'Multi-language transcription',
            'Advanced spam detection',
            '7-day free trial'
        ]
    });
});

// Existing voicemail API for compatibility
app.get('/api/voicemails', (req, res) => {
    const mockVoicemails = [
        {
            id: 'vm_001',
            callerName: 'Samsung Customer Service',
            callerNumber: '+1-800-726-7864',
            timestamp: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
            duration: 45,
            transcription: 'Hello, this is Samsung support regarding your recent inquiry about Visual Voicemail Pro.',
            isRead: false,
            isSpam: false
        },
        {
            id: 'vm_002', 
            callerName: 'Unknown',
            callerNumber: '+1-555-0123',
            timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
            duration: 12,
            transcription: 'Congratulations! You have won a free cruise. Call now to claim your prize.',
            isRead: true,
            isSpam: true
        },
        {
            id: 'vm_003',
            callerName: 'Dr. Smith Office',
            callerNumber: '+1-248-321-9121',
            timestamp: new Date(Date.now() - 1000 * 60 * 60 * 4).toISOString(),
            duration: 33,
            transcription: 'Hi, this is Dr. Smith\'s office. Your appointment for tomorrow at 2 PM is confirmed.',
            isRead: false,
            isSpam: false
        }
    ];

    const stats = {
        total: mockVoicemails.length,
        unread: mockVoicemails.filter(vm => !vm.isRead).length,
        spam: mockVoicemails.filter(vm => vm.isSpam).length,
        favorites: 1
    };

    res.json({
        success: true,
        stats: stats,
        voicemails: mockVoicemails
    });
});

// Start server
app.listen(PORT, () => {
    console.log(`🌟 Visual Voicemail Pro API running on http://localhost:${PORT}`);
    console.log(`📊 Ready to test $3.49/month Pro subscriptions`);
    console.log(`🔗 Test the subscription page: http://localhost:${PORT}/subscribe.html`);
    console.log(`📱 Samsung interface: http://localhost:${PORT}/index.html`);
    console.log(`💳 Pricing API: http://localhost:${PORT}/pricing`);
});