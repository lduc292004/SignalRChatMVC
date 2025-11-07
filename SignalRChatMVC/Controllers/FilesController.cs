using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using SignalRChatMVC.Hubs;
using System;
using System.Threading.Tasks;

public class FilesController : Controller
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IWebHostEnvironment _env;

    // Dependency Injection
    public FilesController(IHubContext<ChatHub> hubContext, IWebHostEnvironment env)
    {
        _hubContext = hubContext;
        _env = env;
    }

    [HttpPost]
    [Route("api/upload")]
    public async Task<IActionResult> Upload(IFormFile file, string senderName, bool isGroupMessage, string targetName)
    {
        if (file == null || file.Length == 0) return BadRequest(new { success = false, message = "Không có file được chọn." });

        try
        {
            // 1. Lưu file vật lý vào wwwroot/uploads
            var fileExtension = Path.GetExtension(file.FileName);
            var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");

            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 2. Chuẩn bị dữ liệu thông báo
            var fileUrl = $"/uploads/{uniqueFileName}";
            var time = DateTime.Now.ToString("HH:mm:ss");
            var fileType = file.ContentType.StartsWith("image") ? "image" : "file";

            var messageData = new
            {
                user = senderName,
                content = fileUrl,
                time = time,
                type = fileType,
                originalFileName = file.FileName
            };

            // 3. Gửi thông báo qua SignalR
            if (isGroupMessage)
            {
                await _hubContext.Clients.Group(targetName).SendAsync("ReceiveGroupMessage", targetName, messageData);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", messageData);
            }

            return Ok(new { success = true, url = fileUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Lỗi Server: {ex.Message}" });
        }
    }
}