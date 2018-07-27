using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kill_1.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            catch (DbUpdateConcurrencyException e)
            {
                return BadRequest($"数据库并发更新错误:{e}");
            }
            catch (ArgumentNullException e)
            {
                return BadRequest($"参数不能为空:{e}");
            }
            catch (ArgumentException e)
            {
                return BadRequest($"参数错误:{e}");
            }
            catch (Exception e)
            {
                return BadRequest($"未知异常:{e}");
            }
        }
    }
}
