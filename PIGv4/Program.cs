using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Entity Framework
builder.Services.AddDbContext<PigContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Auto-create database if it doesn't exist (for new users)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PigContext>();
    
    // Ensure the data directory exists
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
    var dbPath = connStr.Replace("Data Source=", "").Trim();
    if (!Path.IsPathRooted(dbPath))
        dbPath = Path.Combine(Directory.GetCurrentDirectory(), dbPath);
    var dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
    if (dbDir != null) Directory.CreateDirectory(dbDir);
    
    db.Database.EnsureCreated();
    
    // Create view and indexes if they don't exist (wrapped in try-catch for existing DBs)
    try { db.Database.ExecuteSqlRaw(@"
        DROP VIEW IF EXISTS PieceInfo;
        CREATE VIEW PieceInfo AS
        SELECT PieceId, AudioHash, Artist, Title, Genre, Album, Year, Seconds, BPM,
               FileName, FileSize, SourceFolder, Creator, Created, Editor, Edited, IsNew,
               AlbumArtUrl, AlbumArtChecked
        FROM Piece;
    "); } catch { }
    
    var indexes = new[] {
        "CREATE INDEX IF NOT EXISTS IX_Piece_SourceFolder ON Piece(SourceFolder);",
        "CREATE INDEX IF NOT EXISTS IX_Piece_Genre ON Piece(Genre);",
        "CREATE INDEX IF NOT EXISTS IX_Piece_Artist ON Piece(Artist);",
        "CREATE INDEX IF NOT EXISTS IX_Piece_Title ON Piece(Title);",
        "CREATE INDEX IF NOT EXISTS IX_Piece_IsNew ON Piece(IsNew);",
        "CREATE INDEX IF NOT EXISTS IX_Piece_FileName ON Piece(FileName);",
        "CREATE INDEX IF NOT EXISTS IX_Piece_Artist_Title ON Piece(Artist, Title);",
        "CREATE INDEX IF NOT EXISTS IX_ListFilter_ListId ON ListFilter(ListId);"
    };
    foreach (var sql in indexes)
    {
        try { db.Database.ExecuteSqlRaw(sql); } catch { }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
