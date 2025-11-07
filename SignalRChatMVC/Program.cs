using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SignalRChatMVC.Data;
using SignalRChatMVC.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ✅ 1. Kết nối SQL Server LocalDB
builder.Services.AddDbContext<ApplicationDbContext>(options =>
 options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ 2. Cấu hình Identity cho đăng ký / đăng nhập
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// ✅ 3. Cấu hình cookie đăng nhập
builder.Services.ConfigureApplicationCookie(options =>
{
    // Cấu hình này đã đảm bảo khi truy cập trang [Authorize] sẽ chuyển hướng về Login
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
});

// ✅ 4. Thêm MVC + SignalR + Session
builder.Services.AddControllersWithViews();
// 💡 SỬA ĐỔI: Tăng giới hạn kích thước nhận tin nhắn cho SignalR lên 5MB
// Điều này giúp khắc phục lỗi "Connection closed with an error" khi gửi Base64 file lớn
builder.Services.AddSignalR(options =>
{
    // 5 * 1024 * 1024 bytes = 5MB
    options.MaximumReceiveMessageSize = 5 * 1024 * 1024;
});
builder.Services.AddSession();

var app = builder.Build();

// ✅ 5. Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// 🔹 Cho phép truy cập file tĩnh (CSS, JS, uploads, ảnh…)
app.UseStaticFiles();

// 🔹 Đảm bảo thư mục "uploads" tồn tại khi app khởi chạy
var uploadDir = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
if (!Directory.Exists(uploadDir))
{
    Directory.CreateDirectory(uploadDir);
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// ✅ 6. Định tuyến MVC + SignalR
app.MapControllerRoute(
 name: "default",
// 💡 THAY ĐỔI TẠI ĐÂY: Chuyển hướng mặc định từ Home/Index sang Account/Login
  pattern: "{controller=Account}/{action=Login}/{id?}"); // Sửa từ {controller=Home}/{action=Index}

app.MapHub<ChatHub>("/chatHub");

// ✅ 7. Chạy ứng dụng
app.Run();