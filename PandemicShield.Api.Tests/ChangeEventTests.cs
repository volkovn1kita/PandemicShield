using PandemicShield.Api.Events;
using PandemicShield.Contracts.Events;

namespace PandemicShield.Api.Tests
{
    /// <summary>Тема події визначається за шляхом запиту, що змінив дані.</summary>
    public class ChangeEventTests
    {
        [Theory]
        [InlineData("/api/analyze", ChangeTopics.Runs)]
        [InlineData("/api/threats/0a1b2c3d-0000-0000-0000-000000000000", ChangeTopics.Alerts)]
        [InlineData("/api/variant-markers", ChangeTopics.Rules)]
        [InlineData("/api/dictionary/0a1b2c3d-0000-0000-0000-000000000000", ChangeTopics.Rules)]
        [InlineData("/api/imports/ncbi", ChangeTopics.Imports)]
        [InlineData("/api/surveillance/sync", ChangeTopics.Surveillance)]
        public void Topic_follows_the_path(string path, string topic)
        {
            Assert.Equal(topic, ChangeEventsMiddleware.ForPath(path)!.Topic);
        }

        [Fact]
        public void Run_specific_paths_carry_the_run_id()
        {
            var id = Guid.NewGuid();
            Assert.Equal(new ChangeEvent(ChangeTopics.Runs, id), ChangeEventsMiddleware.ForPath($"/api/runs/{id}/nextclade"));
            Assert.Equal(new ChangeEvent(ChangeTopics.Runs), ChangeEventsMiddleware.ForPath("/api/runs"));
        }

        [Theory]
        [InlineData("/hubs/events")]
        [InlineData("/api/mutations")]
        [InlineData("/api")]
        [InlineData(null)]
        public void Other_paths_publish_nothing(string? path)
        {
            Assert.Null(ChangeEventsMiddleware.ForPath(path));
        }
    }
}
