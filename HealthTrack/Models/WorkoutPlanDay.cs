using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models;

public class WorkoutPlanDay
{
    [Key]
    public int WorkoutPlanDayId { get; set; }

    public int WorkoutPlanId { get; set; }

    public int DayNumber { get; set; }

    [StringLength(30)]
    public string DayName { get; set; } = null!;

    [StringLength(500)]
    public string? Notes { get; set; }


    // =========================================================
    // PARENT WORKOUT PLAN
    // =========================================================

    [ForeignKey("WorkoutPlanId")]
    public virtual WorkoutPlan WorkoutPlan { get; set; } = null!;


    // =========================================================
    // EXERCISES
    // =========================================================

    public virtual ICollection<WorkoutExercise> WorkoutExercises { get; set; }
        = new List<WorkoutExercise>();
}