using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITDeviceManager.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialSQLiteMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Device",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IPAddress = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    MACAddress = table.Column<string>(type: "TEXT", maxLength: 17, nullable: false),
                    DeviceType = table.Column<int>(type: "INTEGER", maxLength: 50, nullable: false),
                    OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WakeOnLanEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Device", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "User"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceRelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentDeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChildDeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "MagicPacketCaptures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetMACAddress = table.Column<string>(type: "TEXT", maxLength: 17, nullable: false),
                    SourceIPAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MatchedDeviceId = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchedDeviceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PacketSize = table.Column<int>(type: "INTEGER", nullable: false),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagicPacketCaptures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MagicPacketCaptures_Device_MatchedDeviceId",
                        column: x => x.MatchedDeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PowerOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Operation = table.Column<int>(type: "INTEGER", nullable: false),
                    Result = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequestedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
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
                name: "IX_MagicPacketCaptures_CapturedAt",
                table: "MagicPacketCaptures",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MagicPacketCaptures_MatchedDeviceId",
                table: "MagicPacketCaptures",
                column: "MatchedDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MagicPacketCaptures_TargetMACAddress",
                table: "MagicPacketCaptures",
                column: "TargetMACAddress");

            migrationBuilder.CreateIndex(
                name: "IX_PowerOperations_DeviceId",
                table: "PowerOperations",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceRelations");

            migrationBuilder.DropTable(
                name: "MagicPacketCaptures");

            migrationBuilder.DropTable(
                name: "PowerOperations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Device");
        }
    }
}
