// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

/// <summary>
/// One entry of a character's records-console history (criminal, psionics or crew), kept between rounds.
/// Saved once at round end, the newest N per book; removed with the character.
/// </summary>
[Table("pirate_record_history_entries")]
[Index(nameof(ProfileId), nameof(Book), nameof(Time))]
public sealed class PirateRecordHistoryEntry
{
    [Key]
    public int Id { get; set; }

    [Required, ForeignKey(nameof(Profile))]
    public int ProfileId { get; set; }

    public Profile Profile { get; set; } = default!;

    /// <summary>RecordLogBook: which console's history.</summary>
    public int Book { get; set; }

    /// <summary>RecordLogKind.</summary>
    public int Kind { get; set; }

    [StringLength(128)]
    public string? Topic { get; set; }

    [StringLength(32)]
    public string? Status { get; set; }

    [Required, StringLength(1024)]
    public string Message { get; set; } = string.Empty;

    [StringLength(128)]
    public string? AuthorName { get; set; }

    [StringLength(128)]
    public string? AuthorTitle { get; set; }

    [StringLength(128)]
    public string? StationName { get; set; }

    /// <summary>Real time (UTC) the entry was written.</summary>
    public DateTime Time { get; set; }
}

public static class PirateRecordHistoryModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PirateRecordHistoryEntry>()
            .HasOne(entry => entry.Profile)
            .WithMany()
            .HasForeignKey(entry => entry.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
