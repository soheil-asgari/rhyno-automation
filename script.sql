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
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Leaves] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Leaves] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Leaves_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Letters] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [SentDate] datetime2 NOT NULL,
    [SenderId] nvarchar(450) NOT NULL,
    [ReceiverId] nvarchar(450) NOT NULL,
    [IsRead] bit NOT NULL,
    [ReadDate] datetime2 NULL,
    CONSTRAINT [PK_Letters] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Letters_AspNetUsers_ReceiverId] FOREIGN KEY ([ReceiverId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Letters_AspNetUsers_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_Leaves_UserId] ON [Leaves] ([UserId]);

CREATE INDEX [IX_Letters_ReceiverId] ON [Letters] ([ReceiverId]);

CREATE INDEX [IX_Letters_SenderId] ON [Letters] ([SenderId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260216171734_InitialCreate', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Role');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [AspNetUsers] ALTER COLUMN [Role] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260216172523_MakeFieldsNullable', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Leaves]') AND [c].[name] = N'Status');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Leaves] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Leaves] ALTER COLUMN [Status] nvarchar(max) NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260216174504_FinalFixModels', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [AspNetUsers] ADD [SignaturePath] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260217131146_AddSignatureToUser', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [AspNetUsers] ADD [Gender] int NOT NULL DEFAULT 0;

ALTER TABLE [AspNetUsers] ADD [JobTitle] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260217175452_AddGenderToUser', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUserTokens]') AND [c].[name] = N'Name');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUserTokens] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [AspNetUserTokens] ALTER COLUMN [Name] nvarchar(450) NOT NULL;

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUserTokens]') AND [c].[name] = N'LoginProvider');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUserTokens] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [AspNetUserTokens] ALTER COLUMN [LoginProvider] nvarchar(450) NOT NULL;

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Gender');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [AspNetUsers] ALTER COLUMN [Gender] nvarchar(max) NOT NULL;

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUserLogins]') AND [c].[name] = N'ProviderKey');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUserLogins] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [AspNetUserLogins] ALTER COLUMN [ProviderKey] nvarchar(450) NOT NULL;

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUserLogins]') AND [c].[name] = N'LoginProvider');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUserLogins] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [AspNetUserLogins] ALTER COLUMN [LoginProvider] nvarchar(450) NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260220160506_ChangeGenderToString', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Gender');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var7 + ';');
ALTER TABLE [AspNetUsers] ALTER COLUMN [Gender] nvarchar(max) NULL;

ALTER TABLE [AspNetUsers] ADD [ServiceLocation] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260221051021_AddServiceLocationToUser', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [AspNetUsers] ADD [Department] int NOT NULL DEFAULT 0;

ALTER TABLE [AspNetUsers] ADD [IsManager] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [ManagerId] nvarchar(450) NULL;

CREATE INDEX [IX_AspNetUsers_ManagerId] ON [AspNetUsers] ([ManagerId]);

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_AspNetUsers_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [AspNetUsers] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260221111933_AddServiceLocationToUser1', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260221160530_AddPhoneToUser', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260222064448_UpdateDatabase', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [InsuranceLists] (
    [Id] int NOT NULL IDENTITY,
    [ProjectName] nvarchar(max) NOT NULL,
    [ManagerName] nvarchar(max) NOT NULL,
    [Month] int NOT NULL,
    [Year] int NOT NULL,
    [EmployeeCount] int NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [FilePath] nvarchar(max) NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_InsuranceLists] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260316154427_CreateInsuranceTable', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [InsuranceEmployees] (
    [Id] int NOT NULL IDENTITY,
    [InsuranceListId] int NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [JobTitle] nvarchar(max) NOT NULL,
    [StartWork] datetime2 NOT NULL,
    [EndWork] datetime2 NULL,
    [WorkDays] int NOT NULL,
    [Salary] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_InsuranceEmployees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InsuranceEmployees_InsuranceLists_InsuranceListId] FOREIGN KEY ([InsuranceListId]) REFERENCES [InsuranceLists] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_InsuranceEmployees_InsuranceListId] ON [InsuranceEmployees] ([InsuranceListId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260316155318_CreateInsuranceTables', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var8 nvarchar(max);
SELECT @var8 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InsuranceLists]') AND [c].[name] = N'FilePath');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [InsuranceLists] DROP CONSTRAINT ' + @var8 + ';');
ALTER TABLE [InsuranceLists] ALTER COLUMN [FilePath] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260316174704_UpdateInsuranceList', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var9 nvarchar(max);
SELECT @var9 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Department');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT ' + @var9 + ';');
ALTER TABLE [AspNetUsers] DROP COLUMN [Department];

ALTER TABLE [AspNetUsers] ADD [DepartmentId] int NULL;

CREATE TABLE [Departments] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [ManagerId] nvarchar(450) NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Departments_AspNetUsers_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ManagerId', N'Name') AND [object_id] = OBJECT_ID(N'[Departments]'))
    SET IDENTITY_INSERT [Departments] ON;
INSERT INTO [Departments] ([Id], [ManagerId], [Name])
VALUES (1, NULL, N'Financial'),
(2, NULL, N'Administrative'),
(3, NULL, N'Technical'),
(4, NULL, N'HR'),
(5, NULL, N'Management');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ManagerId', N'Name') AND [object_id] = OBJECT_ID(N'[Departments]'))
    SET IDENTITY_INSERT [Departments] OFF;

CREATE INDEX [IX_AspNetUsers_DepartmentId] ON [AspNetUsers] ([DepartmentId]);

CREATE INDEX [IX_Departments_ManagerId] ON [Departments] ([ManagerId]);

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260316180603_AddDepartments', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Invoices] (
    [Id] int NOT NULL IDENTITY,
    [InvoiceNumber] nvarchar(50) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [VendorName] nvarchar(150) NOT NULL,
    [InvoiceDate] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Invoice_Number_Vendor] ON [Invoices] ([InvoiceNumber], [VendorName]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260517123807_AddInvoiceTable', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Waybills] (
    [Id] int NOT NULL IDENTITY,
    [WaybillNumber] nvarchar(50) NOT NULL,
    [IssueDate] datetime2 NOT NULL,
    [LoadingDate] datetime2 NOT NULL,
    [SenderName] nvarchar(150) NOT NULL,
    [OriginCity] nvarchar(100) NOT NULL,
    [ReceiverName] nvarchar(150) NOT NULL,
    [DestinationCity] nvarchar(100) NOT NULL,
    [DriverName] nvarchar(120) NOT NULL,
    [DriverNationalId] nvarchar(10) NOT NULL,
    [DriverPhone] nvarchar(15) NOT NULL,
    [VehiclePlateNumber] nvarchar(20) NOT NULL,
    [VehicleType] nvarchar(50) NOT NULL,
    [CargoType] nvarchar(120) NOT NULL,
    [Weight] decimal(18,3) NOT NULL,
    [TotalFreightCharges] decimal(18,2) NOT NULL,
    [DriverCommission] decimal(18,2) NOT NULL,
    [NetPayToDriver] decimal(18,2) NOT NULL,
    [PaymentStatus] nvarchar(30) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    CONSTRAINT [PK_Waybills] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Waybills_WaybillNumber] ON [Waybills] ([WaybillNumber]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260601075107_AddWaybillModule', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [HumanCapitalEmployees] (
    [Id] int NOT NULL IDENTITY,
    [PersonnelCode] nvarchar(30) NOT NULL,
    [FullName] nvarchar(120) NOT NULL,
    [NationalCode] nvarchar(20) NOT NULL,
    [BirthDate] datetime2 NOT NULL,
    [HireDate] datetime2 NOT NULL,
    [ContractEndDate] datetime2 NULL,
    [OnboardingCompleted] bit NOT NULL,
    [DepartmentId] int NULL,
    [PositionTitle] nvarchar(100) NOT NULL,
    [EmploymentType] nvarchar(60) NOT NULL,
    [CurrentSalary] decimal(18,2) NOT NULL,
    [CurrentStatus] nvarchar(40) NOT NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [Email] nvarchar(120) NULL,
    [Address] nvarchar(300) NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_HumanCapitalEmployees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HumanCapitalEmployees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [HumanCapitalSalaryHistories] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [PreviousSalary] decimal(18,2) NOT NULL,
    [NewSalary] decimal(18,2) NOT NULL,
    [PromotionTitle] nvarchar(120) NULL,
    [Reason] nvarchar(500) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_HumanCapitalSalaryHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HumanCapitalSalaryHistories_HumanCapitalEmployees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [HumanCapitalEmployees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [HumanCapitalStatusHistories] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [StatusType] nvarchar(40) NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [ReferenceNumber] nvarchar(120) NULL,
    [Description] nvarchar(500) NOT NULL,
    [ExitReason] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_HumanCapitalStatusHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_HumanCapitalStatusHistories_HumanCapitalEmployees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [HumanCapitalEmployees] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_HumanCapitalEmployees_DepartmentId] ON [HumanCapitalEmployees] ([DepartmentId]);

CREATE UNIQUE INDEX [IX_HumanCapitalEmployees_NationalCode] ON [HumanCapitalEmployees] ([NationalCode]);

CREATE UNIQUE INDEX [IX_HumanCapitalEmployees_PersonnelCode] ON [HumanCapitalEmployees] ([PersonnelCode]);

CREATE INDEX [IX_HumanCapitalSalaryHistories_EmployeeId_EffectiveDate] ON [HumanCapitalSalaryHistories] ([EmployeeId], [EffectiveDate]);

CREATE INDEX [IX_HumanCapitalStatusHistories_EmployeeId_EffectiveDate] ON [HumanCapitalStatusHistories] ([EmployeeId], [EffectiveDate]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260601110000_AddHumanCapitalModule', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [PayrollLists] (
    [Id] int NOT NULL IDENTITY,
    [Month] int NOT NULL,
    [Year] int NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [IsFinalized] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollLists] PRIMARY KEY ([Id])
);

CREATE TABLE [PayrollItems] (
    [Id] int NOT NULL IDENTITY,
    [PayrollListId] int NOT NULL,
    [EmployeeName] nvarchar(120) NOT NULL,
    [BaseSalary] decimal(18,2) NOT NULL,
    [Allowance] decimal(18,2) NOT NULL,
    [Overtime] decimal(18,2) NOT NULL,
    [InsuranceDeduction] decimal(18,2) NOT NULL,
    [Tax] decimal(18,2) NOT NULL,
    [NetPayable] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_PayrollItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollItems_PayrollLists_PayrollListId] FOREIGN KEY ([PayrollListId]) REFERENCES [PayrollLists] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_PayrollItems_PayrollListId] ON [PayrollItems] ([PayrollListId]);

CREATE UNIQUE INDEX [IX_PayrollLists_Year_Month] ON [PayrollLists] ([Year], [Month]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260601131249_ixayrollndnsuranceodels', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [PayrollItems] ADD [HumanCapitalEmployeeId] int NULL;

ALTER TABLE [InsuranceEmployees] ADD [HumanCapitalEmployeeId] int NULL;

CREATE INDEX [IX_PayrollItems_HumanCapitalEmployeeId] ON [PayrollItems] ([HumanCapitalEmployeeId]);

CREATE INDEX [IX_InsuranceEmployees_HumanCapitalEmployeeId] ON [InsuranceEmployees] ([HumanCapitalEmployeeId]);

ALTER TABLE [InsuranceEmployees] ADD CONSTRAINT [FK_InsuranceEmployees_HumanCapitalEmployees_HumanCapitalEmployeeId] FOREIGN KEY ([HumanCapitalEmployeeId]) REFERENCES [HumanCapitalEmployees] ([Id]) ON DELETE SET NULL;

ALTER TABLE [PayrollItems] ADD CONSTRAINT [FK_PayrollItems_HumanCapitalEmployees_HumanCapitalEmployeeId] FOREIGN KEY ([HumanCapitalEmployeeId]) REFERENCES [HumanCapitalEmployees] ([Id]) ON DELETE SET NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602060713_IntegratePayrollWithHumanCapital', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [InventoryCountings] (
    [Id] int NOT NULL IDENTITY,
    [DocumentNumber] nvarchar(40) NOT NULL,
    [DateShamsi] nvarchar(20) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Notes] nvarchar(600) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ApprovedAt] datetime2 NULL,
    CONSTRAINT [PK_InventoryCountings] PRIMARY KEY ([Id])
);

CREATE TABLE [Products] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Unit] nvarchar(30) NOT NULL,
    [Description] nvarchar(600) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
);

CREATE TABLE [WarehouseIssuances] (
    [Id] int NOT NULL IDENTITY,
    [IssuanceNumber] nvarchar(40) NOT NULL,
    [DateShamsi] nvarchar(20) NOT NULL,
    [DestinationOrDepartment] nvarchar(200) NOT NULL,
    [Notes] nvarchar(600) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WarehouseIssuances] PRIMARY KEY ([Id])
);

CREATE TABLE [WarehouseReceipts] (
    [Id] int NOT NULL IDENTITY,
    [ReceiptNumber] nvarchar(40) NOT NULL,
    [DateShamsi] nvarchar(20) NOT NULL,
    [SupplierOrSource] nvarchar(200) NOT NULL,
    [Notes] nvarchar(600) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WarehouseReceipts] PRIMARY KEY ([Id])
);

CREATE TABLE [InventoryCountingItems] (
    [Id] int NOT NULL IDENTITY,
    [InventoryCountingId] int NOT NULL,
    [ProductId] int NOT NULL,
    [SystemQuantity] decimal(18,3) NOT NULL,
    [PhysicalQuantity] decimal(18,3) NOT NULL,
    [DiscrepancyQuantity] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_InventoryCountingItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryCountingItems_InventoryCountings_InventoryCountingId] FOREIGN KEY ([InventoryCountingId]) REFERENCES [InventoryCountings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryCountingItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InventoryStocks] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [WarehouseId] int NOT NULL,
    [CurrentQuantity] decimal(18,3) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_InventoryStocks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryStocks_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [WarehouseIssuanceItems] (
    [Id] int NOT NULL IDENTITY,
    [WarehouseIssuanceId] int NOT NULL,
    [ProductId] int NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_WarehouseIssuanceItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseIssuanceItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseIssuanceItems_WarehouseIssuances_WarehouseIssuanceId] FOREIGN KEY ([WarehouseIssuanceId]) REFERENCES [WarehouseIssuances] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [WarehouseReceiptItems] (
    [Id] int NOT NULL IDENTITY,
    [WarehouseReceiptId] int NOT NULL,
    [ProductId] int NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_WarehouseReceiptItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseReceiptItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseReceiptItems_WarehouseReceipts_WarehouseReceiptId] FOREIGN KEY ([WarehouseReceiptId]) REFERENCES [WarehouseReceipts] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_InventoryCountingItems_InventoryCountingId] ON [InventoryCountingItems] ([InventoryCountingId]);

CREATE INDEX [IX_InventoryCountingItems_ProductId] ON [InventoryCountingItems] ([ProductId]);

CREATE UNIQUE INDEX [IX_InventoryCountings_DocumentNumber] ON [InventoryCountings] ([DocumentNumber]);

CREATE UNIQUE INDEX [IX_InventoryStocks_ProductId_WarehouseId] ON [InventoryStocks] ([ProductId], [WarehouseId]);

CREATE UNIQUE INDEX [IX_Products_Code] ON [Products] ([Code]);

CREATE INDEX [IX_WarehouseIssuanceItems_ProductId] ON [WarehouseIssuanceItems] ([ProductId]);

CREATE INDEX [IX_WarehouseIssuanceItems_WarehouseIssuanceId] ON [WarehouseIssuanceItems] ([WarehouseIssuanceId]);

CREATE UNIQUE INDEX [IX_WarehouseIssuances_IssuanceNumber] ON [WarehouseIssuances] ([IssuanceNumber]);

CREATE INDEX [IX_WarehouseReceiptItems_ProductId] ON [WarehouseReceiptItems] ([ProductId]);

CREATE INDEX [IX_WarehouseReceiptItems_WarehouseReceiptId] ON [WarehouseReceiptItems] ([WarehouseReceiptId]);

CREATE UNIQUE INDEX [IX_WarehouseReceipts_ReceiptNumber] ON [WarehouseReceipts] ([ReceiptNumber]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602064242_AddWarehouseAndInventoryModule', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [WarehouseReceipts] ADD [WarehouseId] int NOT NULL DEFAULT 0;

ALTER TABLE [WarehouseIssuances] ADD [WarehouseId] int NOT NULL DEFAULT 0;

ALTER TABLE [Products] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [InventoryCountings] ADD [WarehouseId] int NOT NULL DEFAULT 0;

CREATE TABLE [Warehouses] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [Location] nvarchar(200) NULL,
    [IsActive] bit NOT NULL,
    [IsClosed] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Warehouses] PRIMARY KEY ([Id])
);

CREATE TABLE [WarehouseClosings] (
    [Id] int NOT NULL IDENTITY,
    [WarehouseId] int NOT NULL,
    [DocumentNumber] nvarchar(40) NOT NULL,
    [ClosingDateShamsi] nvarchar(20) NOT NULL,
    [ClosingYear] int NOT NULL,
    [OpeningYear] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WarehouseClosings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseClosings_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InventoryOpeningBalanceLedgers] (
    [Id] int NOT NULL IDENTITY,
    [WarehouseId] int NOT NULL,
    [ProductId] int NOT NULL,
    [WarehouseClosingId] int NOT NULL,
    [PeriodYear] int NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_InventoryOpeningBalanceLedgers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryOpeningBalanceLedgers_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryOpeningBalanceLedgers_WarehouseClosings_WarehouseClosingId] FOREIGN KEY ([WarehouseClosingId]) REFERENCES [WarehouseClosings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryOpeningBalanceLedgers_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [WarehouseClosingItems] (
    [Id] int NOT NULL IDENTITY,
    [WarehouseClosingId] int NOT NULL,
    [ProductId] int NOT NULL,
    [ClosingQuantity] decimal(18,3) NOT NULL,
    [OpeningQuantity] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_WarehouseClosingItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseClosingItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseClosingItems_WarehouseClosings_WarehouseClosingId] FOREIGN KEY ([WarehouseClosingId]) REFERENCES [WarehouseClosings] ([Id]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'IsActive', N'IsClosed', N'Location', N'Name') AND [object_id] = OBJECT_ID(N'[Warehouses]'))
    SET IDENTITY_INSERT [Warehouses] ON;
INSERT INTO [Warehouses] ([Id], [Code], [CreatedAt], [IsActive], [IsClosed], [Location], [Name])
VALUES (1, N'WH-MAIN', '2026-01-01T00:00:00.0000000', CAST(1 AS bit), CAST(0 AS bit), N'ستاد', N'انبار مرکزی');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'IsActive', N'IsClosed', N'Location', N'Name') AND [object_id] = OBJECT_ID(N'[Warehouses]'))
    SET IDENTITY_INSERT [Warehouses] OFF;

CREATE INDEX [IX_WarehouseReceipts_WarehouseId] ON [WarehouseReceipts] ([WarehouseId]);

CREATE INDEX [IX_WarehouseIssuances_WarehouseId] ON [WarehouseIssuances] ([WarehouseId]);

CREATE INDEX [IX_InventoryStocks_WarehouseId] ON [InventoryStocks] ([WarehouseId]);

CREATE INDEX [IX_InventoryCountings_WarehouseId] ON [InventoryCountings] ([WarehouseId]);

CREATE INDEX [IX_InventoryOpeningBalanceLedgers_ProductId] ON [InventoryOpeningBalanceLedgers] ([ProductId]);

CREATE INDEX [IX_InventoryOpeningBalanceLedgers_WarehouseClosingId] ON [InventoryOpeningBalanceLedgers] ([WarehouseClosingId]);

CREATE UNIQUE INDEX [IX_InventoryOpeningBalanceLedgers_WarehouseId_ProductId_PeriodYear] ON [InventoryOpeningBalanceLedgers] ([WarehouseId], [ProductId], [PeriodYear]);

CREATE INDEX [IX_WarehouseClosingItems_ProductId] ON [WarehouseClosingItems] ([ProductId]);

CREATE INDEX [IX_WarehouseClosingItems_WarehouseClosingId] ON [WarehouseClosingItems] ([WarehouseClosingId]);

CREATE UNIQUE INDEX [IX_WarehouseClosings_DocumentNumber] ON [WarehouseClosings] ([DocumentNumber]);

CREATE INDEX [IX_WarehouseClosings_WarehouseId] ON [WarehouseClosings] ([WarehouseId]);

CREATE UNIQUE INDEX [IX_Warehouses_Code] ON [Warehouses] ([Code]);

ALTER TABLE [InventoryCountings] ADD CONSTRAINT [FK_InventoryCountings_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [InventoryStocks] ADD CONSTRAINT [FK_InventoryStocks_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [WarehouseIssuances] ADD CONSTRAINT [FK_WarehouseIssuances_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [WarehouseReceipts] ADD CONSTRAINT [FK_WarehouseReceipts_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602070507_WarehouseExpansionAndClosing', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Invoices] ADD [DateShamsi] nvarchar(20) NOT NULL DEFAULT N'';

ALTER TABLE [Invoices] ADD [GrandTotal] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Invoices] ADD [InvoiceType] nvarchar(20) NOT NULL DEFAULT N'';

ALTER TABLE [Invoices] ADD [NationalCodeOrEconomicId] nvarchar(30) NULL;

ALTER TABLE [Invoices] ADD [Notes] nvarchar(600) NULL;

ALTER TABLE [Invoices] ADD [PartyName] nvarchar(150) NOT NULL DEFAULT N'';

ALTER TABLE [Invoices] ADD [SubTotal] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Invoices] ADD [VatAmount] decimal(18,2) NOT NULL DEFAULT 0.0;

CREATE TABLE [InvoiceItems] (
    [Id] int NOT NULL IDENTITY,
    [InvoiceId] int NOT NULL,
    [ProductId] int NULL,
    [ItemName] nvarchar(150) NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [LineSubTotal] decimal(18,2) NOT NULL,
    [LineVatAmount] decimal(18,2) NOT NULL,
    [LineGrandTotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_InvoiceItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InvoiceItems_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InvoiceItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE SET NULL
);

CREATE INDEX [IX_InvoiceItems_InvoiceId] ON [InvoiceItems] ([InvoiceId]);

CREATE INDEX [IX_InvoiceItems_ProductId] ON [InvoiceItems] ([ProductId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602075622_AddInvoiceAndTradingSchema', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Invoices] ADD [DeadlineDateShamsi] nvarchar(20) NULL;

ALTER TABLE [Invoices] ADD [FollowUpEmployeeId] int NULL;

ALTER TABLE [Invoices] ADD [WarehouseReceiptId] int NULL;

CREATE INDEX [IX_Invoices_FollowUpEmployeeId] ON [Invoices] ([FollowUpEmployeeId]);

CREATE INDEX [IX_Invoices_WarehouseReceiptId] ON [Invoices] ([WarehouseReceiptId]);

ALTER TABLE [Invoices] ADD CONSTRAINT [FK_Invoices_HumanCapitalEmployees_FollowUpEmployeeId] FOREIGN KEY ([FollowUpEmployeeId]) REFERENCES [HumanCapitalEmployees] ([Id]) ON DELETE SET NULL;

ALTER TABLE [Invoices] ADD CONSTRAINT [FK_Invoices_WarehouseReceipts_WarehouseReceiptId] FOREIGN KEY ([WarehouseReceiptId]) REFERENCES [WarehouseReceipts] ([Id]) ON DELETE SET NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602081543_AddInvoiceTrackingAndWarehouseMapping', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_Invoice_Number_Vendor] ON [Invoices];

ALTER TABLE [PayrollLists] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [Invoices] ADD [RowVersion] rowversion NOT NULL;

ALTER TABLE [InventoryStocks] ADD [RowVersion] rowversion NOT NULL;

CREATE UNIQUE INDEX [IX_Invoice_Number_Type] ON [Invoices] ([InvoiceNumber], [InvoiceType]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602100613_ApplyConcurrencyAndFixUniqueIndexes', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AuditLogs] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(100) NULL,
    [Action] nvarchar(20) NOT NULL,
    [EntityName] nvarchar(80) NOT NULL,
    [EntityId] nvarchar(80) NOT NULL,
    [OldValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Invoice_Type_DateShamsi] ON [Invoices] ([InvoiceType], [DateShamsi]);

CREATE INDEX [IX_AuditLogs_EntityName_EntityId_Timestamp] ON [AuditLogs] ([EntityName], [EntityId], [Timestamp]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602101604_CompleteEnterpriseArchitectureAndAuditSchema', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Departments] ADD [ManagerEmployeeId] int NULL;

ALTER TABLE [AspNetUsers] ADD [CanAccessFinance] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [CanAccessHumanCapital] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [CanAccessSystemSettings] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [CanAccessWarehouse] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [EmployeeId] int NULL;

UPDATE [Departments] SET [ManagerEmployeeId] = NULL
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


UPDATE [Departments] SET [ManagerEmployeeId] = NULL
WHERE [Id] = 2;
SELECT @@ROWCOUNT;


UPDATE [Departments] SET [ManagerEmployeeId] = NULL
WHERE [Id] = 3;
SELECT @@ROWCOUNT;


UPDATE [Departments] SET [ManagerEmployeeId] = NULL
WHERE [Id] = 4;
SELECT @@ROWCOUNT;


UPDATE [Departments] SET [ManagerEmployeeId] = NULL
WHERE [Id] = 5;
SELECT @@ROWCOUNT;


CREATE INDEX [IX_Departments_ManagerEmployeeId] ON [Departments] ([ManagerEmployeeId]);

CREATE INDEX [IX_AspNetUsers_EmployeeId] ON [AspNetUsers] ([EmployeeId]);

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_HumanCapitalEmployees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [HumanCapitalEmployees] ([Id]) ON DELETE SET NULL;

ALTER TABLE [Departments] ADD CONSTRAINT [FK_Departments_HumanCapitalEmployees_ManagerEmployeeId] FOREIGN KEY ([ManagerEmployeeId]) REFERENCES [HumanCapitalEmployees] ([Id]) ON DELETE SET NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602104215_LinkUsersAndDepartmentsToHR', N'10.0.8');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [WarehouseReceipts] ADD [VendorId] int NULL;

ALTER TABLE [WarehouseIssuances] ADD [EmployerId] int NULL;

ALTER TABLE [Invoices] ADD [EmployerId] int NULL;

CREATE TABLE [Employers] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    [ContractNumber] nvarchar(50) NULL,
    [Phone] nvarchar(20) NULL,
    [Address] nvarchar(300) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Employers] PRIMARY KEY ([Id])
);

CREATE TABLE [Vendors] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    [EconomicCode] nvarchar(50) NULL,
    [NationalId] nvarchar(20) NULL,
    [Phone] nvarchar(20) NULL,
    [Address] nvarchar(300) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Vendors] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_WarehouseReceipts_VendorId] ON [WarehouseReceipts] ([VendorId]);

CREATE INDEX [IX_WarehouseIssuances_EmployerId] ON [WarehouseIssuances] ([EmployerId]);

CREATE INDEX [IX_Invoices_EmployerId] ON [Invoices] ([EmployerId]);

CREATE INDEX [IX_Employers_ContractNumber] ON [Employers] ([ContractNumber]);

CREATE INDEX [IX_Employers_Name] ON [Employers] ([Name]);

CREATE INDEX [IX_Vendors_EconomicCode] ON [Vendors] ([EconomicCode]);

CREATE INDEX [IX_Vendors_Name] ON [Vendors] ([Name]);

ALTER TABLE [Invoices] ADD CONSTRAINT [FK_Invoices_Employers_EmployerId] FOREIGN KEY ([EmployerId]) REFERENCES [Employers] ([Id]) ON DELETE SET NULL;

ALTER TABLE [WarehouseIssuances] ADD CONSTRAINT [FK_WarehouseIssuances_Employers_EmployerId] FOREIGN KEY ([EmployerId]) REFERENCES [Employers] ([Id]) ON DELETE SET NULL;

ALTER TABLE [WarehouseReceipts] ADD CONSTRAINT [FK_WarehouseReceipts_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE SET NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260602120100_AddVendorsEmployersAndStockOutputColumn', N'10.0.8');

COMMIT;
GO

