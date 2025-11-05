-- Visual Voicemail Pro Enhanced Database Migration
-- Supports coupons, trials, and developer whitelist functionality

-- Create Users table with enhanced features
CREATE TABLE Users (
    Id NVARCHAR(450) NOT NULL PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL,
    PhoneNumber NVARCHAR(20),
    SubscriptionTier NVARCHAR(50) NOT NULL DEFAULT 'Free',
    StripeCustomerId NVARCHAR(100),
    SubscriptionStatus NVARCHAR(50) NOT NULL DEFAULT 'inactive',
    SubscriptionExpiresAt DATETIME2,
    IsWhitelisted BIT NOT NULL DEFAULT 0,
    IsPremium BIT NOT NULL DEFAULT 0,
    WhitelistRole NVARCHAR(50),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Users_Email (Email),
    INDEX IX_Users_PhoneNumber (PhoneNumber),
    INDEX IX_Users_StripeCustomerId (StripeCustomerId),
    INDEX IX_Users_IsWhitelisted (IsWhitelisted)
);

-- Create Coupons table
CREATE TABLE Coupons (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Code NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(500),
    DiscountType NVARCHAR(20) NOT NULL, -- 'percentage' or 'fixed'
    DiscountValue DECIMAL(10,2) NOT NULL,
    MaxUses INT,
    CurrentUses INT NOT NULL DEFAULT 0,
    ExpiresAt DATETIME2,
    IsActive BIT NOT NULL DEFAULT 1,
    ApplicableTiers NVARCHAR(200), -- JSON array of tiers
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Coupons_Code (Code),
    INDEX IX_Coupons_IsActive (IsActive),
    INDEX IX_Coupons_ExpiresAt (ExpiresAt)
);

-- Create DeveloperWhitelists table
CREATE TABLE DeveloperWhitelists (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL UNIQUE,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Developer',
    AccessLevel NVARCHAR(50) NOT NULL DEFAULT 'Full',
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy NVARCHAR(256),
    Notes NVARCHAR(1000),
    
    INDEX IX_DeveloperWhitelists_Email (Email),
    INDEX IX_DeveloperWhitelists_Role (Role),
    INDEX IX_DeveloperWhitelists_IsActive (IsActive)
);

-- Create CouponUsages table for tracking
CREATE TABLE CouponUsages (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CouponId INT NOT NULL,
    UserId NVARCHAR(450) NOT NULL,
    UsedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DiscountAmount DECIMAL(10,2),
    SubscriptionTier NVARCHAR(50),
    
    FOREIGN KEY (CouponId) REFERENCES Coupons(Id),
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    INDEX IX_CouponUsages_CouponId (CouponId),
    INDEX IX_CouponUsages_UserId (UserId),
    INDEX IX_CouponUsages_UsedAt (UsedAt)
);

-- Create Voicemails table (enhanced from original)
CREATE TABLE Voicemails (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    CallerNumber NVARCHAR(20),
    CallerName NVARCHAR(100),
    Duration INT, -- in seconds
    Transcription NVARCHAR(MAX),
    TranscriptionConfidence DECIMAL(3,2),
    IsSpam BIT NOT NULL DEFAULT 0,
    SpamScore DECIMAL(3,2),
    AudioUrl NVARCHAR(500),
    IsRead BIT NOT NULL DEFAULT 0,
    ReceivedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    INDEX IX_Voicemails_UserId (UserId),
    INDEX IX_Voicemails_ReceivedAt (ReceivedAt),
    INDEX IX_Voicemails_IsSpam (IsSpam),
    INDEX IX_Voicemails_CallerNumber (CallerNumber)
);

-- Insert default coupons
INSERT INTO Coupons (Code, Description, DiscountType, DiscountValue, MaxUses, ApplicableTiers) VALUES
('WELCOME50', 'Welcome discount - 50% off first month', 'percentage', 50.00, 1000, '["Pro", "Business"]'),
('FREEMONTH', 'Free first month for new users', 'percentage', 100.00, 500, '["Pro"]'),
('SAVE20', '20% off any subscription', 'percentage', 20.00, NULL, '["Pro", "Business"]'),
('NEWUSER2024', 'New user special - $2 off', 'fixed', 2.00, 2000, '["Pro", "Business"]');

-- Insert developer whitelist entries
INSERT INTO DeveloperWhitelists (Email, Role, AccessLevel, Notes, CreatedBy) VALUES
('developer@visualvoicemail.pro', 'Admin', 'Full', 'Primary developer account', 'system'),
('test@visualvoicemail.pro', 'Developer', 'Full', 'Testing account', 'system'),
('copilot@visualvoicemail.pro', 'Developer', 'Full', 'AI Assistant development access', 'system');

-- Create stored procedure for coupon validation
CREATE PROCEDURE sp_ValidateCoupon
    @CouponCode NVARCHAR(50),
    @UserId NVARCHAR(450),
    @SubscriptionTier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CouponId INT, @MaxUses INT, @CurrentUses INT, @ExpiresAt DATETIME2, @IsActive BIT;
    DECLARE @ApplicableTiers NVARCHAR(200), @DiscountType NVARCHAR(20), @DiscountValue DECIMAL(10,2);
    DECLARE @HasUsed BIT = 0;
    
    -- Get coupon details
    SELECT 
        @CouponId = Id,
        @MaxUses = MaxUses,
        @CurrentUses = CurrentUses,
        @ExpiresAt = ExpiresAt,
        @IsActive = IsActive,
        @ApplicableTiers = ApplicableTiers,
        @DiscountType = DiscountType,
        @DiscountValue = DiscountValue
    FROM Coupons 
    WHERE Code = @CouponCode;
    
    -- Check if coupon exists
    IF @CouponId IS NULL
    BEGIN
        SELECT 'INVALID' as Status, 'Coupon code not found' as Message;
        RETURN;
    END
    
    -- Check if active
    IF @IsActive = 0
    BEGIN
        SELECT 'INACTIVE' as Status, 'Coupon is no longer active' as Message;
        RETURN;
    END
    
    -- Check expiration
    IF @ExpiresAt IS NOT NULL AND @ExpiresAt < GETUTCDATE()
    BEGIN
        SELECT 'EXPIRED' as Status, 'Coupon has expired' as Message;
        RETURN;
    END
    
    -- Check usage limits
    IF @MaxUses IS NOT NULL AND @CurrentUses >= @MaxUses
    BEGIN
        SELECT 'LIMIT_REACHED' as Status, 'Coupon usage limit reached' as Message;
        RETURN;
    END
    
    -- Check if user already used this coupon
    SELECT @HasUsed = 1 FROM CouponUsages WHERE CouponId = @CouponId AND UserId = @UserId;
    IF @HasUsed = 1
    BEGIN
        SELECT 'ALREADY_USED' as Status, 'You have already used this coupon' as Message;
        RETURN;
    END
    
    -- Check if applicable to subscription tier
    IF @ApplicableTiers IS NOT NULL AND @ApplicableTiers NOT LIKE '%"' + @SubscriptionTier + '"%'
    BEGIN
        SELECT 'TIER_MISMATCH' as Status, 'Coupon not applicable to selected subscription tier' as Message;
        RETURN;
    END
    
    -- Coupon is valid
    SELECT 
        'VALID' as Status, 
        'Coupon is valid' as Message,
        @DiscountType as DiscountType,
        @DiscountValue as DiscountValue,
        @CouponId as CouponId;
END;

-- Create function to check whitelist status
CREATE FUNCTION fn_CheckWhitelistStatus(@Email NVARCHAR(256))
RETURNS TABLE
AS
RETURN
(
    SELECT 
        CASE WHEN dw.Id IS NOT NULL THEN 1 ELSE 0 END as IsWhitelisted,
        dw.Role,
        dw.AccessLevel
    FROM DeveloperWhitelists dw
    WHERE dw.Email = @Email AND dw.IsActive = 1
);

-- Create Translation Usage table
CREATE TABLE TranslationUsages (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    SourceLanguage NVARCHAR(10) NOT NULL,
    TargetLanguage NVARCHAR(10) NOT NULL,
    CharacterCount INT NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ProcessingTime TIME,
    Context NVARCHAR(500),
    IsHighQuality BIT NOT NULL DEFAULT 0,
    Cost DECIMAL(10,4) NOT NULL DEFAULT 0,
    
    INDEX IX_TranslationUsages_UserId (UserId),
    INDEX IX_TranslationUsages_CreatedAt (CreatedAt),
    INDEX IX_TranslationUsages_Provider (Provider)
);

-- Create Translation Memory table
CREATE TABLE TranslationMemoryEntries (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SourceText NVARCHAR(MAX) NOT NULL,
    TranslatedText NVARCHAR(MAX) NOT NULL,
    SourceLanguage NVARCHAR(10) NOT NULL,
    TargetLanguage NVARCHAR(10) NOT NULL,
    Context NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastUsed DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UsageCount INT NOT NULL DEFAULT 1,
    QualityScore DECIMAL(3,2) NOT NULL DEFAULT 1.0,
    CreatedBy NVARCHAR(450),
    IsApproved BIT NOT NULL DEFAULT 1,
    
    INDEX IX_TranslationMemory_SourceTarget (SourceLanguage, TargetLanguage),
    INDEX IX_TranslationMemory_Context (Context),
    INDEX IX_TranslationMemory_CreatedAt (CreatedAt),
    INDEX IX_TranslationMemory_LastUsed (LastUsed)
);

-- Create Localization Resources table
CREATE TABLE LocalizationResources (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    [Key] NVARCHAR(200) NOT NULL,
    LanguageCode NVARCHAR(10) NOT NULL,
    Value NVARCHAR(MAX) NOT NULL,
    Description NVARCHAR(500),
    Category NVARCHAR(50) NOT NULL DEFAULT 'general',
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy NVARCHAR(450),
    IsPlural BIT NOT NULL DEFAULT 0,
    PluralForm NVARCHAR(1000),
    
    UNIQUE INDEX IX_LocalizationResources_KeyLanguage ([Key], LanguageCode),
    INDEX IX_LocalizationResources_LanguageCode (LanguageCode),
    INDEX IX_LocalizationResources_Category (Category)
);

-- Insert default localization resources
INSERT INTO LocalizationResources ([Key], LanguageCode, Value, Category, Description) VALUES
-- English resources
('app.title', 'en', 'Visual Voicemail Pro', 'app', 'Application title'),
('voicemail.transcribing', 'en', 'Transcribing voicemail...', 'voicemail', 'Transcription in progress message'),
('voicemail.translating', 'en', 'Translating voicemail...', 'voicemail', 'Translation in progress message'),
('translation.completed', 'en', 'Translation completed', 'voicemail', 'Translation success message'),
('subscription.upgrade', 'en', 'Upgrade to Pro', 'subscription', 'Upgrade button text'),
('spam.detected', 'en', 'Spam detected', 'spam', 'Spam detection alert'),
('language.detected', 'en', 'Language detected: {0}', 'translation', 'Language detection result'),
('provider.failed', 'en', 'Provider {0} failed, trying {1}', 'translation', 'Provider failover message'),

-- Spanish resources
('app.title', 'es', 'Correo de Voz Visual Pro', 'app', 'Título de la aplicación'),
('voicemail.transcribing', 'es', 'Transcribiendo correo de voz...', 'voicemail', 'Mensaje de transcripción en progreso'),
('voicemail.translating', 'es', 'Traduciendo correo de voz...', 'voicemail', 'Mensaje de traducción en progreso'),
('translation.completed', 'es', 'Traducción completada', 'voicemail', 'Mensaje de éxito de traducción'),
('subscription.upgrade', 'es', 'Actualizar a Pro', 'subscription', 'Texto del botón de actualización'),
('spam.detected', 'es', 'Spam detectado', 'spam', 'Alerta de detección de spam'),
('language.detected', 'es', 'Idioma detectado: {0}', 'translation', 'Resultado de detección de idioma'),
('provider.failed', 'es', 'Proveedor {0} falló, intentando {1}', 'translation', 'Mensaje de conmutación de proveedor'),

-- French resources  
('app.title', 'fr', 'Messagerie Vocale Visuelle Pro', 'app', 'Titre de l''application'),
('voicemail.transcribing', 'fr', 'Transcription du message vocal...', 'voicemail', 'Message de transcription en cours'),
('voicemail.translating', 'fr', 'Traduction du message vocal...', 'voicemail', 'Message de traduction en cours'),
('translation.completed', 'fr', 'Traduction terminée', 'voicemail', 'Message de succès de traduction'),
('subscription.upgrade', 'fr', 'Passer à Pro', 'subscription', 'Texte du bouton de mise à niveau'),
('spam.detected', 'fr', 'Spam détecté', 'spam', 'Alerte de détection de spam'),
('language.detected', 'fr', 'Langue détectée: {0}', 'translation', 'Résultat de détection de langue'),
('provider.failed', 'fr', 'Le fournisseur {0} a échoué, essai de {1}', 'translation', 'Message de basculement de fournisseur'),

-- German resources
('app.title', 'de', 'Visuelle Voicemail Pro', 'app', 'Anwendungstitel'),
('voicemail.transcribing', 'de', 'Voicemail wird transkribiert...', 'voicemail', 'Transkriptions-Fortschrittsnachricht'),
('voicemail.translating', 'de', 'Voicemail wird übersetzt...', 'voicemail', 'Übersetzungs-Fortschrittsnachricht'),
('translation.completed', 'de', 'Übersetzung abgeschlossen', 'voicemail', 'Übersetzungs-Erfolgsnachricht'),
('subscription.upgrade', 'de', 'Auf Pro upgraden', 'subscription', 'Upgrade-Button-Text'),
('spam.detected', 'de', 'Spam erkannt', 'spam', 'Spam-Erkennungsalarm'),
('language.detected', 'de', 'Sprache erkannt: {0}', 'translation', 'Spracherkennungsergebnis'),
('provider.failed', 'de', 'Anbieter {0} fehlgeschlagen, versuche {1}', 'translation', 'Anbieter-Failover-Nachricht');

PRINT 'Enhanced Visual Voicemail Pro database migration with multilingual support completed successfully!';