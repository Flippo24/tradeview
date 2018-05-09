﻿using DevelopmentInProgress.MarketView.Interface.Extensions;
using DevelopmentInProgress.MarketView.Interface.Model;
using DevelopmentInProgress.MarketView.Interface.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace DevelopmentInProgress.MarketView.Interface.Test
{
    [TestClass]
    public class ClientOrderValidationBuilderTest
    {
        private static List<Symbol> symbols;
        private static List<SymbolStats> statistics;

        private static Symbol dummyTrx;
        private static Symbol trx;
        private static SymbolStats trxStats;
        private static Symbol eth;
        private static SymbolStats ethStats;

        [ClassInitialize()]
        public static void ClientOrderValidationBuilderTest_Initializ(TestContext testContext)
        {
            var marketHelper = new MarketHelper();

            symbols = marketHelper.Symbols;
            statistics = marketHelper.SymbolsStatistics;

            eth = symbols.Single(s => s.BaseAsset.Symbol.Equals("ETH") && s.QuoteAsset.Symbol.Equals("BTC"));
            ethStats = statistics.Single(s => s.Symbol.Equals("ETHBTC"));

            trx = symbols.Single(s => s.BaseAsset.Symbol.Equals("TRX") && s.QuoteAsset.Symbol.Equals("BTC"));
            trxStats = statistics.Single(s => s.Symbol.Equals("TRXBTC"));

            dummyTrx = new Symbol
            {
                Quantity = new InclusiveRange { Minimum = trx.Quantity.Minimum, Maximum = trx.Quantity.Maximum, Increment = trx.Quantity.Increment },
                OrderTypes = trx.OrderTypes.Where(t => t != OrderType.Limit),
                NotionalMinimumValue = trx.NotionalMinimumValue,               
            };
        }

        [TestMethod]
        public void BaseValidation_Fail_NoSymbol_OrderType_MinQty_StepSize()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Type = OrderType.Limit, Quantity = 0.00090000M, Price = trxStats.LastPrice };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder().Build();
            var result = clientOrderValidation.TryValidate(dummyTrx, clientOrder, out message);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(message, $" {clientOrder.Type} order not valid: Order has no symbol;Limit order is not permitted;Quantity {clientOrder.Quantity} is below the minimum {dummyTrx.Quantity.Minimum};Quantity {clientOrder.Quantity} must be in multiples of the step size {dummyTrx.Quantity.Increment};Notional {clientOrder.Price * clientOrder.Quantity} is less than the minimum notional {dummyTrx.NotionalMinimumValue}");
        }

        [TestMethod]
        public void BaseValidation_Fail_NoSymbol_OrderType_MaxQty()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Type = OrderType.Limit, Quantity = 150000000.00000000M, Price = trxStats.LastPrice };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder().Build();
            var result = clientOrderValidation.TryValidate(dummyTrx, clientOrder, out message);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(message, $" {clientOrder.Type} order not valid: Order has no symbol;Limit order is not permitted;Quantity {clientOrder.Quantity} is above the maximum {dummyTrx.Quantity.Maximum}");
        }

        [TestMethod]
        public void BaseValidation_Failed_SymbolMismatch()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = trxStats.LastPrice };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder().Build();
            var result = clientOrderValidation.TryValidate(trx, clientOrder, out message);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(message, $"{clientOrder.Symbol} {clientOrder.Type} order not valid: Order {clientOrder.Symbol} validation symbol {trx.BaseAsset.Symbol}{trx.QuoteAsset.Symbol} mismatch");
        }

        [TestMethod]
        public void BaseValidation_Pass()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = trxStats.LastPrice };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder().Build();
            var result = clientOrderValidation.TryValidate(trx, clientOrder, out message);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(message, string.Empty);
        }

        [TestMethod]
        public void PriceValidation_Failed_MinPrice_TickSize()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = 0.000000001M };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder()
                .AddPriceValidation()
                .Build();

            var result = clientOrderValidation.TryValidate(trx, clientOrder, out message);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(message, $"{clientOrder.Symbol} {clientOrder.Type} order not valid: Notional {clientOrder.Price * clientOrder.Quantity} is less than the minimum notional {dummyTrx.NotionalMinimumValue};Price {clientOrder.Price} cannot be below the minimum {trx.Price.Minimum};Price {clientOrder.Price} doesn't meet the tick size {trx.Price.Increment}");
        }

        [TestMethod]
        public void PriceValidation_Failed_MaxPrice()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = 15000000.00000000M };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder()
                .AddPriceValidation()
                .Build();

            var result = clientOrderValidation.TryValidate(trx, clientOrder, out message);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(message, $"{clientOrder.Symbol} {clientOrder.Type} order not valid: Price {clientOrder.Price} cannot be above the maximum {trx.Price.Maximum}");
        }

        [TestMethod]
        public void PriceValidation_Pass()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = trxStats.LastPrice };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder()
                .AddPriceValidation()
                .Build();

            var result = clientOrderValidation.TryValidate(trx, clientOrder, out message);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(message, string.Empty);
        }

        [TestMethod]
        public void StopPriceValidation_Failed_MinPrice_TickSize()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = trxStats.LastPrice, StopPrice = 0.000000001M };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder()
                .AddPriceValidation()
                .AddStopPriceValidation()
                .Build();

            var result = clientOrderValidation.TryValidate(trx, clientOrder, out message);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(message, $"{clientOrder.Symbol} {clientOrder.Type} order not valid: Stop Price {clientOrder.StopPrice} cannot be below the minimum {trx.Price.Minimum};Stop Price {clientOrder.StopPrice} doesn't meet the tick size {trx.Price.Increment}");
        }

        [TestMethod]
        public void StopPriceValidation_Failed_MaxPrice()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = trxStats.LastPrice, StopPrice = 15000000.00000000M };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder()
                .AddPriceValidation()
                .AddStopPriceValidation()
                .Build();

            var result = clientOrderValidation.TryValidate(trx, clientOrder, out message);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(message, $"{clientOrder.Symbol} {clientOrder.Type} order not valid: Stop Price {clientOrder.StopPrice} cannot be above the maximum {trx.Price.Maximum}");
        }

        [TestMethod]
        public void StopPriceValidation_Pass()
        {
            // Arrange
            string message;
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = trxStats.LastPrice, StopPrice = (trxStats.LastPrice + (100 * trx.Price.Increment)) };

            // Act
            var clientOrderValidation = new ClientOrderValidationBuilder()
                .AddPriceValidation()
                .AddStopPriceValidation()
                .Build();

            var result = clientOrderValidation.TryValidate(trx, clientOrder, out message);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(message, string.Empty);
        }

        [TestMethod]
        [ExpectedException(typeof(OrderValidationException))]
        public void Limit_Failed_NoPrice()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M };

            // Act
            trx.ValidateClientOrder(clientOrder);

            // Assert
            // Expected OrderValidationException
        }

        [TestMethod]
        public void Limit_Pass()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.Limit, Quantity = 500.00000000M, Price = trxStats.BidPrice };

            // Act
            trx.ValidateClientOrder(clientOrder);

            // Assert
            // Expected no exception
        }

        [TestMethod]
        [ExpectedException(typeof(OrderValidationException))]
        public void LimitMaker_Failed_NoPrice()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.LimitMaker, Quantity = 500.00000000M };

            // Act
            trx.ValidateClientOrder(clientOrder);

            // Assert
            // Expected OrderValidationException
        }

        [TestMethod]
        public void LimitMaker_Pass()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "TRXBTC", Type = OrderType.LimitMaker, Quantity = 500.00000000M, Price = trxStats.BidPrice };

            // Act
            trx.ValidateClientOrder(clientOrder);

            // Assert
            // Expected no exception
        }

        [TestMethod]
        [ExpectedException(typeof(OrderValidationException))]
        public void StopLossLimit_Failed_NoPrice_NoStopPrice()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.StopLossLimit, Quantity = 500.00000000M };

            // Act
            try
            {
                eth.ValidateClientOrder(clientOrder);
            }
            catch (OrderValidationException e)
            {
                // Assert
                Assert.IsTrue(e.Message.Equals($"ETHBTC {clientOrder.Type.GetOrderTypeName()} order not valid: Notional 0 is less than the minimum notional {eth.NotionalMinimumValue};Price 0 cannot be below the minimum {eth.Price.Minimum};Stop Price 0 cannot be below the minimum {eth.Price.Minimum}"));
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(OrderValidationException))]
        public void StopLossLimit_Failed_NoStopPrice()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.StopLossLimit, Quantity = 500.00000000M, Price = ethStats.BidPrice };

            // Act
            try
            {
                eth.ValidateClientOrder(clientOrder);
            }
            catch (OrderValidationException e)
            {
                // Assert
                Assert.IsTrue(e.Message.Equals($"ETHBTC {clientOrder.Type.GetOrderTypeName()} order not valid: Stop Price 0 cannot be below the minimum {eth.Price.Minimum}"));
                throw;
            }
        }

        [TestMethod]
        public void StopLossLimit_Pass()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.StopLossLimit, Quantity = 500.00000000M, Price = ethStats.BidPrice, StopPrice = ethStats.BidPrice };

            // Act
            eth.ValidateClientOrder(clientOrder);

            // Assert
            // Expected no exception
        }

        [TestMethod]
        [ExpectedException(typeof(OrderValidationException))]
        public void TakeProfitLimit_Failed_NoPrice_NoStopPrice()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.TakeProfitLimit, Quantity = 500.00000000M };

            // Act
            try
            {
                eth.ValidateClientOrder(clientOrder);
            }
            catch (OrderValidationException e)
            {
                // Assert
                Assert.IsTrue(e.Message.Equals($"ETHBTC {clientOrder.Type.GetOrderTypeName()} order not valid: Notional 0 is less than the minimum notional {eth.NotionalMinimumValue};Price 0 cannot be below the minimum {eth.Price.Minimum};Stop Price 0 cannot be below the minimum {eth.Price.Minimum}"));
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(OrderValidationException))]
        public void TakeProfitLimit_Failed_NoStopPrice()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.TakeProfitLimit, Quantity = 500.00000000M, Price = ethStats.BidPrice };

            // Act
            try
            {
                eth.ValidateClientOrder(clientOrder);
            }
            catch (OrderValidationException e)
            {
                // Assert
                Assert.IsTrue(e.Message.Equals($"ETHBTC {clientOrder.Type.GetOrderTypeName()} order not valid: Stop Price 0 cannot be below the minimum {eth.Price.Minimum}"));
                throw;
            }
        }

        [TestMethod]
        public void TakeProfitLimit_Pass()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.TakeProfitLimit, Quantity = 500.00000000M, Price = ethStats.BidPrice, StopPrice = ethStats.BidPrice };

            // Act
            eth.ValidateClientOrder(clientOrder);

            // Assert
            // Expected no exception
        }

        [TestMethod]
        [ExpectedException(typeof(OrderValidationException))]
        public void Market_Fail__NoQuantity()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.Market, Price = ethStats.LastPrice };

            // Act
            try
            {
                eth.ValidateClientOrder(clientOrder);
            }
            catch (OrderValidationException e)
            {
                // Assert
                Assert.IsTrue(e.Message.Equals($"ETHBTC {clientOrder.Type.GetOrderTypeName()} order not valid: Quantity 0 is below the minimum {eth.Quantity.Minimum};Notional {clientOrder.Price * clientOrder.Quantity} is less than the minimum notional {eth.NotionalMinimumValue}"));
                throw;
            }
        }

        [TestMethod]
        public void Market_Pass()
        {
            // Arrange
            var clientOrder = new ClientOrder() { Symbol = "ETHBTC", Type = OrderType.Market, Quantity = 500, Price = ethStats.LastPrice };

            // Act
            eth.ValidateClientOrder(clientOrder);

            // Assert
            // Expected no exception
        }
    }
}
