﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Tel.Egram.Components.Messenger.Catalog;

namespace Tel.Egram.Gui.Views.Messenger.Catalog
{
    public class CatalogControl : ReactiveUserControl<CatalogModel>
    {
        public CatalogControl()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
