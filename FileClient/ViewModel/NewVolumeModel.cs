using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using VirtualFileSystem;
using VirtualFileSystem.Annotations;


namespace FileClient
{
    /// <summary>
    /// New volume dialog model.
    /// </summary>
    public class NewVolumeModel : ViewModelBase<NewVolumeModel>
    {
        private readonly ObservableCollection<Drive> _mountedDrives;
        private bool _isVisible;
        private string _volumeSize = "10";
        private string _volumeName;


        /// <summary>
        /// Initializes a new instance of the <see cref="NewVolumeModel"/> class.
        /// </summary>
        /// <param name="mountedDrives">The mounted drives.</param>
        public NewVolumeModel(ObservableCollection<Drive> mountedDrives)
        {
            _mountedDrives = mountedDrives;
            CreateCommand = new RelayCommand(CreateNewStorage);
            CancelCommand = new RelayCommand(() => IsVisible = false);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="NewVolumeModel"/> dialog is visible.
        /// </summary>
        public bool IsVisible
        {
            get { return _isVisible; }
            set { SetProperty(ref _isVisible, value, x => x.IsVisible); }
        }

        /// <summary>
        /// Gets or sets the create new volume command.
        /// </summary>
        public ICommand CreateCommand { get; private set; }


        /// <summary>
        /// Gets or sets the cancel and close dialog command.
        /// </summary>
        public ICommand CancelCommand { get; private set; }

        /// <summary>
        /// Gets or sets the name of the new volume.
        /// </summary>
        public string VolumeName
        {
            get { return _volumeName; }
            set
            {
                SetProperty(ref _volumeName, value, x => x.VolumeName);
                OnPropertyChanged("FullPathWithName"); // also auto-modified
            }
        }

        /// <summary>
        /// Holds possible values for volume sizes selection, in megabytes.
        /// </summary>
        [UsedImplicitly]
        public IEnumerable<string> VolumeSizes
        {
            get
            {
                yield return "10";
                yield return "50";
                yield return "100";
                yield return "500";
                yield return "1000";
                yield return "5000";
                yield return "10000";
                yield return "100000";
                yield return "1000000";
            }
        }

        /// <summary>
        /// Gets or sets the full path to volume.
        /// </summary>
        public string FullPath
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the full name of the volume file, with path.
        /// </summary>
        public string FullPathWithName
        {
            get { return FullPath + VolumeName; }
        }

        /// <summary>
        /// Gets or sets the size of the volume in text form.
        /// </summary>
        public string VolumeSize
        {
            get { return _volumeSize; }
            set { SetProperty(ref _volumeSize, value, x => x.VolumeSize); }
        }

        /// <summary>
        /// Gets or sets the target drive where volume will be created.
        /// </summary>
        public Drive TargetDrive
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the target panel which will open mounted volume.
        /// </summary>
        public FilePanelModel TargetPanel
        {
            get;
            set;
        }

        /// <summary>
        /// Creates and mounts the new storage.
        /// </summary>
        public void CreateNewStorage()
        {
            try
            {
                var selectedDrive = TargetDrive;

                var size = ulong.Parse(VolumeSize) * 1024 * 1024;
                var fs = FileSystems.CreateNewVolume(FullPathWithName, size, selectedDrive.AssociatedFileSystem);
                var newDrive = new Drive
                                   {
                                       AssociatedFileSystem = fs,
                                       Root = "\\",
                                       Name = VolumeName,
                                       CreationOrder = _mountedDrives.Count
                                   };

                _mountedDrives.Add(newDrive);
                TargetPanel.ActiveDrive = newDrive;
                IsVisible = false;
            }
            catch (Exception ex)
            {
                // todo: move to proper popup.
                MessageBox.Show(ex.Message);
            }
        }
    }
}