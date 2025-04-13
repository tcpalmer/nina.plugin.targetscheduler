using CommunityToolkit.Mvvm.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using NINA.Core.MyMessageBox;
using NINA.Plugin.TargetScheduler.Database.ExportImport;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    public class ProfileExportViewVM : BaseVM {
        private const string DEFAULT_TYPE_FILTER = "<any>";

        private ProfileMeta ProfileMeta;
        private string ParentProfileId;

        private Dictionary<Target, string> targetsDict;

        public ProfileExportViewVM(TreeDataItem profileItem, IProfileService profileService) : base(profileService) {
            ProfileMeta = profileItem.Data as ProfileMeta;
            ParentProfileId = (profileItem.Data as ProfileMeta).Id.ToString();

            ExportCommand = new AsyncRelayCommand(ExportProfile);
            SelectFileCommand = new AsyncRelayCommand(SelectFile);

            ExportFilePath = null;
        }

        private string exportFilePath;

        public string ExportFilePath {
            get => exportFilePath;
            set {
                exportFilePath = value;
                if (exportFilePath != null && !exportFilePath.EndsWith(".zip")) {
                    exportFilePath = exportFilePath + ".zip";
                }

                RaisePropertyChanged(nameof(ExportFilePath));
                RaisePropertyChanged(nameof(ExportEnabled));
            }
        }

        private bool exportImageData = false;

        public bool ExportImageData {
            get => exportImageData;
            set {
                exportImageData = value;
                RaisePropertyChanged(nameof(ExportImageData));
            }
        }

        public bool ExportEnabled { get => ExportFileValid(); }

        private bool ExportFileValid() {
            try {
                if (string.IsNullOrEmpty(ExportFilePath)) { return false; }
                _ = new FileInfo(ExportFilePath);
                return true;
            } catch {
                return false;
            }
        }

        private bool exportRunning = false;

        public bool ExportRunning {
            get => exportRunning;
            set {
                exportRunning = value;
                RaisePropertyChanged(nameof(ExportRunning));
            }
        }

        public ICommand ExportCommand { get; private set; }
        public ICommand SelectFileCommand { get; private set; }

        private Task<bool> SelectFile() {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.Title = "Select Zip File";
            dialog.Multiselect = false;

            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok) {
                ExportFilePath = dialog.FileName;
            }

            return Task.FromResult(true);
        }

        private async Task<bool> ExportProfile() {
            ExportStatus status = null;

            await Task.Run(() => {
                ExportRunning = true;
                Thread.Sleep(50);
                status = new ExportProfile(ProfileMeta, ExportImageData).Export();
                ExportRunning = false;
            });

            if (status.IsSuccess) {
                if (File.Exists(ExportFilePath)) {
                    if (MyMessageBox.Show($"Zip file exists ({ExportFilePath}), overwrite?", "Overwrite?", MessageBoxButton.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes) {
                        TSLogger.Debug("export zip file move canceled, won't overwrite");
                        DeleteFile(status.TempZipFileName);
                        return true;
                    } else {
                        DeleteFile(ExportFilePath);
                    }
                }

                string msg = $"{status.GetDetails()}\nto {ExportFilePath}";
                MyMessageBox.Show(msg, "Export Success");

                TSLogger.Debug($"moving {status.TempZipFileName} to {ExportFilePath}");
                try {
                    File.Move(status.TempZipFileName, ExportFilePath);
                } catch (Exception ex) {
                    TSLogger.Error($"error moving export zip file: {ex.Message}");
                    MyMessageBox.Show($"Failed to move zip file: {ex.Message}", "Error");
                    return true;
                } finally {
                    DeleteFile(status.TempZipFileName);
                }
            } else {
                MyMessageBox.Show(status.GetDetails(), "Export Error");
            }

            return true;
        }

        private void DeleteFile(string fileName) {
            try { File.Delete(fileName); } catch { }
        }
    }
}