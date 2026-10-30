BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029152219_AdminVendorContactAndOperatingAreas'
)
BEGIN
    ALTER TABLE [Users] DROP CONSTRAINT [CK_Users_SocialEmail];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029152219_AdminVendorContactAndOperatingAreas'
)
BEGIN
    ALTER TABLE [Vendors] ADD [OperatingRegions] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029152219_AdminVendorContactAndOperatingAreas'
)
BEGIN
    ALTER TABLE [Users] ADD [LoginName] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029152219_AdminVendorContactAndOperatingAreas'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_LoginName] ON [Users] ([LoginName]) WHERE [LoginName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029152219_AdminVendorContactAndOperatingAreas'
)
BEGIN
    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_SocialEmail] CHECK ([Email] IS NOT NULL OR ([Role] = ''Customer'' AND ([GoogleSubject] IS NOT NULL OR [FacebookSubject] IS NOT NULL)) OR ([Role] = ''VendorOwner'' AND [LoginName] IS NOT NULL AND [LoginName] <> ''''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029152219_AdminVendorContactAndOperatingAreas'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029152219_AdminVendorContactAndOperatingAreas', N'10.0.13');
END;

COMMIT;
GO

