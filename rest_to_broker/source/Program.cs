using System.Text;

namespace TTS_RestToBroker
{
    public class Message
    {
        public string MessageId { get; set;}
        public byte[] Audio { get; set;}
        public bool Success { get; set;}
    }
    public class Program
    {
        public static Dictionary<string,Message> Messages { get; set; } = new();
        public static void Main(string[] args)
        {
            ConnectToMessageBroker();

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            // builder.WebHost.UseUrls("http://rest:5072");
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            //app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }

        private static void ConnectToMessageBroker()
        {
            Console.WriteLine("Connection....");
            while (true)
            {
                try
                {
                    TTSMessageBroker.TTSMessageBroker.StartConsumer(ea =>
                    {
                        var success = (bool)ea.BasicProperties.Headers["success"];
                        var msgId = Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["message_id"]);

                        Program.Messages.Add(msgId, new Message()
                        {
                            Audio = ea.Body.ToArray(),
                            MessageId = msgId,
                            Success = success
                        });
                        return;
                    }, "tts_ready");
                    Console.WriteLine("Connected to broker");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Reconnection....");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    Thread.Sleep(3000);
                }
            }
        }
    }
}