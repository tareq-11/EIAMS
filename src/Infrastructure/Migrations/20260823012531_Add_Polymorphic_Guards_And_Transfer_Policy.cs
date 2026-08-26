using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Polymorphic_Guards_And_Transfer_Policy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "governorate_code",
                schema: "public",
                table: "sites",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sites_governorate_code",
                schema: "public",
                table: "sites",
                column: "governorate_code",
                filter: "governorate_code IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sites_governorate_code_valid",
                schema: "public",
                table: "sites",
                sql: "governorate_code IS NULL OR governorate_code ~ '^[A-Z0-9_-]{1,20}$'");

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    invalid_issue_recipients integer;
                    invalid_active_holders integer;
                BEGIN
                    SELECT count(*) INTO invalid_issue_recipients
                    FROM public.issue_to AS reference
                    WHERE reference.recipient_type = 'External'
                       OR (reference.recipient_type = 'Employee' AND NOT EXISTS (
                            SELECT 1 FROM public.employees AS target
                            WHERE target.id = reference.recipient_id AND target.status = 'Active'))
                       OR (reference.recipient_type = 'OrganizationalUnit' AND NOT EXISTS (
                            SELECT 1 FROM public.organizational_units AS target
                            WHERE target.id = reference.recipient_id AND target.status = 'Active'))
                       OR (reference.recipient_type = 'Site' AND NOT EXISTS (
                            SELECT 1 FROM public.sites AS target
                            WHERE target.id = reference.recipient_id AND target.status = 'Active'))
                       OR reference.recipient_type NOT IN ('Employee', 'OrganizationalUnit', 'Site', 'External');

                    SELECT count(*) INTO invalid_active_holders
                    FROM public.custodies AS reference
                    WHERE reference.status = 'Active'
                      AND (
                           reference.holder_type = 'External'
                        OR (reference.holder_type = 'Employee' AND NOT EXISTS (
                            SELECT 1 FROM public.employees AS target
                            WHERE target.id = reference.holder_id AND target.status = 'Active'))
                        OR (reference.holder_type = 'OrganizationalUnit' AND NOT EXISTS (
                            SELECT 1 FROM public.organizational_units AS target
                            WHERE target.id = reference.holder_id AND target.status = 'Active'))
                        OR (reference.holder_type = 'Site' AND NOT EXISTS (
                            SELECT 1 FROM public.sites AS target
                            WHERE target.id = reference.holder_id AND target.status = 'Active'))
                        OR reference.holder_type NOT IN ('Employee', 'OrganizationalUnit', 'Site', 'External'));

                    IF invalid_issue_recipients > 0 OR invalid_active_holders > 0 THEN
                        RAISE EXCEPTION
                            'Cannot install polymorphic reference guards: % invalid issue recipients and % invalid active custody holders exist',
                            invalid_issue_recipients,
                            invalid_active_holders
                            USING ERRCODE = '23514';
                    END IF;
                END;
                $$;

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
                            WHERE id = p_party_id AND status = 'Active'
                            FOR SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'Polymorphic party %/% does not exist or is inactive', p_party_type, p_party_id
                                    USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                            END IF;
                        WHEN 'OrganizationalUnit' THEN
                            PERFORM 1 FROM public.organizational_units
                            WHERE id = p_party_id AND status = 'Active'
                            FOR SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'Polymorphic party %/% does not exist or is inactive', p_party_type, p_party_id
                                    USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                            END IF;
                        WHEN 'Site' THEN
                            PERFORM 1 FROM public.sites
                            WHERE id = p_party_id AND status = 'Active'
                            FOR SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'Polymorphic party %/% does not exist or is inactive', p_party_type, p_party_id
                                    USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                            END IF;
                        ELSE
                            RAISE EXCEPTION 'Unsupported polymorphic party type: %', p_party_type
                                USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                    END CASE;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.validate_issue_to_recipient()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    PERFORM public.assert_active_party_reference(
                        NEW.recipient_type,
                        NEW.recipient_id,
                        'trg_validate_recipient');
                    RETURN NEW;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.validate_custody_holder()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'INSERT'
                       OR NEW.status = 'Active'
                       OR NEW.holder_type IS DISTINCT FROM OLD.holder_type
                       OR NEW.holder_id IS DISTINCT FROM OLD.holder_id THEN
                        PERFORM public.assert_active_party_reference(
                            NEW.holder_type,
                            NEW.holder_id,
                            'trg_validate_custody_holder');
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_validate_recipient
                BEFORE INSERT OR UPDATE OF recipient_type, recipient_id
                ON public.issue_to
                FOR EACH ROW
                EXECUTE FUNCTION public.validate_issue_to_recipient();

                CREATE TRIGGER trg_validate_custody_holder
                BEFORE INSERT OR UPDATE OF holder_type, holder_id, status
                ON public.custodies
                FOR EACH ROW
                EXECUTE FUNCTION public.validate_custody_holder();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_validate_recipient ON public.issue_to;
                DROP TRIGGER IF EXISTS trg_validate_custody_holder ON public.custodies;
                DROP FUNCTION IF EXISTS public.validate_issue_to_recipient();
                DROP FUNCTION IF EXISTS public.validate_custody_holder();
                DROP FUNCTION IF EXISTS public.assert_active_party_reference(text, uuid, text);
                """);

            migrationBuilder.DropIndex(
                name: "ix_sites_governorate_code",
                schema: "public",
                table: "sites");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sites_governorate_code_valid",
                schema: "public",
                table: "sites");

            migrationBuilder.DropColumn(
                name: "governorate_code",
                schema: "public",
                table: "sites");
        }
    }
}
