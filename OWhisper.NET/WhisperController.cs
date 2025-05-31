using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System;
using System.Threading.Tasks;

namespace OWhisper.NET
{
    public class WhisperController : WebApiController
    {
        private readonly WhisperService _whisperService = WhisperService.Instance;

        public WhisperController()
        {
        }

        [Route(HttpVerbs.Get, "/")]
        public async Task<object> GetApiInfo()
        {
            return await Task.FromResult(new {
                endpoints = new[] {
                    "/api/status",
                    "/api/start",
                    "/api/stop",
                    "/api/transcribe"
                },
                service = "OWhisper.NET API"
            });
        }

        /// <summary>
        /// 获取服务状态
        /// </summary>
        /// <returns>
        /// 返回:
        /// {
        ///     status: "success",
        ///     data: {
        ///         serviceStatus: "Running|Stopped|Starting|Stopping"
        ///     }
        /// }
        /// </returns>
        [Route(HttpVerbs.Get, "/api/status")]
        public async Task<object> GetStatus()
        {
            try
            {
                var status = _whisperService.GetStatus();
                return new
                {
                    status = "success",
                    data = new
                    {
                        serviceStatus = status.ToString()
                    }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "error",
                    error = ex.Message
                };
            }
        }

        /// <summary>
        /// 启动语音识别服务
        /// </summary>
        /// <returns>
        /// 成功返回:
        /// {
        ///     status: "success"
        /// }
        /// 失败返回:
        /// {
        ///     status: "error",
        ///     error: "错误信息"
        /// }
        /// </returns>
        [Route(HttpVerbs.Post, "/api/start")]
        public async Task<object> StartService()
        {
            try
            {
                _whisperService.Start();
                return new
                {
                    status = "success"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "error",
                    error = ex.Message
                };
            }
        }

        /// <summary>
        /// 停止语音识别服务
        /// </summary>
        /// <returns>
        /// 成功返回:
        /// {
        ///     status: "success"
        /// }
        /// 失败返回:
        /// {
        ///     status: "error",
        ///     error: "错误信息"
        /// }
        /// </returns>
        [Route(HttpVerbs.Post, "/api/stop")]
        public async Task<object> StopService()
        {
            try
            {
                _whisperService.Stop();
                return new
                {
                    status = "success"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "error",
                    error = ex.Message
                };
            }
        }

        /// <summary>
        /// 接收音频文件并返回转写结果
        /// </summary>
        /// <returns>
        /// 成功返回:
        /// {
        ///     status: "success",
        ///     data: {
        ///         text: "转写文本内容",
        ///         processingTime: "处理耗时(秒)"
        ///     }
        /// }
        /// 失败返回:
        /// {
        ///     status: "error",
        ///     error: "错误信息"
        /// }
        /// </returns>
        [Route(HttpVerbs.Post, "/api/transcribe")]
        public async Task<object> Transcribe()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var audioData = await HttpContext.GetRequestDataAsync<byte[]>();
                
                // 音频预处理
                var processedAudio = AudioProcessor.ProcessAudio(audioData);
                
                // 语音转文字
                var transcription = await _whisperService.Transcribe(processedAudio);
                
                return new
                {
                    status = "success",
                    data = new
                    {
                        text = transcription,
                        processingTime = (DateTime.UtcNow - startTime).TotalSeconds
                    }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "error",
                    error = ex.Message
                };
            }
        }
    }
}