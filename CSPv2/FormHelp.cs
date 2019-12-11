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
    public partial class FormHelp : Form
    {
        public FormHelp()
        {
            InitializeComponent();
        }

        private void FormShortcuts_Load(object sender, EventArgs e)
        {

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
                    this.Close();
                }
                else
                {
                    this.Capture = true;
                }
            }

            base.OnMouseCaptureChanged(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Q))
            {
                this.Close();
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void close_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
            {
                e.Handled = true;
            }

            // allow only one decimal point
            if ((e.KeyChar == '.') && ((sender as TextBox).Text.IndexOf('.') > -1))
            {
                e.Handled = true;
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        { 
            if (Regex.IsMatch(textBox_PPNUM.Text, "[^0-9]"))
            {
                System.Windows.Forms.MessageBox.Show("Please enter only integer numbers.");
                textBox_PPNUM.Text = textBox_PPNUM.Text.Remove(textBox_PPNUM.Text.Length - 1);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (TextBox textBox in Controls.OfType<TextBox>())
                textBox.Text = "";
        }

        private void button_generate(object sender, EventArgs e)
        {
            foreach (TextBox textbox in Controls.OfType<TextBox>())
                if (textbox.Text != "" && textbox.Text != ".")
                {
                    try
                    {
                        string NMin = Convert.ToString(textBox_1F2P.Text);
                        string NMax = Convert.ToString(textBox_1F1P.Text);
                        string HMin = Convert.ToString(textBox_2F2P.Text);
                        string HMax = Convert.ToString(textBox_2F1P.Text);
                        string  MI  = textBox_MI.Text;
                        string PPNUM = textBox_PPNUM.Text;

                        string TopSpinCommand;
                        TopSpinCommand = "1 F1P " + NMax + "; 2 F1P " + HMax + "; 1 F2P " + NMin + "; 2 F2P " + HMin + "; MI " + MI + "; PPNUM " + PPNUM + "; pp2d nodia";
                        textBox_TopSpinCommand.Text = TopSpinCommand;
                    }
                    catch { }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //textBox_TopSpinCommand.Copy();
            System.Windows.Clipboard.SetText(textBox_TopSpinCommand.Text);
        }
    }
}
