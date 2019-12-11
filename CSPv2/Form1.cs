using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using System.Windows.Media;
using System.Windows;
using System.Windows.Threading;
using LiveCharts.Configurations;
using System.Web.Script.Serialization;
using System.Threading;
using System.Diagnostics;
using Microsoft.Scripting.Utils;
using IronPython.Hosting;
using Newtonsoft.Json;

namespace CSPv2
{
    public partial class CSP_Main : Form
    {
        #region Parameters
        /// Code for border none (Future versions)
        /// public const int WM_NCLBUTTONDOWN = 0xA1;
        /// public const int HTCAPTION = 0x2;</summary>
        ///  [DllImport("User32.dll")]
        ///  public static extern bool ReleaseCapture();
        ///  [DllImport("User32.dll")]
        ///  public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        // Form Output Table
        public FormOutputTable f3;

        // Easter Eggs
        public static readonly string[] ErrorMessageTitles =
            { "Incorrect input",
            "Incorrect input, try again",
            "What are you, a Biologist?",
            "Oh, C'mon. Really?",
            };

        // Boolean controls for Ref, DS loading and temp folder
        bool reference_loaded;
        bool ds_loaded;
        bool AnalysisDone;
        // Check if temp exist COMMENTED FOR DEBUGGING
        string tempfolder = Path.GetTempPath();
        //string tempfolder;
        bool MCCsaved;
        string MCCReportSource;
        string MCCReportDestination;
        string pythonpath = System.Windows.Forms.Application.StartupPath + "\\Miniconda3\\python.exe";
        string pythonexe = System.Windows.Forms.Application.StartupPath + "\\Miniconda3\\python.exe";
        bool pythonset;
        bool IsReference;
        bool par_confirmed;

        // Index from chart1 to overview
        public int index;
        public int res;
        public int gotoexp;
        bool found;

        // Configure player
        public int n;
        public int nmin;
        public int nmax;

        // Actives and Inactives
        public bool ShowActives;
        public bool ShowInactives;
        public double ProbThreshold;
        public int IndexBarCharts;

        // Import Parameters
        public double NMin;
        public double NMax;
        public double HMin;
        public double HMax;
        public double RefIntMin;
        public double DSIntMin;
        public int errorcount;
        public double checkimport;
        bool stopasking;

        // Graphs Parameters
        int LegendFontSize = 13;
        int LabelAngle = 30;
        #endregion

        #region Brushes
        // Brushes
        SolidColorBrush LabelForegroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 162, 151));
        SolidColorBrush SeparatorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 79, 86));
        SolidColorBrush ActiveManualForeground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        SolidColorBrush ActiveManualFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 123, 217, 157));
        SolidColorBrush InactiveManualForeground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        SolidColorBrush InactiveManualFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 199, 137, 137));
        SolidColorBrush NotSetManualForeground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        SolidColorBrush NotSetManualFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 178, 178, 178));
        SolidColorBrush AllSpectraFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 250, 163, 0));
        SolidColorBrush AllSpectraFillLight = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 250, 163, 0));
        SolidColorBrush GaugeBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(76, 76, 76, 76));
        SolidColorBrush ActiveAutoFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 45, 161, 63));
        SolidColorBrush InactiveAutoFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 225, 9, 20));
        SolidColorBrush DangerBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 238, 83, 80));
        // Brushes Visual Elements
        SolidColorBrush BrokenSpectrum = new SolidColorBrush(System.Windows.Media.Color.FromArgb(5, 254, 132, 132));
        SolidColorBrush FineSpectrum = new SolidColorBrush(System.Windows.Media.Color.FromArgb(5, 45, 161, 63));
        SolidColorBrush CheckSpectrum = new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 204, 204, 204));
        //text
        SolidColorBrush TextBrushVisualElement = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 255));
        #endregion

        /// XML Deserializer NOT USED
        ///public static Object XMLReader(string RefPath, Type objectType)
        ///{
        ///    StringReader strReader = null;
        ///    XmlSerializer serializer = null;
        ///    XmlTextReader xmlReader = null;
        ///    Object peaklist = null;
        ///    try
        ///    {
        ///        strReader = new StringReader(RefPath);
        ///        serializer = new XmlSerializer(objectType);
        ///        xmlReader = new XmlTextReader(strReader);
        ///        peaklist = serializer.Deserialize(xmlReader);
        ///    }
        ///    catch (Exception excp)
        ///    {
        ///        if (xmlReader == null)
        ///        {
        ///
        ///        }
        ///        // Handle Exception Code
        ///    }
        ///    finally
        ///    {
        ///        if (xmlReader != null)
        ///        {
        ///            xmlReader.Close();
        ///        }
        ///
        ///        if (strReader != null)
        ///        {
        ///            strReader.Close();
        ///        }
        ///    }
        ///    return peaklist;
        ///}

        #region Lists
        // Lists definition: collections of SPECTRUM class
        List<SPECTRUM> SPECTRA = new List<SPECTRUM>();
        List<string> EXP_NAMES = new List<string>();
        List<string> VALID_EXP = new List<string>();
        List<string> FAULT_EXP = new List<string>();
        List<SPECTRUM> VALID_DS_SPECTRA = new List<SPECTRUM>();
        List<SPECTRUM> OOR = new List<SPECTRUM>();
        List<SPECTRUM> ACTIVES = new List<SPECTRUM>();
        List<SPECTRUM> INACTIVES = new List<SPECTRUM>();
        List<SPECTRUM> MAN_ACTIVES = new List<SPECTRUM>();
        List<SPECTRUM> MAN_INACTIVES = new List<SPECTRUM>();
        public static List<SPECTRUM> process = new List<SPECTRUM>();
        List<string> JSON_SPECTRA = new List<string>();
        #endregion

        #region Configure Charts
        // BAR CHART MANUAL RESULTS
        public void results_graph_config()
        {
            cartesianChartResults.Update();
            cartesianChartResults.LegendLocation = LegendLocation.Right;
            cartesianChartResults.DefaultLegend.FontSize = 10;
            cartesianChartResults.DefaultLegend.Foreground = LabelForegroundBrush;

            //X Axis
            cartesianChartResults.AxisX.Add(new Axis
            {
                Labels = new[]
                {
                    "Analysis Results",
                },
                Separator = new Separator // force the separator step to 1, so it always display all labels
                {
                    Step = 1,
                    IsEnabled = false //disable it to make it invisible.
                },
            });

            cartesianChartResults.AxisY.Add(new Axis
            {
                Separator = new Separator()
                {
                    Stroke = SeparatorBrush
                }
            });
        }

        // CHART SCATTER SPECTRA OVERLAY FORMATTING
        public void graph_config()
        {
            cartesianChart2.Update();
            cartesianChart2.LegendLocation = LegendLocation.Top;
            cartesianChart2.DefaultLegend.Foreground = LabelForegroundBrush;
            cartesianChart2.DefaultLegend.FontSize = LegendFontSize;
            cartesianChart2.AxisX.Add(new Axis { });
            cartesianChart2.AxisY.Add(new Axis { });

            // X Axis
            cartesianChart2.AxisX[0].Visibility = Visibility.Visible;
            cartesianChart2.AxisX[0].Title = "1H ppm";
            cartesianChart2.AxisX[0].MaxValue = -1 * HMin;
            cartesianChart2.AxisX[0].MinValue = -1 * HMax;

            // Label formatting
            cartesianChart2.AxisX[0].LabelFormatter = val => Convert.ToString(-1 * Math.Round(val, 2));
            // Separators
            cartesianChart2.AxisX[0].Separator.StrokeThickness = 1.5;
            cartesianChart2.AxisX[0].Separator.Stroke = SeparatorBrush;

            // Y Axis
            cartesianChart2.AxisY[0].Title = "15N ppm";
            cartesianChart2.AxisY[0].MinValue = -1 * NMax;
            cartesianChart2.AxisY[0].MaxValue = -1 * NMin;
            cartesianChart2.AxisY[0].Position = AxisPosition.RightTop;
            cartesianChart2.AxisY[0].Separator = new Separator
            {
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection(new double[] { 4 }),
                Stroke = SeparatorBrush
            };
            cartesianChart2.AxisY[0].Visibility = Visibility.Visible;
            // Label formatting
            cartesianChart2.AxisY[0].LabelFormatter = val => Convert.ToString(-1 * Math.Round(val, 2));
            // Separators
            cartesianChart2.AxisY[0].Separator.StrokeThickness = 1.5;
            cartesianChart2.AxisY[0].Separator.Stroke = SeparatorBrush;

            // Zoom enabled
            cartesianChart2.Zoom = ZoomingOptions.Xy;

            AxisSection axisSection = new AxisSection();
        }

        // CHART BAR CHART PEAK DIFFERENCE FORMATTING
        public void config_chartPeakDiff()
        {
            cartesianChartPeakDiff.Series = PeakDiffSeries;
            cartesianChartPeakDiff.Name = "Read Peaks difference Distribution";
            cartesianChartPeakDiff.Zoom = ZoomingOptions.X;

            cartesianChartPeakDiff.Update();
            cartesianChartPeakDiff.AxisX.Add(new Axis
            {
                Title = "Experiment No.",
                LabelsRotation = LabelAngle,
                MinValue = 0,
                MaxValue = VALID_DS_SPECTRA.Count
            });

            cartesianChartPeakDiff.AxisX[0].Separator = new Separator
            {
                Step = 1,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection(new double[] { 4 }),
                Stroke = SeparatorBrush
            };
            cartesianChartPeakDiff.AxisX[0].RangeChanged += Axis_RangeChanged;
            List<string> Explabels = new List<string>();

            foreach (SPECTRUM vs in VALID_DS_SPECTRA)
            {
                Explabels.Add(vs.EXP_NUMBER.ToString());
            }

            cartesianChartPeakDiff.AxisX[0].Labels = Explabels;

            cartesianChartPeakDiff.AxisY.Add(new Axis
            {
                Title = "ΔPeaks",
                MinValue = -80,
                MaxValue = 80,

                Separator = new Separator
                {
                    StrokeThickness = 1,
                    Stroke = SeparatorBrush
                },

                Sections = new SectionsCollection
                {
                    new AxisSection
                    {
                        Value = -80,
                        SectionWidth = 40,
                        Fill = BrokenSpectrum,
                    },

                    new AxisSection
                    {
                        Value = 25,
                        SectionWidth = 20,
                        Fill = CheckSpectrum,
                    },

                    new AxisSection
                    {
                        Value = -45,
                        SectionWidth = 20,
                        Fill = CheckSpectrum,
                    },

                    new AxisSection
                    {
                        Value = 40,
                        SectionWidth = 40,
                        Fill = BrokenSpectrum,
                    },

                    new AxisSection
                    {
                        Value = -30,
                        SectionWidth = 60,
                        Fill = FineSpectrum,
                    },
                }
            });

            cartesianChartPeakDiff.VisualElements.Add(new VisualElement
            {
                X = VALID_DS_SPECTRA.Count / 2,
                Y = 15,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                {
                    Text = "Safe range",
                    Foreground = ActiveAutoFill,
                    FontSize = 10,
                    Opacity = 0.8
                }
            });

            cartesianChartPeakDiff.AxisY[0].Sections.Add(new AxisSection
            {
                Value = 0,
                Stroke = System.Windows.Media.Brushes.LightGray,
                StrokeThickness = 0.5,
            });

            cartesianChartPeakDiff.VisualElements.Add(new VisualElement
            {
                X = VALID_DS_SPECTRA.Count / 2,
                Y = -15,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                {
                    Text = "Safe range",
                    Foreground = ActiveAutoFill,
                    FontSize = 10,
                    Opacity = 0.8
                }
            });

            cartesianChartPeakDiff.VisualElements.Add(new VisualElement
            {
                X = VALID_DS_SPECTRA.Count / 2,
                Y = 35,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                {
                    Text = "Check PP",
                    Foreground = TextBrushVisualElement,
                    FontSize = 10,
                    Opacity = 0.8
                }
            });

            cartesianChartPeakDiff.VisualElements.Add(new VisualElement
            {
                X = VALID_DS_SPECTRA.Count / 2,
                Y = -35,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                {
                    Text = "Check PP",
                    Foreground = TextBrushVisualElement,
                    FontSize = 10,
                    Opacity = 0.8
                }
            });

            cartesianChartPeakDiff.VisualElements.Add(new VisualElement
            {
                X = VALID_DS_SPECTRA.Count / 2,
                Y = 65,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                {
                    Text = "Broken Spectrum",
                    Foreground = DangerBrush,
                    FontSize = 10,
                    Opacity = 0.8
                }
            });

            cartesianChartPeakDiff.VisualElements.Add(new VisualElement
            {
                X = VALID_DS_SPECTRA.Count / 2,
                Y = -65,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                {
                    Text = "Broken Spectrum",
                    Foreground = DangerBrush,
                    FontSize = 10,
                    Opacity = 0.8
                }
            });

            cartesianChartPeakDiff.VisualElements.Add(new VisualElement
            {
                X = n,
                Y = VALID_DS_SPECTRA[n].PEAK_DIFF,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                {
                    Text = "Current Spectrum" + Environment.NewLine +
                           "              | ",
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrushVisualElement,
                    FontSize = 10,
                }
            });
        }

        // CHART BAR CHART PROBABILITY FORMATTING
        public void cartesianChart1_config()
        {
            cartesianChart1.Zoom = ZoomingOptions.X;
            cartesianChart1.Series = ProbSeries;
            cartesianChart1.Update();
            cartesianChart1.AxisX.Add(new Axis
            {
                Title = "Experiment No.",
                LabelsRotation = LabelAngle,
                MinValue = 0,
                MaxValue = VALID_DS_SPECTRA.Count()
            });

            cartesianChart1.AxisX[0].Separator = new Separator
            {
                Step = 1,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection(new double[] { 4 }),
                Stroke = SeparatorBrush
            };
            cartesianChart1.AxisX[0].RangeChanged += Axis_RangeChanged;
            List<string> Explabelsnew = new List<string>();

            foreach (SPECTRUM vs in VALID_DS_SPECTRA)
            {
                Explabelsnew.Add(vs.EXP_NUMBER.ToString());
            }

            cartesianChart1.AxisX[0].Labels = Explabelsnew;

            cartesianChart1.AxisY.Add(new Axis
            {
                Title = "Probability",

                Separator = new Separator
                {
                    StrokeThickness = 1,
                    Stroke = SeparatorBrush
                },

                Sections = new SectionsCollection
                {
                    new AxisSection
                    {
                        Value = 0,
                        SectionWidth = 0.35,
                        Fill = BrokenSpectrum,
                    },

                    new AxisSection
                    {
                        Value = 0.35,
                        SectionWidth = 0.4,
                        Fill = CheckSpectrum,
                    },

                    new AxisSection
                    {
                        Value = 0.75,
                        SectionWidth = 0.25,
                        Fill = FineSpectrum,
                    },
                }
            });

            cartesianChart1.AxisY[0].MinValue = 0;
            cartesianChart1.AxisY[0].MaxValue = 1;
        }
        #endregion

        #region Class SPECTRUM and PEAK
        // PEAK Definition
        public class PEAK
        {
            public int NUMBER;
            public double F1;
            public double F2;
            public double INTENSITY;
            public string EXP_IDENTIFIER;
        }

        // SPECTRUM CLASS (main class) SPECTRUM -> PEAKLIST -> collection of PEAK structures
        public class SPECTRUM
        {
            // classification 0 = inactive 1 = active
            public double Prob;
            public bool isActive;
            public string UserSelection = "Not set";
            public string JSON_Data;
            public int TOT_PEAKS;
            public int TOT_READ_PEAKS;
            public int EXP_NUMBER;
            public string DS_NAME;
            public string PP_INFO;
            public int PEAK_DIFF; // Defined as SPECTRUM[i].TOT_READ_PEAKS - REFERENCE.TOT_READ_PEAKS
            public List<PEAK> PEAKLIST = new List<PEAK>();
            public SPECTRUM Read_spectrum(string RefPath, double Intensity_threshold, double NMin, double NMax, double HMin, double HMax)
            {
                // Initialize
                SPECTRUM Read_spectrum = new SPECTRUM
                {
                    TOT_PEAKS = 0
                };

                // NUKE-PROOF file name fetcher
                string[] directories = RefPath.Split(Path.DirectorySeparatorChar);
                string value = "pdata";
                int pos = Array.IndexOf(directories, value);
                int l = pos - 1;
                int ds_n = pos - 2;
                Read_spectrum.DS_NAME = directories[ds_n];
                try
                {
                    if (RefPath.Contains(value))
                    { Read_spectrum.EXP_NUMBER = Convert.ToInt16(directories[l].Remove(directories[l].Length - 1)); }
                    // handle code in case user has custom path
                    else
                    { Read_spectrum.EXP_NUMBER = 0; }
                }

                catch (Exception pathexc)
                {
                    if (pathexc.InnerException != null)
                    {
                        string err1 = pathexc.InnerException.Message;
                        System.Windows.Forms.MessageBox.Show("Exception: " + Environment.NewLine + err1,
                        "Exception Handled",
                        MessageBoxButtons.OK);
                    }
                }

                Read_spectrum.PEAKLIST.Clear();
                XDocument CurrSpectrum = XDocument.Load(RefPath);
                var results_rfr = CurrSpectrum.Descendants("Peak2D");
                var ppinfonode = CurrSpectrum.Descendants().Where(n => n.Name == "PeakPickDetails").FirstOrDefault();
                if (ppinfonode != null) { Read_spectrum.PP_INFO = ppinfonode.Value; }
                else { Read_spectrum.PP_INFO = "Peak Picking Info not available"; }

                Read_spectrum.TOT_PEAKS = results_rfr.Count();

                // Xml parsing and values fetcher
                // Element position
                int i = 0;
                // Read Peak index
                int r;
                //int ra;
                foreach (var element in results_rfr)
                {
                    // Get the values
                    string F1coord = results_rfr.ElementAt(i).Attribute("F1").Value;
                    string F2coord = results_rfr.ElementAt(i).Attribute("F2").Value;
                    string intensityvalue = results_rfr.ElementAt(i).Attribute("intensity").Value;

                    // Need for internationalization
                    double F1d = double.Parse(F1coord, CultureInfo.InvariantCulture);
                    double F2d = double.Parse(F2coord, CultureInfo.InvariantCulture);
                    double intensityd = double.Parse(intensityvalue, CultureInfo.InvariantCulture);

                    // Assign Peaks 
                    bool test = F1d >= NMin && F1d <= NMax && F2d >= HMin && F2d <= HMax && intensityd >= Intensity_threshold;
                    if (test)
                    {
                        for (r = 0; r <= Read_spectrum.PEAKLIST.Count(); r++)
                        { /*ra = r + 1;*/ };

                        PEAK Read_peak = new PEAK
                        {
                            F1 = Math.Round(F1d, 5),
                            F2 = Math.Round(F2d, 5),
                            INTENSITY = Math.Round(intensityd, 0),
                            NUMBER = r + 1,
                            EXP_IDENTIFIER = Convert.ToString(Read_spectrum.EXP_NUMBER),
                        };
                        // Add only if PEAK exists in the import limits
                        Read_spectrum.PEAKLIST.Add(Read_peak);

                    }
                    // Sort PEAKLIST by PEAK.INTENSITY
                    Read_spectrum.PEAKLIST.OrderBy(PEAK => PEAK.INTENSITY);
                    i++;
                }
                Read_spectrum.TOT_READ_PEAKS = Read_spectrum.PEAKLIST.Count();
                return Read_spectrum;
            }
        }
        #endregion

        #region Load Parameters Method
        // LOAD PARAMETERS METHOD
        public void LoadParameters()
        {
            try
            {
                NMin = Convert.ToDouble(textBoxNMin.Text);
                NMax = Convert.ToDouble(textBoxNMax.Text);
                HMin = Convert.ToDouble(textBoxHMin.Text);
                HMax = Convert.ToDouble(textBoxHMax.Text);
                RefIntMin = Convert.ToDouble(textBoxRefInt.Text);
                DSIntMin = Convert.ToDouble(textBoxDSInt.Text);
            }

            catch (Exception e)
            {
                string excepLoad = e.Message;
                System.Windows.Forms.MessageBox.Show(excepLoad + Environment.NewLine + "" + Environment.NewLine +
                                                     "Import controls reset to default values.",
                                                     "Error in loading parameters",
                                                     MessageBoxButtons.OK,
                                                     MessageBoxIcon.Error);
                buttonResetIntToDefault.PerformClick();
                buttonResetImportToDefault.PerformClick();
                return;
            }

            // CHECKS
            // N interval
            double NRange = NMax - NMin;
            double HRange = HMax - HMin;

            if (NRange <= 0 || HRange <= 0)
            {
                string WrongRangeMessage = "Minimum value can not be equal to or higher than Maximum value." + Environment.NewLine +
                                           "" + Environment.NewLine +
                                           "Import controls reset to default values.";
                string WrongRangeTitle = "Check Import limits";
                MessageBoxButtons WrongRangeButtons = MessageBoxButtons.OK;
                MessageBoxIcon WrongRangeIcon = MessageBoxIcon.Error;
                System.Windows.Forms.MessageBox.Show(WrongRangeMessage, WrongRangeTitle, WrongRangeButtons, WrongRangeIcon);
                par_confirmed = false;
                buttonResetImportToDefault.PerformClick();
                return;
            }

            string threshold;
            if (IsReference == true)
            {
                threshold = textBoxRefInt.Text;
            }

            else
            {
                threshold = textBoxDSInt.Text;
            }

            string message = "Would you like to use these parameters?" + Environment.NewLine
                              + "N Frequency (F1) boundaries: " + Environment.NewLine
                              + "NMin: " + textBoxNMin.Text + "; NMax: " + textBoxNMax.Text + Environment.NewLine
                              + "H Frequency (F2) boundaries: " + Environment.NewLine
                              + "HMin: " + textBoxHMin.Text + "; HMax: " + textBoxHMax.Text + Environment.NewLine
                              + "Minimum Intensity threshold (AU): " + threshold;

            string msgboxtitle = "Confirm Import Parameters";
            MessageBoxButtons buttons = MessageBoxButtons.OKCancel;
            MessageBoxIcon boxIcon = MessageBoxIcon.Question;
            DialogResult result;

            result = System.Windows.Forms.MessageBox.Show(message, msgboxtitle, buttons, boxIcon);

            if (result == DialogResult.OK)
            {
                par_confirmed = true;
            }

            else
            {
                par_confirmed = false;
            }
        }

        // TEXTBOXES IMPORT CONTROL FORMAT CHECK
        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
                (e.KeyChar != '.'))
            {
                e.Handled = true;
            }

            // allow only one decimal point
            if ((e.KeyChar == '.') && ((sender as TextBox).Text.IndexOf('.') > -1))
            {
                e.Handled = true;
            }
        }
        #endregion

        #region Main Method
        public CSP_Main()
        {
            InitializeComponent();

            // MAIN CHART SPECTRA OVERLAY
            #region Chart Spectra Overlay SeriesCollection
            // Mapper
            ScatterSpectra = new SeriesCollection();

            ScatterSpectra.Add(new ScatterSeries()
            {
                Title = "Reference",
                Values = new ChartValues<ScatterPoint>(),
                Visibility = Visibility.Visible,
                MinPointShapeDiameter = 5,
                MaxPointShapeDiameter = 20,
                LabelPoint = point => "F2: " + -1 * Math.Round(point.X, 2) + ", F1: " + (-1 * Math.Round(point.Y, 2) + ", Int: " + point.Weight),
            });
            #endregion

            // BAR CHART MANUAL RESULTS
            #region Chart Bar Chart Manual Results
            // RESULTS CHART ADD SERIES;
            AnalysisResults = new SeriesCollection();

            AnalysisResults.Add(new ColumnSeries()
            {
                LabelsPosition = BarLabelPosition.Top,
                Foreground = ActiveManualForeground,
                Title = "Act. (man)",
                Values = new ChartValues<int>(),
                Fill = ActiveManualFill,
                DataLabels = true,
                LabelPoint = point => " " + point.Y,
            });

            AnalysisResults.Add(new ColumnSeries()
            {
                LabelsPosition = BarLabelPosition.Top,
                Foreground = InactiveManualForeground,
                Title = "Inact. (man)",
                Values = new ChartValues<int>(),
                Fill = InactiveManualFill,
                DataLabels = true,
                LabelPoint = point => " " + point.Y,
            });

            AnalysisResults.Add(new ColumnSeries()
            {
                LabelsPosition = BarLabelPosition.Top,
                Foreground = NotSetManualForeground,
                Title = "Not set (man)",
                Values = new ChartValues<int>(),
                Fill = NotSetManualFill,
                DataLabels = true,
                LabelPoint = point => " " + point.Y,
            });
            #endregion

            // CHART PEAKDIFF SERIES
            #region Chart Bar Chart Peak Difference


            Mapper2 = Mappers.Xy<ObservableValue>()
                     .X((item, index) => index)
                     .Y(item => item.Value)
                     .Fill(item => item.Value < -40 || item.Value > 40 ? DangerBrush : null);

            //var mappertest = Mappers.Xy<SPECTRUM>()
            //             .X(v => VALID_DS_SPECTRA[n].EXP_NUMBER)
            //             .Y(v => VALID_DS_SPECTRA[n].PEAK_DIFF)
            //             .Fill(v => VALID_DS_SPECTRA[n].PEAK_DIFF < -40 || VALID_DS_SPECTRA[n].PEAK_DIFF > 40 ? DangerBrush : null)
            //             .Stroke(v => VALID_DS_SPECTRA[n].EXP_NUMBER == n+1 ? ActiveAutoFill : null);

            PeakDiffSeries = new SeriesCollection(Mapper2);
            // WORKING
            PeakDiffSeries.Add(new ColumnSeries(Mapper2)
            {
                Title = "ΔPeaks",
                Values = Values,
                //Values = new ChartValues<int>(),
                LabelPoint = point => " " + point.Y,
                Fill = AllSpectraFillLight,
            });
            //WORKING - END
            #endregion
        }
        #endregion

        #region Define collections
        public CartesianMapper<double> mapper { get; set; }
        public SeriesCollection AnalysisResults { get; set; }
        public SeriesCollection ScatterSpectra { get; set; }
        public SeriesCollection ProbSeries { get; set; }
        public SeriesCollection PeakDiffSeries { get; set; }
        public CartesianMapper<ObservableValue> MapperProb { get; set; }
        public CartesianMapper<ObservableValue> Mapper2 { get; set; }
        public ChartValues<ObservableValue> Values { get; set; }
        #endregion

        #region Form Load/Resize/Initialize Events 
        // Initialize
        private void Initialize()
        {
            textBoxDSInt.Enabled = false;
            buttonSetPython.Visible = false;
            buttonSaveMCCstats.Enabled = false;
            progressBar1.Hide();
            textBox1.Visible = false;
            reference_loaded = false;
            ds_loaded = false;
            SPECTRA.Clear(); //clear spectra list
            EXP_NAMES.Clear();
            CSPrun_button.Enabled = false;
            load_ds_button.Enabled = false;
            ButtonFitZoom.Visible = false;
            Button_ResetZoomToImport.Visible = false;
            cartesianChart1.Visible = false;
            cartesianChart2.Visible = false;
            cartesianChartPeakDiff.Visible = false;
            cartesianChartResults.Visible = false;
            ButtonPREVIOUS.Enabled = false;
            ButtonFIRST.Enabled = false;
            ButtonNEXT.Enabled = false;
            ButtonLAST.Enabled = false;
            labelRefPPDetails.Enabled = false;
            labelExpPPDetails.Enabled = false;
            solidGaugeActives.Visible = false;
            solidGaugeInactives.Visible = false;
            checkBoxActives.Visible = false;
            checkBoxInactives.Visible = false;
            buttonMarkAsActive.Visible = false;
            buttonMarkAsInactive.Visible = false;
            buttonResetManualStatus.Visible = false;
            buttonResetAllManualFlags.Visible = false;
            labelAARes.Visible = false;
            labelMARes.Visible = false;
            buttonCancel.Visible = false;
            ZoomResetChartPeakDiff.Visible = false;
            ZoomResetChartProb.Visible = false;
            buttonExport.Enabled = false;
            tableLayoutPanel2.Visible = false;
        }

        // FORM LOAD
        private void Form1_Load(object sender, EventArgs e)
        {
            // Initialize
            backgroundWorker1.DoWork += backgroundWorker1_DoWork;
            backgroundWorker1.ProgressChanged += backgroundWorker1_ProgressChanged;
            backgroundWorker1.RunWorkerCompleted += backgroundWorker1_RunWorkerCompleted;  //Tell the user how the process went
            backgroundWorker1.WorkerReportsProgress = true;
            backgroundWorker1.WorkerSupportsCancellation = true; //Allow for the process to be cancelled
            Initialize();
        }

        // FORM RESIZE
        private void Form1_Resize(object sender, EventArgs e)
        {
            // Livecharts resize issue
            if (WindowState == FormWindowState.Minimized)
            {
                cartesianChart1.Dock = DockStyle.None;
                cartesianChart2.Dock = DockStyle.None;
                cartesianChartPeakDiff.Dock = DockStyle.None;
                cartesianChartResults.Dock = DockStyle.None;
            }

            else
            {
                cartesianChart1.Dock = DockStyle.Fill;
                cartesianChart2.Dock = DockStyle.Fill;
                cartesianChartPeakDiff.Dock = DockStyle.Fill;
                cartesianChartResults.Dock = DockStyle.Fill;
            }
        }
        #endregion

        #region Buttons Load Reference & Load Dataset
        // LOAD REFERENCE BUTTON
        private void Load_ref_Click(object sender, EventArgs e)
        {
            labelProgBar.Text = "  Loading Reference...";
            IsReference = true;
            // Load current import parameters
            LoadParameters();
            if (!par_confirmed)
            {
                labelProgBar.Text = " No Reference Loaded";
                return;
            }
            else
                // clear spectra list and generate instance
                SPECTRA.Clear();
            SPECTRUM Read_spectrum = new SPECTRUM();

            // Open File Dialog path and ext handling DO NOT DELETE! 
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml",
                FilterIndex = 0,
                DefaultExt = "xml"
            };

            // Error loading handling
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                if (!string.Equals(Path.GetExtension(ofd.FileName),
                                   ".xml",
                                   StringComparison.OrdinalIgnoreCase))
                {
                    // Invalid file type selected; display an error.
                    System.Windows.Forms.MessageBox.Show("The type of the selected file is not supported by this application. You must select a valid XML file.",
                                    "Invalid File Type",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    // Optionally, force the user to select another file.
                }
                else { reference_loaded = true; tableLayoutPanel2.Visible = true; }
            }

            else
            {
                reference_loaded = false;
                labelProgBar.Text = " No Reference Loaded";
                return;
            }

            if (reference_loaded == true)
            {
                // PARSING XML FILES
                XmlDocument refofd = new XmlDocument();
                refofd.Load(ofd.FileName);

                /// SHOW REFERENCE INFO ON LOADING
                ///int s;
                ///XmlNodeList xmlnode;
                ///xmlnode = refofd.GetElementsByTagName("PeakList");
                ///s = 0;
                ///string str = null;
                ///XmlNode xmlnode1 = refofd.DocumentElement.SelectSingleNode("PeakList2D/Peak2D");
                ///
                ///for (s = 0; s <= xmlnode.Count - 1; s++)
                ///{
                ///    xmlnode[s].ChildNodes.Item(0);
                ///    str = refofd.DocumentElement.InnerText + "  ";
                ///    System.Windows.Forms.MessageBox.Show(str, "Reference Peak Picking info");
                ///}
                ///

                // The Path to the .Xml file //
                string RefPath = ofd.FileName;
                // ADD REFERENCE
                SPECTRA.Add(Read_spectrum.Read_spectrum(RefPath, RefIntMin, NMin, NMax, HMin, HMax));

                // Update labels 
                if (SPECTRA[0].PEAKLIST.Any())
                {
                    labelRefPeaks.Text = Convert.ToString(SPECTRA[0].PEAKLIST.Count());
                    labelRefMinInt.Text = Convert.ToString(Math.Round(SPECTRA[0].PEAKLIST.Min(PEAK => PEAK.INTENSITY), 0));
                    labelRefMaxInt.Text = Convert.ToString(Math.Round(SPECTRA[0].PEAKLIST.Max(PEAK => PEAK.INTENSITY), 0));

                    // Enable Controls
                    labelRefPPDetails.Enabled = true;
                    Button_ResetZoomToImport.Visible = true;
                    ButtonFitZoom.Visible = true;
                    load_ref_button.Text = "  Reference Loaded";
                    load_ref_button.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
                    load_ref_button.Enabled = false;
                    load_ref_button.ForeColor = System.Drawing.Color.LightGray;
                    labelProgBar.Text = "  Reference Loaded";
                    labelProgBar.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                    cartesianChart2.Show();
                    cartesianChart2.Enabled = true;
                    // Disable MI Reference Threshold
                    textBoxRefInt.Enabled = false;
                    textBoxDSInt.Enabled = true;

                    cartesianChart2.Series = ScatterSpectra;

                    foreach (PEAK peak in SPECTRA[0].PEAKLIST)
                    {
                        cartesianChart2.Series[0].Values.Add(new ScatterPoint((-1 * peak.F2), (-1 * peak.F1), peak.INTENSITY));
                    }

                    graph_config();

                    load_ds_button.Enabled = true;
                    return;
                }

                else
                {
                    // Invalid file type selected; display an error.
                    System.Windows.Forms.MessageBox.Show("No peaks found in the Reference Peaklist. The program stopped. Check the current import limits or the sanity of the Reference \"peaklist.xml\" file.",
                                    "No Peaks found for Reference!",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    reference_loaded = false;
                    labelProgBar.Text = " No Reference Loaded";
                    return;
                }
            }
            return;
        }

        // LOAD DATASET BUTTON
        private void load_ds_button_Click(object sender, EventArgs e)
        {
            labelProgBar.Text = " Loading Dataset...";
            IsReference = false;
            LoadParameters();

            if (!par_confirmed)
            {
                labelProgBar.Text = " Ref OK - No DS Loaded";
                return;
            }
            else
                EXP_NAMES.Clear();
            VALID_DS_SPECTRA.Clear();
            VALID_EXP.Clear();
            FAULT_EXP.Clear();
            OOR.Clear();

            // Open File dialog and pass path to list
            FolderBrowserDialog loadDS = new FolderBrowserDialog();

            // Error handling
            if (loadDS.ShowDialog() == DialogResult.OK && reference_loaded)
            {
                // All directories to LIST
                List<string> temp;
                List<string> sorted = new List<string>();
                temp = Directory.GetDirectories(loadDS.SelectedPath).ToList();
                foreach (string name in temp)
                {   //mod 1012
                    if (name.EndsWith("1") && File.Exists(name + "\\pdata\\1\\peaklist.xml"))
                        sorted.Add(name.Remove(name.Length - 1));
                }
                sorted.Sort();
                foreach (string name in sorted)
                {
                    EXP_NAMES.Add(name+1);
                }

                // Update label
                labelDSTotSubfolders.Text = Convert.ToString(temp.Count());

                foreach (string dir in EXP_NAMES)
                {   //mod 1012
                    if (File.Exists(dir + "\\pdata\\1\\peaklist.xml"))
                    {
                        ds_loaded = true;
                    }
                }

                if (ds_loaded == false)
                {
                    System.Windows.Forms.MessageBox.Show("No experiments were found in this folder!",
                                                         "Warning: No Experiments Found!",
                                                         MessageBoxButtons.OK,
                                                         MessageBoxIcon.Error);
                    CSPrun_button.Enabled = false;
                    labelProgBar.Text = "  No Dataset Loaded";
                    EXP_NAMES.Clear();
                }

                if (ds_loaded == true)
                {
                    labelProgBar.Text = " Loading DS Spectra";
                    Cursor.Current = Cursors.AppStarting;

                    foreach (string dir in temp)
                    {
                        //mod 1012
                        if (File.Exists(dir + "\\pdata\\1\\peaklist.xml"))
                        {
                            VALID_EXP.Add(dir);
                            // Update label loadValidNo
                            labelValidNo.Text = Convert.ToString(VALID_EXP.Count());
                        }

                        else
                        {
                            if (dir.EndsWith("1"))
                                FAULT_EXP.Add(dir);
                            // Update label loadFaultNo
                            labelFalutNo.Text = Convert.ToString(FAULT_EXP.Count());
                        }
                    }

                    // Add SPECTRA to lists
                    Add_DSSpectra();
                    label_OORExp_No.Text = Convert.ToString(OOR.Count());
                    labelTotExp.Text = Convert.ToString(VALID_DS_SPECTRA.Count());
                    label_ValidExp.Text = Convert.ToString(VALID_DS_SPECTRA.Count());
                    List<double> min_int_PEAKS = new List<double>();
                    List<double> max_int_PEAKS = new List<double>();
                    List<int> Peak_count = new List<int>();
                    VALID_DS_SPECTRA.Sort((p, q) => p.EXP_NUMBER.CompareTo(q.EXP_NUMBER));
                    Values = new ChartValues<ObservableValue> { };
                    foreach (SPECTRUM vs in VALID_DS_SPECTRA)
                    {
                        // Find Min/Max intensity and calculate peak difference
                        min_int_PEAKS.Add(vs.PEAKLIST.Min(PEAK => PEAK.INTENSITY));
                        max_int_PEAKS.Add(vs.PEAKLIST.Max(PEAK => PEAK.INTENSITY));
                        Peak_count.Add(vs.TOT_PEAKS);
                        vs.PEAK_DIFF = vs.TOT_READ_PEAKS - SPECTRA[0].TOT_READ_PEAKS;
                        // 
                        Values.Add(new ObservableValue(vs.PEAK_DIFF));
                    }

                    //Values.Count();
                    PeakDiffSeries[0].Values = Values;
                    // Update labels
                    if (min_int_PEAKS.Any() && ds_loaded)
                    {
                        labelDS_PeakCount_AVG.Text = Convert.ToString(Math.Round(Peak_count.Average(), 0));
                        labelDS_min_int_AVG.Text = Convert.ToString(Math.Round(min_int_PEAKS.Average(), 0));
                        labelDS_max_int_AVG.Text = Convert.ToString(Math.Round(max_int_PEAKS.Average(), 0));

                        System.Windows.Media.Color newColor = System.Windows.Media.Color.FromArgb(150, 254, 236, 138);

                        ScatterSpectra.Add(new ScatterSeries()
                        {
                            Title = VALID_DS_SPECTRA[0].DS_NAME + " All",
                            Values = new ChartValues<ScatterPoint>(),
                            Visibility = Visibility.Visible,
                            MinPointShapeDiameter = 5,
                            MaxPointShapeDiameter = 20,
                            // Working line:
                            LabelPoint = point => "Exp. " + VALID_DS_SPECTRA[n].EXP_NUMBER + " F2: " + -1 * Math.Round(point.X, 2) + ", F1: " + -1 * Math.Round(point.Y, 2) + ", Int: " + point.Weight,
                            Fill = AllSpectraFill,
                            StrokeThickness = 0
                        });

                        // Update player, controls and labels
                        update_player();
                        update_graphs();
                        UpdateOverlayLabels();
                        labelExpPPDetails.Enabled = true;
                        load_ds_button.Enabled = false;
                        load_ds_button.Text = VALID_DS_SPECTRA[0].DS_NAME;
                        load_ds_button.Font = new System.Drawing.Font("Century Gothic", 6.5F);
                        load_ds_button.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                        //if (VALID_DS_SPECTRA.Count > 40) { LabelAngle = 90; }
                        buttonMarkAsActive.Visible = true;
                        buttonMarkAsInactive.Visible = true;
                        buttonResetManualStatus.Visible = true;
                        buttonExport.Enabled = true;

                        // Manual Analysis chart and controls
                        textBox1.Visible = true;
                        cartesianChartPeakDiff.Visible = true;
                        config_chartPeakDiff();
                        buttonResetAllManualFlags.Visible = true;
                        cartesianChartResults.Visible = true;
                        labelMARes.Visible = true;
                        ZoomResetChartPeakDiff.Visible = true;
                        results_graph_config();
                        update_results_chart();

                        // Disable textboxes for import control
                        textBoxNMin.Enabled = false;
                        textBoxNMax.Enabled = false;
                        textBoxHMin.Enabled = false;
                        textBoxHMax.Enabled = false;
                        textBoxDSInt.Enabled = false;
                        buttonResetImportToDefault.Enabled = false;
                        buttonResetIntToDefault.Enabled = false;
                        labelProgBar.Text = "Looking for python.exe...";
                    }

                    // Error handling in case no peaklist.xml is found
                    if (FAULT_EXP.Count() > 0)
                    {
                        var message1 = string.Join(Environment.NewLine, FAULT_EXP);
                        System.Windows.Forms.MessageBox.Show("No experiments were found in this folder: " + message1,
                                                             "Warning: No Experiments Found!",
                                                              MessageBoxButtons.OK,
                                                              MessageBoxIcon.Warning);
                        //labelStatus.Text = "Looking for python.exe...";
                    }

                    // Check For python.exe
                    if (!File.Exists(pythonpath) && ds_loaded)
                    {
                        PythonFinder();
                    }

                    else { pythonset = true; pythonexe = pythonpath; CSPrun_button.Enabled = true; labelProgBar.Text = "   Ready for Analysis"; }
                }
            }
            else { CSPrun_button.Enabled = false; ds_loaded = false; labelProgBar.Text = "  Ref OK - No DS Loaded"; }
            Cursor.Current = Cursors.Default;
        }

        // ADD SPECTRA TO LIST METHOD
        private void Add_DSSpectra()
        {
            foreach (string val in VALID_EXP)
            {
                // Generate instance
                SPECTRUM read_spectrum = new SPECTRUM();
                read_spectrum = read_spectrum.Read_spectrum((val + "\\pdata\\1\\peaklist.xml"), DSIntMin, NMin, NMax, HMin, HMax);

                // PEAKLIST ERROR OUT OF RANGE 
                bool isEmpty = !read_spectrum.PEAKLIST.Any();

                if (!isEmpty)
                {
                    SPECTRA.Add(read_spectrum);
                    VALID_DS_SPECTRA.Add(read_spectrum);
                    nmin = 0;
                    nmax = VALID_DS_SPECTRA.Count();
                }

                else
                {
                    OOR.Add(read_spectrum);
                }
            }
        }

        // PATH CHECK FOR PYTHON.EXE
        private void PythonFinder()
        {
            pythonpath = null;
            pythonset = false;
            labelProgBar.Text = "Looking for python.exe...";

            string message = "The program was not able to automatically find the Python Executable. This is required to run the Automated analysis." + Environment.NewLine +
                             "Would you like to manually locate the \"python.exe\" file?" + Environment.NewLine +
                             "" + Environment.NewLine +
                             "NOTICE: the CSP analysis uses Python 3.x or above.";
            string msgboxtitle = "Unable to find python.exe";
            MessageBoxButtons buttons = MessageBoxButtons.OKCancel;
            MessageBoxIcon boxIcon = MessageBoxIcon.Warning;
            DialogResult result;
            result = System.Windows.Forms.MessageBox.Show(message, msgboxtitle, buttons, boxIcon);
            if (result == DialogResult.OK)
            {
                labelProgBar.Text = "Looking for python.exe...";
                OpenFileDialog setpy = new OpenFileDialog()
                {
                    Filter = "EXE Files (*.exe)|*.exe",
                    FilterIndex = 0,
                    DefaultExt = "exe"
                };

                if (setpy.ShowDialog() == DialogResult.OK && setpy.FileName.Contains("\\python"))
                {
                    pythonexe = setpy.FileName;
                    pythonset = true;
                    labelProgBar.Text = "   Pythonpath set!";
                    CSPrun_button.Enabled = true;
                    labelProgBar.Text = "   Ready for Analysis";
                    buttonSetPython.Visible = false;
                }

                else
                {
                    CSPrun_button.Enabled = false;
                    labelProgBar.Text = "Python path not set." + Environment.NewLine + "CSP Analysis Unavailable.";
                    buttonSetPython.Visible = true;
                    string pymessage = "The path to \"python.exe\" was incorrect or not properly set." + Environment.NewLine +
                                                       "" + Environment.NewLine +
                                                       "The CSP Automatic analysis will be unavailable.";
                    string pymsgboxtitle = "Python path not set";
                    MessageBoxButtons pybuttons = MessageBoxButtons.OK;
                    MessageBoxIcon pyboxIcon = MessageBoxIcon.Stop;
                    DialogResult pyresult;
                    pyresult = System.Windows.Forms.MessageBox.Show(pymessage, pymsgboxtitle, pybuttons, pyboxIcon);
                    if (result == DialogResult.OK)
                    {
                        return;
                    }
                }
            }

            if (result == DialogResult.Cancel)
            {
                labelProgBar.Text = "Python path not set." + Environment.NewLine + "CSP Analysis Unavailable";
                buttonSetPython.Visible = true;

                string pymessage = "The path to \"python.exe\" was incorrect or not properly set." + Environment.NewLine +
                                   "" + Environment.NewLine +
                                   "The CSP Automatic analysis will be unavailable.";
                string pymsgboxtitle = "No path to python.exe set!";
                MessageBoxButtons pybuttons = MessageBoxButtons.OK;
                MessageBoxIcon pyboxIcon = MessageBoxIcon.Stop;
                DialogResult pyresult;
                pyresult = System.Windows.Forms.MessageBox.Show(pymessage, pymsgboxtitle, pybuttons, pyboxIcon);
                if (result == DialogResult.OK)
                {
                    CSPrun_button.Enabled = false;
                    labelProgBar.Text = "Python path not set." + Environment.NewLine + "CSP Analysis Unavailable.";
                    buttonSetPython.Visible = true;
                }
            }
        }
        #endregion

        #region CSP Run
        // RUN CSP ANALYSIS BUTTON
        private void CSPrun_button_Click(object sender, EventArgs e)
        {
            labelProgBar.Hide();
            progressBar1.Show();
            buttonCancel.Visible = true;
            buttonCancel.Enabled = true;

            if (tempfolder == null)
            {
                string message = "The program was not able to automatically load the default temporary files folder. Would you like to manually set a different path?";
                string msgboxtitle = "Unable to find temporary folder";
                MessageBoxButtons buttons = MessageBoxButtons.OKCancel;
                MessageBoxIcon boxIcon = MessageBoxIcon.Warning;
                DialogResult result;

                result = System.Windows.Forms.MessageBox.Show(message, msgboxtitle, buttons, boxIcon);
                if (result == DialogResult.OK)
                {
                    FolderBrowserDialog settemp = new FolderBrowserDialog();
                    //settemp.ShowDialog();
                    if (settemp.ShowDialog() == DialogResult.OK)
                        tempfolder = settemp.SelectedPath;
                    else { buttonCancel.Enabled = false; return; }
                }

                else
                {
                    buttonCancel.Enabled = false;
                    return;
                }

            }

            /// BACKGROUND WORKER GOES HERE
            if (tempfolder != "")
            {
                CSPrun_button.Enabled = false;
                backgroundWorker1.RunWorkerAsync();
            }

            else { CSPrun_button.Enabled = true; }
            ///
        }

        // BACKGROUND WORKER DOWORK
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            //Clear existing processed spectra
            File.Delete(Path.Combine(@tempfolder) + "\\" + "processed_spectra.json");
            for (int i = 0; i < 100; i++)
            {
                Thread.Sleep(20);
                backgroundWorker1.ReportProgress(i);

                //Check if there is a request to cancel the process
                if (backgroundWorker1.CancellationPending)
                {
                    e.Cancel = true;
                    backgroundWorker1.ReportProgress(0);
                    return;
                }
            }

            JavaScriptSerializer json_spectrum = new JavaScriptSerializer();
            SPECTRA[0].JSON_Data = "Reference";
            foreach (SPECTRUM js in SPECTRA.Skip(1))
            {
                js.JSON_Data = "Experiment";
            }
            string alljsonspectra;
            alljsonspectra = json_spectrum.Serialize(SPECTRA);

            // PREPARE PATHS AND JSON 
            File.WriteAllText(Path.Combine(@tempfolder) + "\\" + VALID_DS_SPECTRA[0].DS_NAME + ".json", alljsonspectra);
            
            
            // ESCAPE SPACES FOR CMD ARGUMENTS
            string AppStartupPath = @System.Windows.Forms.Application.StartupPath;
            string cmdlike;
            if (AppStartupPath.Contains(" "))
            {
                cmdlike = AppStartupPath.Replace(" ", "^ ");
            }
            
            else { cmdlike = AppStartupPath; }

            // 1ST arg: ACTIVATE CONDA ENVIRONMENT
            string activateEnvCmd = "\"" + AppStartupPath + @"\Miniconda3\Scripts\" + "\"" + "activate.bat py36_csp";
            // 2ND arg: PYTHON.EXE
            pythonexe = "\"" + AppStartupPath + @"\Miniconda3\" + "\"" + "python.exe";
            // 3RD arg: PYTHON SCRIPT
            string pyScript = "\"" + AppStartupPath + "\\" + "NMR_classifier_production.py" + "\"";
            // 4th arg: JSON LOCATION
            string jsonin = "\"" + Path.Combine(@tempfolder) + VALID_DS_SPECTRA[0].DS_NAME + ".json" + "\"";
            // RUN CMD full string
            string strCmdText = "/c " + "\"" + @activateEnvCmd + " && " + pythonexe + " " + pyScript + " " + jsonin + " \"";
            // the '/c' parameter autocloses the cmd window, used for release version
            // the '/k' parameter keeps the cmd open, used for debug purposes; DO NOT USE WITH process.StartInfo.CreateNoWindow = true!

            run_cmd(strCmdText);

            // If the process exits the loop, ensure that progress is set to 100%
            // Remember in the loop we set i < 100 so in theory the process will complete at 99%
            backgroundWorker1.ReportProgress(100);
        }

        // CALL PYTHON SCRIPT
        private void run_cmd(string cmd)
        {
            Process process = new Process();
            process.StartInfo.FileName = @"C:\Windows\System32\cmd.exe";
            process.StartInfo.Arguments = cmd;
            //comment the next 2 lines for debug, in this way the command is silent in the background
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();
        }

        // CANCEL RUN
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            CSPrun_button.Enabled = true;
            buttonCancel.Enabled = false;

            //Check if background worker is doing anything and send a cancellation if it is
            if (backgroundWorker1.IsBusy)
            {
                backgroundWorker1.CancelAsync();
                CSPrun_button.Enabled = true;
            }
        }

        // WORKER STATUS
        private void backgroundWorker1_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
            lblStatus.Text = "Setting up Python Environment";
            if (e.ProgressPercentage > 50)
            {
                lblStatus.Text = " Running Python Classifier";
                buttonCancel.Enabled = false;
            }
        }

        public class SPECTRA_RESULTS
        {
            public int EXP_NUMBER { get; set; }
            public bool isActive { get; set; }
            public float activePseudoprobability { get; set; }
        }

        // BACKGROUND WORKER COMPLETED - Process Script data
        private void backgroundWorker1_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                lblStatus.Text = "        Analysis Canceled";
                lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            }
            else if (e.Error != null)
            {
                lblStatus.Text = "  Error. The thread aborted.";
                lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                buttonCancel.Enabled = true;
            }
            else
            {
                lblStatus.Text = "        Analysis complete";
                buttonSaveMCCstats.Enabled = true;

                // JSON OUTPUT PROCESSING
                try
                {
                    var pathToJson = Path.Combine(Path.Combine(@tempfolder) + "\\" + "processed_spectra" + ".json");
                    var jsonout = new StreamReader(pathToJson);
                    string myJson = jsonout.ReadToEnd();
                    myJson = Regex.Replace(myJson, "(\\[true\\])", "true");
                    myJson = Regex.Replace(myJson, "(\\[false\\])", "false");
                    List<SPECTRA_RESULTS> PROCESSED_SPECTRA = JsonConvert.DeserializeObject<List<SPECTRA_RESULTS>>(myJson);

                    foreach (SPECTRA_RESULTS sr in PROCESSED_SPECTRA)
                    {
                        foreach (SPECTRUM s in VALID_DS_SPECTRA)
                        {
                            if (s.EXP_NUMBER == sr.EXP_NUMBER)
                            {
                                s.Prob = Convert.ToDouble(sr.activePseudoprobability);
                                s.isActive = sr.isActive;
                            }
                        }
                    }

                    foreach (SPECTRUM s in VALID_DS_SPECTRA)
                    {
                        if (s.isActive)
                        { ACTIVES.Add(s); }
                        if (!s.isActive)
                        { INACTIVES.Add(s); }
                    }
                }

                catch (Exception conda)
                {
                    string ErrorConda = conda.Message;
                    System.Windows.Forms.MessageBox.Show(ErrorConda + Environment.NewLine + "" + Environment.NewLine +
                                                         "Something went wrong with setting up the Conda Environment. " +
                                                         "Please check the Application Path, the System Environment variables or " +
                                                         "simply try to reboot your system." + Environment.NewLine +
                                                         "You can still use Manual Analysis.",
                                                         "Analysis Output not found",
                                                         MessageBoxButtons.OK,
                                                         MessageBoxIcon.Error);
                    lblStatus.Text = "Error with Python Conda Env.";
                    buttonSaveMCCstats.Enabled = false;
                }

                // ADD SERIES TO OVERLAY
                ScatterSpectra.Add(new ScatterSeries()
                {
                    Title = "Actives",
                    Values = new ChartValues<ScatterPoint>(),
                    Visibility = Visibility.Visible,
                    MinPointShapeDiameter = 5,
                    MaxPointShapeDiameter = 20,
                    // Working line:
                    LabelPoint = point => "Exp. " + ACTIVES[n].EXP_NUMBER + " F2: " + -1 * Math.Round(point.X, 2) + ", F1: " + -1 * Math.Round(point.Y, 2) + ", Int: " + point.Weight,
                    Fill = ActiveAutoFill,
                    StrokeThickness = 0
                });

                // ADD SERIES TO OVERLAY
                ScatterSpectra.Add(new ScatterSeries()
                {
                    Title = "Inactives",
                    Values = new ChartValues<ScatterPoint>(),
                    Visibility = Visibility.Visible,
                    MinPointShapeDiameter = 5,
                    MaxPointShapeDiameter = 20,
                    // Working line:
                    LabelPoint = point => "Exp. " + INACTIVES[n].EXP_NUMBER + " F2: " + -1 * Math.Round(point.X, 2) + ", F1: " + -1 * Math.Round(point.Y, 2) + ", Int: " + point.Weight,
                    Fill = InactiveAutoFill,
                    StrokeThickness = 0
                });

                // UPDATE RUN INFO LABELS
                labelStatus.Visible = true;
                AnalysisDone = true;

                // UPDATE INTERFACE
                update_player();
                update_graphs();
                UpdateOverlayLabels();

                // UPDATE CONTROLS
                labelAARes.Visible = true;
                CSPrun_button.Enabled = false;
                CSPrun_button.Text = "Done";
                buttonCancel.Visible = false;

                // BIND DATA TO CHART PROBABILITY
                try
                {
                    // Mapper
                    MapperProb = Mappers.Xy<ObservableValue>()
                              .X((item, index) => index)
                              .Y(item => item.Value)
                              .Fill(item => item.Value >= ProbThreshold ? ActiveAutoFill : null);

                    // CHART 1 Prob distribution
                    ProbSeries = new SeriesCollection(MapperProb);

                    ProbSeries.Add(new ColumnSeries()
                    {
                        Title = "Probability",
                        Values = new ChartValues<ObservableValue>(),
                        LabelPoint = point => "" + Math.Round(point.Y, 2),
                        Fill = InactiveAutoFill,
                    });

                    ZoomResetChartProb.Visible = true;

                    foreach (SPECTRUM vs in VALID_DS_SPECTRA)
                    {
                        ProbSeries[0].Values.Add(new ObservableValue(vs.Prob));

                        #region Update Gauge Actives/Inactives
                        if (vs.isActive == true)
                        {
                            // ACTIVES.Add(vs);

                            // GAUGE UPDATE - CONVERT TO METHOD
                            solidGaugeActives.Visible = true;
                            checkBoxActives.Visible = true;
                            solidGaugeActives.From = 0;
                            solidGaugeActives.To = VALID_DS_SPECTRA.Count();
                            solidGaugeActives.Value = ACTIVES.Count();
                            solidGaugeActives.HighFontSize = 25;
                            solidGaugeActives.FromColor = Colors.LimeGreen;
                            solidGaugeActives.ToColor = System.Windows.Media.Color.FromArgb(76, 29, 195, 88);
                            solidGaugeActives.ForeColor = System.Drawing.Color.Black;
                            solidGaugeActives.GaugeBackground = GaugeBackgroundBrush;
                            solidGaugeActives.Base.Foreground = System.Windows.Media.Brushes.White;
                        }

                        else
                        {
                            // INACTIVES.Add(vs);
                            solidGaugeInactives.Visible = true;
                            checkBoxInactives.Visible = true;
                            solidGaugeInactives.From = 0;
                            solidGaugeInactives.To = VALID_DS_SPECTRA.Count();
                            solidGaugeInactives.Value = INACTIVES.Count();
                            solidGaugeInactives.HighFontSize = 25;
                            solidGaugeInactives.FromColor = Colors.WhiteSmoke;
                            solidGaugeInactives.ToColor = System.Windows.Media.Color.FromArgb(50, 200, 0, 0);
                            solidGaugeInactives.ForeColor = System.Drawing.Color.Red;
                            solidGaugeInactives.GaugeBackground = GaugeBackgroundBrush;
                            solidGaugeInactives.Base.Foreground = System.Windows.Media.Brushes.White;
                        }
                        #endregion
                    }

                    #region Chart Bar Chart Probability
                    cartesianChart1.Visible = true;
                    cartesianChart1_config();
                    if (ACTIVES.Count() > 0)
                    {
                        ProbThreshold = ACTIVES.Min(Prob => Prob.Prob);
                        cartesianChart1.VisualElements.Add(new VisualElement
                        {
                            X = VALID_DS_SPECTRA.Count() / 2,
                            Y = ProbThreshold,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Top,
                            UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                            {
                                Text = "Decision Threshold",
                                FontWeight = FontWeights.Bold,
                                Foreground = TextBrushVisualElement,
                                FontSize = 12,
                                Opacity = 0.6
                            }
                        });
                    }
                    else
                    {
                        ProbThreshold = 0.5;
                        cartesianChart1.VisualElements.Add(new VisualElement
                        {
                            X = VALID_DS_SPECTRA.Count() / 2,
                            Y = ProbThreshold,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Top,
                            UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                            {
                                Text = "No Actives Found",
                                FontWeight = FontWeights.Bold,
                                Foreground = InactiveManualForeground,
                                FontSize = 12,
                                Opacity = 0.6
                            }
                        });
                    }
                    
                  


                    cartesianChart1.VisualElements.Add(new VisualElement
                    {
                        X = n,
                        Y = VALID_DS_SPECTRA[n].Prob,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top,
                        UIElement = new System.Windows.Controls.TextBlock //notice this property must be a wpf control
                        {
                            Text = "Current Spectrum" + Environment.NewLine +
                                   "              | ",
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            Foreground = TextBrushVisualElement,
                            FontSize = 10,
                        }
                    });

                    cartesianChart1.VisualElements.Count();

                    cartesianChart1.AxisY[0].Sections.Add(new AxisSection
                    {
                        Value = ProbThreshold,
                        Stroke = System.Windows.Media.Brushes.YellowGreen,
                        StrokeThickness = 0.5,
                        StrokeDashArray = new DoubleCollection(new[] { 10d })
                    });
                    #endregion

                    // CALCULATE HIT RATE
                    float act = ACTIVES.Count();
                    float tot = VALID_DS_SPECTRA.Count();
                    float hr = (act / tot) * 100;

                    string hrtext = Convert.ToString(Math.Round(hr, 2));
                    labelEstHR.Text = hrtext;
                }

                catch (Exception z)
                {
                    if (z.InnerException != null)
                    {
                        string err = z.InnerException.Message;
                    }
                }
            }
        }
        #endregion

        #region Update UI Graphs/Labels/Player/ManualResultsChart
        // METHOD UPDATE OVERLAY CHART
        public void update_graphs()
        {
            cartesianChart2.Series = ScatterSpectra;
            cartesianChart2.Update();
            if (ShowActives && !ShowInactives && ACTIVES.Any())
            {
                ScatterSpectra[1].Values.Clear();
                ScatterSpectra[2].Values.Clear();

                if (n >= 0 && n <= nmax && nmax > 0)
                {
                    foreach (PEAK apeak in ACTIVES[n].PEAKLIST)
                    {
                        ScatterSpectra[2].Values.Add(new ScatterPoint((-1 * apeak.F2), (-1 * apeak.F1), apeak.INTENSITY));
                    }
                };
                IndexBarCharts = VALID_DS_SPECTRA.IndexOf(ACTIVES[n]);
                cartesianChartPeakDiff.VisualElements[6].X = IndexBarCharts;
                cartesianChartPeakDiff.VisualElements[6].Y = VALID_DS_SPECTRA[IndexBarCharts].PEAK_DIFF;
                try
                {
                    cartesianChart1.VisualElements[1].X = IndexBarCharts;
                    cartesianChart1.VisualElements[1].Y = VALID_DS_SPECTRA[IndexBarCharts].Prob;
                }
                catch { }
            }

            else if (ShowActives == false && ACTIVES.Any())
            {
                ScatterSpectra[2].Values.Clear();
            }

            if (ShowInactives == true && !ShowActives && INACTIVES.Any())
            {
                nmax = INACTIVES.Count();
                ScatterSpectra[1].Values.Clear();
                ScatterSpectra[2].Values.Clear();
                ScatterSpectra[3].Values.Clear();

                if (n >= 0 && n <= nmax && nmax > 0)
                {
                    foreach (PEAK ipeak in INACTIVES[n].PEAKLIST)
                    {
                        ScatterSpectra[3].Values.Add(new ScatterPoint((-1 * ipeak.F2), (-1 * ipeak.F1), ipeak.INTENSITY));
                    }
                }
                IndexBarCharts = VALID_DS_SPECTRA.IndexOf(INACTIVES[n]);
                cartesianChartPeakDiff.VisualElements[6].X = IndexBarCharts;
                cartesianChartPeakDiff.VisualElements[6].Y = VALID_DS_SPECTRA[IndexBarCharts].PEAK_DIFF;

                try
                {
                    cartesianChart1.VisualElements[1].X = IndexBarCharts;
                    cartesianChart1.VisualElements[1].Y = VALID_DS_SPECTRA[IndexBarCharts].Prob;
                }
                catch { }
            }

            else if (ShowInactives == false && INACTIVES.Any())
            {
                ScatterSpectra[3].Values.Clear();
            }

            if (ShowActives == false && ShowInactives == false || ShowActives && ShowInactives)
            {
                nmax = VALID_DS_SPECTRA.Count();

                if (n >= 0 && n <= nmax && nmax > 0)
                {
                    cartesianChart2.Series = ScatterSpectra;
                    ScatterSpectra[1].Values.Clear();
                    if (ShowActives && ShowInactives)
                    { ScatterSpectra[2].Values.Clear(); }

                    foreach (PEAK peak in VALID_DS_SPECTRA[n].PEAKLIST)
                    {
                        ScatterSpectra[1].Values.Add(new ScatterPoint((-1 * peak.F2), (-1 * peak.F1), peak.INTENSITY));
                    }
                }
                try
                {
                    IndexBarCharts = n;
                    cartesianChartPeakDiff.VisualElements[6].X = IndexBarCharts;
                    cartesianChartPeakDiff.VisualElements[6].Y = VALID_DS_SPECTRA[IndexBarCharts].PEAK_DIFF;
                    cartesianChart1.VisualElements[1].X = IndexBarCharts;
                    cartesianChart1.VisualElements[1].Y = VALID_DS_SPECTRA[IndexBarCharts].Prob;
                }
                catch { }
            }
        }

        // METHOD UPDATE PLAYER INDEX
        public void update_player()
        {
            if (nmax == 1)
            {
                ButtonNEXT.Enabled = false;
                ButtonLAST.Enabled = false;
                ButtonPREVIOUS.Enabled = false;
                ButtonFIRST.Enabled = false;
            }

            if (n == 0 && nmax > 1)
            {
                ButtonFIRST.Enabled = false;
                ButtonPREVIOUS.Enabled = false;
                ButtonLAST.Enabled = true;
                ButtonNEXT.Enabled = true;
            }

            if (n > 0 && n < nmax && nmax > 1)
            {
                ButtonFIRST.Enabled = true;
                ButtonNEXT.Enabled = true;
                ButtonPREVIOUS.Enabled = true;
                ButtonLAST.Enabled = true;
            }

            if (n == nmax - 1 && nmax > 1)
            {
                ButtonNEXT.Enabled = false;
                ButtonLAST.Enabled = false;
                ButtonPREVIOUS.Enabled = true;
                ButtonFIRST.Enabled = true;
            }
        }

        // METHOD UPDATE OVERLAY LABELS 
        private void UpdateOverlayLabels()
        {
            labelCurrExpMinInt.Text = VALID_DS_SPECTRA[n].PEAKLIST.Min(PEAK => PEAK.INTENSITY).ToString();
            labelCurrExpMaxInt.Text = VALID_DS_SPECTRA[n].PEAKLIST.Max(PEAK => PEAK.INTENSITY).ToString();
            labelCurrExpTotPeaks.Text = VALID_DS_SPECTRA[n].TOT_READ_PEAKS.ToString();
            labelCurrExpNo.Text = Convert.ToString(VALID_DS_SPECTRA[n].EXP_NUMBER);
            textBox1.Text = Convert.ToString(VALID_DS_SPECTRA[n].EXP_NUMBER);
            labelManStatus.Text = VALID_DS_SPECTRA[n].UserSelection;

            labelManStatus.Text = VALID_DS_SPECTRA[n].UserSelection;
            if (VALID_DS_SPECTRA[n].UserSelection == "ACTIVE (MAN)")
            {
                labelManStatus.ForeColor = System.Drawing.Color.LimeGreen;
                textBox1.ForeColor = System.Drawing.Color.LimeGreen;
            }

            if (VALID_DS_SPECTRA[n].UserSelection == "INACTIVE (MAN)")
            {
                labelManStatus.ForeColor = System.Drawing.Color.Red;
                textBox1.ForeColor = System.Drawing.Color.Red;
            }

            if (VALID_DS_SPECTRA[n].UserSelection == "Not set")
            {
                labelManStatus.ForeColor = System.Drawing.Color.Gray;
                textBox1.ForeColor = System.Drawing.Color.DarkSlateGray;
            }

            if (AnalysisDone)
            {
                if (VALID_DS_SPECTRA[n].isActive)
                {
                    labelStatus.Text = "ACTIVE";
                    labelStatus.ForeColor = System.Drawing.Color.LimeGreen;
                    labelCurrExpNo.ForeColor = System.Drawing.Color.LimeGreen;
                }

                else
                {
                    labelStatus.Text = "INACTIVE";
                    labelStatus.ForeColor = System.Drawing.Color.Red;
                    labelCurrExpNo.ForeColor = System.Drawing.Color.Red;
                }
            }

            else
            {
                labelStatus.Text = "Run analysis";
            }

            int delta;
            delta = VALID_DS_SPECTRA[n].PEAK_DIFF;
            if (delta > 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.LimeGreen; }
            if (delta == 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.Gray; }
            if (delta < 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.Red; }
            labelCounter.Text = (n + 1).ToString() + " / " + nmax.ToString();
        }

        // METHOD UPDATE OVERLAY LABELS WHEN AUTO ACTIVES ARE SHOWN
        private void UpdateOverlayLabelsActives()
        {
            labelCurrExpMinInt.Text = ACTIVES[n].PEAKLIST.Min(PEAK => PEAK.INTENSITY).ToString();
            labelCurrExpMaxInt.Text = ACTIVES[n].PEAKLIST.Max(PEAK => PEAK.INTENSITY).ToString();
            labelCurrExpTotPeaks.Text = ACTIVES[n].TOT_READ_PEAKS.ToString();
            labelCurrExpNo.Text = Convert.ToString(ACTIVES[n].EXP_NUMBER);
            textBox1.Text = Convert.ToString(ACTIVES[n].EXP_NUMBER);

            labelManStatus.Text = ACTIVES[n].UserSelection;
            if (ACTIVES[n].UserSelection == "ACTIVE (MAN)")
            {
                labelManStatus.ForeColor = System.Drawing.Color.LimeGreen;
                textBox1.ForeColor = System.Drawing.Color.LimeGreen;
            }

            if (ACTIVES[n].UserSelection == "INACTIVE (MAN)")
            {
                labelManStatus.ForeColor = System.Drawing.Color.Red;
                textBox1.ForeColor = System.Drawing.Color.Red;
            }

            if (ACTIVES[n].UserSelection == "Not set")
            {
                labelManStatus.ForeColor = System.Drawing.Color.Gray;
                textBox1.ForeColor = System.Drawing.Color.DarkSlateGray;
            }

            labelStatus.Text = "ACTIVE";
            labelStatus.ForeColor = System.Drawing.Color.LimeGreen;
            labelCurrExpNo.ForeColor = System.Drawing.Color.LimeGreen;

            int delta;
            delta = ACTIVES[n].PEAK_DIFF;
            if (delta > 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.LimeGreen; }
            if (delta == 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.Gray; }
            if (delta < 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.Red; }
            labelCounter.Text = (n + 1).ToString() + " / " + nmax.ToString();
        }

        // METHOD UPDATE OVERLAY LABELS WHEN AUTO INACTIVES ARE SHOWN
        private void UpdateOverlayLabelsInactives()
        {
            labelCurrExpMinInt.Text = INACTIVES[n].PEAKLIST.Min(PEAK => PEAK.INTENSITY).ToString();
            labelCurrExpMaxInt.Text = INACTIVES[n].PEAKLIST.Max(PEAK => PEAK.INTENSITY).ToString();
            labelCurrExpTotPeaks.Text = INACTIVES[n].TOT_READ_PEAKS.ToString();
            labelCurrExpNo.Text = Convert.ToString(INACTIVES[n].EXP_NUMBER);
            textBox1.Text = Convert.ToString(INACTIVES[n].EXP_NUMBER);

            labelManStatus.Text = INACTIVES[n].UserSelection;
            if (INACTIVES[n].UserSelection == "ACTIVE (MAN)")
            {
                labelManStatus.ForeColor = System.Drawing.Color.LimeGreen;
                textBox1.ForeColor = System.Drawing.Color.LimeGreen;
            }

            if (INACTIVES[n].UserSelection == "INACTIVE (MAN)")
            {
                labelManStatus.ForeColor = System.Drawing.Color.Red;
                textBox1.ForeColor = System.Drawing.Color.Red;
            }

            if (INACTIVES[n].UserSelection == "Not set")
            {
                labelManStatus.ForeColor = System.Drawing.Color.Gray;
                textBox1.ForeColor = System.Drawing.Color.DarkSlateGray;
            }

            labelStatus.Text = "INACTIVE";
            labelStatus.ForeColor = System.Drawing.Color.Red;
            labelCurrExpNo.ForeColor = System.Drawing.Color.Red;

            int delta;
            delta = INACTIVES[n].PEAK_DIFF;
            if (delta > 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.LimeGreen; }
            if (delta == 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.Gray; }
            if (delta < 0) { labelDeltaPeaks.Text = delta.ToString(); labelDeltaPeaks.ForeColor = System.Drawing.Color.Red; }
            labelCounter.Text = (n + 1).ToString() + " / " + nmax.ToString();
        }

        // METHOD UPDATE RESULTS BAR CHART
        private void update_results_chart()
        {
            cartesianChartResults.Series = AnalysisResults;
            // Reset values
            for (int i = 0; i <= 2; i++)
                AnalysisResults[i].Values.Clear();

            AnalysisResults[0].Values.Add(MAN_ACTIVES.Count());
            AnalysisResults[1].Values.Add(MAN_INACTIVES.Count());
            int notset = 0;
            foreach (SPECTRUM s in VALID_DS_SPECTRA)
            {
                if (s.UserSelection == "Not set")
                {
                    notset++;
                }
            }
            AnalysisResults[2].Values.Add(notset);
        }
        #endregion

        #region CheckBoxes Actives/Inactives controls
        private void CheckBoxActives_CheckedChanged(Object sender, EventArgs e)
        {
            ShowActives = true;
            n = 0;
            nmax = ACTIVES.Count();

            update_player();
            update_graphs();
            UpdateOverlayLabelsActives();

            if (checkBoxActives.Checked == false)
            {
                ShowActives = false;
                n = 0;
                nmax = VALID_DS_SPECTRA.Count();

                update_player();
                update_graphs();
                UpdateOverlayLabels();
            }

            if (checkBoxActives.Checked == true && ShowInactives)
            {
                //Reset checkboxes
                checkBoxInactives.Checked = false;
                checkBoxActives.Checked = false;
                reset_checkbox_state();

                update_player();
                update_graphs();
                UpdateOverlayLabels();

            }
        }

        private void CheckBoxInactive_CheckedChanged(Object sender, EventArgs e)
        {
            ShowInactives = true;
            n = 0;
            nmax = INACTIVES.Count();

            update_player();
            update_graphs();
            UpdateOverlayLabelsInactives();

            if (checkBoxInactives.Checked == false)
            {
                ShowInactives = false;
                n = 0;
                nmax = VALID_DS_SPECTRA.Count();
                update_graphs();
                update_player();
                UpdateOverlayLabels();
            }

            if (checkBoxInactives.Checked == true && ShowActives)
            {
                // Reset checkboxes
                checkBoxInactives.Checked = false;
                checkBoxActives.Checked = false;
                reset_checkbox_state();
                update_graphs();
                update_player();
                UpdateOverlayLabels();
            }
        }

        private void reset_checkbox_state()
        {
            ShowActives = false;
            ShowInactives = false;
            checkBoxInactives.Checked = false;
            checkBoxActives.Checked = false;
        }
        #endregion

        #region Player Controls
        // BUTTON NEXT
        private void ButtonNEXT_Click(object sender, EventArgs e)
        {
            if (ShowInactives && !ShowActives)
            {
                n++;
                update_player();
                update_graphs();
                UpdateOverlayLabelsInactives();
            }

            if (ShowActives && !ShowInactives)
            {
                n++;
                update_player();
                update_graphs();
                UpdateOverlayLabelsActives();
            }

            if (ShowActives && ShowInactives || !ShowInactives && !ShowActives)
            {
                n++;
                update_player();
                update_graphs();
                UpdateOverlayLabels();
            }
        }

        // BUTTON PREVIOUS
        private void ButtonPREVIOUS_Click(object sender, EventArgs e)
        {
            n--;
            if (ShowInactives && !ShowActives)
            {
                update_player();
                update_graphs();
                UpdateOverlayLabelsInactives();
            }

            if (ShowActives && !ShowInactives)
            {
                update_player();
                update_graphs();
                UpdateOverlayLabelsActives();
            }

            if (ShowActives && ShowInactives || !ShowInactives && !ShowActives)
            {
                update_player();
                update_graphs();
                UpdateOverlayLabels();
            }

        }

        // BUTTON LAST
        private void ButtonLAST_Click(object sender, EventArgs e)
        {
            n = nmax - 1;
            if (ShowInactives && !ShowActives)
            {
                update_player();
                update_graphs();
                UpdateOverlayLabelsInactives();
            }

            if (ShowActives && !ShowInactives)
            {
                update_player();
                update_graphs();
                UpdateOverlayLabelsActives();
            }

            if (ShowActives && ShowInactives || !ShowInactives && !ShowActives)
            {
                update_player();
                update_graphs();
                UpdateOverlayLabels();
            }
        }

        // BUTTON FIRST
        private void ButtonFIRST_Click(object sender, EventArgs e)
        {
            if (ShowInactives && !ShowActives && nmax > 1)
            {
                n = 0;
                update_player();
                update_graphs();
                UpdateOverlayLabelsInactives();
            }

            if (ShowActives && !ShowInactives && nmax > 1)
            {
                n = 0;
                update_player();
                update_graphs();
                UpdateOverlayLabelsActives();
            }

            if (ShowActives && ShowInactives && nmax > 1 || !ShowInactives && !ShowActives && nmax > 1)
            {
                n = 0;
                update_player();
                update_graphs();
                UpdateOverlayLabels();
            }
        }
        #endregion

        #region GoTo Experiment controls
        // TEXTBOX FORMAT CHECK
        private void textBox1_TextChanged(object sender, EventArgs e)
        {

            textBox1.ForeColor = System.Drawing.Color.Black;
            if (Regex.IsMatch(textBox1.Text, "[^0-9]"))
            {
                System.Windows.Forms.MessageBox.Show("Please enter only numbers.");
                textBox1.Text = textBox1.Text.Remove(textBox1.Text.Length - 1);
            }
        }

        // TEXTBOX CONFIRM INPUT
        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (textBox1.Text != "" && e.KeyCode == Keys.Enter)
            {
                try
                {
                    gotoexp = Convert.ToInt32(textBox1.Text);
                }

                catch
                {

                }

                go_to_experiment();
                update_player();
                update_graphs();
                UpdateOverlayLabels();
            }
        }

        // GOTO METHOD
        public void go_to_experiment()
        {
            found = false;
            reset_checkbox_state();
            foreach (SPECTRUM vs in VALID_DS_SPECTRA)
            {

                if (vs.EXP_NUMBER == gotoexp)
                {
                    n = VALID_DS_SPECTRA.IndexOf(vs);
                    textBox1.ForeColor = System.Drawing.Color.Black;
                    textBox1.BackColor = System.Drawing.Color.White;
                    found = true;
                }
            }

            if (!found)
            {
                textBox1.ForeColor = System.Drawing.Color.Brown;
                textBox1.BackColor = System.Drawing.Color.PaleVioletRed;
                System.Windows.Forms.MessageBox.Show("Experiment " + textBox1.Text + " not found");
                textBox1.Clear();
                textBox1.BackColor = System.Drawing.Color.White;
                n = 0;
            }
        }
        #endregion

        #region Zoom Controls
        // CHART SPECTRA OVERLAY ZOOM TO IMPORT LIMITS
        public void ZoomReset()
        {
            cartesianChart2.AxisX[0].MinValue = -1 * HMax;
            cartesianChart2.AxisX[0].MaxValue = -1 * HMin;
            cartesianChart2.AxisY[0].MinValue = -1 * NMax;
            cartesianChart2.AxisY[0].MaxValue = -1 * NMin;
        }

        // CHART SPECTRA OVERLAY ZOOM FIT
        public void ZoomFit()
        {
            cartesianChart2.AxisX[0].MinValue = -1 * (SPECTRA[0].PEAKLIST.Max(PEAK => PEAK.F2) + 0.5);
            cartesianChart2.AxisX[0].MaxValue = -1 * (SPECTRA[0].PEAKLIST.Min(PEAK => PEAK.F2) - 0.5);
            cartesianChart2.AxisY[0].MinValue = -1 * (SPECTRA[0].PEAKLIST.Max(PEAK => PEAK.F1) + 3);
            cartesianChart2.AxisY[0].MaxValue = -1 * (SPECTRA[0].PEAKLIST.Min(PEAK => PEAK.F1) - 3);
        }

        // BUTTON RESET ZOOM TO IMPORT (SPECTRA OVERLAY)
        private void Button_ResetZoomToImport_Click(object sender, EventArgs e)
        {
            ZoomReset();
        }

        // BUTTON FIT ZOOM TO REFERENCE (SPECTRA OVERLAY)
        private void ButtonFitZoom_Click(object sender, EventArgs e)
        {
            ZoomFit();
        }

        // CHART BAR CHART PEAK DIFFERENCE RESET ZOOM
        private void ZoomResetChartPeakDiff_Click(object sender, EventArgs e)
        {

            cartesianChartPeakDiff.AxisX[0].MinValue = 0;
            cartesianChartPeakDiff.AxisX[0].MaxValue = VALID_DS_SPECTRA.Count;
            try
            {
                cartesianChart1.AxisX[0].MinValue = 0;
                cartesianChart1.AxisX[0].MaxValue = VALID_DS_SPECTRA.Count;
            }
            catch (Exception nograph)
            {

            }
        }

        // CHART BAR CHART PROBABILITY DISTRIBUTION RESET ZOOM
        private void ZoomResetChartProb_Click(object sender, EventArgs e)
        {
            cartesianChart1.AxisX[0].MinValue = 0;
            cartesianChart1.AxisX[0].MaxValue = VALID_DS_SPECTRA.Count;
            cartesianChartPeakDiff.AxisX[0].MinValue = 0;
            cartesianChartPeakDiff.AxisX[0].MaxValue = VALID_DS_SPECTRA.Count;
        }

        // CHART PEAK DIFF & PROB ZOOM SYNC
        private void Axis_RangeChanged(LiveCharts.Events.RangeChangedEventArgs eventArgs)
        {
            try
            {
                //sync the graphs
                double min = ((Axis)eventArgs.Axis).MinValue;
                double max = ((Axis)eventArgs.Axis).MaxValue;

                this.cartesianChart1.AxisX[0].MinValue = min;
                this.cartesianChart1.AxisX[0].MaxValue = max;

                this.cartesianChartPeakDiff.AxisX[0].MinValue = min;
                this.cartesianChartPeakDiff.AxisX[0].MaxValue = max;
            }

            catch (Exception e)
            {

            }
        }
        #endregion

        #region Chart OnDataClick
        // CHART PROBABILITY ON DATA CLICK
        private void ChartOnDataClick(object sender, ChartPoint p)
        {
            checkBoxInactives.Checked = false;
            checkBoxActives.Checked = false;
            // get VALID_DS_SPECTRA index
            index = Convert.ToInt16(p.X);
            n = index;
            update_graphs();
            update_player();
            UpdateOverlayLabels();

            /// OLD ON-CHART CLICK
            ///int index;
            ///index = Convert.ToInt16(p.SeriesView.Title);
            /// // get VALID_DS_SPECTRA index
            ///n = VALID_DS_SPECTRA.FindIndex(VALID_DS_SPECTRA => VALID_DS_SPECTRA.EXP_NUMBER == index);
            /// //var res = VALID_DS_SPECTRA.Find(x => x.EXP_NUMBER == index);
            ///var asPixels = cartesianChart1.Base.ConvertToPixels(p.AsPoint());
            /// //string test = cartesianChart1.DataTooltip.
            /// //n = VALID_DS_SPECTRA.IndexOf(p.SeriesView);
            ///update_graphs();
            ///update_player();
            ///UpdateOverlayLabels();
            ///
        }

        // CHART PEAK DIFFERENCE  ON DATA CLICK
        private void ChartPeakDiff_OnDataClick(object sender, ChartPoint p)
        {
            checkBoxInactives.Checked = false;
            checkBoxActives.Checked = false;

            index = Convert.ToInt16(p.X);
            // get VALID_DS_SPECTRA index
            n = index;
            update_graphs();
            update_player();
            UpdateOverlayLabels();
        }
        #endregion

        #region Labels Click events
        // LABEL FAULT EXPERIMENTS 
        private void loadDSFaultyExp_Click(object sender, EventArgs e)
        {
            if (FAULT_EXP.Any())
            {
                var message1 = string.Join(Environment.NewLine, FAULT_EXP);
                System.Windows.Forms.MessageBox.Show("No experiments were found in this folder: " + message1,
                                                     "Warning: No Experiments Found!",
                                                      MessageBoxButtons.OK,
                                                      MessageBoxIcon.Warning);
            }
        }

        // LABEL OUT OF RANGE EXPERIMENTS
        private void label_OORExp_Click(object sender, EventArgs e)
        {
            if (OOR.Any())
            {
                List<string> oors = new List<string>();
                foreach (var oornumb in OOR) { oors.Add(Convert.ToString(oornumb.EXP_NUMBER)); }
                var message = string.Join(Environment.NewLine, oors);
                System.Windows.Forms.MessageBox.Show("Unable to fetch peaks in current import range for experiment: " + message,
                                                     "Warning: Peaks out of import range!",
                                                      MessageBoxButtons.OK,
                                                      MessageBoxIcon.Warning);
            }
        }

        // LABEL REFERENCE PEAK PICKING INFO
        private void label32_Click(object sender, EventArgs e)
        {
            string title = "Reference Peak Picking info";
            string message = SPECTRA[0].PP_INFO;

            System.Windows.MessageBox.Show(message,
                                           title,
                                           MessageBoxButton.OK,
                                           MessageBoxImage.Information);
        }

        // LABEL CURRENT EXPERIMENT PEAK PICKING INFO
        private void label33_Click(object sender, EventArgs e)
        {
            string title = "Experiment " + VALID_DS_SPECTRA[n].EXP_NUMBER + " Peak Picking Info";
            string message = VALID_DS_SPECTRA[n].PP_INFO;

            System.Windows.MessageBox.Show(message,
                                           title,
                                           MessageBoxButton.OK,
                                           MessageBoxImage.Information);
        }
        #endregion

        #region Buttons Manual UserSelection
        // MARK AS ACTIVE BUTTON
        private void buttonMarkAsActive_Click(object sender, EventArgs e)
        {
            if (ShowActives)
            {
                if (ACTIVES[n].UserSelection == "Not set")
                {
                    ACTIVES[n].UserSelection = "ACTIVE (MAN)";
                    MAN_ACTIVES.Add(ACTIVES[n]);
                    UpdateOverlayLabelsActives();
                }

                if (ACTIVES[n].UserSelection == "INACTIVE (MAN)")
                {
                    ACTIVES[n].UserSelection = "ACTIVE (MAN)";
                    MAN_INACTIVES.Remove(ACTIVES[n]);
                    MAN_ACTIVES.Add(ACTIVES[n]);
                    UpdateOverlayLabelsActives();
                }
            }

            if (ShowInactives)
            {
                if (INACTIVES[n].UserSelection == "Not set")
                {
                    INACTIVES[n].UserSelection = "ACTIVE (MAN)";
                    MAN_ACTIVES.Add(INACTIVES[n]);
                    UpdateOverlayLabelsInactives();
                }

                if (INACTIVES[n].UserSelection == "INACTIVE (MAN)")
                {
                    INACTIVES[n].UserSelection = "ACTIVE (MAN)";
                    MAN_ACTIVES.Add(INACTIVES[n]);
                    MAN_INACTIVES.Remove(INACTIVES[n]);
                    UpdateOverlayLabelsInactives();
                }
            }

            if (!ShowActives && !ShowInactives)
            {
                if (VALID_DS_SPECTRA[n].UserSelection == "Not set")
                {
                    VALID_DS_SPECTRA[n].UserSelection = "ACTIVE (MAN)";
                    MAN_ACTIVES.Add(VALID_DS_SPECTRA[n]);
                    UpdateOverlayLabels();
                }

                if (VALID_DS_SPECTRA[n].UserSelection == "INACTIVE (MAN)")
                {
                    MAN_INACTIVES.Remove(VALID_DS_SPECTRA[n]);
                    MAN_ACTIVES.Add(VALID_DS_SPECTRA[n]);
                    VALID_DS_SPECTRA[n].UserSelection = "ACTIVE (MAN)";
                    UpdateOverlayLabels();
                }
            }
            update_results_chart();
        }

        // MARK AS INACTIVE BUTTON
        private void buttonMarkAsInactive_Click(object sender, EventArgs e)
        {
            if (ShowActives)
            {
                if (ACTIVES[n].UserSelection == "Not set")
                {
                    ACTIVES[n].UserSelection = "INACTIVE (MAN)";
                    MAN_INACTIVES.Add(ACTIVES[n]);
                    UpdateOverlayLabelsActives();
                }

                if (ACTIVES[n].UserSelection == "ACTIVE (MAN)")
                {
                    MAN_ACTIVES.Remove(ACTIVES[n]);
                    ACTIVES[n].UserSelection = "INACTIVE (MAN)";
                    MAN_INACTIVES.Add(ACTIVES[n]);
                    UpdateOverlayLabelsActives();
                }
            }

            if (ShowInactives)
            {
                if (INACTIVES[n].UserSelection == "Not set")
                {
                    INACTIVES[n].UserSelection = "INACTIVE (MAN)";
                    MAN_INACTIVES.Add(INACTIVES[n]);
                    UpdateOverlayLabelsInactives();
                }

                if (INACTIVES[n].UserSelection == "ACTIVE (MAN)")
                {
                    MAN_ACTIVES.Remove(INACTIVES[n]);
                    MAN_INACTIVES.Add(INACTIVES[n]);
                    INACTIVES[n].UserSelection = "INACTIVE (MAN)";
                    UpdateOverlayLabelsInactives();
                }
                UpdateOverlayLabelsInactives();
            }

            if (!ShowActives && !ShowInactives)
            {
                if (VALID_DS_SPECTRA[n].UserSelection == "Not set")
                {
                    VALID_DS_SPECTRA[n].UserSelection = "INACTIVE (MAN)";
                    MAN_INACTIVES.Add(VALID_DS_SPECTRA[n]);
                    UpdateOverlayLabels();
                }

                if (VALID_DS_SPECTRA[n].UserSelection == "ACTIVE (MAN)")
                {
                    MAN_INACTIVES.Add(VALID_DS_SPECTRA[n]);
                    MAN_ACTIVES.Remove(VALID_DS_SPECTRA[n]);
                    VALID_DS_SPECTRA[n].UserSelection = "INACTIVE (MAN)";
                    UpdateOverlayLabels();
                }
            }
            update_results_chart();
        }

        // MARK AS NOT SET BUTTON
        private void buttonResetManualStatus_Click(object sender, EventArgs e)
        {
            if (ShowActives)
            {
                if (ACTIVES[n].UserSelection == "ACTIVE (MAN)")
                {
                    MAN_ACTIVES.Remove(ACTIVES[n]);
                    ACTIVES[n].UserSelection = "Not set";
                }

                if (ACTIVES[n].UserSelection == "INACTIVE (MAN)")
                {
                    MAN_INACTIVES.Remove(ACTIVES[n]);
                    ACTIVES[n].UserSelection = "Not set";
                }
                UpdateOverlayLabelsActives();
            }

            if (ShowInactives)
            {
                if (INACTIVES[n].UserSelection == "ACTIVE (MAN)")
                {
                    MAN_ACTIVES.Remove(INACTIVES[n]);
                    INACTIVES[n].UserSelection = "Not set";
                }

                if (INACTIVES[n].UserSelection == "INACTIVE (MAN)")
                {
                    MAN_INACTIVES.Remove(INACTIVES[n]);
                    INACTIVES[n].UserSelection = "Not set";
                }
                UpdateOverlayLabelsInactives();
            }

            if (!ShowActives && !ShowInactives)
            {
                if (VALID_DS_SPECTRA[n].UserSelection == "ACTIVE (MAN)")
                {
                    MAN_ACTIVES.Remove(VALID_DS_SPECTRA[n]);
                    VALID_DS_SPECTRA[n].UserSelection = "Not set";
                }
                if (VALID_DS_SPECTRA[n].UserSelection == "INACTIVE (MAN)")
                {
                    MAN_INACTIVES.Remove(VALID_DS_SPECTRA[n]);
                    VALID_DS_SPECTRA[n].UserSelection = "Not set";
                }
                UpdateOverlayLabels();
            }
            update_results_chart();
        }

        // RESET ALL MANUAL FLAGS
        private void buttonResetAllManualFlags_Click(object sender, EventArgs e)
        {
            string title = "Manual Flag Reset";
            string message = "Are you sure you want to reset your manual selection?" + Environment.NewLine +
                             " " + Environment.NewLine
                             + "WARNING: All the Spectra Manual Flags will be reset to \"Not set\".";

            if (System.Windows.Forms.MessageBox.Show(message,
                                                     title,
                                                     MessageBoxButtons.YesNo,
                                                     MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                foreach (SPECTRUM s in VALID_DS_SPECTRA)
                {
                    s.UserSelection = "Not set";
                }

                if (ShowActives) { MAN_ACTIVES.Clear(); MAN_INACTIVES.Clear(); UpdateOverlayLabelsActives(); update_results_chart(); }
                if (ShowInactives) { MAN_ACTIVES.Clear(); MAN_INACTIVES.Clear(); UpdateOverlayLabelsInactives(); update_results_chart(); }
                if (!ShowInactives && !ShowActives) { MAN_ACTIVES.Clear(); MAN_INACTIVES.Clear(); UpdateOverlayLabels(); update_results_chart(); }
            }
        }
        #endregion

        #region Export Data
        // BUTTON EXPORT CLICK
        private void buttonExport_Click(object sender, EventArgs e)
        {
            //FormOutputTable f3 = new FormOutputTable(this);
            //f3.passlist(this.VALID_DS_SPECTRA);
            //f3.Show();

            // working line
            buttonExport.Enabled = false;
            FormOutputTable f3 = new FormOutputTable(process);
            //f3 = new FormOutputTable();
            //f3.Owner = this;
            f3.FormClosing += new FormClosingEventHandler(ChildFormClosing);
            ////convert to method
            refresh_list_f3();
            //
            f3.Show();
            f3.GenerateTable();
            //f3.TopMost = true;
        }

        // ON FORM OUTPUT TABLE CLOSE
        private void ChildFormClosing(object sender, FormClosingEventArgs e)
        {
            buttonExport.Enabled = true;
        }

        // SEND DATA TO NEW FORM METHOD
        private void refresh_list_f3()
        {
            process.Clear();
            process.Add(SPECTRA[0]);
            foreach (SPECTRUM s in VALID_DS_SPECTRA)
                process.Add(s);
            var list2pass = new FormOutputTable(process);
            //f3.Update();
        }
        #endregion

        #region Buttons Help/Details/Reset Controls
        // HELP BUTTON
        private void help_button_Click(object sender, EventArgs e)
        {
            try
            {

                FormHelp FormHelp = new FormHelp();
                FormHelp.ShowDialog();
                FormHelp.TopMost = true;
            }

            catch (Exception pp)
            {

            }
        }

        // DETAILS BUTTON
        private void details_button_Click(object sender, EventArgs e)
        {
            Form2 f2 = new Form2();
            f2.ShowDialog();
            f2.TopMost = true;
        }

        // RESET BUTTON
        private void buttonReset_Click(object sender, EventArgs e)
        {
            string title = "Application Reset";
            string message = "Are you sure you want to reset the input?" + Environment.NewLine +
                             " " + Environment.NewLine
                             + "WARNING: All unsaved data will be lost.";

            if (System.Windows.Forms.MessageBox.Show(message,
                                                     title,
                                                     MessageBoxButtons.YesNo,
                                                     MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                System.Windows.Forms.Application.Restart();
            }
        }

        // SHORTCUTS BUTTON
        private void button1_Click(object sender, EventArgs e)
        {
            FormShortcuts shortcuts = new FormShortcuts();
            shortcuts.ShowDialog();
            shortcuts.TopMost = true;
        }

        // BUTTON EXPORT MCC SAVE 
        private void buttonSaveMCCstats_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog saveDialog = new FolderBrowserDialog();
            //saveDialog.Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*";
            //saveDialog.FilterIndex = 2;

            if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //remember to change this to ryan's pdf out
                MCCReportSource = Path.Combine(@tempfolder + "\\" + "processed_spectra" + ".json");
                MCCReportDestination = Path.Combine(saveDialog.SelectedPath + "\\" + VALID_DS_SPECTRA[0].DS_NAME + "_processed" + ".json");
                File.Copy(@MCCReportSource, MCCReportDestination, true);
                System.Windows.Forms.MessageBox.Show("MCC Statistics Report saved to:" + Environment.NewLine + MCCReportDestination, "MCC Statistics saved successfully", MessageBoxButtons.OK, MessageBoxIcon.Information);
                MCCsaved = true;
            }

            if (MCCsaved == true)
            {
                buttonSaveMCCstats.Click -= buttonSaveMCCstats_Click;
                buttonSaveMCCstats.Click += buttonSaveMCCstats_Click2;
                buttonSaveMCCstats.Text = " Show in Explorer";
            }
        }

        // BUTTON EXPORT MCC SAVE CLICK 2
        private void buttonSaveMCCstats_Click2(object sender, EventArgs e)
        {
            if (!File.Exists(MCCReportDestination))
            {
                return;
            }

            string argument = "/select, \"" + MCCReportDestination + "\"";
            Process.Start("explorer.exe", argument);
        }

        // BUTTON RESET IMPORT LIMITS TO DEFAULT
        private void buttonResetImportToDefault_Click(object sender, EventArgs e)
        {
            textBoxNMin.Text = "100";
            textBoxNMax.Text = "140";
            textBoxHMax.Text = "12";
            textBoxHMin.Text = "5";

            stopasking = false;
        }

        // BUTTON RESET MIN INTENSITIES TO DEFAULT
        private void buttonResetIntToDefault_Click(object sender, EventArgs e)
        {
            textBoxRefInt.Text = "5000";
            textBoxDSInt.Text = "2000";
        }
        #endregion

        #region Shortcuts
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            #region Player Shortcuts
            // Focus on GoTo Experiment
            if (keyData == (Keys.G))
            {
                textBox1.Focus();
            }

            // Next
            if (keyData == Keys.Right)
            {
                ButtonNEXT.PerformClick();
            }

            // Previous
            if (keyData == Keys.Left)
            {
                ButtonPREVIOUS.PerformClick();
                //return true;
            }

            // Last
            if (keyData == Keys.Down)
            {
                ButtonLAST.PerformClick();
            }

            // First
            if (keyData == Keys.Up)
            {
                ButtonFIRST.PerformClick();
            }
            #endregion

            #region Data I/O Buttons
            // Reset Button
            if (keyData == (Keys.Control | Keys.R))
            {
                buttonReset.PerformClick();
            }

            // Export Button
            if (keyData == (Keys.Control | Keys.E))
            {
                buttonExport.PerformClick();
            }

            // Load Reference Button
            if (keyData == (Keys.Enter) && load_ds_button.Enabled == false)
            {
                load_ref_button.PerformClick();
            }

            // Load Dataset Button
            else if (keyData == Keys.Enter && load_ds_button.Enabled)
            {
                load_ds_button.PerformClick();
                return true;
            }

            // About Button

            if (keyData == (Keys.I))
            {
                details_button.PerformClick();
            }

            // Help Button
            if (keyData == Keys.H)
            {
                help_button.PerformClick();
            }

            // Run CSP Button
            if (keyData == Keys.R)
            {
                CSPrun_button.PerformClick();
            }

            // Run Cancel Button
            if (keyData == Keys.T)
            {
                buttonCancel.PerformClick();
            }

            // Application Quit
            if (keyData == (Keys.Control | Keys.Q))
            {
                string message = "Are you sure you would like to Exit?" + Environment.NewLine +
                                 "" + Environment.NewLine +
                                 "WARNING: All unsaved data will be lost.";

                string msgboxtitle = "Confirm Exit";
                MessageBoxButtons buttons = MessageBoxButtons.OKCancel;
                MessageBoxIcon boxIcon = MessageBoxIcon.Warning;
                DialogResult result;

                result = System.Windows.Forms.MessageBox.Show(message, msgboxtitle, buttons, boxIcon);
                if (result == DialogResult.OK)
                    this.Close();
            }
            #endregion

            #region Manual Flag Control Buttons
            // Reset All Manual Flags Button
            if (keyData == Keys.N)
            {
                buttonResetAllManualFlags.PerformClick();
            }

            // Mark as Active Button
            if (keyData == Keys.A)
            {
                buttonMarkAsActive.PerformClick();
            }

            // Mark as Inactive Button
            if (keyData == Keys.S)
            {
                buttonMarkAsInactive.PerformClick();
            }

            // Mark as Not set Button
            if (keyData == Keys.D)
            {
                buttonResetManualStatus.PerformClick();
            }
            #endregion

            #region Import Default
            // Reset Import limits to default
            if (keyData == (Keys.Alt | Keys.Control | Keys.I))
            {
                buttonResetImportToDefault.PerformClick();
            }

            // Reset Intensity thresholds to default
            if (keyData == (Keys.Alt | Keys.Control | Keys.T))
            {
                buttonResetIntToDefault.PerformClick();
            }

            // Reset all import textboxes to default
            if (keyData == (Keys.Alt | Keys.Control | Keys.O))
            {
                buttonResetIntToDefault.PerformClick();
                buttonResetImportToDefault.PerformClick();
            }
            #endregion

            #region Zoom Control
            // Reset all Zoom
            if (keyData == (Keys.Alt | Keys.Control | Keys.Space))
            {
                Button_ResetZoomToImport.PerformClick();
                ZoomResetChartPeakDiff.PerformClick();
                ZoomResetChartProb.PerformClick();
            }

            // Zoom to import limits
            if (keyData == (Keys.Control | Keys.Y))
            {
                Button_ResetZoomToImport.PerformClick();
            }

            // Fit to reference
            if (keyData == (Keys.Control | Keys.X))
            {
                ButtonFitZoom.PerformClick();
            }

            // Reset Bar charts Zoom
            if (keyData == (Keys.Control | Keys.C))
            {
                ZoomResetChartPeakDiff.PerformClick();
                ZoomResetChartProb.PerformClick();
            }
            #endregion

            #region Spectra Overlay Show Auto 
            // Show Auto Actives
            if (keyData == (Keys.Control | Keys.A) && checkBoxActives.Visible)
            {
                if (!checkBoxActives.Checked)
                {
                    checkBoxActives.Checked = true;
                }
                else { checkBoxActives.Checked = false; }
            }

            // Show Auto Inactives
            if (keyData == (Keys.Control | Keys.I) && checkBoxInactives.Visible)
            {
                if (!checkBoxInactives.Checked)
                {
                    checkBoxInactives.Checked = true;
                }
                else { checkBoxInactives.Checked = false; }
            }
            #endregion

            #region Info/Miscellaneous
            // Show Ref PP info
            if (keyData == (Keys.Control | Keys.Alt | Keys.R) && reference_loaded == true)
            {
                string title = "Reference Peak Picking info";
                string message = SPECTRA[0].PP_INFO;

                System.Windows.MessageBox.Show(message,
                                               title,
                                               MessageBoxButton.OK,
                                               MessageBoxImage.Information);
            }

            // Show Curr Exp PP Info
            if (keyData == (Keys.Control | Keys.Alt | Keys.E) && reference_loaded == true && ds_loaded == true)
            {
                string title = "Experiment " + VALID_DS_SPECTRA[n].EXP_NUMBER + " Peak Picking Info";
                string message = VALID_DS_SPECTRA[n].PP_INFO;

                System.Windows.MessageBox.Show(message,
                                               title,
                                               MessageBoxButton.OK,
                                               MessageBoxImage.Information);
            }

            // Show OOR Info
            if (keyData == (Keys.Control | Keys.Alt | Keys.O) && reference_loaded == true && ds_loaded == true && OOR.Any())
            {
                List<string> oors = new List<string>();
                foreach (var oornumb in OOR) { oors.Add(Convert.ToString(oornumb.EXP_NUMBER)); }
                var message = string.Join(Environment.NewLine, oors);
                System.Windows.Forms.MessageBox.Show("Unable to fetch peaks in current import range for experiment: " + message,
                                                     "Warning: Peaks out of import range!",
                                                          MessageBoxButtons.OK,
                                                          MessageBoxIcon.Warning);
            }

            // Show Fault Info
            if (keyData == (Keys.Control | Keys.Alt | Keys.F) && reference_loaded == true && ds_loaded == true && FAULT_EXP.Any())
            {
                var message1 = string.Join(Environment.NewLine, FAULT_EXP);
                System.Windows.Forms.MessageBox.Show("No experiments were found in this folder: " + message1,
                                                     "Warning: No Experiments Found!",
                                                      MessageBoxButtons.OK,
                                                      MessageBoxIcon.Warning);
            }
            #endregion

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!this.Capture)
            {
                if (!this.RectangleToScreen(this.DisplayRectangle).Contains(Cursor.Position))
                {
                    this.Capture = false;
                }
                else
                {
                    this.Capture = true;
                }
            }
            
            base.OnMouseCaptureChanged(e);
        }
        #endregion

        #region Test code
        ///In case of Border none
        //private void OnMouseDown(object sender, MouseEventArgs e)
        //{
        //    if (e.Button == MouseButtons.Left)
        //    {
        //        ReleaseCapture();
        //        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        //    }
        //}
        //
        //private bool mouseDown;
        //private System.Drawing.Point lastLocation;
        //
        //private void Form1_MouseDown(object sender, MouseEventArgs e)
        //{
        //    mouseDown = true;
        //    lastLocation = e.Location;
        //}
        //
        //private void Form1_MouseMove(object sender, MouseEventArgs e)
        //{
        //    if (mouseDown)
        //    {
        //        this.Location = new System.Drawing.Point(
        //            (this.Location.X - lastLocation.X) + e.X, (this.Location.Y - lastLocation.Y) + e.Y);
        //
        //        this.Update();
        //    }
        //}
        //
        //private void Form1_MouseUp(object sender, MouseEventArgs e)
        //{
        //    mouseDown = false;
        //}
        ///


        ///private void cartesianChart1_DataHover(object sender, ChartPoint chartPoint)
        ///{
        ///    cartesianChartPeakDiff.VisualElements.Count();
        ///    cartesianChartPeakDiff.VisualElements[6].X = chartPoint.X;
        ///    cartesianChartPeakDiff.VisualElements[6].Y = chartPoint.Y;
        ///}
        ///
        ///private void update_barchart_index()
        ///{
        ///    if (ShowActives)
        ///    {
        ///        IndexBarCharts = VALID_DS_SPECTRA.IndexOf(ACTIVES[n]);
        ///        cartesianChartPeakDiff.VisualElements[6].X = IndexBarCharts;
        ///        cartesianChartPeakDiff.VisualElements[6].Y = VALID_DS_SPECTRA[IndexBarCharts].PEAK_DIFF;
        ///    }
        ///}
        ///
        #endregion

        private void buttonSetPython_Click(object sender, EventArgs e)
        {
            PythonFinder();
        }

        // DOUBLE CLICK EVENT TEXT BOXES
        private void textBox_DoubleClick(object sender, EventArgs e)
        {
            var textbox = sender as TextBox;
            textbox.Clear();
        }

        private void ImportControl_TextChanged(object sender, EventArgs e)
        {
            var textbox = sender as TextBox;
            string name = textbox.Name;

            if (textbox.Text != "" && textbox.Text != ".")
            {
                checkimport = Convert.ToDouble(textbox.Text);
                try
                {
                    NMin = Convert.ToDouble(textBoxNMin.Text);
                    NMax = Convert.ToDouble(textBoxNMax.Text);
                    HMin = Convert.ToDouble(textBoxHMin.Text);
                    HMax = Convert.ToDouble(textBoxHMax.Text);
                }

                catch { }
            }

            //if (textbox.Focus())
            //    stopasking = false;

            if (textbox.Text.Length > 3 && !textbox.Text.Contains("."))
            {
                string TooManyDigitsMessage = "Whoa! Slow down! Check your input, I have never heard of that in NMR!";
                MessageBoxButtons TooManyDigitsButton = MessageBoxButtons.OK;
                MessageBoxIcon TooManyDigitsIcon = MessageBoxIcon.Stop;
                //DialogResult TooManyDigitsResult;

                System.Windows.Forms.MessageBox.Show(TooManyDigitsMessage, ErrorMessageTitles[errorcount], TooManyDigitsButton, TooManyDigitsIcon);
                textbox.Clear();
                if (errorcount < 3)
                    errorcount++;
                else errorcount = 0;
            }

            // CONTROL THRESHOLDS MESSAGE BOX
            string message;
            string title;
            MessageBoxButtons messageBoxButton = MessageBoxButtons.YesNo;
            MessageBoxIcon messageBoxIco = MessageBoxIcon.Warning;
            DialogResult result = new DialogResult();

            #region CONTROL Hmin Threshold
            if (name == "textBoxHMin" && checkimport <= 4.8 && textbox.Text != "" && stopasking == false)
            {
                message = "Warning: the threshold is close or below the water signal. Continue?" + Environment.NewLine;
                title = "Water Signal ahead";

                result = System.Windows.Forms.MessageBox.Show(message, title, messageBoxButton, messageBoxIco);
                if (result == DialogResult.Yes)
                {
                    stopasking = true;
                }
                else { textbox.Text = "5"; }
            }

            if (name == "textBoxHMin" && textbox.Text != "" && checkimport >= HMax)
            {
                System.Windows.Forms.MessageBox.Show("Minimum value can not be equal to or higher than the Maximum value.",
                                                     "Check Input Value",
                                                     MessageBoxButtons.OK,
                                                     MessageBoxIcon.Error);
                textbox.Text = Convert.ToString(HMax - 0.1);
                return;
            }
            #endregion

            #region CONTROL HMax Threshold
            if (name == "textBoxHMax" && checkimport >= 4.5 && checkimport <= 4.8 && textbox.Text != "" && stopasking == false)
            {
                message = "Warning: the threshold is close or below the water signal. Continue?" + Environment.NewLine;
                title = "Water Signal ahead";

                result = System.Windows.Forms.MessageBox.Show(message, title, messageBoxButton, messageBoxIco);
                if (result == DialogResult.Yes)
                {
                    stopasking = true;
                }
                else { textbox.Text = "12"; }
            }

            if (name == "textBoxHMax" && textbox.Text.Length > 2 && textbox.Text != "" && checkimport <= HMin)
            {
                System.Windows.Forms.MessageBox.Show("Maximum value can not be equal to or lower than the Minimum value.",
                                                     "Check Input Value",
                                                     MessageBoxButtons.OK,
                                                     MessageBoxIcon.Error);
                textbox.Text = Convert.ToString(HMin + 0.1);
            }

            if (HMax > 20)
            {
                textBoxHMax.Text = "20";
            }
            #endregion
        }

        private void IntThresholdControl_TextChanged(object sender, EventArgs e)
        {
            var TextBoxInt = sender as TextBox;

            double check;
            if (TextBoxInt.Text != "")
            {
                check = Convert.ToDouble(TextBoxInt.Text);
                if (check > 70000)
                {
                    string TooManyDigitsMessage = "The current Intensity threshold can be too high." + Environment.NewLine +
                                                  " " + Environment.NewLine +
                                                  "Depending on your data, the software can still import the experiments but with a threshold set too high, eventually, no peaks will be displayed.";
                    string Warning = "WARNING: Threshold too high";
                    MessageBoxButtons TooManyDigitsButton = MessageBoxButtons.OK;
                    MessageBoxIcon TooManyDigitsIcon = MessageBoxIcon.Warning;
                    System.Windows.Forms.MessageBox.Show(TooManyDigitsMessage, Warning, TooManyDigitsButton, TooManyDigitsIcon);
                }
            }
        }

        private void CSP_Main_Click(object sender, EventArgs e)
        {
            this.BringToFront();
        }
    }
}