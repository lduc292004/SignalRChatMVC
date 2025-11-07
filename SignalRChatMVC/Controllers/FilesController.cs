using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SignalRChatMVC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public FileController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // 🟩 Upload file/ảnh vào nhóm cụ thể
        [HttpPost("upload/{groupName}")]
        public async Task<IActionResult> UploadToGroup(string groupName, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Không có file nào được chọn." });

            if (string.IsNullOrWhiteSpace(groupName))
                return BadRequest(new { error = "Thiếu tên nhóm." });

            // Giới hạn dung lượng tối đa 20MB
            if (file.Length > 20 * 1024 * 1024)
                return BadRequest(new { error = "Kích thước file vượt quá giới hạn (20MB)." });

            // Chặn định dạng nguy hiểm
            var ext = Path.GetExtension(file.FileName).ToLower();
            var blocked = new[] { ".exe", ".bat", ".cmd", ".dll", ".js", ".msi", ".sh" };
            if (blocked.Contains(ext))
                return BadRequest(new { error = "Định dạng file không được phép tải lên." });

            // Tạo đường dẫn uploads theo nhóm
            var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", groupName);
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            // Tên file duy nhất
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/{groupName}/{fileName}";
            var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext);

            return Ok(new
            {
                success = true,
                name = file.FileName,
                url = fileUrl,
                size = file.Length,
                uploadedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                type = isImage ? "image" : "file"
            });
        }

        // 🟨 Tải file từ nhóm
        [HttpGet("download/{groupName}/{fileName}")]
        public IActionResult DownloadFromGroup(string groupName, string fileName)
        {
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(fileName))
                return BadRequest();

            var filePath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", groupName, fileName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var ext = Path.GetExtension(fileName).ToLower();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, contentType, fileName);
        }

        // 🟧 Xóa file trong nhóm
        [HttpDelete("delete/{groupName}/{fileName}")]
        public IActionResult DeleteFromGroup(string groupName, string fileName)
        {
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(fileName))
                return BadRequest();

            var filePath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", groupName, fileName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            System.IO.File.Delete(filePath);
            return Ok(new { success = true, message = "Đã xóa file thành công." });
        }

        // 🟦 Danh sách file trong nhóm (tùy chọn)
        [HttpGet("list/{groupName}")]
        public IActionResult ListFiles(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return BadRequest();

            var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", groupName);
            if (!Directory.Exists(uploadsDir))
                return Ok(new List<object>());

            var files = Directory.GetFiles(uploadsDir)
                .Select(f => new
                {
                    name = Path.GetFileName(f),
                    url = $"/uploads/{groupName}/{Path.GetFileName(f)}",
                    size = new FileInfo(f).Length,
                    modifiedAt = System.IO.File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm:ss")
                });

            return Ok(files);
        }
    }
}
