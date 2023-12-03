using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveCDKRedeem.Bean
{
    public class Data
    {
        public Profile profile { get; set; }
        public string accessToken { get; set; }
        public long expiresAt { get; set; }
        public string idToken { get; set; }
    }

    public class Profile
    {
        public String sub { get; set; }
        public Gamelinks gamelinks { get; set; }
        public String nickname { get; set; }
    }
    public class Gamelinks
    {
        public string platform_id { get; set; }
        public string provider { get; set; }
        public string gamename { get; set; }
        public string display_name { get; set; }
        public long updated_at { get; set; }
    }
}
