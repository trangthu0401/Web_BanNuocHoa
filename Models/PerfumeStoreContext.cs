using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PerfumeStore.Models
{
    public partial class PerfumeStoreContext : DbContext
    {
        public PerfumeStoreContext()
        {
        }

        public PerfumeStoreContext(DbContextOptions<PerfumeStoreContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Admin> Admins { get; set; } = null!;
        public virtual DbSet<Brand> Brands { get; set; } = null!;
        public virtual DbSet<Category> Categories { get; set; } = null!;
        public virtual DbSet<Comment> Comments { get; set; } = null!;
        public virtual DbSet<Coupon> Coupons { get; set; } = null!;
        public virtual DbSet<Customer> Customers { get; set; } = null!;
        public virtual DbSet<DiscountProgram> DiscountPrograms { get; set; } = null!;
        public virtual DbSet<Fee> Fees { get; set; } = null!;
        public virtual DbSet<Liter> Liters { get; set; } = null!;
        public virtual DbSet<Membership> Memberships { get; set; } = null!;
        public virtual DbSet<Order> Orders { get; set; } = null!;
        public virtual DbSet<OrderDetail> OrderDetails { get; set; } = null!;
        public virtual DbSet<PendingRegistration> PendingRegistrations { get; set; } = null!;
        public virtual DbSet<Permission> Permissions { get; set; } = null!;
        public virtual DbSet<Product> Products { get; set; } = null!;
        public virtual DbSet<ProductImage> ProductImages { get; set; } = null!;
        public virtual DbSet<Role> Roles { get; set; } = null!;
        public virtual DbSet<ShippingAddress> ShippingAddresses { get; set; } = null!;
        public virtual DbSet<Warranty> Warranties { get; set; } = null!;
        public virtual DbSet<WarrantyClaim> WarrantyClaims { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseSqlServer("Server=DESKTOP-DPSMVOG;Database=PerfumeStore Ver.2 (1);User Id=SA;Password=@Password123;TrustServerCertificate=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Admin>(entity =>
            {
                entity.Property(e => e.BirthDate).HasColumnType("date");

                entity.Property(e => e.FullName).HasMaxLength(100);

                entity.Property(e => e.NationalId).HasMaxLength(20);

                entity.Property(e => e.RoleId).HasColumnName("RoleID");

                entity.Property(e => e.UserName).HasMaxLength(50);

                entity.HasOne(d => d.Role)
                    .WithMany(p => p.Admins)
                    .HasForeignKey(d => d.RoleId)
                    .HasConstraintName("FK__Admins__RoleID__778AC167");
            });

            modelBuilder.Entity<Brand>(entity =>
            {
                entity.Property(e => e.BrandId).HasColumnName("BrandID");

                entity.Property(e => e.BrandName).HasMaxLength(100);

                entity.Property(e => e.ImageMimeType).HasMaxLength(100);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(e => e.CategoryId).HasColumnName("CategoryID");

                entity.Property(e => e.CategoryName).HasMaxLength(50);
            });

            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(e => new { e.ProductId, e.CustomerId })
                    .HasName("PK__Comments__2E4620A6CAB8E721");

                entity.Property(e => e.ProductId).HasColumnName("ProductID");

                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

                entity.Property(e => e.CommentDate).HasColumnType("datetime");

                entity.Property(e => e.Content).HasMaxLength(200);

                entity.Property(e => e.IsPublished)
                    .IsRequired()
                    .HasDefaultValueSql("(CONVERT([bit],(1)))");

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Comments__Custom__797309D9");

                entity.HasOne(d => d.Product)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Comments__Produc__787EE5A0");
            });

            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.Property(e => e.CouponId).HasColumnName("CouponID");

                entity.Property(e => e.Code)
                    .HasMaxLength(30)
                    .IsUnicode(false)
                    .IsFixedLength();

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(10, 2)");

                entity.Property(e => e.ExpiryDate).HasColumnType("datetime");

                entity.Property(e => e.IsUsed).HasDefaultValueSql("(CONVERT([bit],(0)))");

                entity.Property(e => e.UsedDate).HasColumnType("datetime");

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.Coupons)
                    .HasForeignKey(d => d.CustomerId)
                    .HasConstraintName("FK__Coupons__Custome__1F98B2C1");
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Email)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.MembershipId).HasColumnName("MembershipID");

                entity.Property(e => e.Name).HasMaxLength(100);

                entity.Property(e => e.Phone)
                    .HasMaxLength(12)
                    .IsUnicode(false)
                    .IsFixedLength();

                entity.Property(e => e.SpinNumber).HasDefaultValueSql("((3))");

                entity.HasOne(d => d.Membership)
                    .WithMany(p => p.Customers)
                    .HasForeignKey(d => d.MembershipId)
                    .HasConstraintName("FK__Customers__Membe__7A672E12");

                entity.HasMany(d => d.Products)
                    .WithMany(p => p.Customers)
                    .UsingEntity<Dictionary<string, object>>(
                        "Favorite",
                        l => l.HasOne<Product>().WithMany().HasForeignKey("ProductId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__Favorites__Produ__2739D489"),
                        r => r.HasOne<Customer>().WithMany().HasForeignKey("CustomerId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__Favorites__Custo__2645B050"),
                        j =>
                        {
                            j.HasKey("CustomerId", "ProductId").HasName("PK__Favorite__6FEEA8D625140F36");

                            j.ToTable("Favorites");

                            j.IndexerProperty<int>("CustomerId").HasColumnName("CustomerID");

                            j.IndexerProperty<int>("ProductId").HasColumnName("ProductID");
                        });
            });

            modelBuilder.Entity<DiscountProgram>(entity =>
            {
                entity.HasKey(e => e.DiscountId)
                    .HasName("PK__Discount__E43F6DF6E8E485F9");

                entity.Property(e => e.DiscountId).HasColumnName("DiscountID");

                entity.Property(e => e.DiscountName).HasMaxLength(100);
            });

            modelBuilder.Entity<Fee>(entity =>
            {
                entity.ToTable("Fee");

                entity.Property(e => e.FeeId).ValueGeneratedNever();

                entity.Property(e => e.Description).HasMaxLength(250);

                entity.Property(e => e.Name).HasMaxLength(100);

                entity.Property(e => e.Threshold).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.Value).HasColumnType("decimal(18, 2)");
            });

            modelBuilder.Entity<Liter>(entity =>
            {
                entity.Property(e => e.LiterId).HasColumnName("LiterID");

                entity.Property(e => e.LiterDescription).HasMaxLength(100);

                entity.Property(e => e.LiterPrice).HasColumnType("money");
            });

            modelBuilder.Entity<Membership>(entity =>
            {
                entity.Property(e => e.MembershipId).HasColumnName("MembershipID");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Description).HasMaxLength(300);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("(CONVERT([bit],(1)))");

                entity.Property(e => e.MinimumSpend).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.OrderId).HasColumnName("OrderID");

                entity.Property(e => e.AddressId).HasColumnName("AddressID");

                entity.Property(e => e.CouponId).HasColumnName("CouponID");

                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

                entity.Property(e => e.OrderDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

                entity.HasOne(d => d.Address)
                    .WithMany(p => p.Orders)
                    .HasForeignKey(d => d.AddressId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Orders__AddressI__02FC7413");

                entity.HasOne(d => d.Coupon)
                    .WithMany(p => p.Orders)
                    .HasForeignKey(d => d.CouponId)
                    .HasConstraintName("FK__Orders__CouponID__02084FDA");

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.Orders)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Orders__Customer__01142BA1");
            });

            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.Property(e => e.OrderDetailId).HasColumnName("OrderDetailID");

                entity.Property(e => e.OrderId).HasColumnName("OrderID");

                entity.Property(e => e.ProductId).HasColumnName("ProductID");

                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

                entity.HasOne(d => d.Order)
                    .WithMany(p => p.OrderDetails)
                    .HasForeignKey(d => d.OrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__OrderDeta__Order__7F2BE32F");

                entity.HasOne(d => d.Product)
                    .WithMany(p => p.OrderDetails)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__OrderDeta__Produ__00200768");
            });

            modelBuilder.Entity<PendingRegistration>(entity =>
            {
                entity.ToTable("PendingRegistration");

                entity.Property(e => e.CreatedAt).HasColumnType("datetime");

                entity.Property(e => e.Email)
                    .HasMaxLength(255)
                    .HasDefaultValueSql("('')");

                entity.Property(e => e.ExpiresAt).HasColumnType("datetime");

                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .HasDefaultValueSql("('')");

                entity.Property(e => e.PasswordHash)
                    .HasMaxLength(255)
                    .HasDefaultValueSql("('')");

                entity.Property(e => e.Token)
                    .HasMaxLength(255)
                    .HasDefaultValueSql("('')");
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.Property(e => e.Action).HasMaxLength(100);

                entity.Property(e => e.Area).HasMaxLength(100);

                entity.Property(e => e.Description).HasMaxLength(250);

                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(e => e.ProductId).HasColumnName("ProductID");

                entity.Property(e => e.BaseNote).HasMaxLength(100);

                entity.Property(e => e.BrandId).HasColumnName("BrandID");

                entity.Property(e => e.Concentration)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .IsFixedLength();

                entity.Property(e => e.Craftsman).HasMaxLength(100);

                entity.Property(e => e.DescriptionNo1)
                    .HasMaxLength(500)
                    .HasColumnName("DescriptionNO1");

                entity.Property(e => e.DescriptionNo2).HasColumnName("DescriptionNO2");

                entity.Property(e => e.DiscountId).HasColumnName("DiscountID");

                entity.Property(e => e.DiscountPrice).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.HeartNote).HasMaxLength(100);

                entity.Property(e => e.Introduction).HasMaxLength(150);

                entity.Property(e => e.IsPublished)
                    .IsRequired()
                    .HasDefaultValueSql("(CONVERT([bit],(1)))");

                entity.Property(e => e.Origin).HasMaxLength(30);

                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.ProductName).HasMaxLength(100);

                entity.Property(e => e.Scent).HasMaxLength(100);

                entity.Property(e => e.Style).HasMaxLength(250);

                entity.Property(e => e.SuggestionName).HasMaxLength(50);

                entity.Property(e => e.TopNote).HasMaxLength(100);

                entity.Property(e => e.UsingOccasion).HasMaxLength(250);

                entity.HasOne(d => d.Brand)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.BrandId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Products__BrandI__04E4BC85");

                entity.HasOne(d => d.Discount)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.DiscountId)
                    .HasConstraintName("FK__Products__Discou__05D8E0BE");

                entity.HasMany(d => d.Categories)
                    .WithMany(p => p.Products)
                    .UsingEntity<Dictionary<string, object>>(
                        "EqualCategory",
                        l => l.HasOne<Category>().WithMany().HasForeignKey("CategoryId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__EqualCate__Categ__7C4F7684"),
                        r => r.HasOne<Product>().WithMany().HasForeignKey("ProductId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__EqualCate__Produ__7B5B524B"),
                        j =>
                        {
                            j.HasKey("ProductId", "CategoryId").HasName("PK__EqualCat__159C554FDAB6BACF");

                            j.ToTable("EqualCategory");

                            j.IndexerProperty<int>("ProductId").HasColumnName("ProductID");

                            j.IndexerProperty<int>("CategoryId").HasColumnName("CategoryID");
                        });

                entity.HasMany(d => d.Liters)
                    .WithMany(p => p.Products)
                    .UsingEntity<Dictionary<string, object>>(
                        "EqualLiter",
                        l => l.HasOne<Liter>().WithMany().HasForeignKey("LiterId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__EqualLite__Liter__7E37BEF6"),
                        r => r.HasOne<Product>().WithMany().HasForeignKey("ProductId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__EqualLite__Produ__7D439ABD"),
                        j =>
                        {
                            j.HasKey("ProductId", "LiterId").HasName("PK__EqualLit__17F01CA7018BA2F5");

                            j.ToTable("EqualLiter");

                            j.IndexerProperty<int>("ProductId").HasColumnName("ProductID");

                            j.IndexerProperty<int>("LiterId").HasColumnName("LiterID");
                        });
            });

            modelBuilder.Entity<ProductImage>(entity =>
            {
                entity.HasKey(e => e.ImageId)
                    .HasName("PK__ProductI__7516F4ECAEE457E7");

                entity.Property(e => e.ImageId).HasColumnName("ImageID");

                entity.Property(e => e.ImageMimeType).HasMaxLength(100);

                entity.Property(e => e.ProductId).HasColumnName("ProductID");

                entity.HasOne(d => d.Product)
                    .WithMany(p => p.ProductImages)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__ProductIm__Produ__03F0984C");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.Property(e => e.RoleId).HasColumnName("RoleID");

                entity.Property(e => e.Description).HasMaxLength(250);

                entity.Property(e => e.RoleName).HasMaxLength(50);

                entity.HasMany(d => d.Permissions)
                    .WithMany(p => p.Roles)
                    .UsingEntity<Dictionary<string, object>>(
                        "RolePermission",
                        l => l.HasOne<Permission>().WithMany().HasForeignKey("PermissionId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__RolePermi__Permi__07C12930"),
                        r => r.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK__RolePermi__RoleI__06CD04F7"),
                        j =>
                        {
                            j.HasKey("RoleId", "PermissionId").HasName("PK__RolePerm__6400A188ABF38F56");

                            j.ToTable("RolePermissions");

                            j.IndexerProperty<int>("RoleId").HasColumnName("RoleID");
                        });
            });

            modelBuilder.Entity<ShippingAddress>(entity =>
            {
                entity.HasKey(e => e.AddressId)
                    .HasName("PK__Shipping__091C2A1B057C08D0");

                entity.Property(e => e.AddressId).HasColumnName("AddressID");

                entity.Property(e => e.AddressLine).HasMaxLength(250);

                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

                entity.Property(e => e.District).HasMaxLength(50);

                entity.Property(e => e.Phone)
                    .HasMaxLength(12)
                    .IsUnicode(false)
                    .IsFixedLength();

                entity.Property(e => e.Province).HasMaxLength(50);

                entity.Property(e => e.RecipientName).HasMaxLength(100);

                entity.Property(e => e.Ward).HasMaxLength(50);

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.ShippingAddresses)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__ShippingA__Custo__08B54D69");
            });

            modelBuilder.Entity<Warranty>(entity =>
            {
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Notes).HasMaxLength(250);

                entity.Property(e => e.StartDate).HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Đang bảo hành')");

                entity.Property(e => e.WarrantyCode).HasMaxLength(50);
            });

            modelBuilder.Entity<WarrantyClaim>(entity =>
            {
                entity.Property(e => e.AdminNotes).HasMaxLength(500);

                entity.Property(e => e.ClaimCode).HasMaxLength(50);

                entity.Property(e => e.IssueDescription).HasMaxLength(250);

                entity.Property(e => e.IssueType)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Sản phẩm hỏng')");

                entity.Property(e => e.ProcessedByAdmin).HasMaxLength(100);

                entity.Property(e => e.Resolution).HasMaxLength(500);

                entity.Property(e => e.ResolutionType).HasMaxLength(20);

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Chờ xử lý')");

                entity.Property(e => e.SubmittedDate).HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.Warranty)
                    .WithMany(p => p.WarrantyClaims)
                    .HasForeignKey(d => d.WarrantyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__WarrantyC__Warra__09A971A2");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
