using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;

using Microsoft.Kinect;

/*!
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
    class KinectManager
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        public KinectSensor sensor;

        /// <summary>
        /// Bitmap that will hold our depth image as it's translated to color. Will be forced to black each time if this.showDepth == false.
        /// </summary>
        public WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera.
        /// </summary>
        public short[] depthPixels;

        /// <summary>
        /// Mask to hide background objects that might interfere. Use this distance (minus a buffer) to ignore static objects.
        /// </summary>
        public int[] depthMask;

        /// <summary>
        /// Marks when our depth mask has finished initializing, letting any other classes know that data coming back is now safe
        ///     to use for processing.
        /// </summary>
        public bool depthMaskInit = false;

        /// <summary>
        /// How many frames do we want to proccess before we're done determining the depth mask.
        /// </summary>
        private int depthMaskCount = 60;

        /// <summary>
        /// Determines whether we use the depth mask to filter data or not. NOTE - only valid after depthMaskInit == true.
        /// </summary>
        public bool useDepthMask = true;

        /// <summary>
        /// Determines whether we translate our depth image into color. If showDepth == false, we force this.colorBitmap to all black.
        /// </summary>
        public bool showDepth = true;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        public byte[] colorPixels;

        /// <summary>
        /// The closest distance (in mm) that we want to process info. More data may come back closer than this, 
        ///     but we will just blank out any pixels that are too close (appearing as a hole in the image instead).
        /// </summary>
        public short nearClip = 0;
        /// <summary>
        /// The furthest distance (in mm) that we want to process info. More data may come back further than this,
        ///     but we will just blank out any pixels that are too far (appearing as a hole in the image instead).
        /// </summary>
        public short farClip = 1500;

        /// <summary>
        /// Is the kinect manager fully setup - this includes establishing a link to an active kinect
        /// </summary>
        public bool init = false;

        /// <summary>
        /// Custom event that fires whenever we finish SensorDepthFrameReady(...) so other classes can know the latest 
        ///     depth info is ready for them to process.
        /// </summary>
        public event FrameDone endFrame;
        public event MaskDone endMask;
        private EventArgs e = null;
        public delegate void FrameDone(object sender, EventArgs e);
        public delegate void MaskDone(object sender, EventArgs e);

        public KinectManager() { }

        public void Initialize()
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new short[this.sensor.DepthStream.FramePixelDataLength];

                this.depthMask = new int[this.sensor.DepthStream.FramePixelDataLength];
                Array.Clear(this.depthMask, 0, this.depthMask.Length);

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];

                // This is the bitmap we'll display on-screen             
                this.colorBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Add an event handler to be called whenever there is new depth frame data
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                    init = true;
                }
                catch (IOException)
                {
                    this.sensor = null;
                }

                this.sensor.ElevationAngle = 0;
            }
        }

        /// <summary>
        /// Stops the kinect sensor - should be called when the window is closing.
        /// </summary>
        public void Stop()
        {
            if (this.sensor != null)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                lock (this.depthPixels)
                {
                    if (depthFrame != null)
                    {
                        // Copy the pixel data from the image to a temporary array
                        depthFrame.CopyPixelDataTo(this.depthPixels);

                        // Convert the depth to RGB
                        int colorPixelIndex = 0;

                        for (int i = 0; i < this.depthPixels.Length; ++i)
                        {
                            // discard the portion of the depth that contains only the player index
                            short depth = (short)(this.depthPixels[i] >> DepthImageFrame.PlayerIndexBitmaskWidth);

                            // Do my depth clipping here
                            // depth values range from -1 -> 4095 - this is actually measured in cm I think (handy :))
                            bool clipping = true;
                            if (clipping == true)
                            {
                                short min = nearClip;
                                short max;
                                if (useDepthMask == true)
                                {
                                    if (depthMask[i] > 0)
                                    {
                                        // arbitrary fudge factor of 10cm
                                        max = (short)(depthMask[i]);
                                    }
                                    else
                                    {
                                        max = farClip;
                                    }
                                }
                                else
                                {
                                    max = farClip;
                                }
                                // If we're "out of bounds", just set the value to -1 so it goes to black
                                if (depth > max || depth < min)
                                    depth = -1;
                            }

                            // Write our clipped depth info back into the array so we can process it later
                            this.depthPixels[i] = depth;

                            // During the first 100 frames we record the static background and save it for a mask later
                            // This allows us to ignore irregular terrain or objects in the room
                            if (depthMaskInit != true)
                            {
                                int fudgeFactor = 80;
                                if (this.depthPixels[i] > 0 && this.depthMask[i] > 0)
                                {
                                    // take the closer of the two values
                                    if (this.depthPixels[i] > this.nearClip && this.depthPixels[i] < this.depthMask[i])
                                    {
                                        this.depthMask[i] = this.depthPixels[i] - fudgeFactor;
                                    }
                                }
                                else if (this.depthPixels[i] > 0 && this.depthMask[i] == 0)
                                {
                                    // record this as the first value
                                    this.depthMask[i] = this.depthPixels[i] - fudgeFactor;
                                }
                            }

                            // to convert to a byte we're looking at only the lower 8 bits
                            // by discarding the most significant rather than least significant data
                            // we're preserving detail, although the intensity will "wrap"
                            // add 1 so that too far/unknown is mapped to black
                            //byte intensity = (byte)((depth + 1) & byte.MaxValue);
                            byte intensity = (byte)(256 - (depth / 256));

                            // Write out blue byte
                            this.colorPixels[colorPixelIndex++] = intensity;

                            // Write out green byte
                            this.colorPixels[colorPixelIndex++] = 0;

                            // Write out red byte                        
                            this.colorPixels[colorPixelIndex++] = intensity;

                            // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                            // If we were outputting BGRA, we would write alpha here.
                            ++colorPixelIndex;
                        } // end for (int i = 0; i < this.depthPixels.Length; ++i)

                        if (depthMaskInit != true)
                        {
                            depthMaskCount--;
                            if (depthMaskCount <= 0)
                            {
                                depthMaskInit = true;
                                endMask(this, e);
                                //depthMaskStatusLabel.Text = "Ready!";
                            }
                        }

                        // Draw the depth map on the background (if appropriate)
                        if (showDepth == true)
                        {
                            this.colorBitmap.WritePixels(
                                new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                                this.colorPixels,
                                this.colorBitmap.PixelWidth * sizeof(int),
                                0);
                        }
                        else
                        {
                            Array.Clear(this.colorPixels, 0, this.colorPixels.Length);
                            this.colorBitmap.WritePixels(
                                new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                                this.colorPixels,
                                this.colorBitmap.PixelWidth * sizeof(int),
                                0);
                        }
                    } // end if (depthFrame != null)
                } // end lock (this.depthPixels)
                endFrame(this, e);
            } // end using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
        } // end SensorDepthFrameReady(...)

        public void resetDepthMask()
        {
            Array.Clear(depthMask, 0, depthMask.Length);
            depthMaskCount = 100;
            useDepthMask = true;
            depthMaskInit = false;
        }
    }
}
