using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RESTfulAPI.Model
{
    public class JWTTokenModel
    {
        public string Access_Token { get; set; }
        public string Refresh_Token { get; set; }
    }
}
