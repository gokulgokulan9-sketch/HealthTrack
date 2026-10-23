using System;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Models;

public partial class HealthTrackDbContext : DbContext
{
    public HealthTrackDbContext()
    {
    }

    public HealthTrackDbContext(
        DbContextOptions<HealthTrackDbContext> options)
        : base(options)
    {
    }


    // =========================================================
    // EXISTING TABLES
    // =========================================================

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<DietPlan> DietPlans { get; set; }

    public virtual DbSet<HealthMetric> HealthMetrics { get; set; }

    public virtual DbSet<HealthProfile> HealthProfiles { get; set; }

    public virtual DbSet<MealLog> MealLogs { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WorkoutLog> WorkoutLogs { get; set; }

    public virtual DbSet<WorkoutPlan> WorkoutPlans { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }


    // =========================================================
    // NEW WORKOUT TABLES
    // =========================================================

    public virtual DbSet<WorkoutPlanDay> WorkoutPlanDays { get; set; }

    public virtual DbSet<WorkoutExercise> WorkoutExercises { get; set; }


    // =========================================================
    // NEW DIET TABLES
    // =========================================================

    public virtual DbSet<DietPlanDay> DietPlanDays { get; set; }

    public virtual DbSet<DietMeal> DietMeals { get; set; }


    // =========================================================
    // DATABASE CONFIGURATION
    // =========================================================

    protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder)

        => optionsBuilder.UseSqlServer(
            "Server=localhost\\SQLEXPRESS;" +
            "Database=HealthTrackDB;" +
            "Trusted_Connection=True;" +
            "TrustServerCertificate=True;"
        );


    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {

        // =====================================================
        // APPOINTMENTS
        // =====================================================

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId)
                .HasName("PK__Appointm__8ECDFCC21900F42C");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())");

            entity.Property(e => e.Status)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Client)
                .WithMany(p => p.AppointmentClients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Client");

            entity.HasOne(d => d.Trainer)
                .WithMany(p => p.AppointmentTrainers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Trainer");
        });


        // =====================================================
        // DIET PLANS
        // =====================================================

        modelBuilder.Entity<DietPlan>(entity =>
        {
            entity.HasKey(e => e.DietPlanId)
                .HasName("PK__DietPlan__D256E10AC8CEC53D");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())");

            entity.Property(e => e.Status)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Client)
                .WithMany(p => p.DietPlanClients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DietPlans_Client");

            entity.HasOne(d => d.Trainer)
                .WithMany(p => p.DietPlanTrainers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DietPlans_Trainer");
        });


        // =====================================================
        // HEALTH METRICS
        // =====================================================

        modelBuilder.Entity<HealthMetric>(entity =>
        {
            entity.HasKey(e => e.MetricId)
                .HasName("PK__HealthMe__561056A51A660F1C");

            entity.HasOne(d => d.Client)
                .WithMany(p => p.HealthMetrics)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HealthMetrics_Client");
        });


        // =====================================================
        // HEALTH PROFILES
        // =====================================================

        modelBuilder.Entity<HealthProfile>(entity =>
        {
            entity.HasKey(e => e.HealthProfileId)
                .HasName("PK__HealthPr__73C2C2D5E83BBA52");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.User)
                .WithMany(p => p.HealthProfiles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HealthProfiles_Users");
        });


        // =====================================================
        // MEAL LOGS
        // =====================================================

        modelBuilder.Entity<MealLog>(entity =>
        {
            entity.HasKey(e => e.MealLogId)
                .HasName("PK__MealLogs__0ED21C720A49092B");

            entity.HasOne(d => d.Client)
                .WithMany(p => p.MealLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MealLogs_Client");

            entity.HasOne(d => d.DietPlan)
                .WithMany(p => p.MealLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MealLogs_DietPlan");
        });


        // =====================================================
        // NOTIFICATIONS
        // =====================================================

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId)
                .HasName("PK__Notifica__20CF2E122BC41077");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())");

            entity.Property(e => e.IsRead)
                .HasDefaultValue(false);

            entity.HasOne(d => d.User)
                .WithMany(p => p.Notifications)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_User");
        });


        // =====================================================
        // USERS
        // =====================================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId)
                .HasName("PK__Users__1788CC4C0D78DC78");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())");

            entity.Property(e => e.Status)
                .HasDefaultValue("Active");
        });


        // =====================================================
        // WORKOUT LOGS
        // =====================================================

        modelBuilder.Entity<WorkoutLog>(entity =>
        {
            entity.HasKey(e => e.WorkoutLogId)
                .HasName("PK__WorkoutL__592592754CD1EB53");

            entity.Property(e => e.Status)
                .HasDefaultValue("Completed");

            entity.HasOne(d => d.Client)
                .WithMany(p => p.WorkoutLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkoutLogs_Client");

            entity.HasOne(d => d.WorkoutPlan)
                .WithMany(p => p.WorkoutLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkoutLogs_Plan");
        });


        // =====================================================
        // WORKOUT PLANS
        // =====================================================

        modelBuilder.Entity<WorkoutPlan>(entity =>
        {
            entity.HasKey(e => e.WorkoutPlanId)
                .HasName("PK__WorkoutP__8C51607BF17A9396");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())");

            entity.Property(e => e.Status)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Client)
                .WithMany(p => p.WorkoutPlanClients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkoutPlans_Client");

            entity.HasOne(d => d.Trainer)
                .WithMany(p => p.WorkoutPlanTrainers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkoutPlans_Trainer");
        });




        // =====================================================
        // NEW: WORKOUT PLAN DAYS
        // =====================================================

        modelBuilder.Entity<WorkoutPlanDay>(entity =>
        {
            entity.HasKey(e => e.WorkoutPlanDayId)
                .HasName("PK_WorkoutPlanDays");

            entity.Property(e => e.DayName)
                .HasMaxLength(30);

            entity.Property(e => e.Notes)
                .HasMaxLength(500);

            entity.HasOne(d => d.WorkoutPlan)
                .WithMany(p => p.WorkoutPlanDays)
                .HasForeignKey(d => d.WorkoutPlanId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName(
                    "FK_WorkoutPlanDays_WorkoutPlans");
        });


        // =====================================================
        // NEW: WORKOUT EXERCISES
        // =====================================================

        modelBuilder.Entity<WorkoutExercise>(entity =>
        {
            entity.HasKey(e => e.WorkoutExerciseId)
                .HasName("PK_WorkoutExercises");

            entity.Property(e => e.ExerciseName)
                .HasMaxLength(150);

            entity.Property(e => e.Notes)
                .HasMaxLength(500);

            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0);

            entity.HasOne(d => d.WorkoutPlanDay)
                .WithMany(p => p.WorkoutExercises)
                .HasForeignKey(d => d.WorkoutPlanDayId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName(
                    "FK_WorkoutExercises_WorkoutPlanDays");
        });


        // =====================================================
        // NEW: DIET PLAN DAYS
        // =====================================================

        modelBuilder.Entity<DietPlanDay>(entity =>
        {
            entity.HasKey(e => e.DietPlanDayId)
                .HasName("PK_DietPlanDays");

            entity.Property(e => e.DayName)
                .HasMaxLength(30);

            entity.Property(e => e.Notes)
                .HasMaxLength(500);

            entity.HasOne(d => d.DietPlan)
                .WithMany(p => p.DietPlanDays)
                .HasForeignKey(d => d.DietPlanId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName(
                    "FK_DietPlanDays_DietPlans");
        });


        // =====================================================
        // NEW: DIET MEALS
        // =====================================================

        modelBuilder.Entity<DietMeal>(entity =>
        {
            entity.HasKey(e => e.DietMealId)
                .HasName("PK_DietMeals");

            entity.Property(e => e.MealType)
                .HasMaxLength(30);

            entity.Property(e => e.FoodName)
                .HasMaxLength(200);

            entity.Property(e => e.Quantity)
                .HasMaxLength(100);

            entity.Property(e => e.Notes)
                .HasMaxLength(500);

            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0);

            entity.HasOne(d => d.DietPlanDay)
                .WithMany(p => p.DietMeals)
                .HasForeignKey(d => d.DietPlanDayId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName(
                    "FK_DietMeals_DietPlanDays");
        });


        // =====================================================
        // PARTIAL CONFIGURATION
        // =====================================================

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(
        ModelBuilder modelBuilder);
}