import express from 'express';
import { AuthenticatedRequest } from '../middleware/auth';
import { asyncHandler } from '../middleware/errorHandler';

const router = express.Router();

router.get('/', asyncHandler(async (req: AuthenticatedRequest, res: express.Response) => {
  res.json({ message: 'User routes' });
}));

export default router;