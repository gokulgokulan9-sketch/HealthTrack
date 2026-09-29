using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models;

public partial class HealthProfile
{
    [Key]
    public int HealthProfileId { get; set; }

    public int UserId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? Height { get; set; }

    [StringLength(500)]
    public string? MedicalNotes { get; set; }

    [StringLength(200)]
    public string? FitnessGoal { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("HealthProfiles")]
    public virtual User User { get; set; } = null!;
}
