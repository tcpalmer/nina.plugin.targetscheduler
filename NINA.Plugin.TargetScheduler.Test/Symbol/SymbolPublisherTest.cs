using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Symbol;
using NINA.Sequencer.Logic;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Symbol {

    [TestFixture]
    public class SymbolPublisherTest {
        private Mock<ISymbolBroker> _brokerMock;
        private Mock<ISymbolProvider> _providerMock;
        private SymbolPublisher _publisher;

        [SetUp]
        public void Setup() {
            _brokerMock = new Mock<ISymbolBroker>();
            _providerMock = new Mock<ISymbolProvider>();

            _brokerMock
                .Setup(b => b.RegisterSymbolProvider("TargetScheduler"))
                .Returns(_providerMock.Object);

            _publisher = new SymbolPublisher();
            _publisher.Init(_brokerMock.Object);
        }

        [TearDown]
        public void TearDown() {
            _publisher.Dispose();
        }

        [Test]
        public void testInit() {
            _brokerMock.Verify(b => b.RegisterSymbolProvider("TargetScheduler"), Times.Once);
            foreach (var name in SymbolPublisher.Tokens) {
                VerifySymbolPublished(name);
            }
        }

        [Test]
        public void testAddOrUpdate() {
            var ex = Assert.Throws<ArgumentException>(() => _publisher.AddOrUpdate("foo", 0));
            Assert.That(ex.Message, Is.EqualTo("symbols must be predefined: 'foo' does not exist"));

            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, true);
            VerifySymbolPublished(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, true);
        }

        [Test]
        public void testGetValue() {
            object val = "1.2.3.4";
            _brokerMock.Setup(b => b.TryGetValue(SymbolPublisher.SYMBOL_VERSION, out val)).Returns(true);
            _publisher.GetValue(SymbolPublisher.SYMBOL_VERSION).Should().Be(val);
        }

        [Test]
        public void testReset() {
            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, true);
            VerifySymbolPublished(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, true);

            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_API_RUNNING, true);
            VerifySymbolPublished(SymbolPublisher.SYMBOL_API_RUNNING, true);

            _publisher.AddOrUpdate(SymbolPublisher.SYMBOL_API_URL, "api_url");
            VerifySymbolPublished(SymbolPublisher.SYMBOL_API_URL, "api_url");

            _publisher.Reset();

            // Not retained
            VerifySymbolPublished(SymbolPublisher.SYMBOL_CONTAINER_RUNNING, null);

            // Retained
            VerifySymbolPublished(SymbolPublisher.SYMBOL_API_RUNNING, true);
            VerifySymbolPublished(SymbolPublisher.SYMBOL_API_URL, "api_url");
        }

        [Test]
        public void testDispose() {
            _publisher.Dispose();
            foreach (var name in SymbolPublisher.Tokens) {
                _providerMock.Verify(p => p.RemoveSymbol(name), Times.Once);
            }
        }

        private void VerifySymbolPublished(string name, object value) {
            _providerMock.Verify(p => p.AddOrUpdateSymbol(name, value), Times.AtLeastOnce);
        }

        private void VerifySymbolPublished(string name) {
            _providerMock.Verify(p => p.AddOrUpdateSymbol(name, It.IsAny<object>()), Times.AtLeastOnce);
        }
    }
}