﻿using Microsoft.WindowsAPICodePack.Dialogs;
using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Plugin.TargetScheduler.Util;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class TargetImportVM : BaseINPC {
        private IFramingAssistantVM framingAssistantVM;
        private IPlanetariumFactory planetariumFactory;

        public TargetImportVM(IDeepSkyObjectSearchVM deepSkyObjectSearchVM, IFramingAssistantVM framingAssistantVM, IPlanetariumFactory planetariumFactory) {
            DeepSkyObjectSearchVM = deepSkyObjectSearchVM;
            DeepSkyObjectSearchVM.PropertyChanged += DeepSkyObjectSearchVM_PropertyChanged;

            this.framingAssistantVM = framingAssistantVM;
            FramingAssistantImportCommand = new RelayCommand(FramingAssistantImport);

            this.planetariumFactory = planetariumFactory;
            PlanetariumImportCommand = new RelayCommand(PlanetariumImport);

            SequenceTargetImportCommand = new RelayCommand(SequenceTargetImport);
        }

        private IDeepSkyObjectSearchVM deepSkyObjectSearchVM;
        public IDeepSkyObjectSearchVM DeepSkyObjectSearchVM { get => deepSkyObjectSearchVM; set => deepSkyObjectSearchVM = value; }

        private Target target;

        public Target Target {
            get { return target; }
            set {
                target = value;
                RaisePropertyChanged(nameof(Target));
            }
        }

        private void DeepSkyObjectSearchVM_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(DeepSkyObjectSearchVM.Coordinates) && DeepSkyObjectSearchVM.Coordinates != null) {
                Target target = new Target();
                target.Coordinates = DeepSkyObjectSearchVM.Coordinates;
                target.Name = DeepSkyObjectSearchVM.TargetName;
                Target = target;
            }
        }

        public ICommand FramingAssistantImportCommand { get; private set; }

        private void FramingAssistantImport() {
            Target target = new Target();
            target.Coordinates = GetFramingAssistantCoordinates();
            target.Name = framingAssistantVM.DSO.Name;
            target.Rotation = AstrometryUtils.ConvertRotation(framingAssistantVM.Rectangle.TotalRotation);

            Target = target;
        }

        private Coordinates GetFramingAssistantCoordinates() {
            var inputCoords = new InputCoordinates();
            inputCoords.RAHours = framingAssistantVM.RAHours;
            inputCoords.RAMinutes = framingAssistantVM.RAMinutes;
            inputCoords.RASeconds = framingAssistantVM.RASeconds;
            inputCoords.DecDegrees = framingAssistantVM.DecDegrees;
            inputCoords.DecMinutes = framingAssistantVM.DecMinutes;
            inputCoords.DecSeconds = framingAssistantVM.DecSeconds;
            return inputCoords.Coordinates;
        }

        public ICommand PlanetariumImportCommand { get; private set; }

        public ICommand SequenceTargetImportCommand { get; private set; }

        private void SequenceTargetImport() {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.Title = "Import Sequence Target";
            dialog.IsFolderPicker = false;
            dialog.Multiselect = false;
            dialog.InitialDirectory = Path.Combine(CoreUtil.APPLICATIONDIRECTORY, "Targets");

            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok) {
                string pathToFile = dialog.FileName;
                SequenceTarget sequenceTarget;

                try {
                    sequenceTarget = SequenceTargetParser.GetSequenceTarget(pathToFile);
                    Target target = new Target();
                    target.Coordinates = sequenceTarget.GetCoordinates();
                    target.Name = sequenceTarget.TargetName;
                    target.Rotation = sequenceTarget.Rotation;
                    Target = target;
                } catch (Exception e) {
                    TSLogger.Error($"failed to read sequence target at {pathToFile}: {e.Message} {e.StackTrace}");
                    Notification.ShowError($"Failed to import target from {pathToFile}");
                    return;
                }
            }
        }

        private async void PlanetariumImport() {
            Target target = await DoPlanetariumImport();
            if (target != null) {
                Target = target;
            }
        }

        private async Task<Target> DoPlanetariumImport() {
            try {
                IPlanetarium planetarium = planetariumFactory.GetPlanetarium();
                DeepSkyObject dso = await planetarium.GetTarget();

                if (dso != null) {
                    Target target = new Target();
                    target.Name = dso.Name;
                    target.Coordinates = dso.Coordinates;

                    if (planetarium.CanGetRotationAngle) {
                        double rotationAngle = await planetarium.GetRotationAngle();
                        if (!double.IsNaN(rotationAngle)) {
                            target.Rotation = rotationAngle;
                        }
                    }

                    return target;
                }

                return null;
            } catch (Exception e) {
                TSLogger.Error($"failed to get coordinates from planetarium: {e.Message}");
                Notification.ShowError($"Failed to get coordinates from planetarium: {e.Message}");
                return null;
            }
        }
    }
}