using Stl.ImmutableModel.Indexing;
using Stl.ImmutableModel.Updating;
using Xunit.Abstractions;

namespace Stl.Tests.ImmutableModel.Updating
{
    public class QueuingUpdaterTest : UpdaterTestBase
    {
        public QueuingUpdaterTest(ITestOutputHelper @out) : base(@out) { }

        protected override IUpdater<ModelRoot> CreateUpdater(IUpdatableIndex<ModelRoot> index) 
            => QueuingUpdater.New(index);
    }
}