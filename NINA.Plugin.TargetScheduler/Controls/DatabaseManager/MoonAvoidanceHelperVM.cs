using NINA.Core.Utility;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Controls.Converters;
using OxyPlot;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.TargetScheduler.Controls.DatabaseManager {

    /// <summary>
    /// View model for the Moon Avoidance Helper dialog (classic Lorentzian avoidance plus relaxation).
    ///
    /// Holds working copies of the avoidance parameters so they can be edited and previewed without
    /// touching the exposure template until the user clicks Save.  The plot shows two curves across a full
    /// lunation (moon age 0-30 days): the classic separation, and the relaxed separation at the assumed
    /// moon altitude.  Relaxation modulates the separation/width parameters exactly as MoonAvoidanceExpert
    /// does at runtime; the assumed altitude is a preview-only input and is not saved.
    /// </summary>
    public class MoonAvoidanceHelperVM : BaseINPC {

        // Days of moon age spanned by the avoidance plot's horizontal axis.
        public const int PlotDays = 30;

        public MoonAvoidanceHelperVM(bool enabled, double separation, int width,
                                     double relaxScale, double relaxMinAltitude, double relaxMaxAltitude, bool moonDownEnabled) {
            classicEnabled = enabled;
            classicSeparation = separation;
            classicWidth = width;
            this.relaxScale = relaxScale;
            this.relaxMinAltitude = relaxMinAltitude;
            this.relaxMaxAltitude = relaxMaxAltitude;
            this.moonDownEnabled = moonDownEnabled;

            // Preview the relaxed curve at the middle of the relaxation altitude band by default.
            altitude = Math.Round((relaxMinAltitude + relaxMaxAltitude) / 2.0);

            RelaxScaleChoices = new List<string> { RelaxScaleChoicesConverter.OFF };
            for (int d = 1; d <= 8; d++) {
                RelaxScaleChoices.Add(d.ToString());
            }

            Recompute();
        }

        private bool classicEnabled;

        public bool ClassicEnabled {
            get => classicEnabled;
            set {
                classicEnabled = value;
                RaisePropertyChanged(nameof(ClassicEnabled));
                RaisePropertyChanged(nameof(RelaxEnabled));
            }
        }

        private double classicSeparation;

        public double ClassicSeparation {
            get => classicSeparation;
            set {
                // Constrain to whole degrees; always raise so the bound ScrollBar snaps its thumb back to
                // the integer position (even mid-drag) and never reports a fractional value.
                double rounded = Math.Round(value);
                bool changed = classicSeparation != rounded;
                classicSeparation = rounded;
                RaisePropertyChanged(nameof(ClassicSeparation));
                if (changed) {
                    Recompute();
                }
            }
        }

        private int classicWidth;

        public int ClassicWidth {
            get => classicWidth;
            set {
                // Width is already integral; always raise so the bound ScrollBar snaps its thumb to the
                // integer position even when a drag lands on the same value.
                bool changed = classicWidth != value;
                classicWidth = value;
                RaisePropertyChanged(nameof(ClassicWidth));
                if (changed) {
                    Recompute();
                }
            }
        }

        // Relaxation parameters.  These modulate the classic separation/width based on the assumed moon
        // altitude, producing the relaxed curve.

        public List<string> RelaxScaleChoices { get; }

        private double relaxScale;

        public double RelaxScale {
            get => relaxScale;
            set {
                relaxScale = value;
                RaisePropertyChanged(nameof(RelaxScale));
                RaisePropertyChanged(nameof(RelaxEnabled));
                RaisePropertyChanged(nameof(ShowRelaxed));
                Recompute();
            }
        }

        // Relaxation altitude controls are only active when classic avoidance is enabled and a scale is set.
        public bool RelaxEnabled => ClassicEnabled && RelaxScale > 0;

        // The relaxed curve is only meaningful (and only shown) when a relaxation scale is set.
        public bool ShowRelaxed => RelaxScale > 0;

        private double relaxMinAltitude;

        public double RelaxMinAltitude {
            get => relaxMinAltitude;
            set {
                double rounded = Math.Round(value);
                bool changed = relaxMinAltitude != rounded;
                relaxMinAltitude = rounded;
                RaisePropertyChanged(nameof(RelaxMinAltitude));
                if (changed) {
                    Recompute();
                }
            }
        }

        private double relaxMaxAltitude;

        public double RelaxMaxAltitude {
            get => relaxMaxAltitude;
            set {
                double rounded = Math.Round(value);
                bool changed = relaxMaxAltitude != rounded;
                relaxMaxAltitude = rounded;
                RaisePropertyChanged(nameof(RelaxMaxAltitude));
                if (changed) {
                    Recompute();
                }
            }
        }

        private double altitude;

        // Assumed moon altitude used to preview the relaxed curve; preview-only, not persisted.
        public double Altitude {
            get => altitude;
            set {
                double rounded = Math.Round(value);
                bool changed = altitude != rounded;
                altitude = rounded;
                RaisePropertyChanged(nameof(Altitude));
                if (changed) {
                    Recompute();
                }
            }
        }

        private bool moonDownEnabled;

        public bool MoonDownEnabled {
            get => moonDownEnabled;
            set {
                moonDownEnabled = value;
                RaisePropertyChanged(nameof(MoonDownEnabled));
            }
        }

        private IList<DataPoint> avoidancePoints;

        public IList<DataPoint> AvoidancePoints {
            get => avoidancePoints;
            private set {
                avoidancePoints = value;
                RaisePropertyChanged(nameof(AvoidancePoints));
            }
        }

        private IList<DataPoint> relaxedPoints;

        public IList<DataPoint> RelaxedPoints {
            get => relaxedPoints;
            private set {
                relaxedPoints = value;
                RaisePropertyChanged(nameof(RelaxedPoints));
            }
        }

        // The relaxed separation parameter at the assumed altitude (mirrors MoonAvoidanceExpert).
        private double RelaxedSeparationParameter() {
            if (RelaxScale > 0 && Altitude <= RelaxMaxAltitude) {
                return ClassicSeparation + (RelaxScale * (Altitude - RelaxMaxAltitude));
            }

            return ClassicSeparation;
        }

        // The relaxed width parameter at the assumed altitude (mirrors MoonAvoidanceExpert).
        private double RelaxedWidthParameter() {
            if (RelaxScale > 0 && Altitude <= RelaxMaxAltitude) {
                double span = RelaxMaxAltitude - RelaxMinAltitude;
                return span > 0 ? ClassicWidth * ((Altitude - RelaxMinAltitude) / span) : 0;
            }

            return ClassicWidth;
        }

        // Rebuild both curves (assigning new lists so the bound plot re-renders immediately).
        private void Recompute() {
            double relaxedSeparation = RelaxedSeparationParameter();
            double relaxedWidth = RelaxedWidthParameter();

            // If relaxation drives separation or width to zero or below, avoidance is off (required separation 0).
            bool relaxedOff = relaxedSeparation <= 0 || relaxedWidth <= 0;

            List<DataPoint> classic = new List<DataPoint>(PlotDays + 1);
            List<DataPoint> relaxed = new List<DataPoint>(PlotDays + 1);
            for (int day = 0; day <= PlotDays; day++) {
                classic.Add(new DataPoint(day, AstrometryUtils.GetMoonAvoidanceLorentzianSeparation(day, ClassicSeparation, ClassicWidth)));

                double relaxedValue = relaxedOff
                    ? 0
                    : AstrometryUtils.GetMoonAvoidanceLorentzianSeparation(day, relaxedSeparation, relaxedWidth);
                relaxed.Add(new DataPoint(day, relaxedValue));
            }

            AvoidancePoints = classic;
            RelaxedPoints = relaxed;
        }
    }
}
