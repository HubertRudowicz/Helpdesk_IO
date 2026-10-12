using System;
using System.Globalization;
using System.Windows.Data;

namespace HelpDeskApp
{
    public class FolderNameToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string folderName = value as string;
            if (string.IsNullOrEmpty(folderName))
                return "";
            if (!folderName.StartsWith("zgloszenie_"))
                return folderName;

            string datePart = folderName.Substring("zgloszenie_".Length);
            datePart = datePart.Replace(".cancelled", "");
            var parts = datePart.Split('_');
            if (parts.Length != 2)
                return folderName;

            if (!DateTime.TryParseExact(parts[0] + " " + parts[1].Replace('-', ':'),
                                       "yyyy-MM-dd HH:mm:ss",
                                       CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                if (!DateTime.TryParseExact(parts[0] + " " + parts[1].Replace('-', ':'),
                                           "yyyy-MM-dd HH:mm",
                                           CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    return folderName;
                }
            }
            return $"Zgłoszenie {dt:dd-MM-yyyy HH:mm}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
