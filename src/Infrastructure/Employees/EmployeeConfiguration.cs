using Domain.Employees;
using Domain.OrganizationalUnits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Employees;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.EmployeeNumber).IsUnique();

        builder.Property(e => e.FullName).HasMaxLength(200);

        builder.Property(e => e.EmployeeNumber).HasMaxLength(50);

        builder.Property(e => e.JobTitle).HasMaxLength(100);

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<OrganizationalUnit>().WithMany().HasForeignKey(e => e.OrgUnitId);
    }
}
