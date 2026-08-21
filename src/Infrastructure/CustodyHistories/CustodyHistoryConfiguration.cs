using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.CustodyHistories;

internal sealed class CustodyHistoryConfiguration : IEntityTypeConfiguration<CustodyHistory>
{
    public void Configure(EntityTypeBuilder<CustodyHistory> builder)
    {
        builder.ToTable("custody_history");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.FromStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(history => history.ToStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(history => history.Note).HasMaxLength(300);
        builder.HasIndex(history => new { history.CustodyId, history.AtUtc, history.Id });
        builder.ToTable("custody_history", table =>
        {
            table.HasCheckConstraint("ck_custody_history_from_status_valid", "from_status IN ('Active', 'Closed')");
            table.HasCheckConstraint("ck_custody_history_to_status_valid", "to_status IN ('Active', 'Closed')");
            table.HasCheckConstraint("ck_custody_history_actual_transition", "from_status <> to_status");
        });
        builder.HasOne<Custody>().WithMany().HasForeignKey(history => history.CustodyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(history => history.ChangedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
