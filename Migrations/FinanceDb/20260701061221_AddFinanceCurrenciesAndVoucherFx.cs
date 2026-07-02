using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.FinanceDb
{
    /// <inheritdoc />
    public partial class AddFinanceCurrenciesAndVoucherFx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[Currencies]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Currencies](
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Symbol] nvarchar(12) NOT NULL,
        [IsBaseCurrency] bit NOT NULL CONSTRAINT [DF_Currencies_IsBaseCurrency] DEFAULT(0),
        [IsActive] bit NOT NULL CONSTRAINT [DF_Currencies_IsActive] DEFAULT(1),
        CONSTRAINT [PK_Currencies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Currencies_Code' AND object_id = OBJECT_ID(N'[dbo].[Currencies]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Currencies_Code] ON [dbo].[Currencies]([Code]);
END;

IF OBJECT_ID(N'[dbo].[CurrencyExchangeRates]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CurrencyExchangeRates](
        [Id] int IDENTITY(1,1) NOT NULL,
        [CurrencyId] uniqueidentifier NOT NULL,
        [RateDate] datetime2 NOT NULL,
        [BuyRate] decimal(18,8) NOT NULL,
        [SellRate] decimal(18,8) NOT NULL,
        CONSTRAINT [PK_CurrencyExchangeRates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CurrencyExchangeRates_Currencies_CurrencyId] FOREIGN KEY([CurrencyId]) REFERENCES [dbo].[Currencies]([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CurrencyExchangeRates_CurrencyId_RateDate' AND object_id = OBJECT_ID(N'[dbo].[CurrencyExchangeRates]'))
BEGIN
    CREATE UNIQUE INDEX [IX_CurrencyExchangeRates_CurrencyId_RateDate] ON [dbo].[CurrencyExchangeRates]([CurrencyId], [RateDate]);
END;

IF OBJECT_ID(N'[dbo].[VoucherLines]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[dbo].[VoucherLines]', N'CurrencyId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VoucherLines] ADD [CurrencyId] uniqueidentifier NULL;
    END
    ELSE IF EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'[dbo].[VoucherLines]')
          AND c.name = N'CurrencyId'
          AND t.name <> N'uniqueidentifier')
    BEGIN
        UPDATE [dbo].[VoucherLines]
        SET [CurrencyId] = NULL
        WHERE TRY_CONVERT(uniqueidentifier, [CurrencyId]) IS NULL;

        ALTER TABLE [dbo].[VoucherLines] ALTER COLUMN [CurrencyId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[dbo].[VoucherLines]', N'CurrencyRate') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VoucherLines] ADD [CurrencyRate] decimal(18,8) NOT NULL CONSTRAINT [DF_VoucherLines_CurrencyRate] DEFAULT(1);
    END;

    IF COL_LENGTH(N'[dbo].[VoucherLines]', N'ForeignAmount') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VoucherLines] ADD [ForeignAmount] decimal(18,4) NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherLines_CurrencyId' AND object_id = OBJECT_ID(N'[dbo].[VoucherLines]'))
    BEGIN
        CREATE INDEX [IX_VoucherLines_CurrencyId] ON [dbo].[VoucherLines]([CurrencyId]);
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VoucherLines_Currencies_CurrencyId')
    BEGIN
        ALTER TABLE [dbo].[VoucherLines]
        ADD CONSTRAINT [FK_VoucherLines_Currencies_CurrencyId]
        FOREIGN KEY([CurrencyId]) REFERENCES [dbo].[Currencies]([Id]) ON DELETE NO ACTION;
    END;
END;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[VoucherLines]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VoucherLines_Currencies_CurrencyId')
    BEGIN
        ALTER TABLE [dbo].[VoucherLines] DROP CONSTRAINT [FK_VoucherLines_Currencies_CurrencyId];
    END;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherLines_CurrencyId' AND object_id = OBJECT_ID(N'[dbo].[VoucherLines]'))
    BEGIN
        DROP INDEX [IX_VoucherLines_CurrencyId] ON [dbo].[VoucherLines];
    END;

    IF COL_LENGTH(N'[dbo].[VoucherLines]', N'ForeignAmount') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[VoucherLines] DROP COLUMN [ForeignAmount];
    END;

    IF COL_LENGTH(N'[dbo].[VoucherLines]', N'CurrencyRate') IS NOT NULL
    BEGIN
        DECLARE @dfName sysname;
        SELECT @dfName = dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[VoucherLines]') AND c.name = N'CurrencyRate';
        IF @dfName IS NOT NULL EXEC(N'ALTER TABLE [dbo].[VoucherLines] DROP CONSTRAINT [' + @dfName + N']');
        ALTER TABLE [dbo].[VoucherLines] DROP COLUMN [CurrencyRate];
    END;

    IF COL_LENGTH(N'[dbo].[VoucherLines]', N'CurrencyId') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[VoucherLines] DROP COLUMN [CurrencyId];
    END;
END;

IF OBJECT_ID(N'[dbo].[CurrencyExchangeRates]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[CurrencyExchangeRates];
END;

IF OBJECT_ID(N'[dbo].[Currencies]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Currencies];
END;
""");
        }
    }
}
