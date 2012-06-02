using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;


namespace FileClient
{
    /// <summary>
    /// Represents find file dialog model.
    /// </summary>
    public class FindDialogModel : ViewModelBase<FindDialogModel>
    {
        private bool _isVisible;
        private string _targetDirectory;
        private bool _isNextVisible;

        /// <summary>
        /// Initializes a new instance of the <see cref="FindDialogModel"/> class.
        /// </summary>
        public FindDialogModel()
        {
            FindCommand = new RelayCommand(Find);
            CloseCommand = new RelayCommand(() => IsVisible = false);
            NextCommand = new RelayCommand(GetNext);
            SearchResults = new ObservableCollection<string>();
            TargetDirectory = "/";
            Mask = "*.*";
        }

        /// <summary>
        /// Gets or sets the find command.
        /// </summary>
        public ICommand FindCommand { get; private set; }

        /// <summary>
        /// Gets or sets the find next command.
        /// </summary>
        public ICommand NextCommand { get; private set; }

        /// <summary>
        /// Gets or sets the close dialog command.
        /// </summary>
        public ICommand CloseCommand { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="FindDialogModel"/> dialog is visible.
        /// </summary>
        public bool IsVisible
        {
            get { return _isVisible; }
            set { SetProperty(ref _isVisible, value, x => x.IsVisible); }
        }

        /// <summary>
        /// Gets or sets the target drive where find operation is performed.
        /// </summary>
        public Drive TargetDrive
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the search mask.
        /// </summary>
        public string Mask
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the target directory where search is performed.
        /// </summary>
        public string TargetDirectory
        {
            get { return _targetDirectory; }
            set { SetProperty(ref _targetDirectory, value, x => x.TargetDirectory); }
        }

        /// <summary>
        /// Contains current search results, displayed to user.
        /// </summary>
        public ObservableCollection<string> SearchResults
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the iterator which will move to next results.
        /// </summary>
        public IEnumerator<string> PendingResults
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether next result button is visible.
        /// </summary>
        public bool IsNextVisible
        {
            get { return _isNextVisible; }
            set { SetProperty(ref _isNextVisible, value, x => x.IsNextVisible); }
        }

        private void GetNext()
        {
            if (PendingResults != null)
            {
                IsNextVisible = true;
                SearchResults.Clear();
                int i = 0;
                while (PendingResults.MoveNext() && i++ < 10)
                {
                    SearchResults.Add(PendingResults.Current);
                }

                if (i <= 10)
                {
                    PendingResults = null;
                    IsNextVisible = false;
                }
            }
        }

        private void Find()
        {
            var fs = TargetDrive.AssociatedFileSystem;
            PendingResults = fs.FindFile(TargetDirectory, Mask, true).GetEnumerator();
            GetNext();
        }
    }
}