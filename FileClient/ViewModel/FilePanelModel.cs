#region Namespace Imports

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

using VirtualFileSystem;
using VirtualFileSystem.Annotations;
using VirtualFileSystem.Interfaces;

#endregion


namespace FileClient
{
    /// <summary>
    /// Model of single file browser panel.
    /// </summary>
    public class FilePanelModel : ViewModelBase<FilePanelModel>
    {
        #region Constants and Fields

        private readonly ObservableCollection<Drive> _mountedDrives;
        private Drive _activeDrive;
        private File _selectedFile;

        #endregion


        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FilePanelModel"/> class.
        /// </summary>
        /// <param name="mountedDrives">The mounted drives collection.</param>
        public FilePanelModel([NotNull] ObservableCollection<Drive> mountedDrives)
        {
            _mountedDrives = mountedDrives;

            Files = new ObservableCollection<File>();

            ActiveDrive = mountedDrives[0];
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets or sets the active drive displayed in panel.
        /// </summary>
        public Drive ActiveDrive
        {
            get
            {
                return _activeDrive;
            }
            set
            {
                SetProperty(ref _activeDrive, value, x => x.ActiveDrive);
                FillRoot();
            }
        }


        /// <summary>
        /// Collection of files displayed in panel.
        /// </summary>
        public ObservableCollection<File> Files
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the name of currently open directory.
        /// </summary>
        public string OpenDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the currently selected file in panel.
        /// </summary>
        public File SelectedFile
        {
            get
            {
                return _selectedFile;
            }
            set
            {
                SetProperty(ref _selectedFile, value, x => x.SelectedFile);
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Copies selected file / directory from current panel to another. Returns async operation.
        /// </summary>
        /// <param name="anotherPanel">Target panel.</param>
        [CanBeNull]
        public Operation CopyTo([NotNull] FilePanelModel anotherPanel)
        {
            var fileToCopy = SelectedFile;
            var destFolder = anotherPanel.OpenDirectory;

            if (fileToCopy == null || string.IsNullOrEmpty(destFolder))
            {
                return null;
            }

            var sourceDrive = ActiveDrive.AssociatedFileSystem;
            var destDrive = anotherPanel.ActiveDrive.AssociatedFileSystem;
            var sourceFullName = fileToCopy.FullName;
            var destFullName = destFolder + fileToCopy.Name;
            var directory = fileToCopy.IsDirectory;

            Action<Operation> work = p =>
            {
                p.ProgressText = string.Format(
                    "Copying file {0} to {1}", sourceFullName, destFullName);
                if (directory)
                {
                    sourceDrive.CopyDirectory(sourceFullName, destFullName, destDrive);
                }
                else
                {
                    sourceDrive.CopyFile(sourceFullName, destFullName, destDrive);
                }
            };

            Action<Operation> update = p => anotherPanel.UpdatePanel();

            return new Operation(work, update);
        }


        /// <summary>
        /// Deletes currently selected file or directory in panel.
        /// Returns async operation.
        /// </summary>
        [CanBeNull]
        public Operation Delete()
        {
            var toDelete = SelectedFile;
            var message = (toDelete.IsDirectory
                               ? "Are you sure you want to delete directory {0} and all its contents?"
                               : "Are you sure you want to delete file {0} ?");

            Action<Operation> update = p => UpdatePanel();

            // todo: move to proper popup.
            if (MessageBox.Show(string.Format(message, toDelete.FullName), "Warning", MessageBoxButton.OKCancel)
                != MessageBoxResult.OK)
            {
                return null;
            }

            Action<Operation> work = p =>
            {
                p.ProgressText = string.Format("Deleting {0}", toDelete.FullName);

                try
                {
                    var fs = ActiveDrive.AssociatedFileSystem;
                    if (toDelete.IsDirectory)
                    {
                        fs.DeleteDirectory(toDelete.FullName, true);
                    }
                    else
                    {
                        fs.DeleteFile(toDelete.FullName);
                    }
                }
                catch (IOException ex)
                {
                    p.ProgressText = ex.Message;
                }
            };

            return new Operation(work, update);
        }


        /// <summary>
        /// Enters the currently selected directory, or mounts volume if selected.
        /// </summary>
        public void EnterDirectory()
        {
            var directory = _selectedFile;
            if (directory == null)
            {
                return;
            }

            var fs = ActiveDrive.AssociatedFileSystem;

            if (!directory.IsDirectory)
            {
                TryMountVolume(directory, fs);
                return;
            }

            var destFolder = directory.FullName;
            if (directory.NavUp)
            {
                destFolder = directory.ContainingFolder;
            }

            OpenDirectory = destFolder.EndsWith("\\") ? destFolder : destFolder + "\\";
            UpdatePanel();
        }


        /// <summary>
        /// Moves currently selected file or directory to another panel.
        /// Returns asynchronous operation.
        /// </summary>
        /// <param name="anotherPanel">Target panel.</param>
        [CanBeNull]
        public Operation MoveTo([NotNull] FilePanelModel anotherPanel)
        {
            var fileToMove = SelectedFile;
            var destFolder = anotherPanel.OpenDirectory;

            if (fileToMove == null || string.IsNullOrEmpty(destFolder))
            {
                return null;
            }

            var sourceDrive = ActiveDrive.AssociatedFileSystem;
            var destDrive = anotherPanel.ActiveDrive.AssociatedFileSystem;
            var destFullName = destFolder + fileToMove.Name;
            var sourceFullName = fileToMove.FullName;
            var directory = fileToMove.IsDirectory;

            Action<Operation> work = p =>
            {
                p.ProgressText = string.Format(
                    "Moving {0} to {1}", sourceFullName, destFullName);
                if (directory)
                {
                    sourceDrive.MoveDirectory(sourceFullName, destFullName, destDrive);
                }
                else
                {
                    sourceDrive.MoveFile(sourceFullName, destFullName, destDrive);
                }
            };

            Action<Operation> update = p =>
                                       {
                                           UpdatePanel();
                                           anotherPanel.UpdatePanel();
                                       };

            return new Operation(work, update);
        }


        /// <summary>
        /// Updates the panel, reloading all files in currently open directory.
        /// </summary>
        public void UpdatePanel()
        {
            var destFolder = OpenDirectory;
            var fs = ActiveDrive.AssociatedFileSystem;

            bool isRoot = destFolder.Length <= 3;

            IEnumerable<string> newFileList;
            try
            {
                newFileList = fs.FindFile(destFolder, "*", false);
            }
            catch (UnauthorizedAccessException ex)
            {
                // todo: move to proper popup.
                MessageBox.Show(ex.Message);
                return;
            }
            catch (IOException ex)
            {
                // todo: move to proper popup.
                MessageBox.Show(ex.Message);
                return;
            }

            Files.Clear();

            if (!isRoot)
            {
                Files.Add(new File { FullName = destFolder, NavUp = true, IsDirectory = true });
            }

            var files = new List<File>();
            var folders = new List<File>();
            foreach (var fileName in newFileList)
            {
                var info = fs.GetFileInfo(fileName);
                if (!info.Exists)
                {
                    continue;
                }

                var file = new File
                {
                    FullName = fileName,
                    IsDirectory = info.IsDirectory,
                    Size = info.IsDirectory ? string.Empty : info.FileSize.ToString(),
                    Created = info.CreatedDate.ToShortDateString(),
                    Modified = info.ModifiedDate.ToShortDateString()
                };

                if (info.IsDirectory)
                {
                    folders.Add(file);
                }
                else
                {
                    files.Add(file);
                }
            }

            folders.ForEach(Files.Add);
            files.ForEach(Files.Add);
        }

        #endregion


        #region Methods

        private void FillRoot()
        {
            var drive = ActiveDrive;
            var root = new File { FullName = drive.Root, IsDirectory = true, NavUp = false, Size = string.Empty };

            SelectedFile = root;

            EnterDirectory();
        }


        private void TryMountVolume([NotNull] File directory, [NotNull] IFileSystem sourceSystem)
        {
            if (!directory.FullName.EndsWith(".vfs"))
            {
                return;
            }

            // virtual file system file
            // todo: move to proper popup.
            if (MessageBox.Show("Do you want to mount selected volume?", "Mount volume", MessageBoxButton.YesNo)
                != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var virtualSystem = FileSystems.MountVirtual(directory.FullName, sourceSystem);
                var newDrive = new Drive
                                   {
                                       AssociatedFileSystem = virtualSystem,
                                       Root = "\\",
                                       Name = directory.Name,
                                       CreationOrder = _mountedDrives.Count
                                   };

                _mountedDrives.Add(newDrive);
                ActiveDrive = newDrive;
            }
            catch (Exception ex)
            {
                // todo: move to proper popup.
                MessageBox.Show(ex.Message);
            }
        }

        #endregion
    }
}