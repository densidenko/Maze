﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FileExplorer.Administration.Controls;
using FileExplorer.Administration.Controls.Models;
using FileExplorer.Administration.Helpers;
using FileExplorer.Administration.Models;
using FileExplorer.Administration.Utilities;
using FileExplorer.Administration.ViewModels.Explorer;
using FileExplorer.Administration.ViewModels.Explorer.Helpers;
using FileExplorer.Shared.Dtos;
using Orcus.Utilities;
using Prism.Mvvm;

namespace FileExplorer.Administration.ViewModels
{
    public class DirectoryTreeViewModel : BindableBase, ISupportTreeSelector<DirectoryNodeViewModel, FileExplorerEntry>, IAsyncAutoComplete
    {
        private readonly FileExplorerViewModel _fileExplorerViewModel;
        private ObservableCollection<DirectoryNodeViewModel> _autoCompleteEntries;
        private List<DirectoryNodeViewModel> _rootViewModels;
        private DirectoryNodeViewModel _selectedViewModel;
        private readonly FileExplorerPathComparer _fileExplorerPathComparer;

        public DirectoryTreeViewModel(FileExplorerViewModel fileExplorerViewModel)
        {
            _fileExplorerViewModel = fileExplorerViewModel;
            NavigationBarViewModel = fileExplorerViewModel.NavigationBarViewModel;

            Entries = new EntriesHelper<DirectoryNodeViewModel>();
            Selection = new TreeRootSelector<DirectoryNodeViewModel, FileExplorerEntry>(Entries)
            {
                Comparers = new[]
                    {_fileExplorerPathComparer = new FileExplorerPathComparer(fileExplorerViewModel.FileSystem)}
            };
            Selection.AsRoot().SelectionChanged += OnSelectionChanged;
            _fileExplorerViewModel.PathChanged += FileExplorerViewModelOnPathChanged;
        }

        public NavigationBarViewModel NavigationBarViewModel { get; }

        public List<DirectoryNodeViewModel> RootViewModels
        {
            get => _rootViewModels;
            private set => SetProperty(ref _rootViewModels, value);
        }

        public ObservableCollection<DirectoryNodeViewModel> AutoCompleteEntries
        {
            get => _autoCompleteEntries;
            private set => SetProperty(ref _autoCompleteEntries, value);
        }

        public DirectoryNodeViewModel SelectedViewModel
        {
            get => _selectedViewModel;
            private set => SetProperty(ref _selectedViewModel, value);
        }

        public IEntriesHelper<DirectoryNodeViewModel> Entries { get; set; }
        public ITreeSelector<DirectoryNodeViewModel, FileExplorerEntry> Selection { get; set; }

        public ValueTask<IEnumerable> GetAutoCompleteEntries()
        {
            return new ValueTask<IEnumerable>(AutoCompleteEntries);
        }

        public async Task SelectAsync(FileExplorerEntry value)
        {
            await Selection.LookupAsync(value,
                RecrusiveSearch<DirectoryNodeViewModel, FileExplorerEntry>.LoadSubentriesIfNotLoaded,
                SetSelected<DirectoryNodeViewModel, FileExplorerEntry>.WhenSelected,
                SetExpanded<DirectoryNodeViewModel, FileExplorerEntry>.WhenChildSelected);
        }

        public void InitializeRootElements(RootElementsDto dto)
        {
            var rootElements = dto.RootDirectories.ToList();
            rootElements.Add(dto.ComputerDirectory);

            RootViewModels = rootElements.Select(x =>
                    new DirectoryNodeViewModel(this, null, x, _fileExplorerViewModel.FileSystem,
                        _fileExplorerViewModel))
                .ToList();
            Entries.SetEntries(UpdateMode.Update, RootViewModels.ToArray());

            InitializeRoots(dto.ComputerDirectory.Yield()).Forget(); //will execute synchronously

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action) (() =>
            {
                //expand and select Computer
                var entry = Entries.AllNonBindable.Last();
                entry.Entries.IsExpanded = true;

                Selection.AsRoot().SelectAsync(dto.ComputerDirectory).Forget();
                entry.BringIntoView();
            }));
        }

        public async Task InitializeRoots(IEnumerable<DirectoryEntry> directories)
        {
            var list = Entries.All.ToList(); //copy

            //load the entries of the root view models
            var tasks = directories.Select(entry => Entries.All.First(x => x.Source == entry).Entries.LoadAsync())
                .ToList();
            await Task.WhenAll(tasks);

            foreach (var task in tasks)
                list.AddRange(task.Result);

            AutoCompleteEntries = new ObservableCollection<DirectoryNodeViewModel>(list);
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            var rootSelector = Selection.AsRoot();
            var currentItem = rootSelector.SelectedViewModel;
            //currentItem.BringIntoView();
            if (currentItem.Parent != null)
                currentItem.Parent.Entries.IsExpanded = true;

            SelectedViewModel = currentItem;

            _fileExplorerViewModel.OpenPath(currentItem.Source.Path).Forget();
        }

        private async void FileExplorerViewModelOnPathChanged(object sender, PathContent e)
        {
            var rootSelection = Selection.AsRoot();

            DirectoryNodeViewModel directoryViewModel = null;
            if (rootSelection.IsChildSelected)
            {
                var relation = _fileExplorerPathComparer.CompareHierarchy(rootSelection.SelectedValue.Path, e.Path);

                switch (relation)
                {
                    case HierarchicalResult.Current:
                        return;
                    case HierarchicalResult.Parent:
                        directoryViewModel = UpwardSelect(e.Path, rootSelection.SelectedViewModel);
                        break;
                    case HierarchicalResult.Child:
                        directoryViewModel = await DownwardSelect(e.PathDirectories, 0, rootSelection.SelectedViewModel);
                        break;
                    case HierarchicalResult.Unrelated:
                        directoryViewModel = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (directoryViewModel == null)
            {
                //check if there is any root view model which the path starts with (or is the path)

                var (rootTreeNodeViewModel, relation) = FindRelatedViewModel(Entries.All, e.Directory);

                if (rootTreeNodeViewModel == null)
                    (rootTreeNodeViewModel, relation) = FindRelatedViewModel(
                        Entries.All.First(x => ((DirectoryEntry) x.Source).IsComputerDirectory()).Entries.All,
                        e.Directory);

                if (rootTreeNodeViewModel != null)
                {
                    switch (relation)
                    {
                        case HierarchicalResult.Current:
                            directoryViewModel = rootTreeNodeViewModel;
                            break;
                        case HierarchicalResult.Child:
                            var position = e.PathDirectories.FindIndex(x =>
                                ComparePaths(x.Path, rootTreeNodeViewModel.Source.Path));

                            rootTreeNodeViewModel.Entries.IsExpanded = true;

                            directoryViewModel =
                                await DownwardSelect(e.PathDirectories, position + 1, rootTreeNodeViewModel);
                            break;
                    }
                }

                //var pathRoot = e.PathDirectories.First();

                //DirectoryNodeViewModel rootEntry = null;
                //foreach (var directoryNodeViewModel in Entries.All)
                //{
                //    if (directoryNodeViewModel.Source.Equals(pathRoot))
                //    {
                //        if (!directoryNodeViewModel.Entries.IsLoaded)
                //            await directoryNodeViewModel.Entries.LoadAsync();

                //        directoryNodeViewModel.Entries.IsExpanded = true;
                //        directoryNodeViewModel.Selection.IsSelected = true;
                //        directoryNodeViewModel.IsBringIntoView = true;
                //        return;
                //    }

                //    if (directoryNodeViewModel.Entries.IsLoaded)
                //    {
                //        rootEntry = directoryNodeViewModel.Entries.All.FirstOrDefault(x =>
                //                        e.Path.StartsWith(x.Source.Path,
                //                            _fileExplorerViewModel.FileSystem.PathStringComparison)) ??
                //                    directoryNodeViewModel.Entries.All.FirstOrDefault(x => x.Source == pathRoot);
                //        if (rootEntry != null)
                //        {
                //            var pathDirectory = e.PathDirectories.First(x => ComparePaths(x.Path, rootEntry.Source.Path));

                //            var index = e.PathDirectories.IndexOf(pathDirectory) + 1;
                //            if (index != e.PathDirectories.Count)
                //            {
                //                await DownwardSelect(e.PathDirectories, e.PathDirectories.IndexOf(pathDirectory) + 1,
                //                    rootEntry);
                //            }
                //            else
                //            {
                //                rootEntry.Selection.IsSelected = true;
                //                rootEntry.IsBringIntoView = true;
                //                if (!rootEntry.Entries.IsLoaded)
                //                    await directoryNodeViewModel.Entries.LoadAsync();
                //            }

                //            rootEntry.Entries.IsExpanded = true;
                //        }
                //    }
                //}
            }
            
            if (rootSelection.IsChildSelected)
                rootSelection.SelectedViewModel.Selection.IsSelected = false;

            if (directoryViewModel == null)
            {
                //WTF
                return;
            }

            if (!directoryViewModel.Entries.IsLoaded)
                await directoryViewModel.Entries.LoadAsync();
            
            directoryViewModel.Selection.IsSelected = true;
            directoryViewModel.BringIntoView();
        }

        private (DirectoryNodeViewModel, HierarchicalResult) FindRelatedViewModel(
            IEnumerable<DirectoryNodeViewModel> entries, DirectoryEntry directoryEntry)
        {
            return entries
                .Select(directoryVm => (directoryVm,
                    _fileExplorerPathComparer.CompareHierarchy(directoryVm.Source, directoryEntry)))
                .Where(x => (x.Item2 & HierarchicalResult.Related) != 0 && x.Item2 != HierarchicalResult.Parent)
                .OrderBy(x => x.directoryVm.Source.Path.Length).FirstOrDefault();
        }

        private DirectoryNodeViewModel UpwardSelect(string path, DirectoryNodeViewModel directoryNodeViewModel)
        {
            while (true)
            {
                directoryNodeViewModel = directoryNodeViewModel.Parent;

                if (directoryNodeViewModel == null)
                    return null;

                if (ComparePaths(directoryNodeViewModel.Source.Path, path))
                    return directoryNodeViewModel;
            }
        }

        private async Task<DirectoryNodeViewModel> DownwardSelect(IReadOnlyList<DirectoryEntry> entries, int index, DirectoryNodeViewModel directoryNodeViewModel)
        {
            var currentEntry = entries[index];
            if (!directoryNodeViewModel.Entries.IsLoaded)
                await directoryNodeViewModel.Entries.LoadAsync();

            foreach (var viewModel in directoryNodeViewModel.Entries.All)
            {
                if (ComparePaths(currentEntry.Path, viewModel.Source.Path))
                {
                    if (index == entries.Count - 1) //we are at the end
                        return viewModel;

                    var result = await DownwardSelect(entries, index + 1, viewModel);
                    viewModel.Entries.IsExpanded = true;
                    return result;
                }
            }

            return null;
        }

        private bool ComparePaths(string path1, string path2) =>
            _fileExplorerViewModel.FileSystem.ComparePaths(path1, path2);
    }
}