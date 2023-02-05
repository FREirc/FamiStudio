﻿using System;
using System.Diagnostics;

namespace FamiStudio
{
    public class FamiStudioContainer : Container
    {
        private const bool ShowRenderingTimes = false;

        private Control transitionControl;
        private Control activeControl;
        private float   transitionTimer;
        private bool    mobilePianoVisible = false;

        private Toolbar         toolbar;
        private Sequencer       sequencer;
        private PianoRoll       pianoRoll;
        private ProjectExplorer projectExplorer;
        private QuickAccessBar  quickAccessBar;
        private MobilePiano     mobilePiano;

        public Toolbar         ToolBar         => toolbar;
        public Sequencer       Sequencer       => sequencer;
        public PianoRoll       PianoRoll       => pianoRoll;
        public ProjectExplorer ProjectExplorer => projectExplorer;
        public QuickAccessBar  QuickAccessBar  => quickAccessBar;
        public MobilePiano     MobilePiano     => mobilePiano;
        public Control         ActiveControl   => activeControl;

        public bool IsLandscape => width > height;
        
        public bool MobilePianoVisible
        {
            get { return mobilePianoVisible; }
            set
            {
                mobilePianoVisible = value;
                UpdateLayout(false);
            }
        }

        public FamiStudioContainer(FamiStudioWindow parent)
        {
            window = parent;
            toolbar = new Toolbar();
            sequencer = new Sequencer();
            pianoRoll = new PianoRoll();
            projectExplorer = new ProjectExplorer();
            quickAccessBar = new QuickAccessBar();
            mobilePiano = new MobilePiano();
            activeControl = sequencer;

            pianoRoll.Visible = false;
            projectExplorer.Visible = false;
            mobilePiano.Visible = false;

            AddControl(sequencer);
            AddControl(pianoRoll);
            AddControl(projectExplorer);
            AddControl(toolbar);
            AddControl(mobilePiano);
            AddControl(quickAccessBar); // Needs to be last, draws on top of everything.
        }

        // CTRLTODO : Show/hide controls as needed.
        public void SetActiveControl(Control ctrl, bool animate = true)
        {
            if (activeControl != ctrl)
            {
                Debug.Assert(transitionTimer == 0.0f && transitionControl == null);

                if (animate)
                {
                    transitionControl = ctrl;
                    transitionTimer   = 1.0f;
                }
                else
                {
                    activeControl   = ctrl;
                    transitionTimer = 0.0f;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            UpdateLayout(false);
        }

        private void UpdateLayout(bool activeControlOnly)
        {
            var landscape          = IsLandscape;
            var quickAccessBarSize = quickAccessBar.LayoutSize;
            var toolLayoutSize     = toolbar.LayoutSize;
            var pianoLayoutSize    = mobilePianoVisible ? mobilePiano.LayoutSize : 0;

            if (landscape)
            {
                if (!activeControlOnly)
                {
                    toolbar.Move(0, 0, toolLayoutSize, height);
                    quickAccessBar.Move(width - quickAccessBarSize, 0, quickAccessBarSize, height);
                    mobilePiano.Move(toolLayoutSize, height - pianoLayoutSize, width - toolLayoutSize - quickAccessBarSize, pianoLayoutSize);
                }

                activeControl.Move(toolLayoutSize, 0, width - toolLayoutSize - quickAccessBarSize, height - pianoLayoutSize);

                // Always update the piano roll since we draw the view range in the sequencer and 
                // it requires valid size to do that.
                if (activeControl != pianoRoll)
                    pianoRoll.Move(toolLayoutSize, 0, width - toolLayoutSize - quickAccessBarSize, height - pianoLayoutSize);
            }
            else
            {
                if (!activeControlOnly)
                {
                    toolbar.Move(0, 0, width, toolLayoutSize);
                    quickAccessBar.Move(0, height - quickAccessBarSize - pianoLayoutSize, width, quickAccessBarSize);
                    mobilePiano.Move(0, height - pianoLayoutSize, width, pianoLayoutSize);
                }

                activeControl.Move(0, toolLayoutSize, width, height - toolLayoutSize - quickAccessBarSize - pianoLayoutSize);

                // Always update the piano roll since we draw the view range in the sequencer and 
                // it requires valid size to do that.
                if (activeControl != pianoRoll)
                    pianoRoll.Move(0, toolLayoutSize, width, height - toolLayoutSize - quickAccessBarSize - pianoLayoutSize);
            }

            sequencer.Visible = activeControl == sequencer;
            pianoRoll.Visible = activeControl == pianoRoll;
            projectExplorer.Visible = activeControl == projectExplorer;
        }

        // CTRLTODO : Change!
        public bool CanAcceptInput
        {
            get
            {
                return transitionTimer == 0.0f && transitionControl == null;
            }
        }

        public override bool CanInteractWithContainer(Container c)
        {
            if (!CanAcceptInput)
            {
                return false;
            }

            return base.CanInteractWithContainer(c);
        }

        //public override Control GetControlAt(int winX, int winY, out int ctrlX, out int ctrlY)
        //{
        //    if (!CanAcceptInput)
        //    {
        //        ctrlX = 0;
        //        ctrlY = 0;
        //        return null;
        //    }

        //    return base.GetControlAt(winX, winY, out ctrlX, out ctrlY);
        //}

        private void RenderTransitionOverlay(Graphics g)
        {
            if (transitionTimer > 0.0f)
            {
                var alpha = (byte)((1.0f - Math.Abs(transitionTimer - 0.5f) * 2) * 255);
                var color = Color.FromArgb(alpha, Theme.DarkGreyColor4);

                g.OverlayCommandList.FillRectangle(activeControl.WindowRectangle, color);
            }
        }

        public override void Tick(float delta)
        {
            base.Tick(delta);

            if (transitionTimer > 0.0f)
            {
                var prevTimer = transitionTimer;
                transitionTimer = Math.Max(0.0f, transitionTimer - delta * 6);

                if (prevTimer > 0.5f && transitionTimer <= 0.5f)
                {
                    activeControl = transitionControl;
                    transitionControl = null;
                    UpdateLayout(true);
                }

                MarkDirty();
            }
        }

        protected override void OnRender(Graphics g)
        {
            RenderTransitionOverlay(g);
            base.OnRender(g);
        }
    }
}
