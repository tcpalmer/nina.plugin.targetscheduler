using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Plugin.TargetScheduler.Astrometry;
using System;
using System.IO;

namespace NINA.Plugin.TargetScheduler.Test.Astrometry {

    public class TestData {
        public static readonly ObserverInfo North_Mid_Lat, South_Mid_Lat, North_Artic, Pittsboro_NC, Waskaganish_QC, Sanikiluaq_NU, North_Upper_Lat, Chapel_Hill_NC;

        public static readonly Coordinates BETELGEUSE = new Coordinates(AstroUtil.HMSToDegrees("5:55:11"), AstroUtil.DMSToDegrees("7:24:30"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates B150 = new Coordinates(AstroUtil.HMSToDegrees("20:51:6"), AstroUtil.DMSToDegrees("60:11:0"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates SPICA = new Coordinates(AstroUtil.HMSToDegrees("13:26:25.92"), AstroUtil.DMSToDegrees("-11:17:2.6"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates STAR_NORTH_CIRCP = new Coordinates(AstroUtil.HMSToDegrees("0:0:0"), AstroUtil.DMSToDegrees("80:0:0"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates STAR_SOUTH_CIRCP = new Coordinates(AstroUtil.HMSToDegrees("0:0:0"), AstroUtil.DMSToDegrees("-80:0:0"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates M42 = new Coordinates(AstroUtil.HMSToDegrees("5:35:17"), AstroUtil.DMSToDegrees("-5:23:28"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates M13 = new Coordinates(AstroUtil.HMSToDegrees("16:41:41"), AstroUtil.DMSToDegrees("36:27:36"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates M31 = new Coordinates(AstroUtil.HMSToDegrees("0:42:44"), AstroUtil.DMSToDegrees("41:16:7"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates IC1805 = new Coordinates(AstroUtil.HMSToDegrees("2:32:42"), AstroUtil.DMSToDegrees("61:27:0"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates C00 = new Coordinates(AstroUtil.HMSToDegrees("0:0:0"), AstroUtil.DMSToDegrees("0:0:0"), Epoch.J2000, Coordinates.RAType.Degrees);

        public static readonly Coordinates C90 = new Coordinates(AstroUtil.HMSToDegrees("9:0:0"), AstroUtil.DMSToDegrees("0:0:0"), Epoch.J2000, Coordinates.RAType.Degrees);

        static TestData() {
            // Northern hemisphere
            North_Mid_Lat = new ObserverInfo();
            North_Mid_Lat.Latitude = 35;
            North_Mid_Lat.Longitude = -79;
            North_Mid_Lat.Elevation = 165;

            // Southern hemisphere
            South_Mid_Lat = new ObserverInfo();
            South_Mid_Lat.Latitude = -35;
            South_Mid_Lat.Longitude = -80;
            South_Mid_Lat.Elevation = 165;

            // Northern hemisphere, above artic circle
            North_Artic = new ObserverInfo();
            North_Artic.Latitude = 67;
            North_Artic.Longitude = -80;
            North_Artic.Elevation = 165;

            // Northern hemisphere, Pittsboro
            Pittsboro_NC = new ObserverInfo();
            Pittsboro_NC.Latitude = 35.72027778;
            Pittsboro_NC.Longitude = -79.17638889;
            Pittsboro_NC.Elevation = 0;

            // Northern hemisphere, high latitude (Waskaganish, Que, ET)
            //
            Waskaganish_QC = new ObserverInfo();
            Waskaganish_QC.Latitude = 51.48;
            Waskaganish_QC.Longitude = -78.75;
            Waskaganish_QC.Elevation = 0;

            // Northern hemisphere, high latitude (Sanikiluaq, Nunavut, ET)
            // https://www.timeanddate.com/sun/canada/sanikiluaq
            Sanikiluaq_NU = new ObserverInfo();
            Sanikiluaq_NU.Latitude = 56.54277778;
            Sanikiluaq_NU.Longitude = -79.225;
            Sanikiluaq_NU.Elevation = 0;

            // Northern hemisphere, mid latitude
            North_Upper_Lat = new ObserverInfo();
            North_Upper_Lat.Latitude = 47.7;
            North_Upper_Lat.Longitude = -79.225;
            North_Upper_Lat.Elevation = 0;

            // Chapel Hill, NC
            Chapel_Hill_NC = new ObserverInfo();
            Chapel_Hill_NC.Latitude = 35.927222;
            Chapel_Hill_NC.Longitude = -79.039167;
            Chapel_Hill_NC.Elevation = 0;
        }

        public static CustomHorizon GetTestHorizon(int num) {
            string horizonDefinition;

            switch (num) {
                case 1: // constant 20°
                    horizonDefinition = $"0 20" + Environment.NewLine
                        + "90 20" + Environment.NewLine
                        + "180 20" + Environment.NewLine
                        + "270 20";
                    break;

                case 2: // up and down
                    horizonDefinition = $"0 20" + Environment.NewLine
                        + "90 30" + Environment.NewLine
                        + "180 40" + Environment.NewLine
                        + "270 30";
                    break;

                case 3: // mine
                    horizonDefinition = $"0 22" + Environment.NewLine
                        + "10 50" + Environment.NewLine
                        + "20 48" + Environment.NewLine
                        + "30 49" + Environment.NewLine
                        + "40 37" + Environment.NewLine
                        + "50 47" + Environment.NewLine
                        + "60 45" + Environment.NewLine
                        + "70 42" + Environment.NewLine
                        + "80 32" + Environment.NewLine
                        + "90 31" + Environment.NewLine
                        + "100 28" + Environment.NewLine
                        + "110 18" + Environment.NewLine
                        + "120 23" + Environment.NewLine
                        + "130 18" + Environment.NewLine
                        + "140 17" + Environment.NewLine
                        + "150 25" + Environment.NewLine
                        + "160 20" + Environment.NewLine
                        + "170 11" + Environment.NewLine
                        + "180.0001 18" + Environment.NewLine
                        + "190 50" + Environment.NewLine
                        + "200 49" + Environment.NewLine
                        + "210 31" + Environment.NewLine
                        + "220 33" + Environment.NewLine
                        + "230 32" + Environment.NewLine
                        + "240 56" + Environment.NewLine
                        + "250 61" + Environment.NewLine
                        + "260 63" + Environment.NewLine
                        + "270 61" + Environment.NewLine
                        + "280 52" + Environment.NewLine
                        + "290 54" + Environment.NewLine
                        + "300 25" + Environment.NewLine
                        + "310 15" + Environment.NewLine
                        + "320 21" + Environment.NewLine
                        + "330 26" + Environment.NewLine
                        + "340 23" + Environment.NewLine
                        + "350 24";
                    break;

                case 4: // @growers
                    horizonDefinition = "076.33      07.84" + Environment.NewLine
                        + "081.39      07.99" + Environment.NewLine
                        + "086.46      08.15" + Environment.NewLine
                        + "091.51      08.30" + Environment.NewLine
                        + "096.61      11.55" + Environment.NewLine
                        + "101.79      14.83" + Environment.NewLine
                        + "106.82      12.08" + Environment.NewLine
                        + "112.06      15.29" + Environment.NewLine
                        + "117.46      21.28" + Environment.NewLine
                        + "122.43      24.75" + Environment.NewLine
                        + "127.36      24.99" + Environment.NewLine
                        + "133.04      34.20" + Environment.NewLine
                        + "138.13      34.26" + Environment.NewLine
                        + "143.34      37.38" + Environment.NewLine
                        + "148.45      40.40" + Environment.NewLine
                        + "153.94      42.83" + Environment.NewLine
                        + "166.75      89.52" + Environment.NewLine
                        + "217.36      89.58" + Environment.NewLine
                        + "278.08      89.17" + Environment.NewLine
                        + "295.96      88.84" + Environment.NewLine
                        + "304.32      58.69" + Environment.NewLine
                        + "309.32      58.65" + Environment.NewLine
                        + "313.74      29.21" + Environment.NewLine
                        + "319.47      21.28" + Environment.NewLine
                        + "324.64      19.37" + Environment.NewLine
                        + "329.66      18.58" + Environment.NewLine
                        + "334.73      20.34" + Environment.NewLine
                        + "339.37      19.90" + Environment.NewLine
                        + "344.92      16.72" + Environment.NewLine
                        + "350.09      14.88" + Environment.NewLine
                        + "355.46      12.02" + Environment.NewLine
                        + "000.55      15.23" + Environment.NewLine
                        + "005.57      15.28" + Environment.NewLine
                        + "010.53      14.60" + Environment.NewLine
                        + "015.79      11.79" + Environment.NewLine
                        + "020.96      13.76" + Environment.NewLine
                        + "026.09      15.79" + Environment.NewLine
                        + "031.18      12.17" + Environment.NewLine
                        + "036.24      12.27" + Environment.NewLine
                        + "041.34      14.47" + Environment.NewLine
                        + "046.49      18.73" + Environment.NewLine
                        + "051.58      16.90" + Environment.NewLine
                        + "056.57      20.36" + Environment.NewLine
                        + "061.77      18.96" + Environment.NewLine
                        + "067.12      13.92";
                    break;

                default:
                    throw new NotImplementedException($"custom horizon not implemented: {num}");
            }

            using (var sr = new StringReader(horizonDefinition)) {
                return CustomHorizon.FromReader_Standard(sr);
            }
        }

        public static HorizonDefinition getHD(double minimumAltitude) {
            return new HorizonDefinition(minimumAltitude);
        }
    }
}