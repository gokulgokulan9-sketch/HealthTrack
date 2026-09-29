using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models;

public partial class HealthMetric
{
    [Key]
    public int MetricId { get; set; }

    public int ClientId { get; set; }

    public DateOnly RecordDate { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? Weight { get; set; }

    [Column("BMI", TypeName = "decimal(5, 2)")]
    public decimal? Bmi { get; set; }

    public int? CaloriesBurned { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? BodyFat { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [ForeignKey("ClientId")]
    [InverseProperty("HealthMetrics")]
    public virtual User Client { get; set; } = null!;
}
