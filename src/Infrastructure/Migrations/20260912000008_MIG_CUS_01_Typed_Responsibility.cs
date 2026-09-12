using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000008_MIG_CUS_01_Typed_Responsibility")]
public sealed class MIG_CUS_01_Typed_Responsibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "subject_type",
            table: "custodies",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "subject_id",
            table: "custodies",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(@"
            ALTER TABLE public.custodies
            ADD CONSTRAINT ck_custodies_subject_type
            CHECK (subject_type IN ('Asset', 'TrackedUnit', 'MaterialQuantity') OR subject_type IS NULL)
            NOT VALID;
            CREATE INDEX ix_custodies_subject
            ON public.custodies (subject_type, subject_id)
            WHERE subject_type IS NOT NULL;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP INDEX IF EXISTS public.ix_custodies_subject;
            ALTER TABLE public.custodies DROP CONSTRAINT IF EXISTS ck_custodies_subject_type;
        ");
        migrationBuilder.DropColumn(name: "subject_id", table: "custodies");
        migrationBuilder.DropColumn(name: "subject_type", table: "custodies");
    }
}
