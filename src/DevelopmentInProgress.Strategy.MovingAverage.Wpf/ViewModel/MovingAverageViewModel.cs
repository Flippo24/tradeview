﻿using CommonServiceLocator;
using DevelopmentInProgress.Strategy.Common.StrategyTrade;
using DevelopmentInProgress.TradeView.Core.Extensions;
using DevelopmentInProgress.TradeView.Core.Interfaces;
using DevelopmentInProgress.TradeView.Core.TradeStrategy;
using DevelopmentInProgress.TradeView.Wpf.Common.Chart;
using DevelopmentInProgress.TradeView.Wpf.Common.Extensions;
using DevelopmentInProgress.TradeView.Wpf.Common.Helpers;
using DevelopmentInProgress.TradeView.Wpf.Common.Model;
using DevelopmentInProgress.TradeView.Wpf.Common.ViewModel;
using LiveCharts;
using Newtonsoft.Json;
using Prism.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WpfStrategy = DevelopmentInProgress.TradeView.Wpf.Common.Model.Strategy;

namespace DevelopmentInProgress.Strategy.MovingAverage.Wpf.ViewModel
{
    public class MovingAverageViewModel : StrategyDisplayViewModelBase
    {
        private readonly SemaphoreSlim tradesSemaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim candlestickSemaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim orderBookSemaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ITradeHelperFactory tradeHelperFactory;
        private readonly IOrderBookHelperFactory orderBookHelperFactory;

        private ChartValues<Trade> tradesChart;
        private ChartValues<Trade> smaTradesChart;
        private ChartValues<Trade> buyIndicatorChart;
        private ChartValues<Trade> sellIndicatorChart;
        private ChartValues<Candlestick> candlesticksChart;
        private ObservableCollection<string> candlestickLabels;
        private List<Trade> trades;
        private OrderBook orderBook;
        private bool isLoadingTrades;
        private bool isLoadingOrderBook;
        private bool disposed;
        private bool showCandlesticks;

        public MovingAverageViewModel(WpfStrategy strategy, IHelperFactoryContainer iHelperFactoryContainer, 
            Dispatcher UiDispatcher, ILoggerFacade logger)
            : base(strategy, iHelperFactoryContainer, UiDispatcher, logger)
        {
            var chartHelper = ServiceLocator.Current.GetInstance<IChartHelper>();
            TimeFormatter = chartHelper.TimeFormatter;
            PriceFormatter = chartHelper.PriceFormatter;

            IsActive = false;
            IsLoadingTrades = true;
            IsLoadingOrderBook = true;

            cancellationTokenSource = new CancellationTokenSource();

            tradeHelperFactory = HelperFactoryContainer.GetFactory<ITradeHelperFactory>();

            orderBookHelperFactory = HelperFactoryContainer.GetFactory<IOrderBookHelperFactory>();

            ShowCandlesticks = Strategy.StrategySubscriptions.Any(s => s.SubscribeCandlesticks);
        }

        public Func<double, string> TimeFormatter { get; set; }
        public Func<double, string> PriceFormatter { get; set; }

        public bool IsLoadingTrades
        {
            get { return isLoadingTrades; }
            set
            {
                if (isLoadingTrades != value)
                {
                    isLoadingTrades = value;
                    OnPropertyChanged(nameof(IsLoadingTrades));
                }
            }
        }

        public bool IsLoadingOrderBook
        {
            get { return isLoadingOrderBook; }
            set
            {
                if (isLoadingOrderBook != value)
                {
                    isLoadingOrderBook = value;
                    OnPropertyChanged(nameof(IsLoadingOrderBook));
                }
            }
        }

        public bool ShowCandlesticks
        {
            get { return showCandlesticks; }
            set
            {
                if (showCandlesticks != value)
                {
                    showCandlesticks = value;
                    OnPropertyChanged(nameof(ShowCandlesticks));
                }
            }
        }
        public ChartValues<Trade> TradesChart
        {
            get { return tradesChart; }
            set
            {
                if (tradesChart != value)
                {
                    tradesChart = value;
                    OnPropertyChanged(nameof(TradesChart));
                }
            }
        }

        public ChartValues<Trade> SmaTradesChart
        {
            get { return smaTradesChart; }
            set
            {
                if (smaTradesChart != value)
                {
                    smaTradesChart = value;
                    OnPropertyChanged(nameof(SmaTradesChart));
                }
            }
        }

        public ChartValues<Trade> BuyIndicatorChart
        {
            get { return buyIndicatorChart; }
            set
            {
                if (buyIndicatorChart != value)
                {
                    buyIndicatorChart = value;
                    OnPropertyChanged(nameof(BuyIndicatorChart));
                }
            }
        }

        public ChartValues<Trade> SellIndicatorChart
        {
            get { return sellIndicatorChart; }
            set
            {
                if (sellIndicatorChart != value)
                {
                    sellIndicatorChart = value;
                    OnPropertyChanged(nameof(SellIndicatorChart));
                }
            }
        }

        public ChartValues<Candlestick> CandlesticksChart
        {
            get { return candlesticksChart; }
            set
            {
                if (candlesticksChart != value)
                {
                    candlesticksChart = value;
                    OnPropertyChanged(nameof(CandlesticksChart));
                }
            }
        }

        public ObservableCollection<string> CandlestickLabels
        {
            get { return candlestickLabels; }
            set
            {
                candlestickLabels = value;
                OnPropertyChanged(nameof(CandlestickLabels));
            }
        }

        public List<Trade> Trades
        {
            get { return trades; }
            set
            {
                if (trades != value)
                {
                    trades = value;
                    OnPropertyChanged(nameof(Trades));
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
                    OnPropertyChanged(nameof(OrderBook));
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                tradesSemaphoreSlim.Dispose();
                candlestickSemaphoreSlim.Dispose();
                candlestickSemaphoreSlim.Dispose();
                orderBookSemaphoreSlim.Dispose();
            }

            disposed = true;
        }

        public override async Task TradeNotificationsAsync(List<StrategyNotification> tradeNotifications)
        {
            if(tradeNotifications == null)
            {
                throw new ArgumentNullException(nameof(tradeNotifications));
            }

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            await tradesSemaphoreSlim.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(true);

            try
            {
                if(cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                if (Symbols != null)
                {
                    IsLoadingTrades = false;

                    List<MovingAverageTrade> tradesUpdate = null;

                    foreach (var notification in tradeNotifications)
                    {
                        if (tradesUpdate == null)
                        {
                            tradesUpdate = JsonConvert.DeserializeObject<List<MovingAverageTrade>>(notification.Message);
                            continue;
                        }

                        var updateTrades = JsonConvert.DeserializeObject<List<MovingAverageTrade>>(notification.Message);
                        var newTrades = updateTrades.Except(tradesUpdate).ToList();
                        tradesUpdate.AddRange(newTrades.OrderBy(t => t.Time));
                    }

                    var trade = tradesUpdate.First();

                    var symbol = Symbols.First(s => s.ExchangeSymbol.Equals(trade.Symbol, StringComparison.Ordinal));

                    var pricePrecision = symbol.PricePrecision;
                    var quantityPrecision = symbol.QuantityPrecision;

                    var tradeHelper = tradeHelperFactory.GetTradeHelper(trade.Exchange);

                    Func<ITrade, int, int, Trade> createSmaTrade = (t, p, q) => new Trade { Price = ((MovingAverageTrade)t).MovingAveragePrice.Trim(p), Time = t.Time.ToLocalTime(), Exchange = t.Exchange };
                    Func<ITrade, int, int, Trade> createBuyIndicator = (t, p, q) => new Trade { Price = ((MovingAverageTrade)t).BuyPrice.Trim(p), Time = t.Time.ToLocalTime(), Exchange = t.Exchange };
                    Func<ITrade, int, int, Trade> createSellIndicator = (t, p, q) => new Trade { Price = ((MovingAverageTrade)t).SellPrice.Trim(p), Time = t.Time.ToLocalTime(), Exchange = t.Exchange };

                    var tradesDisplayCount = Strategy.TradesDisplayCount;
                    var tradesChartDisplayCount = Strategy.TradesChartDisplayCount;

                    if (TradesChart == null)
                    {
                        var result = await tradeHelper.CreateLocalTradeList<Trade>(symbol, tradesUpdate, tradesDisplayCount, tradesChartDisplayCount, 0).ConfigureAwait(true);

                        Trades = result.Trades;
                        TradesChart = result.TradesChart;

                        SmaTradesChart = tradeHelper.CreateLocalChartTrades(tradesUpdate, createSmaTrade, tradesChartDisplayCount, pricePrecision, quantityPrecision);

                        BuyIndicatorChart = tradeHelper.CreateLocalChartTrades(tradesUpdate, createBuyIndicator, tradesChartDisplayCount, pricePrecision, quantityPrecision);

                        SellIndicatorChart = tradeHelper.CreateLocalChartTrades(tradesUpdate, createSellIndicator, tradesChartDisplayCount, pricePrecision, quantityPrecision);
                    }
                    else
                    {
                        List<Trade> newTrades;

                        // Get the latest available trade - the first trade on the 
                        // trade list (which is also the last trade in the chart).
                        var seed = Trades.First();
                        var seedTime = seed.Time;
                        var seedId = seed.Id;

                        tradeHelper.UpdateTrades(symbol, tradesUpdate, Trades, tradesDisplayCount, tradesChartDisplayCount, out newTrades, ref tradesChart);

                        Trades = newTrades;

                        tradeHelper.UpdateLocalChartTrades(tradesUpdate, createSmaTrade, seedTime, seedId, tradesChartDisplayCount, pricePrecision, quantityPrecision, ref smaTradesChart);

                        tradeHelper.UpdateLocalChartTrades(tradesUpdate, createBuyIndicator, seedTime, seedId, tradesChartDisplayCount, pricePrecision, quantityPrecision, ref buyIndicatorChart);

                        tradeHelper.UpdateLocalChartTrades(tradesUpdate, createSellIndicator, seedTime, seedId, tradesChartDisplayCount, pricePrecision, quantityPrecision, ref sellIndicatorChart);
                    }
                }
            }
            catch(Exception ex)
            {
                OnException($"{Strategy.Name} : TradeNotificationsAsync - {ex.Message}", ex);
            }
            finally
            {
                tradesSemaphoreSlim.Release();
            }
        }

        public override async Task CandlestickNotificationsAsync(List<StrategyNotification> candlestickNotifications)
        {
            if(cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            await candlestickSemaphoreSlim.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(true);

            try
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                var candlestickNotification = candlestickNotifications.Last();

                var cs = JsonConvert.DeserializeObject<List<TradeView.Core.Model.Candlestick>>(candlestickNotification.Message);

                Candlestick last = null;

                if (CandlesticksChart != null)
                {
                    last = CandlesticksChart.LastOrDefault();
                }

                if (last == null)
                {
                    var candlesticks = cs.OrderBy(c => c.OpenTime).Select(c => c.ToViewCandlestick()).ToList();

                    CandlesticksChart = new ChartValues<Candlestick>(candlesticks);

                    var labels = candlesticks.Select(c => c.CloseTime.ToString("H:mm:ss"));
                    CandlestickLabels = new ObservableCollection<string>(labels);
                }
                else
                {
                    var candlesticks = cs.Where(c => c.OpenTime.ToLocalTime() >= last.OpenTime)
                        .OrderBy(c => c.OpenTime)                        
                        .ToList();

                    var first = candlesticks.FirstOrDefault();

                    if (first != null
                        && first.OpenTime.ToLocalTime().Equals(last.OpenTime))
                    {
                        last.High = first.High;
                        last.Low = first.Low;
                        last.Close = first.Close;
                        last.CloseTime = first.CloseTime.ToLocalTime();
                        last.Volume = first.Volume;
                        last.QuoteAssetVolume = first.QuoteAssetVolume;
                        last.TakerBuyBaseAssetVolume = first.TakerBuyBaseAssetVolume;
                        last.TakerBuyQuoteAssetVolume = first.TakerBuyQuoteAssetVolume;
                        last.NumberOfTrades = first.NumberOfTrades;
                    }

                    if (candlesticks.Count > 1)
                    {
                        var newCandlesticks = candlesticks.Skip(1).Select(c => c.ToViewCandlestick()).ToList();

                        CandlesticksChart.AddRange(newCandlesticks);

                        var labels = newCandlesticks.Select(c => c.CloseTime.ToString("H:mm:ss"));
                        CandlestickLabels.AddRange(labels);
                    }
                }
            }
            catch (Exception ex)
            {
                OnException($"{Strategy.Name} : CandlestickNotificationsAsync - {ex.Message}", ex);
            }
            finally
            {
                candlestickSemaphoreSlim.Release();
            }

            await Task.FromResult<object>(null).ConfigureAwait(false);
        }

        public override async Task OrderNotificationsAsync(List<StrategyNotification> orderNotifications)
        {
            if (orderNotifications == null)
            {
                throw new ArgumentNullException(nameof(orderNotifications));
            }

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            await orderBookSemaphoreSlim.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(true);

            try
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                if (Symbols != null)
                {
                    IsLoadingOrderBook = false;

                    var orderBookDisplayCount = Strategy.OrderBookDisplayCount;
                    var orderBookChartDisplayCount = Strategy.OrderBookChartDisplayCount;

                    if (OrderBook == null)
                    {
                        var orderBookNotification = orderNotifications.Last();
                        
                        var ob = JsonConvert.DeserializeObject<TradeView.Core.Model.OrderBook>(orderBookNotification.Message);

                        var orderBookHelper = orderBookHelperFactory.GetOrderBookHelper(ob.Exchange);

                        var symbol = Symbols.First(s => s.ExchangeSymbol.Equals(ob.Symbol, StringComparison.Ordinal));
                        var pricePrecision = symbol.PricePrecision;
                        var quantityPrecision = symbol.QuantityPrecision;

                        OrderBook = await orderBookHelper.CreateLocalOrderBook(symbol, ob, orderBookDisplayCount, orderBookChartDisplayCount).ConfigureAwait(true);
                    }
                    else
                    {
                        bool first = true;
                        IOrderBookHelper orderBookHelper = null;
                        Symbol symbol = null;
                        int quantityPrecision;
                        int pricePrecision;

                        var orderBookUpdates = new List<TradeView.Core.Model.OrderBook>();

                        foreach (var notification in orderNotifications)
                        {
                            var ob = JsonConvert.DeserializeObject<TradeView.Core.Model.OrderBook>(notification.Message);

                            if (first)
                            {
                                orderBookHelper = orderBookHelperFactory.GetOrderBookHelper(ob.Exchange);

                                symbol = Symbols.First(s => s.ExchangeSymbol.Equals(ob.Symbol, StringComparison.Ordinal));
                                quantityPrecision = symbol.QuantityPrecision;
                                pricePrecision = symbol.PricePrecision;
                            }

                            orderBookUpdates.Add(ob);
                        }

                        var newOrderBookUpdates 
                            = orderBookUpdates
                            .OrderBy(ob => ob.LastUpdateId)
                            .Where(ob => ob.LastUpdateId > OrderBook.LastUpdateId).ToList();

                        foreach(var ob in newOrderBookUpdates)
                        {
                            orderBookHelper.UpdateLocalOrderBook(OrderBook, ob,
                                symbol.PricePrecision, symbol.QuantityPrecision,
                                orderBookDisplayCount, orderBookChartDisplayCount);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnException($"{Strategy.Name} : OrderNotificationsAsync - {ex.Message}", ex);
            }
            finally
            {
                orderBookSemaphoreSlim.Release();
            }

            await Task.FromResult<object>(null).ConfigureAwait(false);
        }
    }
}
