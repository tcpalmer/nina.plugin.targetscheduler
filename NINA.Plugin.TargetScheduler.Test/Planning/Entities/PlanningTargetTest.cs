using FluentAssertions;
using NINA.Core.Model.Equipment;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Test.Planning.Entities {

    [TestFixture]
    public class PlanningTargetTest {

        [Test]
        public void testPlanningTarget() {
            IProject project = PlanMocks.GetMockPlanProject("project", ProjectState.Active).Object;
            project.FilterSwitchFrequency = 1;
            Target target = new Target();
            target.Id = 101;
            target.Name = "target";
            target.ra = 12;
            target.dec = 13;
            target.rotation = 14;
            target.roi = .8;
            target.OverrideExposureOrders = new List<OverrideExposureOrderItem>();
            target.FilterCadences = new List<FilterCadenceItem>();

            PlanningTarget sut = new PlanningTarget(project, target);
            sut.PlanId.Should().NotBeNull();
            sut.DatabaseId = 101;
            sut.Name = "target";
            sut.Coordinates.Should().Be(target.Coordinates);
            sut.Rotation.Should().Be(14);
            sut.ROI.Should().Be(.8);
            sut.Rejected.Should().BeFalse();
            sut.AllExposurePlans.Should().NotBeNull().And.HaveCount(0);
            sut.ExposurePlans.Should().NotBeNull().And.HaveCount(0);
            sut.CompletedExposurePlans.Should().NotBeNull().And.HaveCount(0);
            sut.ExposureSelector.Should().NotBeNull();
            (sut.ExposureSelector is BasicExposureSelector).Should().BeTrue();

            sut.MinimumTimeSpanEnd = DateTime.Now.Date;
            sut.MinimumTimeSpanEnd.Should().Be(DateTime.Now.Date);
        }

        [Test]
        public void testWithOverrideExposureOrder() {
            IProject project = PlanMocks.GetMockPlanProject("project", ProjectState.Active).Object;
            Target target = new Target();
            target.Id = 101;
            target.Name = "target";
            target.ra = 12;
            target.dec = 13;
            target.rotation = 14;
            target.roi = .8;
            target.FilterCadences = new List<FilterCadenceItem>();

            ExposurePlan L = GetEP("L");
            ExposurePlan R = GetEP("R");
            ExposurePlan G = GetEP("G");
            ExposurePlan B = GetEP("B");
            target.ExposurePlans = new List<ExposurePlan>() { L, R, G, B };

            target.OverrideExposureOrders = new List<OverrideExposureOrderItem>();
            target.OverrideExposureOrders.Add(GetOEO(1, 1, OverrideExposureOrderAction.Exposure, 0));
            target.OverrideExposureOrders.Add(GetOEO(1, 2, OverrideExposureOrderAction.Exposure, 0));
            target.OverrideExposureOrders.Add(GetOEO(1, 3, OverrideExposureOrderAction.Dither, -1));
            target.OverrideExposureOrders.Add(GetOEO(1, 4, OverrideExposureOrderAction.Exposure, 1));
            target.OverrideExposureOrders.Add(GetOEO(1, 5, OverrideExposureOrderAction.Exposure, 1));
            target.OverrideExposureOrders.Add(GetOEO(1, 6, OverrideExposureOrderAction.Dither, -1));

            PlanningTarget sut = new PlanningTarget(project, target);
            sut.AllExposurePlans.Count.Should().Be(4);
            sut.CompletedExposurePlans.Count.Should().Be(0);
            sut.ExposurePlans.Count.Should().Be(2);
            sut.ExposurePlans[0].FilterName.Should().Be("L");
            sut.ExposurePlans[1].FilterName.Should().Be("L");

            sut.ExposureSelector.Should().NotBeNull();
            (sut.ExposureSelector is OverrideOrderExposureSelector).Should().BeTrue();
        }

        [Test]
        public void testExposureClassification() {
            IProject project = PlanMocks.GetMockPlanProject("project", ProjectState.Active).Object;
            project.FilterSwitchFrequency = 1;
            Target target = new Target();
            target.Id = 101;
            target.Name = "target";
            target.ra = 12;
            target.dec = 13;
            target.rotation = 14;
            target.roi = .8;
            target.OverrideExposureOrders = new List<OverrideExposureOrderItem>();
            target.FilterCadences = new List<FilterCadenceItem>();

            ExposureTemplate et = new ExposureTemplate();
            et.FilterName = "L";
            et.BinningMode = new BinningMode(1, 1);
            ExposurePlan e1 = new ExposurePlan();
            e1.Id = 1;
            e1.Desired = 2;
            e1.Accepted = 2;
            e1.Acquired = 2;
            e1.ExposureTemplate = et;
            ExposurePlan e2 = new ExposurePlan();
            e2.Id = 2;
            e2.Desired = 2;
            e2.Accepted = 0;
            e2.Acquired = 0;
            e2.ExposureTemplate = et;

            target.ExposurePlans.Add(e1);
            target.ExposurePlans.Add(e2);

            PlanningTarget sut = new PlanningTarget(project, target);
            sut.PlanId.Should().NotBeNull();
            sut.DatabaseId = 101;
            sut.Name = "target";
            sut.Coordinates.Should().Be(target.Coordinates);
            sut.Rotation.Should().Be(14);
            sut.ROI.Should().Be(.8);
            sut.Rejected.Should().BeFalse();
            sut.AllExposurePlans.Should().NotBeNull().And.HaveCount(2);
            sut.ExposurePlans.Should().NotBeNull().And.HaveCount(1);
            sut.CompletedExposurePlans.Should().NotBeNull().And.HaveCount(1);
            sut.ExposureSelector.Should().NotBeNull();
            (sut.ExposureSelector is BasicExposureSelector).Should().BeTrue();
        }

        private ExposurePlan GetEP(string filterName) {
            var ep = new ExposurePlan();
            ep.ExposureTemplate = new ExposureTemplate { FilterName = filterName, BinningMode = new BinningMode(1, 1) };
            return ep;
        }

        private OverrideExposureOrderItem GetOEO(int tid, int order, OverrideExposureOrderAction action, int refIdx) {
            return new OverrideExposureOrderItem { TargetId = tid, Order = order, Action = action, ReferenceIdx = refIdx };
        }
    }
}