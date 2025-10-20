using FluentAssertions;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Test.Util;
using NINA.Plugin.TargetScheduler.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Database.Schema {

    [TestFixture]
    public class ProjectTest {

        [Test]
        public void TestDefaults() {
            var sut = new Project("123");
            TestUtils.ValidGuid(sut.Guid).Should().BeTrue();
            sut.MinimumTime.Should().Be(30);
            sut.MinimumAltitude.Should().BeApproximately(0, 0.0001);
            sut.MaximumAltitude.Should().BeApproximately(0, 0.0001);
            sut.UseCustomHorizon.Should().BeFalse();
            sut.HorizonOffset.Should().BeApproximately(0, 0.0001);
            sut.SmartExposureOrder.Should().BeFalse();
            sut.DitherEvery.Should().Be(0);
            sut.EnableGrader.Should().BeTrue();
            sut.RuleWeights.Should().NotBeNull();
        }

        [Test]
        public void TestGetPasteCopy() {
            DateTime onDate = DateTime.Now.Date;
            Project p1 = new Project("profileId");
            p1.Name = "p1N";
            p1.Description = "p1D";
            p1.ActiveDate = onDate;
            p1.Priority = ProjectPriority.High;
            p1.State = ProjectState.Inactive;
            p1.MinimumAltitude = 10;
            p1.MaximumAltitude = 80;
            p1.MinimumTime = 90;
            p1.UseCustomHorizon = true;
            p1.MeridianWindow = 60;
            p1.SmartExposureOrder = true;
            p1.IsMosaic = true;
            p1.FlatsHandling = 3;

            p1.RuleWeights = new List<RuleWeight> {
                        {new RuleWeight("a", .1) },
                        {new RuleWeight("b", .2) },
                    };

            Project p2 = p1.GetPasteCopy("profileId2");

            TestUtils.ValidGuid(p2.Guid).Should().BeTrue();
            p1.Guid.Equals(p2.Guid).Should().BeFalse();

            p1.RuleWeights.Clear();
            p1.RuleWeights.Add(new RuleWeight("c", .3));
            p1.RuleWeights.Add(new RuleWeight("d", .4));

            p2.Name.Should().Be(Utils.CopiedItemName("p1N"));
            p2.ProfileId.Should().Be("profileId2");
            p2.Description.Should().Be("p1D");
            p2.ActiveDate.Should().Be(onDate);
            p2.Priority.Should().Be(ProjectPriority.High);
            p2.State.Should().Be(ProjectState.Inactive);
            p2.MinimumAltitude.Should().Be(10);
            p2.MaximumAltitude.Should().Be(80);
            p2.MinimumTime.Should().Be(90);
            p2.UseCustomHorizon.Should().Be(true);
            p2.MeridianWindow.Should().Be(60);
            p2.SmartExposureOrder.Should().Be(true);
            p2.IsMosaic.Should().Be(true);
            p2.FlatsHandling.Should().Be(3);

            p2.RuleWeights.Count.Should().Be(2);
            p2.RuleWeights[0].Weight.Should().Be(.1);
            p2.RuleWeights[1].Weight.Should().Be(.2);

            p1.RuleWeights.Count.Should().Be(2);
            p1.RuleWeights[0].Weight.Should().Be(.3);
            p1.RuleWeights[1].Weight.Should().Be(.4);
        }
    }
}