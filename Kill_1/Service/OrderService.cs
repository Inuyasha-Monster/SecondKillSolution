using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Kill_1.Common;
using Kill_1.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using StackExchange.Redis;
using Order = Kill_1.Data.Model.Order;

namespace Kill_1.Service
{
    public class OrderService : IOrderService
    {
        private readonly KillDbContext _dbContext;
        private readonly IConnectionMultiplexer _connection;

        public OrderService(KillDbContext dbContext, IConnectionMultiplexer connection)
        {
            _dbContext = dbContext;
            _connection = connection;
        }

        public int CreateOrder(int stockId)
        {
            // 无锁模式
            //RateLimit();
            //RateLimit(60, 3);

            // 有锁模式
            //RateLimitWithLock();
            //RateLimitWithLock(10, 5);

            // redis lua 分布式限流:利用lua脚本在单线程的redis的原子操作特性
            RateLimitWithRedisLuaScript();

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


        private static readonly ConcurrentDictionary<string, int> TimeDic = new ConcurrentDictionary<string, int>();

        private static readonly ConcurrentDictionary<string, int> TimeCustomerDic =
            new ConcurrentDictionary<string, int>();

        /// <summary>
        /// 设置为1s中只能给2次请求,进程级别的限流
        /// </summary>
        private static void RateLimit()
        {
            string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 原子操作抗并发
            TimeDic.AddOrUpdate(timeStr, 1, (x, y) => ++y);

            if (TimeDic.TryGetValue(timeStr, out var num))
            {
                if (num > 2)
                {
                    throw new RateLimiteException($"1s内只允许2次请求,您的请求超出范围 key:{timeStr} value:{num}");
                }
            }
        }

        /// <summary>
        /// 在限定时间内限制指定的并发量
        /// </summary>
        /// <param name="second">秒</param>
        /// <param name="limitNum">次数</param>
        private static void RateLimit(int second, int limitNum)
        {
            // 例如10s钟之内只能允许5次请求

            var now = DateTime.Now;
            if (TimeCustomerDic.Any())
            {
                var last = TimeCustomerDic.Last();
                var timeSpan = now - DateTime.Parse(last.Key);
                if (timeSpan.Seconds > second)
                {
                    string timeStr = now.ToString("yyyy-MM-dd HH:mm:ss");
                    TimeCustomerDic.AddOrUpdate(timeStr, 1, (x, y) => ++y);
                }
                else
                {
                    TimeCustomerDic.AddOrUpdate(last.Key, 1, (x, y) => ++y);

                    if (TimeCustomerDic[last.Key] > limitNum)
                    {
                        throw new RateLimiteException(
                            $"{second}s内只允许{limitNum}次请求,您的请求超出范围 key:{last.Key} value:{TimeCustomerDic[last.Key]}");
                    }

                }
            }
            else
            {
                string timeStr = now.ToString("yyyy-MM-dd HH:mm:ss");
                TimeCustomerDic.AddOrUpdate(timeStr, 1, (x, y) => ++y);
            }
        }

        // lock
        private static readonly object Obj = new object();

        // lock锁的模式 1s2次
        private static readonly Dictionary<string, int> TimeDictionary = new Dictionary<string, int>();

        private static void RateLimitWithLock()
        {
            lock (Obj)
            {
                string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                if (TimeDictionary.TryGetValue(timeStr, out var num))
                {
                    if (num++ > 2)
                    {
                        throw new RateLimiteException($"1s内只允许2次请求,您的请求超出范围 key:{timeStr} value:{num}");
                    }

                    TimeDictionary[timeStr]++;
                }
                else
                {
                    TimeDictionary.TryAdd(timeStr, 1);
                }
            }
        }

        // lock锁的模式 {second}s{limitNum}次
        private static readonly Dictionary<string, int> TimeSpanDictionary = new Dictionary<string, int>();

        private static void RateLimitWithLock(int second, int limitNum)
        {
            lock (Obj)
            {
                var now = DateTime.Now;
                if (TimeSpanDictionary.Any())
                {
                    var last = TimeSpanDictionary.Last();
                    var timeSpan = now - DateTime.Parse(last.Key);
                    if (timeSpan.Seconds > second)
                    {
                        string timeStr = now.ToString("yyyy-MM-dd HH:mm:ss");
                        TimeSpanDictionary.TryAdd(timeStr, 1);
                    }
                    else
                    {
                        TimeSpanDictionary[last.Key]++;

                        if (TimeSpanDictionary[last.Key] > limitNum)
                        {
                            throw new RateLimiteException(
                                $"{second}s内只允许{limitNum}次请求,您的请求超出范围 key:{last.Key} value:{TimeSpanDictionary[last.Key]}");
                        }

                    }
                }
                else
                {
                    string timeStr = now.ToString("yyyy-MM-dd HH:mm:ss");
                    TimeSpanDictionary.TryAdd(timeStr, 1);
                }
            }
        }

        private void RateLimitWithRedisLuaScript()
        {
            var database = _connection.GetDatabase();
            string lua = @"
                            --lua 下标从 1 开始
                            -- 限流 key
                            local key = KEYS[1]
                            -- 限流大小
                            local limit = tonumber(ARGV[1])
                            -- 获取当前流量大小
                            local curentLimit = tonumber(redis.call('get', key) or '0')
                            if curentLimit + 1 > limit then
                                -- 达到限流大小 返回
                                return 0;
                            else
                                --没有达到阈值 value + 1
                                redis.call('INCRBY', key, 1)
                                --重置key的失效时间
                                redis.call('EXPIRE', key, 10)
                                return curentLimit + 1
                            end";
            var redisResult = database.ScriptEvaluate(lua,
                new RedisKey[] {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}, new RedisValue[] {2});
            if (redisResult.ToString() == "0")
            {
                throw new RateLimiteException("redis限流超过预期范围");
            }
        }

        private void RateLimitWithRedisLuaScriptByTimeWindow()
        {

        }

    }
}