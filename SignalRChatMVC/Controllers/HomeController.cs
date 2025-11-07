using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SignalRChatMVC.Models;

namespace SignalRChatMVC.Controllers
{
    // 💡 ĐÃ SỬA: BỎ [Authorize] ở cấp Class để cho phép các Action khác (như Index) truy cập mà không cần đăng nhập
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // TRang chinh sau khi dang nhap (hoặc là trang đích nếu không dùng định tuyến mặc định)
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // ✅ THÊM [Authorize] vào Action Chat để BẮT BUỘC ĐĂNG NHẬP
        [Authorize]
        public IActionResult Chat()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}