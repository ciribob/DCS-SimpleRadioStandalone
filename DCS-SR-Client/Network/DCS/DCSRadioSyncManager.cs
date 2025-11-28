using System;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.LotATC;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.VAICOM;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using NLog;
using LogManager = NLog.LogManager;

/**
Keeps radio information in Sync Between DCS and

**/

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;

public class DCSRadioSyncManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly DispatcherTimer _clearRadio;

    private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;

    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
    private readonly DCSGameGuiHandler _dcsGameGuiHandler;
    private readonly DCSRadioSyncHandler _dcsRadioSyncHandler;

    private readonly DCSLineOfSightHandler _lineOfSightHandler;
    private readonly LotATCSyncHandler _lotATCSyncHandler;
    private readonly UDPCommandHandler _udpCommandHandler;

    private VAICOMSyncHandler _vaicomHandler;

    public DCSRadioSyncManager(string guid)
    {
        IsListening = false;
        _lineOfSightHandler = new DCSLineOfSightHandler(guid);
        _udpCommandHandler = new UDPCommandHandler();
        _dcsGameGuiHandler = new DCSGameGuiHandler();
        _dcsRadioSyncHandler = new DCSRadioSyncHandler();
        _vaicomHandler = new VAICOMSyncHandler();
        _lotATCSyncHandler = new LotATCSyncHandler(guid);

        _clearRadio = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
            { Interval = TimeSpan.FromSeconds(1) };
        _clearRadio.Tick += CheckIfRadioIsStale;
    }

    public bool IsListening { get; private set; }


    private void CheckIfRadioIsStale(object sender, EventArgs e)
    {
        if (!_clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
            //check if we've had an update
            if (_clientStateSingleton.DcsPlayerRadioInfo.LastUpdate > 0)
            {
                _clientStateSingleton.PlayerCoaltionLocationMetadata.Reset();
                _clientStateSingleton.DcsPlayerRadioInfo.Reset();
                _clientStateSingleton.LastSent = 0;

                //TODO handle this

                Logger.Info("Reset Radio state - no longer connected");
            }
    }

    public void Start()
    {
        DcsListener();
        IsListening = true;
    }


    private void DcsListener()
    {
        _dcsRadioSyncHandler.Start();
        _dcsGameGuiHandler.Start();
        _lineOfSightHandler.Start();
        _udpCommandHandler.Start();
        _clearRadio.Start();
        _vaicomHandler.Start();
        _lotATCSyncHandler.Start();
    }

    public void Stop()
    {
        IsListening = false;

        _clearRadio.Stop();
        _dcsRadioSyncHandler.Stop();
        _dcsGameGuiHandler.Stop();
        _lineOfSightHandler.Stop();
        _udpCommandHandler.Stop();
        _vaicomHandler.Stop();
        _lotATCSyncHandler.Stop();
    }
}