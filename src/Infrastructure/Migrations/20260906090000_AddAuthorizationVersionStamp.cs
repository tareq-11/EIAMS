using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906090000_AddAuthorizationVersionStamp")]
public sealed class AddAuthorizationVersionStamp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE public.authorization_versions (
                id smallint PRIMARY KEY,
                version bigint NOT NULL,
                updated_at_utc timestamp with time zone NOT NULL,
                CONSTRAINT ck_authorization_versions_singleton CHECK (id = 1)
            );

            INSERT INTO public.authorization_versions (id, version, updated_at_utc)
            VALUES (1, 1, CURRENT_TIMESTAMP);

            CREATE FUNCTION public.bump_authorization_version()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, public
            AS $function$
            BEGIN
                UPDATE public.authorization_versions
                SET version = version + 1,
                    updated_at_utc = CURRENT_TIMESTAMP
                WHERE id = 1;

                RETURN NULL;
            END;
            $function$;

            REVOKE ALL ON FUNCTION public.bump_authorization_version() FROM PUBLIC;

            CREATE TRIGGER trg_users_authorization_version
            AFTER INSERT OR DELETE OR UPDATE OF status ON public.users
            FOR EACH STATEMENT EXECUTE FUNCTION public.bump_authorization_version();

            CREATE TRIGGER trg_permissions_authorization_version
            AFTER INSERT OR DELETE OR UPDATE OF code ON public.permissions
            FOR EACH STATEMENT EXECUTE FUNCTION public.bump_authorization_version();

            CREATE TRIGGER trg_role_permissions_authorization_version
            AFTER INSERT OR UPDATE OR DELETE ON public.role_permissions
            FOR EACH STATEMENT EXECUTE FUNCTION public.bump_authorization_version();

            CREATE TRIGGER trg_user_role_scopes_authorization_version
            AFTER INSERT OR UPDATE OR DELETE ON public.user_role_scopes
            FOR EACH STATEMENT EXECUTE FUNCTION public.bump_authorization_version();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_user_role_scopes_authorization_version ON public.user_role_scopes;
            DROP TRIGGER IF EXISTS trg_role_permissions_authorization_version ON public.role_permissions;
            DROP TRIGGER IF EXISTS trg_permissions_authorization_version ON public.permissions;
            DROP TRIGGER IF EXISTS trg_users_authorization_version ON public.users;
            DROP FUNCTION IF EXISTS public.bump_authorization_version();
            DROP TABLE IF EXISTS public.authorization_versions;
            """);
    }
}
