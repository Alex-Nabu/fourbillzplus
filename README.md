# fourbillzplus

A Bot for XAUUSD low-ATR range scalping.

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

- `src/XauUsdLowAtrRangeScalper.cs` - source code
- `fourbillzplus.csproj` - lightweight C# project file for editor support
