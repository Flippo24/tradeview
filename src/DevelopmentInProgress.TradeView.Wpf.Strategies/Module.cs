﻿using DevelopmentInProgress.TradeView.Wpf.Common.Services;
using DevelopmentInProgress.TradeView.Wpf.Host.Controller.Module;
using DevelopmentInProgress.TradeView.Wpf.Host.Controller.Navigation;
using DevelopmentInProgress.TradeView.Wpf.Strategies.View;
using DevelopmentInProgress.TradeView.Wpf.Strategies.ViewModel;
using Prism.Ioc;
using Prism.Logging;
using System;

namespace DevelopmentInProgress.TradeView.Wpf.Strategies
{
    public class Module : ModuleBase
    {
        public const string ModuleName = "Strategies";

        private static string StrategyUser = $"Strategies";

        public Module(ModuleNavigator moduleNavigator, ILoggerFacade logger)
            : base(moduleNavigator, logger)
        {
        }

        public override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, StrategyRunnerView>(typeof(StrategyRunnerView).Name);
            containerRegistry.Register<StrategyRunnerViewModel>(typeof(StrategyRunnerViewModel).Name);
        }

        public async override void OnInitialized(IContainerProvider containerProvider)
        {
            var moduleSettings = new ModuleSettings
            {
                ModuleName = ModuleName,
                ModuleImagePath = @"/DevelopmentInProgress.TradeView.Wpf.Strategies;component/Images/strategyManager.png"
            };

            var moduleGroup = new ModuleGroup
            {
                ModuleGroupName = StrategyUser
            };

            var strategyService = containerProvider.Resolve<IStrategyService>();

            try
            {
                var userStrategies = await strategyService.GetStrategies().ConfigureAwait(true);

                foreach (var strategy in userStrategies)
                {
                    var strategyDocument = CreateStrategyModuleGroupItem(strategy.Name, strategy.Name);
                    moduleGroup.ModuleGroupItems.Add(strategyDocument);
                }

                moduleSettings.ModuleGroups.Add(moduleGroup);
                ModuleNavigator.AddModuleNavigation(moduleSettings);

                Logger.Log("Initialized DevelopmentInProgress.Wpf.Strategies", Category.Info, Priority.None);
            }
            catch (Exception ex)
            {
                Logger.Log($"Initialize DevelopmentInProgress.Wpf.Strategies failed to load: {ex.ToString()}", Category.Info, Priority.None);
            }
        }

        private static ModuleGroupItem CreateStrategyModuleGroupItem(string name, string title)
        {
            var strategyDocument = new ModuleGroupItem
            {
                ModuleGroupItemName = name,
                TargetView = typeof(StrategyRunnerView).Name,
                TargetViewTitle = title,
                ModuleGroupItemImagePath = @"/DevelopmentInProgress.TradeView.Wpf.Strategies;component/Images/strategy.png"
            };

            return strategyDocument;
        }
    }
}
