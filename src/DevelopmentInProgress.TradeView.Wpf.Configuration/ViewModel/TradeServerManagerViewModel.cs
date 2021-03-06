﻿using DevelopmentInProgress.TradeView.Wpf.Common.Events;
using DevelopmentInProgress.TradeView.Wpf.Common.Model;
using DevelopmentInProgress.TradeView.Wpf.Common.Services;
using DevelopmentInProgress.TradeView.Wpf.Controls.Messaging;
using DevelopmentInProgress.TradeView.Wpf.Host.Controller.Context;
using DevelopmentInProgress.TradeView.Wpf.Host.Controller.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;

namespace DevelopmentInProgress.TradeView.Wpf.Configuration.ViewModel
{
    public class TradeServerManagerViewModel : DocumentViewModel
    {
        private ITradeServerService tradeServerService;
        private ObservableCollection<TradeServer> tradeServers;
        private TradeServerViewModel selectedTradeServerViewModel;
        private Dictionary<string, IDisposable> tradeServerObservableSubscriptions;
        private TradeServer selectedTradeServer;
        private bool isLoading;
        private bool disposed;

        public TradeServerManagerViewModel(ViewModelContext viewModelContext, ITradeServerService tradeServerService)
            : base(viewModelContext)
        {
            this.tradeServerService = tradeServerService;

            AddTradeServerCommand = new ViewModelCommand(AddTradeServer);
            DeleteTradeServerCommand = new ViewModelCommand(DeleteTradeServer);
            CloseCommand = new ViewModelCommand(Close);

            SelectedTradeServerViewModels = new ObservableCollection<TradeServerViewModel>();
            tradeServerObservableSubscriptions = new Dictionary<string, IDisposable>();
        }

        public ICommand AddTradeServerCommand { get; set; }
        public ICommand DeleteTradeServerCommand { get; set; }
        public ICommand CloseCommand { get; set; }

        public ObservableCollection<TradeServer> TradeServers 
        {
            get { return tradeServers; } 
            set
            {
                if(tradeServers != value)
                {
                    tradeServers = value;
                    OnPropertyChanged("TradeServers");
                }
            }
        }

        public ObservableCollection<TradeServerViewModel> SelectedTradeServerViewModels { get; set; }

        public bool IsLoading
        {
            get { return isLoading; }
            set
            {
                if (isLoading != value)
                {
                    isLoading = value;
                    OnPropertyChanged("IsLoading");
                }
            }
        }

        public TradeServer SelectedTradeServer
        {
            get { return selectedTradeServer; }
            set
            {
                if (selectedTradeServer != value)
                {
                    selectedTradeServer = value;

                    if (selectedTradeServer != null)
                    {
                        var serverViewModel = SelectedTradeServerViewModels.FirstOrDefault(s => s.TradeServer.Name.Equals(selectedTradeServer.Name));

                        if (serverViewModel == null)
                        {
                            serverViewModel = new TradeServerViewModel(selectedTradeServer, tradeServerService, Logger);
                            ObserveServer(serverViewModel);
                            SelectedTradeServerViewModels.Add(serverViewModel);
                            SelectedTradeServerViewModel = serverViewModel;
                        }
                        else
                        {
                            SelectedTradeServerViewModel = serverViewModel;
                        }
                    }

                    OnPropertyChanged("SelectedTradeServer");
                }
            }
        }

        public TradeServerViewModel SelectedTradeServerViewModel
        {
            get { return selectedTradeServerViewModel; }
            set
            {
                if (selectedTradeServerViewModel != value)
                {
                    selectedTradeServerViewModel = value;
                    OnPropertyChanged("SelectedTradeServerViewModel");
                }
            }
        }

        public void Close(object param)
        {
            var tradeServer = param as TradeServerViewModel;
            if (tradeServer != null)
            {
                tradeServer.Dispose();

                IDisposable subscription;
                if (tradeServerObservableSubscriptions.TryGetValue(tradeServer.TradeServer.Name, out subscription))
                {
                    subscription.Dispose();
                }

                tradeServerObservableSubscriptions.Remove(tradeServer.TradeServer.Name);
                
                SelectedTradeServerViewModels.Remove(tradeServer);
            }
        }

        protected async override void OnPublished(object data)
        {
            base.OnPublished(data);

            try
            {
                IsLoading = true;

                var tradeServers = await tradeServerService.GetTradeServers();

                TradeServers = new ObservableCollection<TradeServer>(tradeServers);
            }
            catch (Exception ex)
            {
                ShowMessage(new Message { MessageType = MessageType.Error, Text = ex.Message });
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected override void OnDisposing()
        {
            if(disposed)
            {
                return;           
            }

            foreach (var subscription in tradeServerObservableSubscriptions.Values)
            {
                subscription.Dispose();
            }

            foreach (var tradeServerViewModel in SelectedTradeServerViewModels)
            {
                tradeServerViewModel.Dispose();
            }

            disposed = true;
        }

        protected async override void SaveDocument()
        {
            try
            {
                IsLoading = true;

                foreach (var serverViewModel in SelectedTradeServerViewModels)
                {
                    await tradeServerService.SaveTradeServer(serverViewModel.TradeServer);
                }
            }
            catch (Exception ex)
            {
                ShowMessage(new Message { MessageType = MessageType.Error, Text = ex.Message });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void AddTradeServer(object param)
        {
            if (param == null
                || string.IsNullOrEmpty(param.ToString()))
            {
                return;
            }

            var tradeServerName = param.ToString();

            if (TradeServers.Any(s => s.Name.Equals(tradeServerName)))
            {
                ShowMessage(new Message { MessageType = MessageType.Info, Text = $"A trade server with the name {tradeServerName} already exists." });
                return;
            }

            try
            {
                IsLoading = true;

                var tradeServer = new TradeServer { Name = tradeServerName };
                await tradeServerService.SaveTradeServer(tradeServer);
                TradeServers.Add(tradeServer);
            }
            catch (Exception ex)
            {
                ShowMessage(new Message { MessageType = MessageType.Error, Text = ex.Message });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void DeleteTradeServer(object param)
        {
            var tradeServer = param as TradeServer;
            if (tradeServer == null)
            {
                return;
            }

            var result = Dialog.ShowMessage(new MessageBoxSettings
            {
                Title = "Delete Trade Server",
                Text = $"Are you sure you want to delete {tradeServer.Name}?",
                MessageType = MessageType.Question,
                MessageBoxButtons = MessageBoxButtons.OkCancel
            });

            if (result.Equals(MessageBoxResult.Cancel))
            {
                return;
            }

            var tradeServerViewModel = SelectedTradeServerViewModels.FirstOrDefault(s => s.TradeServer.Name.Equals(tradeServer.Name));
            if(tradeServerViewModel != null)
            {
                Close(tradeServerViewModel);
            }

            try
            {
                IsLoading = true;

                await tradeServerService.DeleteTradeServer(tradeServer);
                TradeServers.Remove(tradeServer);
            }
            catch (Exception ex)
            {
                ShowMessage(new Message { MessageType = MessageType.Error, Text = ex.Message });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ObserveServer(TradeServerViewModel tradeServer)
        {
            var tradeServerObservable = Observable.FromEventPattern<TradeServerEventArgs>(
                eventHandler => tradeServer.OnTradeServerNotification += eventHandler,
                eventHandler => tradeServer.OnTradeServerNotification -= eventHandler)
                .Select(eventPattern => eventPattern.EventArgs);

            var tradeServerObservableSubscription = tradeServerObservable.Subscribe(args =>
            {
                if (args.HasException)
                {
                    ShowMessage(new Message { MessageType = MessageType.Error, Text = args.Exception.ToString() });
                }
            });

            tradeServerObservableSubscriptions.Add(tradeServer.TradeServer.Name, tradeServerObservableSubscription);
        }
    }
}