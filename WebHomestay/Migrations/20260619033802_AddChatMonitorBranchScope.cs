using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMonitorBranchScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'admin_chat_sessions'
          AND column_name = 'branch_id'
    ) THEN
        ALTER TABLE admin_chat_sessions ADD COLUMN branch_id integer;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'admin_chat_sessions'
          AND column_name = 'has_prompted_for_branch'
    ) THEN
        ALTER TABLE admin_chat_sessions ADD COLUMN has_prompted_for_branch boolean NOT NULL DEFAULT false;
    END IF;
END $$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'admin_chat_sessions'
          AND column_name = 'branch_id'
    ) THEN
        ALTER TABLE admin_chat_sessions DROP COLUMN branch_id;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'admin_chat_sessions'
          AND column_name = 'has_prompted_for_branch'
    ) THEN
        ALTER TABLE admin_chat_sessions DROP COLUMN has_prompted_for_branch;
    END IF;
END $$;
""");
        }
    }
}
