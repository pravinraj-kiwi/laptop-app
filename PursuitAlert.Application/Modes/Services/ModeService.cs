using Newtonsoft.Json;
using Prism.Events;
using PursuitAlert.Domain.Configuration.Events;
using PursuitAlert.Domain.Configuration.Models;
using PursuitAlert.Domain.Device.Events;
using PursuitAlert.Domain.Device.Payloads.Events;
using PursuitAlert.Domain.Modes.Events;
using PursuitAlert.Domain.Modes.Models;
using PursuitAlert.Domain.Modes.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PursuitAlert.Application.Modes.Services
{
    public class ModeService : IModeService, IDisposable
    {
        #region Fields

        private const string PinDrop = "PinDrop";

        private readonly IEventAggregator _eventAggregator;

        private readonly object ModeLock = new object();

        private List<Mode> ActivatedModes = new List<Mode>();

        private bool ModeIsCanceling;

        #endregion Fields

        #region Properties

        public Mode ActivatingMode { get; private set; }

        public bool BlockInputProcessing { get; private set; }

        public List<Mode> ModeConfiguration { get; private set; }

        public bool PatrolModeActive { get; private set; } = false;

        #endregion Properties

        #region Constructors

        public ModeService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            SetEventSubscriptions();
        }

        #endregion Constructors

        #region Methods

        public void SetSwitchMapping(List<Mode> modes)
        {
            Log.Verbose("Setting switch mapping: {0}", JsonConvert.SerializeObject(modes));
            lock (ModeLock)
            {
                ModeConfiguration = modes;
            }
            Log.Debug("Switch mapping set: {0}", JsonConvert.SerializeObject(modes));
        }

        private void BlockInputForOnePress()
        {
            BlockInputProcessing = true;
        }

        private void CancelActivatingMode()
        {
            _eventAggregator.GetEvent<DelayedModeCancelRequestedEvent>().Publish(ActivatingMode);
            ModeIsCanceling = true;
        }

        private void DisengageMode(Mode existingMode)
        {
            Log.Debug("Disengaging mode {0}", existingMode.Name);
            var modeChange = new ModeChangeEventArgs();
            ActivatedModes.Remove(existingMode);
            modeChange.ChangeType = ModeChangeType.ModeDisengaged;
            modeChange.NewMode = ActivatedModes.LastOrDefault();
            modeChange.OriginalMode = existingMode;
            modeChange.ActivatedModes = ActivatedModes;
            _eventAggregator.GetEvent<ModeChangeEvent>().Publish(modeChange);
        }

        private void EngageMode(Mode newMode)
        {
            Log.Debug("Engaging mode {0}", newMode.Name);
            var modeChange = new ModeChangeEventArgs();
            modeChange.OriginalMode = ActivatedModes.LastOrDefault();
            ActivatedModes.Add(newMode);
            modeChange.ChangeType = ModeChangeType.ModeEngaged;
            modeChange.NewMode = newMode;
            modeChange.ActivatedModes = ActivatedModes;
            _eventAggregator.GetEvent<ModeChangeEvent>().Publish(modeChange);
        }

        private void HandleDelayedModeCanceled(Mode mode)
        {
            ModeIsCanceling = false;
            ActivatingMode = null;
            Log.Debug("{0} has been canceled", mode.Name);
        }

        private void HandleDelayedModeEngaged(Mode newMode)
        {
            // Set that there is no being in the process of being activated anymore
            Log.Debug("{0} has been engaged", newMode.Name);
            ActivatingMode = null;
            EngageMode(newMode);
        }

        private void HandleRetrievedConfiguration(DeviceConfigurationRetrievedEventArgs newConfig)
        {
            // Compare each new mode with the existing mode and determine if there have been any changes
            var modesHaveBeenUpdated = false;
            foreach (var retrievedMode in newConfig.Modes)
            {
                var existingMode = ModeConfiguration.FirstOrDefault(m => m.Name.Equals(retrievedMode.Name, StringComparison.InvariantCultureIgnoreCase));
                if (retrievedMode != existingMode)
                {
                    // We found a mode that has different properties than the one in memory, modes
                    // have been updated. No need to continue checking.
                    modesHaveBeenUpdated = true;
                    break;
                }
            }

            if (modesHaveBeenUpdated)
            {
                Log.Debug("Updating mode configuration to version acquired at: {0}", newConfig.RetrievedAt.ToLocalTime().ToShortTimeString());
                SetSwitchMapping(newConfig.Modes);
                Log.Debug("Switch mapping updated successfully.");

                _eventAggregator.GetEvent<DeviceConfigurationUpdatedEvent>().Publish(newConfig);
            }
        }

        private void HandleSwitchChange(int switchNumber)
        {
            if (BlockInputProcessing)
            {
                Log.Debug("Button press {0} will not be processed as a mode activation because input processing is currently being blocked.", switchNumber);
                BlockInputProcessing = false;
                return;
            }

            Mode newMode = null;
            lock (ModeLock)
            {
                newMode = ModeConfiguration.FirstOrDefault(m => m.ButtonPosition == switchNumber);
            }

            // No matching mode was found. This is an unmapped button
            if (newMode == null)
            {
                Log.Warning("Unrecognized switch activation: {0}", switchNumber);
                _eventAggregator.GetEvent<UnmappedSwitchActivatedEvent>().Publish(switchNumber);
                return;
            }

            // If a button has been pressed while a mode is activating, count this as an attempt to
            // cancel the activating mode
            if (ActivatingMode != null)
            {
                // Make sure the mode isn't already canceling to avoid duplicate button presses
                if (ModeIsCanceling)
                {
                    Log.Debug("{0} is currently canceling. Button press is ignored until the cancelation is complete.", ActivatingMode.Name);
                }
                else
                {
                    Log.Debug("{0} is currently activating. Canceling request for {0}.", ActivatingMode.Name);
                    CancelActivatingMode();
                }
                return;
            }

            // Handle a delayed mode
            if (newMode.IncidentDelay != 0)
            {
                // If the mode isn't already activated, start the countdown
                if (!ActivatedModes.Contains(newMode))
                {
                    Log.Debug("{0} with a delay of {1}s has been requested", newMode.Name, newMode.IncidentDelay);
                    _eventAggregator.GetEvent<DelayedModeRequestedEvent>().Publish(newMode);
                    ActivatingMode = newMode;
                    return;
                }
            }

            // Handle a pin drop
            if (newMode.Name.Equals(PinDrop))
            {
                Log.Debug("Pin Drop (switch {0}) switch activated", switchNumber);
                _eventAggregator.GetEvent<PinDroppedEvent>().Publish(newMode);
                return;
            }

            // Handle mode toggle
            if (ActivatedModes.Contains(newMode))
                DisengageMode(newMode);
            else
                EngageMode(newMode);
        }

        private void SetEventSubscriptions()
        {
            _eventAggregator.GetEvent<DelayedModeEngagedEvent>().Subscribe(HandleDelayedModeEngaged);
            _eventAggregator.GetEvent<DelayedModeCanceledEvent>().Subscribe(HandleDelayedModeCanceled);
            _eventAggregator.GetEvent<DeviceSwitchActivatedEvent>().Subscribe(HandleSwitchChange);
            _eventAggregator.GetEvent<PatrolStartedEvent>().Subscribe(() => PatrolModeActive = true);
            _eventAggregator.GetEvent<PatrolEndedEvent>().Subscribe(() => PatrolModeActive = false);

            _eventAggregator.GetEvent<DeviceSerialNumberNotFoundEvent>().Subscribe(BlockInputForOnePress);

            _eventAggregator.GetEvent<DeviceConfigurationRetrievedEvent>().Subscribe(HandleRetrievedConfiguration);
        }

        public void Dispose()
        {
            _eventAggregator.GetEvent<DelayedModeEngagedEvent>().Unsubscribe(HandleDelayedModeEngaged);
            _eventAggregator.GetEvent<DelayedModeCanceledEvent>().Unsubscribe(HandleDelayedModeCanceled);
            _eventAggregator.GetEvent<DeviceSwitchActivatedEvent>().Unsubscribe(HandleSwitchChange);
            _eventAggregator.GetEvent<PatrolStartedEvent>().Unsubscribe(() => PatrolModeActive = true);
            _eventAggregator.GetEvent<PatrolEndedEvent>().Unsubscribe(() => PatrolModeActive = false);
            _eventAggregator.GetEvent<DeviceSerialNumberNotFoundEvent>().Unsubscribe(BlockInputForOnePress);
            _eventAggregator.GetEvent<DeviceConfigurationRetrievedEvent>().Unsubscribe(HandleRetrievedConfiguration);
        }

        #endregion Methods
    }
}