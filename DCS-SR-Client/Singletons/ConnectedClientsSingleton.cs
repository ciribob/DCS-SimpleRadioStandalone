using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons
{
    public sealed class ConnectedClientsSingleton : INotifyPropertyChanged
    {
        private readonly ConcurrentDictionary<string, SRClient> _clients = new ConcurrentDictionary<string, SRClient>();
        private static volatile ConnectedClientsSingleton _instance;
        private static object _lock = new Object();
        private readonly string _guid = ClientStateSingleton.Instance.ShortGUID;
        private ServerSettingsModel _serverSettings { get; } = Ioc.Default.GetRequiredService<ISrsSettings>().CurrentServerSettings;

        public event PropertyChangedEventHandler PropertyChanged;

        private ConnectedClientsSingleton() { }

        public static ConnectedClientsSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new ConnectedClientsSingleton();
                    }
                }

                return _instance;
            }
        }

        private void NotifyPropertyChanged(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyAll()
        {
            NotifyPropertyChanged("Total");
        }

        public SRClient this[string key]
        {
            get
            {
                return _clients[key];
            }
            set
            {
                _clients[key] = value;
               NotifyAll();
            }
        }

        public ICollection<SRClient> Values
        {
            get
            {
                return _clients.Values;
            }
        }

        public int Total
        {
            get
            {
                return _clients.Count();
            }
        }

        public bool TryRemove(string key, out SRClient value)
        {
            bool result = _clients.TryRemove(key, out value);
            if (result)
            {
                NotifyPropertyChanged("Total");
            }
            return result;
        }

        public void Clear()
        {
            _clients.Clear();
            NotifyPropertyChanged("Total");
        }

        public bool TryGetValue(string key, out SRClient value)
        {
            return _clients.TryGetValue(key, out value);
        }

        public bool ContainsKey(string key)
        {
            return _clients.ContainsKey(key);
        }

        public int ClientsOnFreq(double freq, RadioInformation.Modulation modulation)
        {
            if (!_serverSettings.IsShowTunedListenerCount)
            {
                return 0;
            }
            var currentClientPos = ClientStateSingleton.Instance.PlayerCoaltionLocationMetadata;
            var currentUnitId = ClientStateSingleton.Instance.DcsPlayerRadioInfo.unitId;
            var coalitionSecurity = _serverSettings.IsCoalitionAudioSeperated;
            List<double> globalFrequencies = _serverSettings.GlobalLobbyFrequenciesList;
            var global = globalFrequencies.Contains(freq);
            int count = 0;

            foreach (var client in _clients)
            {
                if (!client.Key.Equals(_guid))
                {
                    // check that either coalition radio security is disabled OR the coalitions match
                    if (global|| (!coalitionSecurity || (client.Value.Coalition == currentClientPos.side)))
                    {

                        var radioInfo = client.Value.RadioInfo;

                        if (radioInfo != null)
                        {
                            RadioReceivingState radioReceivingState = null;
                            bool decryptable;
                            var receivingRadio = radioInfo.CanHearTransmission(freq,
                                modulation,
                                0,
                                false,
                                currentUnitId,
                                new List<int>(),
                                out radioReceivingState,
                                out decryptable);

                            //only send if we can hear!
                            if (receivingRadio != null)
                            {
                                count++;
                            }
                        }
                    }
                }
            }

            return count;
        }
    }
}
