using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using PursuitAlert.Client.Old.Properties;
using PursuitAlert.Domain.API.Services;
using PursuitAlert.Domain.Device.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PursuitAlert.Client.Old.Dialogs.VehicleSettings
{
    public class SettingsDialogViewModel : BindableBase, IDialogAware
    {
        #region Properties

        public DelegateCommand Close { get; private set; }

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

        #endregion Properties

        #region Fields

        private const string MutedBrushKey = "MutedBrush";

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

            OrganizationNameColor = (SolidColorBrush)System.Windows.Application.Current.FindResource(MutedBrushKey);

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
            if (!string.IsNullOrEmpty(Settings.Default.OrganizationCode))
                OrganizationCode = Settings.Default.OrganizationCode;

            if (!string.IsNullOrEmpty(Settings.Default.UnitId))
                UnitId = Settings.Default.UnitId;

            if (!string.IsNullOrEmpty(Settings.Default.Officer))
                Officer = Settings.Default.Officer;

            if (!string.IsNullOrEmpty(Settings.Default.SecondaryOfficer))
                SecondaryOfficer = Settings.Default.SecondaryOfficer;
        }

        private void _close()
        {
            if (ValuesAreValid())
            {
                IsSaving = true;
                Task.Run(async () =>
                {
                    if (CreateVehicle)
                    {
                        // Force a device disconnect/reconnect to update the configuration
                        _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();

                        Settings.Default.OrganizationCode = OrganizationCode;
                        Settings.Default.UnitId = UnitId;
                        Settings.Default.Officer = Officer;
                        Settings.Default.SecondaryOfficer = SecondaryOfficer;
                        Settings.Default.Save();

                        await _api.CreateVehicle(UnitId, DeviceSettings.Default.DeviceSerialNumber, OrganizationCode, Officer, SecondaryOfficer, Notes);
                    }
                    else if (ValuesChanged())
                    {
                        // Force a device disconnect/reconnect to update the configuration
                        _eventAggregator.GetEvent<DeviceDisconnectedEvent>().Publish();

                        Settings.Default.OrganizationCode = OrganizationCode;
                        Settings.Default.UnitId = UnitId;
                        Settings.Default.Officer = Officer;
                        Settings.Default.SecondaryOfficer = SecondaryOfficer;
                        Settings.Default.Save();

                        await _api.UpdateVehicleInfo(UnitId, DeviceSettings.Default.DeviceSerialNumber, OrganizationCode, Officer, SecondaryOfficer, Notes);
                    }
                    IsSaving = false;
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        RequestClose?.Invoke(new DialogResult());
                    });
                });
            }
        }

        private void SearchOrganization(string organizationCode)
        {
            ValidationErrors = string.Empty;
            if (organizationCode.Length != 6)
            {
                OrganizationName = "Invalid organization code";
                OrganizationNameColor = Brushes.Yellow;
                OrganizationCodeIsValid = false;
            }

            Task.Run(async () =>
            {
                try
                {
                    IsSearchingOrganization = true;
                    var organization = await _api.GetOrganizationByOrganizationCode(organizationCode);
                    if (string.IsNullOrEmpty(organization.Properties.GetValue("Display Name").ToString()))
                    {
                        OrganizationName = "No organization found";
                        OrganizationNameColor = Brushes.Yellow;
                        OrganizationCodeIsValid = false;
                    }
                    else
                    {
                        OrganizationName = $"Organization: {organization.Properties.GetValue("Display Name")}";
                        OrganizationNameColor = (SolidColorBrush)System.Windows.Application.Current.FindResource(MutedBrushKey);
                        OrganizationCodeIsValid = true;
                    }
                    IsSearchingOrganization = false;
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
            IsSearchingVehicle = true;
            Task.Run(async () =>
            {
                try
                {
                    IsSearchingOrganization = true;
                    var vehicle = await _api.GetVehicleByUnitId(unitId);
                    if (vehicle == null)
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
                        VehicleFeedbackColor = (SolidColorBrush)System.Windows.Application.Current.FindResource(MutedBrushKey);
                        CreateVehicle = false;
                        Officer = vehicle.Attributes["Primary Officer"].ToString();
                        SecondaryOfficer = vehicle.Attributes["Secondary Officer"].ToString();
                        Notes = vehicle.Attributes["Notes"].ToString();
                    }
                    IsSearchingOrganization = false;
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

        private bool ValuesAreValid()
        {
            if (string.IsNullOrWhiteSpace(OrganizationCode))
            {
                ValidationErrors = "Organization code is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(UnitId))
            {
                ValidationErrors = "Vehicle Id is required";
                return false;
            }

            if (OrganizationCode.Length != 6)
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
            return Settings.Default.OrganizationCode != OrganizationCode
            || Settings.Default.UnitId != UnitId
            || Settings.Default.Officer != Officer
            || Settings.Default.SecondaryOfficer != SecondaryOfficer;
        }

        #endregion Methods
    }
}