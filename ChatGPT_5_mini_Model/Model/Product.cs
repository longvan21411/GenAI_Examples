using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatGPT_5_mini_Model.Model
{
    public class Product
    {
        public int ProductId { get; set; }
        public required string Name { get; set; }
        public string? Model { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
