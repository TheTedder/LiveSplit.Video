using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Drawing;
using System.Xml;
using System.Diagnostics;
using System.Windows.Controls;

namespace LiveSplit.Video
{
    public class VideoComponent : ControlComponent
    {
        public VideoSettings Settings { get; set; }
        public LiveSplitState State { get; set; }
        public System.Timers.Timer SynchronizeTimer { get; set; }

        private class VLCErrorException : Exception { }

        public ElementHost elementHost => (ElementHost)Control;

        public MediaElement mediaElement => (MediaElement)elementHost.Child;

        protected string OldMRL { get; set; }

        public override string ComponentName => "Video";

        public override float HorizontalWidth => Settings.Width;

        public override float MinimumHeight => 10;

        public override float VerticalHeight => Settings.Height;

        public override float MinimumWidth => 10;

        public VideoComponent(LiveSplitState state) : this(state, CreateControl())
        {
        }

        public VideoComponent(LiveSplitState state, ElementHost host) : base(state,host, ex => ErrorCallback(state.Form,ex))
        {
            Settings = new VideoSettings();
            State = state;

            state.OnReset += state_OnReset;
            state.OnStart += state_OnStart;
            state.OnPause += state_OnPause;
            state.OnResume += state_OnResume;
        }

        public new void InvokeIfNeeded(Action x)
        {
            if (Control != null && Control.InvokeRequired)
                Control.Invoke(x);
            else
                x();
        }

        static void ErrorCallback(Form form, Exception ex)
        {
            //string requiredBits = Environment.Is64BitProcess ? "64" : "32";
            //MessageBox.Show(form, "VLC Media Player 2.2.1 (" + requiredBits + "-bit) along with the ActiveX Plugin need to be installed for the Video Component to work.", "Video Component Could Not Be Loaded", MessageBoxButtons.OK, MessageBoxIcon.Error);
            MessageBox.Show(form, ex.Message, "Error Loading Video Component", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void state_OnResume(object sender, EventArgs e)
        {
            InvokeIfNeeded(() =>
            {
                lock (Control)
                {
                    lock (mediaElement)
                        mediaElement.Play();
                }
            });
            
        }

        void state_OnPause(object sender, EventArgs e)
        {
            InvokeIfNeeded(() =>
            {
                lock (Control)
                {
                    lock (mediaElement)
                        mediaElement.Pause();
                }
            });

        }

        void state_OnStart(object sender, EventArgs e)
        {
            InvokeIfNeeded(() =>
            {
                lock (Control)
                {
                    Control.Visible = true;

                    lock (mediaElement)
                        mediaElement.Play();
                }
            });
            Synchronize();
        }

        public void Synchronize()
        {
            Synchronize(TimeSpan.Zero);
        }

        private TimeSpan GetCurrentTime()
        {
            return State.CurrentTime[TimingMethod.RealTime].Value;
        }

        public void Synchronize(TimeSpan offset)
        {
            if (SynchronizeTimer != null && SynchronizeTimer.Enabled)
                SynchronizeTimer.Enabled = false;
            InvokeIfNeeded(() =>
            {
                lock (mediaElement)
                {
                    //((VideoView)Control).MediaPlayer.Time = (GetCurrentTime() + offset + Settings.Offset).Milliseconds;
                    mediaElement.Position = GetCurrentTime() + offset + Settings.Offset;
                }
            });

            SynchronizeTimer = new System.Timers.Timer(1000);

            SynchronizeTimer.Elapsed += (s, ev) =>
            {
                //if (((VideoView)Control).MediaPlayer.State == VLCState.Playing)
                if (State.CurrentPhase == TimerPhase.Running)//kind shitty that this is how we have to do this
                {
                    InvokeIfNeeded(() =>
                    {
                        lock (Control)
                        {
                            var currentTime = GetCurrentTime();
                            //var delta = ((VideoView)Control).MediaPlayer.Time - (currentTime + offset + Settings.Offset).Milliseconds;
                            var delta = (mediaElement.Position - (currentTime + offset + Settings.Offset)).Milliseconds;
                            if (Math.Abs(delta) > 500)
                                mediaElement.Position = (currentTime + offset + Settings.Offset) + new TimeSpan(0, 0, 0, 0, Math.Max(0, -delta));
                            else
                                SynchronizeTimer.Enabled = false;
                        }
                    });

                }
                //else if (((VideoView)Control).MediaPlayer.State == VLCState.Stopped)
                else if (State.CurrentPhase == TimerPhase.NotRunning)
                {
                    SynchronizeTimer.Enabled = false;
                }
            };

            SynchronizeTimer.Enabled = true;
        }

        void state_OnReset(object sender, TimerPhase e)
        {
            mediaElement.Stop();
            InvokeIfNeeded(() =>
            {
                lock (Control)
                {
                    Control.Visible = false;
                }
            });
        }

        private static ElementHost CreateControl()
        {
            var me = new MediaElement();
            me.BeginInit();
            me.LoadedBehavior = MediaState.Manual;
            me.Name = "player";
            me.EndInit();
            return new ElementHost()
            {
                Child = me,
                AllowDrop = false,
                Location = new Point(),
                Name = "playerhost"
            };
        }

        public override System.Windows.Forms.Control GetSettingsControl(UI.LayoutMode mode)
        {
            Settings.Mode = mode;
            return Settings;
        }

        public override XmlNode GetSettings(XmlDocument document)
        {
            return Settings.GetSettings(document);
        }

        public override void SetSettings(XmlNode settings)
        {
            Settings.SetSettings(settings);
        }

        private void DisposeIfError()
        {
            if (ErrorWithControl)
            {
                base.Dispose();

                throw new VLCErrorException();
            }
        }

        public override void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
            base.DrawVertical(g, state, width, clipRegion);
            DisposeIfError();
        }

        public override void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
            base.DrawHorizontal(g, state, height, clipRegion);
            DisposeIfError();
        }

        public override void Update(UI.IInvalidator invalidator, LiveSplitState state, float width, float height, UI.LayoutMode mode)
        {
            State = state;
            if (!Control.IsDisposed && !state.Form.IsDisposed)
            {
                base.Update(invalidator, state, width, height, mode);

                if (Control.Created)
                {
                    InvokeIfNeeded(() =>
                    {
                        lock (Control)
                        {
                            //if (((VideoView)Control).MediaPlayer != null && OldMRL != Settings.MRL && !string.IsNullOrEmpty(Settings.MRL))
                            if (mediaElement != null && OldMRL != Settings.MRL && !string.IsNullOrEmpty(Settings.MRL))
                            {
                                //VLC.playlist.add(Settings.MRL);
                                //((VideoView)Control).MediaPlayer.Media = new Media(libVLC, Settings.MRL);
                                var uri = new Uri(Settings.MRL);
                                mediaElement.Source = uri;
                                Debug.WriteLine("Video Component media changed to " + uri.AbsolutePath);
                            }
                            OldMRL = Settings.MRL;

                            //if (((VideoView)Control).MediaPlayer != null)
                            if (mediaElement != null)
                            {
                                //VLC.Mute = true;
                                //((VideoView)Control).MediaPlayer.Mute = true;
                                //((VideoView)Control).MediaPlayer.Volume = 5;
                                mediaElement.IsMuted = true;
                                mediaElement.Volume = 0.05;
                            }
                        }
                    });
                    
                }
            }
        }

        public override void Dispose()
        {
            State.Form.Invoke((Action)delegate
            {
                lock (Control)
                {
                    mediaElement.Close();
                    elementHost.Child = null;
                }
                base.Dispose();
            });

            State.OnReset -= state_OnReset;
            State.OnStart -= state_OnStart;
            State.OnPause -= state_OnPause;
            State.OnResume -= state_OnResume;
            if (SynchronizeTimer != null)
                SynchronizeTimer.Dispose();
        }

        public int GetSettingsHashCode() => Settings.GetSettingsHashCode();
    }
}
