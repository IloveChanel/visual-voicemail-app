import mongoose, { Document, Schema } from 'mongoose';
import bcrypt from 'bcryptjs';

export interface IUser extends Document {
  _id: mongoose.Types.ObjectId;
  firebaseUid: string;
  email: string;
  phoneNumber: string;
  displayName?: string;
  profilePicture?: string;
  isVerified: boolean;
  subscription: {
    isActive: boolean;
    plan: 'free' | 'premium';
    startDate?: Date;
    endDate?: Date;
    stripeCustomerId?: string;
    stripeSubscriptionId?: string;
  };
  preferences: {
    autoTranscription: boolean;
    spamBlocking: boolean;
    notifications: {
      newVoicemail: boolean;
      spamDetection: boolean;
      promotions: boolean;
    };
    audioQuality: 'low' | 'medium' | 'high';
  };
  deviceTokens: string[]; // FCM tokens for push notifications
  createdAt: Date;
  updatedAt: Date;
  lastLoginAt?: Date;
  isActive: boolean;
  
  // Methods
  comparePassword(candidatePassword: string): Promise<boolean>;
  generateTokens(): { accessToken: string; refreshToken: string };
}

const UserSchema = new Schema<IUser>({
  firebaseUid: {
    type: String,
    required: true,
    unique: true,
    index: true
  },
  email: {
    type: String,
    required: true,
    unique: true,
    lowercase: true,
    index: true
  },
  phoneNumber: {
    type: String,
    required: true,
    unique: true,
    index: true
  },
  displayName: {
    type: String,
    trim: true,
    maxlength: 100
  },
  profilePicture: {
    type: String,
    validate: {
      validator: function(v: string) {
        return !v || /^https?:\/\/.+/.test(v);
      },
      message: 'Profile picture must be a valid URL'
    }
  },
  isVerified: {
    type: Boolean,
    default: false
  },
  subscription: {
    isActive: {
      type: Boolean,
      default: false
    },
    plan: {
      type: String,
      enum: ['free', 'premium'],
      default: 'free'
    },
    startDate: Date,
    endDate: Date,
    stripeCustomerId: String,
    stripeSubscriptionId: String
  },
  preferences: {
    autoTranscription: {
      type: Boolean,
      default: true
    },
    spamBlocking: {
      type: Boolean,
      default: true
    },
    notifications: {
      newVoicemail: {
        type: Boolean,
        default: true
      },
      spamDetection: {
        type: Boolean,
        default: true
      },
      promotions: {
        type: Boolean,
        default: false
      }
    },
    audioQuality: {
      type: String,
      enum: ['low', 'medium', 'high'],
      default: 'medium'
    }
  },
  deviceTokens: [{
    type: String,
    validate: {
      validator: function(v: string) {
        return v && v.length > 10; // Basic FCM token validation
      },
      message: 'Invalid device token'
    }
  }],
  lastLoginAt: Date,
  isActive: {
    type: Boolean,
    default: true
  }
}, {
  timestamps: true,
  toJSON: {
    transform: function(doc: any, ret: any) {
      const obj = ret;
      if ('__v' in obj) {
        delete obj.__v;
      }
      return obj;
    }
  }
});

// Indexes for performance
UserSchema.index({ firebaseUid: 1 });
UserSchema.index({ email: 1 });
UserSchema.index({ phoneNumber: 1 });
UserSchema.index({ 'subscription.isActive': 1 });
UserSchema.index({ createdAt: -1 });
UserSchema.index({ isActive: 1 });

// Methods
UserSchema.methods.comparePassword = async function(candidatePassword: string): Promise<boolean> {
  return bcrypt.compare(candidatePassword, this.password);
};

// Middleware
UserSchema.pre('save', function(next) {
  if (this.isModified('email')) {
    this.email = this.email.toLowerCase();
  }
  next();
});

// Update lastLoginAt when user logs in
UserSchema.methods.updateLastLogin = function() {
  this.lastLoginAt = new Date();
  return this.save();
};

export const User = mongoose.model<IUser>('User', UserSchema);