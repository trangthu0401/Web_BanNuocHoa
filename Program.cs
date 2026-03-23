using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using PerfumeStore.Services;
using PerfumeStore.Areas.Admin.Models;
using PerfumeStore.Areas.Admin.Services;
using Net.payOS;

namespace PerfumeStore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Đăng ký PerfumeStoreContext (Main)
            builder.Services.AddDbContext<PerfumeStore.Models.PerfumeStoreContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
                "Server=DESKTOP-DPSMVOG;Database=PerfumeStore Ver.2 (1);User Id=sa;Password=@Password123;TrustServerCertificate=True;"));

            // Đăng ký PerfumeStoreContext (Admin)
            builder.Services.AddDbContext<PerfumeStore.Areas.Admin.Models.PerfumeStoreContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
                "Server=DESKTOP-DPSMVOG;Database=PerfumeStore Ver.2 (1);User Id=sa;Password=@Password123;TrustServerCertificate=True;"));

            // Cấu hình EmailSettings (đọc từ appsettings.json)
            builder.Services.Configure<EmailSettings>(
                builder.Configuration.GetSection("EmailSettings"));

            // Thêm authentication cookie
            builder.Services.AddAuthentication("Cookies")
                .AddCookie("Cookies", options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                });

            // Đăng ký dịch vụ Session
            builder.Services.AddSession(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Đăng ký HttpClient cho ChatBot
            builder.Services.AddHttpClient();

            // Đăng ký IHttpContextAccessor
            builder.Services.AddHttpContextAccessor();

            // In-memory cache for OTP state
            builder.Services.AddMemoryCache();

            // Đăng ký dịch vụ DBQueryService (Main)
            builder.Services.AddScoped<Services.IDbQueryService, Services.DbQueryService>();
            
            // Đăng ký dịch vụ DBQueryService (Admin)
            builder.Services.AddScoped<DBQueryService.IDbQueryService, DBQueryService.DbQueryService>();
            
			// Đăng ký dịch vụ EmailService
			builder.Services.AddScoped<Services.IEmailService, Services.EmailService>();
			
			// Đăng ký dịch vụ OrderService
			builder.Services.AddScoped<Services.IOrderService, Services.OrderService>();
			
			// Đăng ký dịch vụ WarrantyService
			builder.Services.AddScoped<Areas.Admin.Services.IWarrantyService, Areas.Admin.Services.WarrantyService>();
			
			// Đăng ký dịch vụ PaginationService
			builder.Services.AddScoped<Areas.Admin.Services.IPaginationService, Areas.Admin.Services.PaginationService>();

            // --- ĐĂNG KÝ DESIGN PATTERNS ---
            // Đăng ký Caching Proxy Pattern (Trang chủ)
            builder.Services.AddScoped<PerfumeStore.DesignPatterns.Proxy.RealProductQueryService>();
            builder.Services.AddScoped<PerfumeStore.DesignPatterns.Proxy.IProductQueryService, PerfumeStore.DesignPatterns.Proxy.ProductCacheProxy>();

            // Đăng ký Facade Pattern cho luồng thanh toán
            builder.Services.AddScoped<PerfumeStore.DesignPatterns.Facade.ICheckoutFacade, PerfumeStore.DesignPatterns.Facade.CheckoutFacade>();

            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

            PayOS payOS = new PayOS(configuration["Environment:PAYOS_CLIENT_ID"] ?? throw new Exception("Cannot find environment"),
                                configuration["Environment:PAYOS_API_KEY"] ?? throw new Exception("Cannot find environment"),
                                configuration["Environment:PAYOS_CHECKSUM_KEY"] ?? throw new Exception("Cannot find environment"));

            // Đăng ký PayOS vào DI container
            builder.Services.AddSingleton(payOS);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession(); // Kích hoạt Session

            app.UseAuthentication();
            app.UseAuthorization();

            // Admin portal landing page
            app.MapControllerRoute(
                name: "admin-portal",
                pattern: "admin-portal",
                defaults: new { controller = "Home", action = "AdminPortal" });

            // Admin login shortcut routes
            app.MapControllerRoute(
                name: "admin-login",
                pattern: "admin",
                defaults: new { area = "Admin", controller = "AdminAuth", action = "Login" });

            app.MapControllerRoute(
                name: "admin-register",
                pattern: "admin/register",
                defaults: new { area = "Admin", controller = "AdminAuth", action = "Register" });

            app.MapControllerRoute(
                name: "admin-dashboard",
                pattern: "admin/dashboard",
                defaults: new { area = "Admin", controller = "Dashboard", action = "Index" });

            // Area routing for Admin
            app.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();

        }
    } 
}