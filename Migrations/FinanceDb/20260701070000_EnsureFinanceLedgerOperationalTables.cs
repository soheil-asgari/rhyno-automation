using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    [DbContext(typeof(FinanceDbContext))]
    [Migration("20260701070000_EnsureFinanceLedgerOperationalTables")]
    public partial class EnsureFinanceLedgerOperationalTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[FiscalYears]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FiscalYears](
        [Id] int IDENTITY(1,1) NOT NULL,
        [YearName] nvarchar(32) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsClosed] bit NOT NULL,
        CONSTRAINT [PK_FiscalYears] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_FiscalYears_YearName] ON [dbo].[FiscalYears]([YearName]);
    CREATE INDEX [IX_FiscalYears_StartDate_EndDate] ON [dbo].[FiscalYears]([StartDate], [EndDate]);
END;

IF OBJECT_ID(N'[dbo].[FiscalPeriods]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FiscalPeriods](
        [Id] uniqueidentifier NOT NULL,
        [FiscalYearId] int NOT NULL,
        [Name] nvarchar(64) NOT NULL,
        [PeriodNumber] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_FiscalPeriods_Status] DEFAULT(N'Open'),
        CONSTRAINT [PK_FiscalPeriods] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FiscalPeriods_FiscalYears_FiscalYearId] FOREIGN KEY([FiscalYearId]) REFERENCES [dbo].[FiscalYears]([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_FiscalPeriods_FiscalYearId_PeriodNumber] ON [dbo].[FiscalPeriods]([FiscalYearId], [PeriodNumber]);
    CREATE INDEX [IX_FiscalPeriods_FiscalYearId_StartDate_EndDate] ON [dbo].[FiscalPeriods]([FiscalYearId], [StartDate], [EndDate]);
END;

IF OBJECT_ID(N'[dbo].[AccountGroups]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccountGroups](
        [Id] int IDENTITY(1,1) NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Nature] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_AccountGroups] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_AccountGroups_Code] ON [dbo].[AccountGroups]([Code]);
END;

IF OBJECT_ID(N'[dbo].[GeneralAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GeneralAccounts](
        [Id] int IDENTITY(1,1) NOT NULL,
        [AccountGroupId] int NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_GeneralAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GeneralAccounts_AccountGroups_AccountGroupId] FOREIGN KEY([AccountGroupId]) REFERENCES [dbo].[AccountGroups]([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_GeneralAccounts_Code] ON [dbo].[GeneralAccounts]([Code]);
    CREATE INDEX [IX_GeneralAccounts_AccountGroupId] ON [dbo].[GeneralAccounts]([AccountGroupId]);
END;

IF OBJECT_ID(N'[dbo].[SubsidiaryAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SubsidiaryAccounts](
        [Id] int IDENTITY(1,1) NOT NULL,
        [GeneralAccountId] int NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [SystemKey] nvarchar(80) NOT NULL,
        [IsActive] bit NOT NULL,
        [AllowsFloatingDetail] bit NOT NULL,
        CONSTRAINT [PK_SubsidiaryAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubsidiaryAccounts_GeneralAccounts_GeneralAccountId] FOREIGN KEY([GeneralAccountId]) REFERENCES [dbo].[GeneralAccounts]([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_SubsidiaryAccounts_Code] ON [dbo].[SubsidiaryAccounts]([Code]);
    CREATE UNIQUE INDEX [IX_SubsidiaryAccounts_SystemKey] ON [dbo].[SubsidiaryAccounts]([SystemKey]);
    CREATE INDEX [IX_SubsidiaryAccounts_GeneralAccountId] ON [dbo].[SubsidiaryAccounts]([GeneralAccountId]);
END;

IF OBJECT_ID(N'[dbo].[DetailedAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DetailedAccounts](
        [Id] int IDENTITY(1,1) NOT NULL,
        [SubsidiaryAccountId] int NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [PartyType] nvarchar(50) NULL,
        [ExternalReference] nvarchar(120) NULL,
        [IsFloating] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_DetailedAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DetailedAccounts_SubsidiaryAccounts_SubsidiaryAccountId] FOREIGN KEY([SubsidiaryAccountId]) REFERENCES [dbo].[SubsidiaryAccounts]([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_DetailedAccounts_Code] ON [dbo].[DetailedAccounts]([Code]);
    CREATE INDEX [IX_DetailedAccounts_SubsidiaryAccountId] ON [dbo].[DetailedAccounts]([SubsidiaryAccountId]);
    CREATE INDEX [IX_DetailedAccounts_PartyType_ExternalReference] ON [dbo].[DetailedAccounts]([PartyType], [ExternalReference]);
END;

IF OBJECT_ID(N'[dbo].[JournalTypes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[JournalTypes](
        [Id] int IDENTITY(1,1) NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_JournalTypes] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_JournalTypes_Code] ON [dbo].[JournalTypes]([Code]);
END;

IF OBJECT_ID(N'[dbo].[VoucherHeaders]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[VoucherHeaders](
        [Id] int IDENTITY(1,1) NOT NULL,
        [VoucherNumber] nvarchar(50) NOT NULL,
        [DocumentNumber] nvarchar(80) NOT NULL,
        [VoucherDate] datetime2 NOT NULL,
        [Description] nvarchar(600) NULL,
        [Status] nvarchar(20) NOT NULL,
        [PostingStatus] nvarchar(20) NOT NULL,
        [TotalDebits] decimal(18,2) NOT NULL,
        [TotalCredits] decimal(18,2) NOT NULL,
        [FiscalYearId] int NOT NULL,
        [JournalTypeId] int NOT NULL,
        [ReversalOfVoucherHeaderId] int NULL,
        [ReversedByVoucherHeaderId] int NULL,
        CONSTRAINT [PK_VoucherHeaders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VoucherHeaders_FiscalYears_FiscalYearId] FOREIGN KEY([FiscalYearId]) REFERENCES [dbo].[FiscalYears]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VoucherHeaders_JournalTypes_JournalTypeId] FOREIGN KEY([JournalTypeId]) REFERENCES [dbo].[JournalTypes]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VoucherHeaders_VoucherHeaders_ReversalOfVoucherHeaderId] FOREIGN KEY([ReversalOfVoucherHeaderId]) REFERENCES [dbo].[VoucherHeaders]([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_VoucherHeaders_VoucherNumber] ON [dbo].[VoucherHeaders]([VoucherNumber]);
    CREATE INDEX [IX_VoucherHeaders_DocumentNumber] ON [dbo].[VoucherHeaders]([DocumentNumber]);
    CREATE INDEX [IX_VoucherHeaders_FiscalYearId_VoucherDate] ON [dbo].[VoucherHeaders]([FiscalYearId], [VoucherDate]);
    CREATE INDEX [IX_VoucherHeaders_JournalTypeId_VoucherDate] ON [dbo].[VoucherHeaders]([JournalTypeId], [VoucherDate]);
    CREATE INDEX [IX_VoucherHeaders_PostingStatus] ON [dbo].[VoucherHeaders]([PostingStatus]);
    CREATE UNIQUE INDEX [IX_VoucherHeaders_ReversalOfVoucherHeaderId] ON [dbo].[VoucherHeaders]([ReversalOfVoucherHeaderId]) WHERE [ReversalOfVoucherHeaderId] IS NOT NULL;
END;

IF OBJECT_ID(N'[dbo].[VoucherLines]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[VoucherLines](
        [Id] int IDENTITY(1,1) NOT NULL,
        [VoucherHeaderId] int NOT NULL,
        [SubsidiaryAccountId] int NOT NULL,
        [DetailedAccountId] int NULL,
        [CostCenterId] int NULL,
        [CurrencyId] uniqueidentifier NULL,
        [CurrencyRate] decimal(18,8) NOT NULL,
        [ForeignAmount] decimal(18,4) NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [Narration] nvarchar(600) NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_VoucherLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VoucherLines_VoucherHeaders_VoucherHeaderId] FOREIGN KEY([VoucherHeaderId]) REFERENCES [dbo].[VoucherHeaders]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_VoucherLines_SubsidiaryAccounts_SubsidiaryAccountId] FOREIGN KEY([SubsidiaryAccountId]) REFERENCES [dbo].[SubsidiaryAccounts]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VoucherLines_DetailedAccounts_DetailedAccountId] FOREIGN KEY([DetailedAccountId]) REFERENCES [dbo].[DetailedAccounts]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VoucherLines_Currencies_CurrencyId] FOREIGN KEY([CurrencyId]) REFERENCES [dbo].[Currencies]([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_VoucherLines_VoucherHeaderId_DisplayOrder] ON [dbo].[VoucherLines]([VoucherHeaderId], [DisplayOrder]);
    CREATE INDEX [IX_VoucherLines_SubsidiaryAccountId] ON [dbo].[VoucherLines]([SubsidiaryAccountId]);
    CREATE INDEX [IX_VoucherLines_DetailedAccountId] ON [dbo].[VoucherLines]([DetailedAccountId]);
    CREATE INDEX [IX_VoucherLines_CurrencyId] ON [dbo].[VoucherLines]([CurrencyId]);
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Currencies] WHERE [Code] = N'IRR')
    INSERT INTO [dbo].[Currencies]([Id], [Code], [Name], [Symbol], [IsBaseCurrency], [IsActive])
    VALUES(NEWID(), N'IRR', N'ریال', N'﷼', 1, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Currencies] WHERE [Code] = N'USD')
    INSERT INTO [dbo].[Currencies]([Id], [Code], [Name], [Symbol], [IsBaseCurrency], [IsActive])
    VALUES(NEWID(), N'USD', N'دلار آمریکا', N'$', 0, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Currencies] WHERE [Code] = N'EUR')
    INSERT INTO [dbo].[Currencies]([Id], [Code], [Name], [Symbol], [IsBaseCurrency], [IsActive])
    VALUES(NEWID(), N'EUR', N'یورو', N'€', 0, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[JournalTypes] WHERE [Code] = N'GENERAL')
    INSERT INTO [dbo].[JournalTypes]([Code], [Name], [IsActive]) VALUES(N'GENERAL', N'General Journal', 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[AccountGroups] WHERE [Code] = N'100')
    INSERT INTO [dbo].[AccountGroups]([Code], [Name], [Nature], [IsActive]) VALUES(N'100', N'دارایی‌ها', N'Debit', 1);

DECLARE @assetGroupId int = (SELECT TOP(1) [Id] FROM [dbo].[AccountGroups] WHERE [Code] = N'100');
IF NOT EXISTS (SELECT 1 FROM [dbo].[GeneralAccounts] WHERE [Code] = N'110')
    INSERT INTO [dbo].[GeneralAccounts]([AccountGroupId], [Code], [Name], [IsActive]) VALUES(@assetGroupId, N'110', N'وجوه نقد و بانک', 1);

DECLARE @cashGeneralId int = (SELECT TOP(1) [Id] FROM [dbo].[GeneralAccounts] WHERE [Code] = N'110');
IF NOT EXISTS (SELECT 1 FROM [dbo].[SubsidiaryAccounts] WHERE [Code] = N'1101')
    INSERT INTO [dbo].[SubsidiaryAccounts]([GeneralAccountId], [Code], [Name], [SystemKey], [IsActive], [AllowsFloatingDetail]) VALUES(@cashGeneralId, N'1101', N'بانک عملیاتی', N'OperationalCash', 1, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[SubsidiaryAccounts] WHERE [Code] = N'1102')
    INSERT INTO [dbo].[SubsidiaryAccounts]([GeneralAccountId], [Code], [Name], [SystemKey], [IsActive], [AllowsFloatingDetail]) VALUES(@cashGeneralId, N'1102', N'حساب واسط', N'ClearingAccount', 1, 1);
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
