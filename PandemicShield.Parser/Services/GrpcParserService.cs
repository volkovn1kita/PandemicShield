using Grpc.Core;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Grpc;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace PandemicShield.Parser.Services
{
    public class GrpcParserService : DnaParserService.DnaParserServiceBase
    {
        public override async Task<DnaProcessResponse> StreamDnaForParsing(
            IAsyncStreamReader<DnaChunkRequest> requestStream,
            ServerCallContext context)
        {

            int chunksProcessed = 0;
            int currentPosition = 0;

            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            ConnectionFactory factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "dna_chunks",
                durable: false,
                autoDelete: false,
                exclusive: false);


            await foreach (var request in requestStream.ReadAllAsync())
            {
                string chunkData = Encoding.UTF8.GetString(request.ChunkData.ToArray());

                int categoryId = request.CategoryId;

                var message = new DnaChunkMessage()
                {
                    Sequence = chunkData,
                    Category = (ThreatCategory)request.CategoryId,
                    StartPosition = currentPosition
                };

                currentPosition += chunkData.Length;

                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "dna_chunks",
                    body: body);

                chunksProcessed++;
            }

            return new DnaProcessResponse
            {
                Success = true,
                Message = "Стрімінг успішно завершено",
                ChunkProcessed = chunksProcessed
            };

        }
    }
}
