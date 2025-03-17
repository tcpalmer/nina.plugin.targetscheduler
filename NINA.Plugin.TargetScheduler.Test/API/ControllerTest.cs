using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NINA.Plugin.TargetScheduler.API;
using NINA.Plugin.TargetScheduler.Database;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Profile;
using NINA.Profile.Interfaces;
using EmbedIO;
using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Shared.Utility;

namespace NINA.Plugin.TargetScheduler.Test.API {

    [TestFixture]
    public class ControllerTests {
        private Mock<ISchedulerDatabaseInteraction> _dbInteractionMock;
        private Mock<ISchedulerDatabaseContext> _dbContextMock;
        private Mock<IProfileService> _profileServiceMock;
        private Controller _controller;

        [SetUp]
        public void Setup() {
            _dbInteractionMock = new Mock<ISchedulerDatabaseInteraction>();
            _dbContextMock = new Mock<ISchedulerDatabaseContext>();
            _profileServiceMock = new Mock<IProfileService>();

            // When GetContext() is called, return our mocked ISchedulerDatabaseContext.
            _dbInteractionMock.Setup(d => d.GetContext()).Returns(_dbContextMock.Object);

            _controller = new Controller(_dbInteractionMock.Object, _profileServiceMock.Object);
        }

        [Test]
        public void GetVersion_ShouldReturnValidVersion() {
            // Act
            string version = _controller.GetVersion();

            // Assert
            version.Should().NotBeNullOrWhiteSpace();
            Version.TryParse(version, out Version parsedVersion).Should().BeTrue();
        }

        [Test]
        public void GetProfiles_ShouldReturnAllProfiles() {
            // Arrange
            var profile1 = new ProfileMeta { Id = Guid.NewGuid(), Name = "Profile1", IsActive = true };
            var profile2 = new ProfileMeta { Id = Guid.NewGuid(), Name = "Profile2", IsActive = false };
            var profiles = new Core.Utility.AsyncObservableCollection<ProfileMeta> { profile1, profile2 };

            _profileServiceMock.SetupGet(p => p.Profiles).Returns(profiles);

            // Act
            var result = _controller.GetProfiles().ToList();

            // Assert
            result.Should().HaveCount(2);
            result.First().Name.Should().Be("Profile1");
            result.First().ProfileId.Should().Be(profile1.Id.ToString());
        }

        [Test]
        public void GetProjects_ShouldReturnProjectsForGivenProfile() {
            // Arrange
            string profileId = "profile1";
            var project = new Project {
                ProfileId = profileId,
                Id = 1,
                Name = "Test Project",
                State = ProjectState.Active,
                Priority = ProjectPriority.Normal,
                Description = "Test description",
                CreateDate = DateTime.Now,
                ActiveDate = DateTime.Now,
                InactiveDate = null,
                MinimumTime = 0,
                UseCustomHorizon = false,
                HorizonOffset = 0,
                MeridianWindow = 0,
                FilterSwitchFrequency = 0,
                DitherEvery = 0,
                EnableGrader = true,
                IsMosaic = false,
                FlatsHandling = 0,
                MinimumAltitude = 0,
                MaximumAltitude = 90,
                SmartExposureOrder = false,
                Targets = new List<Target>()
            };

            _dbContextMock.Setup(x => x.GetAllProjectsReadOnly(profileId))
                          .Returns(new List<Project> { project });

            // Act
            var result = _controller.GetProjects(profileId).ToList();

            // Assert
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Test Project");
            result.First().ProfileId.Should().Be(profileId);
        }

        [Test]
        public void GetTargets_ShouldThrowHttpException404_WhenProjectNotFound() {
            // Arrange
            int projectId = 1;
            _dbContextMock.Setup(x => x.GetProjectReadOnly(projectId)).Returns((Project)null);

            // Act
            Action act = () => _controller.GetTargets(projectId);

            // Assert
            act.Should().Throw<HttpException>().Where(ex => ex.StatusCode == 404);
        }

        [Test]
        public void GetTargets_ShouldThrowHttpException500_WhenProfilePreferenceIsNull() {
            // Arrange
            int projectId = 1;
            var project = new Project {
                ProfileId = "profile1",
                EnableGrader = true,
                Targets = new List<Target>
                {
                    new Target {
                        ProjectId = projectId,
                        Id = 100,
                        Name = "Test Target",
                        active = true,
                        ra = 123.45,
                        dec = 67.89,
                        Rotation = 0.0,
                        Epoch = Epoch.J2000,
                        ROI = 1.0,
                        ExposurePlans = new List<ExposurePlan>()
                    }
                }
            };

            _dbContextMock.Setup(x => x.GetProjectReadOnly(projectId)).Returns(project);
            _dbContextMock.Setup(x => x.GetProfilePreference(project.ProfileId, true))
                          .Returns((ProfilePreference)null);

            // Act
            Action act = () => _controller.GetTargets(projectId);

            // Assert
            act.Should().Throw<HttpException>().Where(ex => ex.StatusCode == 500);
        }

        [Test]
        public void GetTargets_ShouldReturnTargets_WhenProjectAndProfilePreferenceExist() {
            // Arrange
            int projectId = 1;
            var profilePreference = new ProfilePreference { DelayGrading = 10, ExposureThrottle = 5 };
            var target = new Target {
                ProjectId = projectId,
                Id = 100,
                Name = "Test Target",
                active = true,
                ra = 123.45,
                dec = 67.89,
                Rotation = 0.0,
                Epoch = Epoch.J2000,
                ROI = 1.0,
                ExposurePlans = new List<ExposurePlan>()
            };
            var project = new Project {
                ProfileId = "profile1",
                EnableGrader = true,
                Targets = new List<Target> { target }
            };

            _dbContextMock.Setup(x => x.GetProjectReadOnly(projectId)).Returns(project);
            _dbContextMock.Setup(x => x.GetProfilePreference(project.ProfileId, true))
                          .Returns(profilePreference);

            // Act
            var result = _controller.GetTargets(projectId).ToList();

            // Assert
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Test Target");
        }

        [Test]
        public void GetTargetStatistics_ShouldThrowHttpException404_WhenTargetNotFound() {
            // Arrange
            int targetId = 1;
            _dbContextMock.Setup(x => x.GetTargetReadOnly(targetId)).Returns((Target)null);

            // Act
            Action act = () => _controller.GetTargetStatistics(targetId);

            // Assert
            act.Should().Throw<HttpException>().Where(ex => ex.StatusCode == 404);
        }

        [Test]
        public void GetTargetStatistics_ShouldReturnEmpty_WhenNoExposurePlansExist() {
            // Arrange
            int targetId = 1;
            var target = new Target {
                ExposurePlans = new List<ExposurePlan>(),
                Project = new Project { ProfileId = "profile1" }
            };

            _dbContextMock.Setup(x => x.GetTargetReadOnly(targetId)).Returns(target);
            // Provide a valid ProfilePreference so that null isn't returned.
            var profilePreference = new ProfilePreference {
                AutoAcceptLevelHFR = 1.5,
                AutoAcceptLevelFWHM = 2.5,
                AutoAcceptLevelEccentricity = 0.3
            };
            _dbContextMock.Setup(x => x.GetProfilePreference("profile1", false)).Returns(profilePreference);

            // Act
            var result = _controller.GetTargetStatistics(targetId);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void GetTargetStatistics_ShouldThrowNullReferenceException_WhenProfilePreferenceIsNull() {
            // Arrange
            int targetId = 1;
            var target = new Target {
                ExposurePlans = new List<ExposurePlan>(),
                Project = new Project { ProfileId = "profile1" }
            };

            _dbContextMock.Setup(x => x.GetTargetReadOnly(targetId)).Returns(target);
            // Intentionally return null to simulate missing ProfilePreference.
            _dbContextMock.Setup(x => x.GetProfilePreference("profile1", false)).Returns((ProfilePreference)null);

            // Act
            Action act = () => _controller.GetTargetStatistics(targetId);

            // Assert
            act.Should().Throw<NullReferenceException>();
        }

        [Test]
        public void GetTargetStatistics_ShouldReturnEmptyStats_WhenNoImagesForPlan() {
            // Arrange
            int targetId = 1;
            var exposurePlan = new ExposurePlan {
                Exposure = -1,
                ExposureTemplate = new ExposureTemplate("profile1", "Filter1", "Filter1")
            };
            var target = new Target {
                ExposurePlans = new List<ExposurePlan> { exposurePlan },
                Project = new Project { ProfileId = "profile1" }
            };
            var profilePreference = new ProfilePreference {
                AutoAcceptLevelHFR = 1.5,
                AutoAcceptLevelFWHM = 2.5,
                AutoAcceptLevelEccentricity = 0.3
            };

            _dbContextMock.Setup(x => x.GetTargetReadOnly(targetId)).Returns(target);
            _dbContextMock.Setup(x => x.GetProfilePreference(target.Project.ProfileId, false))
                          .Returns(profilePreference);
            _dbContextMock.Setup(x => x.GetAcquiredImages(targetId, exposurePlan.ExposureTemplate.FilterName))
                          .Returns(new List<AcquiredImage>()); // no images

            // Act
            var result = _controller.GetTargetStatistics(targetId).ToList();

            // Assert
            result.Should().HaveCount(1);
            var stats = result.First();
            stats.HFRMean.Should().Be(0.0);
            stats.FWHMMean.Should().Be(0.0);
            stats.EccentricityMean.Should().Be(0.0);
            stats.HFRBelowAutoAcceptLevel.Should().Be(-1);
            stats.FWHMBelowAutoAcceptLevel.Should().Be(-1);
            stats.EccentricityBelowAutoAcceptLevel.Should().Be(-1);
        }

        [Test]
        public void GetTargetStatistics_ShouldComputeStats_WhenImagesExist() {
            // Arrange
            int targetId = 1;
            var exposurePlan = new ExposurePlan {
                Exposure = -1,
                ExposureTemplate = new ExposureTemplate("profile1", "Filter1", "Filter1")
            };
            var target = new Target {
                ExposurePlans = new List<ExposurePlan> { exposurePlan },
                Project = new Project { ProfileId = "profile1" }
            };
            var profilePreference = new ProfilePreference {
                AutoAcceptLevelHFR = 1.5,
                AutoAcceptLevelFWHM = 2.5,
                AutoAcceptLevelEccentricity = 0.3
            };

            // Create an acquired image with known metadata.
            var acquiredImage = new AcquiredImage("dummy", targetId, 1, 1, DateTime.Now, "Filter1", 0, "reason",
                new ImageMetadata { HFR = 1.0, FWHM = 2.0, Eccentricity = 0.2 });

            _dbContextMock.Setup(x => x.GetTargetReadOnly(targetId)).Returns(target);
            _dbContextMock.Setup(x => x.GetProfilePreference(target.Project.ProfileId, false))
                          .Returns(profilePreference);
            _dbContextMock.Setup(x => x.GetAcquiredImages(targetId, exposurePlan.ExposureTemplate.FilterName))
                          .Returns(new List<AcquiredImage> { acquiredImage });

            // Act
            var result = _controller.GetTargetStatistics(targetId).ToList();

            // Assert
            result.Should().HaveCount(1);
            var stats = result.First();
            stats.HFRMean.Should().Be(1.0);
            stats.FWHMMean.Should().Be(2.0);
            stats.EccentricityMean.Should().Be(0.2);
            stats.HFRStdDev.Should().Be(0.0);
            stats.FWHMStdDev.Should().Be(0.0);
            stats.EccentricityStdDev.Should().Be(0.0);
            stats.HFRBelowAutoAcceptLevel.Should().Be(1);
            stats.FWHMBelowAutoAcceptLevel.Should().Be(1);
            stats.EccentricityBelowAutoAcceptLevel.Should().Be(1);
        }

        [Test]
        public void GetTargetStatistics_ShouldReturnMixedResults_WhenSomeExposurePlansHaveNoAcquiredImages() {
            // Arrange
            int targetId = 1;
            // Create two exposure plans: one with no images and one with images.
            var exposurePlanNoImages = new ExposurePlan {
                Exposure = -1,
                ExposureTemplate = new ExposureTemplate("profile1", "NoImagesFilter", "NoImagesFilter")
            };
            var exposurePlanWithImages = new ExposurePlan {
                Exposure = -1,
                ExposureTemplate = new ExposureTemplate("profile1", "ImagesFilter", "ImagesFilter")
            };

            var target = new Target {
                ExposurePlans = new List<ExposurePlan> { exposurePlanNoImages, exposurePlanWithImages },
                Project = new Project { ProfileId = "profile1" }
            };

            var profilePreference = new ProfilePreference {
                AutoAcceptLevelHFR = 1.5,
                AutoAcceptLevelFWHM = 2.5,
                AutoAcceptLevelEccentricity = 0.3
            };

            _dbContextMock.Setup(x => x.GetTargetReadOnly(targetId)).Returns(target);
            _dbContextMock.Setup(x => x.GetProfilePreference("profile1", false))
                          .Returns(profilePreference);

            // For the exposure plan with no images, return an empty list.
            _dbContextMock.Setup(x => x.GetAcquiredImages(targetId, "NoImagesFilter"))
                          .Returns(new List<AcquiredImage>());

            // For the exposure plan with images, return one acquired image.
            var acquiredImage = new AcquiredImage("dummy", targetId, 1, 1, DateTime.Now, "ImagesFilter", 0, "reason",
                new ImageMetadata { HFR = 1.2, FWHM = 2.1, Eccentricity = 0.25 });
            _dbContextMock.Setup(x => x.GetAcquiredImages(targetId, "ImagesFilter"))
                          .Returns(new List<AcquiredImage> { acquiredImage });

            // Act
            var result = _controller.GetTargetStatistics(targetId).ToList();

            // Assert
            result.Should().HaveCount(2);

            // Verify that for the "NoImagesFilter" plan, the statistics are empty.
            var emptyStats = result.FirstOrDefault(s => s.FilterName == "NoImagesFilter");
            emptyStats.Should().NotBeNull();
            emptyStats.HFRMean.Should().Be(0.0);
            emptyStats.FWHMMean.Should().Be(0.0);
            emptyStats.EccentricityMean.Should().Be(0.0);
            emptyStats.HFRBelowAutoAcceptLevel.Should().Be(-1);
            emptyStats.FWHMBelowAutoAcceptLevel.Should().Be(-1);
            emptyStats.EccentricityBelowAutoAcceptLevel.Should().Be(-1);

            // Verify that for the "ImagesFilter" plan, computed statistics are returned.
            var computedStats = result.FirstOrDefault(s => s.FilterName == "ImagesFilter");
            computedStats.Should().NotBeNull();
            computedStats.HFRMean.Should().BeApproximately(1.2, 0.001);
            computedStats.FWHMMean.Should().BeApproximately(2.1, 0.001);
            computedStats.EccentricityMean.Should().BeApproximately(0.25, 0.001);
        }

        [Test]
        public void GetTargetStatistics_ShouldReturnEmptyStats_WhenAcquiredImagesAreNull() {
            // Arrange
            int targetId = 1;
            var exposurePlan = new ExposurePlan {
                Exposure = -1,
                ExposureTemplate = new ExposureTemplate("profile1", "NullImagesFilter", "NullImagesFilter")
            };
            var target = new Target {
                ExposurePlans = new List<ExposurePlan> { exposurePlan },
                Project = new Project { ProfileId = "profile1" }
            };
            var profilePreference = new ProfilePreference {
                AutoAcceptLevelHFR = 1.5,
                AutoAcceptLevelFWHM = 2.5,
                AutoAcceptLevelEccentricity = 0.3
            };

            _dbContextMock.Setup(x => x.GetTargetReadOnly(targetId)).Returns(target);
            _dbContextMock.Setup(x => x.GetProfilePreference("profile1", false)).Returns(profilePreference);
            // Simulate that GetAcquiredImages returns null instead of an empty list.
            _dbContextMock.Setup(x => x.GetAcquiredImages(targetId, "NullImagesFilter"))
                .Returns((List<AcquiredImage>)null);

            // Act
            var result = _controller.GetTargetStatistics(targetId).ToList();

            // Assert
            result.Should().HaveCount(1);
            var stats = result.First();
            stats.HFRMean.Should().Be(0.0);
            stats.FWHMMean.Should().Be(0.0);
            stats.EccentricityMean.Should().Be(0.0);
            stats.HFRBelowAutoAcceptLevel.Should().Be(-1);
            stats.FWHMBelowAutoAcceptLevel.Should().Be(-1);
            stats.EccentricityBelowAutoAcceptLevel.Should().Be(-1);
        }

        [Test]
        public void GetTargets_ShouldReturnEmpty_WhenNoTargetsExist() {
            // Arrange
            int projectId = 1;
            var project = new Project {
                ProfileId = "profile1",
                EnableGrader = true,
                // Empty targets list.
                Targets = new List<Target>()
            };

            _dbContextMock.Setup(x => x.GetProjectReadOnly(projectId)).Returns(project);
            // Provide a valid ProfilePreference to bypass GetExposureCompletionHelper throwing.
            var profilePreference = new ProfilePreference { DelayGrading = 10, ExposureThrottle = 5 };
            _dbContextMock.Setup(x => x.GetProfilePreference("profile1", true))
                .Returns(profilePreference);

            // Act
            var result = _controller.GetTargets(projectId);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void GetTargetStatistics_ShouldThrowArgumentNullException_WhenExposurePlansIsNull() {
            // Arrange
            int targetId = 1;
            // Set ExposurePlans to null to simulate an unexpected state.
            var target = new Target {
                ExposurePlans = null,
                Project = new Project { ProfileId = "profile1" }
            };

            _dbContextMock.Setup(x => x.GetTargetReadOnly(targetId)).Returns(target);
            // Set up ProfilePreference so the code passes earlier checks.
            var profilePreference = new ProfilePreference {
                AutoAcceptLevelHFR = 1.5,
                AutoAcceptLevelFWHM = 2.5,
                AutoAcceptLevelEccentricity = 0.3
            };
            _dbContextMock.Setup(x => x.GetProfilePreference("profile1", false))
                .Returns(profilePreference);

            // Act
            Action act = () => _controller.GetTargetStatistics(targetId);

            // Assert: Expect a NullReferenceException because ExposurePlans is null.
            act.Should().Throw<ArgumentNullException>();
        }

    }
}
