using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260927103000_EnforcePolymorphicHolderTargets")]
public sealed class EnforcePolymorphicHolderTargets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Hold transactional write-blocking locks across preflight and guard installation.
            -- Any transaction already modifying these relations must finish before the scan;
            -- subsequent writes wait until the triggers are installed and this migration commits.
            LOCK TABLE public.custodies,
                       public.durable_custody_allocations,
                       public.tracked_material_units,
                       public.issue_to,
                       public.employees,
                       public.external_parties,
                       public.organizational_units,
                       public.sites
            IN SHARE ROW EXCLUSIVE MODE;

            DO $$
            DECLARE invalid_references bigint;
            BEGIN
                SELECT count(*) INTO invalid_references
                FROM (
                    SELECT holder_type AS party_type, holder_id AS party_id FROM public.custodies
                    UNION ALL
                    SELECT holder_type, holder_id FROM public.durable_custody_allocations
                    UNION ALL
                    SELECT holder_type, holder_id FROM public.tracked_material_units
                ) refs
                WHERE refs.party_id IS NULL
                   OR refs.party_type NOT IN ('Employee', 'OrganizationalUnit', 'Site', 'External')
                   OR (refs.party_type = 'Employee' AND NOT EXISTS (SELECT 1 FROM public.employees p WHERE p.id = refs.party_id))
                   OR (refs.party_type = 'OrganizationalUnit' AND NOT EXISTS (SELECT 1 FROM public.organizational_units p WHERE p.id = refs.party_id))
                   OR (refs.party_type = 'Site' AND NOT EXISTS (SELECT 1 FROM public.sites p WHERE p.id = refs.party_id))
                   OR (refs.party_type = 'External' AND NOT EXISTS (SELECT 1 FROM public.external_parties p WHERE p.id = refs.party_id));

                IF invalid_references > 0 THEN
                    RAISE EXCEPTION 'Cannot install polymorphic holder guards: % orphan or unknown references exist', invalid_references
                        USING ERRCODE = '23514';
                END IF;

                SELECT count(*) INTO invalid_references
                FROM (
                    SELECT holder_type AS party_type, holder_id AS party_id FROM public.custodies WHERE status = 'Active'
                    UNION ALL
                    SELECT holder_type, holder_id FROM public.durable_custody_allocations WHERE status = 'Active'
                    UNION ALL
                    SELECT holder_type, holder_id FROM public.tracked_material_units WHERE status = 'Issued'
                ) refs
                WHERE (refs.party_type = 'Employee' AND NOT EXISTS (SELECT 1 FROM public.employees p WHERE p.id = refs.party_id AND p.status = 'Active'))
                   OR (refs.party_type = 'OrganizationalUnit' AND NOT EXISTS (SELECT 1 FROM public.organizational_units p WHERE p.id = refs.party_id AND p.status = 'Active'))
                   OR (refs.party_type = 'Site' AND NOT EXISTS (SELECT 1 FROM public.sites p WHERE p.id = refs.party_id AND p.status = 'Active'))
                   OR (refs.party_type = 'External' AND NOT EXISTS (SELECT 1 FROM public.external_parties p WHERE p.id = refs.party_id AND p.status = 'Active'));

                IF invalid_references > 0 THEN
                    RAISE EXCEPTION 'Cannot install polymorphic holder guards: % active holders are inactive', invalid_references
                        USING ERRCODE = '23514';
                END IF;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.assert_active_party_reference(
                p_party_type text, p_party_id uuid, p_constraint_name text)
            RETURNS void LANGUAGE plpgsql AS $$
            BEGIN
                IF p_party_id IS NULL THEN
                    RAISE EXCEPTION 'Polymorphic party id is required'
                        USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END IF;
                CASE p_party_type
                    WHEN 'Employee' THEN PERFORM 1 FROM public.employees WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                    WHEN 'OrganizationalUnit' THEN PERFORM 1 FROM public.organizational_units WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                    WHEN 'Site' THEN PERFORM 1 FROM public.sites WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                    WHEN 'External' THEN PERFORM 1 FROM public.external_parties WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                    ELSE RAISE EXCEPTION 'Unsupported polymorphic party type: %', p_party_type
                        USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END CASE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Polymorphic party %/% does not exist or is inactive', p_party_type, p_party_id
                        USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END IF;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.validate_issue_to_recipient()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.recipient_type = 'External' THEN
                    RAISE EXCEPTION 'External parties are not supported as issue recipients'
                        USING ERRCODE = '23514', CONSTRAINT = 'trg_validate_recipient';
                END IF;
                PERFORM public.assert_active_party_reference(NEW.recipient_type, NEW.recipient_id, 'trg_validate_recipient');
                RETURN NEW;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.validate_custody_holder()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP = 'INSERT' OR NEW.status = 'Active'
                   OR NEW.holder_type IS DISTINCT FROM OLD.holder_type
                   OR NEW.holder_id IS DISTINCT FROM OLD.holder_id THEN
                    IF NEW.status = 'Active' THEN
                        PERFORM public.assert_active_party_reference(NEW.holder_type, NEW.holder_id, 'trg_validate_custody_holder');
                    ELSE
                        PERFORM public.assert_party_reference_exists(NEW.holder_type, NEW.holder_id, 'trg_validate_custody_holder');
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.validate_durable_allocation_holder()
            RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE require_active boolean := (NEW.status = 'Active');
            BEGIN
                IF TG_OP = 'INSERT' OR require_active
                   OR NEW.holder_type IS DISTINCT FROM OLD.holder_type
                   OR NEW.holder_id IS DISTINCT FROM OLD.holder_id THEN
                    IF require_active THEN
                        PERFORM public.assert_active_party_reference(NEW.holder_type, NEW.holder_id, 'trg_validate_durable_allocation_holder');
                    ELSE
                        PERFORM public.assert_party_reference_exists(NEW.holder_type, NEW.holder_id, 'trg_validate_durable_allocation_holder');
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.validate_tracked_unit_holder()
            RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE require_active boolean := (NEW.status = 'Issued');
            BEGIN
                IF TG_OP = 'INSERT' OR require_active
                   OR NEW.holder_type IS DISTINCT FROM OLD.holder_type
                   OR NEW.holder_id IS DISTINCT FROM OLD.holder_id THEN
                    IF require_active THEN
                        PERFORM public.assert_active_party_reference(NEW.holder_type, NEW.holder_id, 'trg_validate_tracked_unit_holder');
                    ELSE
                        PERFORM public.assert_party_reference_exists(NEW.holder_type, NEW.holder_id, 'trg_validate_tracked_unit_holder');
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.assert_party_reference_exists(
                p_party_type text, p_party_id uuid, p_constraint_name text)
            RETURNS void LANGUAGE plpgsql AS $$
            BEGIN
                IF p_party_id IS NULL THEN
                    RAISE EXCEPTION 'Polymorphic party id is required' USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END IF;
                CASE p_party_type
                    WHEN 'Employee' THEN PERFORM 1 FROM public.employees WHERE id = p_party_id;
                    WHEN 'OrganizationalUnit' THEN PERFORM 1 FROM public.organizational_units WHERE id = p_party_id;
                    WHEN 'Site' THEN PERFORM 1 FROM public.sites WHERE id = p_party_id;
                    WHEN 'External' THEN PERFORM 1 FROM public.external_parties WHERE id = p_party_id;
                    ELSE RAISE EXCEPTION 'Unsupported polymorphic party type: %', p_party_type USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END CASE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Polymorphic party %/% does not exist', p_party_type, p_party_id
                        USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END IF;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.prevent_deactivating_held_external_party()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD.status = 'Active' AND NEW.status <> 'Active' AND (
                    EXISTS (SELECT 1 FROM public.custodies WHERE holder_type = 'External' AND holder_id = OLD.id AND status = 'Active') OR
                    EXISTS (SELECT 1 FROM public.durable_custody_allocations WHERE holder_type = 'External' AND holder_id = OLD.id AND status = 'Active') OR
                    EXISTS (SELECT 1 FROM public.tracked_material_units WHERE holder_type = 'External' AND holder_id = OLD.id AND status = 'Issued')) THEN
                    RAISE EXCEPTION 'External party % has active custody references', OLD.id
                        USING ERRCODE = '23514', CONSTRAINT = 'trg_prevent_deactivating_held_external_party';
                END IF;
                RETURN NEW;
            END;
            $$;

            DROP TRIGGER IF EXISTS trg_validate_custody_holder ON public.custodies;
            CREATE TRIGGER trg_validate_custody_holder BEFORE INSERT OR UPDATE OF holder_type, holder_id, status
                ON public.custodies FOR EACH ROW EXECUTE FUNCTION public.validate_custody_holder();
            CREATE TRIGGER trg_validate_durable_allocation_holder BEFORE INSERT OR UPDATE OF holder_type, holder_id, status
                ON public.durable_custody_allocations FOR EACH ROW EXECUTE FUNCTION public.validate_durable_allocation_holder();
            CREATE TRIGGER trg_validate_tracked_unit_holder BEFORE INSERT OR UPDATE OF holder_type, holder_id, status
                ON public.tracked_material_units FOR EACH ROW EXECUTE FUNCTION public.validate_tracked_unit_holder();
            CREATE TRIGGER trg_prevent_deactivating_held_external_party BEFORE UPDATE OF status
                ON public.external_parties FOR EACH ROW EXECUTE FUNCTION public.prevent_deactivating_held_external_party();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS trg_validate_durable_allocation_holder ON public.durable_custody_allocations;
            DROP TRIGGER IF EXISTS trg_validate_tracked_unit_holder ON public.tracked_material_units;
            DROP TRIGGER IF EXISTS trg_prevent_deactivating_held_external_party ON public.external_parties;
            DROP FUNCTION IF EXISTS public.validate_durable_allocation_holder();
            DROP FUNCTION IF EXISTS public.validate_tracked_unit_holder();
            DROP FUNCTION IF EXISTS public.assert_party_reference_exists(text, uuid, text);
            DROP FUNCTION IF EXISTS public.prevent_deactivating_held_external_party();

            CREATE OR REPLACE FUNCTION public.assert_active_party_reference(
                p_party_type text, p_party_id uuid, p_constraint_name text)
            RETURNS void LANGUAGE plpgsql AS $$
            BEGIN
                IF p_party_id IS NULL THEN
                    RAISE EXCEPTION 'Polymorphic party id is required' USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END IF;
                CASE p_party_type
                    WHEN 'Employee' THEN PERFORM 1 FROM public.employees WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                    WHEN 'OrganizationalUnit' THEN PERFORM 1 FROM public.organizational_units WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                    WHEN 'Site' THEN PERFORM 1 FROM public.sites WHERE id = p_party_id AND status = 'Active' FOR SHARE;
                    ELSE RAISE EXCEPTION 'Unsupported polymorphic party type: %', p_party_type
                        USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END CASE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Polymorphic party %/% does not exist or is inactive', p_party_type, p_party_id
                        USING ERRCODE = '23514', CONSTRAINT = p_constraint_name;
                END IF;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.validate_issue_to_recipient()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                PERFORM public.assert_active_party_reference(NEW.recipient_type, NEW.recipient_id, 'trg_validate_recipient');
                RETURN NEW;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.validate_custody_holder()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP = 'INSERT' OR NEW.status = 'Active'
                   OR NEW.holder_type IS DISTINCT FROM OLD.holder_type
                   OR NEW.holder_id IS DISTINCT FROM OLD.holder_id THEN
                    PERFORM public.assert_active_party_reference(NEW.holder_type, NEW.holder_id, 'trg_validate_custody_holder');
                END IF;
                RETURN NEW;
            END;
            $$;
            """);
    }
}
