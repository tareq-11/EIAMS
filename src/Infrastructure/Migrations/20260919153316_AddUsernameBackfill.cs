using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 20260918224519_AddUserUsername already introduced public.users.username with the
            // email-derived backfill, the NOT NULL tightening, and the unique index. This migration
            // was merged alongside it and must therefore be idempotent: on a database that applied
            // both, the column, constraint, and index already exist and only the second backfill
            // pass has any effect.
            migrationBuilder.Sql("""
                ALTER TABLE public.users ADD COLUMN IF NOT EXISTS username character varying(100);
                """);

            // Only rows that AddUserUsername could not derive a name for are backfilled here.
            migrationBuilder.Sql("""
                UPDATE public.users
                SET username = 'legacy-' || replace(id::text, '-', '')
                WHERE username IS NULL OR btrim(username) = '';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE public.users ALTER COLUMN username SET NOT NULL;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username ON public.users (username);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS public.ix_users_username;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE public.users DROP COLUMN IF EXISTS username;
                """);
        }
    }
}
