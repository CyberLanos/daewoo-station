// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class PirateRecordBackgroundEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "record_backgrounds",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            // The old single text per console becomes the first entry of that console; empty title and
            // author show the defaults ("Особова справа", "Відділ кадрів NanoTrasen").
            migrationBuilder.Sql("UPDATE profile SET record_backgrounds = (record_backgrounds::jsonb || jsonb_build_array(jsonb_build_object('Book', 0, 'Title', '', 'Author', '', 'Body', security_record_background)))::text WHERE security_record_background <> '';");
            migrationBuilder.Sql("UPDATE profile SET record_backgrounds = (record_backgrounds::jsonb || jsonb_build_array(jsonb_build_object('Book', 2, 'Title', '', 'Author', '', 'Body', crew_record_background)))::text WHERE crew_record_background <> '';");
            migrationBuilder.Sql("UPDATE profile SET record_backgrounds = (record_backgrounds::jsonb || jsonb_build_array(jsonb_build_object('Book', 1, 'Title', '', 'Author', '', 'Body', psionics_record_background)))::text WHERE psionics_record_background <> '';");

            migrationBuilder.DropColumn(
                name: "crew_record_background",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "psionics_record_background",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "security_record_background",
                table: "profile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "record_backgrounds",
                table: "profile");

            migrationBuilder.AddColumn<string>(
                name: "crew_record_background",
                table: "profile",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "psionics_record_background",
                table: "profile",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "security_record_background",
                table: "profile",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");
        }
    }
}
