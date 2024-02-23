namespace TodoListWeb.Model
{
    public class BlacklistedToken
    {
        public int Id { get; set; }
        public string Token { get; set; }
        public DateTime BlacklistedAt { get; set; }
    }
}
