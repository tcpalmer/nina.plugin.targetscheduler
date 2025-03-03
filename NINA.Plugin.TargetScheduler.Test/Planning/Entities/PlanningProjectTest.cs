using FluentAssertions;
using Moq;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;

namespace NINA.Plugin.TargetScheduler.Test.Planning.Entities {

    [TestFixture]
    public class PlanningProjectTest {

        [Test]
        public void TestPlanningProject() {
            DateTime create = new DateTime(2024, 11, 11, 1, 2, 3);
            Mock<IProfile> profileMock = new Mock<IProfile>();
            Project project = new Project("abc123");
            project.Id = 101;
            project.Name = "project";
            project.CreateDate = create;
            project.MinimumTime = 30;
            project.MinimumAltitude = 25;
            project.MaximumAltitude = 80;
            project.HorizonOffset = 3;
            project.MeridianWindow = 120;
            project.FilterSwitchFrequency = 4;
            project.DitherEvery = 6;
            project.SmartExposureOrder = true;
            project.FlatsHandling = Project.FLATS_HANDLING_TARGET_COMPLETION;

            PlanningProject sut = new PlanningProject(profileMock.Object, project, new ExposureCompletionHelper(false, 0, 0));

            sut.PlanId.Should().NotBeNull();
            sut.DatabaseId.Should().Be(101);
            sut.Name.Should().Be("project");
            sut.State.Should().Be(ProjectState.Draft);
            sut.Priority.Should().Be(ProjectPriority.Normal);
            sut.CreateDate.Should().Be(create);
            sut.MinimumTime.Should().Be(30);
            sut.MinimumAltitude.Should().Be(25);
            sut.MaximumAltitude.Should().Be(80);
            sut.UseCustomHorizon.Should().BeFalse();
            sut.HorizonOffset.Should().Be(3);
            sut.MeridianWindow.Should().Be(120);
            sut.FilterSwitchFrequency.Should().Be(4);
            sut.DitherEvery.Should().Be(6);
            sut.SmartExposureOrder.Should().BeTrue();
            sut.EnableGrader.Should().BeTrue();
            sut.IsMosaic.Should().BeFalse();
            sut.FlatsHandling.Should().Be(Project.FLATS_HANDLING_TARGET_COMPLETION);
            sut.RuleWeights.Should().NotBeNull().And.HaveCount(7);
            sut.Rejected.Should().BeFalse();
            sut.HorizonDefinition.Should().NotBeNull();
            sut.ExposureCompletionHelper.Should().NotBeNull();
            sut.Targets.Should().NotBeNull();
        }
    }
}