using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Properties;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl.Model;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Microsoft.Win32;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

public class ClientSettingsViewModel : PropertyChangedBaseClass, IHandle<NewUnitEnteredMessage>
{
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();


    public ClientSettingsViewModel()
    {
        SetSRSPathCommand = new DelegateCommand(() =>
        {
            DirectoryInfo di = new DirectoryInfo(Directory.GetCurrentDirectory());
            Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRPathStandalone",
                di.Parent);

            MessageBox.Show(Application.Current.MainWindow,
                Resources.MsgBoxSetSRSPathText + di.Parent,
                Resources.MsgBoxSetSRSPath,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
        ResetOverlayCommand = new DelegateCommand(() =>
        {
            EventBus.Instance.PublishOnUIThreadAsync(new CloseRadioOverlayMessage());

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, 122);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, 270);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioOpacity, 1.0);
        });

        CreateProfileCommand = new DelegateCommand(() =>
        {
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
            {
                if (name.Trim().Length > 0)
                {
                    _globalSettings.ProfileSettingsStore.AddNewProfile(name);

                    NotifyPropertyChanged(nameof(AvailableProfiles));
                    ReloadSettings();
                }
            });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = Application.Current.MainWindow;
            inputProfileWindow.ShowDialog();
        });

        CopyProfileCommand = new DelegateCommand(() =>
        {
            var current = _globalSettings.ProfileSettingsStore.CurrentProfileName;
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
            {
                if (name.Trim().Length > 0)
                {
                    _globalSettings.ProfileSettingsStore.CopyProfile(current, name);
                    NotifyPropertyChanged(nameof(AvailableProfiles));
                    ReloadSettings();
                }
            });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = Application.Current.MainWindow;
            inputProfileWindow.ShowDialog();
        });

        RenameProfileCommand = new DelegateCommand(() =>
        {
            var current = _globalSettings.ProfileSettingsStore.CurrentProfileName;
            if (current.Equals("default"))
            {
                MessageBox.Show(Application.Current.MainWindow,
                    "Cannot rename the default input!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var oldName = current;
                var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
                {
                    if (name.Trim().Length > 0)
                    {
                        _globalSettings.ProfileSettingsStore.RenameProfile(oldName, name);
                        SelectedProfile = _globalSettings.ProfileSettingsStore.CurrentProfileName;
                        NotifyPropertyChanged(nameof(AvailableProfiles));
                        ReloadSettings();
                    }
                }, true, oldName);
                inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                inputProfileWindow.Owner = Application.Current.MainWindow;
                inputProfileWindow.ShowDialog();
            }
        });

        DeleteProfileCommand = new DelegateCommand(() =>
        {
            var current = _globalSettings.ProfileSettingsStore.CurrentProfileName;

            if (current.Equals("default"))
            {
                MessageBox.Show(Application.Current.MainWindow,
                    "Cannot delete the default input!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var result = MessageBox.Show(Application.Current.MainWindow,
                    $"Are you sure you want to delete {current} ?",
                    "Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _globalSettings.ProfileSettingsStore.RemoveProfile(current);
                    SelectedProfile = _globalSettings.ProfileSettingsStore.CurrentProfileName;
                    NotifyPropertyChanged(nameof(AvailableProfiles));
                    ReloadSettings();
                }
            }
        });

        EventBus.Instance.SubscribeOnUIThread(this);
    }

    public ICommand SetSRSPathCommand { get; set; }

    public ICommand ResetOverlayCommand { get; set; }

    public ICommand CreateProfileCommand { get; set; }
    public ICommand CopyProfileCommand { get; set; }
    public ICommand RenameProfileCommand { get; set; }
    public ICommand DeleteProfileCommand { get; set; }

    /**
         * Global Settings
         */

    public bool ExpandInputDevices
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ExpandControls);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.ExpandControls, value);
            NotifyPropertyChanged();
            MessageBox.Show(
                Resources.MsgBoxRestartExpandText,
                Resources.MsgBoxRestart, MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public bool AutoConnectEnabled
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnect);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoConnect, value);
            NotifyPropertyChanged();
        }
    }

    public bool AutoConnectPrompt
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoConnectPrompt, value);
            NotifyPropertyChanged();
        }
    }

    public bool AutoConnectMismatchPrompt
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectMismatchPrompt);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoConnectMismatchPrompt, value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioOverlayTaskbarItem
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RadioOverlayTaskbarHide, value);
            NotifyPropertyChanged();
        }
    }

    public bool RefocusDCS
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RefocusDCS);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RefocusDCS, value);
            NotifyPropertyChanged();
        }
    }

    public bool ShowTransmitterName
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ShowTransmitterName);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.ShowTransmitterName, value);
            NotifyPropertyChanged();
        }
    }

    public bool DisallowedAudioTone
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.DisallowedAudioTone);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.DisallowedAudioTone, value);
            NotifyPropertyChanged();
        }
    }

    public bool DisableExpansionRadios
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.DisableExpansionRadios);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.DisableExpansionRadios,
                value);
            NotifyPropertyChanged();
        }
    }
    
    public List<string> ServerPresetConfigurations => ProfileSettingsStore.ServerPresetSettings;

    public string SelectedServerPresetConfiguration
    {
        set
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSettingString(
                ProfileSettingsKeys.ServerPresetSelection, value);
            NotifyPropertyChanged();
            EventBus.Instance.PublishOnUIThreadAsync(new ServerSettingsPresetsSettingChangedMessage());
        }
        get =>
            GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingString(ProfileSettingsKeys
                .ServerPresetSelection);
    }

    public bool ServerEAMRadioPresetEnabled
    {
        get =>   GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AllowServerEAMRadioPreset);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.AllowServerEAMRadioPreset,
                value);
            NotifyPropertyChanged();
        }
    }
    
    public bool VOXEnabled
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.VOX);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.VOX, value);
            NotifyPropertyChanged();
        }
    }

    public int VOXMinimimumTXTime
    {
        get => _globalSettings.GetClientSettingInt(GlobalSettingsKeys.VOXMinimumTime);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.VOXMinimumTime, value);
            NotifyPropertyChanged();
        }
    }


    public int VOXMode
    {
        get => _globalSettings.GetClientSettingInt(GlobalSettingsKeys.VOXMode);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.VOXMode, value);
            NotifyPropertyChanged();
        }
    }

    public double VOXMinimumRMS
    {
        get => _globalSettings.GetClientSettingDouble(GlobalSettingsKeys.VOXMinimumDB);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.VOXMinimumDB, value);
            NotifyPropertyChanged();
        }
    }

    public bool AllowTransmissionsRecording
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AllowRecording);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AllowRecording, value);
            NotifyPropertyChanged();
        }
    }

    public bool RecordTransmissions
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RecordAudio);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RecordAudio, value);
            NotifyPropertyChanged();
        }
    }

    public int RecordingQuality
    {
        get => int.Parse(_globalSettings.GetClientSetting(GlobalSettingsKeys.RecordingQuality).StringValue.TrimStart('V'));
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RecordingQuality, $"V{value}");
            NotifyPropertyChanged();
        }
    }

    public bool SingleFileMixdown
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.SingleFileMixdown);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.SingleFileMixdown, value);
            NotifyPropertyChanged();
        }
    }

    public IReadOnlyList<string> RecordingFormats => AudioRecordingManager.Instance.AvailableFormats;

    public string SelectedRecordingFormat
    {
        get => _globalSettings.GetClientSetting(GlobalSettingsKeys.RecordingFormat).StringValue;
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RecordingFormat, value);
            NotifyPropertyChanged();
        }
    }

    public bool AutoSelectInputProfile
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoSelectSettingsProfile);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoSelectSettingsProfile, value);
            NotifyPropertyChanged();
        }
    }

    public bool CheckForBetaUpdates
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.CheckForBetaUpdates, value);
            NotifyPropertyChanged();
        }
    }

    public bool RequireAdminToggle
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RequireAdmin);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RequireAdmin, value);

            MessageBox.Show(Application.Current.MainWindow,
                Resources.MsgBoxAdminText,
                Resources.MsgBoxAdmin, MessageBoxButton.OK, MessageBoxImage.Warning);

            NotifyPropertyChanged();
        }
    }

    public bool VAICOMTXInhibitEnabled
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.VAICOMTXInhibitEnabled);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.VAICOMTXInhibitEnabled, value);
            NotifyPropertyChanged();
        }
    }

    public bool AllowXInputController
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AllowXInputController);
        set
        {
            MessageBox.Show(
                Resources.MsgBoxRestartXInputText,
                Resources.MsgBoxRestart, MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AllowXInputController, value);
            NotifyPropertyChanged();
        }
    }

    public bool MinimiseToTray
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.MinimiseToTray, value);
            NotifyPropertyChanged();
        }
    }

    public bool StartMinimised
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.StartMinimised);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.StartMinimised, value);
            NotifyPropertyChanged();
        }
    }

    public bool MicAGC
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AGC);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AGC, value);
            NotifyPropertyChanged();
        }
    }

    public bool MicDenoise
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.Denoise);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.Denoise, value);
            NotifyPropertyChanged();
        }
    }

    public bool PlayConnectionSounds
    {
        get => _globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds);
        set
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.PlayConnectionSounds, value);
            NotifyPropertyChanged();
        }
    }

    /**
         * Profile Settings
         */

    public bool RadioSwitchIsPTT
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT, value);
            NotifyPropertyChanged();
        }
    }

    public bool AutoSelectChannel
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AutoSelectPresetChannel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.AutoSelectPresetChannel,
                value);
            NotifyPropertyChanged();
        }
    }

    public float PTTReleaseDelay
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.PTTReleaseDelay);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.PTTReleaseDelay, value);
            NotifyPropertyChanged();
        }
    }

    public float PTTStartDelay
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.PTTStartDelay);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.PTTStartDelay, value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioRxStart
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioRxEnd
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioTxStart
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioTxEnd
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool AlwaysAllowHotasControls
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AlwaysAllowHotasControls);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.AlwaysAllowHotasControls,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool AlwaysAllowTransponderOverlayControls
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AlwaysAllowTransponderOverlay);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.AlwaysAllowTransponderOverlay,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool AllowDCSPTT
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AllowDCSPTT);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.AllowDCSPTT,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool AllowRotaryIncrement
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.RotaryStyleIncrement);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RotaryStyleIncrement,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioEncryptionEffectsToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.RadioEncryptionEffects);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioMIDSToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.MIDSRadioEffect);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool HQEffectToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.HAVEQUICKTone);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.HAVEQUICKTone,
                value);
            NotifyPropertyChanged();
        }
    }


    public float HQEffectVolume
    {
        get =>
            (float)((_globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.HQToneVolume)
                     / double.Parse(
                         ProfileSettingsStore.DefaultSettingsProfileSettings
                             [ProfileSettingsKeys.HQToneVolume.ToString()], CultureInfo.InvariantCulture)) * 100.0f);
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.HQToneVolume.ToString()],
                CultureInfo.InvariantCulture);

            var vol = orig * (value / 100.0f);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.HQToneVolume, (float)vol);
            NotifyPropertyChanged();
        }
    }

    public bool AmbientEffectToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AmbientCockpitNoiseEffect);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.AmbientCockpitNoiseEffect,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool AmbientEffectIntercomToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AmbientCockpitIntercomNoiseEffect);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(
                ProfileSettingsKeys.AmbientCockpitIntercomNoiseEffect,
                value);
            NotifyPropertyChanged();
        }
    }


    public float AmbientEffectVolume
    {
        get => (float)((_globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys
                            .AmbientCockpitNoiseEffectVolume)
                        / double.Parse(
                            ProfileSettingsStore.DefaultSettingsProfileSettings[
                                ProfileSettingsKeys.AmbientCockpitNoiseEffectVolume.ToString()],
                            CultureInfo.InvariantCulture)) * 100.0f);
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[
                    ProfileSettingsKeys.AmbientCockpitNoiseEffectVolume.ToString()], CultureInfo.InvariantCulture);

            var vol = orig * (value / 100.0f);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(
                ProfileSettingsKeys.AmbientCockpitNoiseEffectVolume, (float)vol);
            NotifyPropertyChanged();
        }
    }

/***
 *
 */
    public List<CachedAudioEffect> RadioTransmissionStart =>
        CachedAudioEffectProvider.Instance.RadioTransmissionStart;

    public CachedAudioEffect SelectedRadioTransmissionStartEffect
    {
        set
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSettingString(
                ProfileSettingsKeys.RadioTransmissionStartSelection, value.FileName);
            NotifyPropertyChanged();
        }
        get => CachedAudioEffectProvider.Instance.SelectedRadioTransmissionStartEffect;
    }

    public List<CachedAudioEffect> RadioTransmissionEnd => CachedAudioEffectProvider.Instance.RadioTransmissionEnd;

    public CachedAudioEffect SelectedRadioTransmissionEndEffect
    {
        set
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSettingString(
                ProfileSettingsKeys.RadioTransmissionEndSelection, value.FileName);
            NotifyPropertyChanged();
        }
        get => CachedAudioEffectProvider.Instance.SelectedRadioTransmissionEndEffect;
    }
/***
 *
 */

/***
 *
 */
    public List<CachedAudioEffect> IntercomTransmissionStart =>
        CachedAudioEffectProvider.Instance.IntercomTransmissionStart;

    public CachedAudioEffect SelectedIntercomStartTransmitEffect
    {
        set
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSettingString(
                ProfileSettingsKeys.IntercomTransmissionStartSelection, value.FileName);
            NotifyPropertyChanged();
        }
        get => CachedAudioEffectProvider.Instance.SelectedIntercomTransmissionStartEffect;
    }

    public List<CachedAudioEffect> IntercomTransmissionEnd =>
        CachedAudioEffectProvider.Instance.IntercomTransmissionEnd;

    public CachedAudioEffect SelectedIntercomEndTransmitEffect
    {
        set
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSettingString(
                ProfileSettingsKeys.IntercomTransmissionEndSelection, value.FileName);
            NotifyPropertyChanged();
        }
        get => CachedAudioEffectProvider.Instance.SelectedIntercomTransmissionEndEffect;
    }

/***
 *
 */
    public bool RadioSoundEffects
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioEffects, value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioSoundEffectsClipping
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool NATORadioToneToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.NATOTone);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.NATOTone, value);
            NotifyPropertyChanged();
        }
    }

    public double NATORadioToneVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.NATOToneVolume.ToString()],
                CultureInfo.InvariantCulture) * 100.0f;
        set
        {
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.NATOToneVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100.0f);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public bool BackgroundRadioNoiseToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
            .RadioBackgroundNoiseEffect);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(
                ProfileSettingsKeys.RadioBackgroundNoiseEffect, value);
            NotifyPropertyChanged();
        }
    }

    public bool PerRadioModelEffects
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.PerRadioModelEffects);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.PerRadioModelEffects, value);
            NotifyPropertyChanged();
        }
    }

    public float NoiseGainDB
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.NoiseGainDB);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.NoiseGainDB, value);
            NotifyPropertyChanged();
        }
    }

    public float HFNoiseGainDB
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.HFNoiseGainDB);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.HFNoiseGainDB, value);
            NotifyPropertyChanged();
        }
    }


    /**
         * Radio Audio Balance
         */

    public float RadioChannel1
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio1Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio1Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel2
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio2Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio2Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel3
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio3Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio3Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel4
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio4Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio4Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel5
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio5Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio5Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel6
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio6Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio6Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel7
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio7Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio7Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel8
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio8Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio8Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel9
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio9Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio9Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel10
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio10Channel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio10Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float Intercom
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.IntercomChannel);
        set
        {
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.IntercomChannel, value);
            NotifyPropertyChanged();
        }
    }

    public string SelectedProfile
    {
        set
        {
            if (value != null)
            {
                _globalSettings.ProfileSettingsStore.CurrentProfileName = value;
                ReloadSettings();
            }

            NotifyPropertyChanged();
        }
        get => _globalSettings.ProfileSettingsStore.CurrentProfileName;
    }

    public List<string> AvailableProfiles
    {
        set
        {
            //do nothing
        }
        get => _globalSettings.ProfileSettingsStore.ProfileNames;
    }


    public Task HandleAsync(NewUnitEnteredMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var seat = message.Seat;
            //Handle Aircraft Name - find matching profile and select if you can
            var name = Regex.Replace(message.Unit.Trim().ToLower(), "[^a-zA-Z0-9]", "");
            //add one to seat so seat_2 is copilot
            var nameSeat = $"_{seat + 1}";

            foreach (var profileName in GlobalSettingsStore.Instance.ProfileSettingsStore
                         .ProfileNames)
            {
                //find matching seat
                var splitName = profileName.Trim().ToLowerInvariant().Split('_')
                    .First();
                if (name.StartsWith(Regex.Replace(splitName, "[^a-zA-Z0-9]", "")) &&
                    profileName.Trim().EndsWith(nameSeat))
                {
                    SelectedProfile = profileName;
                    return Task.CompletedTask;
                }
            }

            foreach (var profileName in GlobalSettingsStore.Instance.ProfileSettingsStore
                         .ProfileNames)
            {
                //find matching seat
                if (name.StartsWith(Regex.Replace(profileName.Trim().ToLower(),
                        "[^a-zA-Z0-9_]", "")))
                {
                    SelectedProfile = profileName;
                    return Task.CompletedTask;
                }
            }

            SelectedProfile = "default";
        }
        catch (Exception)
        {
        }


        return Task.CompletedTask;
    }


    private void ReloadSettings()
    {
        NotifyPropertyChanged(nameof(AutoConnectEnabled));
        NotifyPropertyChanged(nameof(AutoConnectPrompt));
        NotifyPropertyChanged(nameof(AutoConnectMismatchPrompt));
        //ResetOverlayCommand
        NotifyPropertyChanged(nameof(RadioOverlayTaskbarItem));

        NotifyPropertyChanged(nameof(RefocusDCS));
        NotifyPropertyChanged(nameof(MinimiseToTray));
        NotifyPropertyChanged(nameof(StartMinimised));
        NotifyPropertyChanged(nameof(ShowTransmitterName));

        NotifyPropertyChanged(nameof(MicAGC));
        NotifyPropertyChanged(nameof(MicDenoise));

        NotifyPropertyChanged(nameof(VOXEnabled));
        NotifyPropertyChanged(nameof(VOXMinimimumTXTime));
        NotifyPropertyChanged(nameof(VOXMode));
        NotifyPropertyChanged(nameof(VOXMinimumRMS));

        NotifyPropertyChanged(nameof(AllowTransmissionsRecording));
        NotifyPropertyChanged(nameof(RecordTransmissions));
        NotifyPropertyChanged(nameof(SingleFileMixdown));
        NotifyPropertyChanged(nameof(RecordingQuality));

        NotifyPropertyChanged(nameof(AutoSelectInputProfile));
        NotifyPropertyChanged(nameof(CheckForBetaUpdates));
        //SetSRSPathCommand
        NotifyPropertyChanged(nameof(RequireAdminToggle));
        NotifyPropertyChanged(nameof(VAICOMTXInhibitEnabled));
        NotifyPropertyChanged(nameof(ExpandInputDevices));
        NotifyPropertyChanged(nameof(AllowXInputController));
        NotifyPropertyChanged(nameof(PlayConnectionSounds));
        //TODO handle Profile list??

        NotifyPropertyChanged(nameof(RadioSwitchIsPTT));
        NotifyPropertyChanged(nameof(AutoSelectChannel));
        NotifyPropertyChanged(nameof(AlwaysAllowHotasControls));
        NotifyPropertyChanged(nameof(AlwaysAllowTransponderOverlayControls));
        NotifyPropertyChanged(nameof(AllowDCSPTT));
        NotifyPropertyChanged(nameof(AllowRotaryIncrement));
        NotifyPropertyChanged(nameof(PTTReleaseDelay));
        NotifyPropertyChanged(nameof(PTTStartDelay));
        NotifyPropertyChanged(nameof(RadioRxStart));
        NotifyPropertyChanged(nameof(RadioRxEnd));
        NotifyPropertyChanged(nameof(RadioTxStart));
        NotifyPropertyChanged(nameof(RadioTxEnd));
        NotifyPropertyChanged(nameof(SelectedRadioTransmissionStartEffect));
        NotifyPropertyChanged(nameof(SelectedRadioTransmissionEndEffect));
        NotifyPropertyChanged(nameof(SelectedIntercomStartTransmitEffect));
        NotifyPropertyChanged(nameof(SelectedIntercomEndTransmitEffect));
        NotifyPropertyChanged(nameof(RadioEncryptionEffectsToggle));
        NotifyPropertyChanged(nameof(RadioMIDSToggle));
        NotifyPropertyChanged(nameof(RadioSoundEffects));
        NotifyPropertyChanged(nameof(RadioSoundEffectsClipping));
        NotifyPropertyChanged(nameof(NATORadioToneToggle));
        NotifyPropertyChanged(nameof(NATORadioToneVolume));
        NotifyPropertyChanged(nameof(HQEffectToggle));
        NotifyPropertyChanged(nameof(HQEffectVolume));
        NotifyPropertyChanged(nameof(BackgroundRadioNoiseToggle));
        NotifyPropertyChanged(nameof(NoiseGainDB));
        NotifyPropertyChanged(nameof(HFNoiseGainDB));

        NotifyPropertyChanged(nameof(AmbientEffectToggle));
        NotifyPropertyChanged(nameof(AmbientEffectIntercomToggle));
        NotifyPropertyChanged(nameof(AmbientEffectVolume));

        NotifyPropertyChanged(nameof(RadioChannel1));
        NotifyPropertyChanged(nameof(RadioChannel2));
        NotifyPropertyChanged(nameof(RadioChannel3));
        NotifyPropertyChanged(nameof(RadioChannel4));
        NotifyPropertyChanged(nameof(RadioChannel5));
        NotifyPropertyChanged(nameof(RadioChannel6));
        NotifyPropertyChanged(nameof(RadioChannel7));
        NotifyPropertyChanged(nameof(RadioChannel8));
        NotifyPropertyChanged(nameof(RadioChannel9));
        NotifyPropertyChanged(nameof(RadioChannel10));
        NotifyPropertyChanged(nameof(Intercom));
        
        NotifyPropertyChanged(nameof(ServerPresetConfigurations));

        //TODO send message to tell input to reload!
        //TODO pick up in inputhandler that settings have changed?
        EventBus.Instance.PublishOnUIThreadAsync(new ProfileChangedMessage());
    }
}