using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopPOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase7B_GoogleDrive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveAccountEmail",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GoogleDriveDeleteToTrash",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GoogleDriveEnabled",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveFolderId",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GoogleDriveIsFull",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GoogleDriveReconnectNeeded",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveSubFolderId",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KeepManualBackupsOnDrive",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LastDriveUploadError",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulDriveUploadAt",
                table: "ShopSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriveMd5Checksum",
                table: "BackupRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriveUploadSessionUri",
                table: "BackupRecords",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleDriveAccountEmail",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GoogleDriveDeleteToTrash",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GoogleDriveEnabled",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GoogleDriveFolderId",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GoogleDriveIsFull",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GoogleDriveReconnectNeeded",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GoogleDriveSubFolderId",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "KeepManualBackupsOnDrive",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "LastDriveUploadError",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulDriveUploadAt",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "DriveMd5Checksum",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "DriveUploadSessionUri",
                table: "BackupRecords");
        }
    }
}
