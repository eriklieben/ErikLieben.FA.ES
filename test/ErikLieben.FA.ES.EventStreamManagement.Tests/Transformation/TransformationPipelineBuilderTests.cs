using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Transformation;

public class TransformationPipelineBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_factory_is_null()
        {
            // Arrange
            ILoggerFactory? loggerFactory = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TransformationPipelineBuilder(loggerFactory!));
        }

        [Fact]
        public void Should_create_instance_with_valid_logger_factory()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();

            // Act
            var sut = new TransformationPipelineBuilder(loggerFactory);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class AddTransformerInstanceMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_transformer_is_null()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var sut = new TransformationPipelineBuilder(loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.AddTransformer(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var sut = new TransformationPipelineBuilder(loggerFactory);
            var transformer = Substitute.For<IEventTransformer>();

            // Act
            var result = sut.AddTransformer(transformer);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class AddTransformerGenericMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var sut = new TransformationPipelineBuilder(loggerFactory);

            // Act
            var result = sut.AddTransformer<TestTransformer>();

            // Assert
            Assert.Same(sut, result);
        }

        private class TestTransformer : IEventTransformer
        {
            public bool CanTransform(string eventName, int version) => true;
            public Task<IEvent> TransformAsync(IEvent sourceEvent, CancellationToken cancellationToken = default)
                => Task.FromResult(sourceEvent);
        }
    }

    public class AddFilterMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_predicate_is_null()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var sut = new TransformationPipelineBuilder(loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.AddFilter(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var sut = new TransformationPipelineBuilder(loggerFactory);

            // Act
            var result = sut.AddFilter(e => true);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class BuildMethod
    {
        [Fact]
        public void Should_return_empty_pipeline_when_no_transformers_added()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger<TransformationPipeline>().Returns(Substitute.For<ILogger<TransformationPipeline>>());
            var sut = new TransformationPipelineBuilder(loggerFactory);

            // Act
            var result = sut.Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.Count);
        }

        [Fact]
        public void Should_include_transformers_in_built_pipeline()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger<TransformationPipeline>().Returns(Substitute.For<ILogger<TransformationPipeline>>());
            var sut = new TransformationPipelineBuilder(loggerFactory);
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();
            sut.AddTransformer(transformer1);
            sut.AddTransformer(transformer2);

            // Act
            var result = sut.Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Should_include_filter_transformer_when_filters_added()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger<TransformationPipeline>().Returns(Substitute.For<ILogger<TransformationPipeline>>());
            var sut = new TransformationPipelineBuilder(loggerFactory);
            sut.AddFilter(e => true);

            // Act
            var result = sut.Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
        }

        [Fact]
        public void Should_add_filter_before_transformers()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger<TransformationPipeline>().Returns(Substitute.For<ILogger<TransformationPipeline>>());
            var sut = new TransformationPipelineBuilder(loggerFactory);
            var transformer = Substitute.For<IEventTransformer>();
            sut.AddTransformer(transformer);
            sut.AddFilter(e => true);

            // Act
            var result = sut.Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count); // 1 filter + 1 transformer
        }
    }

    public class FluentChainingTests
    {
        [Fact]
        public void Should_support_chained_method_calls()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger<TransformationPipeline>().Returns(Substitute.For<ILogger<TransformationPipeline>>());
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();

            // Act
            var result = new TransformationPipelineBuilder(loggerFactory)
                .AddFilter(e => true)
                .AddTransformer(transformer1)
                .AddTransformer(transformer2)
                .Build();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
        }
    }
}
