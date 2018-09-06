﻿using System;
using Anapher.Wpf.Swan.ViewInterface;
using Autofac;

namespace Orcus.Administration.Library.Services
{
    public interface IWindowService
    {
        void Show(Type viewModelType, string title, IWindow owner, Action<IShellWindow> configureWindow,
            Action<ContainerBuilder> setupContainer);

        void Show(object viewModel, string title, IWindow owner, Action<IShellWindow> configureWindow,
            Action<ContainerBuilder> setupContainer);

        bool? ShowDialog(Type viewModelType, string title, IWindow owner,
            Action<IShellWindow> configureWindow, Action<ContainerBuilder> setupContainer);

        bool? ShowDialog(object viewModel, string title, IWindow owner, Action<IShellWindow> configureWindow,
            Action<ContainerBuilder> setupContainer);
    }
}