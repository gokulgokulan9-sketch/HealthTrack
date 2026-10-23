using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models;

public class WorkoutExercise
{
    [Key]
    public int WorkoutExerciseId { get; set; }

    public int WorkoutPlanDayId { get; set; }

    [StringLength(150)]
    public string ExerciseName { get; set; } = null!;

    public int? Sets { get; set; }

    public int? Reps { get; set; }

    public int? Duration { get; set; }

    public int? RestSeconds { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public int SortOrder { get; set; }


    // =========================================================
    // PARENT DAY
    // =========================================================

    [ForeignKey("WorkoutPlanDayId")]
    public virtual WorkoutPlanDay WorkoutPlanDay { get; set; } = null!;
}