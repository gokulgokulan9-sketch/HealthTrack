using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models;

public partial class DietPlan
{
    [Key]
    public int DietPlanId { get; set; }

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
    [InverseProperty("DietPlanClients")]
    public virtual User Client { get; set; } = null!;

    [InverseProperty("DietPlan")]
    public virtual ICollection<MealLog> MealLogs { get; set; }
        = new List<MealLog>();

    [ForeignKey("TrainerId")]
    [InverseProperty("DietPlanTrainers")]
    public virtual User Trainer { get; set; } = null!;


    // =========================================================
    // NEW DETAILED DIET PLAN
    // DietPlan
    //      ↓
    // DietPlanDays
    //      ↓
    // DietMeals
    // =========================================================

    public virtual ICollection<DietPlanDay> DietPlanDays { get; set; }
        = new List<DietPlanDay>();
}