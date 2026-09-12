using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000002_MIG_IAM_02_ExactlyOne_Assignment_Scope")]
public sealed class MIG_IAM_02_ExactlyOne_Assignment_Scope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "row_version",
            table: "user_role_scopes",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        // The three-scope target constraint belongs to the contract migration. PostgreSQL
        // enforces a NOT VALID constraint for new rows, so adding it during expand would
        // break the legacy application while it can still write OrganizationalUnit scopes.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "row_version", table: "user_role_scopes");
    }
}
