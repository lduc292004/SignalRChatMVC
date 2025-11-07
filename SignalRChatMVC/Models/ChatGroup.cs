using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SignalRChatMVC.Models
{
    public class ChatGroup
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        public string? Avatar { get; set; }

        [Required]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 🟦 Danh sách thành viên
        [NotMapped]
        public List<string> Members { get; set; } = new();

        [Column("MembersJson")]
        public string MembersJson
        {
            get => JsonSerializer.Serialize(Members);
            set => Members = string.IsNullOrEmpty(value) ? new() : JsonSerializer.Deserialize<List<string>>(value)!;
        }

        // 🟩 Quản trị viên nhóm
        [NotMapped]
        public List<string> Admins { get; set; } = new();

        [Column("AdminsJson")]
        public string AdminsJson
        {
            get => JsonSerializer.Serialize(Admins);
            set => Admins = string.IsNullOrEmpty(value) ? new() : JsonSerializer.Deserialize<List<string>>(value)!;
        }

        // 🔒 Nhóm riêng tư (chỉ thành viên mới xem & nhắn tin)
        public bool IsPrivate { get; set; } = true;

        // 🔢 Mã PIN gồm 4 số để bảo vệ nhóm
        [StringLength(4, MinimumLength = 4, ErrorMessage = "Mã PIN phải gồm 4 số.")]
        public string PinCode { get; set; } = string.Empty;

        // 📎 Lưu danh sách file / ảnh / sticker trong nhóm
        [NotMapped]
        public List<string> SharedFiles { get; set; } = new();

        [Column("SharedFilesJson")]
        public string SharedFilesJson
        {
            get => JsonSerializer.Serialize(SharedFiles);
            set => SharedFiles = string.IsNullOrEmpty(value) ? new() : JsonSerializer.Deserialize<List<string>>(value)!;
        }

        [NotMapped]
        public List<string> SharedImages { get; set; } = new();

        [Column("SharedImagesJson")]
        public string SharedImagesJson
        {
            get => JsonSerializer.Serialize(SharedImages);
            set => SharedImages = string.IsNullOrEmpty(value) ? new() : JsonSerializer.Deserialize<List<string>>(value)!;
        }

        [NotMapped]
        public List<string> SharedStickers { get; set; } = new();

        [Column("SharedStickersJson")]
        public string SharedStickersJson
        {
            get => JsonSerializer.Serialize(SharedStickers);
            set => SharedStickers = string.IsNullOrEmpty(value) ? new() : JsonSerializer.Deserialize<List<string>>(value)!;
        }
    }
}
