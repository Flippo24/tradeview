﻿using DevelopmentInProgress.Wpf.MarketView.Model;
using DevelopmentInProgress.Wpf.MarketView.Services;
using LiveCharts;
using LiveCharts.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Interface = DevelopmentInProgress.MarketView.Interface.Model;

namespace DevelopmentInProgress.Wpf.MarketView.ViewModel
{
    public class SymbolViewModel : BaseViewModel
    {
        private CancellationTokenSource symbolCancellationTokenSource;
        private Action<Exception> exception;
        private Symbol symbol;
        private OrderBook orderBook;
        private ChartValues<AggregateTrade> aggregateTrades;
        private object orderBookLock = new object();
        private object aggregateTradesLock = new object();
        private bool isLoadingTrades;
        private bool isLoadingOrderBook;
        private bool disposed;

        private int limit = 20;

        public SymbolViewModel(Symbol symbol, IExchangeService exchangeService, Action<Exception> exception)
            : base(exchangeService)
        {
            Symbol = symbol;

            this.exception = exception;

            var mapper = Mappers.Xy<AggregateTrade>()
                .X(model => model.Time.Ticks)
                .Y(model => Convert.ToDouble(model.Price));

            Charting.For<AggregateTrade>(mapper);

            TimeFormatter = value => new DateTime((long)value).ToString("H:mm:ss");
            PriceFormatter = value => value.ToString("0.00000000");

            IsLoadingTrades = true;
            IsLoadingOrderBook = true;

            symbolCancellationTokenSource = new CancellationTokenSource();

            GetSymbol();
        }
        
        public Func<double, string> TimeFormatter { get; set; }

        public Func<double, string> PriceFormatter { get; set; }

        public bool IsLoadingOrderBook
        {
            get { return isLoadingOrderBook; }
            set
            {
                if (isLoadingOrderBook != value)
                {
                    isLoadingOrderBook = value;
                    OnPropertyChanged("IsLoadingOrderBook");
                }
            }
        }

        public bool IsLoadingTrades
        {
            get { return isLoadingTrades; }
            set
            {
                if (isLoadingTrades != value)
                {
                    isLoadingTrades = value;
                    OnPropertyChanged("IsLoadingTrades");
                }
            }
        }

        public Symbol Symbol
        {
            get { return symbol; }
            private set
            {
                if (symbol != value)
                {
                    symbol = value;
                    OnPropertyChanged("Symbol");
                }
            }
        }

        public ChartValues<AggregateTrade> AggregateTrades
        {
            get { return aggregateTrades; }
            set
            {
                if (aggregateTrades != value)
                {
                    aggregateTrades = value;
                    OnPropertyChanged("AggregateTrades");
                }
            }
        }

        public OrderBook OrderBook
        {
            get { return orderBook; }
            set
            {
                if (orderBook != value)
                {
                    orderBook = value;
                    OnPropertyChanged("OrderBook");
                }
            }
        }

        public async void GetSymbol()
        {
            try
            {
                var tasks = new List<Task>();
                tasks.Add(Task.Run(() => GetOrderBook()));
                tasks.Add(Task.Run(() => GetTrades()));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                exception.Invoke(ex);
            }
        }

        private async Task GetOrderBook()
        {
            try
            {
                var orderBook = await ExchangeService.GetOrderBookAsync(Symbol.Name, limit, symbolCancellationTokenSource.Token);

                UpdateOrderBook(orderBook);

                ExchangeService.SubscribeOrderBook(Symbol.Name, limit, e => UpdateOrderBook(e.OrderBook), exception, symbolCancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                exception.Invoke(ex);
            }

            IsLoadingOrderBook = false;
        }

        private async Task GetTrades()
        {
            try
            {
                var trades = await ExchangeService.GetAggregateTradesAsync(Symbol.Name, limit, symbolCancellationTokenSource.Token);

                UpdateAggregateTrades(trades);

                ExchangeService.SubscribeAggregateTrades(Symbol.Name, limit, e => UpdateAggregateTrades(e.AggregateTrades), exception, symbolCancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                exception.Invoke(ex);
            }

            IsLoadingTrades = false;
        }

        private void UpdateOrderBook(Interface.OrderBook orderBook)
        {
            if (!Symbol.Name.Equals(orderBook.Symbol))
            {
                return;
            }

            lock (orderBookLock)
            {
                if (OrderBook == null
                    || OrderBook.Symbol != Symbol.Name)
                {
                    OrderBook = new OrderBook
                    {
                        Symbol = orderBook.Symbol,
                        BaseSymbol = Symbol.BaseAsset.Symbol,
                        QuoteSymbol = Symbol.QuoteAsset.Symbol
                    };
                }

                if (OrderBook.LastUpdateId < orderBook.LastUpdateId)
                {
                    OrderBook.LastUpdateId = orderBook.LastUpdateId;
                    OrderBook.Top = new OrderBookTop
                    {
                        Ask = new OrderBookPriceLevel { Price = orderBook.Top.Ask.Price, Quantity = orderBook.Top.Ask.Quantity },
                        Bid = new OrderBookPriceLevel { Price = orderBook.Top.Bid.Price, Quantity = orderBook.Top.Bid.Quantity }
                    };

                    var asks = new List<OrderBookPriceLevel>((from ask in orderBook.Asks orderby ask.Price descending select new OrderBookPriceLevel { Price = ask.Price, Quantity = ask.Quantity }));
                    var bids = new List<OrderBookPriceLevel>((from bid in orderBook.Bids orderby bid.Price descending select new OrderBookPriceLevel { Price = bid.Price, Quantity = bid.Quantity }));

                    OrderBook.Asks = asks;
                    OrderBook.Bids = bids;
                }
            }
        }

        private void UpdateAggregateTrades(IEnumerable<Interface.AggregateTrade> trades)
        {
            lock (aggregateTradesLock)
            {
                if (AggregateTrades == null)
                {
                    var orderedTrades = (from t in trades orderby t.Time select new AggregateTrade { Id = t.Id, Time = t.Time, Price = t.Price, Quantity = t.Quantity, IsBuyerMaker = t.IsBuyerMaker });
                    AggregateTrades = new ChartValues<AggregateTrade>(orderedTrades);
                }
                else
                {
                    var maxId = AggregateTrades.Max(at => at.Id);
                    var orderedAggregateTrades = (from t in trades where t.Id > maxId orderby t.Time select new AggregateTrade { Id = t.Id, Time = t.Time, Price = t.Price, Quantity = t.Quantity, IsBuyerMaker = t.IsBuyerMaker }).ToList();
                    AggregateTrades.AddRange(orderedAggregateTrades);
                }
            }
        }

        public override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (symbolCancellationTokenSource != null
                    || !symbolCancellationTokenSource.IsCancellationRequested)
                {
                    symbolCancellationTokenSource.Cancel();
                }
            }

            disposed = true;
        }
    }
}