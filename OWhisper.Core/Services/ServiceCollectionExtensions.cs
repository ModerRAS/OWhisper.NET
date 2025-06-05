using Microsoft.Extensions.DependencyInjection;
using OWhisper.Core.Services;

namespace OWhisper.Core.Services
{
    /// <summary>
    /// 服务集合扩展方法
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加文本润色相关服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddTextPolishingServices(this IServiceCollection services)
        {
            // 注册滑窗文本润色服务
            services.AddScoped<ISlidingWindowPolishingService, SlidingWindowPolishingService>();

            return services;
        }
    }
} 