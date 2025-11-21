using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.AwacsRadioOverlayWindow.InstructorMode;

public class InstructorModeViewModel: INotifyPropertyChanged, IHandle<ServerSettingsUpdatedMessage>, IHandle<EAMDisconnectMessage>, IHandle<TCPClientStatusMessage>
{
    private readonly object _aircraftIntercomModelsListLock = new();
    private ObservableCollection<AircraftIntercomModel> _aircraftIntercomModels = [];
    private int _radioId;
    
    private  DCSRadio _previousRadio;

    public InstructorModeViewModel(int radioId)
    {
        
        _radioId = radioId;
        
        ReloadCommand = new DelegateCommand(OnReload);
        DropDownClosedCommand = new DelegateCommand(DropDownClosed);
        StopInstructorModeCommand = new DelegateCommand(StopInstructorMode);
        //EventBus.Instance.SubscribeOnUIThread(this);
        Reload();
    }

    public ICommand DropDownClosedCommand { get; }

    public ICommand StopInstructorModeCommand { get; }

    public ObservableCollection<AircraftIntercomModel> AircraftIntercoms
    {
        get => _aircraftIntercomModels;
        set
        {
            _aircraftIntercomModels = value;
            BindingOperations.EnableCollectionSynchronization(_aircraftIntercomModels, _aircraftIntercomModelsListLock);
        }
    }
    public ICommand ReloadCommand { get; }

    public AircraftIntercomModel SelectedAircraftIntercom { get; set; }

    public double Max { get; set; }
    public double Min { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    private void DropDownClosed()
    {
       //TODO handle this
       if (SelectedAircraftIntercom != null)
       {
           SelectAircraftIntercom(SelectedAircraftIntercom);
       }
       else
       {
           StopInstructorMode();
       }
    }

    public void Reload()
    {
        // StopInstructorMode();
        // SelectedAircraftIntercom = null;
        
        AircraftIntercoms.Clear();
        
        var radios = ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios;
        
        var aircraftIntercomModels = new Dictionary<uint,AircraftIntercomModel>();

        var guid = ClientStateSingleton.Instance.ShortGUID;
        foreach (var client in ConnectedClientsSingleton.Instance.Clients)
        {
            //copying them to avoid race conditions
            //as they can change during iteration
            var radioInfo = client.Value.RadioInfo;
            if (radioInfo == null)
                continue;

            var unitId = radioInfo.unitId;
            
            if(unitId <=0 || client.Value.ClientGuid == guid)
                continue;
            
            if (aircraftIntercomModels.TryGetValue(unitId, out var lookup))
            {
                lookup.PilotNames.Add(client.Value.Name);
            }
            else
            {
                var aircraftModel = new AircraftIntercomModel()
                {
                    UnitId = unitId,
                    PilotNames = [client.Value.Name],
                    AircraftType = radioInfo.unit
                };
                aircraftIntercomModels[unitId] = aircraftModel;
            }
        }

        AircraftIntercoms.Add(new AircraftIntercomModel()
        {
            AircraftType = "None",
            UnitId = 0,
            PilotNames = ["Disabled"]
        });
        foreach (var model in aircraftIntercomModels.Values)
        {
            AircraftIntercoms.Add(model);
        }
    }

    private void OnReload()
    {
        Reload();
    }

    private void SelectAircraftIntercom(AircraftIntercomModel aircraftIntercomModel)
    {
        EventBus.Instance.SubscribeOnUIThread(this);
        
        if (aircraftIntercomModel.UnitId == 0)
        {
            StopInstructorMode();
            return;
        }
        var radios = ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios;

        var radio = radios[_radioId].DeepClone();

        if (_previousRadio == null)
        {
            _previousRadio = radio.DeepClone();
        }
        
        radio.IntercomUnitId = aircraftIntercomModel.UnitId;
        radio.rtMode = DCSRadio.RetransmitMode.DISABLED;
        radio.modulation = Modulation.INTERCOM;
        radio.freqMode = DCSRadio.FreqMode.COCKPIT;
        radio.guardFreqMode = DCSRadio.FreqMode.COCKPIT;
        radio.secFreq = -1;
        radio.volMode = DCSRadio.VolumeMode.OVERLAY;
        radio.freq = 10000;
        radio.freqMin = 1000;
        radio.freqMax = 100000;
        radio.channel = -1;
        radio.model = "INTERCOM";
        radio.name = "INTERCOM";
        radio.encMode = DCSRadio.EncryptionMode.NO_ENCRYPTION;
        radio.rxOnly = false;
        
        EventBus.Instance.PublishOnCurrentThreadAsync(new InstructorModeMessage()
        {
            RadioId = _radioId,
            Radio = radio.DeepClone()
        });
    }

    public void StopInstructorMode()
    {
        if (_previousRadio == null)
            return;
        
        var radios = ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios;
        
        radios[_radioId] = _previousRadio.DeepClone();
 
        EventBus.Instance.PublishOnCurrentThreadAsync(new InstructorModeMessage()
        {
            RadioId = _radioId,
            Radio = _previousRadio.DeepClone()
        });
        
        _previousRadio = null;
        
        EventBus.Instance.Unsubscribe(this);
    }
    
    public Task HandleAsync(ServerSettingsUpdatedMessage message, CancellationToken cancellationToken)
    {
        if (!SyncedServerSettings.Instance.GetSettingAsBool(ServerSettingsKeys.ALLOW_INSTRUCTOR_MODE))
        {
            StopInstructorMode();
        }
        return Task.CompletedTask;
    }

    public Task HandleAsync(EAMDisconnectMessage message, CancellationToken cancellationToken)
    {
        StopInstructorMode();
        return Task.CompletedTask;
    }

    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        if (!message.Connected)
        {
            StopInstructorMode();
        }
        return Task.CompletedTask;
    }
}
