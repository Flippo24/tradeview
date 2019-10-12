﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevelopmentInProgress.MarketView.Interface.Events;
using DevelopmentInProgress.MarketView.Interface.Interfaces;
using DevelopmentInProgress.MarketView.Interface.Enums;
using DevelopmentInProgress.Wpf.Common.Extensions;
using DevelopmentInProgress.Wpf.Common.Model;
using Interface = DevelopmentInProgress.MarketView.Interface.Model;

namespace DevelopmentInProgress.Wpf.Common.Services
{
    public class WpfExchangeService : IWpfExchangeService
    {
        private IExchangeService exchangeService;

        public WpfExchangeService(IExchangeService exchangeService)
        {
            this.exchangeService = exchangeService;
        }
        
        public async Task<IEnumerable<Symbol>> GetSymbols24HourStatisticsAsync(Exchange exchange, CancellationToken cancellationToken)
        {
            var symbols = await GetSymbolsAsync(exchange, cancellationToken).ConfigureAwait(false);
            var stats = await Get24HourStatisticsAsync(exchange, cancellationToken).ConfigureAwait(false);

            var updatedSymbols = (from sy in symbols
                                 join st in stats on sy.Name equals st.Symbol
                                 select sy.JoinStatistics(st)).ToList();

            return updatedSymbols;
        }

        public void SubscribeStatistics(Exchange exchange, IEnumerable<Symbol> symbols, Action<Exception> exception, CancellationToken cancellationToken)
        {
            exchangeService.SubscribeStatistics(exchange, e =>
            {
                var stats = e.Statistics.ToList();

                (from sy in symbols join st in stats on sy.Name equals st.Symbol select sy.UpdateStatistics(st)).ToList();

            }, exception, cancellationToken);
        }

        public async Task<IEnumerable<Order>> GetOpenOrdersAsync(Exchange exchange, Interface.User user, string symbol = null, long recWindow = 0, Action<Exception> exception = default(Action<Exception>), CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await exchangeService.GetOpenOrdersAsync(exchange, user, symbol, recWindow, exception, cancellationToken).ConfigureAwait(false);
            var orders = result.Select(o => o.GetViewOrder()).ToList();
            return orders;
        }

        public async Task<Account> GetAccountInfoAsync(Exchange exchange, string apiKey, string apiSecret, CancellationToken cancellationToken)
        {
            var accountInfo = await exchangeService.GetAccountInfoAsync(exchange, new Interface.User { ApiKey = apiKey, ApiSecret = apiSecret}, cancellationToken).ConfigureAwait(false);
            return new Account(accountInfo);
        }

        private async Task<IEnumerable<Symbol>> GetSymbolsAsync(Exchange exchange, CancellationToken cancellationToken)
        {
            var results = await exchangeService.GetSymbolsAsync(exchange, cancellationToken).ConfigureAwait(false);
            var symbols = results.Select(s => s.GetViewSymbol()).ToList();
            return symbols;
        }

        private async Task<IEnumerable<SymbolStatistics>> Get24HourStatisticsAsync(Exchange exchange, CancellationToken cancellationToken)
        {
            var results = await exchangeService.Get24HourStatisticsAsync(exchange, cancellationToken).ConfigureAwait(false);
            var symbols = results.Select(s => s.GetViewSymbolStatistics()).ToList();
            return symbols;
        }
        
        public async Task<Interface.Order> PlaceOrder(Exchange exchange, Interface.User user, Interface.ClientOrder clientOrder, long recWindow = 0, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await exchangeService.PlaceOrder(exchange, user, clientOrder, recWindow, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task<string> CancelOrderAsync(Exchange exchange, Interface.User user, string symbol, string orderId, string newClientOrderId = null, long recWindow = 0, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await exchangeService.CancelOrderAsync(exchange, user, symbol, orderId, newClientOrderId, recWindow, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task<IEnumerable<Interface.AggregateTrade>> GetAggregateTradesAsync(Exchange exchange, string symbol, int limit, CancellationToken cancellationToken)
        {
            var aggregateTrades = await exchangeService.GetAggregateTradesAsync(exchange, symbol, limit, cancellationToken).ConfigureAwait(false);
            return aggregateTrades;
        }

        public async Task<IEnumerable<Interface.Trade>> GetTradesAsync(Exchange exchange, string symbol, int limit, CancellationToken cancellationToken)
        {
            var trades = await exchangeService.GetTradesAsync(exchange, symbol, limit, cancellationToken).ConfigureAwait(false);
            return trades;
        }

        public async Task<Interface.OrderBook> GetOrderBookAsync(Exchange exchange, string symbol, int limit, CancellationToken cancellationToken)
        {
            var orderBook = await exchangeService.GetOrderBookAsync(exchange, symbol, limit, cancellationToken).ConfigureAwait(false);
            return orderBook;
        }

        public async Task<IEnumerable<Interface.AccountTrade>> GetAccountTradesAsync(Exchange exchange, Interface.User user, string symbol, DateTime startDate, DateTime endDate, long recWindow = 0, CancellationToken cancellationToken = default(CancellationToken))
        {
            var accountTrades = await exchangeService.GetAccountTradesAsync(exchange, user, symbol, startDate, endDate, recWindow, cancellationToken);
            return accountTrades;
        }

        public async Task<IEnumerable<Candlestick>> GetCandlesticksAsync(Exchange exchange, string symbol, Interface.CandlestickInterval interval, DateTime startTime, DateTime endTime, int limit = default(int), CancellationToken token = default(CancellationToken))
        {
            var results = await exchangeService.GetCandlesticksAsync(exchange, symbol, interval, startTime, endTime, limit, token).ConfigureAwait(false);
            var candlesticks = results.Select(c => c.ToViewCandlestick()).ToList();
            return candlesticks;
        }

        public void SubscribeCandlesticks(Exchange exchange, string symbol, Interface.CandlestickInterval candlestickInterval, int limit, Action<CandlestickEventArgs> callback, Action<Exception> exception, CancellationToken cancellationToken)
        {
            exchangeService.SubscribeCandlesticks(exchange, symbol, candlestickInterval, limit, callback, exception, cancellationToken);
        }

        public void SubscribeOrderBook(Exchange exchange, string symbol, int limit, Action<OrderBookEventArgs> callback, Action<Exception> exception, CancellationToken cancellationToken)
        {
            exchangeService.SubscribeOrderBook(exchange, symbol, limit, callback, exception, cancellationToken);
        }

        public void SubscribeAggregateTrades(Exchange exchange, string symbol, int limit, Action<TradeEventArgs> callback, Action<Exception> exception, CancellationToken cancellationToken)
        {
            exchangeService.SubscribeAggregateTrades(exchange, symbol, limit, callback, exception, cancellationToken);
        }

        public void SubscribeTrades(Exchange exchange, string symbol, int limit, Action<TradeEventArgs> callback, Action<Exception> exception, CancellationToken cancellationToken)
        {
            exchangeService.SubscribeTrades(exchange, symbol, limit, callback, exception, cancellationToken);
        }

        public void SubscribeAccountInfo(Exchange exchange, Interface.User user, Action<AccountInfoEventArgs> callback, Action<Exception> exception, CancellationToken cancellationToken)
        {
            exchangeService.SubscribeAccountInfo(exchange, user, callback, exception, cancellationToken);
        }
    }
}