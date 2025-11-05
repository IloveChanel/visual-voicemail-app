import express from 'express';
import { body, query, param, validationResult } from 'express-validator';
import { Voicemail } from '../models/Voicemail';
import { User } from '../models/User';
import { AuthenticatedRequest } from '../middleware/auth';
import { logger } from '../config/logger';
import { asyncHandler } from '../middleware/errorHandler';
import { requireSubscription } from '../middleware/auth';

const router = express.Router();

// Get all voicemails for user
router.get('/',
  [
    query('page').optional().isInt({ min: 1 }).withMessage('Page must be a positive integer'),
    query('limit').optional().isInt({ min: 1, max: 100 }).withMessage('Limit must be between 1 and 100'),
    query('filter').optional().isIn(['all', 'unread', 'spam', 'favorites', 'archived']),
    query('search').optional().isLength({ min: 1, max: 100 })
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
    if (!userId) {
      return res.status(401).json({ error: 'User not authenticated' });
    }

    const page = parseInt(req.query.page as string) || 1;
    const limit = parseInt(req.query.limit as string) || 20;
    const filter = req.query.filter as string || 'all';
    const search = req.query.search as string;

    try {
      // Build query
      let query: any = { userId };

      // Apply filters
      switch (filter) {
        case 'unread':
          query.isRead = false;
          query.isArchived = false;
          break;
        case 'spam':
          query.isSpam = true;
          break;
        case 'favorites':
          query.isFavorite = true;
          query.isArchived = false;
          break;
        case 'archived':
          query.isArchived = true;
          break;
        case 'all':
        default:
          query.isArchived = false;
          break;
      }

      // Apply search
      if (search) {
        query.$or = [
          { transcription: { $regex: search, $options: 'i' } },
          { callerName: { $regex: search, $options: 'i' } },
          { callerNumber: { $regex: search, $options: 'i' } }
        ];
      }

      // Execute query with pagination
      const [voicemails, total] = await Promise.all([
        Voicemail.find(query)
          .sort({ timestamp: -1 })
          .skip((page - 1) * limit)
          .limit(limit)
          .lean(),
        Voicemail.countDocuments(query)
      ]);

      // Get statistics
      const stats = await Voicemail.aggregate([
        { $match: { userId, isArchived: false } },
        {
          $group: {
            _id: null,
            total: { $sum: 1 },
            unread: { $sum: { $cond: [{ $eq: ['$isRead', false] }, 1, 0] } },
            spam: { $sum: { $cond: [{ $eq: ['$isSpam', true] }, 1, 0] } },
            favorites: { $sum: { $cond: [{ $eq: ['$isFavorite', true] }, 1, 0] } }
          }
        }
      ]);

      res.json({
        voicemails,
        pagination: {
          page,
          limit,
          total,
          totalPages: Math.ceil(total / limit),
          hasNext: page * limit < total,
          hasPrev: page > 1
        },
        stats: stats[0] || { total: 0, unread: 0, spam: 0, favorites: 0 },
        filter
      });

    } catch (error) {
      logger.error('Failed to fetch voicemails:', error);
      res.status(500).json({
        error: 'Failed to fetch voicemails',
        message: 'An error occurred while retrieving voicemails'
      });
    }
  })
);

// Get specific voicemail
router.get('/:id',
  [
    param('id').isMongoId().withMessage('Invalid voicemail ID')
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
    const voicemailId = req.params.id;

    try {
      const voicemail = await Voicemail.findOne({
        _id: voicemailId,
        userId
      });

      if (!voicemail) {
        return res.status(404).json({
          error: 'Voicemail not found',
          message: 'The requested voicemail does not exist or you do not have permission to access it'
        });
      }

      // Mark as read if not already
      if (!voicemail.isRead) {
        voicemail.isRead = true;
        await voicemail.save();
      }

      res.json({ voicemail });

    } catch (error) {
      logger.error('Failed to fetch voicemail:', error);
      res.status(500).json({
        error: 'Failed to fetch voicemail',
        message: 'An error occurred while retrieving the voicemail'
      });
    }
  })
);

// Create new voicemail (typically called by carrier integration)
router.post('/',
  [
    body('callerNumber').isMobilePhone('any').withMessage('Valid caller number is required'),
    body('duration').isInt({ min: 0, max: 3600 }).withMessage('Duration must be between 0 and 3600 seconds'),
    body('audioUrl').isURL().withMessage('Valid audio URL is required'),
    body('timestamp').optional().isISO8601().withMessage('Invalid timestamp format'),
    body('callerName').optional().isLength({ max: 100 })
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
    if (!userId) {
      return res.status(401).json({ error: 'User not authenticated' });
    }

    try {
      const {
        callerNumber,
        callerName,
        duration,
        audioUrl,
        timestamp,
        metadata = {}
      } = req.body;

      // Check if user has premium subscription for unlimited voicemails
      const user = await User.findById(userId);
      if (!user) {
        return res.status(404).json({ error: 'User not found' });
      }

      // Free users limited to 50 voicemails per month
      if (!user.subscription.isActive) {
        const startOfMonth = new Date();
        startOfMonth.setDate(1);
        startOfMonth.setHours(0, 0, 0, 0);

        const monthlyCount = await Voicemail.countDocuments({
          userId,
          createdAt: { $gte: startOfMonth }
        });

        if (monthlyCount >= 50) {
          return res.status(403).json({
            error: 'Limit exceeded',
            message: 'Free users are limited to 50 voicemails per month. Upgrade to premium for unlimited voicemails.'
          });
        }
      }

      // Create voicemail
      const voicemail = new Voicemail({
        userId,
        callerNumber,
        callerName,
        duration,
        audioUrl,
        timestamp: timestamp ? new Date(timestamp) : new Date(),
        metadata
      });

      await voicemail.save();

      // Check if number is spam (basic implementation)
      // In production, integrate with spam detection service
      const spamKeywords = ['sales', 'offer', 'deal', 'promotion', 'limited time'];
      const isSpam = callerName ? 
        spamKeywords.some(keyword => 
          callerName.toLowerCase().includes(keyword)
        ) : false;

      if (isSpam) {
        voicemail.isSpam = true;
        voicemail.spamConfidence = 75;
        await voicemail.save();
      }

      // Trigger transcription for premium users
      if (user.subscription.isActive && user.preferences.autoTranscription) {
        // TODO: Implement transcription service
        voicemail.transcriptionStatus = 'pending';
        await voicemail.save();
      }

      logger.info(`New voicemail created: ${voicemail._id} for user: ${userId}`);

      res.status(201).json({
        message: 'Voicemail created successfully',
        voicemail
      });

    } catch (error) {
      logger.error('Failed to create voicemail:', error);
      res.status(500).json({
        error: 'Failed to create voicemail',
        message: 'An error occurred while creating the voicemail'
      });
    }
  })
);

// Update voicemail
router.put('/:id',
  [
    param('id').isMongoId().withMessage('Invalid voicemail ID'),
    body('isRead').optional().isBoolean(),
    body('isFavorite').optional().isBoolean(),
    body('isArchived').optional().isBoolean(),
    body('tags').optional().isArray()
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
    const voicemailId = req.params.id;

    try {
      const voicemail = await Voicemail.findOne({
        _id: voicemailId,
        userId
      });

      if (!voicemail) {
        return res.status(404).json({
          error: 'Voicemail not found',
          message: 'The requested voicemail does not exist'
        });
      }

      // Update allowed fields
      const allowedUpdates = ['isRead', 'isFavorite', 'isArchived', 'tags'];
      allowedUpdates.forEach(field => {
        if (req.body[field] !== undefined) {
          (voicemail as any)[field] = req.body[field];
        }
      });

      await voicemail.save();

      res.json({
        message: 'Voicemail updated successfully',
        voicemail
      });

    } catch (error) {
      logger.error('Failed to update voicemail:', error);
      res.status(500).json({
        error: 'Failed to update voicemail',
        message: 'An error occurred while updating the voicemail'
      });
    }
  })
);

// Delete voicemail
router.delete('/:id',
  [
    param('id').isMongoId().withMessage('Invalid voicemail ID')
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
    const voicemailId = req.params.id;

    try {
      const voicemail = await Voicemail.findOneAndDelete({
        _id: voicemailId,
        userId
      });

      if (!voicemail) {
        return res.status(404).json({
          error: 'Voicemail not found',
          message: 'The requested voicemail does not exist'
        });
      }

      // TODO: Delete audio file from storage

      logger.info(`Voicemail deleted: ${voicemailId} by user: ${userId}`);

      res.json({
        message: 'Voicemail deleted successfully'
      });

    } catch (error) {
      logger.error('Failed to delete voicemail:', error);
      res.status(500).json({
        error: 'Failed to delete voicemail',
        message: 'An error occurred while deleting the voicemail'
      });
    }
  })
);

// Request transcription (premium feature)
router.post('/:id/transcribe',
  [
    param('id').isMongoId().withMessage('Invalid voicemail ID')
  ],
  requireSubscription,
  asyncHandler(async (req: AuthenticatedRequest, res: express.Response) => {
    const errors = validationResult(req);
    if (!errors.isEmpty()) {
      return res.status(400).json({
        error: 'Validation failed',
        details: errors.array()
      });
    }

    const userId = req.user?.userId;
    const voicemailId = req.params.id;

    try {
      const voicemail = await Voicemail.findOne({
        _id: voicemailId,
        userId
      });

      if (!voicemail) {
        return res.status(404).json({
          error: 'Voicemail not found'
        });
      }

      if (voicemail.transcription) {
        return res.json({
          message: 'Transcription already exists',
          transcription: voicemail.transcription
        });
      }

      // Update transcription status
      voicemail.transcriptionStatus = 'pending';
      await voicemail.save();

      // TODO: Integrate with Google Speech-to-Text API
      // For now, return pending status
      res.json({
        message: 'Transcription requested successfully',
        status: 'pending'
      });

    } catch (error) {
      logger.error('Failed to request transcription:', error);
      res.status(500).json({
        error: 'Failed to request transcription',
        message: 'An error occurred while requesting transcription'
      });
    }
  })
);

export default router;