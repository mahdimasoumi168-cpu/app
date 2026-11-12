using Microsoft.EntityFrameworkCore;

namespace NetYarMohajer.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ServiceItem> Services => Set<ServiceItem>();
    public DbSet<CaseRecord> Cases => Set<CaseRecord>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseRecord>()
            .HasIndex(x => x.TrackingCode).IsUnique();

        modelBuilder.Entity<CaseRecord>()
            .HasOne(x => x.Subscriber).WithMany()
            .HasForeignKey(x => x.SubscriberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CaseRecord>()
            .HasOne(x => x.Service).WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StoredFile>()
            .HasOne(x => x.Case).WithMany()
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class Subscriber
{
    public long Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string NationalOrIdentityCode { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Employee
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Operator";
    public decimal HourlyRate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ServiceItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CaseRecord
{
    public long Id { get; set; }
    public string TrackingCode { get; set; } = "";
    public long SubscriberId { get; set; }
    public int ServiceId { get; set; }
    public string Status { get; set; } = "ثبت اولیه";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Subscriber? Subscriber { get; set; }
    public ServiceItem? Service { get; set; }
}

public class StoredFile
{
    public long Id { get; set; }
    public long? CaseId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public CaseRecord? Case { get; set; }
}
