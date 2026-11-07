using System;

namespace ScavengerTeams
{
    [Serializable]
    public sealed class DeliveryState
    {
        public int version = 1;
        public int nextDay = -1;
        public int offerDay = -1;
        public int offerLevel;
        public int greetedLevel;
        public int offerGreetingLevel;
        public int soldMask; // Compatibility name: entries already bought OR discarded by native overflow.
        public bool closed = true;
        public bool firstOffer;
        public bool paused;
        public int resumeAvailableDay; // Missing fields in existing v1 saves default to active/zero.
    }

    // Pure policy: no Unity, file IO, UI or global state.
    public static class DeliveryRules
    {
        public const int Interval = 3;
        public const int CrateContents = 6;
        public static int Cost(int level) => level == 1 ? 200 : level == 2 ? 300
            : level == 3 ? 450 : throw new ArgumentOutOfRangeException(nameof(level));
        public static int MaterialCount(int level, bool looseOnly = false)
        {
            if (level < 1 || level > 3) throw new ArgumentOutOfRangeException(nameof(level));
            return looseOnly ? checked(level * 6) : level == 1 ? 6 : level;
        }
        public static int JunkCount(int level, bool looseOnly = false) => looseOnly ? 0 : checked(level * 2);
        public static int StockCount(int level, bool looseOnly = false) => MaterialCount(level, looseOnly) + JunkCount(level, looseOnly);
        public static void Unlock(DeliveryState state, int day)
        {
            if (state.nextDay < 0) state.nextDay = checked(day + 1);
        }
        public static bool CanQueue(DeliveryState state, int day, int level)
            => !state.paused && level > 0 && ((state.offerDay == day && !state.closed)
                || (state.nextDay >= 0 && day >= state.nextDay));
        public static bool Pause(DeliveryState state, int day)
        {
            if (state.paused) return false;
            var available = checked(day + Interval);
            state.paused = true;
            state.resumeAvailableDay = available;
            return true;
        }
        public static bool Resume(DeliveryState state, int day)
        {
            if (!state.paused || day < state.resumeAvailableDay) return false;
            var due = checked(day + 1);
            state.paused = false;
            state.resumeAvailableDay = 0;
            state.nextDay = due;
            // A resumed route must not restore a same-day, already dismissed batch.
            state.closed = true;
            return true;
        }
        public static void Arrive(DeliveryState state, int day, int level)
        {
            if (state.offerDay == day && !state.closed) return; // Restore this batch.
            // Previous offerLevel also seeds old v1 saves that predate greeting fields.
            var previousLevel = Math.Max(state.greetedLevel, state.offerLevel);
            state.offerGreetingLevel = level > previousLevel ? level : 0;
            state.greetedLevel = Math.Max(previousLevel, level);
            state.firstOffer = state.offerDay < 0;
            state.offerDay = day;
            state.offerLevel = level;
            state.soldMask = 0;
            state.closed = false;
            state.nextDay = checked(day + Interval);
        }
        public static bool IsSold(DeliveryState state, int index)
            => (state.soldMask & (1 << index)) != 0;
        public static int WholesaleDiscount(DeliveryState state) => state.firstOffer ? -50 : -15;
        public static void Sold(DeliveryState state, int index, bool looseOnly = false)
            => MarkUnavailable(state, index, looseOnly);
        public static void Discard(DeliveryState state, int index, bool looseOnly = false)
            => MarkUnavailable(state, index, looseOnly);
        private static void MarkUnavailable(DeliveryState state, int index, bool looseOnly)
        {
            if (index < 0 || index >= StockCount(state.offerLevel, looseOnly))
                throw new ArgumentOutOfRangeException(nameof(index));
            state.soldMask |= 1 << index;
        }
        public static bool Valid(DeliveryState state, bool looseOnly = false)
        {
            if (state == null || state.version != 1 || state.nextDay < -1 || state.offerDay < -1
                || state.resumeAvailableDay < 0)
                return false;
            if (state.greetedLevel < 0 || state.greetedLevel > 3
                || state.offerGreetingLevel < 0 || state.offerGreetingLevel > state.offerLevel)
                return false;
            if (state.offerDay == -1)
                return state.closed && state.offerLevel == 0 && state.soldMask == 0;
            if (state.offerLevel < 1 || state.offerLevel > 3) return false;
            return state.soldMask >= 0 && state.soldMask < (1 << StockCount(state.offerLevel, looseOnly));
        }
    }
}
