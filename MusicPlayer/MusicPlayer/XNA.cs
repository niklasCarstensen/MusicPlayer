using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;
using NAudio.Wave;
using NAudio.Dsp;
using System.IO;
using System.Net;
using MediaToolkit.Model;
using MediaToolkit;

namespace MusicPlayer
{
    public enum Visualizations
    {
        line,
        dynamicline,
        fft,
        rawfft,
        barchart,
        grid
    }
    public enum BackGroundModes
    {
        None,
        Blur,
        BlurTeint,
        bg1,
        bg2
    }
    public enum SelectedControl
    {
        VolumeSlider,
        DurationBar,
        DragWindow,
        PlayPauseButton,
        UpvoteButton,
        None,
        CloseButton
    }

    public class XNA : Game
    {
        // Graphics
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        public static Form gameWindowForm;
        RenderTarget2D TempBlur;
        RenderTarget2D BlurredTex;
        static RenderTarget2D TitleTarget;

        // Visualization
        public static Visualizations VisSetting = (Visualizations)config.Default.Vis;
        public static BackGroundModes BgModes = (BackGroundModes)config.Default.Background;
        public static Color primaryColor = Color.FromNonPremultiplied(25, 75, 255, 255);
        public static Color secondaryColor = Color.Green;
        public static GaussianDiagram GauD = null;
        public static DynamicGrid DG;
        static ColorDialog LeDialog;
        static bool ShowingColorDialog = false;
        DropShadow Shadow;

        // Stuff
        System.Drawing.Point MouseClickedPos = new System.Drawing.Point();
        System.Drawing.Point WindowLocation = new System.Drawing.Point();
        SelectedControl selectedControl = SelectedControl.None;
        float SecondRowMessageAlpha;
        string SecondRowMessageText;
        public static float UpvoteSavedAlpha = 0;
        float UpvoteIconAlpha = 0;
        List<string> LastConsoleInput = new List<string>();
        int LastConsoleInputIndex = -1;
        long CurrentDebugTime = 0;
        long CurrentDebugTime2 = 0;
        OptionsMenu optionsMenu;
        public static bool FocusWindow = false;
        public static bool Preload;
        public static bool TaskbarHidden = false;
        int originY;

        // Draw Rectangles
        static Rectangle DurationBar = new Rectangle(51, Values.WindowSize.Y - 28, Values.WindowSize.X - 129, 3);
        static Rectangle VolumeIcon = new Rectangle(Values.WindowSize.X - 132, 16, 24, 24);
        static Rectangle VolumeBar = new Rectangle(Values.WindowSize.X - 100, 24, 75, 8);
        static Rectangle PlayPauseButton = new Rectangle(24, Values.WindowSize.Y - 34, 16, 16);
        static Rectangle Upvote = new Rectangle(24, 43, 20, 20);
        static Rectangle TargetVolumeBar = new Rectangle(); // needs Updates
        static Rectangle ActualVolumeBar = new Rectangle(); // needs Updates
        static Rectangle UpvoteButton = new Rectangle(Values.WindowSize.X - 70, Values.WindowSize.Y - 35, 19, 19);
        static Rectangle CloseButton = new Rectangle(Values.WindowSize.X - 43, Values.WindowSize.Y - 34, 18, 18);

        static Rectangle DurationBarShadow = new Rectangle(DurationBar.X + 5, DurationBar.Y + 5, DurationBar.Width, DurationBar.Height);
        static Rectangle VolumeIconShadow = new Rectangle(VolumeIcon.X + 5, VolumeIcon.Y + 5, VolumeIcon.Width, VolumeIcon.Height);
        static Rectangle VolumeBarShadow = new Rectangle(VolumeBar.X + 5, VolumeBar.Y + 5, VolumeBar.Width, VolumeBar.Height);
        static Rectangle PlayPauseButtonShadow = new Rectangle(PlayPauseButton.X + 5, PlayPauseButton.Y + 5, PlayPauseButton.Width, PlayPauseButton.Height);
        static Rectangle UpvoteShadow = new Rectangle(Upvote.X + 5, Upvote.Y + 5, Upvote.Width, Upvote.Height);
        static Rectangle UpvoteButtonShadow = new Rectangle(UpvoteButton.X + 5, UpvoteButton.Y + 5, UpvoteButton.Width, UpvoteButton.Height);
        static Rectangle CloseButtonShadow = new Rectangle(CloseButton.X + 5, CloseButton.Y + 5, CloseButton.Width, CloseButton.Height);

        // Hitbox Rectangles
        Rectangle DurationBarHitbox = new Rectangle(DurationBar.X, DurationBar.Y - 10, DurationBar.Width, 23);
        Rectangle VolumeBarHitbox = new Rectangle(Values.WindowSize.X - 100, 20, 110, 16);
        Rectangle PlayPauseButtonHitbox = new Rectangle(14, Values.WindowSize.Y - 39, 26, 26);
        Rectangle UpvoteButtonHitbox = new Rectangle(UpvoteButton.X, UpvoteButton.Y, 20, 20);

        public object HttpUtility { get; private set; }

        public XNA()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            graphics.PreferredBackBufferWidth = Values.WindowSize.X;
            graphics.PreferredBackBufferHeight = Values.WindowSize.Y;
            gameWindowForm = (Form)System.Windows.Forms.Control.FromHandle(Window.Handle);
            gameWindowForm.FormBorderStyle = FormBorderStyle.None;
            gameWindowForm.Move += ((object sender, EventArgs e) => {
                gameWindowForm.Location = new System.Drawing.Point(config.Default.WindowPos.X, config.Default.WindowPos.Y);
            });
            //this.TargetElapsedTime = TimeSpan.FromSeconds(1.0f / 120.0f);
            //graphics.SynchronizeWithVerticalRetrace = false;
            //IsFixedTimeStep = false;
            IsMouseVisible = true;
        }
        protected override void Initialize()
        {
            base.Initialize();
        }
        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            Preload = config.Default.Preload;

            gameWindowForm.FormClosing += (object sender, FormClosingEventArgs e) => {
                InterceptKeys.UnhookWindowsHookEx(InterceptKeys._hookID);
                Assets.DisposeNAudioData();
                Assets.SaveUserSettings();
                //MessageBox.Show("ApplicationExit EVENT");
            };

            Assets.Load(Content, GraphicsDevice);
            
            BlurredTex = new RenderTarget2D(GraphicsDevice, Values.WindowSize.X + 100, Values.WindowSize.Y + 100);
            TempBlur = new RenderTarget2D(GraphicsDevice, Values.WindowSize.X + 100, Values.WindowSize.Y + 100);

            InactiveSleepTime = new TimeSpan(0);

            DG = new DynamicGrid(new Rectangle(35, (int)(Values.WindowSize.Y / 1.25f) - 60, Values.WindowSize.X - 70, 70), 4, 0.96f, 2.5f);

            Console.WriteLine("Finished Loading!");
            StartSongInputLoop();

            ShowSecondRowMessage("Found " + Assets.Playlist.Count + " Songs!", 3);
            
            Console.CancelKeyPress += ((object o, ConsoleCancelEventArgs e) =>
            {
                e.Cancel = true;
                Console.ForegroundColor = ConsoleColor.Red;
                if (Console.CursorLeft != 0)
                {
                    Console.WriteLine();
                    originY++;
                }
                Console.WriteLine("Canceled by user!");
                originY++;
            });
            
            Shadow = new DropShadow(gameWindowForm, true);
            Shadow.Show();
        }
        void StartSongInputLoop()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    string Path = "";
                    originY = Console.CursorTop;
                    while (!Path.Contains(".mp3\""))
                    {
                        Thread.Sleep(5);
                        Console.SetCursorPosition(0, originY);
                        for (int i = 0; i < Path.Length / 65 + 2; i++)
                            Console.Write("                                                                    ");
                        Console.SetCursorPosition(0, originY);
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("Play Song: ");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write(Path);

                        ConsoleKeyInfo e = Console.ReadKey();

                        if (e.Key == ConsoleKey.UpArrow)
                        {
                            LastConsoleInputIndex++;
                            if (LastConsoleInputIndex > LastConsoleInput.Count - 1)
                                LastConsoleInputIndex = LastConsoleInput.Count - 1;
                            if (LastConsoleInput.Count > 0)
                                Path = LastConsoleInput[LastConsoleInput.Count - 1 - LastConsoleInputIndex];
                        }

                        if (e.Key == ConsoleKey.DownArrow)
                        {
                            LastConsoleInputIndex--;
                            if (LastConsoleInputIndex < -1)
                                LastConsoleInputIndex = -1;
                            if (LastConsoleInputIndex > -1)
                                Path = LastConsoleInput[LastConsoleInput.Count - 1 - LastConsoleInputIndex];
                            else
                                Path = "";
                        }

                        if (e.Key == ConsoleKey.Enter)
                        {
                            #region ConsoleCommands
                            if (Path == "/cls")
                            {
                                LastConsoleInput.Add(Path);
                                Path = "";
                                Console.Clear();
                                originY = 0;
                            }

                            if (Path == "/f")
                            {
                                LastConsoleInput.Add(Path);
                                Path = "";
                                FocusWindow = true;
                                originY++;
                            }

                            if (Path == "/showinweb" || Path == "/showinnet" || Path == "/net" || Path == "/web")
                            {
                                LastConsoleInput.Add(Path);
                                originY++;
                                Path = "";
                                Console.SetCursorPosition(0, originY);
                                Console.WriteLine("Searching for " + Assets.currentlyPlayingSongName.Split('.').First() + "...");
                                originY++;

                                Task.Factory.StartNew(() =>
                                {
                                    // Use the I'm Feeling Lucky URL
                                    var url = string.Format("https://www.google.com/search?num=100&site=&source=hp&q={0}&btnI=1", Assets.currentlyPlayingSongName.Split('.').First());
                                    WebRequest req = HttpWebRequest.Create(url);
                                    Uri U = req.GetResponse().ResponseUri;

                                    Process.Start(U.ToString());
                                });
                            }

                            if (Path == "/help")
                            {
                                LastConsoleInput.Add(Path);
                                Path = "";
                                Console.WriteLine();
                                Console.WriteLine("All currently implemented cammands: ");
                                Console.WriteLine();
                                Console.WriteLine("/f - focuses the main window");
                                Console.WriteLine();
                                Console.WriteLine("/cls - clears the console");
                                Console.WriteLine();
                                Console.WriteLine("/download | /d - Searches for the current song on youtube, converts it to mp3   and puts it into the standard folder");
                                Console.WriteLine();
                                Console.WriteLine("/showinweb | /showinnet | /net | /web - will search google for the current      songs name and display the first result in the standard browser");
                                Console.WriteLine();
                                originY += 12;
                            }

                            if (Path.StartsWith("/d"))
                            {
                                try
                                {
                                    Console.WriteLine();
                                    LastConsoleInput.Add(Path);
                                    string download;
                                    if (Path.StartsWith("/download"))
                                        download = Path.Remove(0, "/download".Length + 1);
                                    else
                                        download = Path.Remove(0, "/d".Length + 1);
                                    Path = "";

                                    // Get fitting youtube video
                                    string url = string.Format("https://www.youtube.com/results?search_query=" + download);
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

                                    // Get video title
                                    req = (HttpWebRequest)HttpWebRequest.Create(ResultURL);
                                    req.KeepAlive = false;
                                    W = req.GetResponse();
                                    string VideoTitle;
                                    using (StreamReader sr = new StreamReader(W.GetResponseStream()))
                                    {
                                        string html = sr.ReadToEnd();
                                        int index = html.IndexOf("<span id=\"eow-title\" class=\"watch-title\" dir=\"ltr\" title=\"");
                                        string startcuthtml = html.Remove(0, index + "<span id=\"eow-title\" class=\"watch-title\" dir=\"ltr\" title=\"".Length);
                                        index = startcuthtml.IndexOf('"');
                                        VideoTitle = startcuthtml.Remove(index, startcuthtml.Length - index);

                                        // Decode the encoded string.
                                        StringWriter myWriter = new StringWriter();
                                        System.Web.HttpUtility.HtmlDecode(VideoTitle, myWriter);
                                        VideoTitle = myWriter.ToString();

                                        Console.WriteLine("Found matching Song at " + ResultURL + " named " + VideoTitle);
                                    }

                                    // Delete File if there still is one for some reason? The thread crashes otherwise so better do it.
                                    string videofile = "" + Environment.CurrentDirectory + "\\Downloads\\File.mp4";
                                    if (File.Exists(videofile))
                                        File.Delete(videofile);

                                    // Download Video File
                                    Process P = new Process();
                                    string b = Environment.CurrentDirectory;
                                    P.StartInfo = new ProcessStartInfo("youtube-dl.exe", "-o \"/Downloads/File.mp4\" " + ResultURL)
                                    {
                                        UseShellExecute = false
                                    };

                                    P.Start();
                                    P.WaitForExit();

                                    // Convert Video File to mp3 and put it into the default Foulder
                                    Console.WriteLine("Converting to mp3...");
                                    MediaFile input = new MediaFile { Filename = videofile };
                                    MediaFile output = new MediaFile { Filename = config.Default.MusicPath + "\\" + VideoTitle + ".mp3" };

                                    using (var engine = new Engine())
                                    {
                                        engine.GetMetadata(input);
                                        engine.Convert(input, output);
                                    }

                                    File.Delete(videofile);
                                    Assets.PlayNewSong(output.Filename);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                }
                                
                                originY = Console.CursorTop;
                            }
                            else
                                break;

                            #endregion
                        }

                        if (e.Key == ConsoleKey.Backspace)
                        {
                            if (Path.Length > 0)
                                Path = Path.Remove(Path.Length - 1);
                        }
                        else if (e.Key >= (ConsoleKey)48 && e.Key <= (ConsoleKey)90 || 
                                 e.Key >= (ConsoleKey)186 && e.Key <= (ConsoleKey)226 ||
                                 e.Key == ConsoleKey.Spacebar)
                            Path += e.KeyChar;
                    }
                    Console.WriteLine();
                    if (Assets.PlayNewSong(Path))
                        LastConsoleInput.Add(Path.Trim('"'));
                }
            });
        }

        protected override void Update(GameTime gameTime)
        {
            //FPSCounter.Update(gameTime);
            Control.Update();
            if (gameWindowForm.Focused)
                ComputeControls();

            // Next / Previous Song [MouseWheel] ((On Win10 mouseWheel input is send to the process even if its not focused))
            if (Control.ScrollWheelWentDown())
                Assets.GetPreviousSong();
            if (Control.ScrollWheelWentUp())
                Assets.GetNextSong(false, true);

            if (FocusWindow) { gameWindowForm.BringToFront(); gameWindowForm.Activate(); FocusWindow = false; }

            if (Assets.IsCurrentSongUpvoted)
            {
                if (UpvoteIconAlpha < 1)
                    UpvoteIconAlpha += 0.05f;
            }
            else if (UpvoteIconAlpha > 0)
                UpvoteIconAlpha -= 0.05f;

            Values.Timer++;
            SecondRowMessageAlpha -= 0.004f;
            UpvoteSavedAlpha -= 0.01f;

            Values.LastOutputVolume = Values.OutputVolume;
            if (Assets.output != null && Assets.output.PlaybackState == PlaybackState.Playing)
            {
                if (Assets.WaveBuffer != null)
                    Values.OutputVolume = Values.GetRootMeanSquareApproximation(Assets.WaveBuffer);

                if (Values.OutputVolume < 0.0001f)
                    Values.OutputVolume = 0.0001f;
                
                if (Assets.Channel32 != null && Assets.Channel32.Position > Assets.Channel32.Length)
                    Assets.GetNextSong(false, false);

                if (config.Default.Preload)
                {
                    if (Assets.EntireSongWaveBuffer != null)
                        Assets.UpdateWaveBufferWithEntireSongWB();
                }
                else
                    Assets.UpdateWaveBuffer();

                if (VisSetting != Visualizations.line && Assets.Channel32 != null)
                {
                    Assets.UpdateFFTbuffer();
                    UpdateGD();
                }
                if (VisSetting == Visualizations.grid)
                {
                    DG.ApplyForce(new Vector2(DG.Field.X, DG.Field.Y + DG.Field.Height), -Values.OutputVolumeIncrease * Values.TargetVolume * 5);
                    //DG.ApplyForce(new Vector2(Values.WindowSize.X / 2f, DG.Field.Y + DG.Field.Height), Values.OutputVolumeIncrease * Values.TargetVolume * 50);
                    DG.ApplyForce(new Vector2(DG.Field.X + DG.Field.Width, DG.Field.Y + DG.Field.Height), -Values.OutputVolumeIncrease * Values.TargetVolume * 5);

                    for (int i = 1; i < DG.Points.GetLength(0) - 1; i++)
                    {
                        float Whatever = -(DG.Points[i, 0].Pos.Y - DG.Field.Y - DG.Field.Height);
                        /*float Acc = -GauD.GetMaximum(i * DG.PointSpacing, (i + 1) * DG.PointSpacing) / Whatever * 5f + 1;
                        DG.Points[i, 0].Vel.Y = Acc;
                        if (DG.Points[i, 0].Pos.Y < DG.Field.Y - 100)
                        {
                            DG.Points[i, 0].Vel.Y = 0;
                            DG.Points[i, 0].Pos.Y = DG.Field.Y - 100;
                        }
                        if (DG.Points[i, 0].Pos.Y > DG.Field.Y + DG.Field.Height)
                        {
                            DG.Points[i, 0].Vel.Y = 0;
                            DG.Points[i, 0].Pos.Y = DG.Field.Y + DG.Field.Height;
                        }*/

                        float Target = -GauD.GetMaximum(i * DG.PointSpacing, (i + 1) * DG.PointSpacing) / Whatever * 200;
                        DG.Points[i, 0].Vel.Y += ((Target + DG.Field.Y + DG.Field.Height - 30) - DG.Points[i, 0].Pos.Y) / 3f;
                    }
                }
            }
            if (VisSetting == Visualizations.grid)
            {
                DG.Update();
                for (int i = 1; i < DG.Points.GetLength(0) - 1; i++)
                    DG.Points[i, 0].Vel.Y += ((DG.Field.Y + DG.Field.Height - 30) - DG.Points[i, 0].Pos.Y) / 2.5f;
            }

            //Values.OutputVolumeIncrease = Values.OutputVolume - Values.LastOutputVolume;

            if (Assets.Channel32 != null)
                Assets.Channel32.Volume = Values.TargetVolume - Values.OutputVolume * Values.TargetVolume;

            CurrentDebugTime = Stopwatch.GetTimestamp();
            UpdateRectangles();
            Debug.WriteLine(Stopwatch.GetTimestamp() - CurrentDebugTime);

            base.Update(gameTime);
        }
        public static void CheckForRequestedSongs()
        {
            bool Worked = false;
            while (!Worked)
            {
                try
                {
                    RequestedSong.Default.Reload();
                    if (RequestedSong.Default.RequestedSongString != "")
                    {
                        Assets.PlayNewSong(RequestedSong.Default.RequestedSongString);
                        RequestedSong.Default.RequestedSongString = "";
                        RequestedSong.Default.Save();
                    }
                    Worked = true;
                }
                catch { }
            }
        }
        void ComputeControls()
        {
            // Mouse Controls
            if (Control.WasLMBJustPressed() && gameWindowForm.Focused &&
                Control.GetMouseRect().Intersects(Values.WindowRect) ||
                Control.WasLMBJustPressed() && gameWindowForm.Focused &&
                new Rectangle(Control.LastMS.X, Control.LastMS.Y, 1, 1).Intersects(Values.WindowRect))
            {
                MouseClickedPos = new System.Drawing.Point(Control.CurMS.X, Control.CurMS.Y);
                WindowLocation = gameWindowForm.Location;

                Shadow.BringToFront();
                gameWindowForm.BringToFront();

                if (Control.GetMouseRect().Intersects(DurationBarHitbox))
                    selectedControl = SelectedControl.DurationBar;
                else if (Control.GetMouseRect().Intersects(VolumeBarHitbox))
                    selectedControl = SelectedControl.VolumeSlider;
                else if (Control.GetMouseRect().Intersects(UpvoteButtonHitbox))
                    selectedControl = SelectedControl.UpvoteButton;
                else if (Control.GetMouseRect().Intersects(PlayPauseButtonHitbox))
                    selectedControl = SelectedControl.PlayPauseButton;
                else if (Control.GetMouseRect().Intersects(CloseButton))
                    selectedControl = SelectedControl.CloseButton;
                else
                    selectedControl = SelectedControl.DragWindow;
            }
            if (Control.WasLMBJustReleased())
            {
                if (selectedControl == SelectedControl.DurationBar)
                    Assets.output.Play();
                selectedControl = SelectedControl.None;
            }

            switch (selectedControl)
            {
                case SelectedControl.VolumeSlider:
                    float value = (Control.GetMouseVector().X - VolumeBar.X) / (VolumeBar.Width * 2f);
                    if (value < 0) value = 0;
                    if (value > 0.5f) value = 0.5f;
                    Values.TargetVolume = value;
                    break;

                case SelectedControl.PlayPauseButton:
                    if (Control.WasLMBJustPressed())
                        Assets.PlayPause();
                    break;

                case SelectedControl.CloseButton:
                    if (Control.WasLMBJustPressed())
                        Exit();
                    break;

                case SelectedControl.UpvoteButton:
                    if (Control.WasLMBJustPressed())
                        Assets.IsCurrentSongUpvoted = !Assets.IsCurrentSongUpvoted;
                    break;

                case SelectedControl.DurationBar:
                    Assets.Channel32.Position =
                           (long)(((Control.GetMouseVector().X - DurationBar.X) / DurationBar.Width) *
                           Assets.Channel32.TotalTime.TotalSeconds * Assets.Channel32.WaveFormat.AverageBytesPerSecond);

                    if (Control.CurMS.X == Control.LastMS.X)
                        Assets.output.Pause();
                    else
                        Assets.output.Play();
                    break;

                case SelectedControl.DragWindow:
                    config.Default.WindowPos = new System.Drawing.Point(gameWindowForm.Location.X + Control.CurMS.X - MouseClickedPos.X,
                                                                        gameWindowForm.Location.Y + Control.CurMS.Y - MouseClickedPos.Y);
                    KeepWindowInScreen();
                    break;
            }
            gameWindowForm.Location = config.Default.WindowPos;

            // Pause [Space]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Space))
                Assets.PlayPause();

            // Set Location to (0, 0) [0]
            if (Control.CurKS.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D0) ||
                Control.CurKS.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.NumPad0))
                config.Default.WindowPos = new System.Drawing.Point(0, 0);

            // Open OptionsMenu [O / F1]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.O) ||
                Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.F1))
            {
                if (optionsMenu == null || optionsMenu.IsDisposed)
                    optionsMenu = new OptionsMenu();

                optionsMenu.Show();
            }

            // Swap Visualisations [V]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.V))
            {
                VisSetting++;
                if ((int)VisSetting > Enum.GetNames(typeof(Visualizations)).Length - 1)
                    VisSetting = 0;

                if (VisSetting == Visualizations.dynamicline)
                    VisSetting = Visualizations.fft;
            }

            // Swap Backgrounds [B]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.B))
            {
                BgModes++;
                if ((int)BgModes > Enum.GetNames(typeof(BackGroundModes)).Length - 1)
                    BgModes = 0;
            }

            // New Color [C]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.C))
                ShowColorDialog();

            // Show Console [K]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.K))
            {
                Values.ShowWindow(Values.GetConsoleWindow(), 0x09);
                Values.SetForegroundWindow(Values.GetConsoleWindow());
            }

            // Toggle Anti-Alising [A]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.A))
                config.Default.AntiAliasing = !config.Default.AntiAliasing;

            // Toggle Preload [P]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.P))
            {
                Preload = !Preload;
                ShowSecondRowMessage("Preload was set to " + Preload + " \nThis setting will be applied when the next song starts", 1);
            }

            // Upvote/Like Current Song [L]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.L))
                Assets.IsCurrentSongUpvoted = !Assets.IsCurrentSongUpvoted;

            // Higher / Lower Volume [Up/Down]
            if (Control.CurKS.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up))
                Values.TargetVolume += 0.005f;
            if (Control.CurKS.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down))
                Values.TargetVolume -= 0.005f;
            if (Values.TargetVolume > 0.5f)
                Values.TargetVolume = 0.5f;
            if (Values.TargetVolume < 0)
                Values.TargetVolume = 0;

            // Next / Previous Song [Left/Right]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Left))
                Assets.GetPreviousSong();
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Right))
                Assets.GetNextSong(false, true);

            // Close [Esc]
            if (Control.CurKS.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && base.IsActive)
                Exit();

            // Show Music File in Explorer [E]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.E))
            {
                if (!File.Exists(Assets.currentlyPlayingSongPath))
                    return;
                else
                    Process.Start("explorer.exe", "/select, \"" + Assets.currentlyPlayingSongPath + "\"");
            }

            // Reset Music Source Folder [S]
            if (Control.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.S))
            {
                ResetMusicSourcePath();
            }
        }
        public static void ShowColorDialog()
        {
            Task.Factory.StartNew(() =>
            {
                if (LeDialog == null)
                {
                    LeDialog = new ColorDialog();
                    LeDialog.AllowFullOpen = true;
                    LeDialog.AnyColor = true;
                    LeDialog.Color = System.Drawing.Color.FromArgb(primaryColor.A, primaryColor.R, primaryColor.G, primaryColor.B);

                    Color[] Background = new Color[Assets.bg.Bounds.Width * Assets.bg.Bounds.Height];
                    Vector3 AvgColor = new Vector3();
                    Assets.bg.GetData(Background);

                    for (int i = 0; i < Background.Length; i++)
                        AvgColor.X += Background[i].R;
                    AvgColor.X /= Background.Length;

                    for (int i = 0; i < Background.Length; i++)
                        AvgColor.Y += Background[i].G;
                    AvgColor.Y /= Background.Length;

                    for (int i = 0; i < Background.Length; i++)
                        AvgColor.Z += Background[i].B;
                    AvgColor.Z /= Background.Length;

                    System.Drawing.Color AvgC = System.Drawing.Color.FromArgb(255, (int)AvgColor.Z, (int)AvgColor.Y, (int)AvgColor.X);
                    System.Drawing.Color DefC = System.Drawing.Color.FromArgb(255, 255, 75, 25);
                    System.Drawing.Color SysC = System.Drawing.Color.FromArgb(255, Assets.SystemDefaultColor.B, Assets.SystemDefaultColor.G, Assets.SystemDefaultColor.R);

                    LeDialog.CustomColors = new int[]{
                        SysC.ToArgb(), SysC.ToArgb(), SysC.ToArgb(), AvgC.ToArgb(), AvgC.ToArgb(), AvgC.ToArgb(), AvgC.ToArgb(), AvgC.ToArgb(),
                        DefC.ToArgb(), DefC.ToArgb(), DefC.ToArgb(), DefC.ToArgb(), DefC.ToArgb(), DefC.ToArgb(), DefC.ToArgb(), DefC.ToArgb()};
                }
                if (!ShowingColorDialog)
                {
                    ShowingColorDialog = true;
                    DialogResult DR = LeDialog.ShowDialog();
                    if (DR == DialogResult.OK)
                    {
                        primaryColor = Color.FromNonPremultiplied(LeDialog.Color.R, LeDialog.Color.G, LeDialog.Color.B, LeDialog.Color.A);
                        secondaryColor = Color.Lerp(primaryColor, Color.White, 0.4f);
                    }
                    ShowingColorDialog = false;
                }
            });
        }
        public static void ResetMusicSourcePath()
        {
            DialogResult DR = MessageBox.Show("Are you sure you want to reset the music source path?", "Source Path Reset", MessageBoxButtons.YesNo);

            if (DR != DialogResult.Yes)
                return;
            
            config.Default.MusicPath = "";
            ProcessStartInfo Info = new ProcessStartInfo();
            Info.Arguments = "/C ping 127.0.0.1 -n 2 && cls && \"" + Application.ExecutablePath + "\"";
            Info.WindowStyle = ProcessWindowStyle.Hidden;
            Info.CreateNoWindow = true;
            Info.FileName = "cmd.exe";
            Process.Start(Info);
            Application.Exit();
        }
        void UpdateGD()
        {
            CurrentDebugTime = Stopwatch.GetTimestamp();
            if (Assets.FFToutput != null)
            {
                Debug.WriteLine("GD Update 0 " + (Stopwatch.GetTimestamp() - CurrentDebugTime));
                float ReadLength = Assets.FFToutput.Length / 3f;
                float[] values = new float[Values.WindowSize.X - 70];
                for (int i = 70; i < Values.WindowSize.X; i++)
                {
                    double lastindex = Math.Pow(ReadLength, (i - 1) / (double)Values.WindowSize.X);
                    double index = Math.Pow(ReadLength, i / (double)Values.WindowSize.X);
                    values[i - 70] = Assets.GetMaxHeight(Assets.FFToutput, (int)lastindex, (int)index);
                }
                Debug.WriteLine("GD Update 1 " + (Stopwatch.GetTimestamp() - CurrentDebugTime));
                if (GauD == null)
                    GauD = new GaussianDiagram(values, new Point(35, (int)(Values.WindowSize.Y / 1.25f)), 175, true, 3);
                else
                {
                    GauD.Update(values);
                    //GauD.MultiplyWith((1 - Values.OutputVolume) * 0.5f + 1f);
                    GauD.Smoothen();
                }

                Debug.WriteLine("GD Update 2 " + (Stopwatch.GetTimestamp() - CurrentDebugTime));
            }
        }
        void UpdateRectangles()
        {
            //TargetVolumeBar = new Rectangle(Values.WindowSize.X - 100, 24, (int)(75 * Values.TargetVolume * 2), 8);
            //if (Assets.Channel32 != null)
            //    ActualVolumeBar = new Rectangle(Values.WindowSize.X - 25 - 75, 24, (int)(75 * Assets.Channel32.Volume * 2), 8);
        
            TargetVolumeBar.X = Values.WindowSize.X - 100;
            TargetVolumeBar.Y = 24;
            TargetVolumeBar.Width = (int)(75 * Values.TargetVolume * 2);
            TargetVolumeBar.Height = 8;

            if (Assets.Channel32 != null)
            {
                ActualVolumeBar.X = Values.WindowSize.X - 25 - 75;
                ActualVolumeBar.Y = 24;
                ActualVolumeBar.Width = (int)(75 * Assets.Channel32.Volume * 2);
                ActualVolumeBar.Height = 8;
            }
            
        }
        public static void KeepWindowInScreen()
        {
            Rectangle VirtualWindow = new Rectangle(config.Default.WindowPos.X, config.Default.WindowPos.Y, 
                gameWindowForm.Bounds.Width, gameWindowForm.Bounds.Height);
            Rectangle[] ScreenBoxes = new Rectangle[Screen.AllScreens.Length];

            for (int i = 0; i < ScreenBoxes.Length; i++)
                ScreenBoxes[i] = new Rectangle(Screen.AllScreens[i].Bounds.X, Screen.AllScreens[i].Bounds.Y, 
                    Screen.AllScreens[i].Bounds.Width, Screen.AllScreens[i].Bounds.Height - 56);

            Point[] WindowPoints = new Point[4];
            WindowPoints[0] = new Point(VirtualWindow.X, VirtualWindow.Y);
            WindowPoints[1] = new Point(VirtualWindow.X + VirtualWindow.Width, VirtualWindow.Y);
            WindowPoints[2] = new Point(VirtualWindow.X, VirtualWindow.Y + VirtualWindow.Height);
            WindowPoints[3] = new Point(VirtualWindow.X + VirtualWindow.Width, VirtualWindow.Y + VirtualWindow.Height);
            
            for (int i = 0; i < WindowPoints.Length; i++)
                if (!RectanglesContainPoint(WindowPoints[i], ScreenBoxes))
                {
                    Screen Main = Values.TheWindowsMainScreen(VirtualWindow);
                    if (Main == null) Main = Screen.PrimaryScreen;
                    Point Diff = new Point();

                    if (TaskbarHidden)
                        Diff = PointRectDiff(WindowPoints[i], new Rectangle(Main.Bounds.X, Main.Bounds.Y, Main.Bounds.Width, Main.Bounds.Height - 2));
                    else
                        Diff = PointRectDiff(WindowPoints[i], new Rectangle(Main.Bounds.X, Main.Bounds.Y, Main.Bounds.Width, Main.Bounds.Height - 40));
                    
                    if (Diff != new Point(0, 0))
                    {
                        VirtualWindow = new Rectangle(VirtualWindow.X + Diff.X, VirtualWindow.Y + Diff.Y, VirtualWindow.Width, VirtualWindow.Height);

                        WindowPoints[0] = new Point(VirtualWindow.X, VirtualWindow.Y);
                        WindowPoints[1] = new Point(VirtualWindow.X + VirtualWindow.Width, VirtualWindow.Y);
                        WindowPoints[2] = new Point(VirtualWindow.X, VirtualWindow.Y + VirtualWindow.Height);
                        WindowPoints[3] = new Point(VirtualWindow.X + VirtualWindow.Width, VirtualWindow.Y + VirtualWindow.Height);
                    }
                }

            config.Default.WindowPos = new System.Drawing.Point(VirtualWindow.X, VirtualWindow.Y);
        }
        static bool RectanglesContainPoint(Point P, Rectangle[] R)
        {
            for (int i = 0; i < R.Length; i++)
                if (R[i].Contains(P))
                    return true;
            return false;
        }
        static Point PointRectDiff(Point P, Rectangle R)
        {
            if (P.X > R.X && P.X < R.X + R.Width &&
                P.Y > R.Y && P.Y < R.Y + R.Height)
                return new Point(0, 0);
            else
            {
                Point r = new Point(0, 0);
                if (P.X < R.X)
                    r.X = R.X - P.X;
                if (P.X > R.X + R.Width)
                    r.X = R.X + R.Width - P.X;
                if (P.Y < R.Y)
                    r.Y = R.Y - P.Y;
                if (P.Y > R.Y + R.Height)
                    r.Y = R.Y + R.Height - P.Y;
                return r;
            }
        }
        public static void ReHookGlobalKeyHooks()
        {
            InterceptKeys.UnhookWindowsHookEx(InterceptKeys._hookID);
            InterceptKeys._hookID = InterceptKeys.SetHook(InterceptKeys._proc);
        }
        void ShowSecondRowMessage(string Message, float StartingAlpha)
        {
            SecondRowMessageAlpha = StartingAlpha;
            SecondRowMessageText = Message;
        }

        protected override void Draw(GameTime gameTime)
        {
            CurrentDebugTime2 = Stopwatch.GetTimestamp();
            // RenderTargets
            #region RT
            if (TitleTarget == null || TitleTarget.IsContentLost || TitleTarget.IsDisposed)
            {
                string Title;
                if (Assets.currentlyPlayingSongName.Contains(".mp3"))
                {
                    Title = Assets.currentlyPlayingSongName.TrimEnd(new char[] { '3' });
                    Title = Title.TrimEnd(new char[] { 'p' });
                    Title = Title.TrimEnd(new char[] { 'm' });
                    Title = Title.TrimEnd(new char[] { '.' });
                }
                else
                    Title = Assets.currentlyPlayingSongName;

                TitleTarget = new RenderTarget2D(GraphicsDevice, Values.WindowSize.X - 166, (int)Assets.Title.MeasureString("III()()()III").Y);
                GraphicsDevice.SetRenderTarget(TitleTarget);
                GraphicsDevice.Clear(Color.Transparent);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicWrap, DepthStencilState.Default, RasterizerState.CullNone);

                char[] arr = Title.ToCharArray();

                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] >= 32 && arr[i] <= 216
                            || arr[i] >= 8192 && arr[i] <= 10239
                            || arr[i] >= 12288 && arr[i] <= 12352
                            || arr[i] >= 65280 && arr[i] <= 65519)
                        arr[i] = arr[i];
                    else
                        arr[i] = (char)10060;
                }

                Title = new string(arr);

                try { spriteBatch.DrawString(Assets.Title, Title, new Vector2(5), Color.Black * 0.6f); } catch { }
                try { spriteBatch.DrawString(Assets.Title, Title, Vector2.Zero, Color.White); } catch { }

                spriteBatch.End();
            }

            if (VisSetting == Visualizations.fft && GauD != null && Assets.Channel32 != null && Assets.FFToutput != null)
                GauD.DrawToRenderTarget(spriteBatch, GraphicsDevice);
            #endregion
            #region Blur
            BeginBlur();
            if (BgModes == BackGroundModes.Blur)
            {
                spriteBatch.Begin();
                // Blurred Background
                foreach (Screen S in Screen.AllScreens)
                    spriteBatch.Draw(Assets.bg, new Rectangle(S.Bounds.X - gameWindowForm.Location.X + 50, S.Bounds.Y - gameWindowForm.Location.Y + 50,
                        S.Bounds.Width, S.Bounds.Height), Color.White);
                spriteBatch.End();
            }
            if (BgModes == BackGroundModes.BlurTeint)
            {
                spriteBatch.Begin();
                // Blurred Background
                foreach (Screen S in Screen.AllScreens)
                    spriteBatch.Draw(Assets.bg, new Rectangle(S.Bounds.X - gameWindowForm.Location.X + 50, S.Bounds.Y - gameWindowForm.Location.Y + 50,
                        S.Bounds.Width, S.Bounds.Height), Color.White);
                spriteBatch.Draw(Assets.White, new Rectangle(0, 0, Values.WindowRect.Width + 100, Values.WindowRect.Height + 100), 
                    Color.FromNonPremultiplied(primaryColor.R / 2, primaryColor.G / 2, primaryColor.B / 2, 255 / 2));
                spriteBatch.End();
            }
            EndBlur();
            #endregion

            base.Draw(gameTime);
            
            // Background
            #region Background
            // Background
            spriteBatch.Begin();
            if (BgModes == BackGroundModes.None)
                foreach (Screen S in Screen.AllScreens)
                    spriteBatch.Draw(Assets.bg, new Rectangle(S.Bounds.X - gameWindowForm.Location.X, S.Bounds.Y - gameWindowForm.Location.Y,
                        S.Bounds.Width, S.Bounds.Height), Color.White);
            else if (BgModes == BackGroundModes.bg1)
                spriteBatch.Draw(Assets.bg1, Vector2.Zero, Color.White);
            else if (BgModes == BackGroundModes.bg2)
                spriteBatch.Draw(Assets.bg2, Vector2.Zero, Color.White);
            spriteBatch.End();

            DrawBlurredTex();

            spriteBatch.Begin();
            spriteBatch.Draw(Assets.White, new Rectangle(0,                       0,                       Values.WindowSize.X, 1                  ), Color.Gray * 0.25f);
            spriteBatch.Draw(Assets.White, new Rectangle(0,                       0,                       1,                   Values.WindowSize.Y), Color.Gray * 0.25f);
            spriteBatch.Draw(Assets.White, new Rectangle(Values.WindowSize.X - 1, 0,                       1,                   Values.WindowSize.Y), Color.Gray * 0.25f);
            spriteBatch.Draw(Assets.White, new Rectangle(0,                       Values.WindowSize.Y - 1, Values.WindowSize.X, 1                  ), Color.Gray * 0.25f);
            spriteBatch.End();
            #endregion

            #region Second Row HUD Shadows
            spriteBatch.Begin();
            if (UpvoteSavedAlpha > 0)
            {
                spriteBatch.Draw(Assets.Upvote, UpvoteShadow, Color.Black * 0.6f * UpvoteSavedAlpha);
                spriteBatch.DrawString(Assets.Font, "Upvote saved!", new Vector2(Upvote.X + Upvote.Width + 8, Upvote.Y + Upvote.Height / 2 - 3), Color.Black * 0.6f * UpvoteSavedAlpha);
            }
            else if (SecondRowMessageAlpha > 0)
                if (SecondRowMessageAlpha > 1)
                    spriteBatch.DrawString(Assets.Font, SecondRowMessageText, new Vector2(29, 50), Color.Black * 0.6f);
                else
                    spriteBatch.DrawString(Assets.Font, SecondRowMessageText, new Vector2(29, 50), Color.Black * 0.6f * SecondRowMessageAlpha);
            spriteBatch.End();
            #endregion

            // Visualizations
            #region Line graph
            // Line Graph
            if (VisSetting == Visualizations.line && Assets.Channel32 != null)
            {
                spriteBatch.Begin();

                float Height = Values.WindowSize.Y / 1.96f;
                int StepLength = Assets.WaveBuffer.Length / 512;

                // Shadow
                for (int i = 1; i < 512; i++)
                {
                    Assets.DrawLine(new Vector2((i - 1) * Values.WindowSize.X / (Assets.WaveBuffer.Length / StepLength) + 5,
                                    Height + (int)(Assets.WaveBuffer[(i - 1) * StepLength] * 100) + 5),

                                    new Vector2(i * Values.WindowSize.X / (Assets.WaveBuffer.Length / StepLength) + 5,
                                    Height + (int)(Assets.WaveBuffer[i * StepLength] * 100) + 5),

                                    2, Color.Black * 0.6f, spriteBatch);
                }

                for (int i = 1; i < 512; i++)
                {
                    Assets.DrawLine(new Vector2((i - 1) * Values.WindowSize.X / (Assets.WaveBuffer.Length / StepLength),
                                    Height + (int)(Assets.WaveBuffer[(i - 1) * StepLength] * 100)),

                                    new Vector2(i * Values.WindowSize.X / (Assets.WaveBuffer.Length / StepLength),
                                    Height + (int)(Assets.WaveBuffer[i * StepLength] * 100)),

                                    2, Color.Lerp(primaryColor, secondaryColor, i / 512), spriteBatch);
                }
                spriteBatch.End();
            }
            #endregion
            #region Dynamic Line graph
            // Line Graph
            if (VisSetting == Visualizations.dynamicline && Assets.Channel32 != null)
            {
                spriteBatch.Begin();

                float Height = Values.WindowSize.Y / 1.96f;
                int StepLength = Assets.WaveBuffer.Length / 512;
                float MostUsedFrequency = Array.IndexOf(Assets.RawFFToutput, Assets.RawFFToutput.Max());
                float MostUsedWaveLength = 10000;
                if (MostUsedFrequency != 0)
                    MostUsedWaveLength = 1 / MostUsedFrequency;
                float[] MostUsedFrequencyMultiplications = new float[100];
                for (int i = 1; i <= 100; i++)
                    MostUsedFrequencyMultiplications[i - 1] = MostUsedFrequency * i;
                Debug.WriteLine((MostUsedFrequency / Assets.Channel32.WaveFormat.SampleRate * Assets.RawFFToutput.Length) + " ::: " + MostUsedFrequency);

                // Shadow
                for (int i = 1; i < 512; i++)
                {
                    Assets.DrawLine(new Vector2((i - 1) * Values.WindowSize.X / (512) + 5,
                                    Height + (int)(Assets.WaveBuffer[(i - 1) * StepLength] * 100) + 5),

                                    new Vector2(i * Values.WindowSize.X / (512) + 5,
                                    Height + (int)(Assets.WaveBuffer[i * StepLength] * 100) + 5),

                                    2, Color.Black * 0.6f, spriteBatch);
                }

                for (int i = 1; i < 512; i++)
                {
                    Assets.DrawLine(new Vector2((i - 1) * Values.WindowSize.X / (512),
                                    Height + (int)(Assets.WaveBuffer[(i - 1) * StepLength] * 100)),

                                    new Vector2(i * Values.WindowSize.X / (512),
                                    Height + (int)(Assets.WaveBuffer[i * StepLength] * 100)),

                                    2, Color.Lerp(primaryColor, secondaryColor, i / 512), spriteBatch);
                }
                spriteBatch.End();
            }
            #endregion
            #region FFT Graph
            // FFT Graph
            if (VisSetting == Visualizations.fft && Assets.Channel32 != null && Assets.FFToutput != null)
                GauD.DrawRenderTarget(spriteBatch);
            #endregion
            #region Raw FFT Graph
            if (VisSetting == Visualizations.rawfft && Assets.Channel32 != null && Assets.FFToutput != null)
            {
                spriteBatch.Begin();
                GauD.DrawInputData(spriteBatch);
                spriteBatch.End();
            }
            #endregion
            #region FFT Bars
            // FFT Bars
            if (VisSetting == Visualizations.barchart)
            {
                spriteBatch.Begin();
                GauD.DrawAsBars(spriteBatch);
                spriteBatch.End();
            }
            #endregion
            #region Grid
            // Grid
            if (VisSetting == Visualizations.grid)
            {
                spriteBatch.Begin();
                DG.DrawShadow(spriteBatch);
                DG.Draw(spriteBatch);
                spriteBatch.End();
            }
            #endregion

            // HUD
            #region HUD
            spriteBatch.Begin();
            // Duration Bar
            spriteBatch.Draw(Assets.White, DurationBarShadow, Color.Black * 0.6f);
            spriteBatch.Draw(Assets.White, DurationBar, Color.White);
            if (Assets.Channel32 != null)
            {
                lock (Assets.Channel32)
                {
                    float PlayPercetage = (Assets.Channel32.Position / (float)Assets.Channel32.WaveFormat.AverageBytesPerSecond /
                        ((float)Assets.Channel32.TotalTime.TotalSeconds));
                    spriteBatch.Draw(Assets.White, new Rectangle(DurationBar.X, DurationBar.Y, (int)(DurationBar.Width * PlayPercetage), 3), primaryColor);
                    if (Assets.EntireSongWaveBuffer != null && config.Default.Preload)
                    {
                        double LoadPercetage = (double)Assets.EntireSongWaveBuffer.Count / Assets.Channel32.Length * 4.0;
                        if (LoadPercetage < 1)
                            spriteBatch.Draw(Assets.White, new Rectangle(DurationBar.X + (int)(DurationBar.Width * LoadPercetage), DurationBar.Y, DurationBar.Width - (int)(DurationBar.Width * LoadPercetage), 3),
                                secondaryColor);
                    }
                    if (config.Default.AntiAliasing)
                    {
                        float AAPercentage = (PlayPercetage * DurationBar.Width) % 1;
                        spriteBatch.Draw(Assets.White, new Rectangle(DurationBar.X + (int)(DurationBar.Width * PlayPercetage), DurationBar.Y, 
                            1, 3), primaryColor * AAPercentage);
                    }
                }
            }

            // Second Row
            spriteBatch.End();
            
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default,
                RasterizerState.CullNone, Assets.TitleFadeout);
            if (TitleTarget != null)
                spriteBatch.Draw(TitleTarget, new Vector2(24, 13), Color.White);
            spriteBatch.End();

            spriteBatch.Begin();
            if (UpvoteSavedAlpha > 0)
            {
                spriteBatch.Draw(Assets.Upvote, Upvote, Color.White * UpvoteSavedAlpha);
                spriteBatch.DrawString(Assets.Font, "Upvote saved!", new Vector2(Upvote.X + Upvote.Width + 3, Upvote.Y + Upvote.Height / 2 - 8), Color.White * UpvoteSavedAlpha);
            }
            else if (SecondRowMessageAlpha > 0)
                if (SecondRowMessageAlpha > 1)
                    spriteBatch.DrawString(Assets.Font, SecondRowMessageText, new Vector2(24, 45), Color.White);
                else
                    spriteBatch.DrawString(Assets.Font, SecondRowMessageText, new Vector2(24, 45), Color.White * SecondRowMessageAlpha);

            // PlayPause Button
            if (Assets.IsPlaying())
            {
                spriteBatch.Draw(Assets.Pause, PlayPauseButtonShadow, Color.Black * 0.6f);
                spriteBatch.Draw(Assets.Pause, PlayPauseButton, Color.White);
            }
            else
            {
                spriteBatch.Draw(Assets.Play, PlayPauseButtonShadow, Color.Black * 0.6f);
                spriteBatch.Draw(Assets.Play, PlayPauseButton, Color.White);
            }

            // Volume
            if (Values.TargetVolume > 0.45f)
            {
                spriteBatch.Draw(Assets.Volume, VolumeIconShadow, Color.Black * 0.6f);
                spriteBatch.Draw(Assets.Volume, VolumeIcon, Color.White);
            }
            else if (Values.TargetVolume > 0.15f)
            {
                spriteBatch.Draw(Assets.Volume2, VolumeIconShadow, Color.Black * 0.6f);
                spriteBatch.Draw(Assets.Volume2, VolumeIcon, Color.White);
            }
            else if (Values.TargetVolume > 0f)
            {
                spriteBatch.Draw(Assets.Volume3, VolumeIconShadow, Color.Black * 0.6f);
                spriteBatch.Draw(Assets.Volume3, VolumeIcon, Color.White);
            }
            else
            {
                spriteBatch.Draw(Assets.Volume4, VolumeIconShadow, Color.Black * 0.6f);
                spriteBatch.Draw(Assets.Volume4, VolumeIcon, Color.White);
            }

            spriteBatch.Draw(Assets.White, VolumeBarShadow, Color.Black * 0.6f);
            spriteBatch.Draw(Assets.White, VolumeBar, Color.White);
            spriteBatch.Draw(Assets.White, TargetVolumeBar, secondaryColor);
            spriteBatch.Draw(Assets.White, ActualVolumeBar, primaryColor);

            // UpvoteButton
            spriteBatch.Draw(Assets.Upvote, UpvoteButtonShadow, Color.Black * 0.6f);
            spriteBatch.Draw(Assets.Upvote, UpvoteButton, Color.Lerp(Color.White, primaryColor, UpvoteIconAlpha));

            // CloseButton
            spriteBatch.Draw(Assets.Close, CloseButtonShadow, Color.Black * 0.6f);
            spriteBatch.Draw(Assets.Close, CloseButton, Color.White);

            // Catalyst Grid
            //for (int x = 1; x < Values.WindowSize.X; x += 2)
            //    for (int y = 1; y < Values.WindowSize.Y; y += 2)
            //        spriteBatch.Draw(Assets.White, new Rectangle(x, y, 1, 1), Color.LightGray * 0.2f);

            //FPSCounter.Draw(spriteBatch);

            spriteBatch.End();
            #endregion
            //Debug.WriteLine("Draw " + (Stopwatch.GetTimestamp() - CurrentDebugTime2));
        }
        public static void ForceTitleRedraw()
        {
            TitleTarget = null;
        }
        void BeginBlur()
        {
            GraphicsDevice.SetRenderTarget(TempBlur);
            GraphicsDevice.Clear(Color.Transparent);
        }
        void EndBlur()
        {
            GraphicsDevice.SetRenderTarget(BlurredTex);
            GraphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, Assets.gaussianBlurVert);
            spriteBatch.Draw(TempBlur, Vector2.Zero, Color.White);
            spriteBatch.End();
            GraphicsDevice.SetRenderTarget(null);
        }
        void DrawBlurredTex()
        {
            spriteBatch.Begin(0, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, Assets.gaussianBlurHorz);
            spriteBatch.Draw(BlurredTex, new Vector2(-50), Color.White);
            spriteBatch.End();
        }
    }
}
