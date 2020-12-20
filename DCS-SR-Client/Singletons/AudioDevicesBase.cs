using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NLog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons
{
    public abstract class AudioDevicesBase : IMMNotificationClient, IDisposable, INotifyPropertyChanged
    {
        private bool _disposed = false;
        private readonly Logger _logger;

        public event PropertyChangedEventHandler PropertyChanged;

        protected MMDeviceEnumerator DeviceEnum { get; private set; }

        protected AudioDevicesBase(Logger logger)
        {
            DeviceEnum = new MMDeviceEnumerator();
            DeviceEnum.RegisterEndpointNotificationCallback(this);
            _logger = logger;
        }

        /// <summary>
        /// Override this method to react to changes in the device list.
        /// </summary>
        protected abstract void OnDeviceEnumChanged(string deviceId);

        public void Dispose()
        {
            Debug.Assert(!_disposed);
            if (_disposed) { return; }

            DeviceEnum?.UnregisterEndpointNotificationCallback(this);
            DeviceEnum?.Dispose();
            DeviceEnum = null;

            _disposed = true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region IMMNotificationClient
        // implement explicitly to reduce surface area of inheriting objects

        void IMMNotificationClient.OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, [MarshalAs(UnmanagedType.I4)] DeviceState newState)
        {
            _logger.Info("Device {deviceId} State Changed to {newState}, rebuilding device list", deviceId, newState);
            OnDeviceEnumChanged(deviceId);
        }

        // The added and removed handlers don't seem to fire for me, but as the logic is the same I'm going to leave these handlers here.
        void IMMNotificationClient.OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId)
        {
            _logger.Info("Device {deviceId} Added, rebuilding device list", deviceId);
            OnDeviceEnumChanged(deviceId);
        }

        void IMMNotificationClient.OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId)
        {
            _logger.Info("Device {deviceId} Removed, rebuilding device list.", deviceId);
            OnDeviceEnumChanged(deviceId);
        }

        void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId)
        {
        }

        void IMMNotificationClient.OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PropertyKey key)
        {
        }

        #endregion
    }
}
