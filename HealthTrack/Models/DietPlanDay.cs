using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models;

public class DietPlanDay
{
    [Key]
    public int DietPlanDayId { get; set; }

    public int DietPlanId { get; set; }

    public int DayNumber { get; set; }

    [StringLength(30)]
    public string DayName { get; set; } = null!;

    [StringLength(500)]
    public string? Notes { get; set; }


    // =========================================================
    // PARENT DIET PLAN
    // =========================================================

    [ForeignKey("DietPlanId")]
    public virtual DietPlan DietPlan { get; set; } = null!;


    // =========================================================
    // MEALS
    // =========================================================

    public virtual ICollection<DietMeal> DietMeals { get; set; }
        = new List<DietMeal>();
}