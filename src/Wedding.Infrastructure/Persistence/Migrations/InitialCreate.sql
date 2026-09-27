IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Slug] nvarchar(150) NOT NULL,
        [Icon] nvarchar(100) NULL,
        [Description] nvarchar(500) NULL,
        [DefaultCommissionRate] decimal(5,4) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Role] nvarchar(30) NOT NULL,
        [AvatarUrl] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Articles] (
        [Id] uniqueidentifier NOT NULL,
        [AuthorId] uniqueidentifier NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Slug] nvarchar(300) NOT NULL,
        [Excerpt] nvarchar(1000) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ThumbnailUrl] nvarchar(500) NULL,
        [Category] nvarchar(100) NOT NULL,
        [TagsJson] nvarchar(1000) NULL,
        [IsPublished] bit NOT NULL,
        [PublishedAt] datetime2 NULL,
        [ViewCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Articles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Articles_Users_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [BudgetItems] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NULL,
        [ItemName] nvarchar(200) NOT NULL,
        [PlannedCost] decimal(18,2) NOT NULL,
        [ActualCost] decimal(18,2) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [IsPaid] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_BudgetItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BudgetItems_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_BudgetItems_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [ChecklistTasks] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Milestone] nvarchar(100) NOT NULL,
        [DueDate] datetime2 NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ChecklistTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChecklistTasks_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Vendors] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [BrandName] nvarchar(200) NOT NULL,
        [Slug] nvarchar(200) NOT NULL,
        [ContactPerson] nvarchar(150) NOT NULL,
        [Hotline] nvarchar(20) NOT NULL,
        [Address] nvarchar(300) NULL,
        [City] nvarchar(100) NOT NULL,
        [Bio] nvarchar(2000) NULL,
        [LogoUrl] nvarchar(500) NULL,
        [CoverImageUrl] nvarchar(500) NULL,
        [CommissionRate] decimal(5,4) NOT NULL,
        [RatingAvg] decimal(3,2) NOT NULL,
        [TotalReviews] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsVerified] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Vendors] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Vendors_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [WeddingInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [Slug] nvarchar(150) NOT NULL,
        [GroomName] nvarchar(100) NOT NULL,
        [BrideName] nvarchar(100) NOT NULL,
        [EventDate] datetime2 NOT NULL,
        [VenueName] nvarchar(200) NOT NULL,
        [VenueAddress] nvarchar(300) NOT NULL,
        [MapUrl] nvarchar(500) NULL,
        [LoveStory] nvarchar(3000) NULL,
        [CoverImageUrl] nvarchar(500) NULL,
        [MusicUrl] nvarchar(500) NULL,
        [TemplateStyle] nvarchar(50) NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_WeddingInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WeddingInvitations_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Listings] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Slug] nvarchar(250) NOT NULL,
        [MinPrice] decimal(18,2) NOT NULL,
        [MaxPrice] decimal(18,2) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [Location] nvarchar(200) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [RejectionReason] nvarchar(1000) NULL,
        [ViewCount] int NOT NULL,
        [FavoriteCount] int NOT NULL,
        [ModeratedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Listings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Listings_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Listings_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [GuestRSVPs] (
        [Id] uniqueidentifier NOT NULL,
        [InvitationId] uniqueidentifier NOT NULL,
        [GuestName] nvarchar(150) NOT NULL,
        [PhoneNumber] nvarchar(30) NULL,
        [Status] nvarchar(30) NOT NULL,
        [CompanionCount] int NOT NULL,
        [Wishes] nvarchar(1000) NULL,
        [DietaryPreference] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_GuestRSVPs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GuestRSVPs_WeddingInvitations_InvitationId] FOREIGN KEY ([InvitationId]) REFERENCES [WeddingInvitations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Leads] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [ListingId] uniqueidentifier NULL,
        [WeddingDate] datetime2 NULL,
        [EstimatedGuests] int NULL,
        [EstimatedBudget] decimal(18,2) NULL,
        [Notes] nvarchar(1000) NULL,
        [RawPhoneNumber] nvarchar(30) NOT NULL,
        [MaskedPhoneNumber] nvarchar(30) NOT NULL,
        [IsPhoneUnlocked] bit NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [SlaDeadline] datetime2 NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Leads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Leads_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Leads_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Leads_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [ListingMedias] (
        [Id] uniqueidentifier NOT NULL,
        [ListingId] uniqueidentifier NOT NULL,
        [MediaUrl] nvarchar(500) NOT NULL,
        [ThumbnailUrl] nvarchar(500) NULL,
        [MediaType] nvarchar(20) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsFeatured] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ListingMedias] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ListingMedias_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Vouchers] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(16) NOT NULL,
        [LeadId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [DiscountValue] decimal(18,2) NULL,
        [DiscountPercent] decimal(5,2) NULL,
        [Status] nvarchar(30) NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RedeemedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Vouchers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Vouchers_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Vouchers_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [BookingContracts] (
        [Id] uniqueidentifier NOT NULL,
        [ContractCode] nvarchar(50) NOT NULL,
        [LeadId] uniqueidentifier NOT NULL,
        [VoucherId] uniqueidentifier NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [ContractValue] decimal(18,2) NOT NULL,
        [DepositAmount] decimal(18,2) NOT NULL,
        [ContractImageUrl] nvarchar(500) NULL,
        [WeddingDate] datetime2 NULL,
        [Status] nvarchar(30) NOT NULL,
        [VerificationDeadline] datetime2 NOT NULL,
        [ConfirmedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_BookingContracts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BookingContracts_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BookingContracts_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BookingContracts_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BookingContracts_Vouchers_VoucherId] FOREIGN KEY ([VoucherId]) REFERENCES [Vouchers] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Commissions] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [Period] nvarchar(30) NOT NULL,
        [CommissionRate] decimal(5,4) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [DueDate] datetime2 NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [PaidAt] datetime2 NULL,
        [PaymentReferenceCode] nvarchar(100) NULL,
        [VietQrPayload] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Commissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Commissions_BookingContracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [BookingContracts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Commissions_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] uniqueidentifier NOT NULL,
        [BookingContractId] uniqueidentifier NULL,
        [ListingId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [Rating] int NOT NULL,
        [Content] nvarchar(3000) NOT NULL,
        [PhotosJson] nvarchar(4000) NULL,
        [IsVerifiedBuyer] bit NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [VendorReply] nvarchar(2000) NULL,
        [VendorRepliedAt] datetime2 NULL,
        [ModeratedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reviews_BookingContracts_BookingContractId] FOREIGN KEY ([BookingContractId]) REFERENCES [BookingContracts] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Reviews_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'DefaultCommissionRate', N'Description', N'DisplayOrder', N'Icon', N'IsActive', N'Name', N'Slug', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [CreatedAt], [DefaultCommissionRate], [Description], [DisplayOrder], [Icon], [IsActive], [Name], [Slug], [UpdatedAt])
    VALUES (''11111111-1111-1111-1111-111111111101'', ''2026-01-01T00:00:00.0000000Z'', 0.03, N''Sảnh tiệc khách sạn, nhà hàng tiệc cưới sang trọng'', 1, N''Building2'', CAST(1 AS bit), N''Trung tâm Tiệc cưới (Venues & Sảnh tiệc)'', N''tiem-cuoi-venues'', NULL),
    (''11111111-1111-1111-1111-111111111102'', ''2026-01-01T00:00:00.0000000Z'', 0.08, N''Thiết kế concept gia tiên, backdrop sảnh tiệc, hoa tươi'', 2, N''Flower2'', CAST(1 AS bit), N''Trang trí Tiệc cưới (Decor Concept)'', N''trang-tri-decor'', NULL),
    (''11111111-1111-1111-1111-111111111103'', ''2026-01-01T00:00:00.0000000Z'', 0.1, N''Chụp ảnh pre-wedding, phóng sự cưới, quay phim ngày cưới'', 3, N''Camera'', CAST(1 AS bit), N''Quay phim & Chụp ảnh (Photo / Video)'', N''quay-chup-photo-video'', NULL),
    (''11111111-1111-1111-1111-111111111104'', ''2026-01-01T00:00:00.0000000Z'', 0.1, N''Thuê và may đo váy cưới haute couture, vest chú rể cao cấp'', 4, N''Sparkles'', CAST(1 AS bit), N''Váy cưới & Vest cưới (Bridal & Suits)'', N''vay-cuoi-vest'', NULL),
    (''11111111-1111-1111-1111-111111111105'', ''2026-01-01T00:00:00.0000000Z'', 0.1, N''Makeup cô dâu ngày cưới, ăn hỏi và mẹ cô dâu chú rể'', 5, N''Smile'', CAST(1 AS bit), N''Trang điểm Cô dâu (Bridal Makeup)'', N''trang-diem-makeup'', NULL),
    (''11111111-1111-1111-1111-111111111106'', ''2026-01-01T00:00:00.0000000Z'', 0.08, N''In ấn thiệp cưới thiết kế riêng và quà tặng tri ân khách mời'', 6, N''Mail'', CAST(1 AS bit), N''Thiệp cưới & Quà cảm ơn (Invitations & Gifts)'', N''thiep-cuoi-qua-tang'', NULL),
    (''11111111-1111-1111-1111-111111111107'', ''2026-01-01T00:00:00.0000000Z'', 0.1, N''Lên kế hoạch, điều phối trọn gói toàn bộ đám cưới'', 7, N''CalendarHeart'', CAST(1 AS bit), N''Wedding Planner trọn gói (Planning)'', N''wedding-planner'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'DefaultCommissionRate', N'Description', N'DisplayOrder', N'Icon', N'IsActive', N'Name', N'Slug', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Articles_AuthorId] ON [Articles] ([AuthorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Articles_IsPublished] ON [Articles] ([IsPublished]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Articles_Slug] ON [Articles] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BookingContracts_ContractCode] ON [BookingContracts] ([ContractCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BookingContracts_CustomerId] ON [BookingContracts] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BookingContracts_LeadId] ON [BookingContracts] ([LeadId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BookingContracts_Status] ON [BookingContracts] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BookingContracts_VendorId] ON [BookingContracts] ([VendorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BookingContracts_VerificationDeadline] ON [BookingContracts] ([VerificationDeadline]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BookingContracts_VoucherId] ON [BookingContracts] ([VoucherId]) WHERE [VoucherId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetItems_CategoryId] ON [BudgetItems] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BudgetItems_CustomerId] ON [BudgetItems] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChecklistTasks_CustomerId] ON [ChecklistTasks] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChecklistTasks_IsCompleted] ON [ChecklistTasks] ([IsCompleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Commissions_ContractId] ON [Commissions] ([ContractId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Commissions_DueDate] ON [Commissions] ([DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Commissions_Status] ON [Commissions] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Commissions_VendorId] ON [Commissions] ([VendorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GuestRSVPs_InvitationId] ON [GuestRSVPs] ([InvitationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GuestRSVPs_Status] ON [GuestRSVPs] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_CustomerId] ON [Leads] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_ListingId] ON [Leads] ([ListingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_SlaDeadline] ON [Leads] ([SlaDeadline]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_Status] ON [Leads] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_VendorId] ON [Leads] ([VendorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ListingMedias_ListingId] ON [ListingMedias] ([ListingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_CategoryId] ON [Listings] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_Slug] ON [Listings] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_Status] ON [Listings] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Listings_VendorId] ON [Listings] ([VendorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Reviews_BookingContractId] ON [Reviews] ([BookingContractId]) WHERE [BookingContractId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_CustomerId] ON [Reviews] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_IsVerifiedBuyer] ON [Reviews] ([IsVerifiedBuyer]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_ListingId] ON [Reviews] ([ListingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_Status] ON [Reviews] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_VendorId] ON [Reviews] ([VendorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_PhoneNumber] ON [Users] ([PhoneNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vendors_City] ON [Vendors] ([City]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vendors_Slug] ON [Vendors] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vendors_UserId] ON [Vendors] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vouchers_Code] ON [Vouchers] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vouchers_CustomerId] ON [Vouchers] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vouchers_LeadId] ON [Vouchers] ([LeadId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WeddingInvitations_CustomerId] ON [WeddingInvitations] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WeddingInvitations_Slug] ON [WeddingInvitations] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927155406_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927155406_InitialCreate', N'8.0.10');
END;
GO

COMMIT;
GO

