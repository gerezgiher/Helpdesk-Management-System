using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpdeskSystem.Migrations
{
    /// <inheritdoc />
    public partial class ActivityStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
     name: "AvailabilityStatus",
     table: "AspNetUsers",
     type: "int",
     nullable: false,
     defaultValue: 0); // Active by default
        }
    }
}

