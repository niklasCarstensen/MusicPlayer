﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicPlayer
{
    public partial class Statistics : Form
    {
        XNA parent;
        int currentMouseOverRow;
        public bool IsClosed = false;

        public Statistics(XNA parent)
        {
            /*
            this.EnableBlur();
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.LimeGreen;
            TransparencyKey = Color.LimeGreen;
            */
            InitializeComponent();
            this.parent = parent;
        }

        private void Statistics_Load(object sender, EventArgs e)
        {
            bRefresh_Click(this, EventArgs.Empty);
        }

        private void dataGridView1_Resize(object sender, EventArgs e)
        {
            dataGridView1.Columns[0].Width = dataGridView1.Width - 460;
            dataGridView1.Columns[1].Width = 80;
            dataGridView1.Columns[2].Width = 80;
            dataGridView1.Columns[3].Width = 80;
            dataGridView1.Columns[4].Width = 80;
            dataGridView1.Columns[5].Width = 80;
        }

        private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (e.RowIndex > 0 && !Assets.PlayPlaylistSong(dataGridView1.Rows[e.RowIndex].Cells[0].Value.ToString() + ".mp3"))
                    MessageBox.Show("This entry isnt linked to a mp3 file!");
            }
        }

        private void bRefresh_Click(object sender, EventArgs e)
        {
            int RowIndex = dataGridView1.FirstDisplayedScrollingRowIndex;
            dataGridView1.Rows.Clear();
            object[] o = new object[6];
            object[,] SongInfo = Assets.GetSongInformationList();

            for (int i = 0; i < Assets.UpvotedSongNames.Count; i++)
            {
                o[0] = SongInfo[i, 0];
                o[1] = SongInfo[i, 1];
                o[2] = SongInfo[i, 2];
                o[3] = SongInfo[i, 3];
                o[4] = SongInfo[i, 4];
                o[5] = SongInfo[i, 5];
                dataGridView1.Rows.Add(o);
            }

            dataGridView1.Columns[0].Width = dataGridView1.Width - 460;
            dataGridView1.Columns[1].Width = 80;
            dataGridView1.Columns[2].Width = 80;
            dataGridView1.Columns[3].Width = 80;
            dataGridView1.Columns[4].Width = 80;
            dataGridView1.Columns[5].Width = 80;
            if (RowIndex != -1)
                dataGridView1.FirstDisplayedScrollingRowIndex = RowIndex;

            for (int i = 0; i < dataGridView1.Rows.Count; i++)
                if (Assets.currentlyPlayingSongName.Equals(dataGridView1.Rows[i].Cells[0].Value))
                {
                    dataGridView1.Rows[i].Selected = true;
                    int heightInRows = dataGridView1.Height / dataGridView1.Rows[0].Height;
                    int index = i - heightInRows / 2 + 2;
                    if (index < 0)
                        index = 0;
                    //dataGridView1.FirstDisplayedScrollingRowIndex = index;
                }

            if (dataGridView1.SortOrder != SortOrder.None)
                dataGridView1.Sort(dataGridView1.SortedColumn, dataGridView1.SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
        }

        private void bSearch_Click(object sender, EventArgs e)
        {
            string Path = textBox1.Text;
            textBox1.Text = "";

            DistancePerSong[] LDistances = new DistancePerSong[dataGridView1.Rows.Count];
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                LDistances[i].SongDifference = Values.OwnDistanceWrapper(Path, ((string)(dataGridView1.Rows[i].Cells[0].Value)));
                LDistances[i].SongIndex = i;
                dataGridView1.Rows[i].Cells[dataGridView1.Rows[i].Cells.Count - 1].Value = LDistances[i].SongDifference;
            }

            dataGridView1.ClearSelection();
            dataGridView1.Sort(dataGridView1.Columns[dataGridView1.Columns.Count - 1], ListSortDirection.Ascending);
            dataGridView1.FirstDisplayedScrollingRowIndex = 0;
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                bSearch_Click(this, EventArgs.Empty);
        }

        // ContextMenu
        private void dataGridView1_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e != null && e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                ContextMenu m = new ContextMenu();
                m.MenuItems.Add(new MenuItem("Play", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        if (!Assets.PlayPlaylistSong(dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString() + ".mp3"))
                            MessageBox.Show("This entry isnt linked to a mp3 file!");
                    }
                    catch { }
                })));
                m.MenuItems.Add(new MenuItem("Queue", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        Assets.QueueNewSong(dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString(), false);
                    }
                    catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                })));
                m.MenuItems.Add(new MenuItem("Copy Title to Clipboard", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        Clipboard.SetText(dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString());
                    }
                    catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                })));
                m.MenuItems.Add(new MenuItem("Open in Browser", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        Task.Factory.StartNew(() =>
                        {
                            // Get fitting youtube video
                            string url = string.Format("https://www.youtube.com/results?search_query=" + dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString());
                            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(url);
                            req.KeepAlive = false;
                            WebResponse W = req.GetResponse();
                            string ResultURL;
                            using (StreamReader sr = new StreamReader(W.GetResponseStream()))
                            {
                                string html = sr.ReadToEnd();
                                int index = html.IndexOf("href=\"/watch?");
                                string startcuthtml = html.Remove(0, index + 6);
                                index = startcuthtml.IndexOf('"');
                                string cuthtml = startcuthtml.Remove(index, startcuthtml.Length - index);
                                ResultURL = "https://www.youtube.com" + cuthtml;
                            }
                            
                            Uri U = new Uri(ResultURL);
                            Process.Start(U.ToString());
                        });
                    }
                    catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                })));
                if (dataGridView1.Rows[e.RowIndex].Cells[0].Value.ToString().Equals(Path.GetFileNameWithoutExtension(Assets.currentlyPlayingSongName)))
                    m.MenuItems.Add(new MenuItem("Open in Browser with timestamp", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        Task.Factory.StartNew(() =>
                        {
                            // Get fitting youtube video
                            string url = string.Format("https://www.youtube.com/results?search_query=" + dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString());
                            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(url);
                            req.KeepAlive = false;
                            WebResponse W = req.GetResponse();
                            string ResultURL;
                            using (StreamReader sr = new StreamReader(W.GetResponseStream()))
                            {
                                string html = sr.ReadToEnd();
                                int index = html.IndexOf("href=\"/watch?");
                                string startcuthtml = html.Remove(0, index + 6);
                                index = startcuthtml.IndexOf('"');
                                string cuthtml = startcuthtml.Remove(index, startcuthtml.Length - index);
                                ResultURL = "https://www.youtube.com" + cuthtml;
                            }

                            int seconds = (int)(Assets.Channel32.Position / (double)Assets.Channel32.Length * Assets.Channel32.TotalTime.TotalSeconds);
                            Uri U = new Uri(ResultURL + "&t=" + seconds + "s");
                            Process.Start(U.ToString());

                            if (Assets.IsPlaying())
                                Assets.PlayPause();
                        });
                    }
                    catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                })));
                m.MenuItems.Add(new MenuItem("Open in Explorer", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        string path = Assets.GetSongPathFromSongName(dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString());
                        if (!File.Exists(path))
                            return;
                        else
                            Process.Start("explorer.exe", "/select, \"" + path + "\"");
                    }
                    catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                })));
                m.MenuItems.Add(new MenuItem("Rename", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        string path = Assets.GetSongPathFromSongName(dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString());
                        int UpvotedSongNamesIndex = Assets.UpvotedSongNames.IndexOf(dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString() + ".mp3");
                        int PlaylistIndex = Assets.Playlist.IndexOf(path);

                        if (!File.Exists(path))
                            return;

                        stringDialog Dia = new stringDialog("What name should it get?", dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString());
                        Dia.ShowDialog();
                        if (Dia.result == dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString())
                        {
                            MessageBox.Show("You didn't chagne the name...");
                        }
                        else if (Dia.result != "")
                        {
                            try
                            {
                                string dest = path.Split('\\').Reverse().Skip(1).Reverse().Aggregate((i, j) => i + "\\" + j) + "\\" + Dia.result + ".mp3";
                                File.Move(path, dest);
                                Assets.UpvotedSongNames[UpvotedSongNamesIndex] = Dia.result + ".mp3";
                                config.Default.SongPaths = Assets.UpvotedSongNames.ToArray();
                                config.Default.Save();

                                Assets.Playlist[PlaylistIndex] = dest;

                                Assets.UpdateSongChoosingList();
                                bRefresh_Click(null, EventArgs.Empty);
                            } catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                        }
                    }
                    catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                })));
                m.MenuItems.Add(new MenuItem("Update Mp3-Metadata of Selection", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        if (parent.BackgroundOperationRunning)
                        {
                            MessageBox.Show("Multiple BackgroundOperations can not run at the same time!\nWait until the other operation is finished");
                            return;
                        }

                        parent.BackgroundOperationRunning = true;

                        List<string> SongPaths = new List<string>();
                        for (int i = 0; i < dataGridView1.Rows.Count; i++)
                            if (dataGridView1.Rows[i].Selected)
                                SongPaths.Add(Assets.GetSongPathFromSongName((string)dataGridView1.Rows[i].Cells[0].Value));
                        UpdateMetadata updat = new UpdateMetadata(SongPaths.ToArray());

                        if (SongPaths.Count > 0)
                            updat.ShowDialog();
                        else
                            MessageBox.Show("You havent selected anything!\nMake sure to select entire Rows");

                        parent.BackgroundOperationRunning = false;
                    }
                    catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                })));
                m.MenuItems.Add(new MenuItem("Show Cover Picture", ((object s, EventArgs ev) =>
                {
                    try
                    {
                        string path = Assets.GetSongPathFromSongName(dataGridView1.Rows[currentMouseOverRow].Cells[0].Value.ToString());
                        TagLib.File file = TagLib.File.Create(path);
                        TagLib.IPicture pic = file.Tag.Pictures[0];
                        MemoryStream ms = new MemoryStream(pic.Data.Data);
                        if (ms != null && ms.Length > 4096)
                        {
                            Image currentImage = Image.FromStream(ms);
                            path = Values.CurrentExecutablePath + "\\Downloads\\Thumbnail.png";
                            currentImage.Save(path);
                            Process.Start(path);
                        }
                        ms.Close();
                    }
                    catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                })));
                //if (dataGridView1.Rows[e.RowIndex].Cells[5].Value == null)
                    m.MenuItems.Add(new MenuItem("Delete Entry", ((object s, EventArgs ev) =>
                    {
                        try
                        {
                            int index = Assets.UpvotedSongNames.IndexOf(dataGridView1.Rows[e.RowIndex].Cells[0].Value.ToString() + ".mp3");
                            Assets.UpvotedSongNames.RemoveAt(index);
                            Assets.UpvotedSongScores.RemoveAt(index);
                            Assets.UpvotedSongStreaks.RemoveAt(index);
                            Assets.UpvotedSongTotalLikes.RemoveAt(index);
                            Assets.UpvotedSongAddingDates.RemoveAt(index);

                            bRefresh_Click(null, EventArgs.Empty);
                        }
                        catch { MessageBox.Show("OOPSIE WOOPSIE!! Uwu We made a fucky wucky!!"); }
                    })));

                currentMouseOverRow = e.RowIndex;

                m.Show(dataGridView1, new Point(e.X + dataGridView1.GetColumnDisplayRectangle(e.ColumnIndex, true).X, e.Y + dataGridView1.GetRowDisplayRectangle(e.RowIndex, true).Y));
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void Statistics_FormClosed(object sender, FormClosedEventArgs e)
        {
            IsClosed = true;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void toPlaying_Click(object sender, EventArgs e)
        {
            int index = 0;
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
                if (Path.GetFileNameWithoutExtension(Assets.currentlyPlayingSongName).Equals(dataGridView1.Rows[i].Cells[0].Value))
                {
                    index = i;
                    break;
                }

            dataGridView1.FirstDisplayedScrollingRowIndex = index;
        }
    }
}