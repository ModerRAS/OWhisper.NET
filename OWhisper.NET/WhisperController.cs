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

        [Route(HttpVerbs.Get, "/api/status")]
        public async Task<object> GetStatus()
        {
            try
            {
                var status = _whisperService.GetStatus();
                return await Task.FromResult(new { status = status, success = true });
            }
            catch (Exception ex)
            {
                return await Task.FromResult(new { error = ex.Message, success = false });
            }
        }

        [Route(HttpVerbs.Post, "/api/start")]
        public async Task<object> StartService()
        {
            try
            {
                _whisperService.Start();
                return await Task.FromResult(new { success = true });
            }
            catch (Exception ex)
            {
                return await Task.FromResult(new { error = ex.Message, success = false });
            }
        }

        [Route(HttpVerbs.Post, "/api/stop")]
        public async Task<object> StopService()
        {
            try
            {
                _whisperService.Stop();
                return await Task.FromResult(new { success = true });
            }
            catch (Exception ex)
            {
                return await Task.FromResult(new { error = ex.Message, success = false });
            }
        }

        [Route(HttpVerbs.Post, "/api/transcribe")]
        public async Task<object> Transcribe()
        {
            try
            {
                var audioData = await HttpContext.GetRequestDataAsync<byte[]>();
                // TODO: 实现语音转文字功能
                return await Task.FromResult(new { text = "语音转文字功能待实现", success = true });
            }
            catch (Exception ex)
            {
                return await Task.FromResult(new { error = ex.Message, success = false });
            }
        }
    }
}