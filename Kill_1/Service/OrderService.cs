using System;
using System.Linq;
using System.Net.Sockets;
using Kill_1.Data;
using Kill_1.Data.Model;

namespace Kill_1.Service
{
    public class OrderService : IOrderService
    {
        private readonly KillDbContext _dbContext;

        public OrderService(KillDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public int CreateOrder(int stockId)
        {
            var stock = _dbContext.Stocks.FirstOrDefault(x => x.Id == stockId);
            if (stock == null)
            {
                throw new ArgumentNullException(nameof(stock));
            }

            if (stock.Count <= 0)
            {
                throw new ArgumentException("库存不足");
            }

            stock.Count--;
            stock.Sale++;

            var order = new Order()
            {
                Name = stock.Name,
                StockId = stock.Id
            };

            _dbContext.Orders.Add(order);

            _dbContext.SaveChanges();

            return order.Id;
        }
    }
}