using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITDeviceManager.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMagicPacketCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MagicPacketCaptures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetMACAddress = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: false),
                    SourceIPAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MatchedDeviceId = table.Column<int>(type: "int", nullable: true),
                    MatchedDeviceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PacketSize = table.Column<int>(type: "int", nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MagicPacketCaptures");
        }
    }
}
