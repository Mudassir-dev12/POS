using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopPOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_Backup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BackupEnabled",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "BackupOnAppClose",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "BackupTime",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "22:00");

            migrationBuilder.AddColumn<bool>(
                name: "EncryptBackups",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionKeyCheck",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionSalt",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KeepDaily",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "KeepMonthly",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "KeepWeekly",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "LastBackupError",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulBackupAt",
                table: "ShopSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondFolderPath",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BackupRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "INTEGER", nullable: false),
                    SchemaVersion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    VerifyStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DriveStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DriveFileId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DriveUploadedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DriveAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    DriveLastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DriveNextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeletedLocally = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_CreatedAt",
                table: "BackupRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_FileName",
                table: "BackupRecords",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_Kind",
                table: "BackupRecords",
                column: "Kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "BackupEnabled",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "BackupOnAppClose",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "BackupTime",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "EncryptBackups",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "EncryptionKeyCheck",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "EncryptionSalt",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "KeepDaily",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "KeepMonthly",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "KeepWeekly",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "LastBackupError",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulBackupAt",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "SecondFolderPath",
                table: "ShopSettings");
        }
    }
}
