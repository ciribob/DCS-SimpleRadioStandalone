using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Preferences;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites
{
    public class FavouriteServersViewModel
    {
        private readonly ObservableCollection<ServerAddress> _addresses = new ObservableCollection<ServerAddress>();
        private readonly IFavouriteServerStore _favouriteServerStore;
        private ClientSettingsModel Settings { get; } = Ioc.Default.GetRequiredService<ISrsSettings>().ClientSettings;

        public FavouriteServersViewModel(IFavouriteServerStore favouriteServerStore)
        {
            _favouriteServerStore = favouriteServerStore;

            _addresses.CollectionChanged += OnServerAddressesCollectionChanged;

            foreach (var favourite in _favouriteServerStore.LoadFromStore())
            {
                _addresses.Add(favourite);
            }

            NewAddressCommand = new DelegateCommand(OnNewAddress);
            RemoveSelectedCommand = new DelegateCommand(OnRemoveSelected);
            OnDefaultChangedCommand = new DelegateCommand(OnDefaultChanged);
        }

        public ObservableCollection<ServerAddress> Addresses => _addresses;

        public string NewName { get; set; }

        public string NewAddress { get; set; }

        public string NewEAMCoalitionPassword { get; set; }

        public ICommand NewAddressCommand { get; }

        public ICommand SaveCommand { get; set; }

        public ICommand RemoveSelectedCommand { get; set; }

        public ICommand OnDefaultChangedCommand { get; set; }

        public ServerAddress SelectedItem { get; set; }

        public ServerAddress DefaultServerAddress
        {
            get
            {
                var defaultAddress = _addresses.FirstOrDefault(x => x.IsDefault);
                if (defaultAddress == null && _addresses.Count > 0)
                {
                    defaultAddress = _addresses.First();
                }
                return defaultAddress;
            }
        }

        private void OnNewAddress()
        {
            var isDefault = _addresses.Count == 0;
            _addresses.Add(new ServerAddress(NewName, NewAddress, string.IsNullOrWhiteSpace(NewEAMCoalitionPassword) ? null : NewEAMCoalitionPassword, isDefault));

            Save();
        }

        private void OnRemoveSelected()
        {
            if (SelectedItem == null)
            {
                return;
            }

            _addresses.Remove(SelectedItem);

            if (_addresses.Count == 0 && !string.IsNullOrEmpty(Settings.LastServer))
            {
                var oldAddress = new ServerAddress(Settings.LastServer,
                    Settings.LastServer, null, true);
                _addresses.Add(oldAddress);
            }

            Save();
        }

        private void Save()
        {
            var saveSucceeded = _favouriteServerStore.SaveToStore(_addresses);
            if (!saveSucceeded)
            {
                MessageBox.Show(Application.Current.MainWindow,
                    "Failed to save favourite servers. Please check logs for details.",
                    "Favourite server save failure",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnDefaultChanged(object obj)
        {
            var address = obj as ServerAddress;
            if (address == null)
            {
                throw new InvalidOperationException();
            }

            if (address.IsDefault)
            {
                return;
            }

            address.IsDefault = true;

            foreach (var serverAddress in _addresses)
            {
                if (serverAddress != address)
                {
                    serverAddress.IsDefault = false;
                }
            }

            Save();
        }

        private void OnServerAddressesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ServerAddress address in e.NewItems)
                {
                    address.PropertyChanged += OnServerAddressPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (ServerAddress address in e.OldItems)
                {
                    address.PropertyChanged -= OnServerAddressPropertyChanged;
                }
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
            {
                Save();
            }
        }

        private void OnServerAddressPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Saving after changing default favourite is done by OnDefaultChanged
            if (e.PropertyName != "IsDefault")
            {
                Save();
            }
        }
    }
}