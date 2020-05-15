﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PxKeystrokesWPF
{
    /// <summary>
    /// Interaktionslogik für KeystrokeDisplay.xaml
    /// </summary>
    public partial class KeystrokeDisplay : Window
    {
        SettingsStore settings;
        IKeystrokeEventProvider k;
        IntPtr windowHandle;

        public KeystrokeDisplay(IKeystrokeEventProvider k, SettingsStore s)
        {
            InitializeComponent();
            InitializeAnimations();

            windowHandle = new WindowInteropHelper(this).Handle;

            this.k = k;
            this.k.KeystrokeEvent += k_KeystrokeEvent;

            this.settings = s;
            this.settings.PropertyChanged += settingChanged;

            this.settings.OnSettingChangedAll();

            //addWelcomeInfo();

            ActivateDisplayOnlyMode(true);

            if (settings.EnableWindowFade)
            {
                FadeOut();
            }

            settings.PropertyChanged += (object sender, PropertyChangedEventArgs e) =>
            {
                if (settings.EnableWindowFade/*TODO &&  tweenLabels.Count == 0 */)
                {
                    FadeOut();
                }
                else
                {
                    FadeIn();
                }
            };
        }

        #region keystroke handler

        void k_KeystrokeEvent(KeystrokeEventArgs e)
        {
            CheckForSettingsMode(e);
            if (e.ShouldBeDisplayed)
            {
                if (settings.EnableWindowFade && !SettingsModeActivated)
                {
                    FadeIn();
                }
                
                if (!e.RequiresNewLine
                    && NumberOfDeletionsAllowed > 0
                    && LastHistoryLineIsText
                    && !LastHistoryLineRequiredNewLineAfterwards
                    && e.NoModifiers
                    && e.Key == System.Windows.Input.Key.Back)
                {
                    Log.e("BS", "delete last char");
                    Log.e("BS", "NumberOfDeletionsAllowed " + NumberOfDeletionsAllowed.ToString());
                    if (!removeLastChar())
                    {
                        // again
                        Log.e("BS", " failed");
                        NumberOfDeletionsAllowed = 0;
                        k_KeystrokeEvent(e);
                        return;
                    }
                }
                else if (e.RequiresNewLine
                    || !addingWouldFitInCurrentLine(e.ToString(false))
                    || !LastHistoryLineIsText
                    || LastHistoryLineRequiredNewLineAfterwards)
                {
                    addNextLine(e.ToString(false));
                    NumberOfDeletionsAllowed = e.Deletable ? 1 : 0;
                    Log.e("BS", "NumberOfDeletionsAllowed " + NumberOfDeletionsAllowed.ToString());
                }
                else
                {
                    Log.e("BS", "add to line ");
                    addToLine(e.ToString(false));
                    if (e.Deletable)
                        NumberOfDeletionsAllowed += 1;
                    else
                        NumberOfDeletionsAllowed = 0;
                    Log.e("BS", "NumberOfDeletionsAllowed " + NumberOfDeletionsAllowed.ToString());
                }

                LastHistoryLineIsText = e.StrokeType == KeystrokeType.Text;
                LastHistoryLineRequiredNewLineAfterwards = e.RequiresNewLineAfterwards;
                
            }
        }


        #endregion

        #region Opacity Animation

        DoubleAnimation windowOpacityAnim;
        Storyboard windowOpacitySB;
        double minOpacityWhenVisible = 0.1;

        private void InitializeAnimations()
        {
            windowOpacityAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            };
            windowOpacitySB = new Storyboard();
            windowOpacitySB.Children.Add(windowOpacityAnim);
            Storyboard.SetTarget(windowOpacityAnim, this);
            Storyboard.SetTargetProperty(windowOpacityAnim, new PropertyPath(Window.OpacityProperty));
        }

        private void FadeOut()
        {
            ToOpacity(0.0, true);
        }

        private void FadeIn()
        {
            ToOpacity(Math.Max(minOpacityWhenVisible, settings.Opacity / 100.0f), true);
        }

        private void FullOpacity()
        {
            ToOpacity(1.0, false);
        }

        private void ToOpacity(double targetOpacity, bool fade)
        {
            if (Opacity != targetOpacity)
            {
                if (fade)
                {
                    windowOpacitySB.Stop();
                    windowOpacityAnim.From = this.Opacity;
                    windowOpacityAnim.To = targetOpacity;
                    windowOpacitySB.Begin(this);
                } else
                {
                    // https://docs.microsoft.com/de-de/dotnet/framework/wpf/graphics-multimedia/how-to-set-a-property-after-animating-it-with-a-storyboard
                    this.BeginAnimation(Window.OpacityProperty, null); // Break connection between storyboard and property
                    Opacity = targetOpacity;
                }
            }
        }
        #endregion


 

        #region settingsChanged, Dialog

        private void settingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "BackgroundColor":
                    //this.BackColor = settings.BackgroundColor;
                    break;
                case "Opacity":
                    ToOpacity(Math.Max(minOpacityWhenVisible, settings.Opacity / 100.0f), false);
                    break;
                case "WindowLocation":
                    //this.Location = settings.WindowLocation;
                    Log.e("KD", String.Format("Apply X: {0}", settings.WindowLocation.X));
                    break;
                case "WindowSize":
                    this.Width = settings.WindowSize.Width;
                    this.Height = settings.WindowSize.Height;
                    break;
                case "PanelLocation":
                    this.innerPanel.Margin = new Thickness(settings.PanelLocation.X, settings.PanelLocation.Y, 0.0, 0.0);
                    break;
                case "PanelSize":
                    this.innerPanel.Width = settings.PanelSize.Width;
                    this.innerPanel.Height = settings.PanelSize.Height;
                    break;
                case "LabelFont":
                case "LabelColor":
                case "LabelTextAlignment":
                case "LineDistance":
                    UpdateLabelStyles();
                    break;
                case "LabelTextDirection":
                    labelStack.VerticalAlignment =
                        (settings.LabelTextDirection == TextDirection.Down)
                        ? VerticalAlignment.Bottom
                        : VerticalAlignment.Top;
                    break;
            }
        }


        void ShowSettingsDialog()
        {
            Settings1 settings1 = new Settings1(settings);
            settings1.ShowDialog();
        }

        #endregion


        #region Settings Mode

        private void CheckForSettingsMode(KeystrokeEventArgs e)
        {
            if (e.Ctrl && e.Shift && e.Alt)
                ActivateSettingsMode();
            else
                ActivateDisplayOnlyMode(false);
        }

        bool SettingsModeActivated = false;

        void ActivateDisplayOnlyMode(bool force)
        {
            if (SettingsModeActivated || force)
            {
                FadeIn();

                NativeMethodsGWL.ClickThrough(this.windowHandle);
                NativeMethodsGWL.HideFromAltTab(this.windowHandle);

                InnerPanelIsDragging = false;

                this.buttonClose.Visibility = Visibility.Hidden;
                this.buttonResizeInnerPanel.Visibility = Visibility.Hidden;
                this.buttonResizeWindow.Visibility = Visibility.Hidden;
                this.buttonSettings.Visibility = Visibility.Hidden;
                //this.panel_textposhelper.Visibility = Visibility.Hidden; TODO

                SettingsModeActivated = false;

                settings.SaveAll();
            }
        }

        void ActivateSettingsMode()
        {
            if (!SettingsModeActivated)
            {
                NativeMethodsGWL.CatchClicks(this.windowHandle);

                FullOpacity();

                InnerPanelIsDragging = false;

                this.buttonClose.Visibility = Visibility.Visible;
                this.buttonResizeInnerPanel.Visibility = Visibility.Visible;
                this.buttonResizeWindow.Visibility = Visibility.Visible;
                this.buttonSettings.Visibility = Visibility.Visible;
                //this.panel_textposhelper.Visibility = Visibility.Visible; TODO

                /* TODO foreach (TweenLabel T in tweenLabels)
                {
                    T.Visibility = Visibility.Hidden;
                }*/

                SettingsModeActivated = true;
            }
        }



        #endregion

        #region Dragging of Window and innerPanel

        private void backgroundGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
            e.Handled = true;

        }

        private bool InnerPanelIsDragging = false;
        private Point InnerPanelDragStartCursorPosition;
        private Point InnerPanelDragStartPosition;

        private void innerPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            InnerPanelDragStartCursorPosition = e.GetPosition(this);
            InnerPanelIsDragging = true;
            InnerPanelDragStartPosition = new Point(innerPanel.Margin.Left, innerPanel.Margin.Top);
            e.Handled = true;
        }

        private void innerPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (InnerPanelIsDragging)
            {
                Point current = e.GetPosition(this);
                Point diff = new Point(
                    current.X - InnerPanelDragStartCursorPosition.X,
                    current.Y - InnerPanelDragStartCursorPosition.Y);
                innerPanel.Margin = new Thickness(InnerPanelDragStartPosition.X + diff.X,
                    InnerPanelDragStartPosition.Y + diff.Y,
                    0,
                    0);
            }
            e.Handled = true;
        }

        private void innerPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            InnerPanelIsDragging = false;
        }


        #endregion

        #region Button Click  and Window Close Events

        private void buttonSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsDialog();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {

        }
        #endregion

        #region display and animate Label

        struct LabelData
        {
            public Label label;
            public Storyboard storyboard;
            public DispatcherTimer historyTimeout;
        }

        List<LabelData> labels = new List<LabelData>(5);
        bool LastHistoryLineIsText = false;
        bool LastHistoryLineRequiredNewLineAfterwards = false;
        int NumberOfDeletionsAllowed = 0;

        void addToLine(string chars)
        {
            LabelData T = labels[labels.Count - 1];
            T.label.Content = HttpUtility.UrlDecode(T.label.Content + chars, Encoding.UTF8);
            //T.Refresh();
        }

        private bool removeLastChar()
        {
            if (labels.Count == 0)
            {
                return false;
            }

            LabelData T = labels[labels.Count - 1];
            var content = ((string)T.label.Content);
            if (content.Length == 0)
                return false;
            T.label.Content = HttpUtility.UrlDecode(content.Substring(0, content.Length - 1));
            NumberOfDeletionsAllowed -= 1;
            //T.Refresh();
            return true;
        }

        void addNextLine(string chars)
        {
            Label next = new Label();
            next.Content = chars;
            ApplyLabelStyle(next);



            Storyboard showLabelSB = new Storyboard();

            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            };
            showLabelSB.Children.Add(fadeInAnimation);
            Storyboard.SetTarget(fadeInAnimation, next);
            Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(Label.OpacityProperty));

            if (settings.LabelTextDirection == TextDirection.Down)
            {
                next.Margin = new Thickness(0, 0, 0, -next.Height);
            }
            else
            {
                next.Margin = new Thickness(0, -next.Height, 0, 0);
            }

            var pushUpwardsAnimation = new ThicknessAnimation
            {
                From = next.Margin,
                To = new Thickness(0, 0, 0, -next.Height + settings.LineDistance),
                Duration = new Duration(TimeSpan.FromMilliseconds(200))
            };
            showLabelSB.Children.Add(pushUpwardsAnimation);
            Storyboard.SetTarget(pushUpwardsAnimation, next);
            Storyboard.SetTargetProperty(pushUpwardsAnimation, new PropertyPath(Label.MarginProperty));

            if (settings.LabelTextDirection == TextDirection.Down)
            {
                labelStack.Children.Add(next);
            } else
            {
                labelStack.Children.Insert(0, next);
            }

            var pack = new LabelData
            {
                label = next,
                storyboard = showLabelSB,
                historyTimeout = null,
            };
            if (settings.EnableHistoryTimeout)
            {
                pack.historyTimeout = FireOnce(settings.HistoryTimeout, () =>
                {
                    fadeOutLabel(pack);
                });
            }
            labels.Add(pack);
            pack.storyboard.Begin(pack.label);

            while (labels.Count > settings.HistoryLength)
            {
                Log.e("TWE", $"Delete: Have: {labels.Count}");
                var toRemove = labels[0];
                fadeOutLabel(toRemove);
                labels.RemoveAt(0);
            }

        }

        void fadeOutLabel(LabelData toRemove)
        {
            if (toRemove.historyTimeout != null)
            {
                toRemove.historyTimeout.Stop();
            }
            toRemove.storyboard.Remove(toRemove.label);

            Storyboard hideLabelSB = new Storyboard();
            var fadeOutAnimation = new DoubleAnimation
            {
                From = toRemove.label.Opacity,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            };
            hideLabelSB.Children.Add(fadeOutAnimation);
            Storyboard.SetTarget(fadeOutAnimation, toRemove.label);
            Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(Label.OpacityProperty));
            fadeOutAnimation.Completed += (object sender, EventArgs e) => {
                hideLabelSB.Remove(toRemove.label);
                labelStack.Children.Remove(toRemove.label);

                if (settings.EnableWindowFade && labels.Count == 0)
                {
                    FadeOut();
                }
            };
            hideLabelSB.Begin(toRemove.label);
        }


        /// <summary>
        /// Fires an action once after "seconds"
        /// </summary>
        public static DispatcherTimer FireOnce(int milliSeconds, Action onElapsed)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliSeconds) };

            var handler = new EventHandler((s, args) =>
            {
                if (timer.IsEnabled)
                {
                    onElapsed();
                }
                timer.Stop();
            });

            timer.Tick += handler;
            timer.Start();
            return timer;
        }

        bool addingWouldFitInCurrentLine(string s)
        {
            if (labels.Count == 0)
                return false;

            //return labels[labels.Count - 1].AddingWouldFit(s); TODO
            return true;
        }

        void ApplyLabelStyle(Label label)
        {
            label.Height = 120;
            label.BeginAnimation(Label.MarginProperty, null);
            label.Margin = new Thickness(0, 0, 0, -label.Height + settings.LineDistance);
            label.BeginAnimation(Label.OpacityProperty, null);
            label.Opacity = 1.0;
            label.Foreground = new SolidColorBrush(UIHelper.ToMediaColor(settings.LabelColor));
            label.FontSize = settings.LabelFont.Size;
            label.FontFamily = settings.LabelFont.Family;
            label.FontStretch = settings.LabelFont.Stretch;
            label.FontStyle = settings.LabelFont.Style;
            label.FontWeight = settings.LabelFont.Weight;

            if (settings.LabelTextAlignment == TextAlignment.Left)
            {
                label.HorizontalContentAlignment = HorizontalAlignment.Left;
            }
            else if (settings.LabelTextAlignment == TextAlignment.Center)
            {
                label.HorizontalContentAlignment = HorizontalAlignment.Center;
            }
            else
            {
                label.HorizontalContentAlignment = HorizontalAlignment.Right;
            }
        }

        void UpdateLabelStyles()
        {
            foreach (var pack in labels)
            {
                ApplyLabelStyle(pack.label);
            }
        }


        #endregion


    }
}