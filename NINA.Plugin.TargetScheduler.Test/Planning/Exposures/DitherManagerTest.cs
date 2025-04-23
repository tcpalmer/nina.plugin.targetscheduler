using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NUnit.Framework;

namespace NINA.Plugin.TargetScheduler.Test.Planning.Exposures {

    [TestFixture]
    public class DitherManagerTest {

        [Test]
        public void testDitherEveryZero() {
            DitherManager sut = new DitherManager(0);

            IExposure L = GetExposure("L");
            IExposure R = GetExposure("R");
            IExposure G = GetExposure("G");
            IExposure B = GetExposure("B");

            sut.AddExposure(L);
            sut.AddExposure(R);
            sut.AddExposure(G);
            sut.AddExposure(B);
            sut.AddExposure(L);
            sut.AddExposure(R);
            sut.AddExposure(G);
            sut.AddExposure(B);
            sut.AddExposure(L);
            sut.AddExposure(R);
            sut.AddExposure(G);
            sut.AddExposure(B);

            sut.DitherRequired(L).Should().BeFalse();
            sut.DitherRequired(R).Should().BeFalse();
            sut.DitherRequired(G).Should().BeFalse();
            sut.DitherRequired(B).Should().BeFalse();
        }

        [Test]
        public void testDitherEveryOne() {
            DitherManager sut = new DitherManager(1);

            IExposure L = GetExposure("L");
            IExposure R = GetExposure("R");
            IExposure G = GetExposure("G");
            IExposure B = GetExposure("B");

            sut.DitherRequired(L).Should().BeFalse();
            sut.AddExposure(L);
            sut.DitherRequired(L).Should().BeTrue();
            sut.Reset();

            sut.DitherRequired(L).Should().BeFalse();
            sut.AddExposure(L);
            sut.DitherRequired(R).Should().BeFalse();
            sut.AddExposure(R);
            sut.DitherRequired(G).Should().BeFalse();
            sut.AddExposure(G);
            sut.DitherRequired(B).Should().BeFalse();
            sut.AddExposure(B);

            sut.DitherRequired(L).Should().BeTrue();
            sut.Reset();
            sut.AddExposure(L);
            sut.DitherRequired(R).Should().BeFalse();
        }

        [Test]
        public void testDitherEveryTwo() {
            DitherManager sut = new DitherManager(2);

            IExposure L = GetExposure("L");
            IExposure R = GetExposure("R");
            IExposure G = GetExposure("G");
            IExposure B = GetExposure("B");

            sut.DitherRequired(L).Should().BeFalse();
            sut.AddExposure(L);
            sut.DitherRequired(L).Should().BeFalse();
            sut.AddExposure(L);
            sut.DitherRequired(L).Should().BeTrue();
            sut.Reset();
        }

        [Test]
        public void testDitherOverride() {
            DitherManager sut = new DitherManager(2);

            // Assume I want to dither L every 2 exposures RGB every 2 exposures and SHO after every exposure

            IExposure L = GetExposure("L");
            IExposure R = GetExposure("R");
            IExposure G = GetExposure("G");
            IExposure B = GetExposure("B");
            IExposure S = GetExposure("S", 1);
            IExposure H = GetExposure("H", 1);
            IExposure O = GetExposure("O", 1);

            // LLRRGGBBSSHHOO => LLRRGGBBSdSHdHOdO
            // SSHHSSHH => SdSHdHSdSHdH

            sut.DitherRequired(L).Should().BeFalse(); sut.AddExposure(L);
            sut.DitherRequired(L).Should().BeFalse(); sut.AddExposure(L);
            sut.DitherRequired(R).Should().BeFalse(); sut.AddExposure(R);
            sut.DitherRequired(R).Should().BeFalse(); sut.AddExposure(R);
            sut.DitherRequired(G).Should().BeFalse(); sut.AddExposure(G);
            sut.DitherRequired(G).Should().BeFalse(); sut.AddExposure(G);
            sut.DitherRequired(B).Should().BeFalse(); sut.AddExposure(B);
            sut.DitherRequired(B).Should().BeFalse(); sut.AddExposure(B);
            sut.DitherRequired(S).Should().BeFalse(); sut.AddExposure(S);
            sut.DitherRequired(S).Should().BeTrue(); sut.AddExposure(S);
            sut.Reset();
            sut.DitherRequired(H).Should().BeFalse(); sut.AddExposure(H);
            sut.DitherRequired(H).Should().BeTrue(); sut.AddExposure(H);
            sut.Reset();
            sut.DitherRequired(O).Should().BeFalse(); sut.AddExposure(O);
            sut.DitherRequired(O).Should().BeTrue(); sut.AddExposure(O);
            sut.Reset();

            sut.DitherRequired(L).Should().BeFalse(); sut.AddExposure(L);
            sut.DitherRequired(L).Should().BeFalse(); sut.AddExposure(L);
            sut.DitherRequired(R).Should().BeFalse(); sut.AddExposure(R);
            sut.DitherRequired(R).Should().BeFalse(); sut.AddExposure(R);
            sut.DitherRequired(G).Should().BeFalse(); sut.AddExposure(G);
            sut.DitherRequired(G).Should().BeFalse(); sut.AddExposure(G);
            sut.DitherRequired(B).Should().BeFalse(); sut.AddExposure(B);
            sut.DitherRequired(B).Should().BeFalse(); sut.AddExposure(B);
            sut.DitherRequired(S).Should().BeFalse(); sut.AddExposure(S);
            sut.DitherRequired(S).Should().BeTrue(); sut.AddExposure(S);
            sut.Reset();
            sut.DitherRequired(H).Should().BeFalse(); sut.AddExposure(H);
            sut.DitherRequired(H).Should().BeTrue(); sut.AddExposure(H);
            sut.Reset();
            sut.DitherRequired(O).Should().BeFalse(); sut.AddExposure(O);
            sut.DitherRequired(O).Should().BeTrue(); sut.AddExposure(O);
            sut.Reset();

            // SSHHSSHH => SdSHdHSdSHdH
            sut.DitherRequired(S).Should().BeFalse(); sut.AddExposure(S);
            sut.DitherRequired(S).Should().BeTrue(); sut.AddExposure(S);
            sut.Reset();
            sut.DitherRequired(H).Should().BeFalse(); sut.AddExposure(H);
            sut.DitherRequired(H).Should().BeTrue(); sut.AddExposure(H);
            sut.Reset();
            sut.DitherRequired(S).Should().BeFalse(); sut.AddExposure(S);
            sut.DitherRequired(S).Should().BeTrue(); sut.AddExposure(S);
            sut.Reset();
            sut.DitherRequired(H).Should().BeFalse(); sut.AddExposure(H);
            sut.DitherRequired(H).Should().BeTrue(); sut.AddExposure(H);
            sut.Reset();
        }

        private IExposure GetExposure(string filterName, int ditherOverride = -1) {
            Mock<IExposure> mockExp = PlanMocks.GetMockPlanExposure(filterName, 1, 0);
            mockExp.SetupProperty(e => e.DitherEvery, ditherOverride);
            return mockExp.Object;
        }
    }
}