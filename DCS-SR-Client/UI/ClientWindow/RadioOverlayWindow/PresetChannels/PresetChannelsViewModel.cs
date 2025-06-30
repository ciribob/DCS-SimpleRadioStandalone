using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow.PresetChannels;

public class PresetChannelsViewModel : INotifyPropertyChanged, IHandle<ProfileChangedMessage>, IHandle<ServerSettingsPresetsSettingChangedMessage>
{
    private readonly IPresetChannelsStore _channelsStore;


    private readonly object _presetChannelLock = new();
    private ObservableCollection<PresetChannel> _presetChannels;
    private int _radioId;
    private Visibility _showPresetCreate = Visibility.Visible;
    private ProfileSettingsStore _profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;

    public PresetChannelsViewModel(IPresetChannelsStore channels, int radioId)
    {
        _radioId = radioId;
        _channelsStore = channels;
        ReloadCommand = new DelegateCommand(OnReload);
        DropDownClosedCommand = new DelegateCommand(DropDownClosed);
        PresetChannels = new ObservableCollection<PresetChannel>();
        PresetCreateCommand = new DelegateCommand(CreatePreset);
        EventBus.Instance.SubscribeOnUIThread(this);
    }

    public ICommand DropDownClosedCommand { get; }

    public DelegateCommand PresetCreateCommand { get; set; }

    public Visibility ShowPresetCreate
    {
        get => _showPresetCreate;
        set
        {
            _showPresetCreate = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ShowPresetCreate"));
        }
    }

    public ObservableCollection<PresetChannel> PresetChannels
    {
        get => _presetChannels;
        set
        {
            _presetChannels = value;
            BindingOperations.EnableCollectionSynchronization(_presetChannels, _presetChannelLock);
        }
    }

    public int RadioId
    {
        private get => _radioId;
        set
        {
            _radioId = value;
            Reload();
        }
    }


    public ICommand ReloadCommand { get; }

    public PresetChannel SelectedPresetChannel { get; set; }

    public double Max { get; set; }
    public double Min { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    private void DropDownClosed()
    {
        if (SelectedPresetChannel != null
            && SelectedPresetChannel.Value is double
            && (double)SelectedPresetChannel.Value > 0 && RadioId > 0)
            RadioHelper.SelectRadioChannel(SelectedPresetChannel, RadioId);
    }

    public void Reload()
    {
        PresetChannels.Clear();
        ShowPresetCreate = Visibility.Collapsed;

        var radios = ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios;

        var radio = radios[_radioId];
        
        var presetChannelSettingsEnum = ServerPresetConfiguration.USE_CLIENT_AND_SERVER_IF_SET;
            
        if (!ServerPresetConfiguration.TryParse(_profileSettings.GetClientSettingString(ProfileSettingsKeys.ServerPresetSelection), out presetChannelSettingsEnum))
        {
            presetChannelSettingsEnum = ServerPresetConfiguration.USE_CLIENT_AND_SERVER_IF_SET;
        }
        var presetChannelsClientList = new List<PresetChannel>();
        var presetChannelServerList = new List<PresetChannel>();
        
        if (radio.modulation != Modulation.MIDS)
        {
            foreach (var channel in _channelsStore.LoadFromStore(radio.name))
                if ((double)channel.Value <= Max
                    && (double)channel.Value >= Min)
                {
                    presetChannelsClientList.Add(channel);
                }
            
            foreach (var channel in SyncedServerSettings.Instance.GetPresetChannels(FilePresetChannelsStore.NormaliseString(radio.name)))
            {
                var freq = channel.Frequency * FilePresetChannelsStore.MHz; //convert to Hz from MHz,
                if (freq <= Max
                    && freq >= Min)
                    presetChannelServerList.Add( new PresetChannel()
                    {
                        Value = (double)freq, 
                        Text = channel.Name
                    });
            }
        }
        else
        {
            foreach (var channel in _channelsStore.LoadFromStore(radio.name, true))
            {
                presetChannelsClientList.Add(channel);
            }
            
            foreach (var channel in SyncedServerSettings.Instance.GetPresetChannels(FilePresetChannelsStore.NormaliseString(radio.name)))
            {
                //frequency is calculated off the channel for mids
                var midsChannel = (int)channel.Frequency;

                if (midsChannel is > 0 and < 126)
                {
                    presetChannelServerList.Add( new PresetChannel()
                    {
                        Value = (double) midsChannel * FilePresetChannelsStore.MHz + FilePresetChannelsStore.MidsOffsetMHz,
                        Text = channel.Name,
                        MidsChannel = midsChannel
                    });
                }
            }
        }
        
        if (presetChannelSettingsEnum == ServerPresetConfiguration.USE_CLIENT_ONLY)
        {
            var channel = 1;
            foreach (var presetChannel in presetChannelsClientList)
            {
                presetChannel.Channel = channel++;
                NameChannel(presetChannel);
                PresetChannels.Add(presetChannel);
            }
        }
        else if (presetChannelSettingsEnum == ServerPresetConfiguration.USE_SERVER_ONLY_IF_SET)
        {
            var channel = 1;
            if (presetChannelServerList.Count > 0)
            {
                foreach (var presetChannel in presetChannelServerList)
                {
                    presetChannel.Channel = channel++;
                    NameChannel(presetChannel);
                    PresetChannels.Add(presetChannel);
                }
            }
            else
            {
                   
                foreach (var presetChannel in presetChannelsClientList)
                {
                    presetChannel.Channel = channel++;
                    NameChannel(presetChannel);
                    PresetChannels.Add(presetChannel);
                }
            }
        }
        else
        {
            //USE BOTH - priority to client
                
            var channel = 1;
            foreach (var presetChannel in presetChannelsClientList)
            {
                presetChannel.Channel = channel++;
                NameChannel(presetChannel);
                PresetChannels.Add(presetChannel);
            }
                
            foreach (var presetChannel in presetChannelServerList)
            {
                presetChannel.Channel = channel++;
                NameChannel(presetChannel);
                PresetChannels.Add(presetChannel);
            }
        }

        //if no preset channels are loaded, create a default one for MIDS
        if (radio.modulation == Modulation.MIDS)
        {
            if (PresetChannels.Count == 0)
            {
                for (var chn = 1; chn < 126; chn++)
                    PresetChannels.Add(new PresetChannel
                    {
                        MidsChannel = chn,
                        Channel = chn,
                        Text = "MIDS " + chn,
                        Value = (double) (chn * FilePresetChannelsStore.MHz + FilePresetChannelsStore.MidsOffsetMHz)
                    });
            }
        }

        if (PresetChannels.Count > 0)
            ShowPresetCreate = Visibility.Collapsed;
        else
            ShowPresetCreate = Visibility.Visible;
    }

    private void NameChannel(PresetChannel presetChannel)
    {
        if (presetChannel.MidsChannel > 0)
            presetChannel.Text = presetChannel.Text + " | " + presetChannel.MidsChannel;
        else
            presetChannel.Text = presetChannel.Channel + ": " + presetChannel.Text;
    }

    private void OnReload()
    {
        Reload();
    }

    private void CreatePreset()
    {
        var radios = ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios;

        var radio = radios[_radioId];

        if (radio.modulation != Modulation.DISABLED &&
            radio.modulation != Modulation.INTERCOM)
        {
            var path = _channelsStore.CreatePresetFile(radio.name);
            if (path != null)
            {
                var res = MessageBox.Show($"Created presets file at path:\n {path} \n\nOpen the file?",
                    "Created Preset File", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No);
                if (res == MessageBoxResult.Yes)
                    try
                    {
                        Process.Start(new ProcessStartInfo(path)
                            { UseShellExecute = true });
                    }
                    catch (Exception)
                    {
                    }
            }
        }
    }

    public void Clear()
    {
        PresetChannels.Clear();
    }

    public Task HandleAsync(ProfileChangedMessage message, CancellationToken cancellationToken)
    {
        ReloadCommand.Execute(null);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ServerSettingsPresetsSettingChangedMessage message, CancellationToken cancellationToken)
    {
        ReloadCommand.Execute(null);
        return Task.CompletedTask;
    }
}