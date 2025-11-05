import mongoose, { Document, Schema } from 'mongoose';

export interface IVoicemail extends Document {
  _id: mongoose.Types.ObjectId;
  userId: mongoose.Types.ObjectId;
  callerNumber: string;
  callerName?: string;
  duration: number; // in seconds
  audioUrl: string;
  transcription?: string;
  transcriptionStatus: 'pending' | 'completed' | 'failed' | 'not_requested';
  isSpam: boolean;
  spamConfidence?: number; // 0-100
  isRead: boolean;
  isArchived: boolean;
  isFavorite: boolean;
  timestamp: Date;
  metadata: {
    fileSize?: number;
    audioFormat?: string;
    quality?: 'low' | 'medium' | 'high';
    source?: 'carrier' | 'app' | 'import';
  };
  tags: string[];
  createdAt: Date;
  updatedAt: Date;
}

const VoicemailSchema = new Schema<IVoicemail>({
  userId: {
    type: Schema.Types.ObjectId,
    ref: 'User',
    required: true,
    index: true
  },
  callerNumber: {
    type: String,
    required: true,
    index: true,
    validate: {
      validator: function(v: string) {
        // Basic phone number validation (supports international formats)
        return /^\+?[\d\s\-\(\)\.]{10,}$/.test(v);
      },
      message: 'Invalid phone number format'
    }
  },
  callerName: {
    type: String,
    trim: true,
    maxlength: 100
  },
  duration: {
    type: Number,
    required: true,
    min: 0,
    max: 3600 // Max 1 hour
  },
  audioUrl: {
    type: String,
    required: true,
    validate: {
      validator: function(v: string) {
        return /^https?:\/\/.+/.test(v);
      },
      message: 'Audio URL must be a valid URL'
    }
  },
  transcription: {
    type: String,
    maxlength: 5000
  },
  transcriptionStatus: {
    type: String,
    enum: ['pending', 'completed', 'failed', 'not_requested'],
    default: 'not_requested'
  },
  isSpam: {
    type: Boolean,
    default: false,
    index: true
  },
  spamConfidence: {
    type: Number,
    min: 0,
    max: 100,
    validate: {
      validator: function(v: number) {
        return v === undefined || (v >= 0 && v <= 100);
      },
      message: 'Spam confidence must be between 0 and 100'
    }
  },
  isRead: {
    type: Boolean,
    default: false,
    index: true
  },
  isArchived: {
    type: Boolean,
    default: false,
    index: true
  },
  isFavorite: {
    type: Boolean,
    default: false,
    index: true
  },
  timestamp: {
    type: Date,
    required: true,
    index: true
  },
  metadata: {
    fileSize: {
      type: Number,
      min: 0
    },
    audioFormat: {
      type: String,
      enum: ['mp3', 'wav', 'aac', 'm4a', 'ogg'],
      lowercase: true
    },
    quality: {
      type: String,
      enum: ['low', 'medium', 'high'],
      default: 'medium'
    },
    source: {
      type: String,
      enum: ['carrier', 'app', 'import'],
      default: 'carrier'
    }
  },
  tags: [{
    type: String,
    trim: true,
    lowercase: true,
    maxlength: 50
  }]
}, {
  timestamps: true,
  toJSON: {
    transform: function(doc: any, ret: any) {
      delete ret.__v;
      return ret;
    }
  }
});

// Compound indexes for efficient queries
VoicemailSchema.index({ userId: 1, timestamp: -1 });
VoicemailSchema.index({ userId: 1, isRead: 1 });
VoicemailSchema.index({ userId: 1, isSpam: 1 });
VoicemailSchema.index({ userId: 1, isArchived: 1 });
VoicemailSchema.index({ userId: 1, isFavorite: 1 });
VoicemailSchema.index({ callerNumber: 1 });
VoicemailSchema.index({ timestamp: -1 });
VoicemailSchema.index({ transcriptionStatus: 1 });

// Text index for searching transcriptions
VoicemailSchema.index({ 
  transcription: 'text', 
  callerName: 'text', 
  callerNumber: 'text' 
});

// Virtual for formatted duration
VoicemailSchema.virtual('formattedDuration').get(function() {
  const minutes = Math.floor(this.duration / 60);
  const seconds = this.duration % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
});

// Methods
VoicemailSchema.methods.markAsRead = function() {
  this.isRead = true;
  return this.save();
};

VoicemailSchema.methods.toggleFavorite = function() {
  this.isFavorite = !this.isFavorite;
  return this.save();
};

VoicemailSchema.methods.archive = function() {
  this.isArchived = true;
  return this.save();
};

VoicemailSchema.methods.unarchive = function() {
  this.isArchived = false;
  return this.save();
};

// Static methods
VoicemailSchema.statics.findByUser = function(userId: mongoose.Types.ObjectId) {
  return this.find({ userId, isArchived: false }).sort({ timestamp: -1 });
};

VoicemailSchema.statics.findUnread = function(userId: mongoose.Types.ObjectId) {
  return this.find({ userId, isRead: false, isArchived: false }).sort({ timestamp: -1 });
};

VoicemailSchema.statics.findSpam = function(userId: mongoose.Types.ObjectId) {
  return this.find({ userId, isSpam: true }).sort({ timestamp: -1 });
};

export const Voicemail = mongoose.model<IVoicemail>('Voicemail', VoicemailSchema);