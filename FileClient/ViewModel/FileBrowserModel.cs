using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

using VirtualFileSystem;


namespace FileClient
{
    /// <summary>
    /// Model of main file browser window.
    /// </summary>
    public class FileBrowserModel : ViewModelBase<FileBrowserModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileBrowserModel"/> class.
        /// </summary>
        public FileBrowserModel() 
        {
            MountedDrives = new ObservableCollection<Drive>();
            NewCommand = new RelayCommand(CreateNewStorage);
            CopyCommand = new RelayCommand(CopyFileOrDirectory);
            MoveCommand = new RelayCommand(MoveFileOrDirectory);
            FindCommand = new RelayCommand(Find);
            DeleteCommand = new RelayCommand(DeleteFileOrDirectory);

            var fileSystem = FileSystems.Real;
            int i = 0;
            foreach (var drive in fileSystem.GetDrives())
            {
                MountedDrives.Add(new Drive { Root = drive, Name = drive, AssociatedFileSystem = fileSystem, CreationOrder = i++});
            }

            LeftPanel = new FilePanelModel(MountedDrives);
            RightPanel = new FilePanelModel(MountedDrives);
            ProgressPanel = new ProgressViewModel();
            NewVolumeDialog = new NewVolumeModel(MountedDrives);
            FindDialog = new FindDialogModel();
        }

        /// <summary>
        /// Close window, unmount all open drives.
        /// </summary>
        public void Close()
        {
            foreach (var drive in MountedDrives.OrderByDescending(d => d.CreationOrder))
            {
                drive.AssociatedFileSystem.Dispose();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether left panel is active.
        /// </summary>
        /// <value><c>true</c> if left panel active; <c>false</c>, if right panel is active.</value>
        public bool LeftPanelActive
        {
            get;
            set;
        }

        /// <summary>
        /// Collection of currently available drives.
        /// </summary>
        public ObservableCollection<Drive> MountedDrives
        {
            get;
            set;
        }

        /// <summary>
        /// Left panel model.
        /// </summary>
        public FilePanelModel LeftPanel
        {
            get;
            private set;
        }


        /// <summary>
        /// Right panel model.
        /// </summary>
        public FilePanelModel RightPanel
        {
            get;
            private set;
        }

        /// <summary>
        /// Progress panel model.
        /// </summary>
        public ProgressViewModel ProgressPanel
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the new volume dialog model.
        /// </summary>
        public NewVolumeModel NewVolumeDialog
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the find dialog model.
        /// </summary>
        public FindDialogModel FindDialog
        {
            get;
            private set;
        }

        #region Commands

        /// <summary>
        /// Create new volume command.
        /// </summary>
        public ICommand NewCommand { get; private set; }

        /// <summary>
        /// Copy file/directory command.
        /// </summary>
        public ICommand CopyCommand { get; private set; }

        /// <summary>
        /// Move file/directory command.
        /// </summary>
        public ICommand MoveCommand { get; private set; }

        /// <summary>
        /// Find file command.
        /// </summary>
        public ICommand FindCommand { get; private set; }

        /// <summary>
        /// Delete file/directory command.
        /// </summary>
        public ICommand DeleteCommand { get; private set; }
        
        #endregion

        #region Actions

        private void CreateNewStorage()
        {
            var panel = RightPanel;
            if (LeftPanelActive)
            {
                panel = LeftPanel;
            }

            NewVolumeDialog.TargetPanel = panel;
            NewVolumeDialog.TargetDrive = panel.ActiveDrive;
            NewVolumeDialog.FullPath = panel.OpenDirectory;
            NewVolumeDialog.VolumeName = "storage.vfs";
            NewVolumeDialog.IsVisible = true;
        }

        private void DeleteFileOrDirectory()
        {
            var activePanel = LeftPanelActive ? LeftPanel : RightPanel;
            var toDelete = activePanel.SelectedFile;
            if (toDelete == null)
                return;

            var op = activePanel.Delete();
            if (op != null)
            {
                ProgressPanel.AddOperation(op);
                op.StartAsync();
            }
        }

        private void Find()
        {
            FindDialog.TargetDirectory = LeftPanelActive ? LeftPanel.OpenDirectory : RightPanel.OpenDirectory;
            FindDialog.TargetDrive = LeftPanelActive ? LeftPanel.ActiveDrive : RightPanel.ActiveDrive;
            FindDialog.IsVisible = true;
        }

        private void CopyFileOrDirectory()
        {
            Operation op;
            if (LeftPanelActive)
            {
                op = LeftPanel.CopyTo(RightPanel);
            }
            else
            {
                op = RightPanel.CopyTo(LeftPanel);
            }

            if (op != null)
            {
                ProgressPanel.AddOperation(op);
                op.StartAsync();
            }
        }

        private void MoveFileOrDirectory()
        {
            Operation op;
            if (LeftPanelActive)
            {
                op = LeftPanel.MoveTo(RightPanel);
            }
            else
            {
                op = RightPanel.MoveTo(LeftPanel);
            }

            if (op != null)
            {
                ProgressPanel.AddOperation(op);
                op.StartAsync();
            }
        }

        #endregion

    }
}