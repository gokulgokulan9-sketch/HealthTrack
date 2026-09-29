using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models;

public partial class WorkoutLog
{
    [Key]
    public int WorkoutLogId { get; set; }

    public int ClientId { get; set; }

    public int WorkoutPlanId { get; set; }

    public DateOnly WorkoutDate { get; set; }

    [StringLength(150)]
    public string ExerciseName { get; set; } = null!;

    public int? Duration { get; set; }

    public int? CaloriesBurned { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [ForeignKey("ClientId")]
    [InverseProperty("WorkoutLogs")]
    public virtual User Client { get; set; } = null!;

    [ForeignKey("WorkoutPlanId")]
    [InverseProperty("WorkoutLogs")]
    public virtual WorkoutPlan WorkoutPlan { get; set; } = null!;
}
