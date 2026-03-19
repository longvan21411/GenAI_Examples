using ChatApp_Ollama.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp_Ollama.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<HistoryMessage> HistoryMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HistoryMessage>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.SessionId).IsRequired();
            // Store the ChatRole enum as text in the database for readability
            b.Property(x => x.Role)
                .HasConversion<string>()
                .IsRequired();
            b.Property(x => x.Content).IsRequired();
            b.Property(x => x.Timestamp).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
