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
    public DbSet<PlaylistSong> PlaylistSong { get; set; }
    public DbSet<PieceLookup> PieceLookup { get; set; }
    public DbSet<MP3Genre> MP3Genre { get; set; }
    public DbSet<ImportError> ImportError { get; set; }
    public DbSet<Log> Log { get; set; }
    public DbSet<AppUser> AppUser { get; set; }
    
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
        modelBuilder.Entity<AppUser>().HasKey(u => u.AppUserId);
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        
        // Index AudioHash on Piece for fast lookups and dedup
        modelBuilder.Entity<Piece>()
            .HasIndex(p => p.AudioHash)
            .IsUnique();
        
        // Indexes on ListFilter for playlist queries
        modelBuilder.Entity<ListFilter>()
            .HasIndex(lf => lf.ListUniqueId);
        modelBuilder.Entity<ListFilter>()
            .HasIndex(lf => lf.AudioHash);
        // Composite indexes for fast playlist resolution
        modelBuilder.Entity<ListFilter>()
            .HasIndex(lf => new { lf.ListId, lf.HasTitle });
        modelBuilder.Entity<ListFilter>()
            .HasIndex(lf => new { lf.ListId, lf.HasArtist });

        // PlaylistSong cache — fast playlist-to-piece lookups
        modelBuilder.Entity<PlaylistSong>().HasKey(ps => ps.PlaylistSongId);

        // PieceLookup — lightweight mirror of Piece (no blob)
        modelBuilder.Entity<PieceLookup>().HasKey(pl => pl.PieceId);
        modelBuilder.Entity<PlaylistSong>()
            .HasIndex(ps => ps.ListId);
        modelBuilder.Entity<PlaylistSong>()
            .HasIndex(ps => ps.PieceId);
        modelBuilder.Entity<PlaylistSong>()
            .HasIndex(ps => new { ps.ListId, ps.PieceId }).IsUnique();
    }
}
