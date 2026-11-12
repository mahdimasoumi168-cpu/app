using Microsoft.EntityFrameworkCore;
using NetYarMohajer.Server.Data;

var builder = WebApplication.CreateBuilder(args);

var projectRoot = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..")
);
var databaseDirectory = Path.Combine(projectRoot, "Database");
Directory.CreateDirectory(databaseDirectory);

var databasePath = Path.Combine(databaseDirectory, "netyar.db");
var connectionString = $"Data Source={databasePath};Cache=Shared;Default Timeout=30";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

// Initial database creation and a demo service.
// For future schema changes, migrate to EF Core Migrations before production.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Services.Any())
    {
        db.Services.Add(new ServiceItem
        {
            Title = "خدمت آزمایشی",
            Price = 0,
            IsActive = true
        });
        db.SaveChanges();
    }
}

app.MapGet("/", () => Results.Ok(new
{
    application = "نت‌یار مهاجر",
    status = "آماده"
}));

app.MapGet("/health", async (AppDbContext db) => Results.Ok(new
{
    status = "فعال",
    application = "نت‌یار مهاجر",
    database = await db.Database.CanConnectAsync() ? "متصل" : "قطع",
    time = DateTime.UtcNow
}));

app.MapGet("/api/database-check", async (AppDbContext db, CancellationToken ct) =>
{
    var connected = await db.Database.CanConnectAsync(ct);
    return Results.Ok(new
    {
        database = connected ? "متصل" : "قطع",
        application = "نت‌یار مهاجر"
    });
});

app.MapGet("/api/services", async (AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Services
        .Where(x => x.IsActive)
        .OrderBy(x => x.Id)
        .Select(x => new { x.Id, x.Title, x.Price })
        .ToListAsync(ct)));

app.MapPost("/api/services", async (ServiceInput input, AppDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.Title) || input.Price < 0)
        return Results.BadRequest(new { message = "عنوان و تعرفه معتبر وارد کنید." });

    var service = new ServiceItem
    {
        Title = input.Title.Trim(),
        Price = input.Price,
        IsActive = true
    };

    db.Services.Add(service);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/services/{service.Id}", new
    {
        service.Id, service.Title, service.Price, service.IsActive
    });
});

app.MapGet("/api/subscribers", async (string? search, AppDbContext db, CancellationToken ct) =>
{
    var query = db.Subscribers.AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(x => x.FullName.Contains(search) || x.Phone.Contains(search));
    return Results.Ok(await query.OrderByDescending(x => x.Id).Take(200).ToListAsync(ct));
});

app.MapPost("/api/requests", async (RequestCreateDto request, AppDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.FullName) ||
        string.IsNullOrWhiteSpace(request.Phone) ||
        request.ServiceId <= 0)
        return Results.BadRequest(new { message = "نام، شماره تلفن و انتخاب خدمت الزامی است." });

    var service = await db.Services.FirstOrDefaultAsync(
        x => x.Id == request.ServiceId && x.IsActive, ct);

    if (service is null)
        return Results.BadRequest(new { message = "خدمت انتخاب‌شده وجود ندارد یا غیرفعال است." });

    var phone = request.Phone.Trim();
    var subscriber = await db.Subscribers.FirstOrDefaultAsync(x => x.Phone == phone, ct);

    if (subscriber is null)
    {
        subscriber = new Subscriber
        {
            FullName = request.FullName.Trim(),
            Phone = phone,
            NationalOrIdentityCode = request.IdentityCode?.Trim() ?? "",
            CreatedAt = DateTime.UtcNow
        };
        db.Subscribers.Add(subscriber);
        await db.SaveChangesAsync(ct);
    }

    var record = new CaseRecord
    {
        TrackingCode = "NY-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
        SubscriberId = subscriber.Id,
        ServiceId = service.Id,
        Status = "ثبت اولیه",
        Description = request.Description?.Trim() ?? "",
        CreatedAt = DateTime.UtcNow
    };

    db.Cases.Add(record);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/requests/{record.Id}", new
    {
        message = "درخواست با موفقیت ثبت شد.",
        requestId = record.Id,
        trackingCode = record.TrackingCode,
        subscriberName = subscriber.FullName,
        serviceName = service.Title,
        status = record.Status
    });
});

app.MapGet("/api/requests", async (AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Cases
        .Include(x => x.Subscriber)
        .Include(x => x.Service)
        .OrderByDescending(x => x.Id)
        .Take(200)
        .Select(x => new
        {
            x.Id,
            x.TrackingCode,
            subscriberName = x.Subscriber!.FullName,
            phone = x.Subscriber!.Phone,
            serviceName = x.Service!.Title,
            x.Status,
            x.Description,
            x.CreatedAt
        })
        .ToListAsync(ct)));

app.MapGet("/api/tracking/{code}", async (string code, AppDbContext db, CancellationToken ct) =>
{
    var record = await db.Cases
        .Include(x => x.Subscriber)
        .Include(x => x.Service)
        .FirstOrDefaultAsync(x => x.TrackingCode == code.Trim().ToUpperInvariant(), ct);

    if (record is null)
        return Results.NotFound(new { message = "پرونده‌ای با این کد رهگیری پیدا نشد." });

    return Results.Ok(new
    {
        record.TrackingCode,
        subscriberName = record.Subscriber!.FullName,
        serviceName = record.Service!.Title,
        record.Status,
        record.Description,
        record.CreatedAt
    });
});

app.MapPatch("/api/requests/{id:long}/status", async (
    long id, StatusUpdateDto input, AppDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.Status))
        return Results.BadRequest(new { message = "وضعیت جدید الزامی است." });

    var record = await db.Cases.FindAsync(new object[] { id }, ct);
    if (record is null)
        return Results.NotFound(new { message = "پرونده پیدا نشد." });

    record.Status = input.Status.Trim();
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { message = "وضعیت پرونده تغییر کرد.", record.Id, record.Status });
});

app.Run();

public record RequestCreateDto(
    string FullName,
    string Phone,
    string? IdentityCode,
    int ServiceId,
    string? Description
);

public record ServiceInput(string Title, decimal Price);
public record StatusUpdateDto(string Status);
