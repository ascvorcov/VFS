namespace FileClient
{
    /// <summary>
    /// Holds reference to file browser model.
    /// </summary>
    public class ViewModelLocator
    {
        private static readonly FileBrowserModel _fileBrowserModel = new FileBrowserModel();

        /// <summary>
        /// Gets the file browser model.
        /// </summary>
        public static FileBrowserModel FileBrowserModel
        {
            get
            {
                return _fileBrowserModel;
            }
        }
    }
}