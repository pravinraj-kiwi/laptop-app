using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Infrastructure.API;
using PursuitAlert.Client.Infrastructure.API.Models;
using PursuitAlert.Client.Properties;
using PursuitAlert.Client.Resources.Colors;
using PursuitAlert.Client.Services.Device.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PursuitAlert.Client.Dialogs.SettingsDialog
{
    public class SettingsDialogViewModel : BindableBase, IDialogAware
    {
        #region Properties

        public DelegateCommand Close { get; private set; }

        public bool Connected { get; private set; }

        public bool IsInEditMode
        {
            get => _isInEditMode;
            set => SetProperty(ref _isInEditMode, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        public bool IsSearchingOrganization
        {
            get => _isSearchingOrganization;
            set => SetProperty(ref _isSearchingOrganization, value);
        }

        public bool IsSearchingVehicle
        {
            get => _isSearchingVehicle;
            set => SetProperty(ref _isSearchingVehicle, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public string Officer
        {
            get => _officer;
            set
            {
                SetProperty(ref _officer, value);
                ValidationErrors = string.Empty;
            }
        }

        public string OrganizationCode
        {
            get => _organizationCode;
            set
            {
                SetProperty(ref _organizationCode, value);
                SearchOrganization(_organizationCode);
            }
        }

        public bool OrganizationCodeIsValid { get; private set; }

        public Infrastructure.API.Models.Node OrganizationFromServer { get; private set; }

        public string OrganizationName
        {
            get => _organizationName;
            set => SetProperty(ref _organizationName, value);
        }

        public SolidColorBrush OrganizationNameColor
        {
            get => _organizationNameColor;
            set => SetProperty(ref _organizationNameColor, value);
        }

        public string SecondaryOfficer
        {
            get => _secondaryOfficer;
            set
            {
                SetProperty(ref _secondaryOfficer, value);
                ValidationErrors = string.Empty;
            }
        }

        public string Title => "Settings";

        public string UnitId
        {
            get => _unitId;
            set
            {
                SetProperty(ref _unitId, value);
                SearchVehicle(_unitId);
            }
        }

        public string ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value);
        }

        public string VehicleFeedback
        {
            get => _vehicleFeedback;
            set => SetProperty(ref _vehicleFeedback, value);
        }

        public SolidColorBrush VehicleFeedbackColor
        {
            get => _vehicleFeedbackColor;
            set => SetProperty(ref _vehicleFeedbackColor, value);
        }

        public Asset VehicleFromServer { get; private set; }

        #endregion Properties

        #region Fields

        private readonly IAPIService _api;

        private readonly IEventAggregator _eventAggregator;

        private bool _isInEditMode;

        private bool _isSaving;

        private bool _isSearchingOrganization;

        private bool _isSearchingVehicle;

        private string _notes;

        private string _officer;

        private string _organizationCode;

        private string _organizationName;

        private SolidColorBrush _organizationNameColor;

        private string _secondaryOfficer;

        private string _unitId;

        private string _validationErrors;

        private string _vehicleFeedback;

        private SolidColorBrush _vehicleFeedbackColor;

        private bool CreateVehicle;

        #endregion Fields

        #region Constructors

        public SettingsDialogViewModel(IAPIService api, IEventAggregator eventAggregator)
        {
            _api = api;
            _eventAggregator = eventAggregator;
            Close = new DelegateCommand(_close);

            OrganizationNameColor = (SolidColorBrush)System.Windows.Application.Current.FindResource(BrushNames.Muted);

            // TODO: Try to read the file, if it exist
            // TODO: Get the organization name
        }

        #endregion Constructors

        #region Events

        public event Action<IDialogResult> RequestClose;

        #endregion Events

        #region Methods

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // Check for a connection to the server right off the bat
            _api.GetOrganizationByOrganizationCode(string.Empty).ContinueWith(connectionCheck =>
            {
                if (connectionCheck.IsFaulted)
                {
                    Connected = false;
                    ValidationErrors = Properties.Resources.CantReachServer;
                }
                else
                {
                    Connected = true;
                }
            });

            if (!string.IsNullOrEmpty(OrganizationSettings.Default.Code))
                OrganizationCode = OrganizationSettings.Default.Code;

            if (!string.IsNullOrEmpty(VehicleSettings.Default.UnitId))
                UnitId = VehicleSettings.Default.UnitId;

            if (!string.IsNullOrEmpty(VehicleSettings.Default.Officer))
                Officer = VehicleSettings.Default.Officer;

            if (!string.IsNullOrEmpty(VehicleSettings.Default.SecondaryOfficer))
                SecondaryOfficer = VehicleSettings.Default.SecondaryOfficer;

            var activeTab = parameters.GetValue<string>("tab");

            if (!string.IsNullOrEmpty(parameters.GetValue<string>("code")))
            {
                var code = parameters.GetValue<string>("code");
                if (code == "vehicle_in_use")
                {
                    VehicleFeedback = string.Format(Properties.Resources.VehicleInUse, UnitId);
                    VehicleFeedbackColor = (SolidColorBrush)App.Current.FindResource(BrushNames.MoveOverYellow);
                }
            }
        }

        private void _close()
        {
            // If we're not connected to the API, just let the settings window close without problem
            if (!Connected)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    RequestClose?.Invoke(new DialogResult());
                });
            }
            else if (ValuesAreValid())
            {
                Task.Run(async () =>
                {
                    if (CreateVehicle)
                    {
                        try
                        {
                            IsSaving = true;
                            VehicleFromServer = await _api.CreateVehicle(UnitId, Settings.Default.DeviceSerialNumber, OrganizationCode, Officer, SecondaryOfficer, Notes);
                        }
                        catch
                        {
                            ValidationErrors = Properties.Resources.CantReachServer;
                        }
                        finally
                        {
                            IsSaving = false;
                        }
                    }
                    else if (ValuesChanged())
                    {
                        try
                        {
                            IsSaving = true;
                            await _api.UpdateVehicleInfo(UnitId, Settings.Default.DeviceSerialNumber, OrganizationCode, Officer, SecondaryOfficer, Notes);
                        }
                        catch
                        {
                            ValidationErrors = Properties.Resources.CantReachServer;
                        }
                        finally
                        {
                            IsSaving = false;
                        }
                    }

                    if (SettingsChanged())
                    {
                        SaveSettings();
                        _eventAggregator.GetEvent<DeviceSettingsChangedEvent>().Publish();
                    }

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        // TODO: If the OK button was used, return an OK result, if the close button was used, return a None result
                        RequestClose?.Invoke(new DialogResult());
                    });
                });
            }
        }

        private void SaveSettings()
        {
            OrganizationSettings.Default.Code = OrganizationCode;
            OrganizationSettings.Default.DisplayName = OrganizationFromServer.Properties.Value<string>("Display Name");
            OrganizationSettings.Default.Id = OrganizationFromServer.Id;
            OrganizationSettings.Default.Save();
            VehicleSettings.Default.Id = VehicleFromServer.Id;
            VehicleSettings.Default.UnitId = UnitId;
            VehicleSettings.Default.Officer = Officer;
            VehicleSettings.Default.SecondaryOfficer = SecondaryOfficer;
            VehicleSettings.Default.Save();
        }

        private void SearchOrganization(string organizationCode)
        {
            ValidationErrors = string.Empty;
            if (organizationCode.Length != 6)
            {
                OrganizationName = "Invalid organization code";
                OrganizationNameColor = Brushes.Yellow;
                OrganizationCodeIsValid = false;
                return;
            }

            Task.Run(async () =>
            {
                IsSearchingOrganization = true;
                try
                {
                    OrganizationFromServer = await _api.GetOrganizationByOrganizationCode(organizationCode);
                    if (string.IsNullOrEmpty(OrganizationFromServer.Properties.GetValue("Display Name").ToString()))
                    {
                        OrganizationName = "No organization found";
                        OrganizationNameColor = Brushes.Yellow;
                        OrganizationCodeIsValid = false;
                    }
                    else
                    {
                        OrganizationName = $"Organization: {OrganizationFromServer.Properties.GetValue("Display Name")}";
                        OrganizationNameColor = (SolidColorBrush)System.Windows.Application.Current.FindResource(BrushNames.Muted);
                        OrganizationCodeIsValid = true;

                        if (!string.IsNullOrWhiteSpace(UnitId))
                            SearchVehicle(UnitId);
                    }
                }
                catch (Exception ex)
                {
                    OrganizationName = ex.Message;
                    OrganizationNameColor = Brushes.Red;
                }
                finally
                {
                    IsSearchingOrganization = false;
                }
            });
        }

        private void SearchVehicle(string unitId)
        {
            ValidationErrors = string.Empty;
            VehicleFeedback = string.Empty;

            if (!OrganizationCodeIsValid)
            {
                VehicleFeedback = "A valid organization is required to find a vehicle";
                VehicleFeedbackColor = Brushes.Yellow;
                return;
            }

            Task.Run(async () =>
            {
                IsSearchingVehicle = true;
                try
                {
                    VehicleFromServer = await _api.GetVehicleByUnitId(unitId, OrganizationFromServer.Id);
                    if (VehicleFromServer == null)
                    {
                        VehicleFeedback = "Vehicle not found. We'll create this vehicle for you.";
                        VehicleFeedbackColor = Brushes.Yellow;
                        CreateVehicle = true;
                        Officer = string.Empty;
                        SecondaryOfficer = string.Empty;
                        Notes = string.Empty;
                    }
                    else
                    {
                        VehicleFeedback = "Vehicle found";
                        VehicleFeedbackColor = (SolidColorBrush)System.Windows.Application.Current.FindResource(BrushNames.Muted);
                        CreateVehicle = false;
                        Officer = VehicleFromServer.Attributes["Primary Officer"].ToString();
                        SecondaryOfficer = VehicleFromServer.Attributes["Secondary Officer"].ToString();
                        Notes = VehicleFromServer.Attributes["Notes"].ToString();
                    }
                }
                catch (Exception ex)
                {
                    VehicleFeedback = ex.Message;
                    VehicleFeedbackColor = Brushes.Red;
                }
                finally
                {
                    IsSearchingVehicle = false;
                }
            });
        }

        private bool SettingsChanged()
        {
            return OrganizationSettings.Default.Id != OrganizationFromServer.Id
                || !OrganizationSettings.Default.Code.Equals(OrganizationCode, StringComparison.InvariantCultureIgnoreCase)
                || !OrganizationSettings.Default.DisplayName.Equals(OrganizationFromServer.Properties.Value<string>("Display Name"), StringComparison.InvariantCultureIgnoreCase)
                || VehicleSettings.Default.Id != VehicleFromServer.Id
                || !VehicleSettings.Default.Notes.Equals(VehicleFromServer.Attributes.Value<string>("Notes"), StringComparison.InvariantCultureIgnoreCase)
                || !VehicleSettings.Default.Officer.Equals(VehicleFromServer.Attributes.Value<string>("Primary Officer"), StringComparison.InvariantCultureIgnoreCase)
                || !VehicleSettings.Default.SecondaryOfficer.Equals(VehicleFromServer.Attributes.Value<string>("Secondary Officer"), StringComparison.InvariantCultureIgnoreCase)
                || !VehicleSettings.Default.UnitId.Equals(VehicleFromServer.Attributes.Value<string>("Unit ID"), StringComparison.InvariantCultureIgnoreCase);
        }

        private bool ValuesAreValid()
        {
            if (OrganizationCode == null || OrganizationCode.Length != 6)
            {
                ValidationErrors = "Invalid organization code";
                return false;
            }

            if (!OrganizationCodeIsValid)
            {
                ValidationErrors = "Organization code not found";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Officer))
            {
                ValidationErrors = "Officer name is required";
                return false;
            }

            return true;
        }

        private bool ValuesChanged()
        {
            if (OrganizationFromServer != null)
            {
                var installCode = OrganizationFromServer.Properties.Value<string>("Install Code");
                if (!installCode.Equals(OrganizationCode))
                    return true;
            }

            if (VehicleFromServer != null)
            {
                return !UnitId.Equals(VehicleFromServer.Attributes.Value<string>("Unit ID"), StringComparison.InvariantCultureIgnoreCase)
                    || !Officer.Equals(VehicleFromServer.Attributes.Value<string>("Primary Officer"), StringComparison.InvariantCultureIgnoreCase)
                    || !SecondaryOfficer.Equals(VehicleFromServer.Attributes.Value<string>("Secondary Officer"), StringComparison.InvariantCultureIgnoreCase)
                    || !Notes.Equals(VehicleFromServer.Attributes.Value<string>("Notes"), StringComparison.InvariantCultureIgnoreCase);
            }

            return true;
        }

        #endregion Methods
    }
}