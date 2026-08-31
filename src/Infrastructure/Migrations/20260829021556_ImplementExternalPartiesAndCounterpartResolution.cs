using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementExternalPartiesAndCounterpartResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_parties",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    normalized_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    contact_info = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_parties", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_parties_normalized_code",
                schema: "public",
                table: "external_parties",
                column: "normalized_code",
                unique: true,
                filter: "normalized_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_external_parties_normalized_name_ar",
                schema: "public",
                table: "external_parties",
                column: "normalized_name_ar",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_external_parties_name_not_blank",
                schema: "public",
                table: "external_parties",
                sql: "length(btrim(name_ar)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_external_parties_status_valid",
                schema: "public",
                table: "external_parties",
                sql: "status IN ('Active', 'Inactive')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_external_parties_row_version_positive",
                schema: "public",
                table: "external_parties",
                sql: "row_version > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_custodies_operational_requires_non_employee",
                schema: "public",
                table: "custodies",
                sql: "custody_kind <> 'Operational' OR holder_type <> 'Employee'");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.assert_active_party_reference(
                    p_party_type text,
                    p_party_id uuid,
                    p_constraint_name text)
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF p_party_id IS NULL THEN
                        RAISE EXCEPTION 'Polymorphic party id is required'
                            USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                    END IF;

                    CASE p_party_type
                        WHEN 'Employee' THEN
                            PERFORM 1 FROM public.employees
                            WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                        WHEN 'OrganizationalUnit' THEN
                            PERFORM 1 FROM public.organizational_units
                            WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                        WHEN 'Site' THEN
                            PERFORM 1 FROM public.sites
                            WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                        WHEN 'External' THEN
                            PERFORM 1 FROM public.external_parties
                            WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                        ELSE
                            RAISE EXCEPTION 'Unsupported polymorphic party type: %', p_party_type
                                USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                    END CASE;

                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Polymorphic party %/% does not exist or is inactive', p_party_type, p_party_id
                            USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                    END IF;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.prevent_referenced_external_party_delete()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM public.issue_to
                        WHERE recipient_type = 'External' AND recipient_id = OLD.id)
                       OR EXISTS (
                        SELECT 1 FROM public.custodies
                        WHERE holder_type = 'External' AND holder_id = OLD.id) THEN
                        RAISE EXCEPTION 'Referenced external parties cannot be deleted'
                            USING ERRCODE = '23503', CONSTRAINT = 'trg_prevent_referenced_external_party_delete';
                    END IF;
                    RETURN OLD;
                END;
                $$;

                CREATE TRIGGER trg_prevent_referenced_external_party_delete
                BEFORE DELETE ON public.external_parties
                FOR EACH ROW
                EXECUTE FUNCTION public.prevent_referenced_external_party_delete();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_prevent_referenced_external_party_delete ON public.external_parties;
                DROP FUNCTION IF EXISTS public.prevent_referenced_external_party_delete();

                CREATE OR REPLACE FUNCTION public.assert_active_party_reference(
                    p_party_type text,
                    p_party_id uuid,
                    p_constraint_name text)
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF p_party_id IS NULL THEN
                        RAISE EXCEPTION 'Polymorphic party id is required'
                            USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                    END IF;

                    CASE p_party_type
                        WHEN 'Employee' THEN
                            PERFORM 1 FROM public.employees WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                        WHEN 'OrganizationalUnit' THEN
                            PERFORM 1 FROM public.organizational_units WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                        WHEN 'Site' THEN
                            PERFORM 1 FROM public.sites WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                        ELSE
                            RAISE EXCEPTION 'Unsupported polymorphic party type: %', p_party_type
                                USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                    END CASE;

                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Polymorphic party %/% does not exist or is inactive', p_party_type, p_party_id
                            USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                    END IF;
                END;
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_custodies_operational_requires_non_employee",
                schema: "public",
                table: "custodies");

            migrationBuilder.DropTable(
                name: "external_parties",
                schema: "public");
        }
    }
}
