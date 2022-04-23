using System;
using System.Collections.Generic;

#nullable disable

namespace RESTfulAPI.Entities
{
    public partial class UserInfo
    {
        public string UserId { get; set; }
        public DateTime? JoinDate { get; set; }
        public string FullName { get; set; }
        public DateTime? Dob { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? TokenExpired { get; set; }
    }
}
