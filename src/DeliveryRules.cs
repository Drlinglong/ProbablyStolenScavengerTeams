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
        public int soldMask; // Compatibility name: entries already bought OR discarded by native overflow.
        public bool closed = true;
        public bool firstOffer;
    }

    // Pure policy: no Unity, file IO, UI or global state.
    public static class DeliveryRules
    {
        public const int Interval = 3;
        public const int CrateContents = 6;
        public static int Cost(int level) => level == 1 ? 200 : level == 2 ? 300
            : level == 3 ? 450 : throw new ArgumentOutOfRangeException(nameof(level));
        public static int MaterialCount(int level) => level == 1 ? 6 : level == 2 ? 2
            : level == 3 ? 3 : throw new ArgumentOutOfRangeException(nameof(level));
        public static int JunkCount(int level) => checked(level * 2);
        public static int StockCount(int level) => MaterialCount(level) + JunkCount(level);
        public static void Unlock(DeliveryState state, int day)
        {
            if (state.nextDay < 0) state.nextDay = checked(day + 1);
        }
        public static bool CanQueue(DeliveryState state, int day, int level)
            => level > 0 && ((state.offerDay == day && !state.closed)
                || (state.nextDay >= 0 && day >= state.nextDay));
        public static void Arrive(DeliveryState state, int day, int level)
        {
            if (state.offerDay == day && !state.closed) return; // Restore this batch.
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
        public static void Sold(DeliveryState state, int index)
            => MarkUnavailable(state, index);
        public static void Discard(DeliveryState state, int index)
            => MarkUnavailable(state, index);
        private static void MarkUnavailable(DeliveryState state, int index)
        {
            if (index < 0 || index >= StockCount(state.offerLevel))
                throw new ArgumentOutOfRangeException(nameof(index));
            state.soldMask |= 1 << index;
        }
        public static bool Valid(DeliveryState state)
        {
            if (state == null || state.version != 1 || state.nextDay < -1 || state.offerDay < -1)
                return false;
            if (state.offerDay == -1)
                return state.closed && state.offerLevel == 0 && state.soldMask == 0;
            if (state.offerLevel < 1 || state.offerLevel > 3) return false;
            return state.soldMask >= 0 && state.soldMask < (1 << StockCount(state.offerLevel));
        }
    }
}
