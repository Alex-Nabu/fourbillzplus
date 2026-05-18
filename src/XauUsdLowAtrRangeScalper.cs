using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum TradeBias
    {
        Both,
        LongOnly,
        ShortOnly
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdLowAtrRangeScalper : Robot
    {
        [Parameter("Bias", DefaultValue = TradeBias.Both)]
        public TradeBias Bias { get; set; }

        [Parameter("Enable Debug Logs", DefaultValue = true)]
        public bool EnableLogs { get; set; }

        [Parameter("Volume Lots", DefaultValue = 0.01)]
        public double VolumeLots { get; set; }

        [Parameter("Stop Loss Dollars", DefaultValue = 20.0)]
        public double StopLossDollars { get; set; }

        [Parameter("Start Trailing After $ Move", DefaultValue = 2.0)]
        public double TrailStartDollars { get; set; }

        [Parameter("Trailing Distance $", DefaultValue = 1.0)]
        public double TrailDistanceDollars { get; set; }

        [Parameter("Max Open Trades", DefaultValue = 1)]
        public int MaxOpenTrades { get; set; }

        [Parameter("Min Minutes Between Trades", DefaultValue = 10)]
        public int MinMinutesBetweenTrades { get; set; }

        [Parameter("Max Bot Dollar Loss", DefaultValue = 50.0)]
        public double MaxDollarLoss { get; set; }

        [Parameter("ATR Timeframe", DefaultValue = "Minute15")]
        public TimeFrame AtrTimeFrame { get; set; }

        [Parameter("ATR Period", DefaultValue = 14)]
        public int AtrPeriod { get; set; }

        [Parameter("Max ATR Dollars", DefaultValue = 2.5)]
        public double MaxAtrDollars { get; set; }

        [Parameter("Range Lookback Bars", DefaultValue = 20)]
        public int RangeLookbackBars { get; set; }

        [Parameter("Max Range Dollars", DefaultValue = 8.0)]
        public double MaxRangeDollars { get; set; }

        [Parameter("SMA Period", DefaultValue = 20)]
        public int SmaPeriod { get; set; }

        [Parameter("Entry Deviation ATR", DefaultValue = 0.35)]
        public double EntryDeviationAtr { get; set; }

        private const string Label = "XAUUSD_LOW_ATR_RANGE_SCALPER";

        private Bars _m15Bars;
        private AverageTrueRange _atr;
        private SimpleMovingAverage _sma;

        private DateTime _lastTradeTime = DateTime.MinValue;
        private double _realizedBotProfit;

        protected override void OnStart()
        {
            _m15Bars = MarketData.GetBars(AtrTimeFrame);
            _atr = Indicators.AverageTrueRange(_m15Bars, AtrPeriod, MovingAverageType.Exponential);
            _sma = Indicators.SimpleMovingAverage(_m15Bars.ClosePrices, SmaPeriod);

            Positions.Closed += OnPositionClosed;
        }

        protected override void OnTick()
        {
            ManageTrailingStops();

            Log("-----------------------------------");
            Log("Bot Tick Running");

            if (SymbolName != "XAUUSD")
            {
                Log("Wrong symbol. Bot only runs on XAUUSD.");
                return;
            }

            if (MaxLossHit())
            {
                Log("MAX LOSS HIT. Closing all bot positions.");
                CloseBotPositions();
                return;
            }

            var botPositions = Positions.FindAll(Label, SymbolName);

            Log("Open Positions: " + botPositions.Length);

            if (Server.Time < _lastTradeTime.AddMinutes(MinMinutesBetweenTrades))
            {
                var remaining = (_lastTradeTime.AddMinutes(MinMinutesBetweenTrades) - Server.Time).TotalMinutes;

                Log("Trade cooldown active. Minutes remaining: " + remaining.ToString("0.00"));
                return;
            }

            if (botPositions.Length >= MaxOpenTrades)
            {
                Log("Max open trades reached.");
                return;
            }

            if (_m15Bars.Count < Math.Max(RangeLookbackBars, SmaPeriod))
            {
                Log("Not enough bars loaded yet.");
                return;
            }

            double atrDollars = _atr.Result.LastValue;

            Log("ATR Value: " + atrDollars.ToString("0.00"));

            if (atrDollars > MaxAtrDollars)
            {
                Log("ATR too high. Market too volatile.");
                return;
            }

            bool ranging = IsRanging(out double rangeDollars);

            Log("Is Ranging: " + ranging);
            Log("Range Value: " + rangeDollars.ToString("0.00"));

            if (!ranging)
            {
                Log("Range too wide. No entry.");
                return;
            }

            double sma = _sma.Result.LastValue;
            double atr = _atr.Result.LastValue;
            double deviation = atr * EntryDeviationAtr;

            double bid = Symbol.Bid;
            double ask = Symbol.Ask;

            Log("Bid: " + bid.ToString("0.00"));
            Log("Ask: " + ask.ToString("0.00"));
            Log("SMA: " + sma.ToString("0.00"));
            Log("Deviation: " + deviation.ToString("0.00"));

            double longSignalPrice = sma - deviation;
            double shortSignalPrice = sma + deviation;
            double dollarsToLongSignal = Math.Max(0, bid - longSignalPrice);
            double dollarsToShortSignal = Math.Max(0, shortSignalPrice - ask);

            bool longSignal = bid <= longSignalPrice;
            bool shortSignal = ask >= shortSignalPrice;

            Log("Long Signal: " + longSignal);
            Log("Short Signal: " + shortSignal);
            Log("Long Signal Price: " + longSignalPrice.ToString("0.00"));
            Log("Short Signal Price: " + shortSignalPrice.ToString("0.00"));
            Log("Dollars To Long Signal: " + dollarsToLongSignal.ToString("0.00"));
            Log("Dollars To Short Signal: " + dollarsToShortSignal.ToString("0.00"));

            if (longSignal && Bias != TradeBias.ShortOnly)
            {
                Log("LONG ENTRY SIGNAL DETECTED");
                Enter(TradeType.Buy);
                return;
            }

            if (shortSignal && Bias != TradeBias.LongOnly)
            {
                Log("SHORT ENTRY SIGNAL DETECTED");
                Enter(TradeType.Sell);
                return;
            }

            Log("No valid entry signal.");
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnPositionClosed;
        }

        private void Log(string message)
        {
            if (EnableLogs)
                Print("[{0}] {1}", Server.Time, message);
        }

        private void ManageTrailingStops()
        {
            foreach (var position in Positions.FindAll(Label, SymbolName))
            {
                if (position.TradeType == TradeType.Buy)
                {
                    double moveInDollars = Symbol.Bid - position.EntryPrice;

                    Log("BUY Position Profit Move: " + moveInDollars.ToString("0.00"));

                    if (moveInDollars < TrailStartDollars)
                    {
                        Log("BUY Trail not active yet.");
                        continue;
                    }

                    double newStop = Symbol.Bid - TrailDistanceDollars;

                    if (position.StopLoss == null || newStop > position.StopLoss)
                    {
                        Log("Updating BUY trailing stop to: " + newStop.ToString("0.00"));
                        ModifyPosition(position, newStop, null);
                    }
                }
                else
                {
                    double moveInDollars = position.EntryPrice - Symbol.Ask;

                    Log("SELL Position Profit Move: " + moveInDollars.ToString("0.00"));

                    if (moveInDollars < TrailStartDollars)
                    {
                        Log("SELL Trail not active yet.");
                        continue;
                    }

                    double newStop = Symbol.Ask + TrailDistanceDollars;

                    if (position.StopLoss == null || newStop < position.StopLoss)
                    {
                        Log("Updating SELL trailing stop to: " + newStop.ToString("0.00"));
                        ModifyPosition(position, newStop, null);
                    }
                }
            }
        }

        private bool IsRanging(out double rangeDollars)
        {
            int last = _m15Bars.Count - 1;

            double highest = double.MinValue;
            double lowest = double.MaxValue;

            for (int i = last - RangeLookbackBars + 1; i <= last; i++)
            {
                highest = Math.Max(highest, _m15Bars.HighPrices[i]);
                lowest = Math.Min(lowest, _m15Bars.LowPrices[i]);
            }

            rangeDollars = highest - lowest;

            return rangeDollars <= MaxRangeDollars;
        }

        private void Enter(TradeType tradeType)
        {
            Log("Preparing order...");

            double volume = Symbol.QuantityToVolumeInUnits(VolumeLots);
            volume = Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);

            Log("Normalized Volume: " + volume);

            var result = ExecuteMarketOrder(
                tradeType,
                SymbolName,
                volume,
                Label,
                null,
                null
            );

            if (!result.IsSuccessful)
            {
                Log("ORDER FAILED");
                Log("Error: " + result.Error);
                return;
            }

            Log("ORDER SUCCESS");
            Log("Trade Type: " + tradeType);
            Log("Entry Price: " + result.Position.EntryPrice);

            double stopLossPrice;

            if (tradeType == TradeType.Buy)
                stopLossPrice = result.Position.EntryPrice - StopLossDollars;
            else
                stopLossPrice = result.Position.EntryPrice + StopLossDollars;

            ModifyPosition(result.Position, stopLossPrice, null);

            Log("Stop Loss Set At: " + stopLossPrice.ToString("0.00"));

            _lastTradeTime = Server.Time;
        }

        private bool MaxLossHit()
        {
            double floatingProfit = Positions
                .FindAll(Label, SymbolName)
                .Sum(p => p.NetProfit);

            double totalBotProfit = _realizedBotProfit + floatingProfit;

            return totalBotProfit <= -Math.Abs(MaxDollarLoss);
        }

        private void CloseBotPositions()
        {
            foreach (var position in Positions.FindAll(Label, SymbolName))
                ClosePosition(position);
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == Label && args.Position.SymbolName == SymbolName)
                _realizedBotProfit += args.Position.NetProfit;
        }
    }
}
