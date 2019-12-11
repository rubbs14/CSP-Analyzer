using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.WinForms;
using LiveCharts.Wpf;
using System.Runtime.InteropServices;
using System.Drawing.Printing;
using System.Collections;

namespace CSPv2
{
    public partial class FormOutputTable : Form
    {
        public static List<CSP_Main.SPECTRUM> allspectra = new List<CSP_Main.SPECTRUM>();
        public DataTable dt = new DataTable();
        public int actives;
        public int inactives;
        public int actives_man;
        public int inactives_man;
        public int notset;
        public double Actives;
        //public int 
        SolidColorBrush LabelForegroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 162, 151));
        SolidColorBrush ActiveAutoFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 45, 161, 63));
        SolidColorBrush InactiveAutoFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 225, 9, 20));
        SolidColorBrush ActiveManualFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 123, 217, 157));
        SolidColorBrush InactiveManualFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 199, 137, 137));
        SolidColorBrush NotSetManualFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 178, 178, 178));
        SolidColorBrush SeparatorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 79, 86));

        StringFormat strFormat; //Used to format the grid rows.
        ArrayList arrColumnLefts = new ArrayList();//Used to save left coordinates of columns
        ArrayList arrColumnWidths = new ArrayList();//Used to save column widths
        int iCellHeight = 0; //Used to get/set the datagridview cell height
        int iTotalWidth = 0; //
        int iRow = 0;//Used as counter
        bool bFirstPage = false; //Used to check whether we are printing first page
        bool bNewPage = false;// Used to check whether we are printing a new page
        int iHeaderHeight = 0; //Used for the header height

        public FormOutputTable(List<CSP_Main.SPECTRUM> process)
        {
            InitializeComponent();
            this.dataGridView1.Size = new System.Drawing.Size(0, 0);
            allspectra = process;
        }

        public void GenerateTable()
        {
            //dt.Clear();
            dt = new DataTable();

            dt.Columns.Add("Name");
            dt.Columns.Add("Dataset");
            dt.Columns.Add("Total Read Peaks");
            dt.Columns.Add("Min Intensity (AU)");
            dt.Columns.Add("Max Intensity (AU)");
            dt.Columns.Add("Peak difference to Reference");
            dt.Columns.Add("Probability");
            dt.Columns.Add("Automatic Analysis");
            dt.Columns.Add("Manual Flag");

            dt.Rows.Add("Reference",
                        allspectra[0].DS_NAME,
                        allspectra[0].TOT_READ_PEAKS,
                        allspectra[0].PEAKLIST.Min(PEAK => PEAK.INTENSITY).ToString(),
                        allspectra[0].PEAKLIST.Max(PEAK => PEAK.INTENSITY).ToString(),
                        "none",
                        "none",
                        "none",
                        "none");

            actives = 0;
            inactives = 0;
            actives_man = 0;
            inactives_man = 0;
            notset = 0;

            foreach (CSP_Main.SPECTRUM s in allspectra.Skip(1))
            {
                DataRow dr = dt.NewRow();
                dr[0] = s.EXP_NUMBER.ToString();
                dr[1] = s.DS_NAME;
                dr[2] = s.TOT_READ_PEAKS.ToString();
                dr[3] = s.PEAKLIST.Min(PEAK => PEAK.INTENSITY).ToString();
                dr[4] = s.PEAKLIST.Max(PEAK => PEAK.INTENSITY).ToString();
                dr[5] = s.TOT_READ_PEAKS - allspectra[0].TOT_READ_PEAKS;
                dr[6] = Math.Round(s.Prob, 2);
                if (s.isActive == true) { dr[7] = "Active"; }
                else dr[7] = "Inactive";
                dr[8] = s.UserSelection;
                dt.Rows.Add(dr);

                // Populate graph
                if (s.isActive == true) { actives++; Actives = Convert.ToDouble(actives); }
                else { inactives++; }

                if (s.UserSelection == "Not set")
                {
                    notset++;
                }
                if (s.UserSelection == "ACTIVE (MAN)")
                {
                    actives_man++;
                }
                if (s.UserSelection == "INACTIVE (MAN)")
                {
                    inactives_man++;
                }
            }
            //piechart[0].Values = actives;
            //Values  = new ObservableValue(actives);
            //Values = new ChartValues<ObservableValue>() { new ObservableValue(actives) };

            dataGridView1.DataSource = dt;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dataGridView1.AllowUserToOrderColumns = true;
            dataGridView1.AllowUserToResizeColumns = true;
            dataGridView1.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        public void update_piecharts()
        {
            //pieChart1.Series.Clear();
            //pieChartAll.Update();
            labelTotExp.Text = (allspectra.Count() - 1).ToString();
            piechart[0].Values = new ChartValues<int> { actives };
            piechartauto[0].Values = new ChartValues<int> { actives };
            labelAutoActives.Text = actives.ToString();
            piechart[1].Values = new ChartValues<int> { inactives };
            piechartauto[1].Values = new ChartValues<int> { inactives };
            labelAutoInactives.Text = inactives.ToString();
            piechart[2].Values = new ChartValues<int> { notset };
            piechartmanual[2].Values = new ChartValues<int> { notset };
            labelNotSet.Text = notset.ToString();
            piechart[3].Values = new ChartValues<int> { actives_man };
            piechartmanual[0].Values = new ChartValues<int> { actives_man };
            labelManualActives.Text = actives_man.ToString();
            piechart[4].Values = new ChartValues<int> { inactives_man };
            piechartmanual[1].Values = new ChartValues<int> { inactives_man };
            labelManualInactives.Text = inactives_man.ToString();
            pieChartAll.Series = piechart;
            pieChartAuto.Series = piechartauto;

            pieChartAll.Update();
            pieChartAuto.Update();
            pieChartManual.Update();
        }

        public SeriesCollection piechart { get; set; }
        public SeriesCollection piechartauto { get; set; }
        public SeriesCollection piechartmanual { get; set; }
        public ChartValues<ObservableValue> Values { get; set; }

        private void FormOutputTable_Load(object sender, EventArgs e)
        {
            Func<ChartPoint, string> labelPoint = chartPoint =>
            string.Format("{0} ({1:P})", chartPoint.Y, chartPoint.Participation);

            piechart = new SeriesCollection()
            {
                new PieSeries
                {
                    Title = "Actives",
                    Values = new ChartValues<int>(),
                    PushOut = 5,
                    DataLabels = true,
                    LabelPoint = labelPoint,
                    Fill = ActiveAutoFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                },
                new PieSeries
                {
                    Title = "Inactives",
                    Values = new ChartValues<int>(),
                    PushOut = 5,
                    DataLabels = true,
                    LabelPoint = labelPoint,
                    Fill = InactiveAutoFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                },
                new PieSeries
                {
                    Title = "Manual: Not set",
                    Values = new ChartValues<int>(),
                    DataLabels = true,
                    LabelPoint = labelPoint,
                    Fill = NotSetManualFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                },
                new PieSeries
                {
                    Title = "Manual: Actives",
                    Values = new ChartValues<int>(),
                    DataLabels = true,
                    LabelPoint = labelPoint,
                    Fill = ActiveManualFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                },
                new PieSeries
                {
                    Title = "Manual: Inactives",
                    Values = new ChartValues<int>(),
                    DataLabels = true,
                    LabelPoint = labelPoint,
                    Fill = InactiveManualFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                }
            };
            pieChartAll.Series = piechart;

            piechartauto = new SeriesCollection()
            {
                new PieSeries
                {
                    Title = "Actives",
                    Values = new ChartValues<int>(),
                    PushOut = 5,
                    DataLabels = true,
                    LabelPoint = labelPoint,
                    Fill = ActiveAutoFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                },
                new PieSeries
                {
                    Title = "Inactives",
                    Values = new ChartValues<int>(),
                    DataLabels = true,
                    LabelPoint = labelPoint,
                    Fill = InactiveAutoFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                },
            };
            pieChartAuto.Series = piechartauto;

            piechartmanual = new SeriesCollection()
            {
                new PieSeries
                {
                    Title = "Manual: Actives",
                    Values = new ChartValues<int>(),
                    DataLabels = true,
                    PushOut = 5,
                    LabelPoint = labelPoint,
                    Fill = ActiveManualFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                },
                new PieSeries
                {
                    Title = "Manual: Inactives",
                    Values = new ChartValues<int>(),
                    DataLabels = true,
                    PushOut = 5,
                    LabelPoint = labelPoint,
                    Fill = InactiveManualFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                },
                new PieSeries
                {
                    Title = "Manual: Not set",
                    Values = new ChartValues<int>(),
                    DataLabels = true,
                    LabelPoint = labelPoint,
                    Fill = NotSetManualFill,
                    Stroke = SeparatorBrush,
                    StrokeThickness = 1
                }
            };
            pieChartManual.Series = piechartmanual;

            GenerateTable();
            dataGridView1.Refresh();
            dataGridView1.Update();
            update_piecharts();

            pieChartAll.LegendLocation = LegendLocation.Right;
            pieChartAll.DefaultLegend.Foreground = LabelForegroundBrush;
            pieChartAuto.LegendLocation = LegendLocation.Right;
            pieChartAuto.DefaultLegend.Foreground = LabelForegroundBrush;
            pieChartManual.LegendLocation = LegendLocation.Right;
            pieChartManual.DefaultLegend.Foreground = LabelForegroundBrush;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            GenerateTable();
            dataGridView1.Refresh();
            dataGridView1.Update();
            update_piecharts();
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            //if (e.Value.Equals("none"))
            //{
            //    e.CellStyle.BackColor = Color.Black;
            //}

            if (e.Value.Equals("Active"))
            {
                e.CellStyle.BackColor = System.Drawing.Color.LightGreen;
                e.CellStyle.ForeColor = System.Drawing.Color.Black;
            }

            if (e.Value.Equals("ACTIVE (MAN)"))
            {
                e.CellStyle.BackColor = System.Drawing.Color.LightGreen;
                e.CellStyle.ForeColor = System.Drawing.Color.Black;
            }

            if (e.Value.Equals("Inactive"))
            {
                e.CellStyle.BackColor = System.Drawing.Color.PaleVioletRed;
                e.CellStyle.ForeColor = System.Drawing.Color.Black;
            }

            if (e.Value.Equals("INACTIVE (MAN)"))
            {
                e.CellStyle.BackColor = System.Drawing.Color.PaleVioletRed;
                e.CellStyle.ForeColor = System.Drawing.Color.Black;
            }
        }

        private void buttonToExcel_Click(object sender, EventArgs e)
        {
            try
            {
                Microsoft.Office.Interop.Excel._Application app = new Microsoft.Office.Interop.Excel.Application();
                Microsoft.Office.Interop.Excel._Workbook workbook = app.Workbooks.Add(Type.Missing);
                Microsoft.Office.Interop.Excel._Worksheet worksheet = null;
                app.Visible = true;
                worksheet = workbook.Sheets["Sheet1"];
                worksheet = workbook.ActiveSheet;
                worksheet.Name = "CSP_Output";

                try
                {
                    for (int i = 0; i < dataGridView1.Columns.Count; i++)
                    {
                        worksheet.Cells[1, i + 1] = dataGridView1.Columns[i].HeaderText;
                    }
                    for (int i = 0; i < dataGridView1.Rows.Count; i++)
                    {
                        for (int j = 0; j < dataGridView1.Columns.Count; j++)
                        {
                            if (dataGridView1.Rows[i].Cells[j].Value != null)
                            {
                                worksheet.Cells[i + 2, j + 1] = dataGridView1.Rows[i].Cells[j].Value.ToString();
                            }
                            else
                            {
                                worksheet.Cells[i + 2, j + 1] = "";
                            }
                        }
                    }

                    //Getting the location and file name of the excel to save from user. 
                    SaveFileDialog saveDialog = new SaveFileDialog();
                    saveDialog.Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*";
                    saveDialog.FilterIndex = 2;

                    if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        workbook.SaveAs(saveDialog.FileName);
                        MessageBox.Show("Export Successful", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                finally
                {
                    app.Quit();
                    workbook = null;
                    worksheet = null;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message.ToString()); }
        }

        private void buttonPrint_Click(object sender, EventArgs e)
        {
            #region Working print
            ////Open the print dialog
            //PrintDialog printDialog = new PrintDialog();
            //printDialog.Document = printDocument1;
            //printDialog.UseEXDialog = true;
            //
            //////Open the print preview dialog
            //PrintPreviewDialog objPPdialog = new PrintPreviewDialog();
            //objPPdialog.Document = printDocument1;
            //objPPdialog.ShowDialog();
            //
            ////Get the document
            //if (DialogResult.OK == printDialog.ShowDialog())
            //{
            //    printDocument1.DocumentName = "Test Page Print";
            //    printDocument1.Print();
            //}
            #endregion
            printPreviewDialog1.Document = printDocument1;
            //ToolStripButton ToolStripButton = new ToolStripButton();
            //this.ToolStripButton.Image = ((System.Windows.Forms.ToolStrip)(printPreviewDialog1.Controls[1])).ImageList.Images[0];
            {
                this.ToolStripButton = new System.Windows.Forms.ToolStripButton();
                this.ToolStripButton.Image = ((System.Windows.Forms.ToolStrip)(printPreviewDialog1.Controls[1])).ImageList.Images[0];
                this.ToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
                this.ToolStripButton.Click += new System.EventHandler(this.printPreview_PrintClick);
                ((System.Windows.Forms.ToolStrip)(printPreviewDialog1.Controls[1])).Items.RemoveAt(0);
                ((System.Windows.Forms.ToolStrip)(printPreviewDialog1.Controls[1])).Items.Insert(0, ToolStripButton);
            }

            printPreviewDialog1.ShowDialog();
        }

        private void printPreview_PrintClick(object sender, EventArgs e)
        {
            try
            {
                printDialog1.Document = printDocument1;
                if (printDialog1.ShowDialog() == DialogResult.OK)
                {
                    //Focus();
                    printDocument1.PrinterSettings = printDialog1.PrinterSettings;
                    printDocument1.Print();
                    printPreviewDialog1.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ToString());
            }
        }

        private ToolStripButton ToolStripButton;

        private void printDocument1_BeginPrint(object sender, System.Drawing.Printing.PrintEventArgs e)
        {
            try
            {
                strFormat = new StringFormat();
                strFormat.Alignment = StringAlignment.Near;
                strFormat.LineAlignment = StringAlignment.Center;
                strFormat.Trimming = StringTrimming.EllipsisCharacter;

                arrColumnLefts.Clear();
                arrColumnWidths.Clear();
                iCellHeight = 0;
                iRow = 0;
                bFirstPage = true;
                bNewPage = true;

                // Calculating Total Widths
                iTotalWidth = 0;
                foreach (DataGridViewColumn dgvGridCol in dataGridView1.Columns)
                {
                    iTotalWidth += dgvGridCol.Width;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void printDocument1_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
        {
            try
            {
                //Set the left margin
                int iLeftMargin = e.MarginBounds.Left;
                //Set the top margin
                int iTopMargin = e.MarginBounds.Top;
                //Whether more pages have to print or not
                bool bMorePagesToPrint = false;
                int iTmpWidth = 0;

                //For the first page to print set the cell width and header height
                if (bFirstPage)
                {
                    foreach (DataGridViewColumn GridCol in dataGridView1.Columns)
                    {
                        iTmpWidth = (int)(Math.Floor((double)((double)GridCol.Width /
                                       (double)iTotalWidth * (double)iTotalWidth *
                                       ((double)e.MarginBounds.Width / (double)iTotalWidth))));

                        iHeaderHeight = (int)(e.Graphics.MeasureString(GridCol.HeaderText,
                                    GridCol.InheritedStyle.Font, iTmpWidth).Height) + 11;

                        // Save width and height of headres
                        arrColumnLefts.Add(iLeftMargin);
                        arrColumnWidths.Add(iTmpWidth);
                        iLeftMargin += iTmpWidth;
                    }
                }
                //Loop till all the grid rows not get printed
                while (iRow <= dataGridView1.Rows.Count - 1)
                {
                    DataGridViewRow GridRow = dataGridView1.Rows[iRow];
                    //Set the cell height
                    iCellHeight = GridRow.Height + 5;
                    int iCount = 0;
                    //Check whether the current page settings allo more rows to print
                    if (iTopMargin + iCellHeight >= e.MarginBounds.Height + e.MarginBounds.Top)
                    {
                        bNewPage = true;
                        bFirstPage = false;
                        bMorePagesToPrint = true;
                        break;
                    }
                    else
                    {
                        if (bNewPage)
                        {
                            //Draw Header
                            e.Graphics.DrawString("CSP Analysis Report", new Font(dataGridView1.Font, FontStyle.Bold),
                                    System.Drawing.Brushes.Black, e.MarginBounds.Left, e.MarginBounds.Top -
                                    e.Graphics.MeasureString("CSP Analysis Report", new Font(dataGridView1.Font,
                                    FontStyle.Bold), e.MarginBounds.Width).Height - 13);

                            String strDate = DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToShortTimeString();
                            //Draw Date
                            e.Graphics.DrawString(strDate, new Font(dataGridView1.Font, FontStyle.Bold),
                                    System.Drawing.Brushes.Black, e.MarginBounds.Left + (e.MarginBounds.Width -
                                    e.Graphics.MeasureString(strDate, new Font(dataGridView1.Font,
                                    FontStyle.Bold), e.MarginBounds.Width).Width), e.MarginBounds.Top -
                                    e.Graphics.MeasureString("CSP Analysis Report", new Font(new Font(dataGridView1.Font,
                                    FontStyle.Bold), FontStyle.Bold), e.MarginBounds.Width).Height - 13);

                            //Draw Columns                 
                            iTopMargin = e.MarginBounds.Top;
                            foreach (DataGridViewColumn GridCol in dataGridView1.Columns)
                            {
                                e.Graphics.FillRectangle(new SolidBrush(System.Drawing.Color.LightGray),
                                    new Rectangle((int)arrColumnLefts[iCount], iTopMargin,
                                    (int)arrColumnWidths[iCount], iHeaderHeight));

                                e.Graphics.DrawRectangle(Pens.Black,
                                    new Rectangle((int)arrColumnLefts[iCount], iTopMargin,
                                    (int)arrColumnWidths[iCount], iHeaderHeight));

                                e.Graphics.DrawString(GridCol.HeaderText, GridCol.InheritedStyle.Font,
                                    new SolidBrush(GridCol.InheritedStyle.ForeColor),
                                    new RectangleF((int)arrColumnLefts[iCount], iTopMargin,
                                    (int)arrColumnWidths[iCount], iHeaderHeight), strFormat);
                                iCount++;
                            }
                            bNewPage = false;
                            iTopMargin += iHeaderHeight;
                        }
                        iCount = 0;
                        //Draw Columns Contents                
                        foreach (DataGridViewCell Cel in GridRow.Cells)
                        {
                            if (Cel.Value != null)
                            {
                                e.Graphics.DrawString(Cel.Value.ToString(), Cel.InheritedStyle.Font,
                                            new SolidBrush(System.Drawing.Color.Black),
                                            new RectangleF((int)arrColumnLefts[iCount], (float)iTopMargin,
                                            (int)arrColumnWidths[iCount], (float)iCellHeight), strFormat);
                            }
                            //Drawing Cells Borders 
                            e.Graphics.DrawRectangle(Pens.Black, new Rectangle((int)arrColumnLefts[iCount],
                                    iTopMargin, (int)arrColumnWidths[iCount], iCellHeight));

                            iCount++;
                        }
                    }
                    iRow++;
                    iTopMargin += iCellHeight;
                }

                //If more lines exist, print another page.
                if (bMorePagesToPrint)
                    e.HasMorePages = true;
                else
                    e.HasMorePages = false;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.R)
            {
                buttonRefresh.PerformClick();
                return true;
            }

            if (keyData == (Keys.Control | Keys.E))
            {
                buttonToExcel.PerformClick();
                return true;
            }

            if (keyData == (Keys.Control | Keys.P))
            {
                buttonPrint.PerformClick();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Q))
            {
                string message = "Are you sure you would like to close this Window?" + Environment.NewLine +
                                 "" + Environment.NewLine +
                                 "WARNING: All unsaved data will be lost.";

                string msgboxtitle = "Confirm Close Window";
                MessageBoxButtons buttons = MessageBoxButtons.OKCancel;
                MessageBoxIcon boxIcon = MessageBoxIcon.Warning;
                DialogResult result;

                result = System.Windows.Forms.MessageBox.Show(message, msgboxtitle, buttons, boxIcon);
                if (result == DialogResult.OK)
                    this.Close();
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.Capture = true;
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
                    this.Focus();
                }
            }

            base.OnMouseCaptureChanged(e);
        }
    }
}
