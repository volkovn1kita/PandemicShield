using Grpc.Core;
using PandemicShield.Bio;
using PandemicShield.Contracts;
using PandemicShield.Contracts.Grpc;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace PandemicShield.Parser.Services
{
    public class GrpcParserService : DnaParserService.DnaParserServiceBase
    {
        // Ядро вікна і перекриття (у нуклеотидах). ORF, коротші за фланг, ніколи не губляться на межі вікон.
        private static readonly int CoreSize = ReadInt("WINDOW_CORE_SIZE", 256 * 1024);
        private static readonly int FlankSize = ReadInt("WINDOW_FLANK_SIZE", 32 * 1024);

        public override async Task<DnaProcessResponse> StreamDnaForParsing(
            IAsyncStreamReader<DnaChunkRequest> requestStream,
            ServerCallContext context)
        {
            int chunksProcessed = 0;

            string rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

            ConnectionFactory factory = new ConnectionFactory() { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "dna_chunks",
                durable: false,
                autoDelete: false,
                exclusive: false);

            var windower = new SequenceWindower(CoreSize, FlankSize);
            var reader = new FastaStreamReader(windower);

            Guid runId = Guid.Empty;
            ThreatCategory category = default;

            async Task PublishReadyAsync()
            {
                foreach (SequenceWindow window in windower.TakeReady())
                {
                    var message = new DnaChunkMessage()
                    {
                        RunId = runId,
                        RecordId = window.RecordId,
                        RecordIndex = window.RecordIndex,
                        Sequence = window.Sequence,
                        WindowStart = window.WindowStart,
                        CoreStart = window.CoreStart,
                        CoreEnd = window.CoreEnd,
                        AtRecordStart = window.AtRecordStart,
                        AtRecordEnd = window.AtRecordEnd,
                        Category = category
                    };

                    var body = JsonSerializer.SerializeToUtf8Bytes(message);

                    await channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: "dna_chunks",
                        body: body);

                    await Task.Delay(2);

                    chunksProcessed++;
                }
            }

            await foreach (var request in requestStream.ReadAllAsync())
            {
                if (runId == Guid.Empty && Guid.TryParse(request.RunId, out Guid parsed))
                {
                    runId = parsed;
                }
                category = (ThreatCategory)request.CategoryId;

                // Байти з API ріжуться по 32 KB без огляду на рядки й записи; FastaStreamReader
                // збирає з них записи, а SequenceWindower — вікна з перекриттям.
                reader.Feed(request.ChunkData.Span);
                await PublishReadyAsync();
            }

            reader.Complete();
            await PublishReadyAsync();

            string summary = $"Стрімінг успішно завершено: записів {reader.RecordCount}, нуклеотидів {reader.BaseCount}";
            if (reader.InvalidSymbolCount > 0)
            {
                summary += $", нерозпізнаних символів {reader.InvalidSymbolCount} (замінено на N; можливо, це білковий FASTA)";
            }

            return new DnaProcessResponse
            {
                Success = true,
                Message = summary,
                ChunkProcessed = chunksProcessed,
                RecordCount = reader.RecordCount,
                BaseCount = reader.BaseCount,
                InvalidSymbolCount = reader.InvalidSymbolCount
            };
        }

        private static int ReadInt(string name, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0 ? value : fallback;
    }
}
