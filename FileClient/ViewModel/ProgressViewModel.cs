using System.Collections.ObjectModel;
using System.Windows.Input;


namespace FileClient
{
    /// <summary>
    /// Model of progress view dialog.
    /// </summary>
    public class ProgressViewModel : ViewModelBase<ProgressViewModel>
    {
        private bool _progressVisible;


        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressViewModel"/> class.
        /// </summary>
        public ProgressViewModel()
        {
            Operations = new ObservableCollection<Operation>();
            HideProgressCommand = new RelayCommand(() => ProgressVisible = false);
        }

        /// <summary>
        /// Gets or sets a value indicating whether progress dialog is visible.
        /// </summary>
        public bool ProgressVisible
        {
            get { return _progressVisible; }
            set { SetProperty(ref _progressVisible, value, x => x.ProgressVisible); }
        }

        /// <summary>
        /// Contains operations in progress, displayed in progress dialog.
        /// </summary>
        public ObservableCollection<Operation> Operations
        {
            get;
            set;
        }


        /// <summary>
        /// Command which hide progress dialog.
        /// </summary>
        public ICommand HideProgressCommand { get; private set; }

        /// <summary>
        /// Adds the operation to list, starts monitoring its progress.
        /// </summary>
        public void AddOperation(Operation operation)
        {
            Operations.Add(operation);
            operation.OnOperationCompleted += OnOperationCompleted;
            ProgressVisible = true;
        }

        private void OnOperationCompleted(Operation op)
        {
            Operations.Remove(op);
            op.OnOperationCompleted -= OnOperationCompleted;
            if (Operations.Count == 0)
            {
                ProgressVisible = false;
            }
        }
   
    }
}