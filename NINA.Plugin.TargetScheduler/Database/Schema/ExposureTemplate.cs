using Newtonsoft.Json;
using NINA.Core.Model.Equipment;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Util;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.CompilerServices;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Database.Schema {

    [JsonObject(MemberSerialization.OptIn)]
    public class ExposureTemplate : INotifyPropertyChanged {

        [JsonProperty]
        [Key] public int Id { get; set; }

        [Required] public string profileId { get; set; }
        [Required] public string name { get; set; }
        [Required] public string filterName { get; set; }

        public double defaultExposure { get; set; }
        public int gain { get; set; }
        public int offset { get; set; }
        [JsonProperty] public int? bin { get; set; }
        public int readoutMode { get; set; }

        public int twilightlevel_col { get; set; }

        public bool moonAvoidanceEnabled { get; set; }
        public double moonAvoidanceSeparation { get; set; }
        public int moonAvoidanceWidth { get; set; }
        public double moonRelaxScale { get; set; }
        public double moonRelaxMaxAltitude { get; set; }
        public double moonRelaxMinAltitude { get; set; }
        public bool moonDownEnabled { get; set; }

        public int ditherEvery { get; set; }
        public double maximumHumidity { get; set; }

        [NotMapped]
        [JsonProperty]
        public string ProfileId {
            get { return profileId; }
            set {
                profileId = value;
                RaisePropertyChanged(nameof(ProfileId));
            }
        }

        [NotMapped]
        [JsonProperty]
        public string Name {
            get { return name; }
            set {
                name = value;
                RaisePropertyChanged(nameof(Name));
            }
        }

        [NotMapped]
        [JsonProperty]
        public string FilterName {
            get { return filterName; }
            set {
                filterName = value;
                RaisePropertyChanged(nameof(FilterName));
            }
        }

        [NotMapped]
        [JsonProperty]
        public double DefaultExposure {
            get { return defaultExposure; }
            set {
                defaultExposure = value;
                RaisePropertyChanged(nameof(DefaultExposure));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int Gain {
            get { return gain; }
            set {
                gain = value;
                RaisePropertyChanged(nameof(Gain));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int Offset {
            get { return offset; }
            set {
                offset = value;
                RaisePropertyChanged(nameof(Offset));
            }
        }

        [NotMapped]
        public BinningMode BinningMode {
            get {
                if (!bin.HasValue) bin = 1;
                return new BinningMode((short)bin, (short)bin);
            }
            set {
                bin = value.X;
                RaisePropertyChanged(nameof(BinningMode));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int ReadoutMode {
            get { return readoutMode; }
            set {
                readoutMode = value;
                RaisePropertyChanged(nameof(ReadoutMode));
            }
        }

        [NotMapped]
        [JsonProperty]
        public TwilightLevel TwilightLevel {
            get { return (TwilightLevel)twilightlevel_col; }
            set {
                twilightlevel_col = (int)value;
                RaisePropertyChanged(nameof(TwilightLevel));
            }
        }

        [NotMapped]
        [JsonProperty]
        public bool MoonAvoidanceEnabled {
            get { return moonAvoidanceEnabled; }
            set {
                moonAvoidanceEnabled = value;
                RaisePropertyChanged(nameof(MoonAvoidanceEnabled));
            }
        }

        [NotMapped]
        [JsonProperty]
        public double MoonAvoidanceSeparation {
            get { return moonAvoidanceSeparation; }
            set {
                moonAvoidanceSeparation = value;
                RaisePropertyChanged(nameof(MoonAvoidanceSeparation));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int MoonAvoidanceWidth {
            get { return moonAvoidanceWidth; }
            set {
                moonAvoidanceWidth = value;
                RaisePropertyChanged(nameof(MoonAvoidanceWidth));
            }
        }

        [NotMapped]
        [JsonProperty]
        public double MoonRelaxScale {
            get { return moonRelaxScale; }
            set {
                moonRelaxScale = value;
                RaisePropertyChanged(nameof(MoonRelaxScale));
            }
        }

        [NotMapped]
        [JsonProperty]
        public double MoonRelaxMaxAltitude {
            get { return moonRelaxMaxAltitude; }
            set {
                moonRelaxMaxAltitude = value;
                RaisePropertyChanged(nameof(MoonRelaxMaxAltitude));
            }
        }

        [NotMapped]
        [JsonProperty]
        public double MoonRelaxMinAltitude {
            get { return moonRelaxMinAltitude; }
            set {
                moonRelaxMinAltitude = value;
                RaisePropertyChanged(nameof(MoonRelaxMinAltitude));
            }
        }

        [NotMapped]
        [JsonProperty]
        public bool MoonDownEnabled {
            get { return moonDownEnabled; }
            set {
                moonDownEnabled = value;
                RaisePropertyChanged(nameof(MoonDownEnabled));
            }
        }

        [NotMapped]
        [JsonProperty]
        public int DitherEvery {
            get => ditherEvery;
            set {
                ditherEvery = value;
                RaisePropertyChanged(nameof(DitherEvery));
            }
        }

        [NotMapped]
        [JsonProperty]
        public double MaximumHumidity {
            get { return maximumHumidity; }
            set {
                maximumHumidity = value;
                RaisePropertyChanged(nameof(MaximumHumidity));
            }
        }

        public ExposureTemplate() {
        }

        public ExposureTemplate(string profileId, string name, string filterName) {
            ProfileId = profileId;
            Name = name;
            FilterName = filterName;

            DefaultExposure = 60;
            Gain = -1;
            Offset = -1;
            BinningMode = new BinningMode(1, 1);
            ReadoutMode = -1;

            TwilightLevel = TwilightLevel.Nighttime;

            MoonAvoidanceEnabled = false;
            MoonAvoidanceSeparation = 60;
            MoonAvoidanceWidth = 7;
            MoonRelaxScale = 0;
            MoonRelaxMaxAltitude = 5;
            MoonRelaxMinAltitude = -15;

            DitherEvery = -1;
            MaximumHumidity = 0;
        }

        public bool IsTwilightNightOnly() {
            return TwilightLevel == TwilightLevel.Nighttime;
        }

        public bool IsTwilightAstronomical() {
            return TwilightLevel == TwilightLevel.Astronomical;
        }

        public bool IsTwilightNautical() {
            return TwilightLevel == TwilightLevel.Nautical;
        }

        public bool IsTwilightCivil() {
            return TwilightLevel == TwilightLevel.Civil;
        }

        public ExposureTemplate GetPasteCopy(string newProfileId) {
            ExposureTemplate copy = new ExposureTemplate(newProfileId, Utils.CopiedItemName(name), filterName);

            copy.DefaultExposure = DefaultExposure;
            copy.Gain = Gain;
            copy.Offset = Offset;
            copy.BinningMode = BinningMode;
            copy.ReadoutMode = ReadoutMode;
            copy.TwilightLevel = TwilightLevel;
            copy.MoonAvoidanceEnabled = MoonAvoidanceEnabled;
            copy.MoonAvoidanceSeparation = MoonAvoidanceSeparation;
            copy.MoonAvoidanceWidth = MoonAvoidanceWidth;
            copy.MoonRelaxScale = MoonRelaxScale;
            copy.MoonRelaxMaxAltitude = MoonRelaxMaxAltitude;
            copy.MoonRelaxMinAltitude = MoonRelaxMinAltitude;
            copy.MoonDownEnabled = MoonDownEnabled;
            copy.MaximumHumidity = MaximumHumidity;

            return copy;
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"ProfileId: {ProfileId}");
            sb.AppendLine($"Name: {Name}");
            sb.AppendLine($"FilterName: {FilterName}");
            sb.AppendLine($"Default Exposure: {DefaultExposure}");
            sb.AppendLine($"Gain: {Gain}");
            sb.AppendLine($"Offset: {Offset}");
            sb.AppendLine($"BinningMode: {BinningMode}");
            sb.AppendLine($"ReadoutMode: {ReadoutMode}");
            sb.AppendLine($"TwilightLevel: {TwilightLevel}");
            sb.AppendLine($"MoonAvoidanceEnabled: {MoonAvoidanceEnabled}");
            sb.AppendLine($"MoonAvoidanceSeparation: {MoonAvoidanceSeparation}");
            sb.AppendLine($"MoonAvoidanceWidth: {MoonAvoidanceWidth}");
            sb.AppendLine($"MoonRelaxScale: {MoonRelaxScale}");
            sb.AppendLine($"MoonRelaxMaxAltitude: {MoonRelaxMaxAltitude}");
            sb.AppendLine($"MoonRelaxMinAltitude: {MoonRelaxMinAltitude}");
            sb.AppendLine($"MoonDownEnabled: {MoonDownEnabled}");
            sb.AppendLine($"MaximumHumidity: {MaximumHumidity}");

            return sb.ToString();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal class ExposureTemplateConfiguration : EntityTypeConfiguration<ExposureTemplate> {

        public ExposureTemplateConfiguration() {
            HasKey(x => new { x.Id });
            Property(x => x.twilightlevel_col).HasColumnName("twilightlevel");
        }
    }
}