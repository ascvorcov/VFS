using System;
using System.ComponentModel;

namespace FileClient
{
    /// <summary>
    /// Represents single long-running operation in file browser.
    /// </summary>
    public class Operation : ViewModelBase<Operation>
    {
        private string _progressText;
        private readonly BackgroundWorker _worker = new BackgroundWorker();

        /// <summary>
        /// Initializes a new instance of the <see cref="Operation"/> class.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="onCompleted">The on completed.</param>
        public Operation(Action<Operation> work, Action<Operation> onCompleted)
        {
            _worker.DoWork += (s, e) =>
            {
                work(this);
                OperationCompleted = true;
            };

            _worker.RunWorkerCompleted += (s, e) =>
            {
                onCompleted(this);
                if (OnOperationCompleted != null)
                {
                    OnOperationCompleted(this);
                }
            };
        }


        /// <summary>
        /// Gets or sets a value indicating whether operation is completed.
        /// </summary>
        public bool OperationCompleted
        {
            get;
            private set;
        }

        /// <summary>
        /// Occurs when on operation completed.
        /// </summary>
        public event Action<Operation> OnOperationCompleted;

        /// <summary>
        /// Gets or sets the progress text.
        /// </summary>
        public string ProgressText
        {
            get { return _progressText; }
            set { SetProperty(ref _progressText, value, x => x.ProgressText); }
        }

        /// <summary>
        /// Starts the operation.
        /// </summary>
        public void StartAsync()
        {
            _worker.RunWorkerAsync();
        }
    }
}