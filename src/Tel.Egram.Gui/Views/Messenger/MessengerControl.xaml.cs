﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Tel.Egram.Components.Messenger;

namespace Tel.Egram.Gui.Views.Messenger
{
    public class MessengerControl : ReactiveUserControl<MessengerModel>
    {
        public MessengerControl()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
