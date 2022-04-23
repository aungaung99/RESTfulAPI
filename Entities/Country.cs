using System;
using System.Collections.Generic;

#nullable disable

namespace RESTfulAPI.Entities
{
    public partial class Country
    {
        public int CountryId { get; set; }
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
    }
}
