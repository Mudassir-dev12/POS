using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopPOS.Core.Entities;

namespace ShopPOS.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExpenseNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2);

        builder.Property(e => e.Payee)
            .HasMaxLength(150);

        builder.Property(e => e.Notes)
            .HasMaxLength(500);

        builder.Property(e => e.CancelReason)
            .HasMaxLength(250);

        builder.HasIndex(e => e.ExpenseDate);
        builder.HasIndex(e => e.ExpenseCategoryId);
        builder.HasIndex(e => new { e.IsCancelled, e.ExpenseDate, e.ExpenseCategoryId });

        builder.HasOne(e => e.ExpenseCategory)
            .WithMany(c => c.Expenses)
            .HasForeignKey(e => e.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

