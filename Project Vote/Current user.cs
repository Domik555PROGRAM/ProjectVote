namespace Project_Vote.Models
{
    public static class CurrentUser
    {
        public static int UserId { get; set; }
        public static string Email { get; set; }
        public static string Name { get; set; }
        public static byte[] Photo { get; set; }

        public static bool IsLoggedIn => UserId > 0;

        public static void Clear()
        {
            UserId = 0;
            Email = null;
            Name = null;
            Photo = null;
        }
    }
}