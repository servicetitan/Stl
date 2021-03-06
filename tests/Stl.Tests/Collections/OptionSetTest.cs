using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Stl.Collections;
using Stl.Testing;
using Xunit;

namespace Stl.Tests.Collections
{
    public class OptionSetTest
    {
        [Fact]
        public void BasicTest()
        {
            var options = new OptionSet();
            options = options.PassThroughAllSerializers();
            options.Items.Count.Should().Be(0);

            options.Set("A");
            options = options.PassThroughAllSerializers();
            options.Get<string>().Should().Be("A");
            options.GetRequiredService<string>().Should().Be("A");
            options.Items.Count.Should().Be(1);

            options.Set("B");
            options = options.PassThroughAllSerializers();
            options.Get<string>().Should().Be("B");
            options.GetRequiredService<string>().Should().Be("B");
            options.Items.Count.Should().Be(1);

            options.Remove<string>();
            options = options.PassThroughAllSerializers();
            options.TryGet<string>().Should().Be(null);
            Assert.Throws<KeyNotFoundException>(() => {
                options.Get<string>();
            });
            options.GetService<string>().Should().Be(null);
            options.Items.Count.Should().Be(0);

            options.Set("C");
            options.Clear();
            options.Items.Count.Should().Be(0);
        }
    }
}
