using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SignalRChatMVC.Hubs
{
    public class ChatHub : Hub
    {
        // Danh sách người dùng đang online
        private static ConcurrentDictionary<string, string> Users = new();

        public override async Task OnConnectedAsync()
        {
            var userName = Context.GetHttpContext()?.Request.Query["username"];
            if (!string.IsNullOrEmpty(userName))
            {
                Users[Context.ConnectionId] = userName!;
                await Clients.All.SendAsync("UserJoined", userName);
                await Clients.All.SendAsync("UpdateUserList", Users.Values);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Users.TryRemove(Context.ConnectionId, out var user))
            {
                await Clients.All.SendAsync("UserLeft", user);
                await Clients.All.SendAsync("UpdateUserList", Users.Values);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string user, string message)
        {
            var time = DateTime.Now.ToString("HH:mm:ss");
            await Clients.All.SendAsync("ReceiveMessage", user, message, time);
        }

        public async Task SendPrivateMessage(string toUser, string fromUser, string message)
        {
            var connectionId = Users.FirstOrDefault(u => u.Value == toUser).Key;
            if (connectionId != null)
            {
                var time = DateTime.Now.ToString("HH:mm:ss");
                await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage", fromUser, message, time);
            }
        }
    }
}
