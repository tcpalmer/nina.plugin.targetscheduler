using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Planning.Scoring.Rules;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Planning.Scoring.Rules {

    [TestFixture]
    public class MeridianFlipPenaltyRuleTest {

        [Test]
        public void testMeridianFlipPenaltyRule() {
            DateTime OneAM = DateTime.Now.Date.AddHours(1);

            Mock<IScoringEngine> scoringEngineMock = PlanMocks.GetMockScoringEnging();
            scoringEngineMock.SetupProperty(se => se.ActiveProfile, GetProfile(5));

            Mock<IProject> pp = PlanMocks.GetMockPlanProject("pp", ProjectState.Active);
            pp.SetupProperty(m => m.MinimumTime, 30);

            Mock<ITarget> targetMock = new Mock<ITarget>().SetupAllProperties();
            targetMock.SetupProperty(m => m.Name, "T1");
            targetMock.SetupProperty(m => m.Project, pp.Object);
            targetMock.SetupProperty(m => m.CulminationTime, OneAM);

            MeridianFlipPenaltyRule sut = new MeridianFlipPenaltyRule();

            // target is already west of flip
            scoringEngineMock.SetupProperty(se => se.AtTime, OneAM.AddMinutes(6));
            sut.Score(scoringEngineMock.Object, targetMock.Object).Should().Be(1);

            // we can squeeze in a minimum time span before a flip
            scoringEngineMock.SetupProperty(se => se.AtTime, OneAM.AddMinutes(-26));
            sut.Score(scoringEngineMock.Object, targetMock.Object).Should().Be(1);

            // minimum time span will no longer fit
            scoringEngineMock.SetupProperty(se => se.AtTime, OneAM.AddMinutes(-25));
            sut.Score(scoringEngineMock.Object, targetMock.Object).Should().Be(0);
            scoringEngineMock.SetupProperty(se => se.AtTime, OneAM.AddMinutes(4));
            sut.Score(scoringEngineMock.Object, targetMock.Object).Should().Be(0);

            // now post flip again
            scoringEngineMock.SetupProperty(se => se.AtTime, OneAM.AddMinutes(6));
            sut.Score(scoringEngineMock.Object, targetMock.Object).Should().Be(1);
        }

        private IProfile GetProfile(double minutesAfterMeridian) {
            Mock<IMeridianFlipSettings> mfSettingsMock = new Mock<IMeridianFlipSettings>();
            mfSettingsMock.SetupProperty(mfs => mfs.MinutesAfterMeridian, minutesAfterMeridian);
            Mock<IProfile> profileMock = new Mock<IProfile>();
            profileMock.SetupProperty(p => p.MeridianFlipSettings, mfSettingsMock.Object);
            return profileMock.Object;
        }
    }
}