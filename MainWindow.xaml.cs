using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using System.IO;

using Microsoft.Kinect;
using AForge.Imaging;

/**
   * KinectMinimum
   * http://github.com/mckinney/kinectMinimum
   *
   * Copyright 2013, McKinney
   * Licensed under the MIT license.
   * http://github.com/mckinney/kinectMinimum/blob/master/LICENSE
   *
   * Author: Colin Dwan
   */

namespace KinectMinimum
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("User32.Dll")]
        public static extern long SetCursorPos(int x, int y);

        KinectManager kinectMgr;
        private float nearClip;
        private float farClip;

        public MainWindow()
        {
            InitializeComponent();
        }

        public void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Setup all resource managers
            kinectMgr = new KinectManager();
            //kinectMgr.depthMaskInit = true;
            kinectMgr.Initialize();
            // Make sure our Kinect initialized properly
            if (kinectMgr.init)
            {
                this.Canvas.Source = kinectMgr.colorBitmap;

                // Piggy back on the endFrame event that is fired when frames finish. 
                // This allows us to know when we can process the latest depth image
                kinectMgr.endFrame += new KinectManager.FrameDone(frameDone);
                kinectMgr.endMask += new KinectManager.MaskDone(updateDepth);

                nearClip = kinectMgr.nearClip;
                farClip = kinectMgr.farClip;
                this.Status.Text = "Initializing...";
            }
            else
            {
                // ERROR
                Trace.WriteLine("ERROR - couldn't load kinect!");
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            kinectMgr.Stop();
        }

        private int frameCount = 0;

        public void frameDone(object sender, EventArgs e)
        {
            if (kinectMgr.depthMaskInit == true)
            {
                // Do processing here
                frameCount++;
                if (frameCount < 3)
                    return;
                frameCount = 0;

            }
        }

        public void updateDepth(object sender, EventArgs e)
        {
            // Do a second pass filtering of the depth map if you want
            // get an average depth mask reading at a few key locations
            // if averageDepthMask - kinectMgr.farClip > 100 
            //      rescan
            this.Status.Text = "Ready!";
        }

        public static BitmapSource loadBitmap(System.Drawing.Bitmap source)
        {
            IntPtr ip = source.GetHbitmap();
            BitmapSource bs = null;
            try
            {
                bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(ip,
                   IntPtr.Zero, Int32Rect.Empty,
                   System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(ip);
            }

            return bs;
        }
    }
}
