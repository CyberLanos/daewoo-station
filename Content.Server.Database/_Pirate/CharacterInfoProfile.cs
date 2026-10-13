// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using System.ComponentModel.DataAnnotations;

namespace Content.Server.Database;

public partial class Profile
{
    [MaxLength(4096)]
    public string PersonalityDescription { get; set; } = string.Empty;

    [MaxLength(4096)]
    public string PersonalNotes { get; set; } = string.Empty;

    [MaxLength(4096)]
    public string OOCNotes { get; set; } = string.Empty;

    [MaxLength(4096)]
    public string Secrets { get; set; } = string.Empty;

    [MaxLength(4096)]
    public string ExploitableInfo { get; set; } = string.Empty;

    // Station records background: NanoTrasen HR's file on the character, one per records console.
    [MaxLength(1024)]
    public string SecurityRecordBackground { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string CrewRecordBackground { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string PsionicsRecordBackground { get; set; } = string.Empty;
}
