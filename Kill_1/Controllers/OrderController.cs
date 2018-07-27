using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kill_1.Service;
using Microsoft.AspNetCore.Mvc;

namespace Kill_1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet]
        [Route("{stockId}")]
        public ActionResult CreateOrder(int stockId)
        {
            try
            {
                var orderId = _orderService.CreateOrder(stockId);
                return Ok(orderId);
            }
            catch (Exception e)
            {
                return BadRequest(e.ToString());
            }
        }
    }
}
