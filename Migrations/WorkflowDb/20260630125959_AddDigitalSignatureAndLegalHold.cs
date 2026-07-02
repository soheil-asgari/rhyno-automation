using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations.WorkflowDb
{
    public partial class AddDigitalSignatureAndLegalHold : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.DocumentArchiveItems', 'IsUnderLegalHold') IS NULL
                    ALTER TABLE dbo.DocumentArchiveItems ADD IsUnderLegalHold bit NOT NULL CONSTRAINT DF_DocumentArchiveItems_IsUnderLegalHold DEFAULT(0);

                IF COL_LENGTH('dbo.DocumentArchiveItems', 'HoldReason') IS NULL
                    ALTER TABLE dbo.DocumentArchiveItems ADD HoldReason nvarchar(1000) NULL;

                IF COL_LENGTH('dbo.DocumentSignatures', 'SignatureValue') IS NULL
                    ALTER TABLE dbo.DocumentSignatures ADD SignatureValue nvarchar(max) NOT NULL CONSTRAINT DF_DocumentSignatures_SignatureValue DEFAULT('');

                IF COL_LENGTH('dbo.DocumentSignatures', 'CertificateThumbprint') IS NULL
                    ALTER TABLE dbo.DocumentSignatures ADD CertificateThumbprint nvarchar(128) NULL;

                IF COL_LENGTH('dbo.DocumentSignatures', 'CertificateSubject') IS NULL
                    ALTER TABLE dbo.DocumentSignatures ADD CertificateSubject nvarchar(500) NULL;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_DocumentArchiveItems_IsUnderLegalHold'
                      AND object_id = OBJECT_ID('dbo.DocumentArchiveItems'))
                    CREATE INDEX IX_DocumentArchiveItems_IsUnderLegalHold ON dbo.DocumentArchiveItems(IsUnderLegalHold);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'IX_DocumentArchiveItems_IsUnderLegalHold'
                      AND object_id = OBJECT_ID('dbo.DocumentArchiveItems'))
                    DROP INDEX IX_DocumentArchiveItems_IsUnderLegalHold ON dbo.DocumentArchiveItems;

                IF COL_LENGTH('dbo.DocumentSignatures', 'CertificateSubject') IS NOT NULL
                    ALTER TABLE dbo.DocumentSignatures DROP COLUMN CertificateSubject;

                IF COL_LENGTH('dbo.DocumentSignatures', 'CertificateThumbprint') IS NOT NULL
                    ALTER TABLE dbo.DocumentSignatures DROP COLUMN CertificateThumbprint;

                IF COL_LENGTH('dbo.DocumentSignatures', 'SignatureValue') IS NOT NULL
                    ALTER TABLE dbo.DocumentSignatures DROP COLUMN SignatureValue;

                IF COL_LENGTH('dbo.DocumentArchiveItems', 'HoldReason') IS NOT NULL
                    ALTER TABLE dbo.DocumentArchiveItems DROP COLUMN HoldReason;

                IF COL_LENGTH('dbo.DocumentArchiveItems', 'IsUnderLegalHold') IS NOT NULL
                    ALTER TABLE dbo.DocumentArchiveItems DROP COLUMN IsUnderLegalHold;
                """);
        }
    }
}
