using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RESTfulAPI.Model
{
    public class ResponseModel
    {
        public dynamic Meta { get; set; }
        public dynamic Data { get; set; }
        public dynamic Error { get; set; }
    }
}
