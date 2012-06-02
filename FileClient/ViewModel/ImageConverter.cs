using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;


namespace FileClient
{
    /// <summary>
    /// Helper to show file/directory icons in list.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(BitmapImage))]
    public class ImageConverter : IValueConverter
    {
        /// <summary>
        /// Converts a value.
        /// </summary>
        /// <param name="value">The value produced by the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A converted value. If the method returns null, the valid null value is used.
        /// </returns>
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            try
            {
                var isDirectory = (bool)value;
                var path = isDirectory ? "/Icons/Folder.png" : "/Icons/File.png";
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                img.EndInit();
                return img;
            }
            catch
            {
                return new BitmapImage();
            }
        }

        /// <summary>
        /// Converts a value.
        /// </summary>
        /// <param name="value">The value that is produced by the binding target.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A converted value. If the method returns null, the valid null value is used.
        /// </returns>
        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}