﻿using MahApps.Metro.IconPacks;

namespace Orcus.Administration.Library.ViewModels
{
    public class ClientListBase : ViewModelBase
    {
        public ClientListBase(string name, PackIconFontAwesomeKind icon)
        {
            Name = name;
            Icon = icon;
        }

        public string Name { get; }
        public PackIconFontAwesomeKind Icon { get; }
    }
}