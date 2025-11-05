import express from 'express';
import { body, validationResult } from 'express-validator';
import { AuthenticatedRequest } from '../middleware/auth';
import { logger } from '../config/logger';
import { asyncHandler } from '../middleware/errorHandler';

const router = express.Router();

// Check if number is spam
router.post('/check',
  [
    body('phoneNumber').isMobilePhone('any').withMessage('Valid phone number is required')
  ],
  asyncHandler(async (req: AuthenticatedRequest, res: express.Response) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      return res.status(400).json({
        error: 'Validation failed',
        details: errors.array()
      });
    }

    const { phoneNumber } = req.body;

    try {
      // Simple spam detection logic (replace with real service)
      const knownSpamNumbers = [
        '+1234567890',
        '+1555123456'
      ];

      const isSpam = knownSpamNumbers.includes(phoneNumber);
      const confidence = isSpam ? 95 : 10;

      res.json({
        phoneNumber,
        isSpam,
        confidence,
        source: 'internal_database'
      });

    } catch (error) {
      logger.error('Spam check failed:', error);
      res.status(500).json({ error: 'Spam check failed' });
    }
  })
);

export default router;