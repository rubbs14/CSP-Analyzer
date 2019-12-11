using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PeakListExtractor
{

    public partial class Form1 : Form
    {
        List<string> VALIDEXP_ORIGINALPATH = new List<string>();
        List<string> EXP_SUBFOLDERS = new List<string>();
        List<SPECTRUM> SPECTRA = new List<SPECTRUM>();
        bool bidim;

        string[] LOADPATH;
        string savepath;
        const string subpath = "\\pdata\\1\\";
        public class SPECTRUM
        {
            public string originalpath { get; set; }
            public string subpath { get; set; }
            public string destinationpath { get; set; }          
        }

        public Form1()
        {
            InitializeComponent();
            button2.Enabled = false;
        }

        private void initialize()
        {
            VALIDEXP_ORIGINALPATH.Clear();
            SPECTRA.Clear();
            EXP_SUBFOLDERS.Clear();
            label_load.Text = "Total Subfolders: 0";
            labelXMLFound.Text = "Peaklist files found: 0";
            button2.Enabled = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            initialize();
            
            FolderBrowserDialog fbdload = new FolderBrowserDialog();

            if (fbdload.ShowDialog() == DialogResult.OK)
            {
                LOADPATH = Directory.GetDirectories(fbdload.SelectedPath, "*", SearchOption.AllDirectories);  
                
                    foreach (string s in LOADPATH)
                    {
                        if (s.EndsWith("1") && !s.Contains("pdata"))
                        {

                            if (File.Exists(s + "\\peaklist.xml"))
                            {
                                //parent = new FileInfo(s).Directory.Root.ToString();
                                string[] temp = s.Split('\\');

                                //int ind = s.LastIndexOf("\\") + 1;
                                //temp = s.Substring(ind);
                                EXP_SUBFOLDERS.Add("\\" + temp[temp.Count() - 4] + "\\" + temp[temp.Count() - 3] + subpath);

                                VALIDEXP_ORIGINALPATH.Add(s + "\\peaklist.xml");
                                SPECTRUM sPECTRUM = new SPECTRUM { };
                                sPECTRUM.originalpath = s + "\\peaklist.xml";
                                sPECTRUM.subpath = "\\" + temp[temp.Count() - 4] + "\\" + temp[temp.Count() - 3] + subpath;
                                SPECTRA.Add(sPECTRUM);
                            }
                        }
                    }
                    button2.Enabled = true;



            label_load.Text = "Total Subfolders: " + LOADPATH.Count();
            labelXMLFound.Text = "Peaklist files found: " + VALIDEXP_ORIGINALPATH.Count().ToString();

            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbdsave = new FolderBrowserDialog();
            if (fbdsave.ShowDialog() == DialogResult.OK)
            {
                savepath = fbdsave.SelectedPath;
                label3.Text = "Saving to: " + savepath;
                //Create folders
                try
                {
                    foreach (string s in EXP_SUBFOLDERS)
                    {
                        Directory.CreateDirectory(Path.Combine(@savepath) + s);
                    }

                    foreach (SPECTRUM sp in SPECTRA)
                    {
                        sp.destinationpath = savepath + sp.subpath + "peaklist.xml";
                        label4.Text = "Copying to: " + sp.destinationpath;
                        //string file = Path.GetFileName(sp.originalpath);  // "file" is the file name
                        //string newFileName = System.IO.Path.Combine(path, file);
                        //System.IO.File.Copy(file, newFileName, true);
                        File.Copy(sp.originalpath, sp.destinationpath);
                    }
                    
                }
                
                catch (Exception z)
                {
                    if (z.InnerException != null)
                    {
                        string err = z.InnerException.Message;
                        MessageBox.Show(err);
                    }
                }
                label4.Text = SPECTRA.Count() + " Peaklist files copied. Click to open in File Explorer";
            }
        }

        private void label4_Click(object sender, EventArgs e)
        {
            try { Process.Start(savepath); }
            catch (Exception z)
            {
                if (z.InnerException != null)
                {
                    string err = z.InnerException.Message;
                    MessageBox.Show(err);
                }
            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
