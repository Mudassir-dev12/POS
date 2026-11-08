using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;

namespace ShopPOS.Data.Configurations;

public class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    public void Configure(EntityTypeBuilder<BackupRecord> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(b => b.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.Sha256)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(b => b.SchemaVersion)
            .HasMaxLength(150);

        builder.Property(b => b.AppVersion)
            .HasMaxLength(50);

        builder.Property(b => b.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(b => b.DriveFileId)
            .HasMaxLength(200);

        builder.Property(b => b.DriveLastError)
            .HasMaxLength(2000);

        builder.Property(b => b.DriveUploadSessionUri)
            .HasMaxLength(1000);

        builder.Property(b => b.DriveMd5Checksum)
            .HasMaxLength(64);

        // Phase 7B-GitHub configurations
        builder.Property(b => b.GitHubFileSha)
            .HasMaxLength(64);

        builder.Property(b => b.GitHubLastError)
            .HasMaxLength(2000);

        builder.HasIndex(b => b.CreatedAt);
        builder.HasIndex(b => b.FileName);
        builder.HasIndex(b => b.Kind);
        builder.HasIndex(b => b.GitHubStatus);
    }
}

