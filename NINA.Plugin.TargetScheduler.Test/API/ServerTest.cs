using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NINA.Plugin.TargetScheduler.API;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Profile.Interfaces;
using EmbedIO;
using EmbedIO.WebApi;
using NINA.Core.Utility.Notification;

namespace NINA.Plugin.TargetScheduler.Test.API {

    [TestFixture]
    public class ServerTests {
        private Mock<IProfileService> _profileServiceMock;
        private Mock<ISchedulerDatabaseInteraction> _dbInteractionMock;
        private Server _server;
        private const int TestPort = 12345;

        // FakeWebServer factory to avoid starting a real web server.
        private WebServer FakeWebServerFactory() {
            return new FakeWebServer();
        }

        [SetUp]
        public void Setup() {
            _profileServiceMock = new Mock<IProfileService>();
            _dbInteractionMock = new Mock<ISchedulerDatabaseInteraction>();
            // Inject our mocks and fake WebServer factory.
            _server = new Server(TestPort, _profileServiceMock.Object, _dbInteractionMock.Object, FakeWebServerFactory);
        }

        [TearDown]
        public void TearDown() {
            _server.Stop();
        }

        [Test]
        public void ControllerFactory_ShouldReturn_ControllerInstance() {
            // Act
            var controller = _server.ControllerFactory();

            // Assert
            controller.Should().NotBeNull();
            controller.Should().BeOfType<Controller>();
        }

        [Test]
        public void CreateServer_ShouldInitialize_WebServer() {
            // Act
            _server.CreateServer();

            // Assert
            _server.WebServer.Should().NotBeNull();
            _server.WebServer.Should().BeOfType<FakeWebServer>();
        }

        [Test]
        public void Start_Should_CreateAndStart_WebServer_And_Stop_Should_DisposeIt() {
            // Act: Start the server.
            _server.Start();
            // Allow a brief moment for the API thread to start.
            Thread.Sleep(200);

            // Assert: Fake WebServer should be assigned.
            _server.WebServer.Should().NotBeNull();

            // Act: Stop the server.
            _server.Stop();

            // Assert: After stopping, WebServer should be null.
            _server.WebServer.Should().BeNull();
        }

        [Test]
        public async Task TestablePreprocessRequestModule_Should_Add_AccessControlHeader() {
            // Arrange: Use our test subclass to expose the protected OnRequestAsync.
            var module = new TestablePreprocessRequestModule();
            var contextMock = new Mock<IHttpContext>();
            var requestMock = new Mock<IHttpRequest>();
            var responseMock = new Mock<IHttpResponse>();

            var testUri = new Uri("http://localhost/test");
            requestMock.Setup(r => r.Url).Returns(testUri);
            var headers = new WebHeaderCollection();
            responseMock.Setup(r => r.Headers).Returns(headers);

            contextMock.Setup(c => c.Request).Returns(requestMock.Object);
            contextMock.Setup(c => c.Response).Returns(responseMock.Object);

            // Act
            await module.CallOnRequestAsync(contextMock.Object);

            // Assert: Verify that the header is added.
            headers["Access-Control-Allow-Origin"].Should().Be("*");
        }

        [Test]
        public void Start_Should_Catch_Exception_When_RunAsync_Fails() {
            // Arrange:
            // Call CreateServer so that WebServer is assigned.
            _server.CreateServer();
            // Act: Start the server; FakeWebServer.RunAsync will throw, but the exception should be caught internally.
            _server.Start();
            // Allow some time for the API thread to run.
            Thread.Sleep(200);
            // Act: Stop the server.
            _server.Stop();
            // Assert: After stopping, WebServer should be null.
            _server.WebServer.Should().BeNull();
        }

    }

    // A test subclass to expose the protected OnRequestAsync method.
    public class TestablePreprocessRequestModule : PreprocessRequestModule {
        public Task CallOnRequestAsync(IHttpContext context) {
            return base.OnRequestAsync(context);
        }
    }

    // FakeWebServer hides the non-virtual RunAsync method using the 'new' keyword.
    public class FakeWebServer : WebServer {
        public bool RunAsyncCalled { get; private set; }

        public FakeWebServer() : base(o => o
            .WithUrlPrefix("http://localhost:12345")
            .WithMode(HttpListenerMode.EmbedIO)) {
        }

        // Hide RunAsync to simulate an exception without starting a real server.
        public new Task RunAsync(CancellationToken cancellationToken) {
            RunAsyncCalled = true;
            throw new Exception("Simulated RunAsync failure");
        }
    }
}
