using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models;

public partial class Appointment
{
    [Key]
    public int AppointmentId { get; set; }

    public int ClientId { get; set; }

    public int TrainerId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public TimeOnly AppointmentTime { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("ClientId")]
    [InverseProperty("AppointmentClients")]
    public virtual User Client { get; set; } = null!;

    [ForeignKey("TrainerId")]
    [InverseProperty("AppointmentTrainers")]
    public virtual User Trainer { get; set; } = null!;
}
