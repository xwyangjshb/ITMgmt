using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITDeviceManager.API.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Device",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IPAddress = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                MACAddress = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: false),
                DeviceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                OperatingSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Manufacturer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                WakeOnLanEnabled = table.Column<bool>(type: "bit", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Device", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DeviceRelations",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ParentDeviceId = table.Column<int>(type: "int", nullable: false),
                ChildDeviceId = table.Column<int>(type: "int", nullable: false),
                RelationType = table.Column<int>(type: "int", nullable: false),
                Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeviceRelations", x => x.Id);
                table.ForeignKey(
                    name: "FK_DeviceRelations_Device_ChildDeviceId",
                    column: x => x.ChildDeviceId,
                    principalTable: "Device",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DeviceRelations_Device_ParentDeviceId",
                    column: x => x.ParentDeviceId,
                    principalTable: "Device",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PowerOperations",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DeviceId = table.Column<int>(type: "int", nullable: false),
                Operation = table.Column<int>(type: "int", nullable: false),
                Result = table.Column<int>(type: "int", nullable: false),
                ResultMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PowerOperations", x => x.Id);
                table.ForeignKey(
                    name: "FK_PowerOperations_Device_DeviceId",
                    column: x => x.DeviceId,
                    principalTable: "Device",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Device_IPAddress",
            table: "Device",
            column: "IPAddress",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Device_MACAddress",
            table: "Device",
            column: "MACAddress",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeviceRelations_ChildDeviceId",
            table: "DeviceRelations",
            column: "ChildDeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_DeviceRelations_ParentDeviceId_ChildDeviceId",
            table: "DeviceRelations",
            columns: new[] { "ParentDeviceId", "ChildDeviceId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PowerOperations_DeviceId",
            table: "PowerOperations",
            column: "DeviceId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DeviceRelations");

        migrationBuilder.DropTable(
            name: "PowerOperations");

        migrationBuilder.DropTable(
            name: "Device");
    }
}
