using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models;

public partial class WorkoutPlan
{
    [Key]
    public int WorkoutPlanId { get; set; }

    public int TrainerId { get; set; }

    public int ClientId { get; set; }

    [StringLength(150)]
    public string PlanName { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }


    // =========================================================
    // EXISTING RELATIONSHIPS
    // =========================================================

    [ForeignKey("ClientId")]
    [InverseProperty("WorkoutPlanClients")]
    public virtual User Client { get; set; } = null!;

    [ForeignKey("TrainerId")]
    [InverseProperty("WorkoutPlanTrainers")]
    public virtual User Trainer { get; set; } = null!;

    [InverseProperty("WorkoutPlan")]
    public virtual ICollection<WorkoutLog> WorkoutLogs { get; set; }
        = new List<WorkoutLog>();


    // =========================================================
    // NEW DETAILED WORKOUT PLAN
    // WorkoutPlan
    //      ↓
    // WorkoutPlanDays
    //      ↓
    // WorkoutExercises
    // =========================================================

    public virtual ICollection<WorkoutPlanDay> WorkoutPlanDays { get; set; }
        = new List<WorkoutPlanDay>();
}