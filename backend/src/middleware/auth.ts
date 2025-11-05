import { Request, Response, NextFunction } from 'express';
import { verifyFirebaseToken } from '../config/firebase';
import { User } from '../models/User';
import { logger } from '../config/logger';

export interface AuthenticatedRequest extends Request {
  user?: {
    uid: string;
    email: string;
    userId: string;
  };
}

export const authMiddleware = async (
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
): Promise<void> => {
  try {
    const authHeader = req.headers.authorization;

    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      res.status(401).json({
        error: 'Unauthorized',
        message: 'No valid authorization token provided'
      });
      return;
    }

    const token = authHeader.split(' ')[1];

    // Verify Firebase token
    const decodedToken = await verifyFirebaseToken(token);

    // Find user in database
    const user = await User.findOne({ 
      firebaseUid: decodedToken.uid,
      isActive: true 
    });

    if (!user) {
      res.status(401).json({
        error: 'User not found',
        message: 'User account not found or inactive'
      });
      return;
    }

    // Update last login
    user.lastLoginAt = new Date();
    await user.save();

    // Attach user info to request
    req.user = {
      uid: decodedToken.uid,
      email: decodedToken.email || user.email,
      userId: user._id.toString()
    };

    next();
  } catch (error) {
    logger.error('Authentication error:', error);
    res.status(401).json({
      error: 'Authentication failed',
      message: 'Invalid or expired token'
    });
  }
};

export const requireSubscription = async (
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
): Promise<void> => {
  try {
    if (!req.user) {
      res.status(401).json({
        error: 'Unauthorized',
        message: 'Authentication required'
      });
      return;
    }

    const user = await User.findById(req.user.userId);

    if (!user) {
      res.status(404).json({
        error: 'User not found',
        message: 'User account not found'
      });
      return;
    }

    // Check if user has active subscription
    if (!user.subscription.isActive) {
      res.status(403).json({
        error: 'Premium subscription required',
        message: 'This feature requires an active premium subscription'
      });
      return;
    }

    // Check if subscription is still valid
    if (user.subscription.endDate && user.subscription.endDate < new Date()) {
      // Update subscription status
      user.subscription.isActive = false;
      await user.save();

      res.status(403).json({
        error: 'Subscription expired',
        message: 'Your premium subscription has expired'
      });
      return;
    }

    next();
  } catch (error) {
    logger.error('Subscription check error:', error);
    res.status(500).json({
      error: 'Internal server error',
      message: 'Failed to verify subscription status'
    });
  }
};

export const requireRole = (roles: string[]) => {
  return async (
    req: AuthenticatedRequest,
    res: Response,
    next: NextFunction
  ): Promise<void> => {
    try {
      if (!req.user) {
        res.status(401).json({
          error: 'Unauthorized',
          message: 'Authentication required'
        });
        return;
      }

      const user = await User.findById(req.user.userId);

      if (!user) {
        res.status(404).json({
          error: 'User not found',
          message: 'User account not found'
        });
        return;
      }

      // For now, we'll implement basic role checking
      // You can extend this based on your role system
      const userRole = user.subscription.isActive ? 'premium' : 'free';

      if (!roles.includes(userRole)) {
        res.status(403).json({
          error: 'Insufficient privileges',
          message: 'You do not have permission to access this resource'
        });
        return;
      }

      next();
    } catch (error) {
      logger.error('Role check error:', error);
      res.status(500).json({
        error: 'Internal server error',
        message: 'Failed to verify user role'
      });
    }
  };
};