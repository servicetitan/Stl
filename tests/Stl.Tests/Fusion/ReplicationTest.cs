using System.Threading.Tasks;
using Autofac;
using FluentAssertions;
using Stl.Fusion;
using Stl.Fusion.Bridge;
using Stl.Tests.Fusion.Services;
using Xunit;
using Xunit.Abstractions;

namespace Stl.Tests.Fusion
{
    public class ReplicationTest : FusionTestBase, IAsyncLifetime
    {
        public ReplicationTest(ITestOutputHelper @out) : base(@out) { }

        [Fact]
        public async Task BasicTest()
        {
            var sp = Container.Resolve<ISimplestProvider>();
            var cp = CreateChannelPair("channel");
            Publisher.ChannelHub.Attach(cp.Channel1).Should().BeTrue();
            Replicator.ChannelHub.Attach(cp.Channel2).Should().BeTrue();

            sp.SetValue("");
            var p1 = await Computed.PublishAsync(Publisher, () => sp.GetValueAsync());
            p1.Should().NotBeNull();

            var r1 = Replicator.GetOrAdd<string>(p1!.Publisher.Id, p1.Id);
            var r1c = await r1.Computed.UpdateAsync();
            r1c.Value.Should().Be("");
            r1.Computed.Should().Be(r1c);

            sp.SetValue("1");
            r1c = await r1.NextUpdateAsync();
            r1c.Value.Should().Be("1");
            r1.Computed.Should().Be(r1c);
        }
    }
}
