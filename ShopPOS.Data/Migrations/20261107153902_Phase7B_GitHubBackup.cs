using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopPOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase7B_GitHubBackup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubBranch",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "main");

            migrationBuilder.AddColumn<bool>(
                name: "GitHubEnabled",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GitHubExcludeImages",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "GitHubFileTooLarge",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GitHubOwner",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GitHubReconnectNeeded",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GitHubRepo",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GitHubRepoIsPublic",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastGitHubSquashAt",
                table: "ShopSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastGitHubUploadError",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulGitHubUploadAt",
                table: "ShopSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GitHubAttempts",
                table: "BackupRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GitHubFileSha",
                table: "BackupRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubLastError",
                table: "BackupRecords",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GitHubNextAttemptAt",
                table: "BackupRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GitHubStatus",
                table: "BackupRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "GitHubUploadedAt",
                table: "BackupRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_GitHubStatus",
                table: "BackupRecords",
                column: "GitHubStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackupRecords_GitHubStatus",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "GitHubBranch",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GitHubEnabled",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GitHubExcludeImages",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GitHubFileTooLarge",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GitHubOwner",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GitHubReconnectNeeded",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GitHubRepo",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GitHubRepoIsPublic",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "LastGitHubSquashAt",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "LastGitHubUploadError",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulGitHubUploadAt",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "GitHubAttempts",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "GitHubFileSha",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "GitHubLastError",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "GitHubNextAttemptAt",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "GitHubStatus",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "GitHubUploadedAt",
                table: "BackupRecords");
        }
    }
}
