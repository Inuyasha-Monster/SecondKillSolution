using System;

namespace Kill_1.Data.Model
{
    public class Order
    {
        public int Id { get; set; }
        public int StockId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedTime { get; set; }
    }
}