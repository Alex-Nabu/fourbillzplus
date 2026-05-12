# fourbillzplus

A cTrader cBot for XAUUSD low-ATR range scalping.

## Bot

`XauUsdLowAtrRangeScalper` trades only on `XAUUSD`. It looks for quiet ranging conditions using ATR, recent range width, and SMA slope, then enters mean-reversion trades around the SMA. It includes:

- Long/short/both trade bias
- Max open trade limit
- Trade cooldown
- Bot-level max dollar loss guard
- Initial dollar-based stop loss
- Dollar-based trailing stop
- Optional debug logging

## Files

- `src/XauUsdLowAtrRangeScalper.cs` - cBot source code
- `fourbillzplus.csproj` - lightweight C# project file for editor support

## cTrader Usage

1. Open cTrader Automate.
2. Create a new cBot named `XauUsdLowAtrRangeScalper`.
3. Replace the generated code with `src/XauUsdLowAtrRangeScalper.cs`.
4. Build in cTrader.
5. Attach the bot only to an `XAUUSD` chart.

## Risk Note

This bot opens live market orders. Backtest and forward-test on demo before using real funds.
