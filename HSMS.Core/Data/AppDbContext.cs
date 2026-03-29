using Microsoft.EntityFrameworkCore;
using HSMS.Core.Entities;

namespace HSMS.Core.Data;

/// <summary>
/// TPH is used for User and Requisition hierarchies: single table per hierarchy reduces JOINs and keeps
/// query patterns (filter by role/type, list all) fast; subclasses have few columns so column sparsity is acceptable.
/// TPT would add one JOIN per subclass and more tables to maintain.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Requisition> Requisitions => Set<Requisition>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<InventoryRecord> InventoryRecords => Set<InventoryRecord>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<ItemWarehouseProfile> ItemWarehouseProfiles => Set<ItemWarehouseProfile>();
    public DbSet<StockTransferOrder> StockTransferOrders => Set<StockTransferOrder>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<RequisitionLineItem> RequisitionLineItems => Set<RequisitionLineItem>();
    public DbSet<PickList> PickLists => Set<PickList>();
    public DbSet<DeliveryTask> DeliveryTasks => Set<DeliveryTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.UserId);
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.Department).HasMaxLength(100).IsRequired();
            e.Property(u => u.ContactNumber).HasMaxLength(50).IsRequired();
            e.HasDiscriminator<string>("UserType")
                .HasValue<MedicalStaff>("MedicalStaff")
                .HasValue<InventoryManager>("InventoryManager")
                .HasValue<LogisticsStaff>("LogisticsStaff");
        });

        modelBuilder.Entity<MedicalStaff>(e =>
        {
            e.Property(m => m.LicenseNumber).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<InventoryManager>(e =>
        {
            e.Property(i => i.AssignedWarehouseZone).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<LogisticsStaff>(e =>
        {
            e.Property(l => l.ActiveVehicleId).HasMaxLength(50);
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("Items");
            e.HasKey(i => i.ItemId);
            e.Property(i => i.ItemName).HasMaxLength(200).IsRequired();
            e.Property(i => i.SpecificationText).HasMaxLength(500);
            e.Property(i => i.UnitOfMeasure).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Warehouse>(e =>
        {
            e.ToTable("Warehouses");
            e.HasKey(w => w.WarehouseId);
            e.Property(w => w.Code).HasMaxLength(64).IsRequired();
            e.Property(w => w.DisplayName).HasMaxLength(200).IsRequired();
            e.HasIndex(w => w.Code).IsUnique();
        });

        modelBuilder.Entity<ItemWarehouseProfile>(e =>
        {
            e.ToTable("ItemWarehouseProfiles");
            e.HasKey(p => p.ItemWarehouseProfileId);
            e.HasOne(p => p.Item).WithMany().HasForeignKey(p => p.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Warehouse).WithMany(w => w.ItemWarehouseProfiles).HasForeignKey(p => p.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => new { p.ItemId, p.WarehouseId }).IsUnique();
        });

        modelBuilder.Entity<InventoryRecord>(e =>
        {
            e.ToTable("InventoryRecords");
            e.HasKey(r => r.RecordId);
            e.Property(r => r.BatchLotNumber).HasMaxLength(100).IsRequired();
            e.Property(r => r.LocationBin).HasMaxLength(100).IsRequired();
            e.HasOne(r => r.Item).WithMany(i => i.InventoryRecords).HasForeignKey(r => r.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Warehouse).WithMany(w => w.InventoryRecords).HasForeignKey(r => r.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => r.ItemId);
            e.HasIndex(r => new { r.WarehouseId, r.ItemId });
        });

        modelBuilder.Entity<StockTransferOrder>(e =>
        {
            e.ToTable("StockTransferOrders");
            e.HasKey(o => o.StockTransferOrderId);
            e.HasOne(o => o.SourceWarehouse).WithMany(w => w.OutboundTransfers).HasForeignKey(o => o.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.DestinationWarehouse).WithMany(w => w.InboundTransfers).HasForeignKey(o => o.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.RequestedBy).WithMany().HasForeignKey(o => o.RequestedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.CompletedBy).WithMany().HasForeignKey(o => o.CompletedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(o => o.Status);
        });

        modelBuilder.Entity<StockTransferLine>(e =>
        {
            e.ToTable("StockTransferLines");
            e.HasKey(l => l.StockTransferLineId);
            e.HasOne(l => l.StockTransferOrder).WithMany(o => o.Lines).HasForeignKey(l => l.StockTransferOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => l.StockTransferOrderId);
        });

        modelBuilder.Entity<Requisition>(e =>
        {
            e.ToTable("Requisitions");
            e.HasKey(r => r.RequisitionId);
            e.Property(r => r.DeliveryLocation).HasMaxLength(500).IsRequired();
            e.HasOne(r => r.RequestedBy).WithMany(u => u.Requisitions).HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.RequisitionLineItems).WithOne(li => li.Requisition).HasForeignKey(li => li.RequisitionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.PickLists).WithOne(p => p.Requisition).HasForeignKey(p => p.RequisitionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.DeliveryTask).WithOne(d => d.Requisition).HasForeignKey<DeliveryTask>(d => d.RequisitionId).OnDelete(DeleteBehavior.Restrict);
            e.HasDiscriminator<string>("RequisitionType")
                .HasValue<StandardRequisition>("StandardRequisition")
                .HasValue<EmergencyRequisition>("EmergencyRequisition");
            e.HasIndex(r => r.RequestDate);
            e.HasIndex(r => r.Status);
        });

        modelBuilder.Entity<StandardRequisition>(e =>
        {
            e.Property(s => s.TargetDeliveryWindow).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<EmergencyRequisition>(e =>
        {
            e.Property(em => em.JustificationCode).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<RequisitionLineItem>(e =>
        {
            e.ToTable("RequisitionLineItems");
            e.HasKey(li => li.LineItemId);
            e.HasOne(li => li.Item).WithMany(i => i.RequisitionLineItems).HasForeignKey(li => li.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(li => li.RequisitionId);
        });

        modelBuilder.Entity<PickList>(e =>
        {
            e.ToTable("PickLists");
            e.HasKey(p => p.PickListId);
            e.HasOne(p => p.GeneratedBy).WithMany(m => m.PickLists).HasForeignKey(p => p.GeneratedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryTask>(e =>
        {
            e.ToTable("DeliveryTasks");
            e.HasKey(d => d.TaskId);
            e.HasOne(d => d.AssignedTo).WithMany(l => l.DeliveryTasks).HasForeignKey(d => d.AssignedToId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
