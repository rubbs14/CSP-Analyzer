using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;


namespace CSPv2
{
    public partial class Form2 : Form
    {

        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
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

        private void close_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.linkedin.com/in/robertofino");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.cadd.ethz.ch/people/ryan_byrne.html");
        }

        private void label5_Click(object sender, EventArgs e)
        {
            Process.Start("https://link.springer.com/article/10.1007/s10822-016-9953-9");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Process.Start("https://lvcharts.net/");
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.aegis-itn.eu/");
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Q))
            {
                //System.Windows.Forms.MessageBox.Show("What the Ctrl+F?");
                this.Close();
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
