
namespace MyGame
{
    public class Player
    {
        public Player(string nickName, int score)
        {
            NickName = nickName;
            Score = score;
        }

        public string NickName { get; set; }
        public int Score { get; set; }
    }
}