using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.VAICOM.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using RadioReceivingState = Ciribob.DCS.SimpleRadio.Standalone.Common.Models.RadioReceivingState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;

public sealed class ClientStateSingleton : PropertyChangedBaseClass, IHandle<TCPClientStatusMessage>
{
    public delegate bool RadioUpdatedCallback();

    private static volatile ClientStateSingleton _instance;
    private static readonly object _lock = new();

    private static readonly DispatcherTimer _timer = new();
    private readonly DCSAutoConnectHandler _dcsAutoConnectHandler;
    

    private bool isConnected;

    private bool isConnectionErrored;

    private bool isVoipConnected;

    private ClientStateSingleton()
    {
        RadioSendingState = new RadioSendingState();
        RadioReceivingState = new RadioReceivingState[11];

        ShortGUID = ShortGuid.NewGuid();
        DcsPlayerRadioInfo = new DCSPlayerRadioInfo();
        PlayerCoaltionLocationMetadata = new DCSPlayerSideInfo();

        // The following members are not updated due to events. Therefore we need to setup a polling action so that they are
        // periodically checked.
        DcsGameGuiLastReceived = 0;
        DcsExportLastReceived = 0;
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (s, e) =>
        {
            NotifyPropertyChanged("IsGameConnected");
            NotifyPropertyChanged("IsLotATCConnected");
            NotifyPropertyChanged("ExternalAWACSModeConnected");
        };
        _timer.Start();

        FixedChannels = new PresetChannelsViewModel[Constants.MAX_RADIOS];

        for (var i = 0; i < FixedChannels.Length; i++)
            FixedChannels[i] = new PresetChannelsViewModel(new FilePresetChannelsStore(), i + 1);

        LastSent = 0;

        IsConnected = false;
        ExternalAWACSModelSelected = false;

        LastSeenName = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.LastSeenName).RawValue;

        EventBus.Instance.SubscribeOnUIThread(this);

        _dcsAutoConnectHandler = new DCSAutoConnectHandler();
    }

    public DCSPlayerRadioInfo DcsPlayerRadioInfo { get; }
    public DCSPlayerSideInfo PlayerCoaltionLocationMetadata { get; set; }

    // Timestamp the last UDP Game GUI broadcast was received from DCS, used for determining active game connection
    public long DcsGameGuiLastReceived { get; set; }

    // Timestamp the last UDP Export broadcast was received from DCS, used for determining active game connection
    public long DcsExportLastReceived { get; set; }

    // Timestamp for the last time 
    public long LotATCLastReceived { get; set; }

    //store radio channels here?
    public PresetChannelsViewModel[] FixedChannels { get; }

    public long LastSent { get; set; }

    public long LastPostionCoalitionSent { get; set; }

    public RadioSendingState RadioSendingState { get; set; }
    public RadioReceivingState[] RadioReceivingState { get; }
    
    public ConcurrentDictionary<uint,uint> InstructorIntercomUnits { get; } = new();

    public bool IsConnected
    {
        get => isConnected;
        set
        {
            isConnected = value;
            NotifyPropertyChanged();
        }
    }

    public bool IsVoipConnected
    {
        get => isVoipConnected;
        set
        {
            isVoipConnected = value;
            NotifyPropertyChanged();
        }
    }

    public string ShortGUID { get; }

    public bool IsConnectionErrored
    {
        get => isConnectionErrored;
        set
        {
            isConnectionErrored = value;
            NotifyPropertyChanged();
        }
    }

    // Indicates the user's desire to be in External Awacs Mode or not
    public bool ExternalAWACSModelSelected { get; set; }

    // Indicates whether we are *actually* connected in External Awacs Mode
    // Used by the Name and Password related UI elements to determine if they are editable or not
    public bool ExternalAWACSModeConnected
    {
        get
        {
            var EamEnabled = SyncedServerSettings.Instance.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE);
            return IsConnected && EamEnabled && ExternalAWACSModelSelected && !IsGameExportConnected;
        }
    }

    public bool IsLotATCConnected => LotATCLastReceived >= DateTime.Now.Ticks - 50000000;

    public bool IsGameGuiConnected => DcsGameGuiLastReceived >= DateTime.Now.Ticks - 100000000;

    public bool IsGameExportConnected => DcsExportLastReceived >= DateTime.Now.Ticks - 100000000;

    // Indicates an active game connection has been detected (1 tick = 100ns, 100000000 ticks = 10s stale timer), not updated by EAM
    public bool IsGameConnected => IsGameGuiConnected && IsGameExportConnected;

    public string LastSeenName
    {
        get
        {
            var name = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.LastSeenName).RawValue;
            if (string.IsNullOrEmpty(name))
            {
                name = "SRS Client";
            }

            return name;
        }
        set => GlobalSettingsStore.Instance.SetClientSetting(GlobalSettingsKeys.LastSeenName, value, true);
    }

    public VAICOMMessageWrapper InhibitTX { get; set; } = new(); //used to temporarily stop PTT for VAICOM

    public static ClientStateSingleton Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ClientStateSingleton();
                }

            return _instance;
        }
    }

    public int IntercomOffset { get; set; }

    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        IsConnected = message.Connected;

        if (!message.Connected && message.Error != TCPClientStatusMessage.ErrorCode.USER_DISCONNECTED)
        {
            IsConnectionErrored = true;
        }
        else
        {
            IsConnectionErrored = false;
        }

        return Task.CompletedTask;
    }

    public bool ShouldUseLotATCPosition()
    {
        if (!IsLotATCConnected) return false;

        if (IsGameExportConnected)
            if (DcsPlayerRadioInfo.inAircraft)
                return false;

        return true;
    }

    public void ClearPositionsIfExpired()
    {
        //not game or Lotatc - clear it!
        if (!IsLotATCConnected && !IsGameExportConnected)
            PlayerCoaltionLocationMetadata.LngLngPosition = new LatLngPosition();
    }

    public void UpdatePlayerPosition(LatLngPosition latLngPosition)
    {
        PlayerCoaltionLocationMetadata.LngLngPosition = latLngPosition;
        DcsPlayerRadioInfo.latLng = latLngPosition;
    }


    public int ClientsOnFreq(double freq, Modulation modulation)
    {
        if (!SyncedServerSettings.Instance.GetSettingAsBool(ServerSettingsKeys.SHOW_TUNED_COUNT)) return 0;
        var currentClientPos = PlayerCoaltionLocationMetadata;
        var currentUnitId = DcsPlayerRadioInfo.unitId;
        var coalitionSecurity =
            SyncedServerSettings.Instance.GetSettingAsBool(ServerSettingsKeys.COALITION_AUDIO_SECURITY);
        var globalFrequencies = SyncedServerSettings.Instance.GlobalFrequencies;
        var global = globalFrequencies.Contains(freq);
        var count = 0;

        foreach (var client in ConnectedClientsSingleton.Instance.Clients)
            if (!client.Key.Equals(ShortGUID))
                // check that either coalition radio security is disabled OR the coalitions match
                if (global || !coalitionSecurity || client.Value.Coalition == currentClientPos.side)
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
                        if (receivingRadio != null) count++;
                    }
                }

        return count;
    }

    public void Close()
    {
        _dcsAutoConnectHandler?.Stop();
    }
}