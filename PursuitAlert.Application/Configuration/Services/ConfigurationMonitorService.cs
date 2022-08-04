using Newtonsoft.Json;
using Prism.Events;
using PursuitAlert.Domain.Application.Events;
using PursuitAlert.Domain.Configuration.Events;
using PursuitAlert.Domain.Configuration.Models;
using PursuitAlert.Domain.Configuration.Services;
using PursuitAlert.Domain.Device.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace PursuitAlert.Application.Configuration.Services
{
    public class ConfigurationMonitorService : IConfigurationMonitorService
    {
        #region Fields

        private const string LastConfigurationParameterName = "lastConfigurationDate";

        private readonly IEventAggregator _eventAggregator;

        private DateTime _lastConfigurationRetrievedUTC;

        private HttpClient HttpClient;

        private int ListenerInterval;

        private Timer Timer;

        private string UpdateUrl;

        #endregion Fields

        #region Constructors

        public ConfigurationMonitorService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<ApplicationExitEvent>().Subscribe(HandleApplicationExit);
            HttpClient = HttpClientFactory.Create();
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Check for new configurations at the given URL at the interval specified.
        /// </summary>
        /// <param name="updateUrl">
        /// The location of the configuration. HTTP GET requests are only supported.
        /// </param>
        /// <param name="listenerInterval">
        /// The interval at which the URL will be polled. Default: 1 hour (3600000ms)
        /// </param>
        /// <param name="lastConfigurationUTC">The time the last configuration was updated.</param>
        public void ListenForChanges(string updateUrl, int listenerInterval = 3600000, DateTime? lastConfigurationUTC = null)
        {
            UpdateUrl = updateUrl;

            if (lastConfigurationUTC.HasValue)
                _lastConfigurationRetrievedUTC = lastConfigurationUTC.Value;

            ListenerInterval = listenerInterval;

            Log.Debug("Starting configuration listener timer with a {0}ms interval", listenerInterval);
            _eventAggregator.GetEvent<ConfigurationMonitorStartedEvent>().Publish();

            Log.Debug("Starting device listener");
            if (Timer == null)
                Timer = new Timer(new TimerCallback(Listen), null, 0, ListenerInterval);
            else
                Timer.Change(listenerInterval, Timeout.Infinite);

            if (!_eventAggregator.GetEvent<DeviceDisconnectedEvent>().Contains(HandleDeviceDisconnected))
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Subscribe(HandleDeviceDisconnected);
        }

        public void StopListening(string reason)
        {
            Log.Debug("Configuration stop requested. (Reason: {0})", reason);
            if (Timer == null)
                Timer = new Timer(new TimerCallback(Listen), null, Timeout.Infinite, Timeout.Infinite);
            else
                try
                {
                    Timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                catch (ObjectDisposedException)
                {
                    Log.Debug("Attempted to stop the configuration listener timer, but the timer was disposed.");
                }

            if (reason == "Device disconnected.")
                _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleDeviceDisconnected);
        }

        private void HandleApplicationExit(int exitCode)
        {
            StopListening("Application shutting down");

            if (Timer != null)
                Timer.Dispose();

            if (HttpClient != null)
                HttpClient.Dispose();

            _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Unsubscribe(HandleDeviceDisconnected);
            _eventAggregator.GetEvent<ApplicationExitEvent>().Unsubscribe(HandleApplicationExit);
        }

        private void HandleDeviceDisconnected() => StopListening("Device disconnected");

        private void Listen(object state)
        {
            try
            {
                Log.Verbose("Retrieving configuration from {0}", UpdateUrl);
                var response = HttpClient.GetAsync($"{UpdateUrl}?{LastConfigurationParameterName}={_lastConfigurationRetrievedUTC:o}").Result;

                // TODO: Remove this when done with the onboarding workflow
                try
                {
                    response = response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException)
                {
                    return;
                }

                var rawResponse = response.Content.ReadAsStringAsync().Result;
                var updateResponse = JsonConvert.DeserializeObject<JurisdictionConfigurationResponse>(rawResponse);

                Log.Verbose("Configuration response: {0}", rawResponse);
                Log.Debug("Device configuration retrieved from the server");
                var newConfigurationNotification = new DeviceConfigurationRetrievedEventArgs
                {
                    RetrievedAt = DateTime.UtcNow,
                    FromURL = UpdateUrl,
                    Modes = updateResponse.Configuration.Modes
                };
                _lastConfigurationRetrievedUTC = DateTime.UtcNow;
                _eventAggregator.GetEvent<DeviceConfigurationRetrievedEvent>().Publish(newConfigurationNotification);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve device configuration");
                _eventAggregator.GetEvent<ConfigurationRetrievalFailedEvent>().Publish(ex);
            }
        }

        #endregion Methods
    }
}