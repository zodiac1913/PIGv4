using Microsoft.EntityFrameworkCore;

namespace PIGv4.Models;

public class PigContext : DbContext
{
    public PigContext(DbContextOptions<PigContext> options) : base(options) { }
    
    public DbSet<Config> Config { get; set; }
    public DbSet<Piece> Piece { get; set; }
    public DbSet<PieceInfo> PieceInfo { get; set; }
    public DbSet<ListModel> List { get; set; }
    public DbSet<ListFilter> ListFilter { get; set; }
    public DbSet<MP3Genre> MP3Genre { get; set; }
    public DbSet<ImportError> ImportError { get; set; }
    public DbSet<Log> Log { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Config>().HasKey(c => c.ConfigId);
        modelBuilder.Entity<Piece>().HasKey(p => p.PieceId);
        
        // Map PieceInfo to the view (no blob, fast queries)
        modelBuilder.Entity<PieceInfo>().HasNoKey();
        modelBuilder.Entity<PieceInfo>().ToView("PieceInfo");
        modelBuilder.Entity<ListModel>().HasKey(l => l.ListId);
        modelBuilder.Entity<ListFilter>().HasKey(lf => lf.ListFilterId);
        modelBuilder.Entity<MP3Genre>().HasKey(g => g.GenreId);
        modelBuilder.Entity<ImportError>().HasKey(ie => ie.ImportErrorId);
        modelBuilder.Entity<Log>().HasKey(l => l.LogIdentifier);
        
        // Index AudioHash on Piece for fast lookups and dedup
        modelBuilder.Entity<Piece>()
            .HasIndex(p => p.AudioHash)
            .IsUnique();
        
        // Indexes on ListFilter for playlist queries
        modelBuilder.Entity<ListFilter>()
            .HasIndex(lf => lf.ListUniqueId);
        modelBuilder.Entity<ListFilter>()
            .HasIndex(lf => lf.AudioHash);
    }
}
