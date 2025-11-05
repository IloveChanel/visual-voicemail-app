import express from 'express';
import { body, validationResult } from 'express-validator';
import { User } from '../models/User';
import { verifyFirebaseToken } from '../config/firebase';
import { logger } from '../config/logger';
import { asyncHandler } from '../middleware/errorHandler';

const router = express.Router();

// Register new user
router.post('/register',
  [
    body('firebaseToken').notEmpty().withMessage('Firebase token is required'),
    body('phoneNumber').isMobilePhone('any').withMessage('Valid phone number is required'),
    body('email').isEmail().withMessage('Valid email is required'),
    body('displayName').optional().isLength({ min: 1, max: 100 })
  ],
  asyncHandler(async (req: express.Request, res: express.Response) => {
    // Check validation results
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      return res.status(400).json({
        error: 'Validation failed',
        details: errors.array()
      });
    }

    const { firebaseToken, phoneNumber, email, displayName } = req.body;

    try {
      // Verify Firebase token
      const decodedToken = await verifyFirebaseToken(firebaseToken);

      // Check if user already exists
      const existingUser = await User.findOne({
        $or: [
          { firebaseUid: decodedToken.uid },
          { email: email.toLowerCase() },
          { phoneNumber }
        ]
      });

      if (existingUser) {
        return res.status(409).json({
          error: 'User already exists',
          message: 'A user with this email, phone number, or Firebase UID already exists'
        });
      }

      // Create new user
      const newUser = new User({
        firebaseUid: decodedToken.uid,
        email: email.toLowerCase(),
        phoneNumber,
        displayName,
        isVerified: decodedToken.email_verified || false
      });

      await newUser.save();

      logger.info(`New user registered: ${newUser._id}`);

      res.status(201).json({
        message: 'User registered successfully',
        user: {
          id: newUser._id,
          email: newUser.email,
          phoneNumber: newUser.phoneNumber,
          displayName: newUser.displayName,
          isVerified: newUser.isVerified,
          subscription: newUser.subscription
        }
      });

    } catch (error) {
      logger.error('User registration failed:', error);
      res.status(500).json({
        error: 'Registration failed',
        message: 'Failed to register user'
      });
    }
  })
);

// Login user (verify and return user data)
router.post('/login',
  [
    body('firebaseToken').notEmpty().withMessage('Firebase token is required')
  ],
  asyncHandler(async (req: express.Request, res: express.Response) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      return res.status(400).json({
        error: 'Validation failed',
        details: errors.array()
      });
    }

    const { firebaseToken } = req.body;

    try {
      // Verify Firebase token
      const decodedToken = await verifyFirebaseToken(firebaseToken);

      // Find user in database
      const user = await User.findOne({ 
        firebaseUid: decodedToken.uid,
        isActive: true 
      });

      if (!user) {
        return res.status(404).json({
          error: 'User not found',
          message: 'Please register first'
        });
      }

      // Update last login
      user.lastLoginAt = new Date();
      await user.save();

      logger.info(`User logged in: ${user._id}`);

      res.json({
        message: 'Login successful',
        user: {
          id: user._id,
          email: user.email,
          phoneNumber: user.phoneNumber,
          displayName: user.displayName,
          isVerified: user.isVerified,
          subscription: user.subscription,
          preferences: user.preferences,
          lastLoginAt: user.lastLoginAt
        }
      });

    } catch (error) {
      logger.error('User login failed:', error);
      res.status(401).json({
        error: 'Login failed',
        message: 'Invalid authentication token'
      });
    }
  })
);

// Update FCM device token
router.post('/device-token',
  [
    body('firebaseToken').notEmpty().withMessage('Firebase token is required'),
    body('deviceToken').notEmpty().withMessage('Device token is required')
  ],
  asyncHandler(async (req: express.Request, res: express.Response) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      return res.status(400).json({
        error: 'Validation failed',
        details: errors.array()
      });
    }

    const { firebaseToken, deviceToken } = req.body;

    try {
      // Verify Firebase token
      const decodedToken = await verifyFirebaseToken(firebaseToken);

      // Find and update user
      const user = await User.findOne({ 
        firebaseUid: decodedToken.uid,
        isActive: true 
      });

      if (!user) {
        return res.status(404).json({
          error: 'User not found',
          message: 'User account not found'
        });
      }

      // Add device token if not already present
      if (!user.deviceTokens.includes(deviceToken)) {
        user.deviceTokens.push(deviceToken);
        await user.save();
      }

      res.json({
        message: 'Device token updated successfully'
      });

    } catch (error) {
      logger.error('Device token update failed:', error);
      res.status(500).json({
        error: 'Update failed',
        message: 'Failed to update device token'
      });
    }
  })
);

// Verify phone number
router.post('/verify-phone',
  [
    body('firebaseToken').notEmpty().withMessage('Firebase token is required'),
    body('verificationCode').notEmpty().withMessage('Verification code is required')
  ],
  asyncHandler(async (req: express.Request, res: express.Response) => {
    // This is a placeholder for phone verification
    // In a real implementation, you would integrate with Twilio or Firebase Phone Auth
    res.json({
      message: 'Phone verification not implemented yet',
      status: 'pending'
    });
  })
);

export default router;