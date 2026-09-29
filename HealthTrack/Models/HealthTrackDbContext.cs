using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models;

public partial class HealthTrackDbContext : DbContext
{
    public HealthTrackDbContext()
    {
    }

    public HealthTrackDbContext(DbContextOptions<HealthTrackDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<DietPlan> DietPlans { get; set; }

    public virtual DbSet<HealthMetric> HealthMetrics { get; set; }

    public virtual DbSet<HealthProfile> HealthProfiles { get; set; }

    public virtual DbSet<MealLog> MealLogs { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WorkoutLog> WorkoutLogs { get; set; }

    public virtual DbSet<WorkoutPlan> WorkoutPlans { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=HealthTrackDB;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__8ECDFCC21900F42C");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            entity.HasOne(d => d.Client).WithMany(p => p.AppointmentClients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Client");

            entity.HasOne(d => d.Trainer).WithMany(p => p.AppointmentTrainers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Trainer");
        });

        modelBuilder.Entity<DietPlan>(entity =>
        {
            entity.HasKey(e => e.DietPlanId).HasName("PK__DietPlan__D256E10AC8CEC53D");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Active");

            entity.HasOne(d => d.Client).WithMany(p => p.DietPlanClients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DietPlans_Client");

            entity.HasOne(d => d.Trainer).WithMany(p => p.DietPlanTrainers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DietPlans_Trainer");
        });

        modelBuilder.Entity<HealthMetric>(entity =>
        {
            entity.HasKey(e => e.MetricId).HasName("PK__HealthMe__561056A51A660F1C");

            entity.HasOne(d => d.Client).WithMany(p => p.HealthMetrics)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HealthMetrics_Client");
        });

        modelBuilder.Entity<HealthProfile>(entity =>
        {
            entity.HasKey(e => e.HealthProfileId).HasName("PK__HealthPr__73C2C2D5E83BBA52");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.User).WithMany(p => p.HealthProfiles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HealthProfiles_Users");
        });

        modelBuilder.Entity<MealLog>(entity =>
        {
            entity.HasKey(e => e.MealLogId).HasName("PK__MealLogs__0ED21C720A49092B");

            entity.HasOne(d => d.Client).WithMany(p => p.MealLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MealLogs_Client");

            entity.HasOne(d => d.DietPlan).WithMany(p => p.MealLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MealLogs_DietPlan");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E122BC41077");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C0D78DC78");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Active");
        });

        modelBuilder.Entity<WorkoutLog>(entity =>
        {
            entity.HasKey(e => e.WorkoutLogId).HasName("PK__WorkoutL__592592754CD1EB53");

            entity.Property(e => e.Status).HasDefaultValue("Completed");

            entity.HasOne(d => d.Client).WithMany(p => p.WorkoutLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkoutLogs_Client");

            entity.HasOne(d => d.WorkoutPlan).WithMany(p => p.WorkoutLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkoutLogs_Plan");
        });

        modelBuilder.Entity<WorkoutPlan>(entity =>
        {
            entity.HasKey(e => e.WorkoutPlanId).HasName("PK__WorkoutP__8C51607BF17A9396");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasDefaultValue("Active");

            entity.HasOne(d => d.Client).WithMany(p => p.WorkoutPlanClients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkoutPlans_Client");

            entity.HasOne(d => d.Trainer).WithMany(p => p.WorkoutPlanTrainers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkoutPlans_Trainer");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
