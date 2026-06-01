using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OfficeAutomation.Migrations
{
    /// <inheritdoc />
    public partial class AddWaybillModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Waybills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WaybillNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LoadingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OriginCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceiverName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DestinationCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DriverName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DriverNationalId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DriverPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    VehiclePlateNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VehicleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CargoType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalFreightCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DriverCommission = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPayToDriver = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waybills", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_WaybillNumber",
                table: "Waybills",
                column: "WaybillNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Waybills");
        }
    }
}
