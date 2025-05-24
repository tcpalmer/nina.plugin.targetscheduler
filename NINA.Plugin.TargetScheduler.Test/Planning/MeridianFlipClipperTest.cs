﻿using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Test.Astrometry;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Planning {

    [TestFixture]
    public class MeridianFlipClipperTest {

        [Test]
        public void testMeridianFlipClipperDisabled() {
            ITarget target = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            IProfile profile = GetProfileService(0, 5);
            DateTime startTime = DateTime.Now.Date;
            DateTime endTime = startTime.AddHours(2);
            DateTime transitTime = startTime.AddHours(1);

            TimeInterval interval = new MeridianFlipClipper(profile, startTime, target, startTime, transitTime, endTime).Clip();
            interval.StartTime.Should().Be(startTime);
            interval.EndTime.Should().Be(endTime);
        }

        [Test]
        public void testMeridianFlipClipperCase1() {
            // Case 1: S------E------P======T======A -> no flip zone after span (no change)
            ITarget target = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            IProfile profile = GetProfileService(5, 5);
            DateTime startTime = DateTime.Now.Date;
            DateTime endTime = startTime.AddHours(2);
            DateTime transitTime = startTime.AddHours(3);

            MeridianFlipClipper sut = new MeridianFlipClipper(profile, startTime, target, startTime, transitTime, endTime);
            TimeInterval interval = sut.Clip();
            interval.StartTime.Should().Be(startTime);
            interval.EndTime.Should().Be(endTime);
            sut.GetSafeAfterTime().Should().Be(transitTime.AddMinutes(5).AddSeconds(30));
        }

        [Test]
        public void testMeridianFlipClipperCase2() {
            // Case 2: P======T======A------S------E -> no flip zone before span (no change)
            ITarget target = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            IProfile profile = GetProfileService(5, 5);
            DateTime startTime = DateTime.Now.Date;
            DateTime endTime = startTime.AddHours(2);
            DateTime transitTime = startTime.AddHours(-1);

            TimeInterval interval = new MeridianFlipClipper(profile, startTime, target, startTime, transitTime, endTime).Clip();
            interval.StartTime.Should().Be(startTime);
            interval.EndTime.Should().Be(endTime);
        }

        [Test]
        public void testMeridianFlipClipperCase3() {
            // Case 3: S------P======T======A------E -> start before and end after no flip zone (clip E to P)
            ITarget target = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            IProfile profile = GetProfileService(5, 5);
            DateTime startTime = DateTime.Now.Date;
            DateTime endTime = startTime.AddHours(2);
            DateTime transitTime = startTime.AddMinutes(30);

            TimeInterval interval = new MeridianFlipClipper(profile, startTime, target, startTime, transitTime, endTime).Clip();
            interval.StartTime.Should().Be(startTime);
            interval.EndTime.Should().Be(transitTime.AddMinutes(-5).AddSeconds(-30));
        }

        [Test]
        public void testMeridianFlipClipperCase4() {
            // Case 4: S------P======T===E===A------ -> start before and end in no flip zone (clip E to P)
            ITarget target = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            IProfile profile = GetProfileService(5, 5);
            DateTime startTime = DateTime.Now.Date;
            DateTime endTime = startTime.AddMinutes(27);
            DateTime transitTime = startTime.AddMinutes(30);

            TimeInterval interval = new MeridianFlipClipper(profile, startTime, target, startTime, transitTime, endTime).Clip();
            interval.StartTime.Should().Be(startTime);
            interval.EndTime.Should().Be(transitTime.AddMinutes(-5).AddSeconds(-30));
        }

        [Test]
        public void testMeridianFlipClipperCase5() {
            // Case 5: -------P===S===T=====A------E -> start in no flip zone, end after (clip S to A)
            ITarget target = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            IProfile profile = GetProfileService(10, 10);
            DateTime startTime = DateTime.Now.Date;
            DateTime endTime = startTime.AddHours(2);
            DateTime transitTime = startTime.AddMinutes(5);

            TimeInterval interval = new MeridianFlipClipper(profile, startTime, target, startTime, transitTime, endTime).Clip();
            interval.StartTime.Should().Be(transitTime.AddMinutes(10).AddSeconds(30));
            interval.EndTime.Should().Be(endTime);
        }

        [Test]
        public void testMeridianFlipClipperCase6() {
            // Case 6: -------P===S===T===E===A----- -> start and end in no flip zone (reject)
            ITarget target = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            IProfile profile = GetProfileService(20, 20);
            DateTime startTime = DateTime.Now.Date;
            DateTime endTime = startTime.AddMinutes(15);
            DateTime transitTime = startTime.AddMinutes(10);

            TimeInterval interval = new MeridianFlipClipper(profile, startTime, target, startTime, transitTime, endTime).Clip();
            interval.Should().BeNull();
        }

        [Test]
        public void testMeridianFlipClipperBadTransit() {
            ITarget target = PlanMocks.GetMockPlanTarget("T1", TestData.M31).Object;
            IProfile profile = GetProfileService(5, 5);
            DateTime startTime = DateTime.Now.Date;
            DateTime endTime = startTime.AddHours(2);
            DateTime transitTime = DateTime.MinValue;

            TimeInterval interval = new MeridianFlipClipper(profile, startTime, target, startTime, transitTime, endTime).Clip();
            interval.StartTime.Should().Be(startTime);
            interval.EndTime.Should().Be(endTime);
        }

        private IProfile GetProfileService(double pauseMinutes, double minutesAfter) {
            Mock<IProfileService> profileMock = PlanMocks.GetMockProfileService(TestData.Pittsboro_NC);
            profileMock.SetupProperty(m => m.ActiveProfile.MeridianFlipSettings.PauseTimeBeforeMeridian, pauseMinutes);
            profileMock.SetupProperty(m => m.ActiveProfile.MeridianFlipSettings.MinutesAfterMeridian, minutesAfter);
            return profileMock.Object.ActiveProfile;
        }
    }
}