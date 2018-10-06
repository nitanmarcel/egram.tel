using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using TdLib;
using Tel.Egram.Feeds;
using Tel.Egram.Graphics;

namespace Tel.Egram.Components.Catalog
{
    public class CatalogProvider : ICatalogProvider
    {
        private readonly CompositeDisposable _serviceDisposable = new CompositeDisposable();

        private readonly Dictionary<long, ChatEntryModel> _entryStore;
        private readonly SourceCache<EntryModel, long> _chats;
        public IObservableCache<EntryModel, long> Chats => _chats;

        public CatalogProvider(
            IChatLoader chatLoader,
            IChatUpdater chatUpdater,
            IAvatarLoader avatarLoader
            )
        {
            _entryStore = new Dictionary<long, ChatEntryModel>();
            _chats = new SourceCache<EntryModel, long>(m => m.Id);
            
            LoadChats(chatLoader, avatarLoader)
                .DisposeWith(_serviceDisposable);
            BindOrderUpdates(chatLoader, chatUpdater, avatarLoader)
                .DisposeWith(_serviceDisposable);
//            BindEntryUpdates(chatLoader, chatUpdater, avatarLoader)
//                .DisposeWith(_serviceDisposable);
        }

        /// <summary>
        /// Load chats into observable cache
        /// </summary>
        private IDisposable LoadChats(IChatLoader chatLoader, IAvatarLoader avatarLoader)
        {
            return chatLoader.LoadChats()
                .Select(GetChatEntryModel)
                .Aggregate(new List<ChatEntryModel>(), (list, model) =>
                {
                    model.Order = list.Count;
                    list.Add(model);
                    return list;
                })
                .Synchronize(_chats)
                .Do(entries =>
                {
                    _chats.EditDiff(entries, (m1, m2) => m1.Id == m2.Id);
                    _chats.Refresh();
                })
                .SelectMany(entries => entries)
                .SelectMany(entry => LoadAvatar(avatarLoader, entry)
                    .Select(avatar => new
                    {
                        Entry = entry,
                        Avatar = avatar
                    }))
                .SubscribeOn(TaskPoolScheduler.Default)
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(item =>
                {
                    var entry = item.Entry;
                    var avatar = item.Avatar;
                    entry.Avatar = avatar;
                });
        }

        /// <summary>
        /// Subscribe to updates that involve order change
        /// </summary>
        private IDisposable BindOrderUpdates(
            IChatLoader chatLoader,
            IChatUpdater chatUpdater,
            IAvatarLoader avatarLoader
            )
        {
            return chatUpdater.GetOrderUpdates()
                .Buffer(TimeSpan.FromSeconds(1))
                .SubscribeOn(TaskPoolScheduler.Default)
                .ObserveOn(TaskPoolScheduler.Default)
                .Synchronize(_chats)
                .Subscribe(changes =>
                {
                    if (changes.Count > 0)
                    {
                        LoadChats(chatLoader, avatarLoader).DisposeWith(_serviceDisposable);
                    }
                });
        }

        /// <summary>
        /// Subscribe to updates for individual entries
        /// </summary>
        private IDisposable BindEntryUpdates(
            IChatLoader chatLoader,
            IChatUpdater chatUpdater,
            IAvatarLoader avatarLoader
            )
        {
            return chatUpdater.GetChatUpdates()
                .Buffer(TimeSpan.FromSeconds(1))
                .SelectMany(chats => chats)
                .Select(chat =>
                {
                    var entry = GetChatEntryModel(chat);
                    UpdateChatEntryModel(entry, chat);
                    return entry;
                })
                .SelectMany(entry => LoadAvatar(avatarLoader, entry)
                    .Select(avatar => new
                    {
                        Entry = entry,
                        Avatar = avatar
                    }))
                .SubscribeOn(TaskPoolScheduler.Default)
                .ObserveOn(TaskPoolScheduler.Default)
                .Synchronize(_chats)
                .Subscribe(item =>
                {
                    var entry = item.Entry;
                    var avatar = item.Avatar;
                    entry.Avatar = avatar;
                });
        }

        private IObservable<Avatar> LoadAvatar(IAvatarLoader avatarLoader, EntryModel entry)
        {
            if (entry.Avatar != null)
            {
                return Observable.Return(entry.Avatar);
            }
            
            switch (entry)
            {
                case ChatEntryModel chatEntry:
                    return avatarLoader.LoadAvatar(chatEntry.Chat.ChatData, AvatarSize.Small);
                
                case AggregateEntryModel aggregateEntry:
                    return avatarLoader.LoadAvatar(new TdApi.Chat
                        {
                            Id = aggregateEntry.Aggregate.Id
                        }, AvatarSize.Small);
            }
            
            return Observable.Return<Avatar>(null);
        }

        private ChatEntryModel GetChatEntryModel(Chat chat)
        {
            var chatData = chat.ChatData;
            
            if (!_entryStore.TryGetValue(chatData.Id, out var entry))
            {
                entry = new ChatEntryModel();
                UpdateChatEntryModel(entry, chat);
                
                _entryStore.Add(chatData.Id, entry);
            }

            return entry;
        }

        private void UpdateChatEntryModel(ChatEntryModel entry, Chat chat)
        {
            var chatData = chat.ChatData;
            
            entry.Chat = chat;
            entry.Id = chatData.Id;
            entry.Title = chatData.Title;
            entry.Avatar = null;
            entry.HasUnread = chatData.UnreadCount > 0;
            entry.UnreadCount = chatData.UnreadCount.ToString();
        }
    }
}