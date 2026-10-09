using System;

namespace DesktopRacerHelper;

internal sealed record MarketPriceQuote(string CacheKey, string MarketHashName, bool Available, string LowestPrice, string MedianPrice, string Volume, DateTime FetchedAt, string Status);
