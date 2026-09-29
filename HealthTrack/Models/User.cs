using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models;

[Index("Email", Name = "UQ__Users__A9D10534749BF89A", IsUnique = true)]
public partial class User
{
    [Key]
    public int UserId { get; set; }

    [StringLength(100)]
    public string FullName { get; set; } = null!;

    [StringLength(150)]
    public string Email { get; set; } = null!;

    [StringLength(255)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(20)]
    public string Role { get; set; } = null!;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(255)]
    public string? ProfileImage { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Client")]
    public virtual ICollection<Appointment> AppointmentClients { get; set; } = new List<Appointment>();

    [InverseProperty("Trainer")]
    public virtual ICollection<Appointment> AppointmentTrainers { get; set; } = new List<Appointment>();

    [InverseProperty("Client")]
    public virtual ICollection<DietPlan> DietPlanClients { get; set; } = new List<DietPlan>();

    [InverseProperty("Trainer")]
    public virtual ICollection<DietPlan> DietPlanTrainers { get; set; } = new List<DietPlan>();

    [InverseProperty("Client")]
    public virtual ICollection<HealthMetric> HealthMetrics { get; set; } = new List<HealthMetric>();

    [InverseProperty("User")]
    public virtual ICollection<HealthProfile> HealthProfiles { get; set; } = new List<HealthProfile>();

    [InverseProperty("Client")]
    public virtual ICollection<MealLog> MealLogs { get; set; } = new List<MealLog>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [InverseProperty("Client")]
    public virtual ICollection<WorkoutLog> WorkoutLogs { get; set; } = new List<WorkoutLog>();

    [InverseProperty("Client")]
    public virtual ICollection<WorkoutPlan> WorkoutPlanClients { get; set; } = new List<WorkoutPlan>();

    [InverseProperty("Trainer")]
    public virtual ICollection<WorkoutPlan> WorkoutPlanTrainers { get; set; } = new List<WorkoutPlan>();
}
