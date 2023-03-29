﻿using CSCore.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CSCore.CoreAudioAPI
{
    /// <summary>
    /// The <see cref="AudioSessionControl"/> class enables a client to configure the control parameters for an audio session and to monitor events in the session.
    /// For more information, see <see href="https://msdn.microsoft.com/en-us/library/windows/desktop/dd368246(v=vs.85).aspx"/>.
    /// </summary>
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    public class AudioSessionControl : ComObject
    {
        private const string InterfaceName = "IAudioSessionControl";

        private readonly List<IAudioSessionEvents> _sessionEventHandler = new List<IAudioSessionEvents>();
        private readonly AudioSessionEvents _sessionEvents = new AudioSessionEvents();

        /// <summary>
        /// Occurs when the display name for the session has changed.
        /// </summary>
        public event EventHandler<AudioSessionDisplayNameChangedEventArgs> DisplayNameChanged
        {
            add { _sessionEvents.DisplayNameChanged += value; }
            remove { _sessionEvents.DisplayNameChanged -= value; }
        }

        /// <summary>
        /// Occurs when the display icon for the session has changed.
        /// </summary>
        public event EventHandler<AudioSessionIconPathChangedEventArgs> IconPathChanged
        {
            add { _sessionEvents.IconPathChanged += value; }
            remove { _sessionEvents.IconPathChanged -= value; }
        }

        /// <summary>
        /// Occurs when the volume level or muting state of the session has changed.
        /// </summary>
        public event EventHandler<AudioSessionSimpleVolumeChangedEventArgs> SimpleVolumeChanged
        {
            add { _sessionEvents.SimpleVolumeChanged += value; }
            remove { _sessionEvents.SimpleVolumeChanged -= value; }
        }

        /// <summary>
        /// Occurs when the volume level of an audio channel in the session submix has changed.
        /// </summary>
        public event EventHandler<AudioSessionChannelVolumeChangedEventArgs> ChannelVolumeChanged
        {
            add { _sessionEvents.ChannelVolumeChanged += value; }
            remove { _sessionEvents.ChannelVolumeChanged -= value; }
        }

        /// <summary>
        /// Occurs when the grouping parameter for the session has changed.
        /// </summary>
        public event EventHandler<AudioSessionGroupingParamChangedEventArgs> GroupingParamChanged
        {
            add { _sessionEvents.GroupingParamChanged += value; }
            remove { _sessionEvents.GroupingParamChanged -= value; }
        }

        /// <summary>
        /// Occurs when the stream-activity state of the session has changed.
        /// </summary>
        public event EventHandler<AudioSessionStateChangedEventArgs> StateChanged
        {
            add { _sessionEvents.StateChanged += value; }
            remove { _sessionEvents.StateChanged -= value; }
        }

        /// <summary>
        /// Occurs when the session has been disconnected.
        /// </summary>
        public event EventHandler<AudioSessionDisconnectedEventArgs> SessionDisconnected
        {
            add { _sessionEvents.SessionDisconnected += value; }
            remove { _sessionEvents.SessionDisconnected -= value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioSessionControl"/> class.
        /// </summary>
        /// <param name="ptr">Native pointer of the <see cref="AudioSessionControl"/> object.</param>
        public AudioSessionControl(IntPtr ptr)
            : base(ptr)
        {
            RegisterAudioSessionNotification(_sessionEvents);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioSessionControl"/> class.
        /// </summary>
        /// <param name="audioClient">The audio client to create a <see cref="AudioSessionControl"/> instance for.</param>
        /// <exception cref="System.ArgumentNullException">audioClient</exception>
        public AudioSessionControl(AudioClient audioClient)
        {
            if (audioClient == null)
                throw new ArgumentNullException("audioClient");

            BasePtr = audioClient.GetService(typeof (AudioSessionControl).GUID);
            RegisterAudioSessionNotification(_sessionEvents);
        }

        /// <summary>
        /// Gets the current state of the audio session.
        /// </summary>
        public AudioSessionState SessionState
        {
            get
            {
                AudioSessionState sessionState;
                CoreAudioAPIException.Try(GetStateNative(out sessionState), InterfaceName, "GetState");
                return sessionState;
            }
        }

        /// <summary>
        /// Gets or sets the display name for the audio session.
        /// </summary>
        public string DisplayName
        {
            get
            {
                string displayName;
                CoreAudioAPIException.Try(GetDisplayNameNative(out displayName), InterfaceName, "GetDisplayName");
                return displayName;
            }
            set { CoreAudioAPIException.Try(SetDisplayNameNative(value, Guid.Empty), InterfaceName, "SetDisplayName"); }
        }

        /// <summary>
        /// Gets or sets the path for the display icon for the audio session.
        /// </summary>
        public string IconPath
        {
            get
            {
                string iconPath;
                CoreAudioAPIException.Try(GetIconPathNative(out iconPath), InterfaceName, "GetIconPath");
                return iconPath;
            }
            set
            {
                CoreAudioAPIException.Try(SetIconPathNative(value, Guid.Empty), InterfaceName, "SetIconPath");
            }
        }

        /// <summary>
        /// Gets or sets the grouping parameter of the audio session.
        /// </summary>
        public Guid GroupingParam
        {
            get
            {
                Guid gp;
                CoreAudioAPIException.Try(GetGroupingParamNative(out gp), InterfaceName, "GetGroupingParam");
                return gp;
            }
            set
            {
                CoreAudioAPIException.Try(SetGroupingParamNative(value, Guid.Empty), InterfaceName, "SetGroupingParam");
            }
        }

        /// <summary>
        /// Retrieves the current state of the audio session.
        /// <seealso cref="SessionState"/>
        /// </summary>
        /// <param name="state">A variable into which the method writes the current session state.</param>
        /// <returns>HRESULT</returns>
        public unsafe int GetStateNative(out AudioSessionState state)
        {
            fixed (void* p = &state)
            {
                return InteropCalls.CallI(UnsafeBasePtr, p, ((void**) (*(void**) UnsafeBasePtr))[3]);
            }
        }

        /// <summary>
        /// Retrieves the display name for the audio session.
        /// <seealso cref="DisplayName"/>
        /// </summary>
        /// <param name="displayName">A variable into which the method writes the display name of the session.</param>
        /// <returns>HRESULT</returns>
        public unsafe int GetDisplayNameNative(out string displayName)
        {
            IntPtr ptr = IntPtr.Zero;
            int result = InteropCalls.CallI(UnsafeBasePtr, &ptr, ((void**) (*(void**) UnsafeBasePtr))[4]);
            if (result == 0 && ptr != IntPtr.Zero)
            {
                displayName = Marshal.PtrToStringUni(ptr);
                Marshal.FreeCoTaskMem(ptr);
            }
            else
                displayName = null;
            return result;
        }

        /// <summary>
        /// Assigns a display name to the current session.
        /// <seealso cref="DisplayName"/>
        /// </summary>
        /// <param name="displayName">The new display name of the audio session.</param>
        /// <param name="eventContext">EventContext which can be accessed in the event handler.</param>        
        /// <returns>HRESULT</returns>
        public unsafe int SetDisplayNameNative(string displayName, Guid eventContext)
        {
            IntPtr p = displayName != null ? Marshal.StringToHGlobalUni(displayName) : IntPtr.Zero;
            int result = InteropCalls.CallI(UnsafeBasePtr, p.ToPointer(), &eventContext,
                ((void**) (*(void**) UnsafeBasePtr))[5]);
            Marshal.FreeHGlobal(p);
            return result;
        }

        /// <summary>
        /// Retrieves the path for the display icon for the audio session.
        /// <seealso cref="IconPath"/>        
        /// </summary>
        /// <param name="iconPath">A variable into which the method writes the path and file name of an .ico, .dll, or .exe file that contains the icon.</param>
        /// <returns>HRESULT</returns>
        public unsafe int GetIconPathNative(out string iconPath)
        {
            IntPtr ptr = IntPtr.Zero;
            int result = InteropCalls.CallI(UnsafeBasePtr, &ptr, ((void**) (*(void**) UnsafeBasePtr))[6]);
            if (result == 0 && ptr != IntPtr.Zero)
            {
                iconPath = Marshal.PtrToStringUni(ptr);
                Marshal.FreeCoTaskMem(ptr);
            }
            else
                iconPath = null;
            return result;
        }

        /// <summary>
        /// Assigns a display icon to the current session.
        /// <seealso cref="IconPath"/>
        /// </summary>
        /// <param name="iconPath">A string that specifies the path and file name of an .ico, .dll, or .exe file that contains the icon.</param>        
        /// <param name="eventContext">EventContext which can be accessed in the event handler.</param>        
        /// <returns>HRESULT</returns>
        public unsafe int SetIconPathNative(string iconPath, Guid eventContext)
        {
            IntPtr p = iconPath != null ? Marshal.StringToHGlobalUni(iconPath) : IntPtr.Zero;
            int result = InteropCalls.CallI(UnsafeBasePtr, p.ToPointer(), &eventContext,
                ((void**) (*(void**) UnsafeBasePtr))[7]);
            Marshal.FreeHGlobal(p);
            return result;
        }

        /// <summary>
        /// Retrieves the grouping parameter of the audio session.
        /// <seealso cref="GroupingParam"/>
        /// </summary>
        /// <param name="groupingParam">A variable into which the method writes the grouping parameter.</param>
        /// <returns>HRESULT</returns>
        /// <remarks>For some more information about grouping parameters, see <see href="http://msdn.microsoft.com/en-us/library/windows/desktop/dd370848(v=vs.85).aspx"/>.</remarks>
        public unsafe int GetGroupingParamNative(out Guid groupingParam)
        {
            fixed (void* p = &groupingParam)
            {
                return InteropCalls.CallI(UnsafeBasePtr, p, ((void**) (*(void**) UnsafeBasePtr))[8]);
            }
        }

        /// <summary>
        /// Assigns a session to a grouping of sessions.
        /// <seealso cref="GroupingParam"/>
        /// </summary>
        /// <param name="groupingParam"></param>
        /// <param name="eventContext"></param>
        /// <returns>HRESULT</returns>
        /// <remarks>For some more information about grouping parameters, see <see href="http://msdn.microsoft.com/en-us/library/windows/desktop/dd370848(v=vs.85).aspx"/>.</remarks>        
        public unsafe int SetGroupingParamNative(Guid groupingParam, Guid eventContext)
        {
            return InteropCalls.CallI(UnsafeBasePtr, &groupingParam, &eventContext,
                ((void**) (*(void**) UnsafeBasePtr))[9]);
        }

        /// <summary>
        /// Registers the client to receive notifications of session events, including changes in the stream state.
        /// <seealso cref="RegisterAudioSessionNotification"/>
        /// </summary>
        /// <param name="notifications">An instance of the <see cref="IAudioSessionEvents"/> object which receives the notifications.</param>
        /// <returns>HRESULT</returns>
        public unsafe int RegisterAudioSessionNotificationNative(IAudioSessionEvents notifications)
        {
            int result = 0;
            if (!_sessionEventHandler.Contains(notifications))
            {
                IntPtr ptr = notifications != null
                    ? Marshal.GetComInterfaceForObject(notifications, typeof (IAudioSessionEvents))
                    : IntPtr.Zero;
                try
                {
                    result = InteropCalls.CallI(UnsafeBasePtr, ptr.ToPointer(), ((void**) (*(void**) UnsafeBasePtr))[10]);
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.Release(ptr);
                    }
                }
                _sessionEventHandler.Add(notifications);
            }
            return result;
        }

        /// <summary>
        /// Registers the client to receive notifications of session events, including changes in the stream state.
        /// </summary>
        /// <param name="notifications">An instance of the <see cref="IAudioSessionEvents"/> object which receives the notifications.</param>
        public void RegisterAudioSessionNotification(IAudioSessionEvents notifications)
        {
            CoreAudioAPIException.Try(RegisterAudioSessionNotificationNative(notifications), InterfaceName,
                "RegisterAudioSessionNotification");
        }

        /// <summary>
        /// Deletes a previous registration by the client to receive notifications.
        /// </summary>
        /// <param name="notifications">The instance of the <see cref="IAudioSessionEvents"/> object which got registered previously by the <see cref="RegisterAudioSessionNotification"/> method.</param>
        /// <returns>HRESULT</returns>
        public unsafe int UnregisterAudioSessionNotificationNative(IAudioSessionEvents notifications)
        {
            int result = 0;
            if (_sessionEventHandler.Contains(notifications))
            {
                IntPtr ptr = notifications != null
                    ? Marshal.GetComInterfaceForObject(notifications, typeof (IAudioSessionEvents))
                    : IntPtr.Zero;
                try
                {
                    result = InteropCalls.CallI(UnsafeBasePtr, ptr.ToPointer(), ((void**) (*(void**) UnsafeBasePtr))[11]);
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.Release(ptr);
                    }
                }
                _sessionEventHandler.Remove(notifications);
            }
            return result;
        }

        /// <summary>
        /// Deletes a previous registration by the client to receive notifications.
        /// </summary>
        /// <param name="notifications">The instance of the <see cref="IAudioSessionEvents"/> object which got registered previously by the <see cref="RegisterAudioSessionNotification"/> method.</param>
        public void UnregisterAudioSessionNotification(IAudioSessionEvents notifications)
        {
            CoreAudioAPIException.Try(UnregisterAudioSessionNotificationNative(notifications), InterfaceName,
                "UnregisterAudioSessionNotification");
        }

        /// <summary>
        /// Releases the COM object.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            for (int i = _sessionEventHandler.Count - 1; i >= 0; i--)
            {
                UnregisterAudioSessionNotification(_sessionEventHandler[i]);
            }

            base.Dispose(disposing);
        }
    }
}