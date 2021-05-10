
namespace MyGame
{
    internal class ChatMessage
    {
        public ChatMessage(string nickName, string message)
        {
            NickName = nickName;
            Message = message;
        }

        public string NickName { get; set; }
        public string Message { get; set; }
    }
}