using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models;

public partial class MealLog
{
    [Key]
    public int MealLogId { get; set; }

    public int ClientId { get; set; }

    public int DietPlanId { get; set; }

    public DateOnly MealDate { get; set; }

    [StringLength(30)]
    public string MealType { get; set; } = null!;

    [StringLength(200)]
    public string FoodName { get; set; } = null!;

    public int? Calories { get; set; }

    [StringLength(100)]
    public string? Quantity { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [ForeignKey("ClientId")]
    [InverseProperty("MealLogs")]
    public virtual User Client { get; set; } = null!;

    [ForeignKey("DietPlanId")]
    [InverseProperty("MealLogs")]
    public virtual DietPlan DietPlan { get; set; } = null!;
}
