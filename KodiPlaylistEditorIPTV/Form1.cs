﻿//  MIT License
//  Copyright (c) 2018 github/isayso
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation
//  files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy,
//  modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
//  subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;



//ToDo bug no move after sorting, no reload, binding problem?


namespace PlaylistEditor
{
    public partial class Form1 : Form
    {

        Stack<object[][]> undoStack = new Stack<object[][]>();
        Stack<object[][]> redoStack = new Stack<object[][]>();

        Boolean ignore = false;

        bool isModified = false;

      
        public string fileName = "";
        public string line;       
        private string path;
       
        public bool _isIt = true;
        public bool _found = false;
        public bool _savenow = false;
        public bool _taglink = false;
        public bool _isSingle = false;

        //zoom of fonts
        public float zoomf = 1F;
       // private static readonly int ROWHEIGHT = 47;
        private static readonly float FONTSIZE = 9.163636F;
        DataSet ds = new DataSet();
        DataTable dt = new DataTable();
        DataRow dr;
        readonly string vlcpath = Properties.Settings.Default.vlcpath;

        public int[] colShow = new int[6];


        public Form1()
        {
            InitializeComponent();

           

            this.Text = String.Format("PlaylistEditor TV " + " v{0}" , Assembly.GetExecutingAssembly().GetName().Version.ToString().Substring(0, 5));

            var spec_key = Properties.Settings.Default.specKey;
            var hotlabel = Properties.Settings.Default.hotkey;
           
            
           
            if (!string.IsNullOrEmpty(vlcpath))
            {
                button_vlc.Visible = true;
            }
            else if (string.IsNullOrEmpty(vlcpath))  //first run
            {
                vlcpath = ClassHelp.GetVlcPath();
                if (string.IsNullOrEmpty(vlcpath)) button_vlc.Visible = false; //no vlc installed
            }
           

            plabel_Filename.Text = "";
            button_revert.Visible = false;


            //  dataGridView1.AllowUserToAddRows = true;

            dataGridView1.DoubleBuffered(true);
            dataGridView1.BringToFront();

            //command line arguments [1]
            string[] args = Environment.GetCommandLineArgs();

            if (args.Length > 1) //drag drop
            {
                plabel_Filename.Text = args[1];
                importDataset(args[1], false);
                button_revert.Visible = true;
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Modifiers == Keys.Control)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.C:
                            toolStripCopy.PerformClick();
                            break;

                        case Keys.V:
                            toolStripPaste.PerformClick();
                            break;
                      
                        case Keys.F:
                            button_search.PerformClick();
                            break;

                        case Keys.R:
                            copyRowMenuItem.PerformClick();
                            break;

                        case Keys.I:
                            pasteRowMenuItem.PerformClick();
                            break;

                        case Keys.X:
                            cutRowMenuItem.PerformClick();
                            break;

                        case Keys.N:
                            var info = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath);
                            System.Diagnostics.Process.Start(info);
                            break;

                        case Keys.P:
                            playToolStripMenuItem.PerformClick();
                            break;

                        case Keys.S:
                            _savenow = true;
                            button_save.PerformClick();
                            break;

                        case Keys.T:  //move line to top
                            
                            MoveLineTop();
                            break;

                        case Keys.Add:    //change font size
                            zoomf += 0.1F;
                            ZoomGrid(zoomf);
                            break;

                        case Keys.Oemplus:      //change font size
                            zoomf += 0.1F;
                            ZoomGrid(zoomf);
                            break;

                        case Keys.Subtract:    //change font size
                            zoomf -= 0.1F;
                            ZoomGrid(zoomf);
                            break;

                        case Keys.OemMinus:     //change font size
                            zoomf -= 0.1F;
                            ZoomGrid(zoomf);
                            break;

                       
                    }
                }
                if (e.KeyCode == Keys.Delete)
                {
                    button_delLine.PerformClick();
                }

               
            }
            catch (Exception ex)
            {
                MessageBox.Show("Key press operation failed. " + ex.Message, "Key press", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }

        /// <summary>
        /// change font size of datagrid
        /// </summary>
        /// <param name="f">change factor float</param>
        public void ZoomGrid(float f)
        {
           
            dataGridView1.Font = new Font(dataGridView1.Font.FontFamily,
                                         FONTSIZE * f, dataGridView1.Font.Style);

          //  dataGridView1.RowTemplate.Height = (int)(ROWHEIGHT * f);

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isModified == true && dataGridView1.RowCount > 0)
            {
                DialogResult dialogSave = MessageBox.Show("Do you want to save your current playlist?",
                "Save Playlist", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogSave == DialogResult.Yes)
                    saveFileDialog1.ShowDialog();
                isModified = false;
            }

            Application.Exit();

         
        }


        /*--------------------------------------------------------------------------------*/
        // Menu Buttons
        /*--------------------------------------------------------------------------------*/

        private void button_search_Click(object sender, EventArgs e)
        {
            textBox_find.BringToFront();

            if (_isIt)
            {
                _isIt = !_isIt;
                textBox_find.Visible = true;
                this.ActiveControl = textBox_find;
            }
            else
            {
                _isIt = !_isIt;
                textBox_find.Visible = false;
            }
            
        }


        private void button_open_Click(object sender, EventArgs e)
        {
            if (_taglink) button_check.PerformClick();

            Cursor.Current = Cursors.WaitCursor;
            string openpath = Properties.Settings.Default.openpath;
            if (!string.IsNullOrEmpty(openpath) && !ClassHelp.MyDirectoryExists(openpath, 4000))
                openpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\";

            using (OpenFileDialog openFileDialog1 = new OpenFileDialog())
            {
                openFileDialog1.InitialDirectory = openpath;
                openFileDialog1.RestoreDirectory = false;

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    importDataset(openFileDialog1.FileName, false);
                    button_revert.Visible = true;
                }
                else  //cancel
                {
                    return;
                }
                
                Properties.Settings.Default.openpath = Path.GetDirectoryName(openFileDialog1.FileName);
                Properties.Settings.Default.Save();
            }
            Cursor.Current = Cursors.Default;
        }

        private void button_Info_Click(object sender, EventArgs e)
        {
            AboutBox1 a = new AboutBox1();
            a.ShowDialog();   //  ShowDialog gets focus, Show not
            //centre position on Infoform
        }

        private void button_settings_Click(object sender, EventArgs e)
        {
            settings s = new settings();
            s.ShowDialog();
        }


        /// <summary>
        /// import of playlist entries
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="append">false/true for append</param>
        public void importDataset(string filename, bool append)  
        {
            
            if (!ClassHelp.FileIsIPTV(filename))
            {
                MessageBox.Show("File has wrong format!  ");
                return;  
            }

            dt.TableName = "IPTV";


            dataGridView1.DataSource = dt;
            string[] col = new string[6];
            Array.Clear(colShow, 0, 6);

            StreamReader playlistFile = new StreamReader(filename);
            if (!append)  //append false
            {
                dt.Clear();  // row clear
                dt.Columns.Clear();  // col clear

                plabel_Filename.Text = filename;

                dt.Columns.Add("Name"); dt.Columns.Add("id"); dt.Columns.Add("Title");
                dt.Columns.Add("logo"); dt.Columns.Add("Name2"); dt.Columns.Add("Link");

            }

            while ((line = playlistFile.ReadLine()) != null)
            {
                

                if (line.StartsWith("#EXTINF"))
                {
                   
                    col[0] = ClassHelp.GetPartString(line, "tvg-name=\"", "\"");
                    if (col[0] == "") col[0] = "N/A";
                    else if (col[0].Contains("N/A")) colShow[0] = 0;
                    else colShow[0] = 1;


                    col[1] = ClassHelp.GetPartString(line, "tvg-id=\"", "\"");
                    if (col[1] == "") col[1] = "N/A";
                    else if (col[1].Contains("N/A")) colShow[1] = 0;
                    else colShow[1] = 1;

                    col[2] = ClassHelp.GetPartString(line, "group-title=\"", "\"");
                    if (col[2] == "") col[2] = "N/A";
                    else if (col[2].Contains("N/A")) colShow[2] = 0;
                    else colShow[2] = 1;

                    col[3] = ClassHelp.GetPartString(line, "tvg-logo=\"", "\"");
                    if (col[3] == "") col[3] = "N/A";
                    else if (col[3].Contains("N/A")) colShow[3] = 0;
                    else colShow[3] = 1;

                    col[4] = line.Split(',').Last();
                    if (col[4] == "") col[4] = "N/A";


                    continue;

                }

                else if (line.StartsWith("ht") && (line.Contains("//") || line.Contains(":\\")))
                {
                     col[5] = line;
                }
                //else if (line.StartsWith("#") || line.StartsWith("."))
                //{

                //    continue;
                //}
                else
                {
                    continue;  //if file has irregular linefeed.
                }

              
                    try
                    {
                        
                        dr = dt.NewRow();
                        dr["Name"] = col[0].Trim(); dr["id"] = col[1].Trim(); dr["Title"] = col[2].Trim();
                        dr["logo"] = col[3].Trim(); dr["Name2"] = col[4].Trim(); dr["Link"] = col[5].Trim();
                        dt.Rows.Add(dr);
                      
                    }
                    catch (System.ArgumentOutOfRangeException)
                    {
                        MessageBox.Show("Argument out of range error. Wrong format.");
                        continue;
                    }
            }
            playlistFile.Close();

            dataGridView1.AllowUserToAddRows = false;

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("Wrong file structure! ", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (colShow[0] != 1) dataGridView1.Columns["Name"].Visible = false;
            if (colShow[1] != 1) dataGridView1.Columns["id"].Visible = false;
            if (colShow[2] != 1) dataGridView1.Columns["Title"].Visible = false;
            if (colShow[3] != 1) dataGridView1.Columns["logo"].Visible = false;
            colShow[4] = 1;
            colShow[5] = 1;



        }

        private void button_delLine_Click(object sender, EventArgs e) 
        {
            
            if (dataGridView1.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in dataGridView1.SelectedRows)
                {
                   // int selectedRow = dataGridView1.SelectedRows[0].Index;
                    int selectedRow = dataGridView1.SelectedCells[0].RowIndex;

                    dt.Rows.RemoveAt(selectedRow);
                }


                toSave(true);

            }
        }

        private void button_save_Click(object sender, EventArgs e)  
        {
            Cursor.Current = Cursors.WaitCursor;

            for (int i = 0; i < dataGridView1.ColumnCount; i++)
            {
                if (dataGridView1.Columns[dataGridView1.Columns[i].HeaderText].Visible == false)
                {
                    switch (MessageBox.Show("Hidden Columns avaliable, continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation))
                    {
                        case DialogResult.Yes:
                            break;

                        case DialogResult.No:
                            return;
                            // break;
                    }
                    break;
                }
            }

            if ((Control.ModifierKeys == Keys.Shift || _savenow) && !string.IsNullOrEmpty(plabel_Filename.Text) 
                && ClassHelp.MyDirectoryExists(Path.GetDirectoryName(plabel_Filename.Text), 4000))
            {
                
                saveFileDialog1.FileName = plabel_Filename.Text;

         
                using (StreamWriter file = new StreamWriter(saveFileDialog1.FileName, false /*, Encoding.UTF8*/))   //false: file ovewrite
                {

                    file.NewLine = "\n";  // win: LF
                    file.WriteLine("#EXTM3U");
                    string writestring = "";

                    for (int i = 0; i < dt.Rows.Count; i++)
                    {//issue #12  write only visible columns
                        writestring = "#EXTINF:-1 ";
                        if (dataGridView1.Columns["Name"].Visible) writestring += "tvg-name=\"" + dt.Rows[i][0] + "\"";
                        if (dataGridView1.Columns["id"].Visible) writestring += " tvg-id=\"" + dt.Rows[i][1] + "\"";
                        if (dataGridView1.Columns["Title"].Visible) writestring += " group-title=\"" + dt.Rows[i][2] + "\"";
                        if (dataGridView1.Columns["logo"].Visible) writestring += " tvg-logo=\"" + dt.Rows[i][3] + "\"";
                     
                            writestring += "," + dt.Rows[i][4];

                        file.WriteLine(writestring);
                        file.WriteLine(dt.Rows[i][5]);

                        //file.WriteLine("#EXTINF:-1 tvg-name=\"" + dt.Rows[i][0] + "\" tvg-id=\"" + dt.Rows[i][1] + "\" group-title=\"" + dt.Rows[i][2] + "\" tvg-logo=\"" + dt.Rows[i][3] + "\"," + dt.Rows[i][4]);
                        //file.WriteLine(dt.Rows[i][5]);

                    }

                }
                toSave(false);
                button_revert.Visible = true;
                _savenow = false;
                
                
            }

            else if (saveFileDialog1.ShowDialog() == DialogResult.OK)  //open file dialog
            {
                plabel_Filename.Text = saveFileDialog1.FileName;

                using (StreamWriter file = new StreamWriter(saveFileDialog1.FileName, false /*, Encoding.UTF8*/))   //false: file ovewrite
                {

                    file.NewLine = "\n";  // win: LF
                    file.WriteLine("#EXTM3U");
                    string writestring = "";

                    for (int i = 0; i < dt.Rows.Count; i++)
                    {//issue #12  write only visible columns
                        writestring = "#EXTINF:-1 ";
                        if (dataGridView1.Columns["Name"].Visible) writestring += "tvg-name=\"" + dt.Rows[i][0] + "\"";
                        if (dataGridView1.Columns["id"].Visible) writestring += " tvg-id=\"" + dt.Rows[i][1] + "\"";
                        if (dataGridView1.Columns["Title"].Visible) writestring += " group-title=\"" + dt.Rows[i][2] + "\"";
                        if (dataGridView1.Columns["logo"].Visible) writestring += " tvg-logo=\"" + dt.Rows[i][3] + "\"";
                       
                        writestring += "," + dt.Rows[i][4];

                        file.WriteLine(writestring);
                        file.WriteLine(dt.Rows[i][5]);

                        //ToDo # remove, "," remove?
                        ////dt.Rows[i][0].Replace("#", " ").Replace(",", " ");
                        //file.WriteLine("#EXTINF:-1 tvg-name=\"" + dt.Rows[i][0] + "\" tvg-id=\"" + dt.Rows[i][1] + "\" group-title=\"" + dt.Rows[i][2] + "\" tvg-logo=\"" + dt.Rows[i][3] + "\"," + dt.Rows[i][4]);
                        //file.WriteLine(dt.Rows[i][5]);

                    }

                }

                toSave(false);
 
                button_revert.Visible = true;
                Cursor.Current = Cursors.Default;
            }
        }



        private void button_moveUp_Click(object sender, EventArgs e)
        {
            if ((Control.ModifierKeys == Keys.Control))
            {
                MoveLineTop();
            }
            else
            {
                MoveLine(-1);
            }
            
        }

        private void button_moveDown_Click(object sender, EventArgs e)
        {
            //bug remove sorting -> .sort="" -> write table before
            //dt.DefaultView.Sort = "";
            //dataGridView1.Columns.Cast<DataGridViewColumn>().ToList().ForEach(f => f.SortMode = DataGridViewColumnSortMode.NotSortable);

            MoveLine(1);
        }

        private void button_add_Click(object sender, EventArgs e)  
        {
            DataRow dr = dt.NewRow();

            
            if (dataGridView1.RowCount > 0)
            {
                int a = dataGridView1.SelectedCells[0].RowIndex;  // row index in a datatable
                dr[0] = "Name"; dr[1] = "id"; dr[2] = "Title"; dr[3] = "Logo";
                dr[4] = "Name2"; dr[5] = "Link";

                dt.Rows.InsertAt(dr, a);
                
            }
            else
            {

                dt.TableName = "IPTV";
             
                dt.Columns.Add("Name"); dt.Columns.Add("id"); dt.Columns.Add("Title");
                dt.Columns.Add("logo"); dt.Columns.Add("Name2"); dt.Columns.Add("Link");
                dr[0] = "Name"; dr[1] = "id"; dr[2] = "Title"; dr[3] = "Logo";
                dr[4] = "Name2"; dr[5] = "Link";

                dt.Rows.InsertAt(dr, 0);

               
                dataGridView1.DataSource = dt;
                dataGridView1.AllowUserToAddRows = false;
            }

            toSave(true);
        }


        private void button_vlc_Click(object sender, EventArgs e)
        {
            
            string param = "";

            if (dataGridView1.RowCount > 0 && !string.IsNullOrEmpty(vlcpath))
            {

                // Set cursor as hourglass
                Cursor.Current = Cursors.WaitCursor;

                param = dataGridView1.CurrentRow.Cells[5].Value.ToString();
               
                System.Diagnostics.ProcessStartInfo ps = new System.Diagnostics.ProcessStartInfo();
                ps.FileName = vlcpath + "\\" + "vlc.exe";
                ps.ErrorDialog = false;
                ps.Arguments = " --no-video-title-show " + param;
                
                ps.CreateNoWindow = true; 
                ps.UseShellExecute = false; 

                ps.RedirectStandardOutput = true; 
                ps.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden; 

                using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
                {
                    proc.StartInfo = ps;
                    
                    proc.Start();
                  //  proc.WaitForExit();
                    
                }
                // Set cursor as default arrow
                Cursor.Current = Cursors.Default;

            }
        }




        private void button_del_all_Click(object sender, EventArgs e)  
        {
            if (_taglink) button_check.PerformClick();

            if (dataGridView1.RowCount > 0)
            {
                switch (MessageBox.Show("Delete List?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation))
                {
                    case DialogResult.Yes:

                        dt.Clear();
                        dt.Columns.Clear();
                        toSave(false);
                        plabel_Filename.Text = "";
                        button_revert.Visible = false;
                        break;

                    case DialogResult.No:
                        
                        break;
                }
                
            }
        }

        private void button_revert_Click(object sender, EventArgs e)
        {
            if (_taglink) button_check.PerformClick();
            //message box -> delete all -> open filename
            switch (MessageBox.Show("Reload File?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation))
            {
                case DialogResult.Yes:
                    importDataset(plabel_Filename.Text, false);
                    toSave(false);
                    break;

                case DialogResult.No:

                    break;
            }
        }

        /*--------------------------------------------------------------------------------*/
        // contextMenueStrip Entries
        /*--------------------------------------------------------------------------------*/

        private async void playToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView1.RowCount == 0) return;

            dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Selected = true;
            string jLink = dataGridView1.CurrentRow.Cells[5].Value.ToString();
           

            jLink = "{ \"jsonrpc\":\"2.0\",\"method\":\"Player.Open\",\"params\":{ \"item\":{ \"file\":\"" + jLink + "\"} },\"id\":0}";

            //  if (ClassHelp.PingHost(rpi_ip,22))
            await ClassKodi.Run(jLink);

        }

        private void copyRowMenuItem_Click(object sender, EventArgs e)
        {
            dataGridView1.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            //copy selection to whatever
            if (dataGridView1.CurrentCell.Value != null && dataGridView1.GetCellCount(DataGridViewElementStates.Selected) > 0)
            {

                dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Selected = true;

                try
               {
                    // Add the selection to the clipboard.
                   
                    Clipboard.SetDataObject(this.dataGridView1.GetClipboardContent());
#if DEBUG
                    Console.WriteLine(Clipboard.GetText());  
#endif
               }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    MessageBox.Show("The Clipboard could not be accessed. Please try again.");
                    Clipboard.Clear();
                }
            }
        }

        private void pasteRowMenuItem_Click(object sender, EventArgs e)
        {
            
            bool _dtEmpty = false;

            if (dataGridView1.RowCount == 0)
            {
               _dtEmpty = true;
                DataRow dr = dt.NewRow();
              
                dt.Columns.Add("Name"); dt.Columns.Add("id"); dt.Columns.Add("Title");
                dt.Columns.Add("logo"); dt.Columns.Add("Name2"); dt.Columns.Add("Link");
                dataGridView1.DataSource = dt;

            }

            DataObject o = (DataObject)Clipboard.GetDataObject();
            
           
            if (Clipboard.ContainsText())
            {
                try
                {
                    int a = 0;
                    if (!_dtEmpty) a = dataGridView1.SelectedCells[0].RowIndex;  //select row in a datatable

                    string[] pastedRows = Regex.Split(o.GetData(DataFormats.UnicodeText).ToString().TrimEnd("\r\n".ToCharArray()), "\r\n");
                    foreach (string pastedRow in pastedRows)
                    {
                        string[] pastedRowCells = pastedRow.Split(new char[] { '\t' });

                        for (int i = 0; i < pastedRowCells.Length; i++)
                        {
                            if (pastedRowCells[i] != "")
                            {
                                dr = dt.NewRow();
                                dr["Name"] = pastedRowCells[i]; dr["id"] = pastedRowCells[i + 1]; dr["Title"] = pastedRowCells[i + 2];
                                dr["logo"] = pastedRowCells[i + 3]; dr["Name2"] = pastedRowCells[i + 4]; dr["Link"] = pastedRowCells[i + 5];

                                if (_dtEmpty)
                                {
                                    dt.Rows.Add(dr);
                                }
                                else
                                {
                                    dt.Rows.InsertAt(dr, a);  
                                }

                                i += 5;
                            }

                        }

                    }
                    toSave(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Paste operation failed. " + ex.Message, "Copy/Paste", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }







        private void cutRowMenuItem_Click(object sender, EventArgs e)
        {
            dataGridView1.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            //copy selection to whatever
            if (dataGridView1.CurrentCell.Value != null && dataGridView1.GetCellCount(DataGridViewElementStates.Selected) > 0)
            {
               
                dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Selected = true;

                try
                {
                    // Add the selection to the clipboard.

                    Clipboard.SetDataObject(this.dataGridView1.GetClipboardContent());
                    button_delLine.PerformClick();
#if DEBUG
                    Console.WriteLine(Clipboard.GetText());
#endif
                    //del line
                    foreach (DataGridViewRow row in dataGridView1.SelectedRows)
                    {
                        int selectedRow = dataGridView1.SelectedRows[0].Index;

                        dt.Rows.RemoveAt(selectedRow);
                    }

                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    MessageBox.Show("The Clipboard could not be accessed. Please try again.");
                    Clipboard.Clear();
                }
            }
        }

        private void toolStripCopy_Click(object sender, EventArgs e)
        {
           
            //Copy to clipboard
            DataObject dataObj = dataGridView1.GetClipboardContent();
            if (dataObj != null)
                Clipboard.SetDataObject(dataObj);
        }

        private void toolStripPaste_Click(object sender, EventArgs e)   //ctrl+v
        {
           
            if (dataGridView1.RowCount == 0)       // when empty
            {
                pasteRowMenuItem.PerformClick();
                return;
            }

             

            int leftshift = Properties.Settings.Default.leftshift;
            try
            {
                string s = Clipboard.GetText();
                string[] lines = s.Split('\n');

                int iRow = dataGridView1.CurrentCell.RowIndex;
                int iCol = dataGridView1.CurrentCell.ColumnIndex;
                DataGridViewCell oCell;
                if (iRow + lines.Length > dataGridView1.Rows.Count - 1)  //true on last line
                {
                    bool bFlag = false;
                    foreach (string sEmpty in lines)
                    {
                        if (sEmpty == "")
                        {
                            bFlag = true;
                        }
                    }

                    int iNewRows = iRow + lines.Length - dataGridView1.Rows.Count;
                    if (iNewRows > 0)     
                    {
                        if (bFlag)
                            dataGridView1.Rows.Add(iNewRows);
                        else
                            dataGridView1.Rows.Add(iNewRows + 1);
                    }
                    else if (iNewRows == 0 && iRow != dataGridView1.Rows.Count -1 )
                        dataGridView1.Rows.Add(iNewRows + 1);  
                }
                foreach (string line in lines)
                {
                    if (iRow < dataGridView1.RowCount && line.Length > 0)
                    {
                        string[] sCells = line.Split('\t');
                        for (int i = 0; i < sCells.GetLength(0); ++i)
                        {
                            if (iCol + i < this.dataGridView1.ColumnCount)
                            {
                                oCell = dataGridView1[iCol + i, iRow];
                                oCell.Value = Convert.ChangeType(sCells[i].Replace("\r", "").Remove(0, leftshift), oCell.ValueType);
                            }
                            else
                            {
                                break;
                            }
                        }
                        iRow++;
                    }
                    else
                    {
                        break;
                    }
                }
                Clipboard.Clear();
            }
            catch (FormatException)
            {
                MessageBox.Show("The data you pasted is in the wrong format for the cell");
                return;
            }


        }



        private void textBox_selectAll_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.TextBox textBox = (System.Windows.Forms.TextBox)sender;
            textBox.SelectAll();
        }

        private void textBox_find_TextChanged(object sender, EventArgs e)
        {
            var colS = Properties.Settings.Default.colSearch;

            if (dataGridView1.Rows.Count > 0)
            {
                dataGridView1.ClearSelection();
                _found = false;

                string _name ="";

               
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                   
                    if (row.Cells[0].Value != null)
                    _name = dt.Rows[row.Index][colS].ToString().ToLower();
                   

                    if (_name.Contains(textBox_find.Text.ToLower()) && textBox_find.Text != "")
                    {
                        dataGridView1.Rows[row.Index].Selected = true;

                      
                        _found = true;
                        textBox_find.ForeColor = System.Drawing.Color.Black;
                    }


                }
                if (!_found)//text red 
                    textBox_find.ForeColor = System.Drawing.Color.Red;
            }


        }



        private void button_dup_Click(object sender, EventArgs e)
        {

            var colD = Properties.Settings.Default.colDupli;

            dataGridView1.ClearSelection();

            if (dataGridView1.Rows.Count > 0)
            {

                for (int row = 0; row < dataGridView1.Rows.Count; row++)
                {
                    for (int a = 1; a < dataGridView1.Rows.Count - row; a++)
                    {
                        if (dataGridView1.Rows[row].Cells[colD].Value.Equals(dataGridView1.Rows[row + a].Cells[colD].Value))
                        {
                           
                            dataGridView1.Rows[row + a].Selected = true;
                            dataGridView1.FirstDisplayedScrollingRowIndex = row + a;

                        }
                    }
                }             
            }

            if (Control.ModifierKeys == Keys.Shift)
            {
                button_delLine.PerformClick();
            }
        }

      

        private void dataGridView1_DragDrop(object sender, DragEventArgs e)
        {
          
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {

                string dirName, shortName, driveName, extName;

                 string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                foreach (string fileName in files)
                {

                     this.path = fileName;

                    dirName = Path.GetDirectoryName(fileName);
                    shortName = Path.GetFileName(fileName);
                    driveName = Path.GetPathRoot(fileName);
                    extName = Path.GetExtension(fileName);


                    if (extName.Equals(".m3u"))
                    {
                        button_revert.Visible = true;

                        if (dataGridView1.RowCount == 0)
                        {
                            importDataset(fileName, false);
                            break;
                        }
                        else  //imoprt and add
                        {
                            importDataset(fileName, true);
                            toSave(true);
                            break;
                        }

                    }
                    toSave(true);

                }
            }
               
            
        }


        private void dataGridView1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }



        /// <summary>
        /// move the selected line up or down
        /// </summary>
        /// <param name="direction">-1 up 1 down</param>
        public void MoveLine(int direction)
        {
            if (_taglink) button_check.PerformClick();

            dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Selected = true;
          

            if (dataGridView1.SelectedCells.Count > 0 && dataGridView1.SelectedRows.Count > 0)  //whole row must be selected
            {
                var row = dataGridView1.SelectedRows[0];
                var maxrow = dataGridView1.RowCount - 1;

                if (row != null)
                {
                    if ((row.Index == 0 && direction == -1) || (row.Index == maxrow && direction == 1)) return;  //check end of dataGridView1
                    
                    var swapRow = dataGridView1.Rows[row.Index + direction];
                    
                    object[] values = new object[swapRow.Cells.Count];

                    foreach (DataGridViewCell cell in swapRow.Cells)
                    {
                        values[cell.ColumnIndex] = cell.Value;
                        cell.Value = row.Cells[cell.ColumnIndex].Value;
                    }

                    foreach (DataGridViewCell cell in row.Cells)
                        cell.Value = values[cell.ColumnIndex];

                    dataGridView1.Rows[row.Index + direction].Selected = true;
                    dataGridView1.Rows[row.Index].Selected = false;
                    dataGridView1.CurrentCell = dataGridView1.Rows[row.Index + direction].Cells[0];  //scroll automatic to cell
                }
                
              //  toSave(true);
            }  
        }

        /// <summary>
        /// move the selected row to top of list
        /// </summary>
        public void MoveLineTop()
        {
            _taglink = false;
            button_check.BackColor = Color.MidnightBlue;
            colorclear();


          //  if (_taglink) button_check.PerformClick();

            dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Selected = true;

            if (dataGridView1.SelectedCells.Count > 0 && dataGridView1.SelectedRows.Count > 0)  //whole row must be selected
            {
                var row = dataGridView1.SelectedRows[0];
                var maxrow = dataGridView1.RowCount /*- 1*/;
                int n = 0;

                while (n < maxrow - 1) 
                {
                    row = dataGridView1.SelectedRows[0];

                    if (row != null)
                    {
                        if ((row.Index == 0 ) || (row.Index == maxrow )) return;  //check end of dataGridView1

                        var swapRow = dataGridView1.Rows[row.Index - 1 ];

                        object[] values = new object[swapRow.Cells.Count];

                        foreach (DataGridViewCell cell in swapRow.Cells)
                        {
                            values[cell.ColumnIndex] = cell.Value;
                            cell.Value = row.Cells[cell.ColumnIndex].Value;
                        }

                        foreach (DataGridViewCell cell in row.Cells)
                            cell.Value = values[cell.ColumnIndex];

                        dataGridView1.Rows[row.Index].Selected = false;
                        dataGridView1.Rows[row.Index - 1 ].Selected = true;
                        
                       
                    }
                    n += 1;
                } 

                toSave(true);
            }
        }

        /// <summary>
        /// changes icon if file is modified
        /// </summary>
        public void toSave(bool hasChanged)
        {
           

            isModified = hasChanged;

            if (hasChanged)
                button_save.BackgroundImage = Properties.Resources.content_save_modified;

            if (!hasChanged)
                button_save.BackgroundImage = Properties.Resources.content_save_1_;

        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            toSave(true);
        }

        private void dataGridView1_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {


            toSave(true);

            //if (dataGridView1.SortOrder.ToString() == "Descending") // Check if sorting is Descending
            //{
            //    dt.DefaultView.Sort = dataGridView1.SortedColumn.Name + " DESC"; // Get Sorted Column name and sort it in Descending order
            //}
            //else
            //{
            //    dt.DefaultView.Sort = dataGridView1.SortedColumn.Name + " ASC";  // Otherwise sort it in Ascending order
            //}
            dt = dt.DefaultView.ToTable(); // The Sorted View converted to DataTable and then assigned to table object.
            dt = dt.DefaultView.ToTable("IPTV");
        }

       
        

        private void dataGridView1_DoubleClick(object sender, EventArgs e)
        {
            if (dataGridView1.Rows.Count == 0)
            {
                button_open.PerformClick();
            }
                
        }



        private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                playToolStripMenuItem.PerformClick();
            }
            else
            {
                if (dataGridView1.RowCount > 0 && !string.IsNullOrEmpty(vlcpath)) button_vlc.PerformClick();
            }
                
        }

        private void Button_check_Click(object sender, EventArgs e)
        {
            
            if (!_taglink)
            {
                _taglink = true;
                button_check.BackColor = Color.LightSalmon;
            }
            else if (_taglink)
            {
                if (Control.ModifierKeys == Keys.Control)
                {
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        if (dataGridView1.Rows[row.Index].Cells[0].Style.BackColor == Color.LightSalmon)
                        {
                            dataGridView1.Rows[row.Index].Selected = true;  
                        }
                    }
                    return;
                }

                _taglink = false;
                button_check.BackColor = Color.MidnightBlue;
                colorclear();
                return;
            }

            bool _mark = false;
            if (Control.ModifierKeys == Keys.Control) _mark = true;  //select links
          

            if (!ClassHelp.CheckIPTVStream("http://www.google.com"))
            {
                MessageBox.Show("No internet connection found!");
                return;
            }

            dataGridView1.ClearSelection();

            Cursor.Current = Cursors.WaitCursor;

            if (dataGridView1.Rows.Count > 0)
            {
                colorclear();

                foreach (DataGridViewRow item in dataGridView1.Rows)
                {
                    var iLink = dataGridView1.Rows[item.Index].Cells[5].Value.ToString();

                    if (!ClassHelp.CheckIPTVStream(iLink))
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            if (_mark) dataGridView1.Rows[item.Index].Selected = true;
                            dataGridView1.Rows[item.Index].Cells[i].Style.BackColor = System.Drawing.Color.LightSalmon;
                        }
                        dataGridView1.FirstDisplayedScrollingRowIndex = item.Index;
                    }

                }
            }

            Cursor.Current = Cursors.Default;

           

        }

        private void colorclear()
        {
            foreach (DataGridViewRow item in dataGridView1.Rows)
            {
                for (int j = 0; j < 6; j++)
                {
                    dataGridView1.Rows[item.Index].Cells[j].Style.BackColor = System.Drawing.Color.White;
                }
            }
        }

        private void UndoButton_Click(object sender, EventArgs e)
        {
            if (dt.Rows.Count == 0) return;
            if (redoStack.Count == 0 || redoStack.LoadItem(dataGridView1))
            {
                redoStack.Push(dataGridView1.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => r.Cells.Cast<DataGridViewCell>().Select(c => c.Value).ToArray()).ToArray());
            }

            if (undoStack.Count > 0)
            {
                object[][] gridrows = undoStack.Pop();
                while (gridrows.ItemEquals(dataGridView1.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToArray()))
                {
                    {
                        try
                        {
                            gridrows = undoStack.Pop();  
                        }
                       catch (Exception) { }
                    }
                }
                ignore = true;
               
                dt.Clear();  // row clear
              

                for (int x = 0; x <= gridrows.GetUpperBound(0); x++)
                {
                   
                    dt.Rows.Add(gridrows[x]);              
                }

                ignore = false;

                UndoButton.Enabled = undoStack.Count > 0;
                RedoButton.Enabled = redoStack.Count > 0;
            }
        }

        private void RedoButton_Click(object sender, EventArgs e)
        {

            if (dt.Rows.Count == 0) return;
            if (undoStack.Count == 0 || undoStack.LoadItem(dataGridView1))
            {
                undoStack.Push(dataGridView1.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => r.Cells.Cast<DataGridViewCell>().Select(c => c.Value).ToArray()).ToArray());
            }
            if (redoStack.Count > 0)
            {
                object[][] gridrows = redoStack.Pop();  


                while (gridrows.ItemEquals(dataGridView1.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToArray()))
                {
                    gridrows = redoStack.Pop();
                }
                ignore = true;
                dt.Clear();
                for (int x = 0; x <= gridrows.GetUpperBound(0); x++)
                {
                    
                     dt.Rows.Add(gridrows[x]);
                }

                ignore = false;

                RedoButton.Enabled = redoStack.Count > 0;
                UndoButton.Enabled = undoStack.Count > 0;
            }
        }

        private void DataGridView1_CellValidated(object sender, DataGridViewCellEventArgs e)
        {
            if (ignore) { return; }
            if (undoStack.LoadItem(dataGridView1))
            {
                undoStack.Push(dataGridView1.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => r.Cells.Cast<DataGridViewCell>().Select(c => c.Value).ToArray()).ToArray());
            }
            UndoButton.Enabled = undoStack.Count > 1;
            RedoButton.Enabled = redoStack.Count > 1;
        }

        private void hideToolStripMenuItem_Click(object sender, EventArgs e)
        { //issue #11
          
            foreach (DataGridViewCell cell in dataGridView1.SelectedCells)
            {             
               
                dataGridView1.Columns[dataGridView1.Columns[cell.ColumnIndex].HeaderText].Visible = false;
            }

        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        { //issue #11

            for (int i = 0; i < dataGridView1.ColumnCount; i++)
            {              
                dataGridView1.Columns[dataGridView1.Columns[i].HeaderText].Visible = true;
            }
                
        }

        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
          
            int formWidth = this.Width;   //321/378
            SetFormWidth(formWidth);
            
        }

        private void singleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //get col width, index, hide all others, set form width to col width
           if (!_isSingle)
            {
                int columnIndex = dataGridView1.CurrentCell.ColumnIndex;
                int colw = dataGridView1.Columns[columnIndex].Width;


                for (int i = 0; i < dataGridView1.ColumnCount; i++)
                {
                    if (i != columnIndex)
                        dataGridView1.Columns[dataGridView1.Columns[i].HeaderText].Visible = false;
                 
                }

                this.Size = new Size(Math.Max(colw, 400), 422);

                SetFormWidth(colw);
                contextMenuStrip1.Items[12].Text = "Single column mode Off";
                _isSingle = true;
            }
            else
            {
                for (int i = 0; i < dataGridView1.ColumnCount; i++)
                {
                    if(colShow[i] == 1) dataGridView1.Columns[dataGridView1.Columns[i].HeaderText].Visible = true;
                }
               
                this.Size = new Size(1140, 422);
                SetFormWidth(1140);
                contextMenuStrip1.Items[12].Text = "Single column mode";
                _isSingle = false;

            }
        }

        /// <summary>
        /// set dataGridView width, resize
        /// </summary>
        /// <param name="formWidth">width in pt</param>
        private void SetFormWidth(int formWidth)
        {
            if (formWidth < 500)
            {
                dataGridView1.Size = new Size(400, 387);
                dataGridView1.Location = new Point(0, 0);
            }
            else if (formWidth > 500 )
            {
                dataGridView1.Size = new Size(formWidth - 18, 321);
                dataGridView1.Location = new Point(0, 59);                
            }
        }

    }
}

    /// <summary>
    /// DataGridView Method extensions
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// double buffer on for large files speed up
        /// </summary>
        /// <param name="dgv"></param>
        /// <param name="setting"></param>
        public static void DoubleBuffered(this DataGridView dgv, bool setting)
        {
            //http://bitmatic.com/c/fixing-a-slow-scrolling-datagridview

            Type dgvType = dgv.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(dgv, setting, null);
        }
    }


