using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace PursuitAlert.Client.Resources.Utilities
{
    public class CaseConverter : IValueConverter
    {
        #region Properties

        public CharacterCasing Case { get; }

        #endregion Properties

        #region Fields

        private string _originalString;

        #endregion Fields

        #region Constructors

        public CaseConverter()
        {
            Case = CharacterCasing.Upper;
        }

        #endregion Constructors

        #region Methods

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            _originalString = value as string;
            if (!string.IsNullOrWhiteSpace(_originalString))
            {
                switch (Case)
                {
                    case CharacterCasing.Lower:
                        return _originalString.ToLower();

                    case CharacterCasing.Normal:
                        return _originalString;

                    case CharacterCasing.Upper:
                        return _originalString.ToUpper();

                    default:
                        return _originalString;
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return _originalString;
        }

        #endregion Methods
    }
}