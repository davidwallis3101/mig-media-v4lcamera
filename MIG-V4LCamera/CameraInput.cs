// <copyright file="CameraInput.cs" company="Bounz">
// This file is part of HomeGenie-BE Project source code.
//
// HomeGenie-BE is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// HomeGenie is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// You should have received a copy of the GNU General Public License
// along with HomeGenie-BE.  If not, see http://www.gnu.org/licenses.
//
//  Project Homepage: https://github.com/Bounz/HomeGenie-BE
//
//  Forked from Homegenie by Generoso Martello gene@homegenie.it
// </copyright>

namespace MIG.Interfaces.Media
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using MIG.Config;
    using NLog;

    /*
     * TODO: Add source code and binaries for V4L camera lib wrapper
     * TODO: Add README.md file with instructions
    */

    /// <summary>
    /// CameraInput
    /// </summary>
    public class CameraInput : MigInterface
    {
        /// <summary>
        /// Logger
        /// </summary>
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Defines the cameraSource
        /// </summary>
        private IntPtr cameraSource = IntPtr.Zero;

        /// <summary>
        /// Defines the configuration
        /// </summary>
        private CameraConfiguration configuration = new CameraConfiguration();

        /// <summary>
        /// Defines the readPictureLock
        /// </summary>
        private object readPictureLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="CameraInput"/> class.
        /// </summary>
        public CameraInput()
        {
            var assemblyFolder = MigService.GetAssemblyDirectory(this.GetType().Assembly);

            Log.Debug($"{this.GetDomain()} Attempting to determine OS for V4L Driver");

            // video 4 linux interop, try to detect Raspbian/Ubuntu
            if (Directory.Exists("/lib/arm-linux-gnueabi") || Directory.Exists("/lib/arm-linux-gnueabihf"))
            {
                Log.Debug($"{this.GetDomain()} /lib/arm-linux-gnueabi exists");
                var command = "cp";
                var commandArgs = " -f \"" + Path.Combine(assemblyFolder, "v4l/raspbian_libCameraCaptureV4L.so") + "\" \"" + Path.Combine(assemblyFolder, "libCameraCaptureV4L.so") + "\"";
                Log.Debug($"{this.GetDomain()} Running: {command}{commandArgs}");
                MigService.ShellCommand(command, commandArgs);

                // if (File.Exists("/usr/lib/libgdiplus.so") && !File.Exists("/usr/local/lib/libgdiplus.so"))
                // {
                //    ShellCommand("ln", " -s \"/usr/lib/libgdiplus.so\" \"/usr/local/lib/libgdiplus.so\"");
                // }
            }
            else
            {
                Log.Debug($"{this.GetDomain()} /lib/arm-linux-gnueabi doesn't exist, using fallback");

                // fallback (ubuntu and other 64bit debians)
                var v4lfile = "v4l/debian64_libCameraCaptureV4L.so.gd3";
                if (!File.Exists("/usr/lib/x86_64-linux-gnu/libgd.so.3"))
                {
                    Log.Debug($"{this.GetDomain()} /usr/lib/x86_64-linux-gnu/libgd.so.3 doesn't exist");
                    v4lfile = "v4l/debian64_libCameraCaptureV4L.so";
                    Log.Debug($"{this.GetDomain()} v4lfile set to {v4lfile}");
                }

                var fallbackCommand = "cp";
                var fallbackArgs = " -f \"" + Path.Combine(assemblyFolder, v4lfile) + "\" \"" + Path.Combine(assemblyFolder, "libCameraCaptureV4L.so") + "\"";
                Log.Debug($"{this.GetDomain()} Running: {fallbackCommand}{fallbackArgs}");
                MigService.ShellCommand(fallbackCommand, fallbackArgs);
            }
        }

        /// <summary>
        /// InterfaceModulesChangedEventHandler
        /// </summary>
        public event InterfaceModulesChangedEventHandler InterfaceModulesChanged;

        /// <summary>
        /// InterfacePropertyChangedEventHandler
        /// </summary>
        public event InterfacePropertyChangedEventHandler InterfacePropertyChanged;

        /// <summary>
        /// Enum for commands available via web control
        /// </summary>
        public enum Commands
        {
            /// <summary>
            /// No Set
            /// </summary>
            NotSet,

            /// <summary>
            /// Get Picture
            /// </summary>
            Camera_GetPicture,

            /// <summary>
            /// Get Luminance
            /// </summary>
            Camera_GetLuminance,

            /// <summary>
            /// Set Device
            /// </summary>
            Camera_SetDevice
        }

        /// <summary>
        /// Gets a value indicating whether the interface/controller device is connected or not.
        /// </summary>
        /// <value>
        /// <c>true</c> if it is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected
        {
            get { return this.cameraSource != IntPtr.Zero; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the interface is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the options for the interface
        /// </summary>
        public List<Option> Options { get; set; }

        /// <summary>
        /// Connect to the automation interface/controller device.
        /// </summary>
        /// <returns>boolean value indicating the status</returns>
        public bool Connect()
        {
            Log.Debug($"{this.GetDomain()} Connecting");
            if (this.cameraSource != IntPtr.Zero)
            {
                Log.Debug($"{this.GetDomain()} invalid camera source");

                this.Disconnect();
            }

            if (this.GetOption("Configuration") != null && !string.IsNullOrEmpty(this.GetOption("Configuration").Value))
            {
                var config = this.GetOption("Configuration").Value.Split(',');
                this.SetConfiguration(config[0], uint.Parse(config[1]), uint.Parse(config[2]), uint.Parse(config[3]));
            }

            Log.Debug($"{this.GetDomain()} Opening camera stream on {this.configuration.Device}");
            this.cameraSource = CameraCaptureV4LInterop.OpenCameraStream(this.configuration.Device, this.configuration.Width, this.configuration.Height, this.configuration.Fps);
            this.OnInterfaceModulesChanged(this.GetDomain());

            this.OnInterfacePropertyChanged(this.GetDomain(), "AV0", "Camera Input", "Widget.DisplayModule", "homegenie/generic/camerainput");

            return this.cameraSource != IntPtr.Zero;
        }

        /// <summary>
        /// Disconnect the automation interface/controller device.
        /// </summary>
        public void Disconnect()
        {
            Log.Debug($"{this.GetDomain()} Disconnecting");
            if (this.cameraSource != IntPtr.Zero)
            {
                CameraCaptureV4LInterop.CloseCameraStream(this.cameraSource);
                this.cameraSource = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Gets the camera configuration
        /// </summary>
        /// <returns>CameraConfiguration</returns>
        public CameraConfiguration GetConfiguration()
        {
            return this.configuration;
        }

        /// <summary>
        /// The GetModules
        /// </summary>
        /// <returns>The <see cref="List{InterfaceModule}"/></returns>
        public List<InterfaceModule> GetModules()
        {
            List<InterfaceModule> modules = new List<InterfaceModule>();

            InterfaceModule module = new InterfaceModule();
            module.Domain = this.GetDomain();
            module.Address = "AV0";
            module.Description = "Video 4 Linux Video Input";
            module.ModuleType = MIG.ModuleTypes.Sensor;
            modules.Add(module);

            return modules;
        }

        /// <summary>
        /// InterfaceControl
        /// </summary>
        /// <param name="request">request from web interface</param>
        /// <returns>object</returns>
        public object InterfaceControl(MigInterfaceCommand request)
        {
            string response = string.Empty;

            Commands command;
            Enum.TryParse<Commands>(request.Command.Replace(".", "_"), out command);

            switch (command)
            {
                case Commands.Camera_GetPicture:
                    {
                        // get picture from camera <nodeid>
                        // TODO: there is actually only single camera support
                        if (this.cameraSource != IntPtr.Zero)
                        {
                            lock (this.readPictureLock)
                            {
                                var pictureBuffer = CameraCaptureV4LInterop.GetFrame(this.cameraSource);
                                var data = new byte[pictureBuffer.Size];
                                Marshal.Copy(pictureBuffer.Data, data, 0, pictureBuffer.Size);
                                return data;
                            }
                        }

                        break;
                    }

                case Commands.Camera_GetLuminance:
                    // TODO: ....
                    break;

                case Commands.Camera_SetDevice:
                    this.GetOption("Configuration").Value = request.GetOption(0) + "," + request.GetOption(1) + "," + request.GetOption(2) + "," + request.GetOption(3);
                    this.Connect();
                    break;
            }

            return response;
        }

        /// <summary>
        /// Returns true if the device has been found in the system
        /// </summary>
        /// <returns>bool</returns>
        public bool IsDevicePresent()
        {
            // eg. check against libusb for device presence by vendorId and productId
            return true;
        }

        /// <summary>
        /// Called When Setting an option
        /// </summary>
        /// <param name="option">the option</param>
        public void OnSetOption(Option option)
        {
            if (this.IsEnabled)
            {
                this.Connect();
            }
        }

        /// <summary>
        /// Sets Camera Confiugration
        /// </summary>
        /// <param name="device">device name</param>
        /// <param name="width">width in pixels</param>
        /// <param name="height">height in pixels</param>
        /// <param name="fps">frames per second</param>
        public void SetConfiguration(string device, uint width, uint height, uint fps)
        {
            this.configuration.Device = device;
            this.configuration.Width = width;
            this.configuration.Height = height;
            this.configuration.Fps = fps;
        }

        /// <summary>
        /// Called when interface modules are changed
        /// </summary>
        /// <param name="domain">domain</param>
        protected virtual void OnInterfaceModulesChanged(string domain)
        {
            if (this.InterfaceModulesChanged != null)
            {
                var args = new InterfaceModulesChangedEventArgs(domain);
                this.InterfaceModulesChanged(this, args);
            }
        }

        /// <summary>
        /// Called when an interface property is changed
        /// </summary>
        /// <param name="domain">domain</param>
        /// <param name="source">source</param>
        /// <param name="description">description</param>
        /// <param name="propertyPath">property path</param>
        /// <param name="propertyValue">property value</param>
        protected virtual void OnInterfacePropertyChanged(string domain, string source, string description, string propertyPath, object propertyValue)
        {
            if (this.InterfacePropertyChanged != null)
            {
                var args = new InterfacePropertyChangedEventArgs(domain, source, description, propertyPath, propertyValue);
                this.InterfacePropertyChanged(this, args);
            }
        }

        /// <summary>
        /// Picture buffer.
        /// </summary>
        public struct PictureBuffer
        {
            /// <summary>
            /// Defines the Data
            /// </summary>
            public IntPtr Data;

            /// <summary>
            /// Defines the Size
            /// </summary>
            public int Size;
        }

        /// <summary>
        /// Camera capture v4L interop.
        /// </summary>
        public class CameraCaptureV4LInterop
        {
            /// <summary>
            /// The CloseCameraStream
            /// </summary>
            /// <param name="source">The <see cref="IntPtr"/></param>
            [DllImport("CameraCaptureV4L.so", EntryPoint = "CloseCameraStream")]
            public static extern void CloseCameraStream(IntPtr source);

            /// <summary>
            /// The GetFrame
            /// </summary>
            /// <param name="source">The <see cref="IntPtr"/>source</param>
            /// <returns>The <see cref="PictureBuffer"/></returns>
            [DllImport("CameraCaptureV4L.so", EntryPoint = "GetFrame")]
            public static extern PictureBuffer GetFrame(IntPtr source);

            /// <summary>
            /// The OpenCameraStream
            /// </summary>
            /// <param name="device">The <see cref="string"/>device name</param>
            /// <param name="width">The <see cref="uint"/>width in pixels</param>
            /// <param name="height">The <see cref="uint"/>height in pixels</param>
            /// <param name="fps">The <see cref="uint"/>frames per second</param>
            /// <returns>The <see cref="IntPtr"/></returns>
            [DllImport("CameraCaptureV4L.so", EntryPoint = "OpenCameraStream")]
            public static extern IntPtr OpenCameraStream(string device, uint width, uint height, uint fps);

            /// <summary>
            /// The TakePicture
            /// </summary>
            /// <param name="device">The <see cref="string"/>device name</param>
            /// <param name="width">The <see cref="uint"/>width in pixels</param>
            /// <param name="height">The <see cref="uint"/>height in pixels</param>
            /// <param name="jpegQuantity">The <see cref="uint"/>quantity of images</param>
            /// <returns>The <see cref="PictureBuffer"/></returns>
            [DllImport("CameraCaptureV4L.so", EntryPoint = "TakePicture")]
            public static extern PictureBuffer TakePicture(string device, uint width, uint height, uint jpegQuantity);
        }

        /// <summary>
        /// Camera configuration.
        /// </summary>
        public class CameraConfiguration
        {
            #pragma warning disable SA1401 // Fields must be private

            /// <summary>
            /// Defines the Device
            /// </summary>
            public string Device = "/dev/video0";

            /// <summary>
            /// Defines the Fps
            /// </summary>
            public uint Fps = 2;

            /// <summary>
            /// Defines the Height
            /// </summary>
            public uint Height = 240;

            /// <summary>
            /// Defines the Width
            /// </summary>
            public uint Width = 320;
            #pragma warning restore SA1401 // Fields must be private
        }
    }
}
