using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models;

public class DietMeal
{
    [Key]
    public int DietMealId { get; set; }

    public int DietPlanDayId { get; set; }

    [StringLength(30)]
    public string MealType { get; set; } = null!;

    [StringLength(200)]
    public string FoodName { get; set; } = null!;

    [StringLength(100)]
    public string? Quantity { get; set; }

    public int? Calories { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public int SortOrder { get; set; }


    // =========================================================
    // PARENT DAY
    // =========================================================

    [ForeignKey("DietPlanDayId")]
    public virtual DietPlanDay DietPlanDay { get; set; } = null!;
}