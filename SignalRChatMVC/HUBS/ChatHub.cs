using Microsoft.AspNetCore.SignalR;
using SignalRChatMVC.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
// 1. THÊM thư viện Authorization
using Microsoft.AspNetCore.Authorization;

namespace SignalRChatMVC.Hubs
{
    // 2. THÊM [Authorize] để BẮT BUỘC ĐĂNG NHẬP khi kết nối Hub
    [Authorize]
    public class ChatHub : Hub
    {
        // 🔹 Danh sách user và nhóm
        private static readonly ConcurrentDictionary<string, string> Users = new();
        // Giả sử ChatGroup là một Model bạn đã định nghĩa
        private static readonly ConcurrentDictionary<string, ChatGroup> ChatGroups = new();

        // 🔹 Khi user kết nối
        public override async Task OnConnectedAsync()
        {
            // 💡 CẢI TIẾN: Khi [Authorize] được thêm vào, chúng ta có thể lấy Username TỪ CONTEXT
            // thay vì từ Query String, giúp bảo mật hơn.
            // Tuy nhiên, để giữ nguyên logic client đã viết, ta sẽ dùng Query String tạm thời.
            var httpContext = Context.GetHttpContext();
            var username = httpContext?.Request.Query["username"].ToString();

            if (!string.IsNullOrEmpty(username))
            {
                Users[Context.ConnectionId] = username;

                // Tự động join vào các nhóm mà user đã là thành viên
                foreach (var group in ChatGroups.Values)
                {
                    if (group.Members.Contains(username))
                        await Groups.AddToGroupAsync(Context.ConnectionId, group.Name);
                }

                await Clients.All.SendAsync("UserJoined", username);
                await Clients.All.SendAsync("UpdateUserList", Users.Values);
                await Clients.Caller.SendAsync("ReceiveGroups", ChatGroups.Values);
            }

            await base.OnConnectedAsync();
        }

        // ... (Giữ nguyên toàn bộ các Action khác: OnDisconnectedAsync, SendMessage, CreateGroup, JoinGroup, LeaveGroup, SendGroupMessage, SendGroupFile, SendPublicFile, GetGroups)

        // 🔹 Khi user ngắt kết nối
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Users.TryRemove(Context.ConnectionId, out var username))
            {
                await Clients.All.SendAsync("UserLeft", username);
                await Clients.All.SendAsync("UpdateUserList", Users.Values);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // 🔹 Gửi tin nhắn công khai (Chat chung) - ĐÃ SỬA LỖI
        public async Task SendMessage(string user, string message)
        {
            var msgObj = new
            {
                sender = user, // THÊM sender
                content = message,
                type = "text",
                time = DateTime.Now.ToString("HH:mm:ss")
            };

            // Gửi trực tiếp object, SignalR sẽ tự động serialize
            await Clients.All.SendAsync("ReceiveMessage", msgObj);
        }

        // 🔹 Tin nhắn riêng tư - Tối ưu hóa gửi object
        public async Task SendPrivateMessage(string toUser, string fromUser, string message)
        {
            // Tìm ConnectionId của người nhận (toUser)
            var targetConn = Users.FirstOrDefault(u => u.Value == toUser).Key;

            if (!string.IsNullOrEmpty(targetConn))
            {
                var msgObj = new
                {
                    sender = fromUser,
                    content = message,
                    type = "text",
                    time = DateTime.Now.ToString("HH:mm:ss")
                };

                // Gửi cho người nhận
                await Clients.Client(targetConn).SendAsync("ReceivePrivateMessage", msgObj);
                // Gửi lại cho người gửi (để hiển thị trên giao diện của người gửi)
                await Clients.Caller.SendAsync("ReceivePrivateMessage", msgObj);
            }
        }

        // 🔹 Tạo nhóm mới (có mã PIN & thêm thành viên) - GIỮ NGUYÊN
        public async Task CreateGroup(string groupName, string description, string createdBy, string? avatar, List<string>? members, bool isPrivate, string pinCode)
        {
            if (ChatGroups.ContainsKey(groupName))
            {
                await Clients.Caller.SendAsync("GroupError", "Tên nhóm đã tồn tại!");
                return;
            }

            if (isPrivate && (string.IsNullOrWhiteSpace(pinCode) || pinCode.Length != 4 || !pinCode.All(char.IsDigit)))
            {
                await Clients.Caller.SendAsync("GroupError", "Mã PIN phải gồm 4 số!");
                return;
            }

            var group = new ChatGroup
            {
                Name = groupName,
                Description = description,
                Avatar = avatar ?? "/images/group-default.png",
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now,
                IsPrivate = isPrivate,
                PinCode = pinCode,
                Members = new List<string> { createdBy },
                Admins = new List<string> { createdBy }
            };

            // Thêm thành viên được chọn
            if (members != null)
            {
                foreach (var m in members)
                {
                    if (!group.Members.Contains(m))
                        group.Members.Add(m);
                }
            }

            if (ChatGroups.TryAdd(groupName, group))
            {
                // Thêm người tạo vào group SignalR
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                // Thêm các thành viên khác vào group nếu đang online
                foreach (var m in group.Members)
                {
                    var conn = Users.FirstOrDefault(u => u.Value == m).Key;
                    if (!string.IsNullOrEmpty(conn))
                        await Groups.AddToGroupAsync(conn, groupName);
                }

                // Gửi thông báo đến mọi người
                await Clients.All.SendAsync("GroupCreated", group);
            }
        }

        // 🔹 Tham gia nhóm có mã PIN - GIỮ NGUYÊN
        public async Task JoinGroup(string groupName, string username, string? pinInput = null)
        {
            if (!ChatGroups.TryGetValue(groupName, out var group))
            {
                await Clients.Caller.SendAsync("JoinFailed", "Nhóm không tồn tại.");
                return;
            }

            if (group.IsPrivate && group.PinCode != pinInput)
            {
                await Clients.Caller.SendAsync("JoinFailed", "Mã PIN không đúng.");
                return;
            }

            if (!group.Members.Contains(username))
                group.Members.Add(username);

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            // Cập nhật Group cho tất cả thành viên trong nhóm
            await Clients.Group(groupName).SendAsync("UserJoinedGroup", username, groupName);
            await Clients.Caller.SendAsync("JoinedGroup", group);
        }

        // 🔹 Rời nhóm - GIỮ NGUYÊN
        public async Task LeaveGroup(string groupName, string username)
        {
            if (!ChatGroups.TryGetValue(groupName, out var group)) return;

            group.Members.Remove(username);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("UserLeftGroup", username, groupName);
        }

        // 🔹 Gửi tin nhắn nhóm (text hoặc JSON) - ĐÃ SỬA LỖI
        public async Task SendGroupMessage(string groupName, string user, string message)
        {
            if (!ChatGroups.TryGetValue(groupName, out var group)) return;

            if (!group.Members.Contains(user))
            {
                await Clients.Caller.SendAsync("PermissionDenied", "Bạn không thuộc nhóm này!");
                return;
            }

            var msgObj = new
            {
                sender = user,
                group = groupName, // THÊM groupName
                content = message,
                type = "text",
                time = DateTime.Now.ToString("HH:mm:ss")
            };

            // Gửi trực tiếp object, SignalR sẽ tự động serialize
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", msgObj);
        }

        // 🔹 Gửi file hoặc ảnh (base64 hoặc URL) - GIỮ NGUYÊN
        // Cần kiểm tra lại logic upload Base64 ở Client và giới hạn kích thước!
        public async Task SendGroupFile(string groupName, string user, string fileName, string base64Data)
        {
            if (!ChatGroups.TryGetValue(groupName, out var group))
            {
                await Clients.Caller.SendAsync("PermissionDenied", "Nhóm không tồn tại.");
                return;
            }

            if (!group.Members.Contains(user))
            {
                await Clients.Caller.SendAsync("PermissionDenied", "Bạn không thuộc nhóm này.");
                return;
            }

            try
            {
                // Logic lưu file tại đây (rất nhạy cảm với kích thước file)
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", groupName);
                Directory.CreateDirectory(uploadDir);

                var filePath = Path.Combine(uploadDir, fileName);
                // Bạn có thể cần loại bỏ tiền tố Base64 như "data:image/png;base64," trước khi Convert.FromBase64String
                var cleanBase64Data = base64Data.Contains(',') ? base64Data.Substring(base64Data.IndexOf(',') + 1) : base64Data;
                await File.WriteAllBytesAsync(filePath, Convert.FromBase64String(cleanBase64Data));

                var fileUrl = $"/uploads/{groupName}/{fileName}";
                var ext = Path.GetExtension(fileName).ToLower();
                var fileType = (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext)) ? "image" : "file";

                var msgObj = new
                {
                    sender = user, // THÊM sender
                    group = groupName, // THÊM groupName
                    content = fileUrl,
                    type = fileType,
                    fileName = fileName, // THÊM fileName để hiển thị tên file
                    time = DateTime.Now.ToString("HH:mm:ss")
                };

                await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", msgObj);
            }
            catch (Exception ex)
            {
                // Log lỗi chi tiết hơn ở Server Console
                Console.WriteLine($"[ERROR SendGroupFile]: {ex.Message}");
                await Clients.Caller.SendAsync("UploadFailed", $"Lỗi khi gửi file: {ex.Message}");
            }
        }

        // 🔹 Gửi file hoặc ảnh ở Chat chung - GIỮ NGUYÊN
        public async Task SendPublicFile(string user, string fileName, string base64Data)
        {
            try
            {
                // Logic lưu file tại đây
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "public");
                Directory.CreateDirectory(uploadDir);

                var filePath = Path.Combine(uploadDir, fileName);
                // Bạn có thể cần loại bỏ tiền tố Base64
                var cleanBase64Data = base64Data.Contains(',') ? base64Data.Substring(base64Data.IndexOf(',') + 1) : base64Data;
                await File.WriteAllBytesAsync(filePath, Convert.FromBase64String(cleanBase64Data));

                var fileUrl = $"/uploads/public/{fileName}";
                var ext = Path.GetExtension(fileName).ToLower();
                var fileType = (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext)) ? "image" : "file";

                var msgObj = new
                {
                    sender = user, // THÊM sender
                    content = fileUrl,
                    type = fileType,
                    fileName = fileName, // THÊM fileName
                    time = DateTime.Now.ToString("HH:mm:ss")
                };

                await Clients.All.SendAsync("ReceiveMessage", msgObj);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR SendPublicFile]: {ex.Message}");
                await Clients.Caller.SendAsync("UploadFailed", $"Lỗi upload: {ex.Message}");
            }
        }

        // 🔹 Lấy danh sách nhóm
        public async Task GetGroups() =>
            await Clients.Caller.SendAsync("ReceiveGroups", ChatGroups.Values);
    }
}