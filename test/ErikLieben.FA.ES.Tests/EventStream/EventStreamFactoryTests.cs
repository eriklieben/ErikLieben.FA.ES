using System;
using System.Collections.Generic;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Exceptions;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.EventStream
{
    public class EventStreamFactoryTests
    {
        public class Ctor
        {
            [Fact]
            public void Should_throw_when_eventStreamFactories_is_null()
            {
                // Arrange
                IDictionary<string, IEventStreamFactory> nullFactories = null!;
                var settings = new EventStreamDefaultTypeSettings { StreamType = "default" };

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new EventStreamFactory(nullFactories, settings));
            }

            [Fact]
            public void Should_throw_when_settings_is_null()
            {
                // Arrange
                var factories = new Dictionary<string, IEventStreamFactory>();
                EventStreamDefaultTypeSettings nullSettings = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new EventStreamFactory(factories, nullSettings));
            }
        }

        public class Create
        {
            [Fact]
            public void Should_throw_when_document_is_null()
            {
                // Arrange
                var factories = new Dictionary<string, IEventStreamFactory>();
                var settings = new EventStreamDefaultTypeSettings { StreamType = "default" };
                var sut = new EventStreamFactory(factories, settings);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.Create(null!));
            }

            [Fact]
            public void Should_throw_when_stream_type_is_null()
            {
                // Arrange
                var factories = new Dictionary<string, IEventStreamFactory>();
                var settings = new EventStreamDefaultTypeSettings { StreamType = "default" };
                var sut = new EventStreamFactory(factories, settings);

                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                document.Active.StreamType = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.Create(document));
            }

            [Fact]
            public void Should_return_EventStream_from_matching_factory()
            {
                // Arrange
                var streamType = "testStream";

                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                document.Active.StreamType = streamType;
                var expectedEventStream = Substitute.For<IEventStream>();
                var matchingFactory = Substitute.For<IEventStreamFactory>();
                matchingFactory.Create(document).Returns(expectedEventStream);
                var factories = new Dictionary<string, IEventStreamFactory>
                {
                    { streamType, matchingFactory }
                };

                var settings = new EventStreamDefaultTypeSettings { StreamType = "default" };
                var sut = new EventStreamFactory(factories, settings);

                // Act
                var result = sut.Create(document);

                // Assert
                Assert.Same(expectedEventStream, result);
                matchingFactory.Received(1).Create(document);
            }


            [Fact]
            public void Should_return_EventStream_from_default_factory_when_matching_not_found()
            {
                // Arrange
                var streamType = "testStream";
                var defaultStreamType = "default";

                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                document.Active.StreamType = streamType;
                document.Active.StreamType = defaultStreamType;

                var expectedEventStream = Substitute.For<IEventStream>();
                var defaultFactory = Substitute.For<IEventStreamFactory>();
                defaultFactory.Create(document).Returns(expectedEventStream);
                var factories = new Dictionary<string, IEventStreamFactory>
                {
                    { defaultStreamType, defaultFactory }
                };
                var settings = new EventStreamDefaultTypeSettings { StreamType = defaultStreamType };
                var sut = new EventStreamFactory(factories, settings);

                // Act
                var result = sut.Create(document);

                // Assert
                Assert.Same(expectedEventStream, result);
                defaultFactory.Received(1).Create(document);
            }

            [Fact]
            public void Should_throw_when_no_matching_or_default_factory_found()
            {
                // Arrange
                const string streamType = "testStream";
                const string defaultStreamType = "default";
                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                document.Active.StreamType = streamType;
                var factories = new Dictionary<string, IEventStreamFactory>();
                var settings = new EventStreamDefaultTypeSettings { StreamType = defaultStreamType };
                var sut = new EventStreamFactory(factories, settings);

                // Act & Assert
                var exception =
                    Assert.Throws<UnableToCreateEventStreamForStreamTypeException>(() => sut.Create(document));
                Assert.Equal(
                    $"[ELFAES-CFG-0003] Unable to create EventStream of the type {streamType} or {defaultStreamType}. Is your configuration correct?",
                    exception.Message);
            }

            [Fact]
            public void Should_use_fallback_factory_when_document_stream_type_not_found()
            {
                // Arrange
                const string documentStreamType = "NotFoundType";
                const string fallbackStreamType = "FallbackType";

                var document = Substitute.For<IObjectDocument>();
                var documentActive = Substitute.For<StreamInformation>();
                documentActive.StreamType = documentStreamType;
                document.Active.Returns(documentActive);
                var mockEventStream = Substitute.For<IEventStream>();
                var fallbackFactory = Substitute.For<IEventStreamFactory>();
                fallbackFactory.Create(document).Returns(mockEventStream);
                var settings = new EventStreamDefaultTypeSettings
                {
                    StreamType = fallbackStreamType
                };
                var factories = new Dictionary<string, IEventStreamFactory>
                {
                    { fallbackStreamType, fallbackFactory }
                };

                var sut = new EventStreamFactory(factories, settings);

                // Act
                var result = sut.Create(document);

                // Assert
                Assert.Same(mockEventStream, result);
                fallbackFactory.Received(1).Create(document);
            }

            [Fact]
            public void Should_throw_when_fallback_factory_not_found()
            {
                // Arrange
                const string documentStreamType = "NotFoundType";
                const string fallbackStreamType = "AlsoNotFoundType";
                var document = Substitute.For<IObjectDocument>();
                var documentActive = Substitute.For<StreamInformation>();
                documentActive.StreamType = documentStreamType;
                document.Active.Returns(documentActive);
                var settings = new EventStreamDefaultTypeSettings
                {
                    StreamType = fallbackStreamType
                };
                var factories = new Dictionary<string, IEventStreamFactory>();
                var sut = new EventStreamFactory(factories, settings);

                // Act & Assert
                var exception =
                    Assert.Throws<UnableToCreateEventStreamForStreamTypeException>(() => sut.Create(document));

                Assert.Equal(
                    $"[ELFAES-CFG-0003] Unable to create EventStream of the type {documentStreamType} or {fallbackStreamType}. Is your configuration correct?",
                    exception.Message);
            }

            [Fact]
            public void Should_use_primary_factory_when_document_stream_type_found()
            {
                // Arrange
                const string documentStreamType = "FoundType";
                const string fallbackStreamType = "FallbackType";
                var document = Substitute.For<IObjectDocument>();
                var documentActive = Substitute.For<StreamInformation>();
                documentActive.StreamType = documentStreamType;
                document.Active.Returns(documentActive);
                var mockEventStream = Substitute.For<IEventStream>();
                var primaryFactory = Substitute.For<IEventStreamFactory>();
                primaryFactory.Create(document).Returns(mockEventStream);
                var fallbackFactory = Substitute.For<IEventStreamFactory>();
                var settings = new EventStreamDefaultTypeSettings
                {
                    StreamType = fallbackStreamType
                };
                var factories = new Dictionary<string, IEventStreamFactory>
                {
                    { documentStreamType, primaryFactory },
                    { fallbackStreamType, fallbackFactory }
                };
                var sut = new EventStreamFactory(factories, settings);

                // Act
                var result = sut.Create(document);

                // Assert
                Assert.Same(mockEventStream, result);
                primaryFactory.Received(1).Create(document);
                fallbackFactory.DidNotReceive().Create(Arg.Any<IObjectDocument>());
            }

            [Fact]
            public void Should_throw_when_document_stream_type_is_null()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                var documentActive = Substitute.For<StreamInformation>();
                documentActive.StreamType = null!;
                document.Active.Returns(documentActive);
                var factories = new Dictionary<string, IEventStreamFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new EventStreamFactory(factories, settings);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.Create(document));
            }
        }
    }
}
