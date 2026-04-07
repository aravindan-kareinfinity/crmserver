using Microsoft.EntityFrameworkCore;
using CRM.Server.Models;

namespace CRM.Server.Data
{
    public class CrmDbContext : DbContext
    {
        public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<CustomerTimeline> CustomerTimelines { get; set; } = null!;
        public DbSet<Service> Services { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<InvoiceTimeline> InvoiceTimelines { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Investment> Investments { get; set; } = null!;
        public DbSet<InvestmentTimeline> InvestmentTimelines { get; set; } = null!;
        public DbSet<ImplementationAssignment> ImplementationAssignments { get; set; } = null!;
        public DbSet<ImplementationTimeline> ImplementationTimelines { get; set; } = null!;
        public DbSet<Ticket> Tickets { get; set; } = null!;
        public DbSet<TicketTimeline> TicketTimelines { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<ReferenceEntry> ReferenceEntries { get; set; } = null!;
        public DbSet<Report> Reports { get; set; } = null!;
        public DbSet<CrmFile> Files { get; set; } = null!;
        public DbSet<SchedulerEvent> SchedulerEvents { get; set; } = null!;
        public DbSet<Trademark> Trademarks { get; set; } = null!;
        public DbSet<Location> Locations { get; set; } = null!;
        public DbSet<LocationTimeline> LocationTimelines { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ticket.StatusId is a FK to reference_entries (Ticket Status).
            modelBuilder.Entity<Ticket>()
                .Property(t => t.Priority)
                .HasColumnType("ticket_priority");

            // User: login id column is user_id (not user_login_id)
            modelBuilder.Entity<User>(e =>
            {
                e.Property(u => u.UserLoginId).HasColumnName("user_id").HasMaxLength(100);
                e.Property(u => u.FirstName).HasMaxLength(255);
                e.Property(u => u.LastName).HasMaxLength(255);
                e.Property(u => u.PasswordHash).HasMaxLength(255);
            });

            modelBuilder.Entity<Customer>(e =>
            {
                e.Property(c => c.Code).HasMaxLength(100).IsRequired();
                e.HasAlternateKey(c => c.Code);

                // Store list fields as comma-separated TEXT; write NULL for empty lists.
                // Also configure a ValueComparer so EF can detect changes reliably.
                var stringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.SequenceEqual(b)),
                    v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                    v => v == null ? new List<string>() : v.ToList());

                e.Property(c => c.ContactPersons)
                    .HasConversion(
                        v => v == null || v.Count == 0 ? null : string.Join(",", v),
                        v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
                    .IsRequired(false)
                    .Metadata.SetValueComparer(stringListComparer);

                e.Property(c => c.Emails)
                    .HasConversion(
                        v => v == null || v.Count == 0 ? null : string.Join(",", v),
                        v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
                    .IsRequired(false)
                    .Metadata.SetValueComparer(stringListComparer);

                e.Property(c => c.Mobiles)
                    .HasConversion(
                        v => v == null || v.Count == 0 ? null : string.Join(",", v),
                        v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
                    .IsRequired(false)
                    .Metadata.SetValueComparer(stringListComparer);

                e.HasOne(c => c.TypeRef)
                    .WithMany()
                    .HasForeignKey(c => c.TypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CustomerTimeline>()
                .HasOne(ct => ct.Customer)
                .WithMany(c => c.Timelines)
                .HasForeignKey(ct => ct.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Services)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Service>()
                .Property(s => s.ImplementationStatus)
                .HasColumnName("implementation_status")
                .HasColumnType("implementation_status_enum")
                .HasDefaultValue(ImplementationWorkflowStatus.OPEN);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Service)
                .WithMany(s => s.Invoices)
                .HasForeignKey(i => i.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoiceTimeline>()
                .HasOne(it => it.Invoice)
                .WithMany(i => i.Timelines)
                .HasForeignKey(it => it.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Payment>(e =>
            {
                e.ToTable("payments");
                e.Property(p => p.CustomerCode).HasMaxLength(100).IsRequired();
                e.Property(p => p.Notes).HasMaxLength(500);
                e.HasOne(p => p.Invoice)
                    .WithMany(i => i.Payments)
                    .HasForeignKey(p => p.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Customer)
                    .WithMany(c => c.Payments)
                    .HasForeignKey(p => p.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Investment>()
                .HasOne(inv => inv.Customer)
                .WithMany(c => c.Investments)
                .HasForeignKey(inv => inv.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvestmentTimeline>()
                .HasOne(invt => invt.Investment)
                .WithMany(inv => inv.Timelines)
                .HasForeignKey(invt => invt.InvestmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Investment>(e =>
            {
                e.ToTable("investments");
                e.Property(x => x.ClaimNotes).HasMaxLength(500);
            });

            modelBuilder.Entity<ImplementationAssignment>()
                .HasOne(ia => ia.Service)
                .WithMany(s => s.Assignments)
                .HasForeignKey(ia => ia.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ImplementationAssignment>()
                .Property(ia => ia.UserIds)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList());

            modelBuilder.Entity<ImplementationTimeline>(e =>
            {
                e.HasOne(imt => imt.Service)
                    .WithMany(s => s.Timelines)
                    .HasForeignKey(imt => imt.ServiceId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.Property(imt => imt.WorkflowStatus)
                    .HasColumnName("status")
                    .HasColumnType("implementation_status_enum");
            });

            modelBuilder.Entity<Ticket>(e =>
            {
                e.HasOne(t => t.Customer)
                    .WithMany(c => c.Tickets)
                    .HasForeignKey(t => t.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TicketTimeline>()
                .HasOne(tt => tt.Ticket)
                .WithMany(t => t.Timelines)
                .HasForeignKey(tt => tt.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Trademark>()
                .Property(t => t.ContactPersons)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            modelBuilder.Entity<Trademark>()
                .Property(t => t.Emails)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            modelBuilder.Entity<Trademark>()
                .Property(t => t.Mobiles)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            modelBuilder.Entity<Trademark>()
                .HasOne(t => t.Customer)
                .WithMany(c => c.Trademarks)
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Trademark>()
                .HasOne(t => t.Location)
                .WithMany()
                .HasForeignKey(t => t.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Location>()
                .Property(b => b.ContactPersons)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            modelBuilder.Entity<Location>()
                .Property(b => b.Emails)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            modelBuilder.Entity<Location>()
                .Property(b => b.Mobiles)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            modelBuilder.Entity<Location>()
                .Property(b => b.CustomerCode).HasMaxLength(100);
            modelBuilder.Entity<Location>()
                .HasOne(b => b.Customer)
                .WithMany(c => c.Locations)
                .HasForeignKey(b => b.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LocationTimeline>()
                .HasOne(bt => bt.Location)
                .WithMany(b => b.Timelines)
                .HasForeignKey(bt => bt.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Role>()
                .Property(r => r.Permissions)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

            modelBuilder.Entity<Report>(e =>
            {
                e.Property(r => r.Columns)
                    .HasConversion(
                        v => string.Join(",", v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
                e.Property(r => r.Filters)
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => string.IsNullOrWhiteSpace(v)
                            ? new Dictionary<string, string>()
                            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>());
            });

            modelBuilder.Entity<CrmFile>(e =>
            {
                e.ToTable("files");
                e.Property(f => f.Content).IsRequired();
                e.Property(f => f.Attributes).HasColumnType("jsonb");
                e.Property(f => f.Version).HasDefaultValue(1);
                e.Property(f => f.Notes).HasMaxLength(255);
                e.Property(f => f.Type).HasMaxLength(100);
            });

            modelBuilder.Entity<SchedulerEvent>(e =>
            {
                e.Property(se => se.Attendees)
                    .HasConversion(
                        v => string.Join(",", v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList());
            });
        }
    }
}
