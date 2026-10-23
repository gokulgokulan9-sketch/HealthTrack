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

    // Database column is BMI. Keep Bmi for the existing Progress Reports code.
    [Column("BMI", TypeName = "decimal(5, 2)")]
    public decimal? Bmi { get; set; }

    // Existing controller/report code also uses BMI (uppercase).
    // This alias points to the same Bmi value and is not a second DB column.
    [NotMapped]
    public decimal? BMI
    {
        get => Bmi;
        set => Bmi = value;
    }

    // HealthMetrics table contains Height in centimetres.
    [Column(TypeName = "decimal(5, 2)")]
    public decimal? Height { get; set; }

    public int? CaloriesBurned { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? BodyFat { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [ForeignKey("ClientId")]
    [InverseProperty("HealthMetrics")]
    public virtual User Client { get; set; } = null!;
}
