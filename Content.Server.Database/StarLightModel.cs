// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-License-Identifier: Starlight-MIT

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public partial class Profile
{
    public string Voice { get; set; } = null!;
    public string SiliconVoice { get; set; } = null!;
    public bool HairGlowing { get; set; } = false;
    public bool FacialHairGlowing { get; set; } = false;
    public bool EyeGlowing { get; set; } = false;
    public bool Enabled { get; set; }

    public StarLightModel.CharacterInfo? CharacterInfo { get; set; }
}

public abstract partial class ServerDbContext
{
    public DbSet<StarLightModel.PlayerDataDTO> PlayerData { get; set; } = null!;
    public DbSet<StarLightModel.CharacterInfo> CharacterInfo { get; set; } = null!;
    public DbSet<StarLightModel.PendingPreference> PendingPreference { get; set; } = null!;
}

public sealed class StarLightModel : DataModelBase
{
    public override void OnModelCreating(ServerDbContext dbContext, ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StarLightProfile>(entity =>
        {
            entity.HasOne(e => e.Profile)
                .WithOne(p => p.StarLightProfile)
                .HasForeignKey<StarLightProfile>(e => e.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProfileId)
                .IsUnique();

            entity.Property(e => e.CustomSpecieName)
                .HasMaxLength(32);
        });

        modelBuilder.Entity<CharacterInfo>(entity =>
        {
            entity.HasOne(e => e.Profile)
                .WithOne(p => p.CharacterInfo)
                .HasForeignKey<CharacterInfo>(e => e.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public class StarLightProfile
    {
        public int Id { get; set; }
        public int ProfileId { get; set; }
        public virtual Profile Profile { get; set; } = null!;
        public string? CustomSpecieName { get; set; }
        public string? ForcedPrototype { get; set; }
        public List<string> CyberneticIds { get; set; } = [];
        public float Width { get; set; } = 1f;
        public float Height { get; set; } = 1f;
    }

    public class PlayerDataDTO
    {
        [Key] public Guid UserId { get; set; }
        public string? Title { get; set; }
        public string? GhostTheme { get; set; }
        public int Balance { get; set; }
    }

    [Table("sl_character_info")]
    public partial class CharacterInfo
    {
        [Key, ForeignKey("Profile")]
        public int ProfileId { get; set; }

        public virtual Profile Profile { get; set; } = null!;

        [MaxLength(4096)]
        public string PhysicalDesc { get; set; } = string.Empty;

        [MaxLength(4096)]
        public string PersonalityDesc { get; set; } = string.Empty;

        [MaxLength(4096)]
        public string PersonalNotes { get; set; } = string.Empty;

        [MaxLength(4096)]
        public string CharacterSecrets { get; set; } = string.Empty;

        [MaxLength(4096)]
        public string ExploitableInfo { get; set; } = string.Empty;

        [MaxLength(4096)]
        public string OOCNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Preference edits made by an admin that have not yet been accepted or rejected by the player they belong to.
    /// One row per player; further edits are merged into the existing row.
    /// </summary>
    [Table("sl_pending_preference")]
    public class PendingPreference
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }

        /// <summary>
        /// The last admin to modify the pending data.
        /// </summary>
        public Guid LastAdminUserId { get; set; }
        public string LastAdminName { get; set; } = string.Empty;

        /// <summary>
        /// Incremented on each edit so an accept/reject only clears the edits the player actually saw.
        /// </summary>
        public int Revision { get; set; }

        /// <summary>
        /// Whether <see cref="JobPriorities"/> holds a pending change, since an empty list is also a valid change.
        /// </summary>
        public bool HasJobPriorities { get; set; }

        public List<PendingProfile> Profiles { get; set; } = new();
        public List<PendingJobPriority> JobPriorities { get; set; } = new();
    }

    [Table("sl_pending_profile")]
    public class PendingProfile
    {
        public int Id { get; set; }
        public int PendingPreferenceId { get; set; }
        public PendingPreference PendingPreference { get; set; } = null!;
        public int Slot { get; set; }

        /// <summary>
        /// The profile serialized as a <c>HumanoidProfileExport</c> yaml document, or null if the slot is to be deleted.
        /// </summary>
        public string? Data { get; set; }
    }

    [Table("sl_pending_job_priority")]
    public class PendingJobPriority
    {
        public int Id { get; set; }
        public int PendingPreferenceId { get; set; }
        public PendingPreference PendingPreference { get; set; } = null!;
        public string JobName { get; set; } = null!;
        public DbJobPriority Priority { get; set; }
    }
}
