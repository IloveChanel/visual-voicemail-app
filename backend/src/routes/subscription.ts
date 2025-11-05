import express from 'express';
import { body, validationResult } from 'express-validator';
import Stripe from 'stripe';
import { User } from '../models/User';
import { AuthenticatedRequest } from '../middleware/auth';
import { logger } from '../config/logger';
import { asyncHandler } from '../middleware/errorHandler';

const router = express.Router();
const stripe = new Stripe(process.env.STRIPE_SECRET_KEY!, {
  apiVersion: '2023-10-16'
});

// Get subscription status
router.get('/status',
  asyncHandler(async (req: AuthenticatedRequest, res: express.Response) => {
    const userId = req.user?.userId;
    if (!userId) {
      return res.status(401).json({ error: 'User not authenticated' });
    }

    try {
      const user = await User.findById(userId);
      if (!user) {
        return res.status(404).json({ error: 'User not found' });
      }

      res.json({
        subscription: user.subscription,
        features: {
          unlimitedVoicemails: user.subscription.isActive,
          transcription: user.subscription.isActive,
          advancedSpamDetection: user.subscription.isActive,
          noAds: user.subscription.isActive
        }
      });

    } catch (error) {
      logger.error('Failed to get subscription status:', error);
      res.status(500).json({ error: 'Failed to get subscription status' });
    }
  })
);

// Create subscription
router.post('/create',
  [
    body('priceId').notEmpty().withMessage('Price ID is required'),
    body('paymentMethodId').notEmpty().withMessage('Payment method ID is required')
  ],
  asyncHandler(async (req: AuthenticatedRequest, res: express.Response) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      return res.status(400).json({
        error: 'Validation failed',
        details: errors.array()
      });
    }

    const userId = req.user?.userId;
    const { priceId, paymentMethodId } = req.body;

    try {
      const user = await User.findById(userId);
      if (!user) {
        return res.status(404).json({ error: 'User not found' });
      }

      // Create or get Stripe customer
      let customerId = user.subscription.stripeCustomerId;
      
      if (!customerId) {
        const customer = await stripe.customers.create({
          email: user.email,
          phone: user.phoneNumber,
          name: user.displayName
        });
        customerId = customer.id;
        
        user.subscription.stripeCustomerId = customerId;
        await user.save();
      }

      // Attach payment method to customer
      await stripe.paymentMethods.attach(paymentMethodId, {
        customer: customerId
      });

      // Set as default payment method
      await stripe.customers.update(customerId, {
        invoice_settings: {
          default_payment_method: paymentMethodId
        }
      });

      // Create subscription
      const subscription = await stripe.subscriptions.create({
        customer: customerId,
        items: [{ price: priceId }],
        default_payment_method: paymentMethodId,
        expand: ['latest_invoice.payment_intent'],
        trial_period_days: 7 // 7-day free trial
      });

      // Update user subscription
      user.subscription.isActive = true;
      user.subscription.plan = 'premium';
      user.subscription.stripeSubscriptionId = subscription.id;
      user.subscription.startDate = new Date();
      user.subscription.endDate = new Date(subscription.current_period_end * 1000);
      await user.save();

      logger.info(`Subscription created for user: ${userId}`);

      res.json({
        message: 'Subscription created successfully',
        subscription: {
          id: subscription.id,
          status: subscription.status,
          trialEnd: subscription.trial_end
        }
      });

    } catch (error) {
      logger.error('Failed to create subscription:', error);
      res.status(500).json({ error: 'Failed to create subscription' });
    }
  })
);

// Cancel subscription
router.post('/cancel',
  asyncHandler(async (req: AuthenticatedRequest, res: express.Response) => {
    const userId = req.user?.userId;

    try {
      const user = await User.findById(userId);
      if (!user) {
        return res.status(404).json({ error: 'User not found' });
      }

      if (!user.subscription.stripeSubscriptionId) {
        return res.status(400).json({ error: 'No active subscription found' });
      }

      // Cancel at period end
      await stripe.subscriptions.update(user.subscription.stripeSubscriptionId, {
        cancel_at_period_end: true
      });

      logger.info(`Subscription cancelled for user: ${userId}`);

      res.json({
        message: 'Subscription cancelled successfully. Access will continue until the end of the current billing period.'
      });

    } catch (error) {
      logger.error('Failed to cancel subscription:', error);
      res.status(500).json({ error: 'Failed to cancel subscription' });
    }
  })
);

// Handle Stripe webhook
router.post('/webhook',
  express.raw({ type: 'application/json' }),
  asyncHandler(async (req: express.Request, res: express.Response) => {
    const sig = req.headers['stripe-signature']!;
    const endpointSecret = process.env.STRIPE_WEBHOOK_SECRET!;

    let event: Stripe.Event;

    try {
      event = stripe.webhooks.constructEvent(req.body, sig, endpointSecret);
    } catch (err: any) {
      logger.error(`Webhook signature verification failed: ${err.message}`);
      return res.status(400).send(`Webhook Error: ${err.message}`);
    }

    // Handle the event
    switch (event.type) {
      case 'subscription.created':
      case 'subscription.updated': {
        const subscription = event.data.object as Stripe.Subscription;
        const customer = subscription.customer as string;

        const user = await User.findOne({ 'subscription.stripeCustomerId': customer });
        if (user) {
          user.subscription.isActive = subscription.status === 'active';
          user.subscription.endDate = new Date(subscription.current_period_end * 1000);
          await user.save();
        }
        break;
      }

      case 'subscription.deleted': {
        const subscription = event.data.object as Stripe.Subscription;
        const customer = subscription.customer as string;

        const user = await User.findOne({ 'subscription.stripeCustomerId': customer });
        if (user) {
          user.subscription.isActive = false;
          user.subscription.plan = 'free';
          user.subscription.endDate = undefined;
          await user.save();
        }
        break;
      }

      default:
        logger.info(`Unhandled event type: ${event.type}`);
    }

    res.json({ received: true });
  })
);

export default router;