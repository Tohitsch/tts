using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.ComponentModel;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Serialization;
using Ogg = OggVorbisEncoder.Example;

namespace TTS_RestToBroker.Controllers
{
    [ApiController]
    public class BrokerController : Controller
    {
        public BrokerController(ILogger<BrokerController> logger)
        {
            _logger = logger;
        }
        ILogger<BrokerController> _logger;
        public class Input
        {
            public string Text { get; set; }
            internal string? MessageId { get; set; }
            [DefaultValue("wav")]
            public string AudioFormat { get; set; }
            [DefaultValue("text")]
            public string? MessageType { get; set; }
        }

        public class GenerateVoiceResponse
        {
            [JsonPropertyName("results")]
            public List<VoiceResult> Results { get; set; }

            [JsonPropertyName("original_sha1")]
            public string Hash { get; set; }
        }
        public class VoiceResult
        {
            [JsonPropertyName("audio")]
            public string Audio { get; set; }
        }
        public struct GenerateVoiceRequest
        {
            public GenerateVoiceRequest()
            {
            }

            [JsonPropertyName("api_token")]
            public string ApiToken { get; set; } = "";

            [JsonPropertyName("text")]
            public string Text { get; set; } = "";

            [JsonPropertyName("speaker")]
            public string Speaker { get; set; } = "";

            [JsonPropertyName("ssml")]
            public bool SSML { get; private set; } = true;

            [JsonPropertyName("word_ts")]
            public bool WordTS { get; private set; } = false;

            [JsonPropertyName("put_accent")]
            public bool PutAccent { get; private set; } = true;

            [JsonPropertyName("put_yo")]
            public bool PutYo { get; private set; } = false;

            [JsonPropertyName("sample_rate")]
            public int SampleRate { get; private set; } = 24000;

            [JsonPropertyName("format")]
            public string Format { get; private set; } = "ogg";
        }

        private string[] audioFormats = { "wav", "pcm", "ogg" };

        [HttpPost("/tts")]
        public async Task<IActionResult> Tts([FromBody] Input input)
        {
#if DEBUG
            var start = DateTime.Now;
#endif
            if (!audioFormats.Contains(input.AudioFormat.ToLower()))
            {
                return BadRequest($"Not accepted audio format {{{audioFormats.Aggregate((s1, s2) => $"{s1}, {s2}")}}}");
            }
            if (input.MessageType == null)
                input.MessageType = "text";

            input.Text = Encoding.UTF8.GetString(Convert.FromBase64String(input.Text));
            Message message = await Task.Run(() => GetAudio(input));

            if (message == null || !message.Success)
            {
                //HttpContext.Response.StatusCode = 403;
                //await HttpContext.Response.WriteAsync("Empty response");
                return StatusCode(500);
            }
#if DEBUG
            _logger.LogInformation($"Request time - {DateTime.Now - start}. {Program.Messages.Count}");
#endif
            return File(message.Audio, MediaTypeNames.Application.Octet);
        }

        private Message GetAudio(Input input)
        {
            string audioFormat = string.Empty;
            if (input.AudioFormat == "ogg")
                audioFormat = "pcm";
            else
                audioFormat = input.AudioFormat;

            input.MessageId = Guid.NewGuid().ToString();

            Dictionary<string, object> headers = new Dictionary<string, object>()
            {
                ["message_id"] = input.MessageId,
                ["audio_format"] = audioFormat,
                ["message_type"] = input.MessageType
            };

            TTSMessageBroker.TTSMessageBroker.Publish(input.Text, "tts", headers);
            var message = GetMessage(input.MessageId);
            if (message != null && message.Success && input.AudioFormat == "ogg")
                message.Audio = PcmToOgg(message.Audio);

            return message;
        }
        [HttpPost("/tts_ss")]
        public GenerateVoiceResponse TtsForSpaceStation([FromBody] GenerateVoiceRequest input)
        {
            
            var message = GetAudio(new Input()
            {
                AudioFormat = "wav",
                MessageType = "ssml",
                Text = input.Text
            });

            if (message == null || !message.Success)
            {
                HttpContext.Response.StatusCode = 403;
                HttpContext.Response.WriteAsync("Empty response").Wait();
                return null;
            }

            string ogg = Convert.ToBase64String(message.Audio);

            return new GenerateVoiceResponse()
            {
                Results = new List<VoiceResult> { new VoiceResult() { Audio = ogg } }
            };
        }

        private byte[] PcmToOgg(byte[] pcm)
        {
            return Ogg.Encoder.PCMToOgg(pcm);
        }

        static object _q = new();
        private Message GetMessage(string messageId)
        {
            int time = 0;
            while (true)
            {
                if (Program.Messages.ContainsKey(messageId))
                {
                    var message = Program.Messages[messageId];
                    lock (_q)
                    {
                        Program.Messages.Remove(messageId);
                    }
                    return message;
                }
                else
                {
                    Thread.Sleep(10);
                    time += 10;
                    if (time > 5000)
                    {
                        return null;
                    }
                }
            }
        }
    }

}
