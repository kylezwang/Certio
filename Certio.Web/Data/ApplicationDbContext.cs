using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Certio.Domain.Projects;
using Certio.Domain.Services;

namespace Certio.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        private readonly TenantContext _tenant;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, TenantContext tenant)
            : base(options)
        {
            _tenant = tenant;
        }

        public DbSet<Project> Projects => Set<Project>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Project>().HasIndex(p => new { p.TenantId, p.CreatedAt });
            builder.Entity<Conversation>().HasIndex(c => new { c.TenantId, c.CreatedAt });
            builder.Entity<ChatMessage>().HasIndex(m => new { m.ConversationId, m.CreatedAt });
        }

        public override int SaveChanges()
        {
            foreach (var e in ChangeTracker.Entries<Project>().Where(e => e.State == EntityState.Added))
                e.Entity.TenantId = _tenant.CurrentTenant;
            foreach (var e in ChangeTracker.Entries<Conversation>().Where(e => e.State == EntityState.Added))
                e.Entity.TenantId = _tenant.CurrentTenant;
            foreach (var e in ChangeTracker.Entries<ChatMessage>().Where(e => e.State == EntityState.Added))
                e.Entity.TenantId = _tenant.CurrentTenant;
            return base.SaveChanges();
        }
    }
}
