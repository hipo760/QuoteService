using System;
using System.IO;
using System.Reactive.Linq;

namespace QuoteData
{
    public static class ExtensionMethods
    {
        public static IObservable<IGroupedObservable<long, TSource>> WindowByTimestamp<TSource>(this IObservable<TSource> source,
            Func<TSource, long> timestampTicksSelector,
            TimeSpan windowDuration)
        {
            long durationTicks = windowDuration.Ticks;

            return source.Publish(ps =>
                ps.GroupByUntil(
                    x => timestampTicksSelector(x) / durationTicks,
                    g => ps.Where(x => timestampTicksSelector(x) / durationTicks != g.Key)));
        }
    }
}