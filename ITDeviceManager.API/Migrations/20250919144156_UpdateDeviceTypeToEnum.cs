using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITDeviceManager.API.Migrations;

/// <inheritdoc />
public partial class UpdateDeviceTypeToEnum : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 首先添加新的临时列
        migrationBuilder.AddColumn<int>(
            name: "DeviceTypeTemp",
            table: "Device",
            type: "int",
            nullable: false,
            defaultValue: 0);

        // 更新数据：将字符串值转换为对应的枚举值
        migrationBuilder.Sql(@"
                UPDATE Device 
                SET DeviceTypeTemp = CASE 
                    WHEN DeviceType = 'Computer' THEN 1
                    WHEN DeviceType = 'Server' THEN 2
                    WHEN DeviceType = 'Router' THEN 3
                    WHEN DeviceType = 'Switch' THEN 4
                    WHEN DeviceType = 'Printer' THEN 5
                    WHEN DeviceType = 'Phone' THEN 6
                    WHEN DeviceType = 'Tablet' THEN 7
                    WHEN DeviceType = 'IoTDevice' THEN 8
                    WHEN DeviceType = 'Camera' THEN 9
                    WHEN DeviceType = 'AccessPoint' THEN 10
                    ELSE 0
                END");

        // 删除原列
        migrationBuilder.DropColumn(
            name: "DeviceType",
            table: "Device");

        // 重命名临时列
        migrationBuilder.RenameColumn(
            name: "DeviceTypeTemp",
            table: "Device",
            newName: "DeviceType");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "DeviceType",
            table: "Device",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int",
            oldMaxLength: 50);
    }
}
